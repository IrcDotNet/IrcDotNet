using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Provides data for the <see cref="IrcClient.ChannelListReceived"/> event.
    /// </summary>
    public class IrcChannelListReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelListReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="channels">A list of information about the channels returned by the server.</param>
        public IrcChannelListReceivedEventArgs(IList<IrcChannelInfo> channels)
            : base()
        {
            if (channels == null)
                throw new ArgumentNullException("channels");

            this.Channels = channels;
        }

        /// <summary>
        /// Gets the list of information about the channels returned by the server.
        /// </summary>
        /// <value>The list of channels.</value>
        public IList<IrcChannelInfo> Channels
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// <inheritdoc select="/summary/node()"/>
    /// Gives the option to handle the preview event and thus stop the normal event from being raised.
    /// </summary>
    public class IrcPreviewMessageEventArgs : IrcMessageEventArgs
    {
        /// <inheritdoc/>
        public IrcPreviewMessageEventArgs(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
            : base(source, targets, text)
        {
            this.Handled = false;
        }

        /// <summary>
        /// Gets or sets whether the event has been handled. If it is handled, the corresponding normal (non-preview)
        /// event is not raised.
        /// </summary>
        /// <value><see langword="true"/> if the event has been handled; otherwise, <see langword="false"/>.</value>
        public bool Handled
        {
            get;
            set;
        }
    }

    /// <summary>
    /// Provides data for events that are raised when a message or notice is sent or received.
    /// </summary>
    public class IrcMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcMessageEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The text of the mesage</param>
        /// <exception cref="ArgumentNullException"><paramref name="targets"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public IrcMessageEventArgs(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
            : base()
        {
            if (targets == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            this.Source = source;
            this.Targets = new ReadOnlyCollection<IIrcMessageTarget>(targets);
            this.Text = text;
        }

        /// <summary>
        /// Gets the text of the message in the specified encoding.
        /// </summary>
        /// <param name="encoding">The encoding in which to get the message text, or <see langword="null"/> to use the
        /// default encoding.</param>
        /// <returns>The text of the message.</returns>
        public string GetText(Encoding encoding = null)
        {
            return this.Text.ChangeEncoding(Encoding.Default, encoding);
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
    }

    /// <summary>
    /// Provides data for events that concern an <see cref="IrcChannelUser"/>.
    /// </summary>
    public class IrcChannelUserEventArgs : IrcCommentEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelUserEventArgs"/> class.
        /// </summary>
        /// <param name="channelUser">The channel user that the event concerns</param>
        public IrcChannelUserEventArgs(IrcChannelUser channelUser, string comment)
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
    public class IrcChannelEventArgs : IrcCommentEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcChannelEventArgs"/> class.
        /// </summary>
        /// <param name="channel">The channel that the event concerns.</param>
        public IrcChannelEventArgs(IrcChannel channel, string comment)
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
    public class IrcUserEventArgs : IrcCommentEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcUserEventArgs"/> class.
        /// </summary>
        /// <param name="user">The user that the event concerns.</param>
        public IrcUserEventArgs(IrcUser user, string comment)
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
    /// Provides data for the <see cref="IrcClient.RawMessageReceived"/> event.
    /// </summary>
    public class IrcRawMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="IrcRawMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message.</param>
        public IrcRawMessageEventArgs(IrcClient.IrcMessage message)
            : base()
        {
            this.Message = message;
        }

        /// <summary>
        /// Gets the message received from the server.
        /// </summary>
        /// <value>The message received from the server.</value>
        public IrcClient.IrcMessage Message
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="IrcClient.Error"/> event.
    /// </summary>
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
