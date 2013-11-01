using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using PostSharp;
using PostSharp.Aspects;
using PostSharp.Extensibility;
using PostSharp.Reflection;

namespace Nito.AsyncEx.AsyncDiagnostics
{
    /// <summary>
    /// An aspect that applies an async diagnostic stack to both synchronous and asynchronous methods.
    /// </summary>
    [Serializable]
    [AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Struct)]
    public class AsyncDiagnosticAspect : MethodLevelAspect, IAspectProvider
    {
        /// <summary>
        /// Applies aspects to async or synchronous methods.
        /// </summary>
        /// <param name="targetElement">The method to examine.</param>
        public IEnumerable<AspectInstance> ProvideAspects(object targetElement)
        {
            var method = (MethodBase)targetElement;

            // Ignore all compiler generated methods.
            if (method.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(CompilerGeneratedAttribute)) != null)
                yield break;

            // Ignore all methods on compiler generated types.
            if (method.DeclaringType != null && method.DeclaringType.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(CompilerGeneratedAttribute)) != null)
                yield break;

            // Get the AsyncStateMachineAttribute on this method (if async), or null if sync.
            var stateMachineAttribute = method.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(AsyncStateMachineAttribute));
            var methodName = MethodName(method);
            
            if (stateMachineAttribute == null)
            {
                // Apply SynchronousTracingAspect to all synchronous methods.
                yield return new AspectInstance(targetElement, new SynchronousTracingAspect(method, methodName));
            }
            else
            {
                // For async methods, apply AsynchronousTracingAspect to the async state machine's MoveNext method.
                var stateMachineType = stateMachineAttribute.ConstructorArguments.First().Value as Type;
                if (stateMachineType == null)
                {
                    Message.Write(MessageLocation.Of(targetElement), SeverityType.Warning, "1", "First argument to AsyncStateMachineAttribute constructor is not of type Type. No tracing will be added to method " + methodName + ".");
                    yield break;
                }
                var stateFields = stateMachineType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Where(x => x.Name.Contains("state")).ToArray();
                if (stateFields.Length != 1)
                {
                    if (stateFields.Length == 0)
                        Message.Write(MessageLocation.Of(targetElement), SeverityType.Warning, "2", "Could not find state field in async state machine. No tracing will be added to method " + methodName + ".");
                    else
                        Message.Write(MessageLocation.Of(targetElement), SeverityType.Warning, "3", "Found multiple state fields in async state machine. No tracing will be added to method " + methodName + ".");
                    yield break;
                }
                var stateField = stateFields[0];
                var moveNextMethod = stateMachineType.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (moveNextMethod == null)
                {
                    Message.Write(MessageLocation.Of(targetElement), SeverityType.Warning, "4", "Could not find MoveNext method in async state machine. No tracing will be added to method " + methodName + ".");
                }
                yield return new AspectInstance(moveNextMethod, new AsynchronousTracingAspect(method, stateField, "async " + methodName));
            }
        }

        /// <summary>
        /// Generates a human-readable, C#-ish string for a method, essentially the same as the formatting done by the built-in stack traces.
        /// </summary>
        /// <param name="method">The method to inspect.</param>
        private static string MethodName(MethodBase method)
        {
            var sb = new StringBuilder();
            if (method.DeclaringType != null)
                sb.Append(method.DeclaringType.FullName.Replace('+', '.') + '.');
            sb.Append(method.Name);
            if (method.IsGenericMethod)
            {
                sb.Append("[");
                sb.Append(string.Join(",", method.GetGenericArguments().Select(x => x.Name)));
                sb.Append("]");
            }
            sb.Append("(");
            sb.Append(string.Join(", ", method.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name)));
            sb.Append(")");
            return sb.ToString();
        }

        /// <summary>
        /// An apsect for tracing synchronous methods on an asynchronous diagnostic stack.
        /// </summary>
        [Serializable]
        public class SynchronousTracingAspect : OnMethodBoundaryAspect
        {
            /// <summary>
            /// The formatted name of this method.
            /// </summary>
            private string _methodName;

            private MethodBase _method;

            private readonly object _mutex;

            /// <summary>
            /// Creates a synchronous tracing aspect with the given method name.
            /// </summary>
            /// <param name="methodName">The formatted name of this method.</param>
            public SynchronousTracingAspect(MethodBase method, string methodName)
            {
                _methodName = methodName;
                _method = method;
                _mutex = new object();
            }

            /// <summary>
            /// Pushes the method name onto the asynchronous diagnostic stack.
            /// </summary>
            public override void OnEntry(MethodExecutionArgs args)
            {
                lock (_mutex)
                {
                    if (_method != null)
                    {
                        var frame = new StackTrace(1, true).GetFrames().FirstOrDefault(f => f.GetMethod() == _method);
                        if (frame != null)
                        {
                            var fileName = frame.GetFileName();
                            if (!string.IsNullOrWhiteSpace(fileName))
                            {
                                _methodName += " in " + fileName;
                                var line = frame.GetFileLineNumber();
                                if (line != 0)
                                    _methodName += ":line " + line;
                            }
                        }
                        _method = null;
                    }
                }

                AsyncDiagnosticStack.Push(_methodName);
            }

            /// <summary>
            /// Pops the method name off the asynchronous diagnostic stack.
            /// </summary>
            public override void OnExit(MethodExecutionArgs args)
            {
                AsyncDiagnosticStack.Pop();
            }
        }

        /// <summary>
        /// An apsect for tracing asynchronous methods on an asynchronous diagnostic stack.
        /// This aspect is applied to the <c>MoveNext</c> method of the asynchronous state machine.
        /// </summary>
        [Serializable]
        public class AsynchronousTracingAspect : MethodInterceptionAspect
        {
            /// <summary>
            /// The private <c>state</c> field of the asynchronous state machine.
            /// </summary>
            private readonly LocationInfo _state;

            /// <summary>
            /// The name of the original async method.
            /// </summary>
            private string _methodName;

            private MethodBase _method;

            private object _mutex;

            /// <summary>
            /// Creates an asynchronous tracing aspect with the specified <c>state</c> field and name of the original async method.
            /// </summary>
            /// <param name="state">The private <c>state</c> field of the asynchronous state machine.</param>
            /// <param name="methodName">The name of the original async method.</param>
            public AsynchronousTracingAspect(MethodBase method, FieldInfo state, string methodName)
            {
                _state = new LocationInfo(state);
                _methodName = methodName;
                _method = method;
                _mutex = new object();
            }

            /// <summary>
            /// Replaces the <c>MoveNext</c> method of the asynchronous state machine.
            /// </summary>
            /// <param name="args">An arguments object representing the <c>MoveNext</c> invocation.</param>
            public override void OnInvoke(MethodInterceptionArgs args)
            {
                lock (_mutex)
                {
                    if (_method != null)
                    {
                        var frame = new StackTrace(true).GetFrames().FirstOrDefault(f => f.GetMethod() == _method);
                        if (frame != null)
                        {
                            var fileName = frame.GetFileName();
                            if (!string.IsNullOrWhiteSpace(fileName))
                            {
                                _methodName += " in " + fileName;
                                var line = frame.GetFileLineNumber();
                                if (line != 0)
                                    _methodName += ":line " + line;
                            }
                        }
                        _method = null;
                    }
                }

                // Push the method name on the asynchronous diagnostic stack if the state machine is just starting.
                var oldValue = (int)_state.GetValue(args.Instance);
                if (oldValue == -1)
                    AsyncDiagnosticStack.Push(_methodName);

                // Invoke the original MoveNext.
                args.Proceed();

                // Pop the method name off the asynchronous diagnostic stack if the state machine is complete.
                var newValue = (int)_state.GetValue(args.Instance);
                if (newValue == -2)
                    AsyncDiagnosticStack.Pop();
            }
        }
    }
}
