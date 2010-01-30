using System;
using System.Collections;
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

namespace IrcDotNet
{
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

        ~IrcClient()
        {
            Dispose(false);
        }

        public bool IsRegistered
        {
            get { return this.isRegistered; }
        }

        public IrcLocalUser LocalUser
        {
            get { return this.localUser; }
        }

        public string WelcomeMessage
        {
            get;
            private set;
        }

        public string YourHostMessage
        {
            get;
            private set;
        }

        public string ServerCreatedMessage
        {
            get;
            private set;
        }

        public string ServerName
        {
            get;
            private set;
        }

        public string ServerVersion
        {
            get;
            private set;
        }

        public string ServerAvailableUserModes
        {
            get;
            private set;
        }

        public string ServerAvailableChannelModes
        {
            get;
            private set;
        }

        public ReadOnlyDictionary<string, string> ServerSupportedFeatures
        {
            get { return this.serverSupportedFeaturesReadOnly; }
        }

        public string MessageOfTheDay
        {
            get;
            private set;
        }

        public IrcChannelCollection Channels
        {
            get { return this.channelsReadOnly; }
        }

        public IrcUserCollection Users
        {
            get { return this.usersReadOnly; }
        }

        public IIrcFloodPreventer FloodPreventer
        {
            get { return floodPreventer; }
            set { this.floodPreventer = value; }
        }

        public bool IsConnected
        {
            get { return this.client.Connected; }
        }

        public bool IsDisposed
        {
            get { return this.isDisposed; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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

        public event EventHandler<EventArgs> Connected;
        public event EventHandler<IrcErrorEventArgs> ConnectFailed;
        public event EventHandler<EventArgs> Disconnected;
        public event EventHandler<IrcErrorEventArgs> Error;
        public event EventHandler<IrcProtocolErrorEventArgs> ProtocolError;
        public event EventHandler<IrcErrorMessageEventArgs> ErrorMessageReceived;
        public event EventHandler<EventArgs> Registered;
        public event EventHandler<IrcServerInfoEventArgs> ServerBounce;
        public event EventHandler<EventArgs> ServerSupportedFeaturesReceived;
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PingReceived;
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PongReceived;
        public event EventHandler<EventArgs> MotdReceived;
        public event EventHandler<IrcChannelEventArgs> ChannelJoined;
        public event EventHandler<IrcChannelEventArgs> ChannelParted;
        public event EventHandler<IrcNameEventArgs> WhoReplyReceived;
        public event EventHandler<IrcUserEventArgs> WhoIsReplyReceived;
        public event EventHandler<IrcUserEventArgs> WhoWasReplyReceived;

        public void QueryWho(string mask = null, bool onlyOperators = false)
        {
            SendMessageWho(mask, onlyOperators);
        }

        public void QueryWhoIs(params string[] nickNameMasks)
        {
            QueryWhoIs((IEnumerable<string>)nickNameMasks);
        }

        public void QueryWhoIs(IEnumerable<string> nickNameMasks)
        {
            SendMessageWhoIs(nickNameMasks);
        }

        public void QueryWhoWas(params string[] nickNames)
        {
            QueryWhoWas((IEnumerable<string>)nickNames);
        }

        public void QueryWhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
        {
            SendMessageWhoWas(nickNames, entriesCount);
        }

        public void GetMotd(string server = null)
        {
            SendMessageMotd(server);
        }

        public void GetNetworkStatistics(string serverMask = null, string targetServer = null)
        {
            SendMessageLusers(serverMask, targetServer);
        }

        public void GetServerVersion(string server = null)
        {
            SendMessageVersion(server);
        }

        public void GetServerStats(string query = null, string server = null)
        {
            SendMessageStats(query, server);
        }

        public void GetServerLinks(string serverMask = null, string targetServer = null)
        {
            SendMessageStats(targetServer, serverMask);
        }

        public void GetServerTime(string server = null)
        {
            SendMessageTime(server);
        }

        public void Ping(string server = null)
        {
            SendMessagePing(this.localUser.NickName, server);
        }

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

        internal void Part(IEnumerable<string> channels, string comment = null)
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

        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            WriteMessage(new IrcMessage(this, null, command, parameters));
        }

        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            WriteMessage(new IrcMessage(this, null, command, parameters.ToArray()));
        }

        protected void WriteMessage(IrcMessage message)
        {
            if (message.Parameters.Count > maxParamsCount)
                throw new ArgumentException(Properties.Resources.ErrorMessageTooManyParams, "parameters");

            var line = new StringBuilder();
            if (message.Prefix != null)
                line.Append(":" + message.Prefix + " ");
            line.Append(message.Command.ToUpper());
            for (int i = 0; i < message.Parameters.Count - 1; i++)
            {
                if (message.Parameters[i] != null)
                    line.Append(" " + CheckMiddleParam(message.Parameters[i].ToString()));
            }
            if (message.Parameters.Count > 0)
            {
                var lastParameter = message.Parameters[message.Parameters.Count - 1];
                if (lastParameter != null)
                    line.Append(" :" + CheckTrailingParam(lastParameter));
            }
            WriteMessage(line.ToString());
        }

        private void WriteMessage(string line)
        {
            if (line.Length > 510)
                throw new ArgumentException(Properties.Resources.ErrorMessageLineTooLong, "line");

            messageSendQueue.Enqueue(line);
        }

