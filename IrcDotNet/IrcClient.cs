using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Reflection;

namespace IrcDotNet
{
    // TODO: Handle and raise events for numeric error responses.
    public class IrcClient : IDisposable
    {
        // Error messages to be used in thrown exceptions.
        private const string errorMessageTooManyParams = "No more than 15 command parameters may be specified.";
        private const string errorMessageLineTooLong = "The length of the line must not exceed 510 characters.";
        private const string errorMessageInvalidMiddleParam = "The non-trailing parameter '{0}' is invalid.";
        private const string errorMessageInvalidTrailingParam = "The trailing parameter '{0}' is invalid.";
        private const string errorMessageBadMessageCommand = "The command '{0}' was not recognised.";
        private const string errorMessageInvalidPassword = "The specified password is invalid.";
        private const string errorMessageInvalidNickName = "The specified nick name is invalid.";
        private const string errorMessageInvalidUserName = "The specified user name is invalid.";
        private const string errorMessageInvalidRealName = "The specified real name is invalid.";
        private const string errorMessageInvalidUserMode = "The specified user mode is invalid.";
        private const string errorMessageCannotSetUserMode = "Cannot set user mode for '{0}'.";
        private const string errorMessageTooManyModeParameters = "No more than 3 mode parameters may be sent per message.";
        private const string errorMessageInvalidChannelType = "The channel type ('{0}') sent by the server is invalid.";

        private const int defaultPort = 6667;
        private const int maxParamsCount = 15;

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
        // Internal and exposable collections of all currently known users.
        private ObservableCollection<IrcUser> users;
        private IrcUserCollection usersReadOnly;

        private TcpClient client;
        private Thread readThread;
        private NetworkStream stream;
        private StreamWriter writer;
        private StreamReader reader;
        // True if client can currently be disconnected.
        private bool canDisconnect;
        // Dictionary of message processor routines, keyed by their command names.
        private Dictionary<string, MessageProcessor> messageProcessors;

        private bool isDisposed = false;

        public IrcClient()
        {
            this.client = new TcpClient();
            this.readThread = new Thread(ReadLoop);
            this.canDisconnect = false;

            this.messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
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

        public string NickName
        {
            get { return this.localUser.NickName; }
            set
            {
                SendMessageNick(value);
            }
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
        public event EventHandler<EventArgs> ProtocolError;
        public event EventHandler<EventArgs> Registered;
        public event EventHandler<IrcServerInfoEventArgs> ServerBounce;
        public event EventHandler<EventArgs> ServerSupportedFeaturesReceived;
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PingReceived;
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PongReceived;
        public event EventHandler<EventArgs> MotdReceived;
        public event EventHandler<IrcChannelEventArgs> ChannelJoined;
        public event EventHandler<IrcChannelEventArgs> ChannelParted;

        public void Ping(string target = null)
        {
            SendMessagePing(this.localUser.NickName, target);
        }

        #region Proxy Methods

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
                        this.messageProcessors.Add(attribute.Command, methodDelegate);
                }
            }
        }

