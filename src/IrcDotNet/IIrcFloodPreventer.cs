using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Defines a mechanism for preventing server floods by limiting the rate of outgoing raw messages from the client.
    /// </summary>
    public interface IIrcFloodPreventer
    {
        /// <summary>
        /// Gets the time delay before which the client may currently send the next message.
        /// </summary>
        /// <returns>The time delay before the next message may be sent, in milliseconds.</returns>
        long GetSendDelay();

        /// <summary>
        /// Notifies the flood preventer that a message has just been send by the client.
        /// </summary>
        void HandleMessageSent();
    }
}
