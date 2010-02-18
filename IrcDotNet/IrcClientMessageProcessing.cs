using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    // Defines all message processors for the client.
    partial class IrcClient
    {
        /// <summary>
        /// Process NICK messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("nick")]
        protected void ProcessMessageNick(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));

            // Local or remote user has changed nick name.
            Debug.Assert(message.Parameters[0] != null);
            sourceUser.NickName = message.Parameters[0];
        }

        /// <summary>
        /// Process QUIT messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("quit")]
        protected void ProcessMessageQuit(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));

            // Remote user has quit server.
            Debug.Assert(message.Parameters[0] != null);
            sourceUser.HandeQuit(message.Parameters[0]);
            this.users.Remove(sourceUser);
        }

        /// <summary>
        /// Process JOIN messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("join")]
        protected void ProcessMessageJoin(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));

            // Local or remote user has joined one or more channels.
            Debug.Assert(message.Parameters[0] != null);
            var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
            if (sourceUser == this.localUser)
                channels.ForEach(c => this.localUser.HandleJoinedChannel(c));
            else
                channels.ForEach(c => c.HandleUserJoined(new IrcChannelUser(sourceUser)));
        }

        /// <summary>
        /// Process PART messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("part")]
        protected void ProcessMessagePart(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));

            // Local or remote user has left one or more channels.
            Debug.Assert(message.Parameters[0] != null);
            var comment = message.Parameters[1];
            var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
            if (sourceUser == this.localUser)
                channels.ForEach(c => this.localUser.HandleLeftChannel(c));
            else
                channels.ForEach(c => c.HandleUserLeft(sourceUser, comment));
        }

        /// <summary>
        /// Process MODE messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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
                throw new InvalidOperationException(string.Format(Properties.Resources.ErrorMessageCannotSetUserMode,
                    message.Parameters[0]));
            }
        }

        /// <summary>
        /// Process TOPIC messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("topic")]
        protected void ProcessMessageTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channel = GetChannelFromName(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            channel.Topic = message.Parameters[1];
        }

        /// <summary>
        /// Process KICK messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("kick")]
        protected void ProcessMessageKick(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var channels = GetChannelsFromList(message.Parameters[0]);
            Debug.Assert(message.Parameters[1] != null);
            var users = GetUsersFromList(message.Parameters[1]).ToArray();
            var comment = message.Parameters[2];

            // Handle kick command for each user given in message.
            foreach (var channelUser in Enumerable.Zip(channels, users,
                (channel, user) => channel.GetChannelUser(user)))
            {
                if (channelUser.User == this.localUser)
                {
                    // Local user was kicked from channel.
                    var channel = channelUser.Channel;
                    this.channels.Remove(channel);
                    channelUser.Channel.HandleUserKicked(channelUser, comment);
                    this.localUser.HandleLeftChannel(channel);

                    // Local user has left channel. Do not process other kicks.
                    break;
                }
                else
                {
                    // Remote user was kicked from channel.
                    channelUser.Channel.HandleUserKicked(channelUser, comment);
                }
            }
        }

        /// <summary>
        /// Process PRIVMSG messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

        /// <summary>
        /// Process NOTICE messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

        /// <summary>
        /// Process PING messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("ping")]
        protected void ProcessMessagePing(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            var target = message.Parameters[1];
            OnPingReceived(new IrcPingOrPongReceivedEventArgs(server));
            SendMessagePong(server, target);
        }

        /// <summary>
        /// Process PONG messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("pong")]
        protected void ProcessMessagePong(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var server = message.Parameters[0];
            OnPongReceived(new IrcPingOrPongReceivedEventArgs(server));
        }

        /// <summary>
        /// Process ERROR messages from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("error")]
        protected void ProcessMessageError(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            var errorMessage = message.Parameters[0];
            OnErrorMessageReceived(new IrcErrorMessageEventArgs(errorMessage));
        }

        /// <summary>
        /// Process RPL_WELCOME responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

        /// <summary>
        /// Process RPL_YOURHOST responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("002")]
        protected void ProcessMessageReplyYourHost(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            this.YourHostMessage = message.Parameters[1];
        }

        /// <summary>
        /// Process RPL_CREATED responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("003")]
        protected void ProcessMessageReplyCreated(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            this.ServerCreatedMessage = message.Parameters[1];
        }

        /// <summary>
        /// Process RPL_MYINFO responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

            // All initial information about client has now been received.
            OnClientInfoReceived(new EventArgs());
        }

        /// <summary>
        /// Process RPL_BOUNCE and RPL_ISUPPORT responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

                    var paramParts = message.Parameters[i].Split('=');
                    var paramName = paramParts[0];
                    var paramValue = paramParts.Length == 1 ? null : paramParts[1];
                    HandleISupportParameter(paramName, paramValue);
                    this.serverSupportedFeatures.Add(paramName, paramValue);
                }
                OnServerSupportedFeaturesReceived(new EventArgs());
            }
        }

        /// <summary>
        /// Process RPL_ISON responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("303")]
        protected void ProcessMessageReplyIsOn(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            // Set each user listed in reply as online.
            Debug.Assert(message.Parameters[1] != null);
            var onlineUsers = message.Parameters[1].Split(' ').Select(n => GetUserFromNickName(n));
            onlineUsers.ForEach(u => u.IsOnline = true);
        }

        /// <summary>
        /// Process RPL_AWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("301")]
        protected void ProcessMessageReplyAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.AwayMessage = message.Parameters[2];
            user.IsAway = true;
        }

        /// <summary>
        /// Process RPL_UNAWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("305")]
        protected void ProcessMessageReplyUnAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            this.localUser.IsAway = false;
        }

        /// <summary>
        /// Process RPL_NOWAWAY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("306")]
        protected void ProcessMessageReplyNowAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            this.localUser.IsAway = true;
        }

        /// <summary>
        /// Process RPL_WHOISUSER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

        /// <summary>
        /// Process RPL_WHOISSERVER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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

        /// <summary>
        /// Process RPL_WHOISOPERATOR responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("313")]
        protected void ProcessMessageReplyWhoIsOperator(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            user.IsOperator = true;
        }

        /// <summary>
        /// Process RPL_WHOISIDLE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("317")]
        protected void ProcessMessageReplyWhoIsIdle(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            user.IdleDuration = TimeSpan.FromSeconds(int.Parse(message.Parameters[2]));
        }

        /// <summary>
        /// Process 318 responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("318")]
        protected void ProcessMessageReplyEndOfWhoIs(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1]);
            OnWhoIsReplyReceived(new IrcUserEventArgs(user, null));
        }

        /// <summary>
        /// Process RPL_WHOISCHANNELS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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
                var channelNameAndUserMode = GetUserModeAndNickName(channelId);
                var channel = GetChannelFromName(channelNameAndUserMode.Item1);
                if (channel.GetChannelUser(user) == null)
                    channel.HandleUserJoined(new IrcChannelUser(user, channelNameAndUserMode.Item2));
            }
        }

        /// <summary>
        /// Process RPL_WHOWASUSER responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("314")]
        protected void ProcessMessageReplyWhoWasUser(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

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
        /// Process RPL_ENDOFWHOWAS responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("369")]
        protected void ProcessMessageReplyEndOfWhoWas(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1], false);
            OnWhoWasReplyReceived(new IrcUserEventArgs(user, null));
        }

        /// <summary>
        /// Process RPL_WHOREPLY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("352")]
        protected void ProcessMessageReplyWhoReply(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

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
                    channel.HandleUserJoined(channelUser);
                }

                // Set modes on user corresponding to given mode flags (prefix characters).
                foreach(var c in userModeFlags)
                {
                    char mode;
                    if (this.channelUserModesPrefixes.TryGetValue(c, out mode))
                        channelUser.HandleModeChanged(true, mode);
                    else
                        break;
                }
            }

            Debug.Assert(message.Parameters[7] != null);
            var lastParamParts = message.Parameters[7].SplitAtIndex(message.Parameters[7].IndexOf(' '));
            user.HopCount = int.Parse(lastParamParts.Item1);
            if (lastParamParts.Item2 != null)
                user.RealName =lastParamParts.Item2;
        }

        /// <summary>
        /// Process RPL_ENDOFWHO responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("315")]
        protected void ProcessMessageReplyEndOfWho(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var mask = message.Parameters[1];
            OnWhoReplyReceived(new IrcNameEventArgs(mask));
        }

        /// <summary>
        /// Process RPL_NAMREPLY responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("332")]
        protected void ProcessMessageReplyTopic(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            Debug.Assert(message.Parameters[2] != null);
            channel.Topic = message.Parameters[2];
        }

        /// <summary>
        /// Process 353 responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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
                    var userNickNameAndMode = GetUserModeAndNickName(userId);
                    var user = GetUserFromNickName(userNickNameAndMode.Item1);
                    channel.HandleUserNameReply(new IrcChannelUser(user, userNickNameAndMode.Item2));
                }
            }
        }

        /// <summary>
        /// Process RPL_ENDOFNAMES responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("366")]
        protected void ProcessMessageReplyEndOfNames(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var channel = GetChannelFromName(message.Parameters[1]);
            channel.HandleUsersListReceived();
        }

        /// <summary>
        /// Process RPL_MOTD responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("372")]
        protected void ProcessMessageReplyMotd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        /// <summary>
        /// Process RPL_MOTDSTART responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("375")]
        protected void ProcessMessageReplyMotdStart(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.Clear();
            this.motdBuilder.AppendLine(message.Parameters[1]);
        }

        /// <summary>
        /// Process RPL_ENDOFMOTD responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("376")]
        protected void ProcessMessageReplyMotdEnd(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            this.motdBuilder.AppendLine(message.Parameters[1]);
            this.MessageOfTheDay = this.motdBuilder.ToString();
            OnMotdReceived(new EventArgs());
        }

        /// <summary>
        /// Process RPL_YOURESERVICE responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
        [MessageProcessor("383")]
        protected void ProcessMessageReplyYoureService(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            Debug.Assert(message.Parameters[1] != null);
            this.localUser.NickName = message.Parameters[1].Split(' ')[3];

            this.isRegistered = true;
            OnRegistered(new EventArgs());
        }

        /// <summary>
        /// Process numeric error (from 400 to 599) responses from the server.
        /// </summary>
        /// <param name="message">The message received from the server.</param>
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
    }
}
