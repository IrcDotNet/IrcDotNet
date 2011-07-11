using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

#if !SILVERLIGHT
using System.Net.Security;
#endif

namespace IrcDotNet
{

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ChannelListReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcChannelListReceivedEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelListReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="channels">A list of information about the channels that was returned by the server.</param>
        public IrcChannelListReceivedEventArgs(IList<IrcChannelInfo> channels)
            : base()
        {
            if (channels == null)
                throw new ArgumentNullException("channels");

            this.Channels = new ReadOnlyCollection<IrcChannelInfo>(channels);
        }

        /// <summary>
        /// Gets the list of information about the channels that was returned by the server.
        /// </summary>
        /// <value>The list of channels.</value>
        public IList<IrcChannelInfo> Channels
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ServerVersionInfoReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcServerVersionInfoEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServerVersionInfoEventArgs"/> class.
        /// </summary>
        /// <param name="version">The version of the server.</param>
        /// <param name="debugLevel">The debug level of the server.</param>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="comments">The comments about the server.</param>
        public IrcServerVersionInfoEventArgs(string version, string debugLevel, string serverName, string comments)
            : base()
        {
            if (version == null)
                throw new ArgumentNullException("version");
            if (debugLevel == null)
                throw new ArgumentNullException("debugLevel");
            if (serverName == null)
                throw new ArgumentNullException("serverName");
            if (comments == null)
                throw new ArgumentNullException("comments");

            this.Version = version;
            this.DebugLevel = debugLevel;
            this.ServerName = serverName;
            this.Comments = comments;
        }

        /// <summary>
        /// Gets the version of the server.
        /// </summary>
        /// <value>The version of the server.</value>
        public string Version
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the debug level of the server.
        /// </summary>
        /// <value>The debug level of the server.</value>
        public string DebugLevel
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the name of the server to which the version information applies.
        /// </summary>
        /// <value>The name of the server.</value>
        public string ServerName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the comments about the server.
        /// </summary>
        /// <value>The comments about the server.</value>
        public string Comments
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ServerTimeReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcServerTimeEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServerTimeEventArgs"/> class.
        /// </summary>
        /// <param name="serverName">The name of the server.</param>
        /// <param name="dateTime">The local date/time received from the server.</param>
        public IrcServerTimeEventArgs(string serverName, string dateTime)
            : base()
        {
            if (serverName == null)
                throw new ArgumentNullException("serverName");
            if (dateTime == null)
                throw new ArgumentNullException("dateTime");

            this.ServerName = serverName;
            this.DateTime = dateTime;
        }

        /// <summary>
        /// Gets the name of the server to which the version information applies.
        /// </summary>
        /// <value>The name of the server.</value>
        public string ServerName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the local date/time for the server.
        /// </summary>
        /// <value>The local date/time for the server.</value>
        public string DateTime
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ServerLinksListReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcServerLinksListReceivedEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServerLinksListReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="links">A list of information about the server links that was returned by the server.</param>
        public IrcServerLinksListReceivedEventArgs(IList<IrcServerInfo> links)
            : base()
        {
            if (links == null)
                throw new ArgumentNullException("links");

            this.Links = new ReadOnlyCollection<IrcServerInfo>(links);
        }

        /// <summary>
        /// Gets the list of information about the server links that was returned by the server
        /// </summary>
        /// <value>The list of server links.</value>
        public IList<IrcServerInfo> Links
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ServerStatsReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcServerStatsReceivedEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServerStatsReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="entries">A list of statistical entries that was returned by the server.</param>
        public IrcServerStatsReceivedEventArgs(IList<IrcServerStatisticalEntry> entries)
            : base()
        {
            if (entries == null)
                throw new ArgumentNullException("entries");

            this.Entries = new ReadOnlyCollection<IrcServerStatisticalEntry>(entries);
        }

        /// <summary>
        /// Gets the list of statistical entries that was returned by the server.
        /// </summary>
        /// <value>The list of statistical entries.</value>
        public IList<IrcServerStatisticalEntry> Entries
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// <inheritdoc select="/summary/node()"/>
    /// Gives the option to handle the preview event and thus stop the normal event from being raised.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcPreviewMessageEventArgs : IrcMessageEventArgs
    {

        /// <inheritdoc/>
        public IrcPreviewMessageEventArgs(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text,
            Encoding encoding)
            : base(source, targets, text, encoding)
        {
            this.Handled = false;
        }

        /// <summary>
        /// Gets or sets whether the event has been handled. If it is handled, the corresponding normal (non-preview)
        /// event is not raised.
        /// </summary>
        /// <value><see langword="true"/> if the event has been handled; <see langword="false"/>, otherwise.</value>
        public bool Handled
        {
            get;
            set;
        }

    }

