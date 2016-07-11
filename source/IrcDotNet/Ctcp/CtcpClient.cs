using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using IrcDotNet.Collections;
using IrcDotNet.Properties;

namespace IrcDotNet.Ctcp
{
    /// <summary>
    ///     Represents a client that communicates with a server using CTCP (Client to Client Protocol), operating over an
    ///     IRC connection.
    ///     Do not inherit this class unless the protocol itself is being extended.
    /// </summary>
    /// <remarks>
    ///     All collection objects must be locked on the <see cref="ICollection.SyncRoot" /> object for thread-safety.
    ///     They can however be used safely without locking within event handlers.
    /// </remarks>
    /// <threadsafety static="true" instance="true" />
    /// <seealso cref="IrcClient" />
    [DebuggerDisplay("{ToString(), nq}")]
    public partial class CtcpClient
    {
        // Message indicating that no error occurred.
        private const string messageNoError = "no error";

        // Tag used for checking whether no error occurred for remote user.
        private const string noErrorTag = "NO_ERROR";

        // Character that marks start and end of tagged data.
        private const char taggedDataDelimeterChar = '\x001';

        // Information for low-level quoting of messages.
        private const char lowLevelQuotingEscapeChar = '\x10';

        // Information for CTCP-quoting of messages.
        private const char ctcpQuotingEscapeChar = '\x5C';

        private static readonly IDictionary<char, char> lowLevelQuotedChars = new Dictionary<char, char>
        {
            {'\0', '0'},
            {'\n', 'n'},
            {'\r', 'r'}
        };

        private static readonly IDictionary<char, char> lowLevelDequotedChars = lowLevelQuotedChars.Invert();

        private static readonly IDictionary<char, char> ctcpQuotedChars = new Dictionary<char, char>
        {
            {taggedDataDelimeterChar, 'a'}
        };

        private static readonly IDictionary<char, char> ctcpDequotedChars = ctcpQuotedChars.Invert();

        // IRC client for communication.

        // Dictionary of message processor routines, keyed by their command names.
        private readonly Dictionary<string, MessageProcessor> messageProcessors;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpClient" /> class.
        /// </summary>
        /// <param name="ircClient">The IRC client by which the CTCP client should communicate.</param>
        public CtcpClient(IrcClient ircClient)
        {
            if (ircClient == null)
                throw new ArgumentNullException("ircClient");

            IrcClient = ircClient;
            messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.OrdinalIgnoreCase);

            InitializeMessageProcessors();

            IrcClient.Connected += ircClient_Connected;
            IrcClient.Disconnected += ircClient_Disconnected;
        }

        /// <summary>
        ///     Gets or sets information about the client version.
        /// </summary>
        /// <value>Information about the client version.</value>
        public string ClientVersion { get; set; }

        /// <summary>
        ///     Gets or sets the IRC client by which the CTCP client should communicate.
        /// </summary>
        /// <value>The IRC client.</value>
        public IrcClient IrcClient { get; }

        /// <summary>
        ///     Occurs when an action has been sent to a user.
        /// </summary>
        public event EventHandler<CtcpMessageEventArgs> ActionSent;

        /// <summary>
        ///     Occurs when an action has been received from a user.
        /// </summary>
        public event EventHandler<CtcpMessageEventArgs> ActionReceived;

        /// <summary>
        ///     Occurs when a response to a date/time request has been received from a user.
        /// </summary>
        public event EventHandler<CtcpTimeResponseReceivedEventArgs> TimeResponseReceived;

        /// <summary>
        ///     Occurs when a response to a version request has been received from a user.
        /// </summary>
        public event EventHandler<CtcpVersionResponseReceivedEventArgs> VersionResponseReceived;

        /// <summary>
        ///     Occurs when an error message has been received from a user.
        /// </summary>
        public event EventHandler<CtcpErrorMessageReceivedEventArgs> ErrorMessageReceived;

        /// <summary>
        ///     Occurs when a ping response has been received from a user.
        /// </summary>
        public event EventHandler<CtcpPingResponseReceivedEventArgs> PingResponseReceived;

        /// <summary>
        ///     Occurs when a raw message has been sent to a user.
        /// </summary>
        public event EventHandler<CtcpRawMessageEventArgs> RawMessageSent;

        /// <summary>
        ///     Occurs when a raw message has been received from a user.
        /// </summary>
        public event EventHandler<CtcpRawMessageEventArgs> RawMessageReceived;

