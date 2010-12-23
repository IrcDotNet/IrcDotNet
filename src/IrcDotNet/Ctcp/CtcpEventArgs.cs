using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace IrcDotNet.Ctcp
{
/// <summary>
    /// Provides data for events that are raised when a CTCP message or notice is sent or received.
    /// </summary>
    public class CtcpMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpMessageEventArgs"/> class.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The text of the mesage.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targets"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
        public CtcpMessageEventArgs(IrcUser source, IList<IIrcMessageTarget> targets, string text)
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
        /// Gets the source of the message.
        /// </summary>
        /// <value>The source of the message.</value>
        public IrcUser Source
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
    /// Provides data for the <see cref="CtcpClient.TimeResponseReceived"/> event.
    /// </summary>
    public class CtcpTimeResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpTimeResponseReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="dateTime">The local date/time received from the user.</param>
        public CtcpTimeResponseReceivedEventArgs(IrcUser user, string dateTime)
            : base(user)
        {
            this.DateTime = dateTime;
        }

        /// <summary>
        /// Gets the local date/time for the user.
        /// </summary>
        /// <value>The local date/time for the user.</value>
        public string DateTime
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="CtcpClient.VersionResponseReceived"/> event.
    /// </summary>
    public class CtcpVersionResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpVersionResponseReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="versionInfo">The information about the client version.</param>
        public CtcpVersionResponseReceivedEventArgs(IrcUser user, string versionInfo)
            : base(user)
        {
            this.VersionInfo = versionInfo;
        }

        /// <summary>
        /// Gets the information about the client version of the user.
        /// </summary>
        /// <value>The ping time.</value>
        public string VersionInfo
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="CtcpClient.PingResponseReceived"/> event.
    /// </summary>
    public class CtcpPingResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpPingResponseReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="pingTime">The ping time.</param>
        public CtcpPingResponseReceivedEventArgs(IrcUser user, TimeSpan pingTime)
            : base(user)
        {
            this.PingTime = pingTime;
        }

        /// <summary>
        /// Gets the duration of time elapsed between the sending of the ping request and the receiving of the ping
        /// response.
        /// </summary>
        /// <value>The ping time.</value>
        public TimeSpan PingTime
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for events that indicate a response to a CTCP request.
    /// </summary>
    public class CtcpResponseReceivedEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpResponseReceivedEventArgs"/> class.
        /// </summary>
        /// <param name="user">The user from which the response was received.</param>
        public CtcpResponseReceivedEventArgs(IrcUser user)
        {
            this.User = user;
        }

        /// <summary>
        /// Gets the user from which the response was received.
        /// </summary>
        /// <value>The user from which the request was received.</value>
        public IrcUser User
        {
            get;
            private set;
        }
    }

    /// <summary>
    /// Provides data for the <see cref="CtcpClient.RawMessageSent"/> and
    /// <see cref="CtcpClient.RawMessageReceived"/> events.
    /// </summary>
    public class CtcpRawMessageEventArgs : EventArgs
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CtcpRawMessageEventArgs"/> class.
        /// </summary>
        /// <param name="message">The message that was sent/received.</param>
        public CtcpRawMessageEventArgs(CtcpClient.CtcpMessage message)
            : base()
        {
            this.Message = message;
        }

        /// <summary>
        /// Gets the message that was sent/received by the client.
        /// </summary>
        /// <value>The message that was sent/received by the client.</value>
        public CtcpClient.CtcpMessage Message
        {
            get;
            private set;
        }
    }
}