        private string CheckMiddleParam(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\0' || value[i] == '\r' || value[i] == '\n' || value[i] == ' ' ||
                    (i == 0 && value[i] == ':'))
                    throw new ArgumentException(string.Format(
                        Properties.Resources.ErrorMessageInvalidMiddleParam, value), "value");
            }

            return value;
        }

        private string CheckTrailingParam(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\0' || value[i] == '\r' || value[i] == '\n')
                    throw new ArgumentException(string.Format(
                        Properties.Resources.ErrorMessageInvalidTrailingParam, value), "value");
            }

            return value;
        }

        public void Connect(string host, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(host, defaultPort, password, nickName, userName, realName, userMode);
        }

        public void Connect(string host, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(host, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        public void Connect(IPAddress address, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(address, defaultPort, password, nickName, userName, realName, userMode);
        }

        public void Connect(IPAddress address, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(address, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

        public void Connect(IPAddress[] addresses, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            Connect(addresses, defaultPort, password, nickName, userName, realName, userMode);
        }

        public void Connect(IPAddress[] addresses, int port, string password,
            string nickName, string userName, string realName, ICollection<char> userMode = null)
        {
            CheckDisposed();

            DisconnectInternal();
            this.client.BeginConnect(addresses, port, ConnectCallback,
                CreateConnectState(password, nickName, userName, realName, userMode));
            HandleClientConnecting();
        }

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

        public void Disconnect()
        {
            CheckDisposed();
            DisconnectInternal();
        }

        protected void DisconnectInternal()
        {
            if (this.client != null && this.client.Client.Connected)
            {
                try
                {
                    SendMessageQuit();
                    this.client.Client.Disconnect(true);
                }
                catch(SocketException exSocket)
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

        protected IEnumerable<IrcChannel> GetChannelsFromList(string namesList)
        {
            return namesList.Split(',').Select(n => GetChannelFromName(n));
        }

        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
            return nickNamesList.Split(',').Select(n => this.users.Single(u => u.NickName == n));
        }

        protected bool IsChannelName(string name)
        {
            return Regex.IsMatch(name, regexChannelName);
        }

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
                    throw new InvalidOperationException(string.Format(
                        Properties.Resources.ErrorMessageInvalidChannelType, type));
            }
        }

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
                var user = GetUserFromNickName(nickName, out createdNew);
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
                var user = GetUserFromNickName(nickName, out createdNew);
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
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageInvalidSource, targetName));
            }
        }

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
                var user = GetUserFromNickName(nickName, out createdNew);
                if (createdNew)
                {
                    user.UserName = userName;
                    user.HostName = hostName;
                }
                return user;
            }
            else
            {
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageInvalidSource, prefix));
            }
        }

        protected IrcServer GetServerFromHostName(string hostName)
        {
            bool createdNew;
            return GetServerFromHostName(hostName, out createdNew);
        }

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

        protected IrcChannel GetChannelFromName(string channelName)
        {
            bool createdNew;
            return GetChannelFromName(channelName, out createdNew);
        }

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

        protected IrcUser GetUserFromNickName(string nickName, bool isOnline = true)
        {
            bool createdNew;
            return GetUserFromNickName(nickName, out createdNew, isOnline);
        }

        protected IrcUser GetUserFromNickName(string nickName, out bool createdNew, bool isOnline = true)
        {
            // Search for user with given nick name in list of known users. If it does not exist, add it.
            var user = this.users.SingleOrDefault(u => u.NickName == nickName);
            if (user == null)
            {
                user = new IrcUser();
                user.IsOnline = isOnline;
                user.NickName = nickName;
                this.users.Add(user);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return user;
        }

        protected IrcUser GetUserFromUserName(string userName)
        {
            bool createdNew;
            return GetUserFromUserName(userName, out createdNew);
        }

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

        protected void CheckDisposed()
        {
            if (this.isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        protected virtual void OnConnected(EventArgs e)
        {
            var handler = this.Connected;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnConnectFailed(IrcErrorEventArgs e)
        {
            var handler = this.ConnectFailed;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnDisconnected(EventArgs e)
        {
            var handler = this.Disconnected;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnError(IrcErrorEventArgs e)
        {
            var handler = this.Error;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnProtocolError(IrcProtocolErrorEventArgs e)
        {
            var handler = this.ProtocolError;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnErrorMessageReceived(IrcErrorMessageEventArgs e)
        {
            var handler = this.ErrorMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnRegistered(EventArgs e)
        {
            var handler = this.Registered;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnServerBounce(IrcServerInfoEventArgs e)
        {
            var handler = this.ServerBounce;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnServerSupportedFeaturesReceived(EventArgs e)
        {
            var handler = this.ServerSupportedFeaturesReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnPingReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = this.PingReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnPongReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = this.PongReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnMotdReceived(EventArgs e)
        {
            var handler = this.MotdReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnChannelJoined(IrcChannelEventArgs e)
        {
            var handler = this.ChannelJoined;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnChannelParted(IrcChannelEventArgs e)
        {
            var handler = this.ChannelParted;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnWhoReplyReceived(IrcNameEventArgs e)
        {
            var handler = this.WhoReplyReceived;
            if (handler != null)
                handler(this, e);
        }
        protected virtual void OnWhoIsReplyReceived(IrcUserEventArgs e)
        {
            var handler = this.WhoIsReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnWhoWasReplyReceived(IrcUserEventArgs e)
        {
            var handler = this.WhoWasReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        protected delegate void MessageProcessor(IrcMessage message);

        protected class MessageProcessorAttribute : Attribute
        {
            public MessageProcessorAttribute(string command)
            {
                this.Command = command;
            }

            public string Command
            {
                get;
                private set;
            }
        }

        protected struct IrcMessage
        {
            public IIrcMessageSource Source;

            public string Prefix;
            public string Command;
            public IList<string> Parameters;

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
