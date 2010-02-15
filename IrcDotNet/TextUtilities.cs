using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    internal static class TextUtilities
    {
        public static string GetValue(this Group match)
        {
            if (!match.Success)
                return null;
            return match.Value;
        }

        public static Tuple<string, string> SplitAtIndex(this string value, int index)
        {
            if (index < 0)
                return Tuple.Create(value, (string)null);
            else
                return Tuple.Create(value.Substring(0, index), value.Substring(index + 1));
        }
    }
}
