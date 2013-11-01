using System.Collections.Generic;
using System.Collections.Immutable;

namespace Nito.AsyncEx.AsyncDiagnostics.Internal
{
    /// <summary>
    /// An async-local stack.
    /// </summary>
    /// <typeparam name="T">The type of values on the stack.</typeparam>
    internal sealed class AsyncLocalStack<T> : AsyncLocal<ImmutableStack<T>>, IEnumerable<T>
    {
        /// <summary>
        /// Creates a new async-local stack.
        /// </summary>
        public AsyncLocalStack()
            : base(ImmutableStack.Create<T>())
        {
        }

        /// <summary>
        /// Removes all values from the stack.
        /// </summary>
        public void Clear()
        {
            Value = Value.Clear();
        }

        /// <summary>
        /// Returns a value indicating whether the stack is empty.
        /// </summary>
        public bool IsEmpty
        {
            get { return Value.IsEmpty; }
        }

        /// <summary>
        /// Returns the value at the top of the stack without modifying the stack.
        /// </summary>
        public T Peek()
        {
            return Value.Peek();
        }

        /// <summary>
        /// Pops the value off the top of the stack and returns it.
        /// </summary>
        public T Pop()
        {
            T ret;
            Value = Value.Pop(out ret);
            return ret;
        }

        /// <summary>
        /// Pops the value off the top of the stack and discards it.
        /// </summary>
        public void PopDiscardingValue()
        {
            Value = Value.Pop();
        }

        /// <summary>
        /// Pushes a value on the top of the stack.
        /// </summary>
        /// <param name="value">The value to push.</param>
        public void Push(T value)
        {
            Value = Value.Push(value);
        }

        /// <summary>
        /// Enumerates all values in the stack.
        /// </summary>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)Value).GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}