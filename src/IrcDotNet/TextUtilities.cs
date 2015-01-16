using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    using Collections;

    // Utilities for text manipulation.
    internal static class TextUtilities
    {
        // Gets single matched value of group, if match succeeded.
        public static string GetValue(this Group match)
        {
            if (!match.Success)
                return null;
            return match.Value;
        }

        // Enquotes specified string given escape character and mapping for quotation characters.
        public static string Quote(this string value, char escapeChar, IDictionary<char, char> quotedChars)
        {
            var textBuilder = new StringBuilder(value.Length * 2);
            for (int i = 0; i < value.Length; i++)
            {
                char curQuotedChar = escapeChar;
                if (quotedChars.TryGetValue(value[i], out curQuotedChar) || value[i] == escapeChar)
                {
                    textBuilder.Append(escapeChar);
                    textBuilder.Append(curQuotedChar);
                }
                else
                {
                    textBuilder.Append(value[i]);
                }
            }

            return textBuilder.ToString();
        }

        // Dequotes specified string given escape character and mapping for quotation characters.
        public static string Dequote(this string value, char escapeChar, IDictionary<char, char> dequotedChars)
        {
            var textBuilder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == escapeChar)
                {
                    i++;
                    char curDequotedChar = escapeChar;
                    if (dequotedChars.TryGetValue(value[i], out curDequotedChar) || value[i] == escapeChar)
                    {
                        textBuilder.Append(curDequotedChar);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            Properties.Resources.MessageInvalidQuotedChar);
                    }
                }
                else
                {
                    textBuilder.Append(value[i]);
                }
            }

            return textBuilder.ToString();
        }

        // Splits specified string into pair of strings at position of first occurrence of separator.
        public static Tuple<string, string> SplitIntoPair(this string value, string separator)
        {
            var index = value.IndexOf(separator);
            if (index < 0)
                return Tuple.Create(value, (string)null);
            else
                return Tuple.Create(value.Substring(0, index), value.Substring(index + separator.Length));
        }

        // Change character encoding of specified string.
        internal static string ChangeEncoding(this string value, Encoding currentEncoding, Encoding newEncoding)
        {
            if (newEncoding == null)
                return value;
            var buffer = currentEncoding.GetBytes(value);
            return newEncoding.GetString(buffer, 0, buffer.Length);
        }
    }
}
