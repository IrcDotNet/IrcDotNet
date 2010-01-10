using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Collections
{
    internal static class CollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (range == null)
                throw new ArgumentNullException("range");

            foreach (var item in range)
                collection.Add(item);
        }

        public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (range == null)
                throw new ArgumentNullException("range");

            foreach (var item in range)
                collection.Remove(item);
        }

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
