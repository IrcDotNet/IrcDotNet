using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace IrcDotNet.Ctcp
{
    // Defines all message processors for the client.
    partial class CtcpClient
    {
        /// <summary>
        /// Process PING messages from a user.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("ping")]
        protected void ProcessMessagePing(CtcpMessage message)
        {
            Debug.Assert(message.Data != null);

            if (message.IsResponse)
            {
                // Calculate time elapsed since the ping request was sent.
                var sendTime = new DateTime(long.Parse(message.Data));
                var receiveTime = DateTime.Now;
                var pingTime = receiveTime - sendTime;

                OnPingResponseReceived(new CtcpPingResponseReceivedEventArgs(message.Source, pingTime));
            }
            else
            {
                SendMessagePing(message.Source, message.Data, true);
            }
        }

        /// <summary>
        /// Process VERSION messages from a user.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("version")]
        protected void ProcessMessageVersion(CtcpMessage message)
        {
            if (message.IsResponse)
            {
                var versionInfo = message.Data;

                OnVersionResponseReceived(new CtcpVersionResponseReceivedEventArgs(message.Source, versionInfo));
            }
            else
            {
                if (this.ClientVersion != null)
                {
                    SendMessageVersion(message.Source, this.ClientVersion);
                }
            }
        }
    }
}
