using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using IrcDotNet.Collections;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    // Defines all message processors for the client.
    partial class IrcClient
    {
        /// <summary>
        ///     Process NICK messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("nick")]
        protected internal void ProcessMessageNick(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new ProtocolViolationException(string.Format(
                    Resources.MessageSourceNotUser, message.Source.Name));

            // Local or remote user has changed nick name.
            Debug.Assert(message.Parameters[0] != null);

            var oldNickName = sourceUser.NickName;
            var newNickName = message.Parameters[0];
            sourceUser.NickName = newNickName;
            OnNickChanged(new IrcNickChangedEventArgs(message, newNickName, oldNickName));
        }

        /// <summary>
        ///     Process QUIT messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("quit")]
        protected internal void ProcessMessageQuit(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new ProtocolViolationException(string.Format(
                    Resources.MessageSourceNotUser, message.Source.Name));

            // Remote user has quit server.
            Debug.Assert(message.Parameters[0] != null);
            sourceUser.HandleQuit(message, message.Parameters[0]);

            lock (((ICollection)Users).SyncRoot)
                users.Remove(sourceUser);
        }

        /// <summary>
        ///     Process JOIN messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("join")]
        protected internal void ProcessMessageJoin(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new ProtocolViolationException(string.Format(
                    Resources.MessageSourceNotUser, message.Source.Name));

            // Local or remote user has joined one or more channels.
            Debug.Assert(message.Parameters[0] != null);
            var chans = GetChannelsFromList(message.Parameters[0]).ToArray();
            if (sourceUser == localUser)
                chans.ForEach(c => localUser.HandleJoinedChannel(message, c));
            else
                chans.ForEach(c => c.HandleUserJoined(message, new IrcChannelUser(sourceUser)));
        }

        /// <summary>
        ///     Process PART messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("part")]
        protected internal void ProcessMessagePart(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new ProtocolViolationException(string.Format(
                    Resources.MessageSourceNotUser, message.Source.Name));

            // Local or remote user has left one or more channels.
            Debug.Assert(message.Parameters[0] != null);
            var comment = message.Parameters[1];
            var chans = GetChannelsFromList(message.Parameters[0]).ToArray();
            if (sourceUser == localUser)
                chans.ForEach(c => localUser.HandleLeftChannel(message, c));
            else
                chans.ForEach(c => c.HandleUserLeft(message, sourceUser, comment));
        }

        /// <summary>
        ///     Process MODE messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("mode")]
        protected internal void ProcessMessageMode(IrcMessage message)
        {
            // Check if mode applies to channel or user.
            Debug.Assert(message.Parameters[0] != null);
            if (IsChannelName(message.Parameters[0]))
            {
                var channel = GetChannelFromName(message.Parameters[0]);

                // Get channel modes and list of mode parameters from message parameters.
                Debug.Assert(message.Parameters[1] != null);
                var modesAndParameters = GetModeAndParameters(message.Parameters.Skip(1));
                var source = message.Source as IrcUser;
                OnChannelModeChanged(message, channel, source, modesAndParameters.Item1, modesAndParameters.Item2);
                channel.HandleModesChanged(message, source, modesAndParameters.Item1,
                    modesAndParameters.Item2);
            }
            else if (message.Parameters[0] == localUser.NickName)
            {
                Debug.Assert(message.Parameters[1] != null);
                localUser.HandleModesChanged(message.Parameters[1]);
            }
            else
            {
                throw new ProtocolViolationException(string.Format(Resources.MessageCannotSetUserMode,
                    message.Parameters[0]));
            }
        }

        protected virtual void OnChannelModeChanged(IrcMessage ircMessage, IrcChannel channel, IrcUser source, string newModes,
            IEnumerable<string> newModeParameters)
        {
        }

        /// <summary>
        ///     Process TOPIC messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("topic")]
        protected internal void ProcessMessageTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channel = GetChannelFromName(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            channel.HandleTopicChanged(message, message.Source as IrcUser, message.Parameters[1]);
        }

        /// <summary>
        ///     Process KICK messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("kick")]
        protected internal void ProcessMessageKick(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channels = GetChannelsFromList(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            var users = GetUsersFromList(message.Parameters[1]).ToArray();
            var comment = message.Parameters[2];

            // Handle kick command for each user given in message.
            foreach (var channelUser in channels.Zip(users,
                (channel, user) => channel.GetChannelUser(user)))
            {
                if (channelUser.User == localUser)
                {
                    // Local user was kicked from channel.
                    var channel = channelUser.Channel;
                    lock (((ICollection)Channels).SyncRoot)
                        this.channels.Remove(channel);

                    channelUser.Channel.HandleUserKicked(message, channelUser, comment);
                    localUser.HandleLeftChannel(message, channel);

                    // Local user has left channel. Do not process kicks of remote users.
                    break;
                }
                // Remote user was kicked from channel.
                channelUser.Channel.HandleUserKicked(message, channelUser, comment);
            }
        }

        /// <summary>
        ///     Process INVITE messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("invite")]
        protected internal void ProcessMessageInvite(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var user = GetUserFromNickName(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);

            Debug.Assert(message.Source is IrcUser);
            if (message.Source is IrcUser)
                user.HandleInviteReceived(message, (IrcUser)message.Source, channel);
        }

        /// <summary>
        ///     Process PRIVMSG messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("privmsg")]
        protected internal void ProcessMessagePrivateMessage(IrcMessage message)
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
                var messageHandler = curTarget as IIrcMessageReceiveHandler ?? localUser;
                messageHandler.HandleMessageReceived(message, message.Source, targets, text);
            }
        }

        /// <summary>
        ///     Process NOTICE messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("notice")]
        protected internal void ProcessMessageNotice(IrcMessage message)
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
                var messageHandler = curTarget as IIrcMessageReceiveHandler ?? localUser;
                if (messageHandler != null)
                    messageHandler.HandleNoticeReceived(message, message.Source, targets, text);
            }
        }

        /// <summary>
        ///     Process PING messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("ping")]
        protected internal void ProcessMessagePing(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            var target = message.Parameters[1];
            var ircPingReceivedEventArgs = new IrcPingReceivedEventArgs(message, server);
            try
            {
                OnPingReceived(ircPingReceivedEventArgs);
            }
            finally
            {
                if (ircPingReceivedEventArgs.SendPong)
                {
                    SendMessagePong(server, target);
                }
            }
        }

        /// <summary>
        ///     Process PONG messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("pong")]
        protected internal void ProcessMessagePong(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            OnPongReceived(new IrcPingOrPongReceivedEventArgs(message, server));
        }

        /// <summary>
        ///     Process ERROR messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("error")]
        protected internal void ProcessMessageError(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var errorMessage = message.Parameters[0];
            OnErrorMessageReceived(new IrcErrorMessageEventArgs(message, errorMessage));
        }

        /// <summary>
        ///     Process CAP messages received from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("cap")]
        protected internal void ProcessMessageCap(IrcMessage message)
        {
            string subCmd = message.Parameters[1];
            Debug.Assert(subCmd != null);

            switch (subCmd.ToUpper())
            {
                case "LS":
                    string[] caps = message.Parameters[2]?.Split(' ');
                    Debug.Assert(caps != null);

                    serverCapabilities.Clear();
                    serverCapabilities.AddRange(caps);

                    OnServerCapabilitiesReceived(new EventArgs());
                    break;
                case "LIST":
                    string[] active = message.Parameters[2]?.Split(' ');
                    OnActiveCapabilitiesReceived(new ActiveCapabilitiesEventArgs(active));
                    break;
                case "ACK":
                    string[] ackd = message.Parameters[2]?.Split(' ');
                    OnCapabilityAcknowledged(new CapabilityAcknowledgedEventArgs(true, ackd));
                    SendMessageCapEnd();
                    break;
                case "NACK":
                    string[] nakd = message.Parameters[2]?.Split(' ');
                    OnCapabilityAcknowledged(new CapabilityAcknowledgedEventArgs(false, nakd));
                    SendMessageCapEnd();
                    break;
            }
        }

        /// <summary>
        ///     Process RPL_WELCOME responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("001")]
        protected internal virtual void ProcessMessageReplyWelcome(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            Debug.Assert(message.Parameters[1] != null);
            WelcomeMessage = message.Parameters[1];

            // Extract nick name, user name, and host name from welcome message. Use fallback info if not present.
            var nickNameIdMatch = Regex.Match(WelcomeMessage.Split(' ').Last(), regexNickNameId);
            localUser.NickName = nickNameIdMatch.Groups["nick"].GetValue() ?? localUser.NickName;
            localUser.UserName = nickNameIdMatch.Groups["user"].GetValue() ?? localUser.UserName;
            localUser.HostName = nickNameIdMatch.Groups["host"].GetValue() ?? localUser.HostName;

            isRegistered = true;
            OnRegistered(new EventArgs());
        }

        /// <summary>
        ///     Process RPL_YOURHOST responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("002")]
        protected internal void ProcessMessageReplyYourHost(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            YourHostMessage = message.Parameters[1];
        }

        /// <summary>
        ///     Process RPL_CREATED responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("003")]
        protected internal void ProcessMessageReplyCreated(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            ServerCreatedMessage = message.Parameters[1];
        }

        /// <summary>
        ///     Process RPL_MYINFO responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("004")]
        protected internal virtual void ProcessMessageReplyMyInfo(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            ServerName = message.Parameters[1];
            Debug.Assert(message.Parameters[2] != null);
            ServerVersion = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            ServerAvailableUserModes = message.Parameters[3];
            Debug.Assert(message.Parameters[4] != null);
            ServerAvailableChannelModes = message.Parameters[4];

            // All initial information about client has now been received.
            OnClientInfoReceived(new EventArgs());
        }

        /// <summary>
        ///     Process RPL_BOUNCE and RPL_ISUPPORT responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("005")]
        protected internal void ProcessMessageReplyBounceOrISupport(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Check if message is RPL_BOUNCE or RPL_ISUPPORT.
            Debug.Assert(message.Parameters[1] != null);
            if (message.Parameters[1].StartsWith("Try server"))
            {
                // Message is RPL_BOUNCE.
                // Current server is redirecting client to new server.
                var textParts = message.Parameters[0].Split(' ', ',');
                var serverAddress = textParts[2];
                var serverPort = int.Parse(textParts[6]);

                OnServerBounce(new IrcServerInfoEventArgs(message, serverAddress, serverPort));
            }
            else
            {
                // Message is RPL_ISUPPORT.
                // Add key/value pairs to dictionary of supported server features.
                for (var i = 1; i < message.Parameters.Count - 1; i++)
                {
                    if (message.Parameters[i + 1] == null)
                        break;

                    var paramParts = message.Parameters[i].Split('=');
                    var paramName = paramParts[0];
                    var paramValue = paramParts.Length == 1 ? null : paramParts[1];
                    HandleISupportParameter(paramName, paramValue);
                    serverSupportedFeatures.Set(paramName, paramValue);
                }

                OnServerSupportedFeaturesReceived(new EventArgs());
            }
        }

        /// <summary>
        ///     Process RPL_STATSLINKINFO responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("211")]
        protected internal void ProcessMessageStatsLinkInfo(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.Connection, message);
        }

        /// <summary>
        ///     Process RPL_STATSCOMMANDS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("212")]
        protected internal void ProcessMessageStatsCommands(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.Command, message);
        }

        /// <summary>
        ///     Process RPL_STATSCLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("213")]
        protected internal void ProcessMessageStatsCLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.AllowedServerConnect, message);
        }

        /// <summary>
        ///     Process RPL_STATSNLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("214")]
        protected internal void ProcessMessageStatsNLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.AllowedServerAccept, message);
        }

        /// <summary>
        ///     Process RPL_STATSILINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("215")]
        protected internal void ProcessMessageStatsILine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.AllowedClient, message);
        }

        /// <summary>
        ///     Process RPL_STATSKLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("216")]
        protected internal void ProcessMessageStatsKLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.BannedClient, message);
        }

        /// <summary>
        ///     Process RPL_STATSYLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("218")]
        protected internal void ProcessMessageStatsYLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.ConnectionClass, message);
        }

        /// <summary>
        ///     Process RPL_ENDOFSTATS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("219")]
        protected internal void ProcessMessageEndOfStats(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            OnServerStatsReceived(new IrcServerStatsReceivedEventArgs(message, listedStatsEntries));
            listedStatsEntries = new List<IrcServerStatisticalEntry>();
        }

        /// <summary>
        ///     Process RPL_STATSLLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("241")]
        protected internal void ProcessMessageStatsLLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.LeafDepth, message);
        }

        /// <summary>
        ///     Process RPL_STATSUPTIME responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("242")]
        protected internal void ProcessMessageStatsUpTime(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.Uptime, message);
        }

        /// <summary>
        ///     Process RPL_STATSOLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("243")]
        protected internal void ProcessMessageStatsOLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.AllowedOperator, message);
        }

        /// <summary>
        ///     Process RPL_STATSHLINE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("244")]
        protected internal void ProcessMessageStatsHLine(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            HandleStatsEntryReceived((int)IrcServerStatisticalEntryCommonType.HubServer, message);
        }

        /// <summary>
        ///     Process RPL_LUSERCLIENT responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("251")]
        protected internal void ProcessMessageLUserClient(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Extract network information from text.
            Debug.Assert(message.Parameters[1] != null);
            var info = message.Parameters[1];
            var infoParts = info.Split(' ');
            Debug.Assert(infoParts.Length == 10);
            networkInformation.VisibleUsersCount = int.Parse(infoParts[2]);
            networkInformation.InvisibleUsersCount = int.Parse(infoParts[5]);
            networkInformation.ServersCount = int.Parse(infoParts[8]);

            OnNetworkInformationReceived(new IrcCommentEventArgs(message, info));
        }

        /// <summary>
        ///     Process RPL_LUSEROP responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("252")]
        protected internal void ProcessMessageLUserOp(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Extract network information from text.
            Debug.Assert(message.Parameters[1] != null);
            var info = message.Parameters[1];
            networkInformation.OperatorsCount = int.Parse(info);

            OnNetworkInformationReceived(new IrcCommentEventArgs(message, info));
        }

        /// <summary>
        ///     Process RPL_LUSERUNKNOWN responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("253")]
        protected internal void ProcessMessageLUserUnknown(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Extract network information from text.
            Debug.Assert(message.Parameters[1] != null);
            var info = message.Parameters[1];
            networkInformation.UnknownConnectionsCount = int.Parse(info);

            OnNetworkInformationReceived(new IrcCommentEventArgs(message, info));
        }

        /// <summary>
        ///     Process RPL_LUSERCHANNELS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("254")]
        protected internal void ProcessMessageLUserChannels(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Extract network information from text.
            Debug.Assert(message.Parameters[1] != null);
            var info = message.Parameters[1];
            networkInformation.ChannelsCount = int.Parse(info);

            OnNetworkInformationReceived(new IrcCommentEventArgs(message, info));
        }

        /// <summary>
        ///     Process RPL_LUSERME responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("255")]
        protected internal void ProcessMessageLUserMe(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Extract network information from text.
            Debug.Assert(message.Parameters[1] != null);
            var info = message.Parameters[1];
            var infoParts = info.Split(' ');
            for (var i = 0; i < infoParts.Length; i++)
            {
                switch (infoParts[i].ToLowerInvariant())
                {
                    case "user":
                    case "users":
                        networkInformation.ServerClientsCount = int.Parse(infoParts[i - 1]);
                        break;
                    case "server":
                    case "servers":
                        networkInformation.ServerServersCount = int.Parse(infoParts[i - 1]);
                        break;
                    case "service":
                    case "services":
                        networkInformation.ServerClientsCount = int.Parse(infoParts[i - 1]);
                        break;
                }
            }

            OnNetworkInformationReceived(new IrcCommentEventArgs(message, info));
        }

        /// <summary>
        ///     Process RPL_AWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("301")]
        protected internal void ProcessMessageReplyAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.AwayMessage = message.Parameters[2];
            user.IsAway = true;
        }

        /// <summary>
        ///     Process RPL_ISON responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("303")]
        protected internal void ProcessMessageReplyIsOn(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Set each user listed in reply as online.
            Debug.Assert(message.Parameters[1] != null);
            var onlineUsers = message.Parameters[1].Split(' ').Select(n => GetUserFromNickName(n));
            onlineUsers.ForEach(u => u.IsOnline = true);
        }

        /// <summary>
        ///     Process RPL_UNAWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("305")]
        protected internal void ProcessMessageReplyUnAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            localUser.IsAway = false;
        }

        /// <summary>
        ///     Process RPL_NOWAWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("306")]
        protected internal void ProcessMessageReplyNowAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            localUser.IsAway = true;
        }

        /// <summary>
        ///     Process RPL_WHOISUSER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("311")]
        protected internal void ProcessMessageReplyWhoIsUser(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

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

        /// <summary>
        ///     Process RPL_WHOISSERVER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("312")]
        protected internal void ProcessMessageReplyWhoIsServer(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.ServerName = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            user.ServerInfo = message.Parameters[3];
        }

        /// <summary>
        ///     Process RPL_WHOISOPERATOR responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("313")]
        protected internal void ProcessMessageReplyWhoIsOperator(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            user.IsOperator = true;
        }

        /// <summary>
        ///     Process RPL_WHOWASUSER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("314")]
        protected internal void ProcessMessageReplyWhoWasUser(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1], false);
            Debug.Assert(message.Parameters[2] != null);
            user.UserName = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            user.HostName = message.Parameters[3];
            Debug.Assert(message.Parameters[4] != null);
            Debug.Assert(message.Parameters[5] != null);
            user.RealName = message.Parameters[5];
        }

        /// <summary>
        ///     Process RPL_ENDOFWHO responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("315")]
        protected internal void ProcessMessageReplyEndOfWho(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var mask = message.Parameters[1];
            OnWhoReplyReceived(new IrcNameEventArgs(message, mask));
        }

        /// <summary>
        ///     Process RPL_WHOISIDLE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("317")]
        protected internal void ProcessMessageReplyWhoIsIdle(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.IdleDuration = TimeSpan.FromSeconds(int.Parse(message.Parameters[2]));
        }

        /// <summary>
        ///     Process 318 responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("318")]
        protected internal void ProcessMessageReplyEndOfWhoIs(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            OnWhoIsReplyReceived(new IrcUserEventArgs(message, user, null));
        }

        /// <summary>
        ///     Process RPL_WHOISCHANNELS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("319")]
        protected internal void ProcessMessageReplyWhoIsChannels(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);

            Debug.Assert(message.Parameters[2] != null);
            foreach (var channelId in message.Parameters[2].Split(' '))
            {
                if (channelId.Length == 0)
                    return;

                // Find user by nick name and add it to collection of channel users.
                var channelNameAndUserMode = GetUserModeAndNickName(channelId);
                var channel = GetChannelFromName(channelNameAndUserMode.Item1);
                if (channel.GetChannelUser(user) == null)
                    channel.HandleUserJoined(message, new IrcChannelUser(user, channelNameAndUserMode.Item2));
            }
        }

        /// <summary>
        ///     Process RPL_LIST responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("322")]
        protected internal void ProcessMessageReplyList(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channelName = message.Parameters[1];
            Debug.Assert(message.Parameters[2] != null);
            var visibleUsersCount = int.Parse(message.Parameters[2]);
            Debug.Assert(message.Parameters[3] != null);
            var topic = message.Parameters[3];

            // Add channel information to temporary list.
            listedChannels.Add(new IrcChannelInfo(channelName, visibleUsersCount, topic));
        }

        /// <summary>
        ///     Process RPL_LISTEND responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("323")]
        protected internal void ProcessMessageReplyListEnd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            OnChannelListReceived(new IrcChannelListReceivedEventArgs(message, listedChannels));
            listedChannels = new List<IrcChannelInfo>();
        }

        /// <summary>
        ///     Process RPL_NOTOPIC responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("331")]
        protected internal void ProcessMessageReplyNoTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            channel.HandleTopicChanged(message, null, null);
        }

        /// <summary>
        ///     Process RPL_TOPIC responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("332")]
        protected internal void ProcessMessageReplyTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            channel.HandleTopicChanged(message, null, message.Parameters[2]);
        }

        /// <summary>
        ///     Process RPL_INVITING responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("341")]
        protected internal void ProcessMessageReplyInviting(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var invitedUser = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            var channel = GetChannelFromName(message.Parameters[2]);

            channel.HandleUserInvited(message, invitedUser);
        }

        /// <summary>
        ///     Process RPL_VERSION responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("351")]
        protected internal void ProcessMessageReplyVersion(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var versionInfo = message.Parameters[1];
            var versionSplitIndex = versionInfo.LastIndexOf('.');
            var version = versionInfo.Substring(0, versionSplitIndex);
            var debugLevel = versionInfo.Substring(versionSplitIndex + 1);
            Debug.Assert(message.Parameters[2] != null);
            var server = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            var comments = message.Parameters[3];

            OnServerVersionInfoReceived(new IrcServerVersionInfoEventArgs(message, version, debugLevel, server, comments));
        }

        /// <summary>
        ///     Process RPL_WHOREPLY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("352")]
        protected internal void ProcessMessageReplyWhoReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = message.Parameters[1] == "*" ? null : GetChannelFromName(message.Parameters[1]);

            Debug.Assert(message.Parameters[5] != null);
            var user = GetUserFromNickName(message.Parameters[5]);

            Debug.Assert(message.Parameters[2] != null);
            var userName = message.Parameters[2];
            Debug.Assert(message.Parameters[3] != null);
            user.HostName = message.Parameters[3];
            Debug.Assert(message.Parameters[4] != null);
            user.ServerName = message.Parameters[4];

            Debug.Assert(message.Parameters[6] != null);
            var userModeFlags = message.Parameters[6];
            Debug.Assert(userModeFlags.Length > 0);
            if (userModeFlags.Contains('H'))
                user.IsAway = false;
            else if (userModeFlags.Contains('G'))
                user.IsAway = true;
            user.IsOperator = userModeFlags.Contains('*');
            if (channel != null)
            {
                // Add user to channel if it does not already exist in it.
                var channelUser = channel.GetChannelUser(user);
                if (channelUser == null)
                {
                    channelUser = new IrcChannelUser(user);
                    channel.HandleUserJoined(message, channelUser);
                }

                // Set modes on user corresponding to given mode flags (prefix characters).
                foreach (var c in userModeFlags)
                {
                    char mode;
                    if (channelUserModesPrefixes.TryGetValue(c, out mode))
                        channelUser.HandleModeChanged(true, mode);
                    else
                        break;
                }
            }

            Debug.Assert(message.Parameters[7] != null);
            var lastParamParts = message.Parameters[7].SplitIntoPair(" ");
            user.HopCount = int.Parse(lastParamParts.Item1);
            if (lastParamParts.Item2 != null)
                user.RealName = lastParamParts.Item2;
        }

        /// <summary>
        ///     Process RPL_NAMEREPLY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("353")]
        protected internal void ProcessMessageReplyNameReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[2] != null);
            var channel = GetChannelFromName(message.Parameters[2]);
            if (channel != null)
            {
                Debug.Assert(message.Parameters[1] != null);
                Debug.Assert(message.Parameters[1].Length == 1);
                channel.HandleTypeChanged(GetChannelType(message.Parameters[1][0]));

                Debug.Assert(message.Parameters[3] != null);
                foreach (var userId in message.Parameters[3].Split(' '))
                {
                    if (userId.Length == 0)
                        return;

                    // Find user by nick name and add it to collection of channel users.
                    var userNickNameAndMode = GetUserModeAndNickName(userId);
                    var user = GetUserFromNickName(userNickNameAndMode.Item1);
                    channel.HandleUserNameReply(new IrcChannelUser(user, userNickNameAndMode.Item2));
                }
            }
        }

        /// <summary>
        ///     Process RPL_WHOSPCRPL responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("354")]
        protected internal void ProcessMessageReplyWhoXReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            OnWhoXReplyReceived(new IrcRawMessageEventArgs(message, message.Parameters[1]));
        }

        /// <summary>
        ///     Process RPL_LINKS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("364")]
        protected internal void ProcessMessageReplyLinks(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var hostName = message.Parameters[1];
            Debug.Assert(message.Parameters[2] != null);
            var clientServerHostName = message.Parameters[2];
            Debug.Assert(ServerName == null || clientServerHostName == ServerName);
            Debug.Assert(message.Parameters[3] != null);
            var infoParts = message.Parameters[3].SplitIntoPair(" ");
            Debug.Assert(infoParts.Item2 != null);
            var hopCount = int.Parse(infoParts.Item1);
            var info = infoParts.Item2;

            // Add server information to temporary list.
            listedServerLinks.Add(new IrcServerInfo(hostName, hopCount, info));
        }

        /// <summary>
        ///     Process RPL_ENDOFLINKS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("365")]
        protected internal void ProcessMessageReplyEndOfLinks(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var mask = message.Parameters[1];

            OnServerLinksListReceived(new IrcServerLinksListReceivedEventArgs(message, listedServerLinks));
            listedServerLinks = new List<IrcServerInfo>();
        }

        /// <summary>
        ///     Process RPL_ENDOFNAMES responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("366")]
        protected internal void ProcessMessageReplyEndOfNames(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            channel.HandleUsersListReceived();
        }

        /// <summary>
        ///     Process RPL_ENDOFWHOWAS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("369")]
        protected internal void ProcessMessageReplyEndOfWhoWas(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1], false);
            OnWhoWasReplyReceived(new IrcUserEventArgs(message, user, null));
        }

        /// <summary>
        ///     Process RPL_MOTD responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("372")]
        protected internal void ProcessMessageReplyMotd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            motdBuilder.AppendLine(message.Parameters[1]);
        }

        /// <summary>
        ///     Process RPL_MOTDSTART responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("375")]
        protected internal virtual void ProcessMessageReplyMotdStart(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            motdBuilder.Clear();
            motdBuilder.AppendLine(message.Parameters[1]);
        }

        /// <summary>
        ///     Process RPL_ENDOFMOTD responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("376")]
        protected internal void ProcessMessageReplyMotdEnd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            motdBuilder.AppendLine(message.Parameters[1]);

            OnMotdReceived(new EventArgs());
        }

        /// <summary>
        ///     Process RPL_YOURESERVICE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("383")]
        protected internal void ProcessMessageReplyYouAreService(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            Debug.Assert(message.Parameters[1] != null);
            localUser.NickName = message.Parameters[1].Split(' ')[3];

            isRegistered = true;
            OnRegistered(new EventArgs());
        }

        /// <summary>
        ///     Process RPL_TIME responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("391")]
        protected internal void ProcessMessageReplyTime(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            Debug.Assert(message.Parameters[1] != null);
            var server = message.Parameters[1];
            Debug.Assert(message.Parameters[2] != null);
            var dateTime = message.Parameters[2];

            OnServerTimeReceived(new IrcServerTimeEventArgs(message, server, dateTime));
        }

        /// <summary>
        ///     Process numeric error (from 400 to 599) responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("400-599")]
        protected internal void ProcessMessageNumericError(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            // Extract error parameters and message text from message parameters.
            Debug.Assert(message.Parameters[1] != null);
            var errorParameters = new List<string>();
            string errorMessage = null;
            for (var i = 1; i < message.Parameters.Count; i++)
            {
                if (i + 1 == message.Parameters.Count || message.Parameters[i + 1] == null)
                {
                    errorMessage = message.Parameters[i];
                    break;
                }
                errorParameters.Add(message.Parameters[i]);
            }

            Debug.Assert(errorMessage != null);
            OnProtocolError(new IrcProtocolErrorEventArgs(message, int.Parse(message.Command), errorParameters,
                errorMessage));
        }
    }
}
