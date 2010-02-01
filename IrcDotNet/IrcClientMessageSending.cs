using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    partial class IrcClient
    {
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

        protected void SendMessageQuit(string comment = null)
        {
            WriteMessage(null, "quit", comment);
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
                    throw new ArgumentException(Properties.Resources.ErrorMessageTooManyModeParameters);
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
                    throw new ArgumentException(Properties.Resources.ErrorMessageInvalidTargetName, "arguments");
            }
            WriteMessage(null, "privmsg", string.Join(",", targetsArray), text);
        }

        protected void SendMessageNotice(IEnumerable<string> targets, string text)
        {
            var targetsArray = targets.ToArray();
            foreach (var target in targetsArray)
            {
                if (target.Contains(","))
                    throw new ArgumentException(Properties.Resources.ErrorMessageInvalidTargetName, "arguments");
            }
            WriteMessage(null, "notice", string.Join(",", targetsArray), text);
        }

        protected void SendMessageMotd(string target = null)
        {
            WriteMessage(null, "motd", target);
        }

        protected void SendMessageLUsers(string serverMask = null, string target = null)
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
    }
}
