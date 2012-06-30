// --------------------------------------------------------------------------------------------------------------------
// <copyright file="DictionaryExtensions.cs" company="Yet another App Factory">
//   @ Matthias Dittrich
// </copyright>
// <summary>
//   The dictionary extensions.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Yaaf.Utils.Extensions
{ 
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;

    /// <summary>
    /// The dictionary extensions.
    /// </summary>
    public static class DictionaryExtensions
    {
        #region Public Methods

        /// <summary>
        /// The try get or create value.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary.
        /// </param>
        /// <param name="createValue">
        /// The create value.
        /// </param>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <typeparam name="TKey">
        /// </typeparam>
        /// <typeparam name="TValue">
        /// </typeparam>
        /// <returns>
        /// </returns>
        public static TValue TryGetOrCreateValue<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, Func<TValue> createValue, TKey key)
        {
            return dictionary.TryGetOrCreateValue(k => createValue(), key);
        }

        /// <summary>
        /// The try get or create value.
        /// </summary>
        /// <param name="dictionary">
        /// The dictionary.
        /// </param>
        /// <param name="createValue">
        /// The create value.
        /// </param>
        /// <param name="key">
        /// The key.
        /// </param>
        /// <param name="ensureSingleCallForKey">
        /// ensures createValue gets only called once for every key (will nest the call into the lock statement so be carefull!)
        /// this flag should be false! If this flag is true a deadlock is possible due user code!
        /// </param>
        /// <typeparam name="TKey">
        /// </typeparam>
        /// <typeparam name="TValue">
        /// </typeparam>
        /// <returns>
        /// </returns>
        public static TValue TryGetOrCreateValue<TKey, TValue>(
            this IDictionary<TKey, TValue> dictionary, Func<TKey, TValue> createValue, TKey key, bool ensureSingleCallForKey = false)
        {
            Contract.Requires(dictionary != null);
            Contract.Requires(createValue != null);

            TValue value;
            var list = dictionary as IList;
            object syncRoot = list != null ? list.SyncRoot : dictionary;

            if (ensureSingleCallForKey)
            {
                if (!dictionary.TryGetValue(key, out value))
                {
                    lock (syncRoot)
                    {
                        if (!dictionary.TryGetValue(key, out value))
                        {
                            var createdValue = createValue(key);
                            dictionary.Add(key, createdValue);
                            value = createdValue;
                        }
                    }
                }
            }
            else
            {
                if (!dictionary.TryGetValue(key, out value))
                {
                    var createdValue = createValue(key);
                    lock (syncRoot)
                    {
                        if (!dictionary.TryGetValue(key, out value))
                        {
                            dictionary.Add(key, createdValue);
                            value = createdValue;
                        }
                    }
                }
            }


            return value;
        }

        #endregion
    }
}