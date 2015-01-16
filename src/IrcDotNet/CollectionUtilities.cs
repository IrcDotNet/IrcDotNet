using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    internal static class CollectionUtilities
    {
        public static IDictionary<TValue, TKey> Invert<TKey, TValue>(this IDictionary<TKey, TValue> dictionary)
        {
            return dictionary.ToDictionary(pair => pair.Value, pair => pair.Key);
        }
    }
}
