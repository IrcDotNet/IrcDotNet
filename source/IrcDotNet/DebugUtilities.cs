using System;
using System.Diagnostics;

namespace IrcDotNet
{
    // Utilities for debugging execution.
    // TODO: Use TraceSource here and configure trace listeners in test project.
    internal static class DebugUtilities
    {
        [Conditional("DEBUG")]
        public static void WriteIrcRawLine(IrcClient client, string line)
        {
#if DEBUG
            WriteEvent("({0}) {1}", client.ClientId, line);
#endif
        }

        [Conditional("DEBUG")]
        public static void WriteEvent(string message, params object[] args)
        {
            Debug.WriteLine("{0:HH:mm:ss} {1}", DateTime.Now, string.Format(message, args));
        }
    }
}