        /// <summary>
        ///     Occurs when the client encounters an error during execution.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> Error;

        /// <inheritdoc cref="SendAction(IList{IIrcMessageTarget}, string)" />
        /// <summary>
        ///     Sends an action message to the specified list of users.
        /// </summary>
        /// <param name="user">The user to which to send the request.</param>
        public void SendAction(IIrcMessageTarget user, string text)
        {
            SendMessageAction(new[] {user}, text);
        }

        /// <summary>
        ///     Sends an action message to the specified list of users.
        /// </summary>
        /// <param name="users">A list of users to which to send the request.</param>
        /// <param name="text">The text of the message.</param>
        public void SendAction(IList<IIrcMessageTarget> users, string text)
        {
            SendMessageAction(users, text);
        }

        /// <inheritdoc cref="GetTime(IList{IIrcMessageTarget})" />
        /// <summary>
        ///     Gets the local date/time of the specified user.
        /// </summary>
        /// <param name="user">The user to which to send the request.</param>
        public void GetTime(IIrcMessageTarget user)
        {
            GetTime(new[] {user});
        }

        /// <summary>
        ///     Gets the local date/time of the specified list of users.
        /// </summary>
        /// <param name="users">A list of users to which to send the request.</param>
        public void GetTime(IList<IIrcMessageTarget> users)
        {
            SendMessageTime(users, null, false);
        }

        /// <inheritdoc cref="GetVersion(IList{IIrcMessageTarget})" />
        /// <summary>
        ///     Gets the client version of the specified user.
        /// </summary>
        /// <param name="user">The user to which to send the request.</param>
        public void GetVersion(IIrcMessageTarget user)
        {
            GetVersion(new[] {user});
        }

        /// <summary>
        ///     Gets the client version of the specified list of users.
        /// </summary>
        /// <param name="users">A list of users to which to send the request.</param>
        public void GetVersion(IList<IIrcMessageTarget> users)
        {
            SendMessageVersion(users, null, false);
        }

        /// <inheritdoc cref="CheckErrorOccurred(IList{IIrcMessageTarget})" />
        /// <summary>
        ///     Asks the specified user whether an error just occurred.
        /// </summary>
        /// <param name="user">The user to which to send the request.</param>
        public void CheckErrorOccurred(IIrcMessageTarget user)
        {
            CheckErrorOccurred(new[] {user});
        }

        /// <summary>
        ///     Asks the specified list of users whether an error just occurred.
        /// </summary>
        /// <param name="users">A list of users to which to send the request.</param>
        public void CheckErrorOccurred(IList<IIrcMessageTarget> users)
        {
            SendMessageErrMsg(users, noErrorTag, false);
        }

        /// <inheritdoc cref="Ping(IList{IIrcMessageTarget})" />
        /// <summary>
        ///     Pings the specified user.
        /// </summary>
        /// <param name="user">The user to which to send the request.</param>
        public void Ping(IIrcMessageTarget user)
        {
            Ping(new[] {user});
        }

        /// <summary>
        ///     Pings the specified list of users.
        /// </summary>
        /// <param name="users">A list of users to which to send the request.</param>
        public void Ping(IList<IIrcMessageTarget> users)
        {
            SendMessagePing(users, DateTime.Now.Ticks.ToString(), false);
        }

        private void ircClient_Connected(object sender, EventArgs e)
        {
            if (IrcClient.LocalUser != null)
            {
                IrcClient.LocalUser.PreviewMessageReceived += ircClient_LocalUser_PreviewMessageReceived;
                IrcClient.LocalUser.PreviewNoticeReceived += ircClient_LocalUser_PreviewNoticeReceived;
            }
        }

        private void ircClient_Disconnected(object sender, EventArgs e)
        {
            if (IrcClient.LocalUser != null)
            {
                IrcClient.LocalUser.PreviewMessageReceived -= ircClient_LocalUser_PreviewMessageReceived;
                IrcClient.LocalUser.PreviewNoticeReceived -= ircClient_LocalUser_PreviewNoticeReceived;
            }
        }

        private void ircClient_LocalUser_PreviewMessageReceived(object sender, IrcPreviewMessageEventArgs e)
        {
            ReadMessage(e, false);
        }

        private void ircClient_LocalUser_PreviewNoticeReceived(object sender, IrcPreviewMessageEventArgs e)
        {
            ReadMessage(e, true);
        }

