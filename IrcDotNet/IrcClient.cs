using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    /// <summary>
    /// Provides methods for communicating with an IRC (Internet Relay Chat) server.
    /// </summary>
    public partial class IrcClient : IDisposable
    {
        private const int defaultPort = 6667;
        private const int maxParamsCount = 15;

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

        static IrcClient()
        {
            regexNickName = @"(?<nick>[^!@]+)";
            regexUserName = @"(?<user>[^!@]+)";
            regexHostName = @"(?<host>[^%@]+)";
            regexChannelName = @"(?<channel>[#+!&].+)";
            regexTargetMask = @"(?<targetMask>[$#].+)";
            regexServerName = @"(?<server>[^%@]+?\..*)";
            regexNickNameId = string.Format(@"{0}(?:(?:!{1})?@{2})?", regexNickName, regexUserName, regexHostName);
            regexUserNameId = string.Format(@"{0}(?:(?:%{1})?@{2}|%{1})", regexUserName, regexHostName,
                regexServerName);
            regexMessagePrefix = string.Format(@"^(?:{0}|{1})$", regexServerName, regexNickNameId);
            regexMessageTarget = string.Format(@"^(?:{0}|{1}|{2}|{3})$", regexChannelName, regexUserNameId,
                regexTargetMask, regexNickNameId);
        }

        // Internal collection of all known servers.
        private Collection<IrcServer> servers;
        // True if connection has been registered with server;
        private bool isRegistered;
        // Stores information about local user.
        private IrcLocalUser localUser;
        // Internal and exposable dictionaries of various features supported by server.
        private Dictionary<string, string> serverSupportedFeatures;
        private ReadOnlyDictionary<string, string> serverSupportedFeaturesReadOnly;
        // Builds MOTD (message of the day) string as it is received from server.
        private StringBuilder motdBuilder;
        // Internal and exposable collections of all currently joined channels.
        private ObservableCollection<IrcChannel> channels;
        private IrcChannelCollection channelsReadOnly;
        // Internal and exposable collections of all known users.
        private ObservableCollection<IrcUser> users;
        private IrcUserCollection usersReadOnly;

        private TcpClient client;
        private Thread readThread;
        private Thread writeThread;
        private NetworkStream stream;
        private StreamWriter writer;
        private StreamReader reader;
        // True if client can currently be disconnected.
        private bool canDisconnect;
        // Dictionary of message processor routines, keyed by their command names.
        private Dictionary<string, MessageProcessor> messageProcessors;
        // Array of message processor routines, keyed by their numeric codes (000 to 999).
        private Dictionary<int, MessageProcessor> numMessageProcessors;
        // Queue of messages to be sent by write loop when appropiate.
        private Queue<string> messageSendQueue;
        // Prevents client from flooding server with messages by limiting send rate.
        private IIrcFloodPreventer floodPreventer;

        private bool isDisposed = false;

        /// <summary>
        /// Initialises a new instance of the <see cref="IrcClient"/> class.
        /// </summary>
        public IrcClient()
        {
            this.client = new TcpClient();
            this.readThread = new Thread(ReadLoop);
            this.writeThread = new Thread(WriteLoop);
            this.canDisconnect = false;
            this.messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
            this.numMessageProcessors = new Dictionary<int, MessageProcessor>(1000);
            this.messageSendQueue = new Queue<string>();
            this.floodPreventer = null;

            InitialiseMessageProcessors();
            ResetState();
        }

        /// <summary>
        /// Finalises an instance of the <see cref="IrcClient"/> class.
        /// </summary>
        ~IrcClient()
        {
            Dispose(false);
        }

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
        /// Gets the Message of the Day (MOTD) sent by the server.
        /// This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public string MessageOfTheDay
        {
            get;
            private set;
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
        public bool IsConnected
        {
            get { return this.client.Connected; }
        }

        /// <summary>
        /// Gets whether the <see cref="IrcClient"/> object has been disposed.
        /// </summary>
        /// <value><see langword="true"/> if the <see cref="IrcClient"/> object has been disposed;
        /// <see langword="false"/>, otherwise.</value>
        public bool IsDisposed
        {
            get { return this.isDisposed; }
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
        protected void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    DisconnectInternal();

                    if (this.client != null)
                    {
                        this.client.Close();
                        this.client = null;
                    }
                    if (this.readThread != null)
                    {
                        if (this.readThread.IsAlive)
                            this.readThread.Join(1000);
                        this.readThread = null;
                    }
                    if (this.writeThread != null)
                    {
                        if (this.writeThread.IsAlive)
                            this.writeThread.Join(1000);
                        this.writeThread = null;
                    }
                    if (this.stream != null)
                    {
                        this.stream.Close();
                        this.stream = null;
                    }
                    if (this.writer != null)
                    {
                        this.writer.Close();
                        this.writer = null;
                    }
                    if (this.reader != null)
                    {
                        this.reader.Close();
                        this.reader = null;
                    }
                }
            }
            this.isDisposed = true;
        }

        /// <summary>
        /// Occurs when the client has connected to the server.
        /// </summary>
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
        /// Occurs when the client encounters an error.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> Error;
        /// <summary>
        /// Occurs when a protocol (numeric) error is received from the server.
        /// </summary>
        public event EventHandler<IrcProtocolErrorEventArgs> ProtocolError;
        /// <summary>
        /// Occurs when an Error message is received from the server.
        /// </summary>
        public event EventHandler<IrcErrorMessageEventArgs> ErrorMessageReceived;
        /// <summary>
        /// Occurs when the connection has been registered.
        /// </summary>
        public event EventHandler<EventArgs> Registered;
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
        /// Sends a Who query to the server targeting the specified channel or user masks.
        /// </summary>
        /// <param name="mask">A wildcard expression for matching against channel names; or if none can be found,
        /// host names, server names, real names, and nick names of users.</param>
        /// <param name="onlyOperators"><see langword="true"/> to match only server operators; otherwise,
        /// <see langword="false"/>. Default is to match all users.</param>
        public void QueryWho(string mask = null, bool onlyOperators = false)
        {
            SendMessageWho(mask, onlyOperators);
        }

        /// <inheritdoc cref="QueryWhoIs(IEnumerable{string})"/>
        public void QueryWhoIs(params string[] nickNameMasks)
        {
            QueryWhoIs((IEnumerable<string>)nickNameMasks);
        }

        /// <overloads>Sends a Who Is query to the server.</overloads>
        /// <summary>
        /// Sends a Who Is query to server targeting the specified nick name masks.
        /// </summary>
        /// <param name="nickNameMasks">A collection of wildcard expressions for matching against nick names of users.
        /// </param>
        public void QueryWhoIs(IEnumerable<string> nickNameMasks)
        {
            SendMessageWhoIs(nickNameMasks);
        }

        /// <inheritdoc cref="QueryWhoWas(IEnumerable{string}, int)"/>
        public void QueryWhoWas(params string[] nickNames)
        {
            QueryWhoWas((IEnumerable<string>)nickNames);
        }

        /// <summary>
        /// Sends a Who Was query to server targeting the specified nick names.
        /// </summary>
        /// <param name="nickNames">The nick names of the users to query.</param>
        /// <param name="entriesCount">The maximum number of entries to return from the query. Default is an unlimited
        /// number.</param>
        public void QueryWhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
        {
            SendMessageWhoWas(nickNames, entriesCount);
        }

        /// <summary>
        /// Requests the Message of the Day (MOTD) from the specified server.
        /// </summary>
        /// <param name="serverName">The name of the server from which to request the MOTD. Default is the current
        /// server.</param>
        public void GetMessageOfTheDay(string serverName = null)
        {
            SendMessageMotd(serverName);
        }

        /// <summary>
        /// Requests statistics about the size of the network.
        /// If <paramref name="serverMask"/> is specified, then the server only returns information about the part of
        /// the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        /// network
        /// </summary>
        /// <param name="serverMask">A wildcard expression for matching against server names. Default matches the whole
        /// network.</param>
        /// <param name="targetServer">The server to which to forward the request.</param>
        public void GetNetworkStatistics(string serverMask = null, string targetServer = null)
        {
            SendMessageLUsers(serverMask, targetServer);
        }

        /// <summary>
        /// Requests the version of the specified server.
        /// </summary>
        /// <param name="serverName">The name of the server whose version to request.</param>
        public void GetServerVersion(string serverName = null)
        {
            SendMessageVersion(serverName);
        }

        /// <summary>
        /// Requests the statistics of the specified server.
        /// </summary>
        /// <param name="query">The query that indicates to the server what statistics to return.</param>
        /// <param name="serverName">The name of the server whose statistics to request.</param>
        public void GetServerStats(string query = null, string serverName = null)
        {
            SendMessageStats(query, serverName);
        }

        /// <summary>
        /// Requests a list of all servers known by the target server.
        /// If <paramref name="serverMask"/> is specified, then the server only returns information about the part of
        /// the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        /// network.
        /// </summary>
        /// <param name="serverMask">A wildcard expression for matching against server names. Default matches the whole
        /// network.</param>
        /// <param name="targetServer">The server to which to forward the request.</param>
        public void GetServerLinks(string serverMask = null, string targetServer = null)
        {
            SendMessageStats(targetServer, serverMask);
        }

        /// <summary>
        /// Requests the local time on the specified server.
        /// </summary>
        /// <param name="serverName">The name of the server whose time to request</param>
        public void GetServerTime(string serverName = null)
        {
            SendMessageTime(serverName);
        }

        /// <summary>
        /// Sends a ping to the specified server.
        /// </summary>
        /// <param name="serverName">The name of the server to ping.</param>
        public void Ping(string serverName = null)
        {
            SendMessagePing(this.localUser.NickName, serverName);
        }

        /// <summary>
        /// Quits the server, giving the specified comment.
        /// </summary>
        /// <param name="comment">The comment to send the server upon quitting.</param>
        public void Quit(string comment = null)
        {
            SendMessageQuit(comment);
        }

        #region Proxy Methods

        internal void SetNickName(string nickName)
        {
            SendMessageNick(nickName);
        }

        internal void SetAway(string text)
        {
            SendMessageAway(text);
        }

        internal void UnsetAway()
        {
            SendMessageAway();
        }

        internal void GetChannelModes(IrcChannel channel, string modes = null)
        {
            SendMessageChannelMode(channel.Name, modes);
        }

        internal void SetChannelModes(IrcChannel channel, string modes, IEnumerable<string> modeParameters = null)
        {
            SendMessageChannelMode(channel.Name, modes, modeParameters);
        }

        internal void GetLocalUserModes(IrcLocalUser user)
        {
            SendMessageUserMode(user.NickName);
        }

        internal void SetLocalUserModes(IrcLocalUser user, string modes)
        {
            SendMessageUserMode(user.NickName, modes);
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

        internal void Invite(IrcChannel channel, IrcUser user)
        {
            SendMessageInvite(channel.Name, user.NickName);
        }

        internal void Kick(IrcChannel channel, IEnumerable<IrcUser> users, string comment = null)
        {
            SendMessageKick(channel.Name, users.Select(u => u.NickName), comment);
        }

        internal void Kick(IEnumerable<IrcChannelUser> channelUsers, string comment = null)
        {
            SendMessageKick(channelUsers.Select(cu => Tuple.Create(cu.Channel.Name, cu.User.NickName)), comment);
        }

        internal void SendPrivateMessage(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
            CheckTextValid(text);
            SendMessagePrivateMessage(targetsNamesArray, text);
            this.localUser.HandleMessageSent(targets, text);
        }

        internal void SendNotice(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
            CheckTextValid(text);
            SendMessageNotice(targetsNamesArray, text);
            this.localUser.HandleNoticeSent(targets, text);
        }

        private void CheckTextValid(string text)
        {
            if (text.Any(c => c == '\r' || c == '\n'))
                throw new ArgumentException(Properties.Resources.ErrorMessageTextCannotContainNewLine, "text");
        }

        #endregion

        private void InitialiseMessageProcessors()
        {
            // Find all methods in class that are marked by one or more instances of MessageProcessrAttribute.
            // Add  each pair of command & processor  to dictionary (with at least one command per processor).
            var messageProcessorsMethods = this.GetType().GetMethods(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var methodInfo in messageProcessorsMethods)
            {
                var messageProcessorAttributes = (MessageProcessorAttribute[])methodInfo.GetCustomAttributes(
                    typeof(MessageProcessorAttribute), true);
                if (messageProcessorAttributes.Length > 0)
                {
                    var methodDelegate = (MessageProcessor)Delegate.CreateDelegate(typeof(MessageProcessor), this,
                        methodInfo);
                    foreach (var attribute in messageProcessorAttributes)
                    {
                        var commandRangeParts = attribute.Command.Split('-');
                        if (commandRangeParts.Length == 2)
                        {
                            // Numeric command range was specified.
                            var commandRangeStart = int.Parse(commandRangeParts[0]);
                            var commandRangeEnd = int.Parse(commandRangeParts[1]);
                            for (int code = commandRangeStart; code <= commandRangeEnd; code++)
                                this.numMessageProcessors.Add(code, methodDelegate);
                        }
                        else
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
                    }
                }
            }
        }

        private void ReadLoop()
        {
            try
            {
                // Read each message from network stream, one per line, until client is disconnected.
                while (this.client != null && this.client.Connected)
                {
                    var line = this.reader.ReadLine();
                    if (line == null)
                        break;

                    Debug.WriteLine(DateTime.Now.ToLongTimeString() + " >>> " + line);

                    string prefix = null;
                    string command = null;

                    // Extract prefix from message, if it contains one.
                    if (line[0] == ':')
                    {
                        var firstSpaceIndex = line.IndexOf(' ');
                        prefix = line.Substring(1, firstSpaceIndex - 1);
                        line = line.Substring(firstSpaceIndex + 1);
                    }

                    // Extract command from message.
                    command = line.Substring(0, line.IndexOf(' '));
                    line = line.Substring(command.Length + 1);

                    // Extract parameters from message.
                    // Each parameter is separated by a single space, except the last one, which may contain spaces if it is prefixed by a colon.
                    var parameters = new string[maxParamsCount];
                    int paramStartIndex, paramEndIndex = -1;
                    int lineColonIndex = line.LastIndexOf(':');
                    if (lineColonIndex == -1)
                        lineColonIndex = line.Length;
                    for (int i = 0; i < parameters.Length; i++)
                    {
                        paramStartIndex = paramEndIndex + 1;
                        paramEndIndex = line.IndexOf(' ', paramStartIndex);
                        if (paramEndIndex == -1)
                            paramEndIndex = line.Length;
                        if (paramEndIndex > lineColonIndex)
                        {
                            paramStartIndex++;
                            paramEndIndex = line.Length;
                        }
                        parameters[i] = line.Substring(paramStartIndex, paramEndIndex - paramStartIndex);
                        if (paramEndIndex == line.Length)
                            break;
                    }

                    var message = new IrcMessage(this, prefix, command, parameters);
                    ReadMessage(message);
                }
            }
            catch (IOException exIO)
            {
                var socketException = exIO.InnerException as SocketException;
                if (socketException != null)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                        case SocketError.NotConnected:
                            return;
                    }
                }

                OnError(new IrcErrorEventArgs(exIO));
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                DisconnectInternal();
            }
        }

        private void WriteLoop()
        {
            try
            {
                // Continuously write messages in send queue to network stream, within given rate limit.
                while (this.client != null && this.client.Connected)
                {
                    // Send messages in send queue until flood preventer stops it.
                    while (this.messageSendQueue.Count > 0)
                    {
                        if (this.floodPreventer != null && !this.floodPreventer.CanSendMessage())
                            break;
                        var line = this.messageSendQueue.Dequeue();
                        this.writer.WriteLine(line);
                        if (this.floodPreventer != null)
                            this.floodPreventer.HandleMessageSent();

                        Debug.WriteLine(DateTime.Now.ToLongTimeString() + " <<< " + line);
                    }
                    this.writer.Flush();

                    Thread.Sleep(50);
                }
            }
            catch (IOException exIO)
            {
                var socketException = exIO.InnerException as SocketException;
                if (socketException != null)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.Interrupted:
                        case SocketError.NotConnected:
                            return;
                    }
                }

                OnError(new IrcErrorEventArgs(exIO));
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                DisconnectInternal();
            }
        }

        private void ReadMessage(IrcMessage message)
        {
            // Try to find corresponding message processor for command of given message.
            MessageProcessor msgProc;
            int commandCode;
            if (this.messageProcessors.TryGetValue(message.Command, out msgProc) ||
                (int.TryParse(message.Command, out commandCode) &&
                this.numMessageProcessors.TryGetValue(commandCode, out msgProc)))
            {
                try
                {
                    msgProc(message);
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
                Debug.WriteLine("Unknown message command '{0}'.", message.Command);
            }
        }

        /// <inheritdoc cref="WriteMessage(string, string, IEnumerable{string})"/>
        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            WriteMessage(new IrcMessage(this, null, command, parameters));
        }

        /// <inheritdoc cref="WriteMessage(IrcMessage)"/>
        /// <param name="prefix">The message prefix, which represents the source of the message.</param>
        /// <param name="command">The name of the command.</param>
        /// <param name="parameters">A collection of the parameters to the command.</param>
        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            WriteMessage(new IrcMessage(this, null, command, parameters.ToArray()));
        }

        /// <inheritdoc cref="WriteMessage(string)"/>
        /// <summary>
        /// Writes the specified message (prefix, command, and parameters) to the network stream.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <exception cref="ArgumentException">
        /// <paramref name="message"/> contains more than 15 many parameters. -or-
        /// The value of <see cref="IrcMessage.Prefix"/> of <paramref name="message"/> is invalid. -or-
        /// The value of <see cref="IrcMessage.Command"/> of <paramref name="message"/> is invalid. -or-
        /// The value of one of the items of <see cref="IrcMessage.Parameters"/> of <paramref name="message"/> is
        /// invalid.
        /// </exception>
        protected void WriteMessage(IrcMessage message)
        {
            if (message.Parameters.Count > maxParamsCount)
                throw new ArgumentException(Properties.Resources.ErrorMessageTooManyParams, "parameters");

            var line = new StringBuilder();
            if (message.Prefix != null)
                line.Append(":" + CheckPrefix(message.Prefix) + " ");
            line.Append(CheckCommand(message.Command).ToUpper());
            for (int i = 0; i < message.Parameters.Count - 1; i++)
            {
                if (message.Parameters[i] != null)
                    line.Append(" " + CheckMiddleParameter(message.Parameters[i].ToString()));
            }
            if (message.Parameters.Count > 0)
            {
                var lastParameter = message.Parameters[message.Parameters.Count - 1];
                if (lastParameter != null)
                    line.Append(" :" + CheckTrailingParameter(lastParameter));
            }
            WriteMessage(line.ToString());
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
        /// <exception cref="ArgumentException"><paramref name="line"/> is longer than 510 characters.</exception>
        private void WriteMessage(string line)
        {
            CheckDisposed();

            if (line.Length > 510)
                throw new ArgumentException(Properties.Resources.ErrorMessageLineTooLong, "line");

            messageSendQueue.Enqueue(line);
        }

        private string CheckPrefix(string value)
        {
            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidPrefix, value), "value");
            }

            return value;
        }

        private string CheckCommand(string value)
        {
            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidCommand, value), "value");
            }

            return value;
        }

        private string CheckMiddleParameter(string value)
        {
            if (value.Length == 0 || value.Any(c => IsInvalidMessageChar(c)) || value[0] == ':')
            {
                throw new ArgumentException(string.Format(
                    Properties.Resources.ErrorMessageInvalidMiddleParameter, value), "value");
            }

            return value;
        }

        private string CheckTrailingParameter(string value)
        {
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

        /// <inheritdoc cref="Connect(string, int, string, string, string, string, ICollection{char})"/>
        public void Connect(string host, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(host, defaultPort, password, nickName, userName, realName, userMode);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, string, string, string, string, ICollection{char})"/>
        /// <param name="host">The name of the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(string host, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(host, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        /// <inheritdoc cref="Connect(IPAddress, int, string, string, string, string, ICollection{char})"/>
        public void Connect(IPAddress address, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(address, defaultPort, password, nickName, userName, realName, userMode);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, string, string, string, string, ICollection{char})"/>
        /// <param name="address">An IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(IPAddress address, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(address, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        /// <inheritdoc cref="Connect(IPAddress[], string, string, string, string, ICollection{char})"/>
        public void Connect(IPAddress[] addresses, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(addresses, defaultPort, password, nickName, userName, realName, userMode);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, string, string, string, string, ICollection{char})"/>
        /// <param name="addresses">A collection of one or more IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(IPAddress[] addresses, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(addresses, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        /// <summary>
        /// Connects to a server using the specified host and user information.
        /// </summary>
        /// <param name="remoteEP">The network endpoint (IP address and port) of the server to which to connect.</param>
        /// <param name="password">The password to register with the server.</param>
        /// <param name="nickName">The nick name to register with the server. This can later be changed.</param>
        /// <param name="userName">The user name to register with the server.</param>
        /// <param name="realName">The real name to register with the server.</param>
        /// <param name="userMode">The initial user mode to register with the server. The value should not contain any
        /// character except 'w' or 'i'.</param>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void Connect(IPEndPoint remoteEP, string password, string nickName,
            string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(remoteEP.Address, remoteEP.Port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        private object CreateConnectState(string password, string nickName, string userName, string realName,
            ICollection<char> userMode)
        {
            CheckDisposed();

            if (nickName == null)
                throw new ArgumentException(Properties.Resources.ErrorMessageInvalidNickName, "nickName");
            if (userName == null)
                throw new ArgumentException(Properties.Resources.ErrorMessageInvalidNickName, "userName");
            if (realName == null)
                throw new ArgumentException(Properties.Resources.ErrorMessageInvalidNickName, "realName");

            return new IrcConnectContext
                {
                    Password = password,
                    NickName = nickName,
                    UserName = userName,
                    RealName = realName,
                    UserMode = userMode,
                };
        }

        /// <summary>
        /// Disconnects immediately from the server. A quit message is sent if the connection is still active.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has already been been disposed.</exception>
        public void Disconnect()
        {
            CheckDisposed();
            DisconnectInternal();
        }

        /// <summary>
        /// Disconnects from the server, regardless of whether the client object has already been disposed.
        /// </summary>
        protected void DisconnectInternal()
        {
            if (this.client != null && this.client.Client.Connected)
            {
                try
                {
                    SendMessageQuit();
                    this.client.Client.Disconnect(true);
                }
                catch (SocketException exSocket)
                {
                    if (exSocket.SocketErrorCode != SocketError.NotConnected)
                        throw;
                }
            }

            if (this.canDisconnect)
            {
                this.canDisconnect = false;
                OnDisconnected(new EventArgs());
                HandleClientClosed();
            }
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                this.client.EndConnect(ar);
                this.stream = this.client.GetStream();
                this.writer = new StreamWriter(this.stream, Encoding.ASCII);
                this.reader = new StreamReader(this.stream, Encoding.ASCII);

                HandleClientConnected((IrcConnectContext)ar.AsyncState);
                this.readThread.Start();
                this.writeThread.Start();

                OnConnected(new EventArgs());
            }
            catch (Exception ex)
            {
                OnConnectFailed(new IrcErrorEventArgs(ex));
            }
        }

        private void HandleClientConnecting()
        {
            Debug.WriteLine("Connecting to server...");

            this.canDisconnect = true;
        }

        private void HandleClientConnected(IrcConnectContext initState)
        {
            Debug.WriteLine("Connected to server '{0}'.", ((IPEndPoint)this.client.Client.RemoteEndPoint).Address);

            try
            {
                if (initState.Password != null)
                    SendMessagePassword(initState.Password);
                SendMessageNick(initState.NickName);
                SendMessageUser(initState.UserName, GetNumericUserMode(initState.UserMode), initState.RealName);

                // Initialise local user and add it to collection.
                this.localUser = new IrcLocalUser(initState.NickName, initState.UserName, initState.RealName,
                    initState.UserMode);
                this.users.Add(this.localUser);
            }
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
                DisconnectInternal();
            }
        }

        private void HandleClientClosed()
        {
            Debug.WriteLine("Disconnected from server.");

            ResetState();
        }

        /// <summary>
        /// Extracts the nick name and user mode from the specified value.
        /// </summary>
        /// <param name="input">The input value, containing a nick name prefixed by a user mode.</param>
        /// <returns>A 2-tuple of the nick name and user mode.</returns>
        protected Tuple<string, string> ExtractUserMode(string input)
        {
            switch (input[0])
            {
                case '@':
                    return Tuple.Create(input.Substring(1), "o");
                case '+':
                    return Tuple.Create(input.Substring(1), "v");
                default:
                    return Tuple.Create(input, string.Empty);
            }
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
            return namesList.Split(',').Select(n => GetChannelFromName(n));
        }

        /// <summary>
        /// Gets a list of user objedcts from the specified comma-separated list of nick names.
        /// </summary>
        /// <param name="nickNamesList">A value that contains a comma-separated list of nick names of users.</param>
        /// <returns>A list of user objects that corresponds to the given list of nick names.</returns>
        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
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
        /// <returns>The target object that corresponds to the given name. The object is an instance of
        /// <see cref="IrcUser"/>, <see cref="IrcChannel"/>, or <see cref="IrcTargetMask"/>.</returns>
        /// <exception cref="ArgumentException"><paramref name="targetName"/> does not represent a valid message target.
        /// </exception>
        protected IIrcMessageTarget GetMessageTarget(string targetName)
        {
            Debug.Assert(targetName.Length > 0);

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
            Debug.Assert(prefix.Length > 0);

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

        private int GetNumericUserMode(ICollection<char> mode)
        {
            var value = 0;
            if (mode == null)
                return value;
            if (mode.Contains('w'))
                value |= 0x02;
            if (mode.Contains('i'))
                value |= 0x04;
            return value;
        }

        private void ResetState()
        {
            this.servers = new Collection<IrcServer>();
            this.isRegistered = false;
            this.localUser = null;
            this.serverSupportedFeatures = new Dictionary<string, string>();
            this.serverSupportedFeaturesReadOnly = new ReadOnlyDictionary<string, string>(this.serverSupportedFeatures);
            this.motdBuilder = new StringBuilder();
            this.channels = new ObservableCollection<IrcChannel>();
            this.channelsReadOnly = new IrcChannelCollection(this, this.channels);
            this.users = new ObservableCollection<IrcUser>();
            this.usersReadOnly = new IrcUserCollection(this, this.users);
        }

        /// <summary>
        /// Throws an exception if the object has been dispoed; otherwise, simply returns immediately.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The object has already been disposed.</exception>
        protected void CheckDisposed()
        {
            if (this.isDisposed)
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
        /// Represents a method that processes <see cref="IrcMessage"/>s.
        /// </summary>
        /// <param name="message">The message that the method should process.</param>
        protected delegate void MessageProcessor(IrcMessage message);

        /// <summary>
        /// Indicates that a method processes <see cref="IrcMessage"/>s for a given command.
        /// </summary>
        protected class MessageProcessorAttribute : Attribute
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="MessageProcessorAttribute"/> class.
            /// </summary>
            /// <param name="command">The name of the command for which messages are processed.</param>
            public MessageProcessorAttribute(string command)
            {
                this.Command = command;
            }

            /// <summary>
            /// Gets the name of the command for which messages are processed.
            /// </summary>
            /// <value>The command name.</value>
            public string Command
            {
                get;
                private set;
            }
        }

        /// <summary>
        /// Represents a message that is sent/received by the client/server. A message contains a prefix (representing
        /// the source), a command name (a word or three-digit number), and an arbitrary number of parameters (up to a
        /// maximum of 15).
        /// </summary>
        protected struct IrcMessage
        {
            /// <summary>
            /// The source of the message, which is the object represented by <see cref="Prefix"/>.
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
            /// Initializes a new instance of the <see cref="IrcMessage"/> struct.
            /// </summary>
            /// <param name="client">A client object that has sent/will received the message.</param>
            /// <param name="prefix">The message prefix, which represents the source of the message.</param>
            /// <param name="command">The command name; either a word or 3-digit number.</param>
            /// <param name="parameters">A lisit of the parameters to the message, containing a maximum of 15 items.
            /// </param>
            public IrcMessage(IrcClient client, string prefix, string command, IList<string> parameters)
            {
                this.Prefix = prefix;
                this.Command = command;
                this.Parameters = parameters;

                this.Source = client.GetSourceFromPrefix(prefix);
            }
        }

        private struct IrcConnectContext
        {
            public string Password;
            public string UserName;
            public string NickName;
            public string RealName;
            public ICollection<char> UserMode;
        }
    }
}