        private void ReadLoop()
        {
            try
            {
                // Read each message, one per line, from network stream.
                while (this.client != null && this.client.Connected)
                {
                    var line = this.reader.ReadLine();
                    if (line == null)
                        break;

                    Trace.WriteLine(">>> " + line);

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

                    var message = new IrcMessage(prefix, command, parameters);
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

        private void ReadMessage(IrcMessage message)
        {
            // Try to find message processor for command of given message.
            MessageProcessor msgProc;
            if (this.messageProcessors.TryGetValue(message.Command, out msgProc))
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
                Trace.TraceWarning("Unknown message command '{0}'.", message.Command);
            }
        }

        #region Message Processing

        [MessageProcessor("nick")]
        protected void ProcessMessageNick(IrcMessage message)
        {
            Debug.Assert(message.Prefix != null);
            var origin = GetUserByPrefix(message.Prefix);
            Debug.Assert(message.Parameters[0] != null);
            this.localUser.NickName = message.Parameters[0];
        }

        [MessageProcessor("join")]
        protected void ProcessMessageJoin(IrcMessage message)
        {
            Debug.Assert(message.Prefix != null);
            var origin = GetUserByPrefix(message.Prefix);
            Debug.Assert(message.Parameters[0] != null);
            if (origin == this.localUser)
            {
                // Local user has joined one or more channels. Add channels to collection.
                var channels = message.Parameters[0].Split(',').Select(n => new IrcChannel(n)).ToArray();
                this.channels.AddRange(channels);
                channels.ForEach(c => OnChannelJoined(new IrcChannelEventArgs(c)));
            }
            else
            {
                // Remote user has joined one or more channels.
                var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
                channels.ForEach(c => c.HandleUserJoined(new IrcChannelUser(origin)));
            }
        }

        [MessageProcessor("part")]
        protected void ProcessMessagePart(IrcMessage message)
        {
            Debug.Assert(message.Prefix != null);
            var origin = GetUserByPrefix(message.Prefix);
            Debug.Assert(message.Parameters[0] != null);
            if (origin == this.localUser)
            {
                // Local user has parted one or more channels. Remove channel from collections.
                var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
                this.channels.RemoveRange(channels);
                channels.ForEach(c => OnChannelParted(new IrcChannelEventArgs(c)));
            }
            else
            {
                // Remote user has parted one or more channels.
                var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
                channels.ForEach(c => c.HandleUserParted(new IrcChannelUser(origin)));
            }
        }

        [MessageProcessor("mode")]
        protected void ProcessMessageMode(IrcMessage message)
        {
            // Check if mode applies to channel or user.
            Debug.Assert(message.Parameters[0] != null);
            if (IsChannel(message.Parameters[0]))
            {
                var channel = this.channels.Single(c => c.Name == message.Parameters[0]);

                // Get specified channel modes and list of mode parameters
                Debug.Assert(message.Parameters[1] != null);
                var modesAndParameters = GetModeAndParameters(message.Parameters.Skip(1));
                channel.HandleModesChanged(modesAndParameters.Item1, modesAndParameters.Item2);
            }
            else if (message.Parameters[0] == this.localUser.NickName)
            {
                Debug.Assert(message.Parameters[1] != null);
                this.localUser.HandleModesChanged(message.Parameters[1]);
            }
            else
            {
                throw new InvalidOperationException(string.Format(errorMessageCannotSetUserMode,
                    message.Parameters[0]));
            }
        }

        [MessageProcessor("topic")]
        protected void ProcessMessageTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channel = this.channels.Single(c => c.Name == message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            channel.Topic = message.Parameters[1];
        }

        [MessageProcessor("kick")]
        protected void ProcessMessageKick(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channels = GetChannelsFromList(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            var users = GetUsersFromList(message.Parameters[1]).ToArray();
            foreach (var channelUser in Enumerable.Zip(channels, users,
                (channel, user) => channel.GetChannelUser(user)))
            {
                if (channelUser.User == this.localUser)
                {
                    // Local user was kicked from channel.
                    var channel = channelUser.Channel;
                    this.channels.Remove(channel);
                    channelUser.Channel.HandleUserKicked(channelUser);
                    OnChannelParted(new IrcChannelEventArgs(channel));
                    break;
                }
                else
                {
                    // Remote user was kicked from channel.
                    channelUser.Channel.HandleUserKicked(channelUser);
                }
            }
        }

        [MessageProcessor("ping")]
        protected void ProcessMessagePing(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            var target = message.Parameters[1];
            OnPingReceived(new IrcPingOrPongReceivedEventArgs(server));
            SendMessagePong(this.localUser.NickName, target);
        }

        [MessageProcessor("pong")]
        protected void ProcessMessagePong(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            OnPongReceived(new IrcPingOrPongReceivedEventArgs(server));
        }

        [MessageProcessor("001")]
        protected void ProcessMessageReplyWelcome(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            this.localUser.NickName = message.Parameters[0];
            Debug.Assert(message.Parameters[1] != null);
            this.WelcomeMessage = message.Parameters[1];

            this.isRegistered = true;
            OnRegistered(new EventArgs());
        }

        [MessageProcessor("002")]
        protected void ProcessMessageReplyYourHost(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.YourHostMessage = message.Parameters[1];
        }

        [MessageProcessor("003")]
        protected void ProcessMessageReplyCreated(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.ServerCreatedMessage = message.Parameters[1];
        }

        [MessageProcessor("004")]
        protected void ProcessMessageReplyMyInfo(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.ServerName = message.Parameters[1];
            Debug.Assert(message.Parameters[2] != null);
            this.ServerVersion = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            this.ServerAvailableUserModes = message.Parameters[3];
            Debug.Assert(message.Parameters[4] != null);
            this.ServerAvailableChannelModes = message.Parameters[4];
        }

        [MessageProcessor("005")]
        protected void ProcessMessageReplyBounceOrISupport(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            // Check if message is RPL_BOUNCE or RPL_ISUPPORT.
            if (message.Parameters[1].StartsWith("Try server"))
            {
                // RPL_BOUNCE
                // Current server is redirecting client to new server.
                var textParts = message.Parameters[0].Split(' ', ',');
                var serverAddress = textParts[2];
                var serverPort = int.Parse(textParts[6]);
                OnServerBounce(new IrcServerInfoEventArgs(serverAddress, serverPort));
            }
            else
            {
                // RPL_ISUPPRT
                // Add key/value pairs to dictionary of supported server features.
                for (int i = 1; i < message.Parameters.Count - 1; i++)
                {
                    if (message.Parameters[i + 1] == null)
                        break;
                    var tokenParts = message.Parameters[i].Split('=');
                    this.serverSupportedFeatures.Add(tokenParts[0], tokenParts.Length == 1 ? null : tokenParts[1]);
                }
                OnServerSupportedFeaturesReceived(new EventArgs());
            }
        }

        [MessageProcessor("332")]
        protected void ProcessMessageReplyTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            var channel = this.channels.Single(c => c.Name == message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            channel.Topic = message.Parameters[2];
        }

        [MessageProcessor("353")]
        protected void ProcessMessageReplyNameReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[2] != null);
            var channel = this.channels.Single(c => c.Name == message.Parameters[2]);
            if (channel != null)
            {
                Debug.Assert(message.Parameters[1] != null);
                Debug.Assert(message.Parameters[1].Length == 1);
                switch (message.Parameters[1][0])
                {
                    case '=':
                        channel.Type = IrcChannelType.Public;
                        break;
                    case '*':
                        channel.Type = IrcChannelType.Private;
                        break;
                    case '@':
                        channel.Type = IrcChannelType.Secret;
                        break;
                    default:
                        throw new InvalidOperationException(string.Format(errorMessageInvalidChannelType,
                            message.Parameters[1]));
                }

                Debug.Assert(message.Parameters[3] != null);
                foreach (var userId in message.Parameters[3].Split(' '))
                {
                    if (userId.Length == 0)
                        return;

                    // Extract nick name and mode of user.
                    string userMode;
                    string userNickName;
                    switch (userId[0])
                    {
                        case '@':
                            userMode = "o";
                            userNickName = userId.Substring(1);
                            break;
                        case '+':
                            userMode = "v";
                            userNickName = userId.Substring(1);
                            break;
                        default:
                            userMode = string.Empty;
                            userNickName = userId;
                            break;
                    }

                    // Find user by nick name and add it to collection of channel users.
                    var user = GetUserByNickName(userNickName);
                    channel.HandleUserJoined(new IrcChannelUser(user, userMode));
                }
            }
        }

        [MessageProcessor("366")]
        protected void ProcessMessageReplyEndOfNames(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            var channel = this.channels.Single(c => c.Name == message.Parameters[1]);
            channel.HandleUsersListReceived();
        }

        [MessageProcessor("372")]
        protected void ProcessMessageReplyMotd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.Clear();
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        [MessageProcessor("375")]
        protected void ProcessMessageReplyMotdStart(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        [MessageProcessor("376")]
        protected void ProcessMessageReplyMotdEnd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);

            this.MessageOfTheDay = this.motdBuilder.ToString();
            OnMotdReceived(new EventArgs());
        }

