using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet.Common.Collections
{
    /// <summary>
    /// Contains common utilities for functionality relating to collections.
    /// </summary>
    public static class CollectionUtilities
    {
        /// <summary>
        /// Adds the specified items to the collection.
        /// </summary>
        /// <typeparam name="T">The type of the items in the collection.</typeparam>
        /// <param name="collection">The collection to which to add the items.</param>
        /// <param name="range">A collection of items to add to <paramref name="collection"/>.</param>
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (range == null)
                throw new ArgumentNullException("range");

            foreach (var item in range)
                collection.Add(item);
        }

        /// <summary>
        /// Removes the specified items from the collection.
        /// </summary>
        /// <typeparam name="T">The type of the items in the collection.</typeparam>
        /// <param name="collection">The collection fom which to remove the items.</param>
        /// <param name="range">A collection of items to remove from <paramref name="collection"/>.</param>
        public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (range == null)
                throw new ArgumentNullException("range");

            foreach (var item in range)
                collection.Remove(item);
        }

        /// <summary>
        /// Performs the specified action on each item in the collection.
        /// </summary>
        /// <typeparam name="T">The type of the items in the collection.</typeparam>
        /// <param name="source">The collection on whose items to perform the action.</param>
        /// <param name="action">The action to perform on each item of the collection.</param>
        public static void ForEach<T>(this IEnumerable<T> source, Action<T> action)
        {
            if (source == null)
                throw new ArgumentNullException("source");
            if (action == null)
                throw new ArgumentNullException("action");

            foreach (var item in source)
                action(item);
        }
    }
}
