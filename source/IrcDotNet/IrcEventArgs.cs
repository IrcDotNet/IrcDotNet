using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using static IrcDotNet.IrcClient;
#if !SILVERLIGHT
using System.Net.Security;

#endif

namespace IrcDotNet
{
    /// <summary>
    ///     Base class for all irc event args.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcEventArgs"/> class.
        /// </summary>
        /// <param name="ircMessage">The <see cref="IrcClient.IrcMessage"/> this event originates from.</param>
        public IrcEventArgs(IrcMessage ircMessage)
        {
            IrcMessage = ircMessage;
        }

        /// <summary>
        ///     Gets the source irc message.
        /// </summary>
        public IrcMessage IrcMessage { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.IrcNickChangedEventArgs" /> event.
    /// </summary>
    public class IrcNickChangedEventArgs : IrcEventArgs
    {
        /// <summary>
        /// Provides the new nickname.
        /// </summary>
        public readonly string NewNickName;

        /// <summary>
        /// Provides the old nickname.
        /// </summary>
        public readonly string OldNickName;

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcNickChangedEventArgs" /> class.
        /// </summary>
        /// <param name="newNickName"></param>
        /// <param name="oldNickName"></param>
        public IrcNickChangedEventArgs(IrcMessage ircMessage, string newNickName, string oldNickName) : base(ircMessage)
        {
            NewNickName = newNickName;
            OldNickName = oldNickName;
        }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ChannelListReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcChannelListReceivedEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcChannelListReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="channels">A list of information about the channels that was returned by the server.</param>
        public IrcChannelListReceivedEventArgs(IrcMessage ircMessage, IList<IrcChannelInfo> channels) : base(ircMessage)
        {
            if (channels == null)
                throw new ArgumentNullException("channels");

            Channels = new ReadOnlyCollection<IrcChannelInfo>(channels);
        }

        /// <summary>
        ///     Gets the list of information about the channels that was returned by the server.
        /// </summary>
        /// <value>The list of channels.</value>
        public IList<IrcChannelInfo> Channels { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ServerVersionInfoReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServerVersionInfoEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerVersionInfoEventArgs" /> class.
        /// </summary>
        /// <param name="version">The version of the server.</param>
        /// <param name="debugLevel">The debug level of the server.</param>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="comments">The comments about the server.</param>
        public IrcServerVersionInfoEventArgs(IrcMessage ircMessage, string version, string debugLevel, string serverName, string comments) : base(ircMessage)
        {
            if (version == null)
                throw new ArgumentNullException("version");
            if (debugLevel == null)
                throw new ArgumentNullException("debugLevel");
            if (serverName == null)
                throw new ArgumentNullException("serverName");
            if (comments == null)
                throw new ArgumentNullException("comments");

            Version = version;
            DebugLevel = debugLevel;
            ServerName = serverName;
            Comments = comments;
        }

        /// <summary>
        ///     Gets the version of the server.
        /// </summary>
        /// <value>The version of the server.</value>
        public string Version { get; private set; }

        /// <summary>
        ///     Gets the debug level of the server.
        /// </summary>
        /// <value>The debug level of the server.</value>
        public string DebugLevel { get; private set; }

        /// <summary>
        ///     Gets the name of the server to which the version information applies.
        /// </summary>
        /// <value>The name of the server.</value>
        public string ServerName { get; private set; }

        /// <summary>
        ///     Gets the comments about the server.
        /// </summary>
        /// <value>The comments about the server.</value>
        public string Comments { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ServerTimeReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServerTimeEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerTimeEventArgs" /> class.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="dateTime">The local date/time received from the server.</param>
        public IrcServerTimeEventArgs(IrcMessage ircMessage, string serverName, string dateTime) : base(ircMessage)
        {
            if (serverName == null)
                throw new ArgumentNullException("serverName");
            if (dateTime == null)
                throw new ArgumentNullException("dateTime");

            ServerName = serverName;
            DateTime = dateTime;
        }

        /// <summary>
        ///     Gets the name of the server to which the version information applies.
        /// </summary>
        /// <value>The name of the server.</value>
        public string ServerName { get; private set; }

        /// <summary>
        ///     Gets the local date/time for the server.
        /// </summary>
        /// <value>The local date/time for the server.</value>
        public string DateTime { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ServerLinksListReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServerLinksListReceivedEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerLinksListReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="links">A list of information about the server links that was returned by the server.</param>
        public IrcServerLinksListReceivedEventArgs(IrcMessage ircMessage, IList<IrcServerInfo> links) : base(ircMessage)
        {
            if (links == null)
                throw new ArgumentNullException("links");

            Links = new ReadOnlyCollection<IrcServerInfo>(links);
        }

        /// <summary>
        ///     Gets the list of information about the server links that was returned by the server
        /// </summary>
        /// <value>The list of server links.</value>
        public IList<IrcServerInfo> Links { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ServerStatsReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServerStatsReceivedEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerStatsReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="entries">A list of statistical entries that was returned by the server.</param>
        public IrcServerStatsReceivedEventArgs(IrcMessage ircMessage, IList<IrcServerStatisticalEntry> entries) : base(ircMessage)
        {
            if (entries == null)
                throw new ArgumentNullException("entries");

            Entries = new ReadOnlyCollection<IrcServerStatisticalEntry>(entries);
        }

        /// <summary>
        ///     Gets the list of statistical entries that was returned by the server.
        /// </summary>
        /// <value>The list of statistical entries.</value>
        public IList<IrcServerStatisticalEntry> Entries { get; private set; }
    }

    /// <summary>
    ///     <inheritdoc select="/summary/node()" />
    ///     Gives the option to handle the preview event and thus stop the normal event from being raised.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcPreviewMessageEventArgs : IrcMessageEventArgs
    {
        /// <inheritdoc />
        public IrcPreviewMessageEventArgs(IrcMessage ircMessage, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text,
            Encoding encoding)
            : base(ircMessage, source, targets, text, encoding)
        {
            Handled = false;
        }

        /// <summary>
        ///     Gets or sets whether the event has been handled. If it is handled, the corresponding normal (non-preview)
        ///     event is not raised.
        /// </summary>
        /// <value><see langword="true" /> if the event has been handled; <see langword="false" />, otherwise.</value>
        public bool Handled { get; set; }
    }

    /// <summary>
    ///     Provides data for events that are raised when an IRC message or notice is sent or received.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcMessageEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcMessageEventArgs" /> class.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The text of the message.</param>
        /// <param name="encoding">The encoding of the message text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targets" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
        public IrcMessageEventArgs(IrcMessage ircMessage, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text,
            Encoding encoding) : base(ircMessage)
        {
            if (targets == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");
            if (encoding == null)
                throw new ArgumentNullException("textEncoding");

            Source = source;
            Targets = new ReadOnlyCollection<IIrcMessageTarget>(targets);
            Text = text;
            Encoding = encoding;
        }

        /// <summary>
        ///     Gets the source of the message.
        /// </summary>
        /// <value>The source of the message.</value>
        public IIrcMessageSource Source { get; private set; }

        /// <summary>
        ///     Gets a list of the targets of the message.
        /// </summary>
        /// <value>The targets of the message.</value>
        public IList<IIrcMessageTarget> Targets { get; private set; }

        /// <summary>
        ///     Gets the text of the message.
        /// </summary>
        /// <value>The text of the message.</value>
        public string Text { get; }

        /// <summary>
        ///     Gets the encoding of the message text.
        /// </summary>
        /// <value>The encoding of the message text.</value>
        public Encoding Encoding { get; }

        /// <summary>
        ///     Gets the text of the message in the specified encoding.
        /// </summary>
        /// <param name="encoding">
        ///     The encoding in which to get the message text, or <see langword="null" /> to use the
        ///     default encoding.
        /// </param>
        /// <returns>The text of the message.</returns>
        public string GetText(Encoding encoding = null)
        {
            return Text.ChangeEncoding(Encoding, encoding);
        }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.PingReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcChannelInvitationEventArgs : IrcChannelEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcChannelInvitationEventArgs" /> class.
        /// </summary>
        /// <param name="channel">The channel to which the recipient user is invited.</param>
        /// <param name="inviter">The user inviting the recipient user to the channel.</param>
        public IrcChannelInvitationEventArgs(IrcMessage ircMessage, IrcChannel channel, IrcUser inviter, string comment = null) : base(ircMessage, channel, comment)
        {
            if (inviter == null)
                throw new ArgumentNullException("inviter");

            Inviter = inviter;
        }

        /// <summary>
        ///     Gets the user inviting the recipient user to the channel
        /// </summary>
        /// <value>The inviter user.</value>
        public IrcUser Inviter { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that concern an <see cref="IrcChannelUser" />.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcChannelUserEventArgs : IrcCommentEventArgs
    {
        /// <inheritdoc />
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcChannelUserEventArgs" /> class.
        /// </summary>
        /// <param name="channelUser">The channel user that the event concerns.</param>
        public IrcChannelUserEventArgs(IrcMessage ircMessage, IrcChannelUser channelUser, string comment = null) : base(ircMessage, comment)
        {
            if (channelUser == null)
                throw new ArgumentNullException("channelUser");

            ChannelUser = channelUser;
        }

        /// <summary>
        ///     Gets the channel user that the event concerns.
        /// </summary>
        /// <value>The channel user that the event concerns.</value>
        public IrcChannelUser ChannelUser { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that concern an <see cref="IrcChannel" />.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcChannelEventArgs : IrcCommentEventArgs
    {
        /// <inheritdoc />
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcChannelEventArgs" /> class.
        /// </summary>
        /// <param name="channel">The channel that the event concerns.</param>
        public IrcChannelEventArgs(IrcMessage ircMessage, IrcChannel channel, string comment = null)
            : base(ircMessage, comment)
        {
            if (channel == null)
                throw new ArgumentNullException("channel");

            Channel = channel;
        }

        /// <summary>
        ///     Gets the channel that the event concerns.
        /// </summary>
        /// <value>The channel that the event concerns.</value>
        public IrcChannel Channel { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that concern an <see cref="IrcUser" />.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcUserEventArgs : IrcCommentEventArgs
    {
        /// <inheritdoc />
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcUserEventArgs" /> class.
        /// </summary>
        /// <param name="user">The user that the event concerns, or <see langword="null" /> for no user.</param>
        public IrcUserEventArgs(IrcMessage ircMessage, IrcUser user, string comment = null)
            : base(ircMessage, comment)
        {
            User = user;
        }

        /// <summary>
        ///     Gets the user that the event concerns.
        /// </summary>
        /// <value>The user that the event concerns.</value>
        public IrcUser User { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that specify a comment.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcNameEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcNameEventArgs" /> class.
        /// </summary>
        /// <param name="name">The name that the event specified.</param>
        public IrcNameEventArgs(IrcMessage ircMessage, string name) : base(ircMessage)
        {
            Name = name;
        }

        /// <summary>
        ///     Gets the name that the event specified.
        /// </summary>
        /// <value>The name that the event specified.</value>
        public string Name { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that specify a name.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcCommentEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcCommentEventArgs" /> class.
        /// </summary>
        /// <param name="comment">The comment that the event specified.</param>
        public IrcCommentEventArgs(IrcMessage ircMessage, string comment) : base(ircMessage)
        {
            Comment = comment;
        }

        /// <summary>
        ///     Gets the comment that the event specified.
        /// </summary>
        /// <value>The comment that the event specified.</value>
        public string Comment { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.PingReceived" /> and <see cref="IrcClient.PongReceived" /> events.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcPingOrPongReceivedEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcPingOrPongReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="server">The name of the server that is the source of the ping or pong.</param>
        public IrcPingOrPongReceivedEventArgs(IrcMessage ircMessage, string server) : base(ircMessage)
        {
            if (server == null)
                throw new ArgumentNullException("server");

            Server = server;
        }

        /// <summary>
        ///     Gets the name of the server that is the source of the ping or pong.
        /// </summary>
        /// <value>The name of the server.</value>
        public string Server { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.PingReceived" /> events.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcPingReceivedEventArgs : IrcPingOrPongReceivedEventArgs
    {
        public IrcPingReceivedEventArgs(IrcMessage ircMessage, string server) : base(ircMessage, server)
        {
            SendPong = true;
        }

        /// <summary>
        ///     Gets or sets if we should send a Pong back
        /// </summary>
        /// <value>A value indicating sending a Pong.</value>
        public bool SendPong { get; set; }
    }

    /// <summary>
    ///     Provides data for events that specify information about a server.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServerInfoEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerInfoEventArgs" /> class.
        /// </summary>
        /// <param name="address">The address of the server.</param>
        /// <param name="port">The port on which to connect to the server.</param>
        public IrcServerInfoEventArgs(IrcMessage ircMessage, string address, int port) : base(ircMessage)
        {
            if (address == null)
                throw new ArgumentNullException("address");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("port");

            Address = address;
            Port = port;
        }

        /// <summary>
        ///     Gets the address of the server.
        /// </summary>
        /// <value>The address of the server.</value>
        public string Address { get; private set; }

        /// <summary>
        ///     Gets the port on which to connect to the server.
        /// </summary>
        /// <value>The port on which to connect to the server.</value>
        public int Port { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ErrorMessageReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcErrorMessageEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcErrorMessageEventArgs" /> class.
        /// </summary>
        /// <param name="message">The error message given by the server.</param>
        public IrcErrorMessageEventArgs(IrcMessage ircMessage, string message) : base(ircMessage)
        {
            if (message == null)
                throw new ArgumentNullException("message");

            Message = message;
        }

        /// <summary>
        ///     Gets the text of the error message.
        /// </summary>
        /// <value>The text of the error message.</value>
        public string Message { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ActiveCapabilitiesReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class ActiveCapabilitiesEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="ActiveCapabilitiesEventArgs" /> class.
        /// </summary>
        /// <param name="caps">The list of active capabilities</param>
        public ActiveCapabilitiesEventArgs(string[] caps)
        {
            if (caps == null)
                throw new ArgumentNullException("caps");

            Capabilities = caps;
        }

        /// <summary>
        ///     Gets the list of capabilities.
        /// </summary>
        /// <value>The list of capabilities.</value>
        public string[] Capabilities { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.CapabilityAcknowledged" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CapabilityAcknowledgedEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CapabilityAcknowledgedEventArgs" /> class.
        /// </summary>
        /// <param name="acknowledged">Whether (ACK) or not (NAK) the request has been acknowledged by the server</param>
        /// <param name="caps">The list of active capabilities</param>
        public CapabilityAcknowledgedEventArgs(bool acknowledged, string[] caps)
        {
            Capabilities = caps;
            Acknowledged = acknowledged;
        }

        /// <summary>
        ///     Gets the list of capabilities.
        /// </summary>
        /// <value>The list of capabilities.</value>
        public string[] Capabilities { get; private set; }

        /// <summary>
        ///     Gets whether (ACK) or not (NAK) the request has been acknowledged by the server.
        /// </summary>
        /// <value>Whether (ACK) or not (NAK) the request has been acknowledged by the server.</value>
        public bool Acknowledged { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ProtocolError" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcProtocolErrorEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcProtocolErrorEventArgs" /> class.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="message">The message.</param>
        public IrcProtocolErrorEventArgs(IrcMessage ircMessage, int code, IList<string> parameters, string message) : base(ircMessage)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            if (message == null)
                throw new ArgumentNullException("message");

            Code = code;
            Parameters = new ReadOnlyCollection<string>(parameters);
            Message = message;
        }

        /// <summary>
        ///     Gets or sets the numeric code that indicates the type of error.
        /// </summary>
        /// <value>The numeric code that indicates the type of error.</value>
        public int Code { get; private set; }

        /// <summary>
        ///     Gets a list of the parameters of the error.
        /// </summary>
        /// <value>A lsit of the parameters of the error.</value>
        public IList<string> Parameters { get; private set; }

        /// <summary>
        ///     Gets the text of the error message.
        /// </summary>
        /// <value>The text of the error message.</value>
        public string Message { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.RawMessageSent" /> and
    ///     <see cref="IrcClient.RawMessageReceived" /> events.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcRawMessageEventArgs : IrcEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcRawMessageEventArgs" /> class.
        /// </summary>
        /// <param name="rawContent">The raw content of the message.</param>
        public IrcRawMessageEventArgs(IrcMessage ircMessage, string rawContent) : base(ircMessage)
        {
            RawContent = rawContent;
        }

        [Obsolete("Accessor for backwards compatibility. Use IrcMessage instead.")]
        public IrcMessage Message => IrcMessage;

        /// <summary>
        ///     Gets the raw content of the message.
        /// </summary>
        /// <value>The raw content of the message.</value>
        public string RawContent { get; private set; }
    }

#if !SILVERLIGHT

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.ValidateSslCertificate" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcValidateSslCertificateEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcValidateSslCertificateEventArgs" /> class.
        /// </summary>
        /// <param name="certificate">The certificate used to authenticate the remote party.</param>
        /// <param name="chain">The chain of certificate authorities.</param>
        /// <param name="sslPolicyErrors">The errors associated with the remote certificate.</param>
        public IrcValidateSslCertificateEventArgs(X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
        {
            Certificate = certificate;
            Chain = chain;
            SslPolicyErrors = sslPolicyErrors;
        }

        /// <summary>
        ///     Gets the certificate used to authenticate the remote party..
        /// </summary>
        /// <value>The certificate.</value>
        public X509Certificate Certificate { get; private set; }

        /// <summary>
        ///     Gets the chain of certificate authorities associated with the remote certificate.
        /// </summary>
        /// <value>The chain.</value>
        public X509Chain Chain { get; private set; }

        /// <summary>
        ///     Gets the errors associated with the remote certificate.
        /// </summary>
        /// <value>The SSL policy errors.</value>
        public SslPolicyErrors SslPolicyErrors { get; private set; }

        /// <summary>
        ///     Gets or sets whether the certificate given by the server is valid.
        /// </summary>
        /// <value><see langword="true" /> if the certificate is valid; <see langword="false" />, otherwise.</value>
        public bool IsValid { get; set; }
    }

#endif

    /// <summary>
    ///     Provides data for the <see cref="IrcClient.Error" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcErrorEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcErrorEventArgs" /> class.
        /// </summary>
        /// <param name="error">The error.</param>
        public IrcErrorEventArgs(Exception error)
        {
            if (error == null)
                throw new ArgumentNullException("error");

            Error = error;
        }

        /// <summary>
        ///     Gets the error encountered by the client.
        /// </summary>
        /// <value>The error encountered by the client.</value>
        public Exception Error { get; private set; }
    }
}
