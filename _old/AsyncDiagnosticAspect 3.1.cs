using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using PostSharp.Aspects;
using PostSharp.Aspects.Advices;
using PostSharp.Extensibility;
using PostSharp.Reflection;
using PostSharp.Serialization;

namespace Nito.AsyncEx.PostSharp
{
    internal static class Util
    {
        public static string FormatMethodName(MethodBase method)
        {
            var sb = new StringBuilder();
            if (method.DeclaringType != null)
                sb.Append(method.DeclaringType.FullName.Replace('+', '.') + '.');
            sb.Append(method.Name);
            if (method.IsGenericMethod)
            {
                sb.Append("[");
                sb.Append(String.Join(",", method.GetGenericArguments().Select(x => x.Name)));
                sb.Append("]");
            }
            sb.Append("(");
            sb.Append(String.Join(", ", method.GetParameters().Select(x => x.ParameterType.Name + " " + x.Name)));
            sb.Append(")");
            return sb.ToString();
        }

        public static MethodBase FindAsyncMethod(MethodBase method)
        {
            Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "Examining " + method.Name);
            if (method.Name != "MoveNext")
                return null;
            Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "Searching for MoveNext");
            var stateMachineType = method.DeclaringType;
            if (stateMachineType == null || !stateMachineType.IsNested)
                return null;
            Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "State machine type is " + stateMachineType.ToString());
            var containingType = stateMachineType.DeclaringType;
            if (containingType == null)
                return null;
            var containingTypeMethods = containingType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            MethodBase ret = null;
            foreach (var containingTypeMethod in containingTypeMethods)
            {
                var stateMachineAttribute = containingTypeMethod.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(AsyncStateMachineAttribute));
                if (stateMachineAttribute == null)
                    continue;
                var stateMachineAttributeArgument = stateMachineAttribute.ConstructorArguments.FirstOrDefault().Value as Type;
                if (stateMachineAttributeArgument != stateMachineType)
                    continue;

                if (ret != null)
                {
                    Message.Write(global::PostSharp.MessageLocation.Of(containingTypeMethod), SeverityType.Warning, "13", "Cannot distinguish async state machines.");
                    Message.Write(global::PostSharp.MessageLocation.Of(ret), SeverityType.Warning, "13", "Cannot distinguish async state machines.");
                }
                else
                {
                    ret = containingTypeMethod;
                }
            }

            return ret;
        }
    }

    [Serializable]
    [MulticastAttributeUsage(MulticastTargets.Method, TargetTypeAttributes = MulticastAttributes.AnyVisibility | MulticastAttributes.AnyGeneration, TargetMemberAttributes = MulticastAttributes.AnyVisibility | MulticastAttributes.AnyScope | MulticastAttributes.NonAbstract | MulticastAttributes.AnyVirtuality | MulticastAttributes.Managed | MulticastAttributes.AnyGeneration)]
    public class AsyncDiagnosticAspect : MethodInterceptionAspect
    {
        private LocationInfo _state;
        private string _methodName;

        public override bool CompileTimeValidate(MethodBase method)
        {
            // Do not include any methods that are compiler generated (or in compiler generated types), except for the MoveNext of an async state machine.
            if (method.IsConstructor)
                return false;
            if (Util.FindAsyncMethod(method) != null)
                return true;
            if (method.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(CompilerGeneratedAttribute) || x.AttributeType == typeof(AsyncStateMachineAttribute)) != null)
            {
                Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "- Rejecting due to compiler generation / async state machine.");
                return false;
            }
            if (method.DeclaringType != null && method.DeclaringType.CustomAttributes.FirstOrDefault(x => x.AttributeType == typeof(CompilerGeneratedAttribute)) != null)
            {
                Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "- Rejecting due to compiler generation / async state machine.");
                return false;
            }
            Message.Write(global::PostSharp.MessageLocation.Of(method), SeverityType.Info, "0", "+ Accepting.");
            return true;
        }

        public override void CompileTimeInitialize(MethodBase method, AspectInfo aspectInfo)
        {
            // If we are a MoveNext inside a nested state machine, then pull the method name from the related async method.
            var asyncMethod = Util.FindAsyncMethod(method);
            _methodName = Util.FormatMethodName(asyncMethod ?? method);
            if (asyncMethod != null)
            {
                _state = new LocationInfo(method.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Single(x => x.Name.Contains("state"))); // todo: change exception to error
            }
        }

        public override sealed void OnInvoke(MethodInterceptionArgs args)
        {
            if (_state == null)
                AsyncDiagnosticStack.Push(_methodName);
            else if ((int)_state.GetValue(args.Instance) == -1)
                AsyncDiagnosticStack.Push("async " + _methodName);
            try
            {
                base.OnInvoke(args);
            }
            finally
            {
                if (_state == null || (int)_state.GetValue(args.Instance) == -2)
                    AsyncDiagnosticStack.Pop();
            }
        }
    }
}
