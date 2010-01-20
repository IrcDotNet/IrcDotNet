using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Reflection;
using System.Globalization;

namespace IrcDotNet
{
    public class IrcClient : IDisposable
    {
        // Error messages used for throwing exceptions.
        private const string errorMessageTooManyParams = "No more than 15 command parameters may be specified.";
        private const string errorMessageLineTooLong = "The length of the line must not exceed 510 characters.";
        private const string errorMessageInvalidMiddleParam = "The non-trailing parameter '{0}' is invalid.";
        private const string errorMessageInvalidTrailingParam = "The trailing parameter '{0}' is invalid.";
        private const string errorMessageInvalidSource = "The source '{0}' of the message was not recognised as either a server or user.";
        private const string errorMessageInvalidMessageCommand = "The command '{0}' was not recognised.";
        private const string errorMessageInvalidPassword = "The specified password is invalid.";
        private const string errorMessageInvalidNickName = "The specified nick name is invalid.";
        private const string errorMessageInvalidUserName = "The specified user name is invalid.";
        private const string errorMessageInvalidRealName = "The specified real name is invalid.";
        private const string errorMessageInvalidUserMode = "The specified user mode is invalid.";
        private const string errorMessageCannotSetUserMode = "Cannot set user mode for '{0}'.";
        private const string errorMessageTooManyModeParameters = "No more than 3 mode parameters may be sent per message.";
        private const string errorMessageInvalidChannelType = "The channel type '{0}' sent by the server is invalid.";
        private const string errorMessageSourceNotUser = "The message source '{0}' is not a user.";
        private const string errorMessageInvalidTargetName = "A target name may not contain any ',' character.";
        private const string errorMessageTextCannotContainNewLine = "Text sent in a message or notice must not contain a new line";

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
            regexHostName = @"(?<host>[^%@]+?\..*)";
            regexChannelName = @"(?<channel>[#+!&].+)";
            regexTargetMask = @"(?<targetMask>[$#].+)";
            regexServerName = @"(?<server>[^%@]+?\..*)";
            regexNickNameId = string.Format(@"{0}(?:(?:!{1})?@{2})?", regexNickName, regexUserName, regexHostName);
            regexUserNameId = string.Format(@"{0}(?:(?:%{1})?@{2}|%{1})", regexUserName, regexHostName, regexServerName);
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
        private NetworkStream stream;
        private StreamWriter writer;
        private StreamReader reader;
        // True if client can currently be disconnected.
        private bool canDisconnect;
        // Dictionary of message processor routines, keyed by their command names.
        private Dictionary<string, MessageProcessor> messageProcessors;
        // Array of message processor routines, keyed by their numeric codes (000 to 999).
        private Dictionary<int, MessageProcessor> numMessageProcessors;

        private bool isDisposed = false;

        public IrcClient()
        {
            this.client = new TcpClient();
            this.readThread = new Thread(ReadLoop);
            this.canDisconnect = false;

            this.messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
            this.numMessageProcessors = new Dictionary<int, MessageProcessor>(1000);
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
        public event EventHandler<IrcUserEventArgs> WhoIsReplyReceived;
        public event EventHandler<EventArgs> WhoWasReplyReceived;

        public void WhoIs(params string[] nickNameMasks)
        {
            WhoIs((IEnumerable<string>)nickNameMasks);
        }

        public void WhoIs(IEnumerable<string> nickNameMasks)
        {
            SendMessageWhoIs(nickNameMasks);
        }

        public void WhoWas(params string[] nickNames)
        {
            WhoWas((IEnumerable<string>)nickNames);
        }

        public void WhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
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
                throw new ArgumentException(errorMessageTextCannotContainNewLine, "text");
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
                Trace.TraceWarning("Unknown message command '{0}'.", message.Command);
            }
        }

        #region Message Processing

        [MessageProcessor("nick")]
        protected void ProcessMessageNick(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            this.localUser.NickName = message.Parameters[0];
        }

