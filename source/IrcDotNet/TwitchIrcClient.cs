using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;

namespace IrcDotNet
{
    public class TwitchIrcClient : StandardIrcClient
    {
        public override void Connect(EndPoint remoteEndPoint, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            registrationInfo.NickName = registrationInfo.NickName.ToLower();
            base.Connect(remoteEndPoint, useSsl, registrationInfo);
        }

        protected override void WriteMessage(string message, object token = null)
        {
            base.WriteMessage(message,
                token ?? new IrcRawMessageEventArgs(new IrcMessage(this, null, null, null), message));
        }

        protected override void OnChannelModeChanged(IrcChannel channel, IrcUser source, string newModes,
            IEnumerable<string> newModeParameters)
        {
            // Twitch doesn't actually send JOIN messages. This means we need to add users
            // to the channel when changing their mode if we haven't already.
            foreach (var username in newModeParameters)
            {
                var user = GetUserFromNickName(username);
                if (channel.GetChannelUser(user) == null)
                    channel.HandleUserJoined(new IrcChannelUser(user));
            }
        }

        protected internal override void ProcessMessageReplyWelcome(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] != null);

            Debug.Assert(message.Parameters[1] != null);
            WelcomeMessage = message.Parameters[1];

            // Twitch does not send a normal welcome message, so this code is actually incorrect.
            isRegistered = true;
            OnRegistered(new EventArgs());
        }

        protected internal override void ProcessMessageReplyMyInfo(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Twitch doesn't seem to give us this information.
            Debug.Assert(message.Parameters[1] == "-");
            OnClientInfoReceived(new EventArgs());
        }

        protected internal override void ProcessMessageReplyMotdStart(IrcMessage message)
        {
            Debug.Assert(message.Parameters[0] == localUser.NickName);

            // Looks like the motd doesn't start on the start message for twitch.
            Debug.Assert(message.Parameters[1] == "-");
            motdBuilder.Clear();
        }
    }
}