using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using PostSharp.Aspects;

namespace Nito.AsyncEx.AsyncDiagnostics
{
    /// <summary>
    /// An aspect that applies an async diagnostic stack to both synchronous and asynchronous methods.
    /// </summary>
    [Serializable]
    public class AsyncDiagnosticAspect : OnMethodBoundaryAspect
    {
        /// <summary>
        /// The method name for this aspect instance, calculated at build time.
        /// </summary>
        private string _methodName;

        /// <summary>
        /// Creates an aspect that applies an async diagnostic stack to both synchronous and asynchronous methods.
        /// </summary>
        public AsyncDiagnosticAspect()
        {
            ApplyToStateMachine = false;
        }

        /// <summary>
        /// Calculates and saves the method name that will later be pushed on to the diagnostic stack.
        /// </summary>
        public override void CompileTimeInitialize(MethodBase method, AspectInfo aspectInfo)
        {
            base.CompileTimeInitialize(method, aspectInfo);
            _methodName = MethodName(method);
        }

        /// <summary>
        /// Pushes the method name onto the asynchronous diagnostic stack.
        /// </summary>
        public override void OnEntry(MethodExecutionArgs args)
        {
            AsyncDiagnosticStack.Push(_methodName);
        }

        /// <summary>
        /// Pops the method name off the asynchronous diagnostic stack.
        /// </summary>
        public override void OnExit(MethodExecutionArgs args)
        {
            AsyncDiagnosticStack.Pop();
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
            if (method.CustomAttributes.Any(x => x.AttributeType == typeof(AsyncStateMachineAttribute)))
                sb.Append(" // async");

            return sb.ToString();
        }
    }
}
