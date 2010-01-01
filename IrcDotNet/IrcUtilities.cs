using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public static class IrcUtilities
    {
        public static void UpdateModes(this ICollection<char> collection, string newModes)
        {
            bool? addMode = null;
            for (int i = 0; i < newModes.Length; i++)
            {
                if (newModes[i] == '+')
                {
                    addMode = true;
                }
                else if (newModes[i] == '-')
                {
                    addMode = false;
                }
                else if (addMode.HasValue)
                {
                    if (addMode.Value)
                        collection.Add(newModes[i]);
                    else
                        collection.Remove(newModes[i]);
                }
            }
        }
    }
}