        [MessageProcessor("join")]
        protected void ProcessMessageJoin(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(errorMessageSourceNotUser, message.Source.Name));
            Debug.Assert(message.Parameters[0] != null);
            if (sourceUser == this.localUser)
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
                channels.ForEach(c => c.HandleUserJoined(new IrcChannelUser(sourceUser)));
            }
        }

        [MessageProcessor("part")]
        protected void ProcessMessagePart(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(errorMessageSourceNotUser, message.Source.Name));
            Debug.Assert(message.Parameters[0] != null);
            if (sourceUser == this.localUser)
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
                channels.ForEach(c => c.HandleUserParted(new IrcChannelUser(sourceUser)));
            }
        }

        [MessageProcessor("mode")]
        protected void ProcessMessageMode(IrcMessage message)
        {
            // Check if mode applies to channel or user.
            Debug.Assert(message.Parameters[0] != null);
            if (IsChannelName(message.Parameters[0]))
            {
                var channel = GetChannelFromName(message.Parameters[0]);

                // Get channel modes and list of mode parameters from message parameters.
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
            var channel = GetChannelFromName(message.Parameters[0]);
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

        [MessageProcessor("privmsg")]
        protected void ProcessMessagePrivateMessage(IrcMessage message)
        {
            // Get list of message targets.
            Debug.Assert(message.Parameters[0] != null);
            var targets = message.Parameters[0].Split(',').Select(n => GetMessageTarget(n)).ToArray();

            // Get message text.
            Debug.Assert(message.Parameters[1] != null);
            var text = message.Parameters[1];

            // Process message for each given target.
            foreach (var curTarget in targets)
            {
                Debug.Assert(curTarget != null);
                var messageHandler = (curTarget as IIrcMessageReceiveHandler) ?? this.localUser;
                messageHandler.HandleMessageReceived(message.Source, targets, text);
            }
        }

        [MessageProcessor("notice")]
        protected void ProcessMessageNotice(IrcMessage message)
        {
            // Get list of message targets.
            Debug.Assert(message.Parameters[0] != null);
            var targets = message.Parameters[0].Split(',').Select(n => GetMessageTarget(n)).ToArray();

            // Get message text.
            Debug.Assert(message.Parameters[1] != null);
            var text = message.Parameters[1];

            // Process notice for each given target.
            foreach (var curTarget in targets)
            {
                Debug.Assert(curTarget != null);
                var messageHandler = (curTarget as IIrcMessageReceiveHandler) ?? this.localUser;
                messageHandler.HandleNoticeReceived(message.Source, targets, text);
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

        [MessageProcessor("error")]
        protected void ProcessMessageError(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var errorMessage = message.Parameters[0];
            OnErrorMessageReceived(new IrcErrorMessageEventArgs(errorMessage));
        }

        [MessageProcessor("001")]
        protected void ProcessMessageReplyWelcome(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            Debug.Assert(message.Parameters[1] != null);
            this.WelcomeMessage = message.Parameters[1];

            // Extract nick name, user name, and host name from welcome message. Use fallback info if not present.
            var nickNameIdMatch = Regex.Match(this.WelcomeMessage.Split(' ').Last(), regexNickNameId);
            this.localUser.NickName = nickNameIdMatch.Groups["nick"].GetValue() ?? this.localUser.NickName;
            this.localUser.UserName = nickNameIdMatch.Groups["user"].GetValue() ?? this.localUser.UserName;
            this.localUser.HostName = nickNameIdMatch.Groups["host"].GetValue() ?? this.localUser.HostName;

            this.isRegistered = true;
            OnRegistered(new EventArgs());
        }

        [MessageProcessor("002")]
        protected void ProcessMessageReplyYourHost(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            this.YourHostMessage = message.Parameters[1];
        }

        [MessageProcessor("003")]
        protected void ProcessMessageReplyCreated(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            this.ServerCreatedMessage = message.Parameters[1];
        }

        [MessageProcessor("004")]
        protected void ProcessMessageReplyMyInfo(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
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
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
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
                // RPL_ISUPPORT
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

        [MessageProcessor("311")]
        protected void ProcessMessageReplyWhoIsUser(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.UserName = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            user.HostName = message.Parameters[3];
            Debug.Assert(message.Parameters[4] != null);
            Debug.Assert(message.Parameters[5] != null);
            user.RealName = message.Parameters[5];
        }

        [MessageProcessor("312")]
        protected void ProcessMessageReplyWhoIsServer(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.ServerName = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            user.ServerInfo = message.Parameters[3];
        }

        [MessageProcessor("313")]
        protected void ProcessMessageReplyWhoIsOperator(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            user.IsOperator = true;
        }

        [MessageProcessor("317")]
        protected void ProcessMessageReplyWhoIsIdle(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.IdleDuration = TimeSpan.FromSeconds(int.Parse(message.Parameters[2]));
        }

        [MessageProcessor("318")]
        protected void ProcessMessageReplyEndOfWhoIs(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            OnWhoIsReplyReceived(new IrcUserEventArgs(user));
        }

        [MessageProcessor("319")]
        protected void ProcessMessageReplyWhoIsChannels(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            
            Debug.Assert(message.Parameters[2] != null);
            foreach (var channelId in message.Parameters[2].Split(' '))
            {
                if (channelId.Length == 0)
                    return;

                // Find user by nick name and add it to collection of channel users.
                var channelNameAndUserMode = ExtractUserMode(channelId);
                var channel = GetChannelFromName(channelNameAndUserMode.Item1);
                if(channel.GetChannelUser(user) == null)
                    channel.HandleUserJoined(new IrcChannelUser(user, channelNameAndUserMode.Item2));
            }
        }

        [MessageProcessor("332")]
        protected void ProcessMessageReplyTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            channel.Topic = message.Parameters[2];
        }

        [MessageProcessor("353")]
        protected void ProcessMessageReplyNameReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[2] != null);
            var channel = GetChannelFromName(message.Parameters[2]);
            if (channel != null)
            {
                Debug.Assert(message.Parameters[1] != null);
                Debug.Assert(message.Parameters[1].Length == 1);
                channel.Type = GetChannelType(message.Parameters[1][0]);

                Debug.Assert(message.Parameters[3] != null);
                foreach (var userId in message.Parameters[3].Split(' '))
                {
                    if (userId.Length == 0)
                        return;

                    // Find user by nick name and add it to collection of channel users.
                    var userNickNameAndMode = ExtractUserMode(userId);
                    var user = GetUserFromNickName(userNickNameAndMode.Item1);
                    channel.HandleUserJoined(new IrcChannelUser(user, userNickNameAndMode.Item2));
                }
            }
        }

        [MessageProcessor("366")]
        protected void ProcessMessageReplyEndOfNames(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            channel.HandleUsersListReceived();
        }

        [MessageProcessor("372")]
        protected void ProcessMessageReplyMotd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.Clear();
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        [MessageProcessor("375")]
        protected void ProcessMessageReplyMotdStart(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        [MessageProcessor("376")]
        protected void ProcessMessageReplyMotdEnd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);
            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);
            this.MessageOfTheDay = this.motdBuilder.ToString();
            OnMotdReceived(new EventArgs());
        }

        [MessageProcessor("400-599")]
        protected void ProcessMessageNumericError(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            // Extract error parameters and message text from message parameters.
            Debug.Assert(message.Parameters[1] != null);
            var errorParameters = new List<string>();
            string errorMessage = null;
            for (int i = 1; i < message.Parameters.Count; i++)
            {
                if (i + 1 == message.Parameters.Count || message.Parameters[i + 1] == null)
                {
                    errorMessage = message.Parameters[i];
                    break;
                }
                else
                {
                    errorParameters.Add(message.Parameters[i]);
                }
            }

            Debug.Assert(errorMessage != null);
            OnProtocolError(new IrcProtocolErrorEventArgs(int.Parse(message.Command), errorParameters, errorMessage));
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

        protected void SendMessagePrivateMessage(IEnumerable<string> targets, string text)
        {
            var targetsArray = targets.ToArray();
            foreach (var target in targetsArray)
            {
                if (target.Contains(","))
                    throw new ArgumentException(errorMessageInvalidTargetName, "arguments");
            }
            WriteMessage(null, "privmsg", string.Join(",", targetsArray), text);
        }

        protected void SendMessageNotice(IEnumerable<string> targets, string text)
        {
            var targetsArray = targets.ToArray();
            foreach (var target in targetsArray)
            {
                if (target.Contains(","))
                    throw new ArgumentException(errorMessageInvalidTargetName, "arguments");
            }
            WriteMessage(null, "notice", string.Join(",", targetsArray), text);
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

        protected void SendMessageWhoIs(IEnumerable<string> nickNameMasks, string target = null)
        {
            WriteMessage(null, "whois", target, string.Join(",", nickNameMasks));
        }

        protected void SendMessageWhoWas(IEnumerable<string> nickNames, int entriesCount = -1, string target = null)
        {
            WriteMessage(null, "whowas", string.Join(",", nickNames), entriesCount.ToString(), target);
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

        protected void SendMessageUserHost(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "userhost", nickNames);
        }

        protected void SendMessageIsOn(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "ison", nickNames);
        }

        #endregion

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

                HandleClientConnected((IrcConnectContext)ar.AsyncState);
                this.readThread.Start();

                OnConnected(new EventArgs());
            }
            catch (Exception ex)
            {
                OnConnectFailed(new IrcErrorEventArgs(ex));
            }
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
                    throw new InvalidOperationException(string.Format(errorMessageInvalidChannelType, type));
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
                throw new InvalidOperationException(string.Format(errorMessageInvalidSource, targetName));
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
                throw new InvalidOperationException(string.Format(errorMessageInvalidSource, prefix));
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

        protected IrcUser GetUserFromNickName(string nickName)
        {
            bool createdNew;
            return GetUserFromNickName(nickName, out createdNew);
        }

        protected IrcUser GetUserFromNickName(string nickName, out bool createdNew)
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

        protected virtual void OnProtocolError(IrcProtocolErrorEventArgs e)
        {
            if (this.ProtocolError != null)
                this.ProtocolError(this, e);
        }

        protected virtual void OnErrorMessageReceived(IrcErrorMessageEventArgs e)
        {
            if (this.ErrorMessageReceived != null)
                this.ErrorMessageReceived(this, e);
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

        protected virtual void OnWhoIsReplyReceived(IrcUserEventArgs e)
        {
            if (this.WhoIsReplyReceived != null)
                this.WhoIsReplyReceived(this, e);
        }

        protected virtual void OnWhoWasReplyReceived(EventArgs e)
        {
            if (this.WhoWasReplyReceived != null)
                this.WhoWasReplyReceived(this, e);
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
