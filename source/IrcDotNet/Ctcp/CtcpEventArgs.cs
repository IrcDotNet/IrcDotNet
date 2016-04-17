using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace IrcDotNet.Ctcp
{
    /// <summary>
    ///     Provides data for events that are raised when a CTCP message or notice is sent or received.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpMessageEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpMessageEventArgs" /> class.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A list of the targets of the message.</param>
        /// <param name="text">The text of the message.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targets" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="text" /> is <see langword="null" />.</exception>
        public CtcpMessageEventArgs(IrcUser source, IList<IIrcMessageTarget> targets, string text)
        {
            if (targets == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            Source = source;
            Targets = new ReadOnlyCollection<IIrcMessageTarget>(targets);
            Text = text;
        }

        /// <summary>
        ///     Gets the source of the message.
        /// </summary>
        /// <value>The source of the message.</value>
        public IrcUser Source { get; private set; }

        /// <summary>
        ///     Gets a list of the targets of the message.
        /// </summary>
        /// <value>The targets of the message.</value>
        public IList<IIrcMessageTarget> Targets { get; private set; }

        /// <summary>
        ///     Gets the text of the message.
        /// </summary>
        /// <value>The text of the message.</value>
        public string Text { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="CtcpClient.TimeResponseReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpTimeResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpTimeResponseReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="dateTime">The local date/time received from the user.</param>
        public CtcpTimeResponseReceivedEventArgs(IrcUser user, string dateTime)
            : base(user)
        {
            DateTime = dateTime;
        }

        /// <summary>
        ///     Gets the local date/time for the user.
        /// </summary>
        /// <value>The local date/time for the user.</value>
        public string DateTime { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="CtcpClient.VersionResponseReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpVersionResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpVersionResponseReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="versionInfo">The information about the client version.</param>
        public CtcpVersionResponseReceivedEventArgs(IrcUser user, string versionInfo)
            : base(user)
        {
            VersionInfo = versionInfo;
        }

        /// <summary>
        ///     Gets the information about the client version of the user.
        /// </summary>
        /// <value>The ping time.</value>
        public string VersionInfo { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="CtcpClient.ErrorMessageReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpErrorMessageReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpErrorMessageReceivedEventArgs" /> class,
        ///     specifying that no error occurred.
        /// </summary>
        /// <param name="noErrorMessage">The message indicating that no error occurred.</param>
        public CtcpErrorMessageReceivedEventArgs(IrcUser user, string noErrorMessage)
            : base(user)
        {
            ErrorOccurred = false;
            FailedQuery = null;
            ErrorMessage = noErrorMessage;
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpErrorMessageReceivedEventArgs" /> class,
        ///     specifying the query that failed with an error message.
        /// </summary>
        /// <param name="failedQuery">A string containing the query that failed.</param>
        /// <param name="errorMessage">The message describing the error that occurred for the remote user.</param>
        public CtcpErrorMessageReceivedEventArgs(IrcUser user, string failedQuery, string errorMessage)
            : base(user)
        {
            ErrorOccurred = true;
            FailedQuery = failedQuery;
            ErrorMessage = errorMessage;
        }

        /// <summary>
        ///     Gets a value indicating whether an error occurred or the user confirmed that no error occurred.
        /// </summary>
        /// <value>
        ///     <see langword="true" /> if an error occurred; <see langword="false" /> if the remote user confirmed
        ///     that no error occurred.
        /// </value>
        public bool ErrorOccurred { get; private set; }

        /// <summary>
        ///     Gets a string containing the query that failed
        /// </summary>
        /// <value>The failed query.</value>
        public string FailedQuery { get; private set; }

        /// <summary>
        ///     Gets message describing the error that occurred for the remote user.
        /// </summary>
        /// <value>The error message.</value>
        public string ErrorMessage { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="CtcpClient.PingResponseReceived" /> event.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpPingResponseReceivedEventArgs : CtcpResponseReceivedEventArgs
    {
        /// <inheritdoc />
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpPingResponseReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="pingTime">The ping time.</param>
        public CtcpPingResponseReceivedEventArgs(IrcUser user, TimeSpan pingTime)
            : base(user)
        {
            PingTime = pingTime;
        }

        /// <summary>
        ///     Gets the duration of time elapsed between the sending of the ping request and the receiving of the ping
        ///     response.
        /// </summary>
        /// <value>The ping time.</value>
        public TimeSpan PingTime { get; private set; }
    }

    /// <summary>
    ///     Provides data for events that indicate a response to a CTCP request.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpResponseReceivedEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpResponseReceivedEventArgs" /> class.
        /// </summary>
        /// <param name="user">The user from which the response was received.</param>
        public CtcpResponseReceivedEventArgs(IrcUser user)
        {
            User = user;
        }

        /// <summary>
        ///     Gets the user from which the response was received.
        /// </summary>
        /// <value>The user from which the request was received.</value>
        public IrcUser User { get; private set; }
    }

    /// <summary>
    ///     Provides data for the <see cref="CtcpClient.RawMessageSent" /> and
    ///     <see cref="CtcpClient.RawMessageReceived" /> events.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class CtcpRawMessageEventArgs : EventArgs
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="CtcpRawMessageEventArgs" /> class.
        /// </summary>
        /// <param name="message">The message that was sent/received.</param>
        public CtcpRawMessageEventArgs(CtcpClient.CtcpMessage message)
        {
            Message = message;
        }

        /// <summary>
        ///     Gets the message that was sent/received by the client.
        /// </summary>
        /// <value>The message that was sent/received by the client.</value>
        public CtcpClient.CtcpMessage Message { get; private set; }
    }
}