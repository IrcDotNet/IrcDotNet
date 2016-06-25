using System;
using System.Collections.Generic;
using System.Reflection;

namespace IrcDotNet
{
    // Utilities for reflection of managed entities.
    internal static class ReflectionUtilities
    {
        public static IEnumerable<Tuple<TAttribute, TDelegate>> GetAttributedMethods<TAttribute, TDelegate>(
            this object obj)
            where TAttribute : Attribute
            where TDelegate : class
        {
            // Find all methods in class that are marked by one or more instances of given attribute.
#if NETSTANDARD1_5
            var messageProcessorsMethods = obj.GetType().GetTypeInfo().GetMethods(
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
#else
            var messageProcessorsMethods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic
                                                                    | BindingFlags.Public);
#endif
            foreach (var methodInfo in messageProcessorsMethods)
            {
                var methodAttributes = (TAttribute[]) methodInfo.GetCustomAttributes(
                    typeof (TAttribute), true);
                if (methodAttributes.Length > 0)
                {
#if NETSTANDARD1_5
                    var methodDelegate =
                        (TDelegate)(object)methodInfo.CreateDelegate(typeof(TDelegate), obj);
#else
                    var methodDelegate =
                        (TDelegate) (object) Delegate.CreateDelegate(typeof (TDelegate), obj, methodInfo);
#endif

                    // Get each attribute applied to method.
                    foreach (var attribute in methodAttributes)
                        yield return Tuple.Create(attribute, methodDelegate);
                }
            }
        }
    }
}