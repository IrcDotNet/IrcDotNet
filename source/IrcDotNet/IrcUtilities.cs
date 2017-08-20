using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    // Utilities for IRC.
    internal static class IrcUtilities
    {
        private static readonly Regex ValidTagKeyRegex = new Regex(@"^([\w.-]+/)?[\w-]+$");

        // Updates collection of modes from specified mode string.
        // Mode string is of form `( "+" | "-" ) ( mode character )+`.
        public static void UpdateModes(this ICollection<char> collection, string newModes,
            IEnumerable<string> newModeParameters = null, ICollection<char> modesWithParameters = null,
            Action<bool, char, string> handleModeParameter = null)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");
            if (newModes == null)
                throw new ArgumentNullException("newModes");
            if (newModeParameters != null)
            {
                if (modesWithParameters == null)
                    throw new ArgumentNullException("modesWithParameters");
                if (handleModeParameter == null)
                    throw new ArgumentNullException("handleModeParameter");
            }

            // Reads list of mode changes, where each group of modes is prefixed by a '+' or '-', representing
            // respectively setting or unsetting of the given modes.
            bool? addMode = null;
            var modeParametersEnumerator = newModeParameters == null ? null : newModeParameters.GetEnumerator();
            foreach (var mode in newModes)
            {
                if (mode == '+')
                {
                    addMode = true;
                }
                else if (mode == '-')
                {
                    addMode = false;
                }
                else if (addMode.HasValue)
                {
                    if (newModeParameters != null && modesWithParameters.Contains(mode))
                    {
                        if (!modeParametersEnumerator.MoveNext())
                            throw new ArgumentException(Resources.MessageNotEnoughModeParameters,
                                "newModeParameters");
                        handleModeParameter(addMode.Value, mode, modeParametersEnumerator.Current);
                    }
                    else
                    {
                        if (addMode.Value)
                            collection.Add(mode);
                        else
                            collection.Remove(mode);
                    }
                }
            }
        }

        private static string escapeTagValue(string value)
        {
            // No need to use StringBuilder, as most of these replacements are expected to not match anything,
            // and therefore just return themselves, causing no new string reallocations.
            return value.Replace("\\", "\\\\")
                .Replace(";", @"\:")
                .Replace(" ", @"\s")
                .Replace("\r", @"\r")
                .Replace("\n", @"\n");
        }
        
        private static string unescapeTagValue(string value)
        {
            // No need to use StringBuilder, as most of these replacements are expected to not match anything,
            // and therefore just return themselves, causing no new string allocations.
            // Use NUL character as temporary replacement, because it's illegal in irc messages.
            return value.Replace("\\\\", "\x00")
                .Replace(@"\:", ";")
                .Replace(@"\s", " ")
                .Replace(@"\r", "\r")
                .Replace(@"\n", "\n")
                .Replace("\x00", "\\");
        }

        // takes a tag-string and turns it into a dictionary
        public static IDictionary<string, string> DecodeTags(string tagString)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(tagString))
            {
                return result;
            }
            // tagString looks something like this: key1=value1;key2=value2
            foreach (var tag in tagString.Split(';'))
            {
                var valueSplitIndex = tag.IndexOf('=');
                if (valueSplitIndex == -1)
                {
                    result.Add(tag, null);
                }
                else
                {
                    var value = unescapeTagValue(tag.Substring(valueSplitIndex + 1));
                    var tagName = tag.Substring(0, valueSplitIndex);
                    result.Add(tagName, value);
                }
            }
            return result;
        }

        // takes a dictionary and turns it into a tag-string
        public static string EncodeTags(IDictionary<string, string> tags)
        {
            // encode dictionary entires into something like this: key1=value1;key2=value2
            IList<string> tagstrings = new List<string>(tags.Count);
            foreach (var tag in tags)
            {
                if (!ValidTagKeyRegex.IsMatch(tag.Key))
                {
                    throw new ArgumentException("key is not a valid irc tag key: " + tag.Key);
                }
                if (tag.Value == null)
                {
                    tagstrings.Add(tag.Key);
                }
                else
                {
                    tagstrings.Add(tag.Key + "=" + escapeTagValue(tag.Value));
                }
            }
            return string.Join(";", tagstrings);
        }
    }
}