        #endregion

        #region Message Sending

        protected void SendMessagePassword(string password)
        {
            WriteMessage(null, "pass", password);
        }

        protected void SendMessageNick(string nickName)
        {
            WriteMessage(null, "nick", nickName);
        }

        protected void SendMessageUser(string userName, int userMode, string realName)
        {
            WriteMessage(null, "user", userName, userMode.ToString(), "*", realName);
        }

        protected void SendMessageService(string nickName, string distribution, string description = "")
        {
            WriteMessage(null, "service", nickName, distribution, "0", "0", description);
        }

        protected void SendMessageOper(string userName, string password)
        {
            WriteMessage(null, "oper", userName, password);
        }

        protected void SendMessageUserMode(string nickName, string modes = null)
        {
            WriteMessage(null, "mode", nickName, modes);
        }

        protected void SendMessageQuit(string message = null)
        {
            WriteMessage(null, "quit", message);
        }

        protected void SendMessageSquit(string server, string comment)
        {
            WriteMessage(null, "squit", server, comment);
        }

        protected void SendMessageLeaveAll()
        {
            WriteMessage(null, "join", "0");
        }

        protected void SendMessageJoin(IEnumerable<Tuple<string, string>> channels)
        {
            WriteMessage(null, "join", string.Join(",", channels.Select(c => c.Item1)),
                string.Join(",", channels.Select(c => c.Item2)));
        }

