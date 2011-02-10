using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Utilities for reflection of managed entities.
    /// </summary>
    internal static class ReflectionUtilities
    {
        /// <summary>
        /// Gets a collection of methods of the specified object that have an attribute of the specified type.
        /// </summary>
        /// <typeparam name="TAttribute">The type of the attribute.</typeparam>
        /// <typeparam name="TDelegate">The type of the method delegates.</typeparam>
        /// <param name="obj">The object whose methods to get.</param>
        /// <returns>A collection of methods that have an attribute of the specified type.</returns>
        public static IEnumerable<Tuple<TAttribute, TDelegate>> GetAttributedMethods<TAttribute, TDelegate>(
            this object obj)
            where TAttribute : Attribute
            where TDelegate : class
        {
            // Find all methods in class that are marked by one or more instances of given attribute.
            var messageProcessorsMethods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var methodInfo in messageProcessorsMethods)
            {
                var methodAttributes = (TAttribute[])methodInfo.GetCustomAttributes(
                    typeof(TAttribute), true);
                if (methodAttributes.Length > 0)
                {
                    var methodDelegate = (TDelegate)(object)Delegate.CreateDelegate(typeof(TDelegate), obj, methodInfo);

                    // Get each attribute applied to method.
                    foreach (var attribute in methodAttributes)
                        yield return Tuple.Create(attribute, methodDelegate);
                }
            }
        }
    }
}
