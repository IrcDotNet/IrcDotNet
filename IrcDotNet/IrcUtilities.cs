using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public static class IrcUtilities
    {
        // Reads list of mode changes, where each group of modes is prefixed by a '+' or '-', representing respectively
        // setting or unsetting of the given modes.
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
                            throw new InvalidOperationException(Properties.Resources.ErrorMessageNotEnoughModeParameters);
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
