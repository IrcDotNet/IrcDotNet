using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace System.Collections
{
    public static class CollectionExtensions
    {
        public static void AddRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            foreach (var item in range)
                collection.Add(item);
        }

        public static void RemoveRange<T>(this ICollection<T> collection, IEnumerable<T> range)
        {
            foreach (var item in range)
                collection.Remove(item);
        }

        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var item in enumerable)
                action(item);
        }
    }
}