        protected void SendMessageJoin(IEnumerable<string> channels)
        {
            WriteMessage(null, "join", string.Join(",", channels));
        }

        protected void SendMessagePart(IEnumerable<string> channels, string comment = null)
        {
            WriteMessage(null, "part", string.Join(",", channels), comment);
        }

        protected void SendMessageChannelMode(string channel, string modes = null)
        {
            WriteMessage(null, "mode", channel, modes);
        }

        protected void SendMessageChannelMode(string channel, string modes, IEnumerable<string> modeParameters = null)
        {
            string modeParametersList = null;
            if (modeParameters != null)
            {
                var modeParametersArray = modeParameters.ToArray();
                if (modeParametersArray.Length > 3)
                    throw new ArgumentException(errorMessageTooManyModeParameters);
                modeParametersList = string.Join(",", modeParametersArray);
            }
            WriteMessage(null, "mode", channel, modes, modeParametersList);
        }

        protected void SendMessageTopic(string channel, string topic = null)
        {
            WriteMessage(null, "topic", channel, topic);
        }

        protected void SendMessageNames(IEnumerable<string> channels = null, string target = null)
        {
            WriteMessage(null, "names", channels == null ? null : string.Join(",", channels), target);
        }

        protected void SendMessageList(IEnumerable<string> channels = null, string target = null)
        {
            WriteMessage(null, "list", channels == null ? null : string.Join(",", channels), target);
        }

        protected void SendMessageInvite(string channel, string nickName)
        {
            WriteMessage(null, "invite", nickName, channel);
        }

        protected void SendMessageKick(string channel, IEnumerable<string> nickNames, string comment = null)
        {
            WriteMessage(null, "kick", channel, string.Join(",", nickNames), comment);
        }

        protected void SendMessageKick(IEnumerable<Tuple<string, string>> users, string comment = null)
        {
            WriteMessage(null, "kick", string.Join(",", users.Select(user => user.Item1)),
                string.Join(",", users.Select(user => user.Item2)), comment);
        }

        protected void SendMessagePrivateMessage(string target, string text)
        {
            WriteMessage(null, "privmsg", target, text);
        }

        protected void SendMessageNotice(string target, string text)
        {
            WriteMessage(null, "notice", target, text);
        }

        protected void SendMessageMotd(string target = null)
        {
            WriteMessage(null, "motd", target);
        }

        protected void SendMessageLusers(string serverMask = null, string target = null)
        {
            WriteMessage(null, "lusers", serverMask, target);
        }

        protected void SendMessageVersion(string target = null)
        {
            WriteMessage(null, "version", target);
        }

        protected void SendMessageStats(string query = null, string target = null)
        {
            WriteMessage(null, "stats", query, target);
        }

        protected void SendMessageLinks(string serverMask = null, string remoteServer = null)
        {
            WriteMessage(null, "links", remoteServer, serverMask);
        }

        protected void SendMessageTime(string target = null)
        {
            WriteMessage(null, "time", target);
        }

        protected void SendMessageConnect(string target, int port, string remoteServer = null)
        {
            WriteMessage(null, "connect", target, port.ToString(), remoteServer);
        }

        protected void SendMessageTrace(string target = null)
        {
            WriteMessage(null, "trace", target);
        }

        protected void SendMessageAdmin(string target = null)
        {
            WriteMessage(null, "admin", target);
        }

        protected void SendMessageInfo(string target = null)
        {
            WriteMessage(null, "info", target);
        }

        protected void SendMessageServlist(string mask = null, string type = null)
        {
            WriteMessage(null, "servlist", mask, type);
        }

        protected void SendMessageSquery(string serviceName, string text)
        {
            WriteMessage(null, "squery", serviceName, text);
        }

