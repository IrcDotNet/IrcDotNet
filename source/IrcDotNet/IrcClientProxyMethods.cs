using System;
using System.Collections.Generic;
using System.Linq;

namespace IrcDotNet
{
    partial class IrcClient
    {
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

        internal void Join(IEnumerable<string> chs)
        {
            SendMessageJoin(chs);
        }

        internal void Join(IEnumerable<Tuple<string, string>> chs)
        {
            SendMessageJoin(chs);
        }

        internal void Leave(IEnumerable<string> chs, string comment = null)
        {
            SendMessagePart(chs, comment);
        }

        internal void SendPrivateMessage(IEnumerable<string> targetsNames, string text)
        {
            string[] targetsNamesArray = targetsNames.ToArray();
            IIrcMessageTarget[] targets = targetsNamesArray.Select(GetMessageTarget).ToArray();
            SendMessagePrivateMessage(targetsNamesArray, text);
            localUser.HandleMessageSent(targets, text);
        }

        internal void SendNotice(IEnumerable<string> targetsNames, string text)
        {
            string[] targetsNamesArray = targetsNames.ToArray();
            IIrcMessageTarget[] targets = targetsNamesArray.Select(GetMessageTarget).ToArray();
            SendMessageNotice(targetsNamesArray, text);
            localUser.HandleNoticeSent(targets, text);
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
    }
}
