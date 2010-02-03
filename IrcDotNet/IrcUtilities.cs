using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Contains common utilities for functionality relating to IRC.
    /// </summary>
    public static class IrcUtilities
    {
        /// <summary>
        /// Updates the specified collection of mode characters from the specified mode string.
        /// Optionally outputs any mode parameters.
        /// </summary>
        /// <param name="collection">A collection of mode characters, to which changes are made.</param>
        /// <param name="newModes">A mode string. A mode string is of the form `( "+" / "-" ) *( mode character )`,
        /// and specifies mode changes.</param>
        /// <param name="newModeParameters">A collection of all mode parameters for the specified mode string.</param>
        /// <param name="modesWithParameters">A collection of all modes that take parameters..</param>
        /// <param name="handleModeParameter">A function that is called whenever a mode parameter is found for a certain
        /// mode character. The first argument is <see langword="true"/> if the mode is being added; false if it is
        /// being removed. The second argument is the mode character. The third argument is the mode parameter.</param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collection"/> is <see langword="null"/>. -or-
        /// <paramref name="newModes"/> is <see langword="null"/>. -or-
        /// <paramref name="modesWithParameters"/> is <see langword="null"/> and <paramref name="newModeParameters"/>
        /// is specified. -or-
        /// <paramref name="handleModeParameter"/> is <see langword="null"/> and <paramref name="newModeParameters"/>
        /// is specified.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="newModeParameters"/> does not contain enough mode
        /// parameters for the specified mode string, <paramref name="newModes"/>.</exception>
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

            // Reads list of mode changes, where each group of modes is prefixed by a '+' or '-', representing respectively
            // setting or unsetting of the given modes.
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
                            throw new ArgumentException(Properties.Resources.ErrorMessageNotEnoughModeParameters,
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
    }
}
