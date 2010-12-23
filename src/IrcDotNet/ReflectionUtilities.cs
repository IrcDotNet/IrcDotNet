using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IrcDotNet
{
    internal static class ReflectionUtilities
    {
        public static IEnumerable<Tuple<TAttribute, TDelegate>> GetMethodAttributes<TAttribute, TDelegate>(
            this object obj)
            where TAttribute : Attribute
            where TDelegate : class
        {
            // Find all methods in class that are marked by one or more instances of TAttribute.
            // Add each pair of attribute instance & method delegate to dictionary.
            var messageProcessorsMethods = obj.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var methodInfo in messageProcessorsMethods)
            {
                var messageProcessorAttributes = (TAttribute[])methodInfo.GetCustomAttributes(
                    typeof(TAttribute), true);
                if (messageProcessorAttributes.Length > 0)
                {
                    var methodDelegate = (TDelegate)(object)Delegate.CreateDelegate(typeof(TDelegate), obj, methodInfo);
                    foreach (var attribute in messageProcessorAttributes)
                        yield return Tuple.Create(attribute, methodDelegate);
                }
            }
        }
    }
}
