using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    using Common.Collections;

    internal static class TextUtilities
    {
        public static string GetValue(this Group match)
        {
            if (!match.Success)
                return null;
            return match.Value;
        }

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
                            Properties.Resources.ErrorMessageInvalidQuotedChar);
                    }
                }
                else
                {
                    textBuilder.Append(value[i]);
                }
            }

            return textBuilder.ToString();
        }

        public static Tuple<string, string> SplitAtIndex(this string value, int index)
        {
            if (index < 0)
                return Tuple.Create(value, (string)null);
            else
                return Tuple.Create(value.Substring(0, index), value.Substring(index + 1));
        }

        internal static string ChangeEncoding(this string value, Encoding currentEncoding, Encoding newEncoding)
        {
            if (newEncoding == null)
                return value;
            return newEncoding.GetString(currentEncoding.GetBytes(value));
        }
    }
}
