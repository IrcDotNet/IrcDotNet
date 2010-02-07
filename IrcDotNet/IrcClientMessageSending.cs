using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    // TODO: Finish writing XML comments for methods.
    // Defines all message senders for the client.
    partial class IrcClient
    {
        /// <summary>
        /// Sends the password for registering the connection.
        /// This message must only be sent before the actual registration, which is done by
        /// <see cref="SendMessageUser"/> (for normal users) or <see cref="SendMessageService"/> (for services).
        /// </summary>
        /// <param name="password">The connection password.</param>
        protected void SendMessagePassword(string password)
        {
            WriteMessage(null, "pass", password);
        }

        /// <summary>
        /// Sends the nick name of the local user to the server. This command may be used either for intitially setting
        /// the nick name or changing it at any point.
        /// </summary>
        /// <param name="nickName">The nick name to set.</param>
        protected void SendMessageNick(string nickName)
        {
            WriteMessage(null, "nick", nickName);
        }

        /// <summary>
        /// Sends a request to register the client as a user on the server.
        /// </summary>
        /// <param name="userName">The user name of the user.</param>
        /// <param name="userMode">The initial mode of the user.</param>
        /// <param name="realName">The real name of the user.</param>
        protected void SendMessageUser(string userName, int userMode, string realName)
        {
            WriteMessage(null, "user", userName, userMode.ToString(), "*", realName);
        }

        /// <summary>
        /// Sends a request to register the client as a service on the server.
        /// </summary>
        /// <param name="nickName">The nick name of the service.</param>
        /// <param name="distribution">A wildcard expression for matching against server names that determines where the
        /// service is visible.</param>
        /// <param name="description">A description of the service.</param>
        protected void SendMessageService(string nickName, string distribution, string description = "")
        {
            WriteMessage(null, "service", nickName, distribution, "0", "0", description);
        }

        /// <summary>
        /// Sends a request for server operator priveleges.
        /// </summary>
        /// <param name="userName">The user name with which to register.</param>
        /// <param name="password">The password with which to register.</param>
        protected void SendMessageOper(string userName, string password)
        {
            WriteMessage(null, "oper", userName, password);
        }

        /// <summary>
        /// Sends an update or request for the current modes of the specified user.
        /// </summary>
        /// <param name="nickName">The nick name of the user whose modes to update/request.</param>
        /// <param name="modes">The mode string that indicates the modes of the user to change.</param>
        protected void SendMessageUserMode(string nickName, string modes = null)
        {
            WriteMessage(null, "mode", nickName, modes);
        }

        /// <summary>
        /// Sends a notification to the server indicating that the client is quitting the network.
        /// </summary>
        /// <param name="comment">The comment to send the server.</param>
        protected void SendMessageQuit(string comment = null)
        {
            WriteMessage(null, "quit", comment);
        }

        /// <summary>
        /// Sends a request to disconnect the specified server from the network.
        /// This command is only available to oeprators.
        /// </summary>
        /// <param name="serverName">The name of the server to disconnected from the network.</param>
        /// <param name="comment">The comment to send the server.</param>
        protected void SendMessageSquit(string serverName, string comment)
        {
            WriteMessage(null, "squit", serverName, comment);
        }

        /// <summary>
        /// Sends the message leave all.
        /// </summary>
        protected void SendMessageLeaveAll()
        {
            WriteMessage(null, "join", "0");
        }

        /// <summary>
        /// Sends the message join.
        /// </summary>
        /// <param name="channels">The channels.</param>
        protected void SendMessageJoin(IEnumerable<Tuple<string, string>> channels)
        {
            WriteMessage(null, "join", string.Join(",", channels.Select(c => c.Item1)),
                string.Join(",", channels.Select(c => c.Item2)));
        }

        /// <summary>
        /// Sends the message join.
        /// </summary>
        /// <param name="channels">The channels.</param>
        protected void SendMessageJoin(IEnumerable<string> channels)
        {
            WriteMessage(null, "join", string.Join(",", channels));
        }

        /// <summary>
        /// Sends the message part.
        /// </summary>
        /// <param name="channels">The channels.</param>
        /// <param name="comment">The comment.</param>
        protected void SendMessagePart(IEnumerable<string> channels, string comment = null)
        {
            WriteMessage(null, "part", string.Join(",", channels), comment);
        }

        /// <summary>
        /// Sends the message channel mode.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="modes">The modes.</param>
        protected void SendMessageChannelMode(string channel, string modes = null)
        {
            WriteMessage(null, "mode", channel, modes);
        }

        /// <summary>
        /// Sends the message channel mode.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="modes">The modes.</param>
        /// <param name="modeParameters">The mode parameters.</param>
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

        /// <summary>
        /// Sends the message topic.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="topic">The topic.</param>
        protected void SendMessageTopic(string channel, string topic = null)
        {
            WriteMessage(null, "topic", channel, topic);
        }

        /// <summary>
        /// Sends the message names.
        /// </summary>
        /// <param name="channels">The channels.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageNames(IEnumerable<string> channels = null, string target = null)
        {
            WriteMessage(null, "names", channels == null ? null : string.Join(",", channels), target);
        }

        /// <summary>
        /// Sends the message list.
        /// </summary>
        /// <param name="channels">The channels.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageList(IEnumerable<string> channels = null, string target = null)
        {
            WriteMessage(null, "list", channels == null ? null : string.Join(",", channels), target);
        }

        /// <summary>
        /// Sends the message invite.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="nickName">Name of the nick.</param>
        protected void SendMessageInvite(string channel, string nickName)
        {
            WriteMessage(null, "invite", nickName, channel);
        }

        /// <summary>
        /// Sends the message kick.
        /// </summary>
        /// <param name="channel">The channel.</param>
        /// <param name="nickNames">The nick names.</param>
        /// <param name="comment">The comment.</param>
        protected void SendMessageKick(string channel, IEnumerable<string> nickNames, string comment = null)
        {
            WriteMessage(null, "kick", channel, string.Join(",", nickNames), comment);
        }

        /// <summary>
        /// Sends the message kick.
        /// </summary>
        /// <param name="users">The users.</param>
        /// <param name="comment">The comment.</param>
        protected void SendMessageKick(IEnumerable<Tuple<string, string>> users, string comment = null)
        {
            WriteMessage(null, "kick", string.Join(",", users.Select(user => user.Item1)),
                string.Join(",", users.Select(user => user.Item2)), comment);
        }

        /// <summary>
        /// Sends the message private message.
        /// </summary>
        /// <param name="targets">The targets.</param>
        /// <param name="text">The text.</param>
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

        /// <summary>
        /// Sends the message notice.
        /// </summary>
        /// <param name="targets">The targets.</param>
        /// <param name="text">The text.</param>
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

        /// <summary>
        /// Sends the message motd.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageMotd(string target = null)
        {
            WriteMessage(null, "motd", target);
        }

        /// <summary>
        /// Sends the message L users.
        /// </summary>
        /// <param name="serverMask">The server mask.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageLUsers(string serverMask = null, string target = null)
        {
            WriteMessage(null, "lusers", serverMask, target);
        }

        /// <summary>
        /// Sends the message version.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageVersion(string target = null)
        {
            WriteMessage(null, "version", target);
        }

        /// <summary>
        /// Sends the message stats.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageStats(string query = null, string target = null)
        {
            WriteMessage(null, "stats", query, target);
        }

        /// <summary>
        /// Sends the message links.
        /// </summary>
        /// <param name="serverMask">The server mask.</param>
        /// <param name="remoteServer">The remote server.</param>
        protected void SendMessageLinks(string serverMask = null, string remoteServer = null)
        {
            WriteMessage(null, "links", remoteServer, serverMask);
        }

        /// <summary>
        /// Sends the message time.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageTime(string target = null)
        {
            WriteMessage(null, "time", target);
        }

        /// <summary>
        /// Sends the message connect.
        /// </summary>
        /// <param name="target">The target.</param>
        /// <param name="port">The port.</param>
        /// <param name="remoteServer">The remote server.</param>
        protected void SendMessageConnect(string target, int port, string remoteServer = null)
        {
            WriteMessage(null, "connect", target, port.ToString(), remoteServer);
        }

        /// <summary>
        /// Sends the message trace.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageTrace(string target = null)
        {
            WriteMessage(null, "trace", target);
        }

        /// <summary>
        /// Sends the message admin.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageAdmin(string target = null)
        {
            WriteMessage(null, "admin", target);
        }

        /// <summary>
        /// Sends the message info.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageInfo(string target = null)
        {
            WriteMessage(null, "info", target);
        }

        /// <summary>
        /// Sends the message servlist.
        /// </summary>
        /// <param name="mask">The mask.</param>
        /// <param name="type">The type.</param>
        protected void SendMessageServlist(string mask = null, string type = null)
        {
            WriteMessage(null, "servlist", mask, type);
        }

        /// <summary>
        /// Sends the message squery.
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="text">The text.</param>
        protected void SendMessageSquery(string serviceName, string text)
        {
            WriteMessage(null, "squery", serviceName, text);
        }

        /// <summary>
        /// Sends the message who.
        /// </summary>
        /// <param name="mask">The mask.</param>
        /// <param name="onlyOperators">if set to <c>true</c> [only operators].</param>
        protected void SendMessageWho(string mask = null, bool onlyOperators = false)
        {
            WriteMessage(null, "who", mask, onlyOperators ? "o" : null);
        }

        /// <summary>
        /// Sends the message who is.
        /// </summary>
        /// <param name="nickNameMasks">The nick name masks.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageWhoIs(IEnumerable<string> nickNameMasks, string target = null)
        {
            WriteMessage(null, "whois", target, string.Join(",", nickNameMasks));
        }

        /// <summary>
        /// Sends the message who was.
        /// </summary>
        /// <param name="nickNames">The nick names.</param>
        /// <param name="entriesCount">The entries count.</param>
        /// <param name="target">The target.</param>
        protected void SendMessageWhoWas(IEnumerable<string> nickNames, int entriesCount = -1, string target = null)
        {
            WriteMessage(null, "whowas", string.Join(",", nickNames), entriesCount.ToString(), target);
        }

        /// <summary>
        /// Sends the message kill.
        /// </summary>
        /// <param name="nickName">Name of the nick.</param>
        /// <param name="comment">The comment.</param>
        protected void SendMessageKill(string nickName, string comment)
        {
            WriteMessage(null, "kill", nickName, comment);
        }

        /// <summary>
        /// Sends the message ping.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="target">The target.</param>
        protected void SendMessagePing(string server, string target = null)
        {
            WriteMessage(null, "ping", server, target);
        }

        /// <summary>
        /// Sends the message pong.
        /// </summary>
        /// <param name="server">The server.</param>
        /// <param name="target">The target.</param>
        protected void SendMessagePong(string server, string target = null)
        {
            WriteMessage(null, "pong", server, target);
        }

        /// <summary>
        /// Sends the message away.
        /// </summary>
        /// <param name="text">The text.</param>
        protected void SendMessageAway(string text = null)
        {
            WriteMessage(null, "away", text);
        }

        /// <summary>
        /// Sends the message rehash.
        /// </summary>
        protected void SendMessageRehash()
        {
            WriteMessage(null, "rehash");
        }

        /// <summary>
        /// Sends the message die.
        /// </summary>
        protected void SendMessageDie()
        {
            WriteMessage(null, "die");
        }

        /// <summary>
        /// Sends the message restart.
        /// </summary>
        protected void SendMessageRestart()
        {
            WriteMessage(null, "restart");
        }

        /// <summary>
        /// Sends the message summon.
        /// </summary>
        /// <param name="user">The user.</param>
        /// <param name="target">The target.</param>
        /// <param name="channel">The channel.</param>
        protected void SendMessageSummon(string user, string target = null, string channel = null)
        {
            WriteMessage(null, "summon", user, target, channel);
        }

        /// <summary>
        /// Sends the message users.
        /// </summary>
        /// <param name="target">The target.</param>
        protected void SendMessageUsers(string target = null)
        {
            WriteMessage(null, "users", target);
        }

        /// <summary>
        /// Sends the message wallpos.
        /// </summary>
        /// <param name="text">The text.</param>
        protected void SendMessageWallpos(string text)
        {
            WriteMessage(null, "wallops", text);
        }

        /// <summary>
        /// Sends the message user host.
        /// </summary>
        /// <param name="nickNames">The nick names.</param>
        protected void SendMessageUserHost(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "userhost", nickNames);
        }

        /// <summary>
        /// Sends the message is on.
        /// </summary>
        /// <param name="nickNames">The nick names.</param>
        protected void SendMessageIsOn(IEnumerable<string> nickNames)
        {
            WriteMessage(null, "ison", nickNames);
        }
    }
}