        private void InitializeMessageProcessors()
        {
            // Find each method defined as processor for CTCP message.
            this.GetAttributedMethods<MessageProcessorAttribute, MessageProcessor>().ForEach(item =>
            {
                var attribute = item.Item1;
                var methodDelegate = item.Item2;

                messageProcessors.Add(attribute.CommandName, methodDelegate);
            });
        }

        private void ReadMessage(IrcPreviewMessageEventArgs previewMessageEventArgs, bool isNotice)
        {
            if (!(previewMessageEventArgs.Source is IrcUser))
                return;

            // Check if message represents tagged data.
            if (previewMessageEventArgs.Text.First() == taggedDataDelimeterChar &&
                previewMessageEventArgs.Text.Last() == taggedDataDelimeterChar)
            {
                if (previewMessageEventArgs.Source is IrcUser)
                {
                    var message = new CtcpMessage();
                    message.Source = (IrcUser) previewMessageEventArgs.Source;
                    message.Targets = previewMessageEventArgs.Targets;
                    message.IsResponse = isNotice;

                    // Parse tagged data into message.
                    var dequotedText = LowLevelDequote(CtcpDequote(previewMessageEventArgs.Text.Substring(
                        1, previewMessageEventArgs.Text.Length - 2)));
                    var firstSpaceIndex = dequotedText.IndexOf(' ');
                    if (firstSpaceIndex == -1)
                    {
                        message.Tag = dequotedText;
                        message.Data = null;
                    }
                    else
                    {
                        message.Tag = dequotedText.Substring(0, firstSpaceIndex);
                        message.Data = dequotedText.Substring(firstSpaceIndex + 1).TrimStart(':');
                    }

                    ReadMessage(message);
                    previewMessageEventArgs.Handled = true;
                }
            }
        }

