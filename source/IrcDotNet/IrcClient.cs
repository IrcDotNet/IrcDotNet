using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents a client that communicates with a server using the IRC (Internet Relay Chat) protocol.
    ///     Do not inherit this class unless the protocol itself is being extended.
    /// </summary>
    /// <remarks>
    ///     All collection objects must be locked on the <see cref="ICollection.SyncRoot" /> object for thread-safety.
    ///     They can however be used safely without locking within event handlers.
    /// </remarks>
    /// <threadsafety static="true" instance="true" />
    [DebuggerDisplay("{" + nameof(ToString) + "(), nq}")]
    public abstract partial class IrcClient : IDisposable
    {
        // Maximum number of parameters that can be sent in single raw message.        
        private const int MAX_PARAMS_COUNT = 15;

        // Default port on which to connect to IRC server.
        public static readonly int DefaultPort = 6667;

        #region Regexes

        // Regular expressions used for extracting information from protocol messages.
        protected static readonly string RegexNickName = @"(?<nick>[^!@]+)";
        protected static readonly string RegexUserName = @"(?<user>[^!@]+)";
        protected static readonly string RegexHostName = @"(?<host>[^%@]+)";
        protected static readonly string RegexChannelName = @"(?<channel>[#+!&].+)";
        protected static readonly string RegexTargetMask = @"(?<targetMask>[$#].+)";
        protected static readonly string RegexServerName = @"(?<server>[^%@]+?\.[^%@]*)";
        protected static readonly string RegexNickNameId = $@"{RegexNickName}(?:(?:!{RegexUserName})?@{RegexHostName})?";
        protected static readonly string RegexUserNameId = $@"{RegexUserName}(?:(?:%{RegexHostName})?@{RegexServerName}|%{RegexHostName})";
        protected static readonly string RegexMessagePrefix = $@"^(?:{RegexServerName}|{RegexNickNameId})$";
        protected static readonly string RegexMessageTarget = $@"^(?:{RegexChannelName}|{RegexUserNameId}|{RegexTargetMask}|{RegexNickNameId})$";

        protected static readonly string IsupportPrefix = @"\((?<modes>.*)\)(?<prefixes>.*)";

        #endregion

        // Non-zero if object has been disposed or is currently being disposed.
        private int disposedFlag;

        // Dictionary of message processor routines, keyed by their command names.
        private readonly Dictionary<string, MessageProcessor> messageProcessors;

        // Dictionary of message processor routines, keyed by their numeric codes (000 to 999).
        private readonly Dictionary<int, MessageProcessor> numericMessageProcessors;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcClient" /> class.
        /// </summary>
        public IrcClient()
        {
            TextEncoding = Encoding.UTF8;
            messageProcessors = new Dictionary<string, MessageProcessor>(StringComparer.OrdinalIgnoreCase);
            numericMessageProcessors = new Dictionary<int, MessageProcessor>(1000);
            FloodPreventer = null;

            InitializeMessageProcessors();
        }

#if DEBUG
        public string ClientId { get; set; }
#endif

        /// <summary>
        ///     Gets whether the client connection has been registered with the server.
        /// </summary>
        /// <value>
        ///     <see langword="true" /> if the connection has been registered; <see langword="false" />, otherwise.
        /// </value>
        public bool IsRegistered => isRegistered;

        /// <summary>
        ///     Gets the local user. The local user is the user managed by this client connection.
        /// </summary>
        /// <value>The local user.</value>
        public IrcLocalUser LocalUser => localUser;

        /// <summary>
        ///     Gets the 'Welcome' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Welcome' message received from the server..</value>
        public string WelcomeMessage { get; protected set; }

        /// <summary>
        ///     Gets the 'Your Host' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Your Host' message received from the server.</value>
        public string YourHostMessage { get; private set; }

        /// <summary>
        ///     Gets the 'Created' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Created' message received from the server.</value>
        public string ServerCreatedMessage { get; private set; }

        /// <summary>
        ///     Gets the host name of the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The host name given received from the server.</value>
        public string ServerName { get; private set; }

        /// <summary>
        ///     Gets the version of the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The version given received from the server.</value>
        public string ServerVersion { get; private set; }

        /// <summary>
        ///     Gets a collection of the user modes available on the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of user modes available on the server.</value>
        public IEnumerable<char> ServerAvailableUserModes { get; private set; }

        /// <summary>
        ///     Gets a collection of the channel modes available on the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of channel modes available on the server.</value>
        public IEnumerable<char> ServerAvailableChannelModes { get; private set; }

        /// <summary>
        ///     Gets a dictionary of the features supported by the server, keyed by feature name, as returned by the
        ///     ISUPPORT message.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A dictionary of features supported by the server.</value>
        public Collections.ReadOnlyDictionary<string, string> ServerSupportedFeatures { get; private set; }

        /// <summary>
        ///     Gets a collection of channel modes that apply to users in a channel.
        /// </summary>
        /// <value>A collection of channel modes that apply to users.</value>
        public ReadOnlyCollection<char> ChannelUserModes { get; private set; }

        /// <summary>
        ///     Gets the Message of the Day (MOTD) sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public string MessageOfTheDay => motdBuilder.ToString();

        /// <summary>
        ///     Gets information about the IRC network that is given by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public IrcNetworkInfo? NetworkInformation => networkInformation;

        /// <summary>
        ///     Gets a collection of all channels known to the client.
        /// </summary>
        /// <value>A collection of known channels.</value>
        public IrcChannelCollection Channels { get; private set; }

        /// <summary>
        ///     Gets a collection of all users known to the client, including the local user.
        /// </summary>
        /// <value>A collection of known users.</value>
        public IrcUserCollection Users { get; private set; }

        /// <summary>
        ///     Gets or sets an object that limits the rate of outgoing messages in order to prevent flooding the server.
        ///     The value is <see langword="null" /> by default, which indicates that no flood prevention should be
        ///     performed.
        /// </summary>
        /// <value>A flood preventer object.</value>
        public IIrcFloodPreventer FloodPreventer { get; set; }

        /// <summary>
        ///     Gets or sets the text encoding to use for reading from and writing to the network data stream.
        /// </summary>
        /// <value>The text encoding of the network stream.</value>
        public Encoding TextEncoding { get; set; }

        /// <summary>
        ///     Gets whether the client is currently connected to a server.
        /// </summary>
        /// <value><see langword="true" /> if the client is connected; <see langword="false" />, otherwise.</value>
        public abstract bool IsConnected { get; }

        /// <summary>
        ///     Gets whether the <see cref="IrcClient" /> object has been disposed.
        /// </summary>
        /// <value>
        ///     <see langword="true" /> if the <see cref="IrcClient" /> object has been disposed;
        ///     <see langword="false" />, otherwise.
        /// </value>
        protected bool IsDisposed => Interlocked.CompareExchange(ref disposedFlag, 0, 0) > 0;

        /// <summary>
        ///     Releases all resources used by the <see cref="IrcClient" /> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="IrcClient" /> class.
        /// </summary>
        ~IrcClient() => Dispose(false);

        /// <summary>
        ///     Releases all resources used by the <see cref="IrcClient" />.
        /// </summary>
        /// <param name="disposing">
        ///     <see langword="true" /> if the consumer is actively disposing the object;
        ///     <see langword="false" /> if the garbage collector is finalizing the object.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            Interlocked.CompareExchange(ref disposedFlag, 1, 0);
        }

        /// <inheritdoc cref="ListChannels(IEnumerable{string})" />
        public void ListChannels(params string[] channelNames)
        {
            CheckDisposed();

            if (channelNames == null)
                throw new ArgumentNullException(nameof(channelNames));

            SendMessageList(channelNames);
        }

        /// <summary>
        ///     Requests a list of information about the specified (or all) channels on the network.
        /// </summary>
        /// <param name="channelNames">
        ///     The names of the channels to list, or <see langword="null" /> to list all channels
        ///     on the network.
        /// </param>
        public void ListChannels(IEnumerable<string> channelNames = null)
        {
            CheckDisposed();

            SendMessageList(channelNames);
        }

        /// <summary>
        ///     Requests the Message of the Day (MOTD) from the specified server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server from which to request the MOTD, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetMessageOfTheDay(string targetServer = null)
        {
            CheckDisposed();

            SendMessageMotd(targetServer);
        }

        /// <summary>
        ///     Requests statistics about the connected IRC network.
        ///     If <paramref name="serverMask" /> is specified, then the server only returns information about the part of
        ///     the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        ///     network
        /// </summary>
        /// <param name="serverMask">
        ///     A wildcard expression for matching against server names, or <see langword="null" />
        ///     to match the entire network.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetNetworkInfo(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLUsers(serverMask, targetServer);
        }

        /// <summary>
        ///     Requests the version of the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose version to request.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerVersion(string targetServer = null)
        {
            CheckDisposed();

            SendMessageVersion(targetServer);
        }

        /// <summary>
        ///     Requests statistics about the specified server.
        /// </summary>
        /// <param name="query">
        ///     The query character that indicates which server statistics to return.
        ///     The set of valid query characters is dependent on the implementation of the particular IRC server.
        /// </param>
        /// <param name="targetServer">The name of the server whose statistics to request.</param>
        /// <remarks>
        ///     The server may not accept the command if <paramref name="query" /> is unspecified.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerStatistics(char? query = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageStats(query?.ToString(), targetServer);
        }

        /// <summary>
        ///     Requests a list of all servers known by the target server.
        ///     If <paramref name="serverMask" /> is specified, then the server only returns information about the part of
        ///     the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        ///     network.
        /// </summary>
        /// <param name="serverMask">
        ///     A wildcard expression for matching against server names, or <see langword="null" />
        ///     to match the entire network.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the request, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerLinks(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLinks(serverMask, targetServer);
        }

        /// <summary>
        ///     Requests the local time on the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose local time to request.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerTime(string targetServer = null)
        {
            CheckDisposed();

            SendMessageTime(targetServer);
        }

        /// <summary>
        ///     Sends a ping to the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server to ping.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void Ping(string targetServer = null)
        {
            CheckDisposed();

            SendMessagePing(localUser.NickName, targetServer);
        }

        /// <summary>
        ///     Sends a Who query to the server targeting the specified channel or user masks.
        /// </summary>
        /// <param name="mask">
        ///     A wildcard expression for matching against channel names; or if none can be found,
        ///     host names, server names, real names, and nick names of users. If the value is <see langword="null" />,
        ///     all users are matched.
        /// </param>
        /// <param name="onlyOperators">
        ///     <see langword="true" /> to match only server operators;
        ///     <see langword="false" /> to match all users.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void QueryWho(string mask = null, bool onlyOperators = false)
        {
            CheckDisposed();

            SendMessageWho(mask, onlyOperators);
        }

        /// <inheritdoc cref="QueryWhoIs(IEnumerable{string})" />
        public void QueryWhoIs(params string[] nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException(nameof(nickNameMasks));

            QueryWhoIs((IEnumerable<string>) nickNameMasks);
        }

        /// <overloads>Sends a Who Is query to the server.</overloads>
        /// <summary>
        ///     Sends a Who Is query to server targeting the specified nick name masks.
        /// </summary>
        /// <param name="nickNameMasks">
        ///     A collection of wildcard expressions for matching against nick names of users.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNameMasks" /> is <see langword="null" />.</exception>
        public void QueryWhoIs(IEnumerable<string> nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException(nameof(nickNameMasks));

            SendMessageWhoIs(nickNameMasks);
        }

        /// <inheritdoc cref="QueryWhoWas(IEnumerable{string}, int)" />
        public void QueryWhoWas(params string[] nickNames)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException(nameof(nickNames));

            QueryWhoWas((IEnumerable<string>) nickNames);
        }

        /// <summary>
        ///     Sends a Who Was query to server targeting the specified nick names.
        /// </summary>
        /// <param name="nickNames">The nick names of the users to query.</param>
        /// <param name="entriesCount">
        ///     The maximum number of entries to return from the query. A negative value
        ///     specifies to return an unlimited number of entries.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNames" /> is <see langword="null" />.</exception>
        public void QueryWhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException(nameof(nickNames));

            SendMessageWhoWas(nickNames, entriesCount);
        }

        /// <inheritdoc cref="Quit(string)" />
        /// <summary>
        ///     Quits the server, giving the specified comment. Waits the specified duration of time before forcibly
        ///     disconnecting.
        /// </summary>
        /// <param name="timeout">The number of milliseconds to wait before forcibly disconnecting.</param>
        /// <param name="comment">The comment to issue when quitting</param>
        /// <remarks>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public virtual void Quit(int timeout, string comment = null) => SendMessageQuit(comment);

        /// <summary>
        ///     Quits the server, giving the specified comment.
        /// </summary>
        /// <param name="comment">The comment to send to the server.</param>
        /// <remarks>
        ///     Note that because this message is not sent immediately, calling <see cref="Disconnect" /> immediately after
        ///     this will likely disconnect the client before it has a chance to quit the server properly.
        ///     Quitting the server should automatically disconnect the client.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void Quit(string comment = null) => Quit(0, comment);

        /// <summary>
        ///     Sends the specified raw message to the server.
        /// </summary>
        /// <param name="message">The text (single line) of the message to send the server.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
        public void SendRawMessage(string message)
        {
            CheckDisposed();

            if (message == null)
                throw new ArgumentNullException(nameof(message));

            WriteMessage(message);
        }

        protected void Connect(IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException(nameof(registrationInfo));

            CheckRegistrationInfo(registrationInfo);
            ResetState();
        }

        /// <summary>
        ///     Disconnects asynchronously from the server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <remarks>
        ///     This method closes the client connection immediately and forcibly, and does not send a quit message to the
        ///     server. To disconnect from the IRC server gracefully, call <see cref="Quit(string)" /> and wait for the
        ///     connection to be closed.
        /// </remarks>
        public virtual void Disconnect()
        {
            CheckDisposed();
        }

        protected void CheckDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            if (!IsDisposed && IsConnected)
                return string.Format("{0}@{1}", localUser.UserName, ServerName);
            return "(Not connected)";
        }
    }
}