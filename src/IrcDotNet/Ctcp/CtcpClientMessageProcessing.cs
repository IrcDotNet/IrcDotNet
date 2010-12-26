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
        /// Process ACTION messages from a user.
        /// </summary>
        /// <param name="message">The message received from the user.</param>
        [MessageProcessor("action")]
        protected void ProcessMessageAction(CtcpMessage message)
        {
            Debug.Assert(message.Data != null);

            if (!message.IsResponse)
            {
                var text = message.Data;

                OnActionReceived(new CtcpMessageEventArgs(message.Source, message.Targets, text));
            }
        }

        /// <summary>
        /// Process TIME messages from a user.
        /// </summary>
        /// <param name="message">The message received from the user.</param>
        [MessageProcessor("time")]
        protected void ProcessMessageTime(CtcpMessage message)
        {
            if (message.IsResponse)
            {
                var dateTime = message.Data;

                OnTimeResponseReceived(new CtcpTimeResponseReceivedEventArgs(message.Source, dateTime));
            }
            else
            {
                var localDateTime = DateTimeOffset.Now.ToString("o");
                
                SendMessageTime(new[] { message.Source }, localDateTime, true);
            }
        }

        /// <summary>
        /// Process VERSION messages from a user.
        /// </summary>
        /// <param name="message">The message received from the user.</param>
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
                    SendMessageVersion(new[] { message.Source }, this.ClientVersion, true);
                }
            }
        }

        /// <summary>
        /// Process PING messages from a user.
        /// </summary>
        /// <param name="message">The message received from the user.</param>
        [MessageProcessor("ping")]
        protected void ProcessMessagePing(CtcpMessage message)
        {
            Debug.Assert(message.Data != null);

            if (message.IsResponse)
            {
                // Calculate time elapsed since the ping request was sent.
                var sendTime = new DateTime(long.Parse(message.Data));
                var pingTime = DateTime.Now - sendTime;

                OnPingResponseReceived(new CtcpPingResponseReceivedEventArgs(message.Source, pingTime));
            }
            else
            {
                SendMessagePing(new[] { message.Source }, message.Data, true);
            }
        }
    }
}
