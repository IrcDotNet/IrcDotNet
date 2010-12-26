using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace IrcDotNet.Common.Collections
{
    /// <summary>
    /// Represents a read-only set of values.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
#if !SILVERLIGHT
    [Serializable()]
#endif
    [DebuggerDisplay("Count = {Count}")]
    public class ReadOnlySet<T> : ISet<T>
#if !SILVERLIGHT
        , ISerializable, IDeserializationCallback
#endif
    {
        private ISet<T> set;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReadOnlySet{T}"/> class.
        /// </summary>
        /// <param name="set">The set to wrap.</param>
        /// <exception cref="ArgumentNullException"><paramref name="set"/> is <see langword="null"/>.</exception>
        public ReadOnlySet(ISet<T> set)
        {
            if (set == null)
                throw new ArgumentNullException("set");

            this.set = set;
        }

        #region ISet<T> Members

        bool ISet<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        void ISet<T>.ExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        void ISet<T>.SymmetricExceptWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        void ISet<T>.IntersectWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        void ISet<T>.UnionWith(IEnumerable<T> other)
        {
            throw new NotSupportedException();
        }

        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            return this.set.IsProperSubsetOf(other);
        }

        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            return this.set.IsProperSupersetOf(other);
        }

        public bool IsSubsetOf(IEnumerable<T> other)
        {
            return this.set.IsSubsetOf(other);
        }

        public bool IsSupersetOf(IEnumerable<T> other)
        {
            return this.set.IsSupersetOf(other);
        }

        public bool Overlaps(IEnumerable<T> other)
        {
            return this.set.Overlaps(other);
        }

        public bool SetEquals(IEnumerable<T> other)
        {
            return this.set.SetEquals(other);
        }

        #endregion

        #region ICollection<T> Members

        public int Count
        {
            get { return this.set.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        void ICollection<T>.Add(T item)
        {
            throw new NotSupportedException();
        }

        bool ICollection<T>.Remove(T item)
        {
            throw new NotSupportedException();
        }

        void ICollection<T>.Clear()
        {
            throw new NotSupportedException();
        }

        public bool Contains(T item)
        {
            return this.set.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            this.set.CopyTo(array, arrayIndex);
        }

        #endregion

        #region IEnumerable<T> Members

        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this.set).GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable)this.set).GetEnumerator();
        }

        #endregion

#if !SILVERLIGHT

        #region ISerializable Members

        void ISerializable.GetObjectData(SerializationInfo info, StreamingContext context)
        {
            ((ISerializable)this.set).GetObjectData(info, context);
        }

        #endregion

        #region IDeserializationCallback Members

        void IDeserializationCallback.OnDeserialization(object sender)
        {
            ((IDeserializationCallback)this.set).OnDeserialization(sender);
        }

        #endregion

#endif
    }
}