        private void ReadMessage(CtcpMessage message)
        {
            OnRawMessageReceived(new CtcpRawMessageEventArgs(message));

            // Try to find corresponding message processor for command of given message.
            MessageProcessor messageProcessor;
            if (messageProcessors.TryGetValue(message.Tag, out messageProcessor))
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
                // Command is unknown.
                DebugUtilities.WriteEvent("Unknown CTCP message tag '{0}'.", message.Tag);
            }
        }

        /// <inheritdoc cref="WriteMessage(IList{IIrcMessageTarget}, CtcpMessage)" />
        /// <param name="tag">The tag of the message.</param>
        /// <param name="data">The data contained by the message.</param>
        /// <param name="isResponse">
        ///     <see langword="true" /> if the message is a response to another message;
        ///     <see langword="false" />, otherwise.
        /// </param>
        protected void WriteMessage(IList<IIrcMessageTarget> targets, string tag, string data = null,
            bool isResponse = false)
        {
            WriteMessage(targets, new CtcpMessage(IrcClient.LocalUser, targets, tag, data, isResponse));
        }

        /// <inheritdoc cref="WriteMessage(IList{IIrcMessageTarget}, string, bool)" />
        /// <param name="message">The message to write.</param>
        /// <exception cref="ArgumentException">
        ///     <paramref name="message" /> contains more than 15 many parameters.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The value of <see cref="CtcpMessage.Tag" /> of <paramref name="message" />
        ///     is invalid.
        /// </exception>
        protected void WriteMessage(IList<IIrcMessageTarget> targets, CtcpMessage message)
        {
            if (message.Tag == null)
                throw new ArgumentException(Resources.MessageInvalidTag, "message");

            var tag = message.Tag.ToUpper();
            var taggedData = message.Data == null ? tag : tag + " :" + message.Data;
            WriteMessage(targets, taggedData, message.IsResponse);
            OnRawMessageSent(new CtcpRawMessageEventArgs(message));
        }

        /// <summary>
        ///     Writes the specified message to a target.
        /// </summary>
        /// <param name="targets">A list of the targets to which to write the message.</param>
        /// <param name="taggedData">The tagged data to write.</param>
        /// <param name="isResponse">
        ///     <see langword="true" /> if the message is a response to another message;
        ///     <see langword="false" />, otherwise.
        /// </param>
        private void WriteMessage(IList<IIrcMessageTarget> targets, string taggedData, bool isResponse)
        {
            Debug.Assert(taggedData != null);
            var text = taggedDataDelimeterChar + LowLevelQuote(CtcpQuote(taggedData)) + taggedDataDelimeterChar;

            if (isResponse)
                IrcClient.LocalUser.SendNotice(targets, text);
            else
                IrcClient.LocalUser.SendMessage(targets, text);
        }

        private string LowLevelQuote(string value)
        {
            return value.Quote(lowLevelQuotingEscapeChar, lowLevelQuotedChars);
        }

        private string LowLevelDequote(string value)
        {
            return value.Dequote(lowLevelQuotingEscapeChar, lowLevelDequotedChars);
        }

        private string CtcpQuote(string value)
        {
            return value.Quote(ctcpQuotingEscapeChar, ctcpQuotedChars);
        }

        private string CtcpDequote(string value)
        {
            return value.Dequote(ctcpQuotingEscapeChar, ctcpDequotedChars);
        }

        /// <summary>
        ///     Raises the <see cref="ActionSent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="CtcpMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnActionSent(CtcpMessageEventArgs e)
        {
            var handler = ActionSent;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ActionReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="CtcpMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnActionReceived(CtcpMessageEventArgs e)
        {
            var handler = ActionReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="TimeResponseReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="CtcpTimeResponseReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnTimeResponseReceived(CtcpTimeResponseReceivedEventArgs e)
        {
            var handler = TimeResponseReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="VersionResponseReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="CtcpVersionResponseReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnVersionResponseReceived(CtcpVersionResponseReceivedEventArgs e)
        {
            var handler = VersionResponseReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ErrorMessageReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="CtcpErrorMessageReceivedEventArgs" /> instance containing the event
        ///     data.
        /// </param>
        protected virtual void OnErrorMessageResponseReceived(CtcpErrorMessageReceivedEventArgs e)
        {
            var handler = ErrorMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PingResponseReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="CtcpPingResponseReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnPingResponseReceived(CtcpPingResponseReceivedEventArgs e)
        {
            var handler = PingResponseReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="RawMessageSent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="CtcpRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageSent(CtcpRawMessageEventArgs e)
        {
            var handler = RawMessageSent;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="RawMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="CtcpRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageReceived(CtcpRawMessageEventArgs e)
        {
            var handler = RawMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Error" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnError(IrcErrorEventArgs e)
        {
            var handler = Error;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            return string.Format("CTCP / {0}", IrcClient);
        }

        /// <summary>
        ///     Represents a method that processes <see cref="CtcpMessage" /> objects.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        protected delegate void MessageProcessor(CtcpMessage message);

        /// <summary>
        ///     Represents a raw CTCP message that is sent/received by <see cref="CtcpClient" />.
        /// </summary>
        /// <seealso cref="CtcpClient" />
        [DebuggerDisplay("{ToString(), nq}")]
        public struct CtcpMessage
        {
            /// <summary>
            ///     The user that sent the message.
            /// </summary>
            public IrcUser Source;

            /// <summary>
            ///     A list of users to which to send the message.
            /// </summary>
            public IList<IIrcMessageTarget> Targets;

            /// <summary>
            ///     The tag of the message, that specifies the kind of data it contains or the type of the request.
            /// </summary>
            public string Tag;

            /// <summary>
            ///     The data contained by the message.
            /// </summary>
            public string Data;

            /// <summary>
            ///     <see langword="true" /> if this message is a response to another message; <see langword="false" />,
            ///     otherwise.
            /// </summary>
            public bool IsResponse;

            /// <summary>
            ///     Initializes a new instance of the <see cref="CtcpMessage" /> structure.
            /// </summary>
            /// <param name="source">The source of the message.</param>
            /// <param name="targets">A list of the targets of the message.</param>
            /// <param name="tag">The tag of the message.</param>
            /// <param name="data">The data contained by the message, or <see langword="null" /> for no data.</param>
            /// <param name="isResponse">
            ///     <see langword="true" /> if the message is a response to another message;
            ///     <see langword="false" />, otherwise.
            /// </param>
            public CtcpMessage(IrcUser source, IList<IIrcMessageTarget> targets, string tag, string data,
                bool isResponse)
            {
                Source = source;
                Targets = targets;
                Tag = tag;
                Data = data;
                IsResponse = isResponse;
            }

            /// <summary>
            ///     Returns a string representation of this instance.
            /// </summary>
            /// <returns>A string that represents this instance.</returns>
            public override string ToString()
            {
                return string.Format("{0} {1}", Tag, Data);
            }
        }
    }
}