        protected void SendMessageWho(string mask = null, bool onlyOperators = false)
        {
            WriteMessage(null, "who", mask, onlyOperators ? "o" : null);
        }

        protected void SendMessageWhois(IEnumerable<string> masks, string target = null)
        {
            WriteMessage(null, "whois", target, string.Join(",", masks));
        }

        protected void SendMessageWhowas(IEnumerable<string> nickNames, int count = -1, string target = null)
        {
            WriteMessage(null, "whowas", string.Join(",", nickNames), count.ToString(), target);
        }

        protected void SendMessageKill(string nickName, string comment)
        {
            WriteMessage(null, "kill", nickName, comment);
        }

        protected void SendMessagePing(string server, string target = null)
        {
            WriteMessage(null, "ping", server, target);
        }

        protected void SendMessagePong(string server, string target = null)
        {
            WriteMessage(null, "pong", server, target);
        }

        protected void SendMessageAway(string text = null)
        {
            WriteMessage(null, "away", text);
        }

        protected void SendMessageRehash()
        {
            WriteMessage(null, "rehash");
        }

        protected void SendMessageDie()
        {
            WriteMessage(null, "die");
        }

        protected void SendMessageRestart()
        {
            WriteMessage(null, "restart");
        }

        protected void SendMessageSummon(string user, string target = null, string channel = null)
        {
            WriteMessage(null, "summon", user, target, channel);
        }

        protected void SendMessageUsers(string target = null)
        {
            WriteMessage(null, "users", target);
        }

        protected void SendMessageWallpos(string text)
        {
            WriteMessage(null, "wallops", text);
        }

