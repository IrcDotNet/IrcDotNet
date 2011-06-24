using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    // Utilities for debugging execution.
    // TODO: use TraceSource here and configure trace listeners in test project.
    internal static class DebugUtilities
    {
        public static void WriteIrcRawLine(IrcClient client, string line)
        {
            WriteEvent("({0}) {1}", client.ClientId, line);
        }

        public static void WriteEvent(string message, params object[] args)
        {
            Debug.WriteLine(string.Format("{0:HH:mm:ss} {1}", DateTime.Now, string.Format(message, args)));
        }
    }
}
