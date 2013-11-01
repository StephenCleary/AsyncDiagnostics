using System;
using System.Text;

namespace Nito.AsyncEx.AsyncDiagnostics
{
    /// <summary>
    /// Extension methods for the <see cref="Exception"/> type.
    /// </summary>
    public static class ExceptionExtensions
    {
        /// <summary>
        /// Returns <see cref="Exception.ToString"/> concatenated with the <see cref="AsyncDiagnosticStack"/> for this exception and any inner exceptions. This can be used as a replacement for <see cref="Exception.ToString"/>.
        /// </summary>
        public static string ToAsyncDiagnosticString(this Exception exception)
        {
            var ret = exception.ToString();
            if (ret.EndsWith("\n"))
                return ret + exception.NestedAsyncDiagnosticStacks();
            return ret + Environment.NewLine + exception.NestedAsyncDiagnosticStacks();
        }

        /// <summary>
        /// Returns the async diagnostic stack attached to this <see cref="Exception"/>, or <see cref="string.Empty"/> if there is no async diagnostic stack. This does not return any details for inner exceptions.
        /// </summary>
        public static string AsyncDiagnosticStack(this Exception exception)
        {
            return exception.Data[AsyncDiagnostics.AsyncDiagnosticStack.DataKey] as string ?? string.Empty;
        }

        /// <summary>
        /// Returns the logical stack for this exception, including any async diagnostic stacks of inner exceptions as well.
        /// </summary>
        private static string NestedAsyncDiagnosticStacks(this Exception exception)
        {
            var sb = new StringBuilder();
            var asyncStack = exception.AsyncDiagnosticStack();
            if (asyncStack != string.Empty)
            {
                sb.AppendLine("Logical stack:");
                sb.Append(asyncStack);
            }
            else
            {
                sb.AppendLine("No logical stack.");
            }

            var aggregate = exception as AggregateException;
            if (aggregate != null)
            {
                var inner = aggregate.InnerExceptions;
                for (int i = 0; i != inner.Count; ++i)
                {
                    sb.AppendLine("--> (Inner exception #" + i + ")");
                    sb.Append(AsyncDiagnosticStack(inner[i]));
                    sb.AppendLine("<--");
                }
            }
            else if (exception.InnerException != null)
            {
                sb.AppendLine("--> (Inner exception)");
                sb.Append(AsyncDiagnosticStack(exception.InnerException));
                sb.AppendLine("<--");
            }

            return sb.ToString();
        }
    }
}