        protected void SendMessageUserhost(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "userhost", nickNames);
        }

        protected void SendMessageIson(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "ison", nickNames);
        }

        #endregion

        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            WriteMessage(new IrcMessage(null, command, parameters));
        }

        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            WriteMessage(new IrcMessage(null, command, parameters.ToArray()));
        }

        protected void WriteMessage(IrcMessage message)
        {
            if (message.Parameters.Count > maxParamsCount)
                throw new ArgumentException(errorMessageTooManyParams, "parameters");

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

        private string CheckMiddleParam(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\0' || value[i] == '\r' || value[i] == '\n' || value[i] == ' ' ||
                    (i == 0 && value[i] == ':'))
                    throw new ArgumentException(string.Format(errorMessageInvalidMiddleParam, value), "value");
            }

            return value;
        }

        private string CheckTrailingParam(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == '\0' || value[i] == '\r' || value[i] == '\n')
                    throw new ArgumentException(string.Format(errorMessageInvalidTrailingParam, value), "value");
            }

            return value;
        }

        private void WriteMessage(string line)
        {
            if (line.Length > 510)
                throw new ArgumentException(errorMessageLineTooLong, "line");

            try
            {
                this.writer.WriteLine(line);
                this.writer.Flush();

                Trace.WriteLine("<<< " + line);
            }
            catch (IOException exIO)
            {
                var socketException = exIO.InnerException as SocketException;
                if (socketException != null)
                {
                    switch (socketException.SocketErrorCode)
                    {
                        case SocketError.NotConnected:
                            DisconnectInternal();
                            return;
                    }
                }

                OnError(new IrcErrorEventArgs(exIO));
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
                DisconnectInternal();
            }
#endif
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
                throw new ArgumentException(errorMessageInvalidNickName, "nickName");
            if (userName == null)
                throw new ArgumentException(errorMessageInvalidNickName, "userName");
            if (realName == null)
                throw new ArgumentException(errorMessageInvalidNickName, "realName");

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
                SendMessageQuit();
                this.client.Client.Disconnect(true);
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
                this.readThread.Start();

                OnConnected(new EventArgs());
            }
            catch (Exception ex)
            {
                OnConnectFailed(new IrcErrorEventArgs(ex));
            }

            HandleClientConnected((IrcConnectContext)ar.AsyncState);
        }

        private void HandleClientConnecting()
        {
#if DEBUG
            Trace.TraceInformation("Connecting to server...");
#endif

            this.canDisconnect = true;
        }

        private void HandleClientConnected(IrcConnectContext initState)
        {
#if DEBUG
            Trace.TraceInformation("Connected to server '{0}'.",
                ((IPEndPoint)this.client.Client.RemoteEndPoint).Address);
#endif

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
#if DEBUG
            Trace.TraceInformation("Disconnected from server.");
#endif

            ResetState();
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
            return namesList.Split(',').Select(n => this.channels.Single(c => c.Name == n));
        }

        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
            return nickNamesList.Split(',').Select(n => this.users.Single(u => u.NickName == n));
        }

        protected IrcUser GetUserByPrefix(string prefix)
        {
            // Extract user information from prefix. Format is `nickname [ [ "!" user ] "@" host ]`.
            string nickName = null, userName = null, host = null;
            var sep1Index = prefix.IndexOf('!');
            var sep2Index = prefix.IndexOf('@', sep1Index + 1);
            if (sep1Index == -1)
            {
                nickName = prefix;
            }
            else
            {
                nickName = prefix.Substring(0, sep1Index);
                if (sep2Index == -1)
                {
                    userName = prefix.Substring(sep1Index + 1);
                }
                else
                {
                    userName = prefix.Substring(sep1Index + 1, sep2Index - sep1Index - 1);
                    host = prefix.Substring(sep2Index + 1);
                }
            }

            // Find user by nick name. If no user exists in list, create it and set its properties.
            bool createdNew;
            var user = GetUserByNickName(nickName, out createdNew);
            if (createdNew)
            {
                user.UserName = userName;
                user.Host = host;
            }
            return user;
        }

        protected IrcUser GetUserByNickName(string nickName)
        {
            bool createdNew;
            return GetUserByNickName(nickName, out createdNew);
        }

        protected IrcUser GetUserByNickName(string nickName, out bool createdNew)
        {
            // Search for user with given nick name in list of known users. If it does not exist, add it.
            var user = users.SingleOrDefault(u => u.NickName == nickName);
            if (user == null)
            {
                user = new IrcUser(nickName);
                this.users.Add(user);
                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return user;
        }

        protected bool IsChannel(string name)
        {
            return name[0] == '&' || name[0] == '#' || name[0] == '+' || name[0] == '!';
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
            if (this.Connected != null)
                this.Connected(this, e);
        }

        protected virtual void OnConnectFailed(IrcErrorEventArgs e)
        {
            if (this.ConnectFailed != null)
                this.ConnectFailed(this, e);
        }

        protected virtual void OnDisconnected(EventArgs e)
        {
            if (this.Disconnected != null)
                this.Disconnected(this, e);
        }

        protected virtual void OnError(IrcErrorEventArgs e)
        {
            if (this.Error != null)
                this.Error(this, e);
        }

        protected virtual void OnProtocolError(IrcErrorEventArgs e)
        {
            if (this.ProtocolError != null)
                this.ProtocolError(this, e);
        }

        protected virtual void OnRegistered(EventArgs e)
        {
            if (this.Registered != null)
                this.Registered(this, e);
        }

        protected virtual void OnServerBounce(IrcServerInfoEventArgs e)
        {
            if (this.ServerBounce != null)
                this.ServerBounce(this, e);
        }

        protected virtual void OnServerSupportedFeaturesReceived(EventArgs e)
        {
            if (this.ServerSupportedFeaturesReceived != null)
                this.ServerSupportedFeaturesReceived(this, e);
        }

        protected virtual void OnPingReceived(IrcPingOrPongReceivedEventArgs e)
        {
            if (this.PingReceived != null)
                this.PongReceived(this, e);
        }

        protected virtual void OnPongReceived(IrcPingOrPongReceivedEventArgs e)
        {
            if (this.PongReceived != null)
                this.PongReceived(this, e);
        }

        protected virtual void OnMotdReceived(EventArgs e)
        {
            if (this.MotdReceived != null)
                this.MotdReceived(this, e);
        }

        protected virtual void OnChannelJoined(IrcChannelEventArgs e)
        {
            if (this.ChannelJoined != null)
                this.ChannelJoined(this, e);
        }

        protected virtual void OnChannelParted(IrcChannelEventArgs e)
        {
            if (this.ChannelParted != null)
                this.ChannelParted(this, e);
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
            public string Prefix;
            public string Command;
            public IList<string> Parameters;

            public IrcMessage(string command, IList<string> parameters)
                : this(null, command, parameters)
            {
            }

            public IrcMessage(string prefix, string command, IList<string> parameters)
            {
                this.Prefix = prefix;
                this.Command = command;
                this.Parameters = parameters;
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
