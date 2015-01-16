using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace IrcDotNet.Ctcp
{
    // Defines all message senders for the client.
    partial class CtcpClient
    {
        
        /// <summary>
        /// Sends an action message to the specified target.
        /// </summary>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The message text.</param>
        protected void SendMessageAction(IList<IIrcMessageTarget> targets, string text)
        {
            WriteMessage(targets, "action", text);

            OnActionSent(new CtcpMessageEventArgs(this.ircClient.LocalUser, targets, text));
        }

        /// <summary>
        /// Sends a request for the local date/time to the specified target.
        /// </summary>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="info">The information to send.</param>
        /// <param name="isResponse"><see langword="true"/> if the message is a response; <see langword="false"/>,
        /// otherwise.</param>
        protected void SendMessageTime(IList<IIrcMessageTarget> targets, string info, bool isResponse)
        {
            WriteMessage(targets, "time", info, isResponse);
        }

        /// <summary>
        /// Sends a request or response for information about the version of the client.
        /// </summary>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="info">The information to send.</param>
        /// <param name="isResponse"><see langword="true"/> if the message is a response; <see langword="false"/>,
        /// otherwise.</param>
        protected void SendMessageVersion(IList<IIrcMessageTarget> targets, string info, bool isResponse)
        {
            WriteMessage(targets, "version", info, isResponse);
        }

        /// <summary>
        /// Sends a request for confirming that no error has occurred.
        /// </summary>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="tag">A tag that can be used for tracking the response.</param>
        /// <param name="isResponse"><see langword="true"/> if the message is a response; <see langword="false"/>,
        /// otherwise.</param>
        protected void SendMessageErrMsg(IList<IIrcMessageTarget> targets, string tag, bool isResponse)
        {
            WriteMessage(targets, "errmsg", tag, isResponse);
        }

        /// <summary>
        /// Sends a ping request or response to the specified target.
        /// </summary>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="info">The information to send.</param>
        /// <param name="isResponse"><see langword="true"/> if the message is a response; <see langword="false"/>,
        /// otherwise.</param>
        protected void SendMessagePing(IList<IIrcMessageTarget> targets, string info, bool isResponse)
        {
            WriteMessage(targets, "ping", info, isResponse);
        }
    }
}
