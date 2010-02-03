using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    /// <summary>
    /// Contains common utilities for functionality relating to text manipulation.
    /// </summary>
    public static class TextUtilities
    {
        public static string GetValue(this Group match)
        {
            if (!match.Success)
                return null;
            return match.Value;
        }
    }
}
