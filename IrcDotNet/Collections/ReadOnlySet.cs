using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;
using System.Text;

namespace IrcDotNet.Collections
{
    /// <summary>
    /// Represents a read-only set of values.
    /// </summary>
    /// <typeparam name="T">The type of elements in the set.</typeparam>
#if !SILVERLIGHT
    [Serializable()]
#endif
    [DebuggerDisplay("Count = {Count}")]
    public class ReadOnlySet<T> : ISet<T>, ICollection<T>, IEnumerable<T>, ICollection, IEnumerable
#if !SILVERLIGHT
        , ISerializable, IDeserializationCallback
#endif
    {
        // Set to expose as read-only.
        private ISet<T> set;

        private object syncRoot = new object();

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

        /// <summary>
        /// Determines whether the set is a proper subset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set is a proper subset of <paramref name="other"/>;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool IsProperSubsetOf(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.IsProperSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the set is a proper superset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set is a proper superset of <paramref name="other"/>;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool IsProperSupersetOf(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.IsProperSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the set is a subset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set is a subset of <paramref name="other"/>;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool IsSubsetOf(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.IsSubsetOf(other);
        }

        /// <summary>
        /// Determines whether the set is a superset of the specified collection.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set is a superset of <paramref name="other"/>;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool IsSupersetOf(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.IsSupersetOf(other);
        }

        /// <summary>
        /// Determines whether the set and the specified collection share common elements.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set and <paramref name="other"/> share at least one common element;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool Overlaps(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.Overlaps(other);
        }

        /// <summary>
        /// Determines whether the set and the specified collection contain the same elements.
        /// </summary>
        /// <param name="other">The collection to compare to the current set.</param>
        /// <returns>
        /// <see langword="true"/> if the set and <paramref name="other"/> are equal;
        /// <see langword="false"/>, otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/> is <see langword="null"/>.</exception>
        public bool SetEquals(IEnumerable<T> other)
        {
            if (other == null)
                throw new ArgumentNullException("other");

            return this.set.SetEquals(other);
        }

        #endregion

        #region ICollection<T> Members

        /// <summary>
        /// Gets the number of elements that are contained in the set.
        /// </summary>
        /// <value>The number of elements that are contained in the set.</value>
        public int Count
        {
            get { return this.set.Count; }
        }

        bool ICollection<T>.IsReadOnly
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

        /// <summary>
        /// Determines whether the set contains the specified element.
        /// </summary>
        /// <param name="item">The element to locate in the set.</param>
        /// <returns><see langword="true"/> if the set contains the specified element;
        /// <see langword="false"/>, otherwise.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> is <see langword="null"/>.</exception>
        public bool Contains(T item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            return this.set.Contains(item);
        }

        /// <inheritdoc cref="CopyTo(T[], int)"/>
        public void CopyTo(T[] array)
        {
            CopyTo(array, 0);
        }

        /// <summary>
        /// Copies the elements of the set to an array.
        /// </summary>
        /// <param name="array">The one-dimensional array that is the destination of the elements copied from the
        /// set. The array must have zero-based indexing.</param>
        /// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
        /// <exception cref="ArgumentNullException"><paramref name="array"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="arrayIndex"/> is less than 0.</exception>
        /// <exception cref="ArgumentException"><paramref name="arrayIndex"/> is greater than the length of the
        /// destination array.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException("item");
            
            this.set.CopyTo(array, arrayIndex);
        }

        #endregion

        #region IEnumerable<T> Members

        /// <summary>
        /// Returns an enumerator that iterates through the set.
        /// </summary>
        /// <returns>An enumerator for the set.</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)this.set).GetEnumerator();
        }

        #endregion

        #region ICollection Members

        void ICollection.CopyTo(Array array, int index)
        {
            this.set.CopyTo((T[])array, index);
        }

        bool ICollection.IsSynchronized
        {
            get { return true; }
        }

        object ICollection.SyncRoot
        {
            get { return this.syncRoot; }
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