    /// <summary>
    /// Provides data for events that are raised when an IRC message or notice is sent or received.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcMessageEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcMessageEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The text of the message.</param>
        /// <param name="encoding">The encoding of the message text.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targets"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public IrcMessageEventArgs(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text,
            Encoding encoding)
            : base()
        {
            if (targets == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");
            if (encoding == null)
                throw new ArgumentNullException("textEncoding");

            this.Source = source;
            this.Targets = new ReadOnlyCollection<IIrcMessageTarget>(targets);
            this.Text = text;
            this.Encoding = encoding;
        }

        /// <summary>
        /// Gets the text of the message in the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding in which to get the message text, or <see langword="null"/> to use the
        /// default encoding.</param>
        /// <returns>The text of the message.</returns>
        public string GetText(Encoding encoding = null)
        {
            return this.Text.ChangeEncoding(this.Encoding, encoding);
        }

        /// <summary>
        /// Gets the source of the message.
        /// </summary>
        /// <value>The source of the message.</value>
        public IIrcMessageSource Source
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a list of the targets of the message.
        /// </summary>
        /// <value>The targets of the message.</value>
        public IList<IIrcMessageTarget> Targets
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the text of the message.
        /// </summary>
        /// <value>The text of the message.</value>
        public string Text
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the encoding of the message text.
        /// </summary>
        /// <value>The encoding of the message text.</value>
        public Encoding Encoding
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.PingReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcChannelInvitationEventArgs : IrcChannelEventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelInvitationEventArgs"/> class.
        /// </summary>
        /// <param name="channel">The channel to which the recipient user is invited.</param>
        /// <param name="inviter">The user inviting the recipient user to the channel.</param>
        public IrcChannelInvitationEventArgs(IrcChannel channel, IrcUser inviter)
            : base(channel)
        {
            if (inviter == null)
                throw new ArgumentNullException("inviter");

            this.Inviter = inviter;
        }

        /// <summary>
        /// Gets the user inviting the recipient user to the channel
        /// </summary>
        /// <value>The inviter user.</value>
        public IrcUser Inviter
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that concern an <see cref="IrcChannelUser"/>.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcChannelUserEventArgs : IrcCommentEventArgs
    {

        /// <inheritdoc/>
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelUserEventArgs"/> class.
        /// </summary>
        /// <param name="channelUser">The channel user that the event concerns.</param>
        public IrcChannelUserEventArgs(IrcChannelUser channelUser, string comment = null)
            : base(comment)
        {
            if (channelUser == null)
                throw new ArgumentNullException("channelUser");

            this.ChannelUser = channelUser;
        }

        /// <summary>
        /// Gets the channel user that the event concerns.
        /// </summary>
        /// <value>The channel user that the event concerns.</value>
        public IrcChannelUser ChannelUser
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that concern an <see cref="IrcChannel"/>.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcChannelEventArgs : IrcCommentEventArgs
    {

        /// <inheritdoc/>
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelEventArgs"/> class.
        /// </summary>
        /// <param name="channel">The channel that the event concerns.</param>
        public IrcChannelEventArgs(IrcChannel channel, string comment = null)
            : base(comment)
        {
            if (channel == null)
                throw new ArgumentNullException("channel");

            this.Channel = channel;
        }

        /// <summary>
        /// Gets the channel that the event concerns.
        /// </summary>
        /// <value>The channel that the event concerns.</value>
        public IrcChannel Channel
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that concern an <see cref="IrcUser"/>.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcUserEventArgs : IrcCommentEventArgs
    {

        /// <inheritdoc/>
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcUserEventArgs"/> class.
        /// </summary>
        /// <param name="user">The user that the event concerns.</param>
        public IrcUserEventArgs(IrcUser user, string comment = null)
            : base(comment)
        {
            if (user == null)
                throw new ArgumentNullException("user");

            this.User = user;
        }

        /// <summary>
        /// Gets the user that the event concerns.
        /// </summary>
        /// <value>The user that the event concerns.</value>
        public IrcUser User
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that specify a comment.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcNameEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcNameEventArgs"/> class.
        /// </summary>
        /// <param name="name">The name that the event specified.</param>
        public IrcNameEventArgs(string name)
            : base()
        {
            this.Name = name;
        }

        /// <summary>
        /// Gets the name that the event specified.
        /// </summary>
        /// <value>The name that the event specified.</value>
        public string Name
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that specify a name.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcCommentEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcCommentEventArgs"/> class.
        /// </summary>
        /// <param name="comment">The comment that the event specified.</param>
        public IrcCommentEventArgs(string comment)
            : base()
        {
            this.Comment = comment;
        }

        /// <summary>
        /// Gets the comment that the event specified.
        /// </summary>
        /// <value>The comment that the event specified.</value>
        public string Comment
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.PingReceived"/> and <see cref="IrcClient.PongReceived"/> events.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcPingOrPongReceivedEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcPingOrPongReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="server">The name of the server that is the source of the ping or pong.</param>
        public IrcPingOrPongReceivedEventArgs(string server)
        {
            if (server == null)
                throw new ArgumentNullException("server");

            this.Server = server;
        }

        /// <summary>
        /// Gets the name of the server that is the source of the ping or pong.
        /// </summary>
        /// <value>The name of the server.</value>
        public string Server
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for events that specify information about a server.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcServerInfoEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcServerInfoEventArgs"/> class.
        /// </summary>
        /// <param name="address">The address of the server.</param>
        /// <param name="port">The port on which to connect to the server.</param>
        public IrcServerInfoEventArgs(string address, int port)
            : base()
        {
            if (address == null)
                throw new ArgumentNullException("address");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("port");

            this.Address = address;
            this.Port = port;
        }

        /// <summary>
        /// Gets the address of the server.
        /// </summary>
        /// <value>The address of the server.</value>
        public string Address
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the port on which to connect to the server.
        /// </summary>
        /// <value>The port on which to connect to the server.</value>
        public int Port
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ErrorMessageReceived"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcErrorMessageEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcErrorMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The error message given by the server.</param>
        public IrcErrorMessageEventArgs(string message)
            : base()
        {
            if (message == null)
                throw new ArgumentNullException("message");

            this.Message = message;
        }

        /// <summary>
        /// Gets the text of the error message.
        /// </summary>
        /// <value>The text of the error message.</value>
        public string Message
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ProtocolError"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcProtocolErrorEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcProtocolErrorEventArgs"/> class.
        /// </summary>
        /// <param name="code">The code.</param>
        /// <param name="parameters">The parameters.</param>
        /// <param name="message">The message.</param>
        public IrcProtocolErrorEventArgs(int code, IList<string> parameters, string message)
            : base()
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            if (message == null)
                throw new ArgumentNullException("message");

            this.Code = code;
            this.Parameters = new ReadOnlyCollection<string>(parameters);
            this.Message = message;
        }

        /// <summary>
        /// Gets or sets the numeric code that indicates the type of error.
        /// </summary>
        /// <value>The numeric code that indicates the type of error.</value>
        public int Code
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a list of the parameters of the error.
        /// </summary>
        /// <value>A lsit of the parameters of the error.</value>
        public IList<string> Parameters
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the text of the error message.
        /// </summary>
        /// <value>The text of the error message.</value>
        public string Message
        {
            get;
            private set;
        }

    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.RawMessageSent"/> and
    /// <see cref="IrcClient.RawMessageReceived"/> events.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcRawMessageEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcRawMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message that was sent/received.</param>
        /// <param name="rawContent">The raw content of the message.</param>
        public IrcRawMessageEventArgs(IrcClient.IrcMessage message, string rawContent)
            : base()
        {
            this.Message = message;
            this.RawContent = rawContent;
        }

        /// <summary>
        /// Gets the message that was sent/received by the client.
        /// </summary>
        /// <value>The message that was sent/received by the client.</value>
        public IrcClient.IrcMessage Message
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the raw content of the message.
        /// </summary>
        /// <value>The raw content of the message.</value>
        public string RawContent
        {
            get;
            private set;
        }

    }

#if !SILVERLIGHT

    /// <summary>
    /// Provides data for the <see cref="IrcClient.ValidateSslCertificate"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcValidateSslCertificateEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcValidateSslCertificateEventArgs"/> class.
        /// </summary>
        /// <param name="certificate">The certificate used to authenticate the remote party.</param>
        /// <param name="chain">The chain of certificate authorities.</param>
        /// <param name="sslPolicyErrors">The errors associated with the remote certificate.</param>
        public IrcValidateSslCertificateEventArgs(X509Certificate certificate, X509Chain chain,
            SslPolicyErrors sslPolicyErrors)
            : base()
        {
            this.Certificate = certificate;
            this.Chain = chain;
            this.SslPolicyErrors = sslPolicyErrors;
        }

        /// <summary>
        /// Gets the certificate used to authenticate the remote party..
        /// </summary>
        /// <value>The certificate.</value>
        public X509Certificate Certificate
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the chain of certificate authorities associated with the remote certificate.
        /// </summary>
        /// <value>The chain.</value>
        public X509Chain Chain
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the errors associated with the remote certificate.
        /// </summary>
        /// <value>The SSL policy errors.</value>
        public SslPolicyErrors SslPolicyErrors
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets whether the certificate given by the server is valid.
        /// </summary>
        /// <value><see langword="true"/> if the certificate is valid; <see langword="false"/>, otherwise.</value>
        public bool IsValid
        {
            get;
            set;
        }

    }

#endif

    /// <summary>
    /// Provides data for the <see cref="IrcClient.Error"/> event.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcErrorEventArgs : EventArgs
    {

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcErrorEventArgs"/> class.
        /// </summary>
        /// <param name="error">The error.</param>
        public IrcErrorEventArgs(Exception error)
            : base()
        {
            if (error == null)
                throw new ArgumentNullException("error");

            this.Error = error;
        }

        /// <summary>
        /// Gets the error encountered by the client.
        /// </summary>
        /// <value>The error encountered by the client.</value>
        public Exception Error
        {
            get;
            private set;
        }

    }

}
