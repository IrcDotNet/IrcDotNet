using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet.Ctcp
{
    // Defines all message senders for the client.
    partial class CtcpClient
    {
        /// <summary>
        /// Sends a ping reuqest or response to the specified target.
        /// </summary>
        /// <param name="target">The target of the message.</param>
        /// <param name="info">The information to send.</param>
        /// <param name="isResponse"><see langword="true"/> if the message is a response; <see langword="false"/>,
        /// otherwise.</param>
        protected void SendMessagePing(IIrcMessageTarget target, string info, bool isResponse)
        {
            WriteMessage(target, "ping", info, isResponse);
        }

        /// <summary>
        /// Sends a request or response for information about the version of the client.
        /// </summary>
        /// <param name="target">The target of the message.</param>
        /// <param name="info">The information to send.</param>
        protected void SendMessageVersion(IIrcMessageTarget target, string info = null)
        {
            WriteMessage(target, "version", info, info != null);
        }
    }
}
