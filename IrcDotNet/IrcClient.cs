using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    using Common.Collections;

    /// <summary>
    /// Provides methods for communicating with a server using the IRC (Internet Relay Chat) protocol.
    /// Do not inherit unless the protocol itself is being extended.
    /// </summary>
    [DebuggerDisplay("{ToString(), nq}")]
    public abstract partial class IrcClient : IDisposable
    {
        public static readonly int DefaultPort = 6667;

        public static readonly int maxParamsCount = 15;

        // Regular expressions used for extracting information from protocol messages.
        private static readonly string regexNickName;
        private static readonly string regexUserName;
        private static readonly string regexHostName;
        private static readonly string regexChannelName;
        private static readonly string regexTargetMask;
        private static readonly string regexServerName;
        private static readonly string regexNickNameId;
        private static readonly string regexUserNameId;
        private static readonly string regexMessagePrefix;
        private static readonly string regexMessageTarget;

        private static readonly string isupportPrefix;

        static IrcClient()
        {
            regexNickName = @"(?<nick>[^!@]+)";
            regexUserName = @"(?<user>[^!@]+)";
            regexHostName = @"(?<host>[^%@]+)";
            regexChannelName = @"(?<channel>[#+!&].+)";
            regexTargetMask = @"(?<targetMask>[$#].+)";
            regexServerName = @"(?<server>[^%@]+?\.[^%@]*)";
            regexNickNameId = string.Format(@"{0}(?:(?:!{1})?@{2})?", regexNickName, regexUserName, regexHostName);
            regexUserNameId = string.Format(@"{0}(?:(?:%{1})?@{2}|%{1})", regexUserName, regexHostName,
                regexServerName);
            regexMessagePrefix = string.Format(@"^(?:{0}|{1})$", regexServerName, regexNickNameId);
            regexMessageTarget = string.Format(@"^(?:{0}|{1}|{2}|{3})$", regexChannelName, regexUserNameId,
                regexTargetMask, regexNickNameId);

            isupportPrefix = @"\((?<modes>.*)\)(?<prefixes>.*)";
        }

        // Internal collection of all known servers.
        private Collection<IrcServer> servers;

        // True if connection has been registered with server;
        private bool isRegistered;

        // Stores information about local user.
        protected IrcLocalUser localUser;

        // Internal and exposable dictionary of various features supported by server.
        private Dictionary<string, string> serverSupportedFeatures;
        private ReadOnlyDictionary<string, string> serverSupportedFeaturesReadOnly;

        // Internal and exposable collection of channel modes that apply to users in a channel.
        private Collection<char> channelUserModes;
        private ReadOnlyCollection<char> channelUserModesReadOnly;

        // Dictionary of nick name prefixes (keys) and their corresponding channel modes.
        private Dictionary<char, char> channelUserModesPrefixes;

        // Builds MOTD (message of the day) string as it is received from server.
        private StringBuilder motdBuilder;

        // Information about the IRC network given by the server.
        private IrcNetworkInfo networkInformation;

        // Internal and exposable collection of all currently joined channels.
        private ObservableCollection<IrcChannel> channels;
        private IrcChannelCollection channelsReadOnly;

        // Internal and exposable collection of all known users.
        protected ObservableCollection<IrcUser> users;
        private IrcUserCollection usersReadOnly;

        // List of information about channels returned by server in response to last LIST message.
        private List<IrcChannelInfo> listedChannels;

        // Dictionary of message processor routines, keyed by their command names.
        private Dictionary<string, MessageProcessor> messageProcessors;

        // Dictionary of message processor routines, keyed by their numeric codes (000 to 999).
        private Dictionary<int, MessageProcessor> numMessageProcessors;

        // Prevents client from flooding server with messages by limiting send rate.
        protected IIrcFloodPreventer floodPreventer;

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcClient"/> class.
        /// </summary>
        public IrcClient()
        {
            this.messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
            this.numMessageProcessors = new Dictionary<int, MessageProcessor>(1000);
            this.floodPreventer = null;

            InitializeMessageProcessors();
            ResetState();
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="IrcClient"/> class.
        /// </summary>
        ~IrcClient()
        {
            Dispose(false);
        }

#if DEBUG
        public string ClientId
        {
            get;
            set;
        }
#endif

        /// <summary>
        /// Gets whether the client connection has been registered with the server.
        /// </summary>
        /// <value><see langword="true"/> if the connection has been registered; <see langword="false"/>, otherwise.
        /// </value>
        public bool IsRegistered
        {
            get { return this.isRegistered; }
        }

        /// <summary>
        /// Gets the local user. The local user is the user managed by this client connection.
        /// </summary>
        /// <value>The local user.</value>
        public IrcLocalUser LocalUser
        {
            get { return this.localUser; }
        }

        /// <summary>
        /// Gets the Welcome message sent by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The server Welcome message.</value>
        public string WelcomeMessage
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the Your Host message sent by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The server Your Host message.</value>
        public string YourHostMessage
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the Created message sent by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The server Created message.</value>
        public string ServerCreatedMessage
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the host name of the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The server host name.</value>
        public string ServerName
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the version of the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The server version.</value>
        public string ServerVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a collection of the user modes available on the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of user modes available on the server.</value>
        public IEnumerable<char> ServerAvailableUserModes
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a collection of the channel modes available on the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of channel modes available on the server.</value>
        public IEnumerable<char> ServerAvailableChannelModes
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets a dictionary of the features supported by the server, keyed by feature name, as returned by the
        /// ISUPPORT message.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A dictionary of features supported by the server.</value>
        public ReadOnlyDictionary<string, string> ServerSupportedFeatures
        {
            get { return this.serverSupportedFeaturesReadOnly; }
        }

        /// <summary>
        /// Gets a collection of channel modes that apply to users in a channel.
        /// </summary>
        /// <value>A collection of channel modes that apply to users.</value>
        public ReadOnlyCollection<char> ChannelUserModes
        {
            get { return this.channelUserModesReadOnly; }
        }

        /// <summary>
        /// Gets the Message of the Day (MOTD) sent by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public string MessageOfTheDay
        {
            get { return this.motdBuilder.ToString(); }
        }

        /// <summary>
        /// Gets information about the IRC network that is given by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public IrcNetworkInfo? NetworkInformation
        {
            get { return this.networkInformation; }
        }

        /// <summary>
        /// Gets a collection of all channels known to the client.
        /// </summary>
        /// <value>A collection of known channels.</value>
        public IrcChannelCollection Channels
        {
            get { return this.channelsReadOnly; }
        }

        /// <summary>
        /// Gets a collection of all users known to the client, including the local user.
        /// </summary>
        /// <value>A collection of known users.</value>
        public IrcUserCollection Users
        {
            get { return this.usersReadOnly; }
        }

        /// <summary>
        /// Gets or sets an object that limits the rate of outgoing messages in order to prevent flooding the server.
        /// The value is <see langword="null"/> by default, which indicates that no flood prevention should be
        /// performed.
        /// </summary>
        /// <value>A flood preventer object.</value>
        public IIrcFloodPreventer FloodPreventer
        {
            get { return floodPreventer; }
            set { this.floodPreventer = value; }
        }

        /// <summary>
        /// Gets whether the client is currently connected to a server.
        /// </summary>
        /// <value><see langword="true"/> if the client is connected; <see langword="false"/>, otherwise.</value>
        public abstract bool IsConnected
        {
            get;
        }

        /// <summary>
        /// Gets whether the <see cref="IrcClient"/> object has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if the <see cref="IrcClient"/> object has been disposed;
        /// <see langword="false"/>, otherwise.</value>
        public bool IsDisposed
        {
            get;
            protected set;
        }

        /// <summary>
        /// Releases all resources used by the <see cref="IrcClient"/> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all resources used by the <see cref="IrcClient"/>.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> if the user is actively disposing the object;
        /// <see langword="false"/> if the garbage collector is finalising the object.</param>
        protected virtual void Dispose(bool disposing)
        {
        }

        /// <summary>
        /// Occurs when the client has connected to the server.
        /// </summary>
        /// <remarks>
        /// Note that the <see cref="LocalUser"/> object is not set at this point, but is accessible when the
        /// <see cref="Registered"/> event is raised.
        /// </remarks>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        /// Occurs when the client has failed to connect to the server.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> ConnectFailed;

        /// <summary>
        /// Occurs when the client has disconnected from the server.
        /// </summary>
        public event EventHandler<EventArgs> Disconnected;

        /// <summary>
        /// Occurs when the client encounters an error during execution.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> Error;

        /// <summary>
        /// Occurs when the SSL certificate received from the server should be validated.
        /// The certificate is automatically validated if this event is not handled.
        /// </summary>
        public event EventHandler<IrcValidateSslCertificateEventArgs> ValidateSslCertificate;

        /// <summary>
        /// Occurs when a raw message has been sent to the server.
        /// </summary>
        public event EventHandler<IrcRawMessageEventArgs> RawMessageSent;

        /// <summary>
        /// Occurs when a raw message has been received from the server.
        /// </summary>
        public event EventHandler<IrcRawMessageEventArgs> RawMessageReceived;

        /// <summary>
        /// Occurs when a protocol (numeric) error is received from the server.
        /// </summary>
        public event EventHandler<IrcProtocolErrorEventArgs> ProtocolError;

        /// <summary>
        /// Occurs when an error message (ERROR command) is received from the server.
        /// </summary>
        public event EventHandler<IrcErrorMessageEventArgs> ErrorMessageReceived;

        /// <summary>
        /// Occurs when the connection has been registered.
        /// </summary>
        public event EventHandler<EventArgs> Registered;

        /// <summary>
        /// Occurs when the client information has been received from the server, following registration.
        /// </summary>
        /// <remarks>
        /// Client information is accessible via <see cref="WelcomeMessage"/>, <see cref="YourHostMessage"/>,
        /// <see cref="ServerCreatedMessage"/>, <see cref="ServerName"/>, <see cref="ServerVersion"/>,
        /// <see cref="ServerAvailableUserModes"/>, and <see cref="ServerAvailableChannelModes"/>.
        /// </remarks>
        public event EventHandler<EventArgs> ClientInfoReceived;

        /// <summary>
        /// Occurs when a bounce message is received from the server, telling the client to connect to a new server.
        /// </summary>
        public event EventHandler<IrcServerInfoEventArgs> ServerBounce;

        /// <summary>
        /// Occurs when a list of features supported by the server (ISUPPORT) has been received.
        /// This event may be raised more than once after registration, depending on the size of the list received.
        /// </summary>
        public event EventHandler<EventArgs> ServerSupportedFeaturesReceived;

        /// <summary>
        /// Occurs when a ping query is received from the server.
        /// The client automatically replies to pings from the server; this event is only a notification.
        /// </summary>
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PingReceived;

        /// <summary>
        /// Occurs when a pong reply is received from the server.
        /// </summary>
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PongReceived;

        /// <summary>
        /// Occurs when the Message of the Day (MOTD) has been received from the server.
        /// </summary>
        public event EventHandler<EventArgs> MotdReceived;

        /// <summary>
        /// Occurs when information about the IRC network has been received from the server.
        /// </summary>
        public event EventHandler<EventArgs> NetworkInformationReceived;

        /// <summary>
        /// Occurs when information about a specific server on the IRC network has been received from the server.
        /// </summary>
        public event EventHandler<IrcServerVersionInfoEventArgs> ServerVersionInfoReceived;

        /// <summary>
        /// Occurs when the local date/time of a specific server has been received from the server.
        /// </summary>
        public event EventHandler<IrcServerTimeEventArgs> ServerTimeReceived;

        /// <summary>
        /// Occurs when a reply to a Who query has been received from the server.
        /// </summary>
        public event EventHandler<IrcNameEventArgs> WhoReplyReceived;

        /// <summary>
        /// Occurs when a reply to a Who Is query has been received from the server.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> WhoIsReplyReceived;

        /// <summary>
        /// Occurs when a reply to a Who Was query has been received from the server.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> WhoWasReplyReceived;

        /// <summary>
        /// Occurs when a list of channels has been received from the server in response to a query.
        /// </summary>
        public event EventHandler<IrcChannelListReceivedEventArgs> ChannelListReceived;

        /// <inheritdoc cref="ListChannels(IEnumerable{string})"/>
        public void ListChannels(params string[] channelNames)
        {
            CheckDisposed();

            if (channelNames == null)
                throw new ArgumentNullException("channelNames");

            SendMessageList((IEnumerable<string>)channelNames);
        }

        /// <summary>
        /// Requests a list of information about the specified (or all) channels on the network.
        /// </summary>
        /// <param name="channelNames">The names of the channels to list, or <see langword="null"/> to list all channels
        /// on the network.</param>
        public void ListChannels(IEnumerable<string> channelNames = null)
        {
            CheckDisposed();

            SendMessageList(channelNames);
        }

        /// <summary>
        /// Requests the Message of the Day (MOTD) from the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server from which to request the MOTD, or <see langword="null"/>
        /// for the current server.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetMessageOfTheDay(string targetServer = null)
        {
            CheckDisposed();

            SendMessageMotd(targetServer);
        }

        /// <summary>
        /// Requests statistics about the connected IRC network.
        /// If <paramref name="serverMask"/> is specified, then the server only returns information about the part of
        /// the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        /// network
        /// </summary>
        /// <param name="serverMask">A wildcard expression for matching against server names, or <see langword="null"/>
        /// to match the entire network.</param>
        /// <param name="targetServer">The name of the server to which to forward the message, or <see langword="null"/>
        /// for the current server.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetNetworkInfo(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLUsers(serverMask, targetServer);
        }

        /// <summary>
        /// Requests the version of the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose version to request.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetServerVersion(string targetServer = null)
        {
            CheckDisposed();

            SendMessageVersion(targetServer);
        }

        /// <summary>
        /// Requests statistics about the specified server.
        /// </summary>
        /// <param name="query">The query that indicates to the server what statistics to return.
        /// The syntax for this value is dependent on the implementation of the server, but should support the following
        /// query characters:
        /// <list type="bullet">
        ///   <listheader>
        ///     <term>Character</term>
        ///     <description>Query</description>
        ///   </listheader>
        ///   <item>
        ///     <term>l</term>
        ///     <description>A list of connections of the server and information about them.</description>
        ///   </item>
        ///   <item>
        ///     <term>m</term>
        ///     <description>The usage count for each of the commands supported by the server.</description>
        ///   </item>
        ///   <item>
        ///     <term>o</term>
        ///     <description>A list of all server operators.</description>
        ///   </item>
        ///   <item>
        ///     <term>u</term>
        ///     <description>The duration for which the server has been running since its last start.</description>
        ///   </item>
        /// </list></param>
        /// <param name="targetServer">The name of the server whose statistics to request.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetServerInfo(string query = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageStats(query, targetServer);
        }

        /// <summary>
        /// Requests a list of all servers known by the target server.
        /// If <paramref name="serverMask"/> is specified, then the server only returns information about the part of
        /// the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        /// network.
        /// </summary>
        /// <param name="serverMask">A wildcard expression for matching against server names, or <see langword="null"/>
        /// to match the entire network.</param>
        /// <param name="targetServer">The name of the server to which to forward the request, or <see langword="null"/>
        /// for the current server.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetServerLinks(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLinks(serverMask, targetServer);
        }

        /// <summary>
        /// Requests the local time on the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose local time to request.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void GetServerTime(string targetServer = null)
        {
            CheckDisposed();

            SendMessageTime(targetServer);
        }

        /// <summary>
        /// Sends a ping to the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server to ping.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void Ping(string targetServer = null)
        {
            CheckDisposed();

            SendMessagePing(this.localUser.NickName, targetServer);
        }

        /// <summary>
        /// Sends a Who query to the server targeting the specified channel or user masks.
        /// </summary>
        /// <param name="mask">A wildcard expression for matching against channel names; or if none can be found,
        /// host names, server names, real names, and nick names of users. If the value is <see langword="null"/>,
        /// all users are matched.</param>
        /// <param name="onlyOperators"><see langword="true"/> to match only server operators; 
        /// <see langword="false"/> to match all users.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void QueryWho(string mask = null, bool onlyOperators = false)
        {
            CheckDisposed();

            SendMessageWho(mask, onlyOperators);
        }

        /// <inheritdoc cref="QueryWhoIs(IEnumerable{string})"/>
        public void QueryWhoIs(params string[] nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException("nickNames");

            QueryWhoIs((IEnumerable<string>)nickNameMasks);
        }

        /// <overloads>Sends a Who Is query to the server.</overloads>
        /// <summary>
        /// Sends a Who Is query to server targeting the specified nick name masks.
        /// </summary>
        /// <param name="nickNameMasks">A collection of wildcard expressions for matching against nick names of users.
        /// </param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNameMasks"/> is <see langword="null"/>.</exception>
        public void QueryWhoIs(IEnumerable<string> nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException("nickNames");

            SendMessageWhoIs(nickNameMasks);
        }

        /// <inheritdoc cref="QueryWhoWas(IEnumerable{string}, int)"/>
        public void QueryWhoWas(params string[] nickNames)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException("nickNames");

            QueryWhoWas((IEnumerable<string>)nickNames);
        }

        /// <summary>
        /// Sends a Who Was query to server targeting the specified nick names.
        /// </summary>
        /// <param name="nickNames">The nick names of the users to query.</param>
        /// <param name="entriesCount">The maximum number of entries to return from the query. A negative value
        /// specifies to return an unlimited number of entries.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNames"/> is <see langword="null"/>.</exception>
        public void QueryWhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException("nickNames");

            SendMessageWhoWas(nickNames, entriesCount);
        }

        /// <inheritdoc cref="Quit(string)"/>
        /// <summary>
        /// Quits the server, giving the specified comment. Waits the specified time before forcibly disconnecting.
        /// </summary>
        /// <param name="timeout">The number of milliseconds to wait before forcibly disconnecting.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public virtual void Quit(int timeout, string comment = null)
        {
            SendMessageQuit(comment);
        }

        /// <summary>
        /// Quits the server, giving the specified comment.
        /// </summary>
        /// <param name="comment">The comment to send to the server.</param>
        /// <remarks>
        /// Note that because this message is not sent immediately, calling <see cref="Disconnect"/> immediately after
        /// this will likely disconnect the client before it has a chance to send the command.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void Quit(string comment = null)
        {
            Quit(0, comment);
        }

        /// <summary>
        /// Sends the specified raw message to the server.
        /// </summary>
        /// <param name="message">The text (single line) of the message to send the server.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="message"/> is <see langword="null"/>.</exception>
        public void SendRawMessage(string message)
        {
            CheckDisposed();

            if (message == null)
                throw new ArgumentNullException("message");

            WriteMessage(message);
        }

        #region Proxy Methods

        internal void SetTopic(string channel, string topic = null)
        {
            SendMessageTopic(channel, topic);
        }

        internal void GetChannelModes(IrcChannel channel, string modes = null)
        {
            SendMessageChannelMode(channel.Name, modes);
        }

        internal void SetChannelModes(IrcChannel channel, string modes, IEnumerable<string> modeParameters = null)
        {
            SendMessageChannelMode(channel.Name, modes, modeParameters);
        }

        internal void Invite(IrcChannel channel, string userNickName)
        {
            SendMessageInvite(channel.Name, userNickName);
        }

        internal void Kick(IrcChannel channel, IEnumerable<string> usersNickNames, string comment = null)
        {
            SendMessageKick(channel.Name, usersNickNames, comment);
        }

        internal void Kick(IEnumerable<IrcChannelUser> channelUsers, string comment = null)
        {
            SendMessageKick(channelUsers.Select(cu => Tuple.Create(cu.Channel.Name, cu.User.NickName)), comment);
        }

        internal void Join(IEnumerable<string> channels)
        {
            SendMessageJoin(channels);
        }

        internal void Join(IEnumerable<Tuple<string, string>> channels)
        {
            SendMessageJoin(channels);
        }

        internal void Leave(IEnumerable<string> channels, string comment = null)
        {
            SendMessagePart(channels, comment);
        }

        internal void SendPrivateMessage(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
            SendMessagePrivateMessage(targetsNamesArray, text);
            this.localUser.HandleMessageSent(targets, text);
        }

        internal void SendNotice(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
            SendMessageNotice(targetsNamesArray, text);
            this.localUser.HandleNoticeSent(targets, text);
        }

        internal void SetAway(string text)
        {
            SendMessageAway(text);
        }

        internal void UnsetAway()
        {
            SendMessageAway();
        }

        internal void SetNickName(string nickName)
        {
            SendMessageNick(nickName);
        }

        internal void GetLocalUserModes(IrcLocalUser user)
        {
            SendMessageUserMode(user.NickName);
        }

        internal void SetLocalUserModes(IrcLocalUser user, string modes)
        {
            SendMessageUserMode(user.NickName, modes);
        }

        #endregion

        /// <summary>
        /// Handles the specified parameter of an ISUPPORT message received from the server upon registration.
        /// </summary>
        /// <param name="paramName">The name of the parameter.</param>
        /// <param name="paramValue">The value of the parameter, or <see langword="null"/> if it does not have a value.
        /// </param>
        protected bool HandleISupportParameter(string paramName, string paramValue)
        {
            if (paramName == null)
                throw new ArgumentNullException("paramName");
            if (paramName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "paramName");

            switch (paramName.ToLower())
            {
                case "prefix":
                    var prefixValueMatch = Regex.Match(paramValue, isupportPrefix); ;
                    var prefixes = prefixValueMatch.Groups["prefixes"].GetValue();
                    var modes = prefixValueMatch.Groups["modes"].GetValue();

                    if (prefixes.Length != modes.Length)
                        throw new ProtocolViolationException(Properties.Resources.ErrorMessageISupportPrefixInvalid);
                    this.channelUserModes.Clear();
                    this.channelUserModes.AddRange(modes);
                    this.channelUserModesPrefixes.Clear();
                    for (int i = 0; i < prefixes.Length; i++)
                        this.channelUserModesPrefixes.Add(prefixes[i], modes[i]);

                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Extracts the the mode and nick name of a user from the specified value.
        /// </summary>
        /// <param name="input">The input value, containing a nick name optionally prefixed by a mode character.</param>
        /// <returns>A 2-tuple of the nick name and user mode.</returns>
        protected Tuple<string, string> GetUserModeAndNickName(string input)
        {
            if (input == null)
                throw new ArgumentNullException("input");
            if (input.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "input");

            char mode;
            if (this.channelUserModesPrefixes.TryGetValue(input[0], out mode))
                return Tuple.Create(input.Substring(1), mode.ToString());
            else
                return Tuple.Create(input, string.Empty);
        }

        /// <summary>
        /// Gets a collection of mode characters and mode parameters from the specified mode parameters.
        /// Combines multiple mode strings into a single mode string.
        /// </summary>
        /// <param name="messageParameters">A collection of message parameters, which consists of mode strings and mode
        /// parameters. A mode string is of the form `( "+" / "-" ) *( mode character )`, and specifies mode changes.
        /// A mode parameter is arbitrary text associated with a certain mode.</param>
        /// <returns>A 2-tuple of a single mode string and a collection of mode parameters.
        /// Each mode parameter corresponds to a single mode character, in the same order.</returns>
        protected Tuple<string, IEnumerable<string>> GetModeAndParameters(IEnumerable<string> messageParameters)
        {
            if (messageParameters == null)
                throw new ArgumentNullException("messageParameters");

            var modes = new StringBuilder();
            var modeParameters = new List<string>();
            foreach (var p in messageParameters)
            {
                if (p == null)
                    break;
                else if (p.Length == 0)
                    continue;
                else if (p[0] == '+' || p[0] == '-')
                    modes.Append(p);
                else
                    modeParameters.Add(p);
            }
            return Tuple.Create(modes.ToString(), (IEnumerable<string>)modeParameters.AsReadOnly());
        }

        /// <summary>
        /// Gets a list of channel objects from the specified comma-separated list of channel names.
        /// </summary>
        /// <param name="namesList">A value that contains a comma-separated list of names of channels.</param>
        /// <returns>A list of channel objects that corresponds to the given list of channel names.</returns>
        protected IEnumerable<IrcChannel> GetChannelsFromList(string namesList)
        {
            if (namesList == null)
                throw new ArgumentNullException("namesList");

            return namesList.Split(',').Select(n => GetChannelFromName(n));
        }

        /// <summary>
        /// Gets a list of user objedcts from the specified comma-separated list of nick names.
        /// </summary>
        /// <param name="nickNamesList">A value that contains a comma-separated list of nick names of users.</param>
        /// <returns>A list of user objects that corresponds to the given list of nick names.</returns>
        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
            if (nickNamesList == null)
                throw new ArgumentNullException("nickNamesList");

            return nickNamesList.Split(',').Select(n => this.users.Single(u => u.NickName == n));
        }

        /// <summary>
        /// Determines whether the specified name refers to a channel.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns><see langword="true"/> if the specified name represents a channel; <see langword="false"/>,
        /// otherwise.</returns>
        protected bool IsChannelName(string name)
        {
            if (name == null)
                throw new ArgumentNullException("name");

            return Regex.IsMatch(name, regexChannelName);
        }

        /// <summary>
        /// Gets the type of the channel from the specified character.
        /// </summary>
        /// <param name="type">A character that represents the type of the channel.
        /// The character may be one of the following:
        /// <list type="bullet">
        ///     <listheader>
        ///         <term>Character</term>
        ///         <description>Channel type</description>
        ///     </listheader>
        ///     <item>
        ///         <term>=</term>
        ///         <description>Public channel</description>
        ///     </item>
        ///     <item>
        ///         <term>*</term>
        ///         <description>Private channel</description>
        ///     </item>
        ///     <item>
        ///         <term>@</term>
        ///         <description>Secret channel</description>
        ///     </item>
        /// </list></param>
        /// <returns>The channel type that corresponds to the specified character.</returns>
        /// <exception cref="ArgumentException"><paramref name="type"/> does not correspond to any known channel type.
        /// </exception>
        protected IrcChannelType GetChannelType(char type)
        {
            switch (type)
            {
                case '=':
                    return IrcChannelType.Public;
                case '*':
                    return IrcChannelType.Private;
                case '@':
                    return IrcChannelType.Secret;
                default:
                    throw new ArgumentException(string.Format(
                        Properties.Resources.ErrorMessageInvalidChannelType, type), "type");
            }
        }

        /// <summary>
        /// Gets the target of a message from the specified name.
        /// A message target may be an <see cref="IrcUser"/>, <see cref="IrcChannel"/>, or <see cref="IrcTargetMask"/>.
        /// </summary>
        /// <param name="targetName">The name of the target.</param>
        /// <returns>The target object that corresponds to the given name.</returns>
        /// <exception cref="ArgumentException"><paramref name="targetName"/> does not represent a valid message target.
        /// </exception>
        protected IIrcMessageTarget GetMessageTarget(string targetName)
        {
            if (targetName == null)
                throw new ArgumentNullException("targetName");
            if (targetName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "targetName");

            // Check whether target name represents channel, user, or target mask.
            var targetNameMatch = Regex.Match(targetName, regexMessageTarget);
            var channelName = targetNameMatch.Groups["channel"].GetValue();
            var nickName = targetNameMatch.Groups["nick"].GetValue();
            var userName = targetNameMatch.Groups["user"].GetValue();
            var hostName = targetNameMatch.Groups["host"].GetValue();
            var serverName = targetNameMatch.Groups["server"].GetValue();
            var targetMask = targetNameMatch.Groups["targetMask"].GetValue();
            if (channelName != null)
            {
                return GetChannelFromName(channelName);
            }
            else if (nickName != null)
            {
                // Find user by nick name. If no user exists in list, create it and set its properties.
                bool createdNew;
                var user = GetUserFromNickName(nickName, true, out createdNew);
                if (createdNew)
                {
                    user.UserName = userName;
                    user.HostName = hostName;
                }
                return user;
            }
            else if (userName != null)
            {
                // Find user by user  name. If no user exists in list, create it and set its properties.
                bool createdNew;
                var user = GetUserFromNickName(nickName, true, out createdNew);
                if (createdNew)
                {
                    user.HostName = hostName;
                }
                return user;
            }
            else if (targetMask != null)
            {
                return new IrcTargetMask(targetMask);
            }
            else
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidSource, targetName), "targetName");
            }
        }

        /// <summary>
        /// Gets the source of a message from the specified prefix.
        /// A message source may be a <see cref="IrcUser"/> or <see cref="IrcServer"/>.
        /// </summary>
        /// <param name="prefix">The raw prefix of the message.</param>
        /// <returns>The message source that corresponds to the specified prefix. The object is an instance of
        /// <see cref="IrcUser"/> or <see cref="IrcServer"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="prefix"/> does not represent a valid message source.
        /// </exception>
        protected IIrcMessageSource GetSourceFromPrefix(string prefix)
        {
            if (prefix == null)
                return null;
            if (prefix.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "prefix");

            // Check whether prefix represents server or user.
            var prefixMatch = Regex.Match(prefix, regexMessagePrefix);
            var serverName = prefixMatch.Groups["server"].GetValue();
            var nickName = prefixMatch.Groups["nick"].GetValue();
            var userName = prefixMatch.Groups["user"].GetValue();
            var hostName = prefixMatch.Groups["host"].GetValue();
            if (serverName != null)
            {
                return GetServerFromHostName(serverName);
            }
            else if (nickName != null)
            {
                // Find user by nick name. If no user exists in list, create it and set its properties.
                bool createdNew;
                var user = GetUserFromNickName(nickName, true, out createdNew);
                if (createdNew)
                {
                    user.UserName = userName;
                    user.HostName = hostName;
                }
                return user;
            }
            else
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidSource, prefix), "prefix");
            }
        }

        /// <inheritdoc cref="GetServerFromHostName(string, out bool)"/>
        protected IrcServer GetServerFromHostName(string hostName)
        {
            bool createdNew;
            return GetServerFromHostName(hostName, out createdNew);
        }

        /// <summary>
        /// Gets the server with the specified host name, creating it if necessary.
        /// </summary>
        /// <param name="hostName">The host name of the server.</param>
        /// <param name="createdNew"><see langword="true"/> if the server object was created during the call;
        /// <see langword="false"/>, otherwise.</param>
        /// <returns>The server object that corresponds to the specified host name.</returns>
        protected IrcServer GetServerFromHostName(string hostName, out bool createdNew)
        {
            if (hostName == null)
                throw new ArgumentNullException("hostName");
            if (hostName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "hostName");

            // Search for server  with given name in list of known servers. If it does not exist, add it.
            var server = this.servers.SingleOrDefault(s => s.HostName == hostName);
            if (server == null)
            {
                server = new IrcServer(hostName);
                this.servers.Add(server);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return server;
        }

        /// <inheritdoc cref="GetChannelFromName(string, out bool)"/>
        protected IrcChannel GetChannelFromName(string channelName)
        {
            bool createdNew;
            return GetChannelFromName(channelName, out createdNew);
        }

        /// <summary>
        /// Gets the channel with the specified name, creating it if necessary.
        /// </summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <param name="createdNew"><see langword="true"/> if the channel object was created during the call;
        /// <see langword="false"/>, otherwise.</param>
        /// <returns>The channel object that corresponds to the specified name.</returns>
        protected IrcChannel GetChannelFromName(string channelName, out bool createdNew)
        {
            if (channelName == null)
                throw new ArgumentNullException("channelName");
            if (channelName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "channelName");

            // Search for channel with given name in list of known channel. If it does not exist, add it.
            var channel = this.channels.SingleOrDefault(c => c.Name == channelName);
            if (channel == null)
            {
                channel = new IrcChannel(channelName);
                this.channels.Add(channel);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return channel;
        }

        /// <inheritdoc cref="GetUserFromNickName(string, bool, out bool)"/>
        protected IrcUser GetUserFromNickName(string nickName, bool isOnline = true)
        {
            bool createdNew;
            return GetUserFromNickName(nickName, isOnline, out createdNew);
        }

        /// <summary>
        /// Gets the user with the specified nick name, creating it if necessary.
        /// </summary>
        /// <param name="nickName">The nick name of the user.</param>
        /// <param name="isOnline"><see langword="true"/> if the user is currently online;
        /// <see langword="false"/>, if the user is currently offline.
        /// The <see cref="IrcUser.IsOnline"/> property of the user object is set to this value.</param>
        /// <param name="createdNew"><see langword="true"/> if the user object was created during the call;
        /// <see langword="false"/>, otherwise.</param>
        /// <returns>The user object that corresponds to the specified nick name.</returns>
        protected IrcUser GetUserFromNickName(string nickName, bool isOnline, out bool createdNew)
        {
            if (nickName == null)
                throw new ArgumentNullException("nickName");
            if (nickName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "nickName");

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            var user = this.users.SingleOrDefault(u => u.NickName == nickName);
            if (user == null)
            {
                user = new IrcUser();
                user.NickName = nickName;
                this.users.Add(user);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            user.IsOnline = isOnline;
            return user;
        }

        /// <inheritdoc cref="GetUserFromUserName(string, out bool)"/>
        protected IrcUser GetUserFromUserName(string userName)
        {
            bool createdNew;
            return GetUserFromUserName(userName, out createdNew);
        }

        /// <summary>
        /// Gets the user with the specified user name, creating it if necessary.
        /// </summary>
        /// <param name="userName">The user name of the user.</param>
        /// <param name="createdNew"><see langword="true"/> if the user object was created during the call;
        /// <see langword="false"/>, otherwise.</param>
        /// <returns>The user object that corresponds to the specified user name.</returns>
        protected IrcUser GetUserFromUserName(string userName, out bool createdNew)
        {
            if (userName == null)
                throw new ArgumentNullException("userName");
            if (userName.Length == 0)
                throw new ArgumentException(Properties.Resources.ErrorMessageValueCannotBeEmptyString, "userName");

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            var user = this.users.SingleOrDefault(u => u.UserName == userName);
            if (user == null)
            {
                user = new IrcUser();
                user.UserName = userName;
                this.users.Add(user);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return user;
        }

        protected int GetNumericUserMode(ICollection<char> modes)
        {
            var value = 0;
            if (modes == null)
                return value;
            if (modes.Contains('w'))
                value |= 0x02;
            if (modes.Contains('i'))
                value |= 0x04;
            return value;
        }

        protected void ResetState()
        {
            this.servers = new Collection<IrcServer>();
            this.isRegistered = false;
            this.localUser = null;
            this.serverSupportedFeatures = new Dictionary<string, string>();
            this.serverSupportedFeaturesReadOnly = new ReadOnlyDictionary<string, string>(this.serverSupportedFeatures);
            this.channelUserModes = new Collection<char>() {
                'o', 'v' };
            this.channelUserModesReadOnly = new ReadOnlyCollection<char>(this.channelUserModes);
            this.channelUserModesPrefixes = new Dictionary<char, char>() {
                { '@', 'o' }, { '+', 'v' } };
            this.motdBuilder = new StringBuilder();
            this.networkInformation = new IrcNetworkInfo();
            this.channels = new ObservableCollection<IrcChannel>();
            this.channelsReadOnly = new IrcChannelCollection(this, this.channels);
            this.users = new ObservableCollection<IrcUser>();
            this.usersReadOnly = new IrcUserCollection(this, this.users);
            this.listedChannels = new List<IrcChannelInfo>();
        }

        protected void InitializeMessageProcessors()
        {
            this.GetMethodAttributes<MessageProcessorAttribute, MessageProcessor>().ForEach(item =>
                {
                    var attribute = item.Item1;
                    var methodDelegate = item.Item2;

                    var commandRangeParts = attribute.Command.Split('-');
                    if (commandRangeParts.Length == 2)
                    {
                        // Numeric command range was specified.
                        var commandRangeStart = int.Parse(commandRangeParts[0]);
                        var commandRangeEnd = int.Parse(commandRangeParts[1]);
                        for (int code = commandRangeStart; code <= commandRangeEnd; code++)
                            this.numMessageProcessors.Add(code, methodDelegate);
                    }
                    else if (commandRangeParts.Length == 1)
                    {
                        // Single command was specified. Check whether it is numeric or alphabetic.
                        int commandCode;
                        if (int.TryParse(attribute.Command, out commandCode))
                            // Numeric
                            this.numMessageProcessors.Add(commandCode, methodDelegate);
                        else
                            // Alphabetic
                            this.messageProcessors.Add(attribute.Command, methodDelegate);
                    }
                    else
                    {
                        throw new ProtocolViolationException(string.Format(
                            Properties.Resources.ErrorMessageInvalidCommandDefinition, attribute.Command));
                    }
                });
        }

        protected void ReadMessage(IrcMessage message, string line)
        {
            OnRawMessageReceived(new IrcRawMessageEventArgs(message, line));

            // Try to find corresponding message processor for command of given message.
            MessageProcessor messageProcessor;
            int commandCode;
            if (this.messageProcessors.TryGetValue(message.Command, out messageProcessor) ||
                (int.TryParse(message.Command, out commandCode) &&
                this.numMessageProcessors.TryGetValue(commandCode, out messageProcessor)))
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
                Debug.WriteLine(string.Format("Unknown IRC message command '{0}'.", message.Command));
            }
        }

        /// <inheritdoc cref="WriteMessage(string, string, string[])"/>
        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            WriteMessage(prefix, command, parameters.ToArray());
        }

        /// <inheritdoc cref="WriteMessage(IrcMessage)"/>
        /// <param name="prefix">The message prefix that represents the source of the message.</param>
        /// <param name="command">The name of the command.</param>
        /// <param name="parameters">A collection of the parameters to the command.</param>
        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            var message = new IrcMessage(this, prefix, command, parameters.ToArray());
            if (message.Source == null)
                message.Source = this.localUser;
            WriteMessage(message);
        }

        /// <inheritdoc cref="WriteMessage(string)"/>
        /// <summary>
        /// Writes the specified message (prefix, command, and parameters) to the network stream.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="message"/> contains more than 15 many parameters. -or-
        /// The value of <see cref="IrcMessage.Command"/> of <paramref name="message"/> is invalid. -or-
        /// The value of one of the items of <see cref="IrcMessage.Parameters"/> of <paramref name="message"/> is
        /// invalid.
        /// </exception>
        protected void WriteMessage(IrcMessage message)
        {
            if (message.Command == null)
                throw new ArgumentException(Properties.Resources.ErrorMessageInvalidCommand, "message");
            if (message.Parameters.Count > maxParamsCount)
                throw new ArgumentException(Properties.Resources.ErrorMessageTooManyParams, "parameters");

            var lineBuilder = new StringBuilder();

            // Append prefix to line, if specified.
            if (message.Prefix != null)
                lineBuilder.Append(":" + CheckPrefix(message.Prefix) + " ");

            // Append command name to line.
            lineBuilder.Append(CheckCommand(message.Command).ToUpper());

            // Append each parameter to line, adding ':' character before last parameter.
            for (int i = 0; i < message.Parameters.Count - 1; i++)
            {
                if (message.Parameters[i] != null)
                    lineBuilder.Append(" " + CheckMiddleParameter(message.Parameters[i].ToString()));
            }
            if (message.Parameters.Count > 0)
            {
                var lastParameter = message.Parameters[message.Parameters.Count - 1];
                if (lastParameter != null)
                    lineBuilder.Append(" :" + CheckTrailingParameter(lastParameter));
            }

            var line = lineBuilder.ToString();
            WriteMessage(line);
            OnRawMessageSent(new IrcRawMessageEventArgs(message, line));
        }

        /// <summary>
        /// Writes the specified line to the network stream.
        /// </summary>
        /// <param name="line">The line to send.</param>
        /// <remarks>
        /// This method adds the specified line to the send queue and then immediately returns.
        /// The message is in fact only sent when the write loop takes the message from the queue and sends it over the
        /// connection.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        protected abstract void WriteMessage(string line);

        private string CheckPrefix(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidPrefix, value), "value");
            }

            return value;
        }

        private string CheckCommand(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidCommand, value), "value");
            }

            return value;
        }

        private string CheckMiddleParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(c => IsInvalidMessageChar(c) || c == ' ') || value[0] == ':')
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidMiddleParameter, value), "value");
            }

            return value;
        }

        private string CheckTrailingParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(c => IsInvalidMessageChar(c)))
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidMiddleParameter, value), "value");
            }

            return value;
        }

        private bool IsInvalidMessageChar(char value)
        {
            return value == '\0' || value == '\r' || value == '\n';
        }

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)"/>
        /// <summary>
        /// Connects to a server using the specified URL and user information.
        /// </summary>
        public abstract void Connect(Uri url, IrcRegistrationInfo registrationInfo);

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)"/>
        public void Connect(string host, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            Connect(host, DefaultPort, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="host">The name of the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public abstract void Connect(string host, int port, bool useSsl, IrcRegistrationInfo registrationInfo);

        /// <inheritdoc cref="Connect(IPAddress, int, bool, IrcRegistrationInfo)"/>
        public void Connect(IPAddress address, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            Connect(address, DefaultPort, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="address">An IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public abstract void Connect(IPAddress address, int port, bool useSsl, IrcRegistrationInfo registrationInfo);

        /// <inheritdoc cref="Connect(IPAddress[], int, bool, IrcRegistrationInfo)"/>
        public void Connect(IPAddress[] addresses, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            Connect(addresses, DefaultPort, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="addresses">A collection of one or more IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public abstract void Connect(IPAddress[] addresses, int port, bool useSsl, IrcRegistrationInfo registrationInfo);

        /// <summary>
        /// Connects to a server using the specified host and user information.
        /// </summary>
        /// <param name="remoteEP">The network endpoint (IP address and port) of the server to which to connect.</param>
        /// <param name="useSsl"><see langword="true"/> to connect to the server via SSL; <see langword="false"/>,
        /// otherwise</param>
        /// <param name="registrationInfo">The information used for registering the client. The type of the object may
        /// be either <see cref="IrcUserRegistrationInfo"/> or <see cref="IrcServiceRegistrationInfo"/>.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public abstract void Connect(IPEndPoint remoteEP, bool useSsl, IrcRegistrationInfo registrationInfo);

        protected void CheckRegistrationInfo(IrcRegistrationInfo registrationInfo, string registrationInfoParamName)
        {
            // Check that given registration info is valid.
            if (registrationInfo is IrcUserRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcUserRegistrationInfo)registrationInfo).UserName == null)
                    throw new ArgumentException(Properties.Resources.ErrorMessageInvalidUserRegistrationInfo,
                        registrationInfoParamName);
            }
            else if (registrationInfo is IrcServiceRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcServiceRegistrationInfo)registrationInfo).Description == null)
                    throw new ArgumentException(Properties.Resources.ErrorMessageInvalidServiceRegistrationInfo,
                        registrationInfoParamName);
            }
            else
            {
                throw new ArgumentException(Properties.Resources.ErrorMessageInvalidRegistrationInfoObject,
                    registrationInfoParamName);
            }
        }

        /// <summary>
        /// Disconnects immediately from the server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        /// <remarks>
        /// This method closes the client connection immediately and forcibly, and does not send a quit message to the
        /// server. To disconnect from the IRC server gracefully, call <see cref="Quit(string)"/> and wait for the
        /// connection to be closed.
        /// </remarks>
        public abstract void Disconnect();

        /// <summary>
        /// Throws an exception if the object has been dispoed; otherwise, simply returns immediately.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has already been disposed.</exception>
        protected void CheckDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        /// Raises the <see cref="Connected"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnConnected(EventArgs e)
        {
            var handler = this.Connected;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ConnectFailed"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs"/> instance containing the event data.</param>
        protected virtual void OnConnectFailed(IrcErrorEventArgs e)
        {
            var handler = this.ConnectFailed;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="Disconnected"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnDisconnected(EventArgs e)
        {
            var handler = this.Disconnected;
            if (handler != null)
                handler(this, e);
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
        /// Raises the <see cref="ValidateSslCertificate"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcValidateSslCertificateEventArgs"/> instance containing the event data.
        /// </param>
        protected virtual void OnValidateSslCertificate(IrcValidateSslCertificateEventArgs e)
        {
            var handler = this.ValidateSslCertificate;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="RawMessageSent"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs"/> instance containing the event data.</param>
        protected virtual void OnRawMessageSent(IrcRawMessageEventArgs e)
        {
            var handler = this.RawMessageSent;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="RawMessageReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs"/> instance containing the event data.</param>
        protected virtual void OnRawMessageReceived(IrcRawMessageEventArgs e)
        {
            var handler = this.RawMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ProtocolError"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcProtocolErrorEventArgs"/> instance containing the event data.</param>
        protected virtual void OnProtocolError(IrcProtocolErrorEventArgs e)
        {
            var handler = this.ProtocolError;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ErrorMessageReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorMessageEventArgs"/> instance containing the event data.</param>
        protected virtual void OnErrorMessageReceived(IrcErrorMessageEventArgs e)
        {
            var handler = this.ErrorMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ClientInfoReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnClientInfoReceived(EventArgs e)
        {
            var handler = this.ClientInfoReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="Registered"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnRegistered(EventArgs e)
        {
            var handler = this.Registered;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ServerBounce"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerInfoEventArgs"/> instance containing the event data.</param>
        protected virtual void OnServerBounce(IrcServerInfoEventArgs e)
        {
            var handler = this.ServerBounce;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ServerSupportedFeaturesReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnServerSupportedFeaturesReceived(EventArgs e)
        {
            var handler = this.ServerSupportedFeaturesReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="PingReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPingReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = this.PingReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="PongReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPongReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = this.PongReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="MotdReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnMotdReceived(EventArgs e)
        {
            var handler = this.MotdReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="NetworkInformationReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnNetworkInformationReceived(EventArgs e)
        {
            var handler = this.NetworkInformationReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ServerVersionInfoReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerVersionInfoEventArgs"/> instance containing the event data.</param>
        protected virtual void OnServerVersionInfoReceived(IrcServerVersionInfoEventArgs e)
        {
            var handler = this.ServerVersionInfoReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ServerTimeReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerTimeEventArgs"/> instance containing the event data.</param>
        protected virtual void OnServerTimeReceived(IrcServerTimeEventArgs e)
        {
            var handler = this.ServerTimeReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="WhoReplyReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcNameEventArgs"/> instance containing the event data.</param>
        protected virtual void OnWhoReplyReceived(IrcNameEventArgs e)
        {
            var handler = this.WhoReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="WhoIsReplyReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs"/> instance containing the event data.</param>
        protected virtual void OnWhoIsReplyReceived(IrcUserEventArgs e)
        {
            var handler = this.WhoIsReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="WhoWasReplyReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs"/> instance containing the event data.</param>
        protected virtual void OnWhoWasReplyReceived(IrcUserEventArgs e)
        {
            var handler = this.WhoWasReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ChannelListReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelListReceivedEventArgs"/> instance containing the event data.
        /// </param>
        protected virtual void OnChannelListReceived(IrcChannelListReceivedEventArgs e)
        {
            var handler = this.ChannelListReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Represents a method that processes <see cref="IrcMessage"/> objects.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        protected delegate void MessageProcessor(IrcMessage message);

        /// <summary>
        /// Represents a message that is sent/received by the client/server using the IRC protocol.
        /// A message contains a prefix (representing the source), a command name (a word or three-digit number),
        /// and an arbitrary number of parameters (up to a maximum of 15).
        /// </summary>
        [DebuggerDisplay("{ToString(), nq}")]
        public struct IrcMessage
        {
            /// <summary>
            /// The source of the message, which is the object represented by the value of <see cref="Prefix"/>.
            /// </summary>
            public IIrcMessageSource Source;

            /// <summary>
            /// The message prefix.
            /// </summary>
            public string Prefix;

            /// <summary>
            /// The name of the command.
            /// </summary>
            public string Command;

            /// <summary>
            /// A list of the parameters to the message.
            /// </summary>
            public IList<string> Parameters;

            /// <summary>
            /// Initializes a new instance of the <see cref="IrcMessage"/> structure.
            /// </summary>
            /// <param name="client">A client object that has sent/will receive the message.</param>
            /// <param name="prefix">The message prefix that represents the source of the message.</param>
            /// <param name="command">The command name; either an alphabetic word or 3-digit number.</param>
            /// <param name="parameters">A list of the parameters to the message. Can contain a maximum of 15 items.
            /// </param>
            public IrcMessage(IrcClient client, string prefix, string command, IList<string> parameters)
            {
                this.Prefix = prefix;
                this.Command = command;
                this.Parameters = parameters;

                this.Source = client.GetSourceFromPrefix(prefix);
            }

            /// <summary>
            /// Returns a string representation of this instance.
            /// </summary>
            /// <returns>A string that represents this instance.</returns>
            public override string ToString()
            {
                return string.Format("{0} ({1} parameters)", this.Command, this.Parameters.Count);
            }
        }
    }
}
