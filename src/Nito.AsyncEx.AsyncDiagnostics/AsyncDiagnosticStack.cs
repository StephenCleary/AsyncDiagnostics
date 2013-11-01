using System;
using System.Collections.Generic;
using System.Text;
using Nito.AsyncEx.AsyncDiagnostics.Internal;

namespace Nito.AsyncEx.AsyncDiagnostics
{
    /// <summary>
    /// Provides an async-aware diagnostic stack.
    /// </summary>
    public static class AsyncDiagnosticStack
    {
        /// <summary>
        /// The underlying async-aware stack.
        /// </summary>
        private static readonly AsyncLocalStack<string> Stack;

        /// <summary>
        /// Initializes the async-aware diagnostic stack, including a domain-wide hook to place the stack on all raised exceptions.
        /// </summary>
        static AsyncDiagnosticStack()
        {
            Stack = new AsyncLocalStack<string>();
            AppDomain.CurrentDomain.FirstChanceException += (_, e) =>
            {
                if (e.Exception.Data.Contains(DataKey))
                    return;

                var sb = new StringBuilder();
                foreach (var context in Current)
                    sb.AppendLine("   " + context);
                var current = sb.ToString();
                if (current != string.Empty)
                    e.Exception.Data.Add(DataKey, current);
            };
        }

        /// <summary>
        /// The <see cref="Exception.Data"/> key under which the async diagnostic stack is added to each exception.
        /// </summary>
        public static string DataKey { get { return "Nito.AsyncEx.PostSharp.AsyncDiagnosticStack"; } }

        /// <summary>
        /// Gets the current async diagnostic stack.
        /// </summary>
        public static IEnumerable<string> Current
        {
            get
            {
                return Stack;
            }
        }

        /// <summary>
        /// Pushes a context value onto the async diagnostic stack.
        /// </summary>
        /// <param name="context">The context.</param>
        internal static void Push(string context)
        {
            Stack.Push(context);
        }

        /// <summary>
        /// Pops a context value off the async diagnostic stack.
        /// </summary>
        internal static void Pop()
        {
            Stack.PopDiscardingValue();
        }

        /// <summary>
        /// Pushes a context value onto the async diagnostic stack, and returns a disposable that pops the context value off the async diagnostic stack when disposed.
        /// </summary>
        /// <param name="context">The context.</param>
        /// <returns>A disposable that pops the context value off the async diagnostic stack when disposed.</returns>
        public static IDisposable Enter(string context)
        {
            Push(context);
            return new PopWhenDisposed();
        }

        /// <summary>
        /// A disposable that pops the context value off the async diagnostic stack when disposed.
        /// </summary>
        private sealed class PopWhenDisposed : IDisposable
        {
            /// <summary>
            /// Whether this disposable has already been disposed.
            /// </summary>
            private bool _disposed;

            /// <summary>
            /// Pops the context value off the async diagnostic stack.
            /// </summary>
            public void Dispose()
            {
                if (_disposed)
                    return;
                Pop();
                _disposed = true;
            }
        }
    }
}
