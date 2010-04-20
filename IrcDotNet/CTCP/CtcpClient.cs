using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IrcDotNet.Common.Collections;

namespace IrcDotNet.CTCP
{
    /// <summary>
    /// Provides methods for communicating with a server using CTCP (Client to Client Protocol), which operates over a
    /// connection to an IRC server.
    /// Do not inherit unless the protocol itself is being extended.
    /// </summary>
    [DebuggerDisplay("{ToString(), nq}")]
    public class CtcpClient
    {
        // Dictionary of message processor routines, keyed by their command names.
        private Dictionary<string, MessageProcessor> messageProcessors;

        private IrcClient ircClient;

        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpClient"/> class.
        /// </summary>
        /// <param name="ircClient">The IRC client by which the CTCP client should communicate.</param>
        public CtcpClient(IrcClient ircClient)
        {
            if (ircClient == null)
                throw new ArgumentNullException("ircClient");

            this.ircClient = ircClient;
            this.messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.InvariantCultureIgnoreCase);

            InitialiseMessageProcessors();

            this.ircClient.Connected += ircClient_Connected;
            this.ircClient.Disconnected += ircClient_Disconnected;
        }

        /// <summary>
        /// Gets or sets the IRC client by which the CTCP client should communicate.
        /// </summary>
        /// <value>The IRC client.</value>
        public IrcClient IrcClient
        {
            get { return this.ircClient; }
        }

        /// <summary>
        /// Occurs when the client encounters an error during execution.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> Error;

        private void ircClient_Connected(object sender, EventArgs e)
        {
            this.ircClient.LocalUser.PreviewMessageReceived += ircClient_LocalUser_PreviewMessageReceived;
            this.ircClient.LocalUser.PreviewNoticeReceived += ircClient_LocalUser_PreviewNoticeReceived;
        }

        private void ircClient_Disconnected(object sender, EventArgs e)
        {
            this.ircClient.LocalUser.PreviewMessageReceived -= ircClient_LocalUser_PreviewMessageReceived;
            this.ircClient.LocalUser.PreviewNoticeReceived -= ircClient_LocalUser_PreviewNoticeReceived;
        }

        private void ircClient_LocalUser_PreviewMessageReceived(object sender, IrcPreviewMessageEventArgs e)
        {
            // TODO
        }

        private void ircClient_LocalUser_PreviewNoticeReceived(object sender, IrcPreviewMessageEventArgs e)
        {
            // TODO
        }

        private void InitialiseMessageProcessors()
        {
            this.GetMethodAttributes<MessageProcessorAttribute, MessageProcessor>().ForEach(item =>
                {
                    var attribute = item.Item1;
                    var methodDelegate = item.Item2;
                    
                    this.messageProcessors.Add(attribute.Command, methodDelegate);
                });
        }

        private void ReadMessage(CtcpMessage message)
        {
            // Try to find corresponding message processor for command of given message.
            MessageProcessor messageProcessor;
            if (this.messageProcessors.TryGetValue(message.Tag, out messageProcessor))
            {
                try
                {
                    messageProcessor(message);
                }
#if !DEBUG
                catch (Exception ex)
                {
                    OnError(new IrcErrorEventArgs(ex));
                }
#endif
                finally
                {
                }
            }
            else
            {
                // Unknown command.
                Debug.WriteLine(string.Format("Unknown CTCP message tag '{0}'.", message.Tag));
            }
        }

        /// <summary>
        /// Raises the <see cref="Error"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs"/> instance containing the event data.</param>
        protected virtual void OnError(IrcErrorEventArgs e)
        {
            var handler = this.Error;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            return string.Format("CTCP / {0}", this.ircClient);
        }

        /// <summary>
        /// Represents a method that processes <see cref="CtcpMessage"/> objects.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        protected delegate void MessageProcessor(CtcpMessage message);

        /// <summary>
        /// Represents a message that is sent/received by the client/server using the CTCP protocol.
        /// </summary>
        [DebuggerDisplay("{ToString(), nq}")]
        public struct CtcpMessage
        {
            /// <summary>
            /// The tag of the message that specifies the kind of data it contains
            /// </summary>
            public string Tag;

            /// <summary>
            /// The data contained by the message.
            /// </summary>
            public string Data;

            public override string ToString()
            {
                return string.Format("{0} {1}", this.Tag, this.Data);
            }
        }
    }
}
