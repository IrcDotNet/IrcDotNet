using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace IrcDotNet
{
    partial class IrcClient
    {
        [MessageProcessor("nick")]
        protected void ProcessMessageNick(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);
            this.localUser.NickName = message.Parameters[0];
        }

        [MessageProcessor("quit")]
        protected void ProcessMessageQuit(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));
            sourceUser.HandeQuit(message.Parameters[0]);
        }

        [MessageProcessor("join")]
        protected void ProcessMessageJoin(IrcMessage message)
        {
            var sourceUser = message.Source as IrcUser;
            if (sourceUser == null)
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));
            Debug.Assert(message.Parameters[0] != null);
            if (sourceUser == this.localUser)
            {
                // Local user has joined one or more channels. Add channels to collection.
                var channels = message.Parameters[0].Split(',').Select(n => new IrcChannel(n)).ToArray();
                this.channels.AddRange(channels);
                channels.ForEach(c => OnChannelJoined(new IrcChannelEventArgs(c, null)));
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
                throw new InvalidOperationException(string.Format(
                    Properties.Resources.ErrorMessageSourceNotUser, message.Source.Name));
            Debug.Assert(message.Parameters[0] != null);
            var comment = message.Parameters[1];
            if (sourceUser == this.localUser)
            {
                // Local user has parted one or more channels. Remove channel from collections.
                var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
                this.channels.RemoveRange(channels);
                channels.ForEach(c => OnChannelParted(new IrcChannelEventArgs(c, comment)));
            }
            else
            {
                // Remote user has parted one or more channels.
                var channels = GetChannelsFromList(message.Parameters[0]).ToArray();
                channels.ForEach(c => c.HandleUserParted(sourceUser, comment));
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
                throw new InvalidOperationException(string.Format(Properties.Resources.ErrorMessageCannotSetUserMode,
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
                    OnChannelParted(new IrcChannelEventArgs(channel, comment));
                    break;
                }
                else
                {
                    // Remote user was kicked from channel.
                    channelUser.Channel.HandleUserKicked(channelUser, comment);
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

        [MessageProcessor("305")]
        protected void ProcessMessageReplyUnAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            this.localUser.IsAway = false;
        }

        [MessageProcessor("306")]
        protected void ProcessMessageReplyNowAway(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            this.localUser.IsAway = true;
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
            OnWhoIsReplyReceived(new IrcUserEventArgs(user, null));
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
                if (channel.GetChannelUser(user) == null)
                    channel.HandleUserJoined(new IrcChannelUser(user, channelNameAndUserMode.Item2));
            }
        }

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

        [MessageProcessor("369")]
        protected void ProcessMessageReplyEndOfWhoWas(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var user = GetUserFromNickName(message.Parameters[1], false);
            OnWhoWasReplyReceived(new IrcUserEventArgs(user, null));
        }

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
            var userFlags = message.Parameters[6];
            Debug.Assert(userFlags.Length > 0);
            if (userFlags.Contains('H'))
                user.IsAway = false;
            else if (userFlags.Contains('G'))
                user.IsAway = true;
            user.IsOperator = userFlags.Contains('*');
            if (channel != null)
            {
                // Add user to channel if it does not already exist in it.
                var channelUser = channel.GetChannelUser(user);
                if (channelUser == null)
                {
                    channelUser = new IrcChannelUser(user);
                    channel.HandleUserJoined(channelUser);
                }

                if (userFlags.Contains('@'))
                    channelUser.HandleModeChanged(true, 'o');
                if (userFlags.Contains('+'))
                    channelUser.HandleModeChanged(false, 'v');
            }

            Debug.Assert(message.Parameters[7] != null);
            var lastParamParts = message.Parameters[7].Split(new char[] { ' ' }, 2);
            Debug.Assert(lastParamParts.Length == 2);
            user.HopCount = int.Parse(lastParamParts[0]);
            user.RealName = lastParamParts[1];
        }

        [MessageProcessor("315")]
        protected void ProcessMessageReplyEndOfWho(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == this.localUser.NickName);

            Debug.Assert(message.Parameters[1] != null);
            var mask = message.Parameters[1];
            OnWhoReplyReceived(new IrcNameEventArgs(mask));
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
    }
}
