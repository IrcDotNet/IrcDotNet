using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Defines a mechanism for preventing server floods by limiting the rate of outgoing messages from the client.
    /// </summary>
    public interface IIrcFloodPreventer
    {
        /// <summary>
        /// Determines whether the client may currently send a message.
        /// </summary>
        /// <returns><see langword="true"/> if the client may currently send a message; <see langword="false"/> if the
        /// client must wait before sending another message.</returns>
        bool CanSendMessage();

        /// <summary>
        /// Notifies the mechanism that a message has just been send by the client.
        /// </summary>
        void HandleMessageSent();
    }
}
