using System;
using System.Collections.Generic;
using System.Linq;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    // Defines all message senders for the client.
    partial class IrcClient
    {
        private void EnsureChannelName(string c)
        {
            // NOTE: we are missing the Control G (Asci 7) restriction
            if (!IsChannelName(c) || c.Length > 50 || c.IndexOfAny(new[] {' ', ',', ':'}) != -1)
            {
                var t = new ArgumentException(string.Format("collection contains an invalid Channelname ({0})!", c));

                t.Data["ChannelNameRequirements"] = "http://www.irchelp.org/irchelp/rfc/rfc2811.txt";
                throw t;
            }
        }

        /// <summary>
        ///     Sends the password for registering the connection.
        ///     This message must only be sent before the actual registration, which is done by
        ///     <see cref="SendMessageUser" /> (for normal users) or <see cref="SendMessageService" /> (for services).
        /// </summary>
        /// <param name="password">The connection password.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessagePassword(string password)
        {
            return WriteMessage(null, "pass", password);
        }

        /// <summary>
        ///     Sends the nick name of the local user to the server. This command may be used either for intitially setting
        ///     the nick name or changing it at any point.
        /// </summary>
        /// <param name="nickName">The nick name to set.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageNick(string nickName)
        {
            return WriteMessage(null, "nick", nickName);
        }

        /// <summary>
        ///     Requests a list of supported capabilities from the server
        /// </summary>
        protected void SendMessageCapList()
        {
            WriteMessage(null, "CAP", "LS");
        }

        /// <summary>
        ///     Requests a list of capabilities associated with the current connection from the server.
        /// </summary>
        protected void SendMessageCapListActive()
        {
            WriteMessage(null, "CAP", "LIST");
        }

        /// <summary>
        ///     Indicates the end of the Capability negotiation process to the server.
        /// </summary>
        protected void SendMessageCapEnd()
        {
            WriteMessage(null, "CAP", "END");
        }

        /// <summary>
        ///     Sends a request to activate a capability to the server
        /// </summary>
        /// <param name="caps"></param>
        protected void SendMessageCapRequest(string[] caps)
        {
            if (caps == null || caps.Length == 0)
                return;

            WriteMessage(null, "CAP", "REQ", string.Join(" ", caps));
        }
        
        /// <summary>
        ///     Sends a request to register the client as a user on the server.
        /// </summary>
        /// <param name="userName">The user name of the user.</param>
        /// <param name="userMode">The initial mode of the user.</param>
        /// <param name="realName">The real name of the user.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageUser(string userName, int userMode, string realName)
        {
            return WriteMessage(null, "user", userName, userMode.ToString(), "*", realName);
        }

        /// <summary>
        ///     Sends a request to register the client as a service on the server.
        /// </summary>
        /// <param name="nickName">The nick name of the service.</param>
        /// <param name="distribution">
        ///     A wildcard expression for matching against server names, which determines where
        ///     the service is visible.
        /// </param>
        /// <param name="description">A description of the service.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageService(string nickName, string distribution, string description = "")
        {
            return WriteMessage(null, "service", nickName, distribution, "0", "0", description);
        }

        /// <summary>
        ///     Sends a request for server operator privileges.
        /// </summary>
        /// <param name="userName">The user name with which to register.</param>
        /// <param name="password">The password with which to register.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageOper(string userName, string password)
        {
            return WriteMessage(null, "oper", userName, password);
        }

        /// <summary>
        ///     Sends an update or request for the current modes of the specified user.
        /// </summary>
        /// <param name="nickName">The nick name of the user whose modes to update/request.</param>
        /// <param name="modes">The mode string that indicates the user modes to change.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageUserMode(string nickName, string modes = null)
        {
            return WriteMessage(null, "mode", nickName, modes);
        }

        /// <summary>
        ///     Sends a notification to the server indicating that the client is quitting the network.
        /// </summary>
        /// <param name="comment">The comment to send the server, or <see langword="null" /> for none.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageQuit(string comment = null)
        {
            return WriteMessage(null, "quit", comment);
        }

        /// <summary>
        ///     Sends a request to disconnect the specified server from the network.
        ///     This command is only available to oeprators.
        /// </summary>
        /// <param name="targetServer">The name of the server to disconnected from the network.</param>
        /// <param name="comment">The comment to send the server.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageSquit(string targetServer, string comment)
        {
            return WriteMessage(null, "squit", targetServer, comment);
        }

        /// <summary>
        ///     Sends a request to leave all channels in which the user is currently present.
        /// </summary>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageLeaveAll()
        {
            return WriteMessage(null, "join", "0");
        }

        /// <inheritdoc cref="SendMessageJoin(IEnumerable{string})" />
        /// <param name="channels">A collection of 2-tuples of the names and keys of the channels to join.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageJoin(IEnumerable<Tuple<string, string>> channels)
        {
            var secureChannels = channels.Select(c =>
            {
                EnsureChannelName(c.Item1);
                return c;
            }).ToList();
            return WriteMessage(null, "join", string.Join(",", secureChannels.Select(c => c.Item1)),
                string.Join(",", secureChannels.Select(c => c.Item2)));
        }

        /// <summary>
        ///     Sends a request to join the specified channels.
        /// </summary>
        /// <param name="channels">A collection of the names of the channels to join.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageJoin(IEnumerable<string> channels)
        {
            var secureChannels = channels.Select(c =>
            {
                EnsureChannelName(c);
                return c;
            }).ToList();
            return WriteMessage(null, "join", string.Join(",", secureChannels));
        }

        /// <summary>
        ///     Sends a request to leave the specified channels.
        /// </summary>
        /// <param name="channels">A collection of the names of the channels to leave.</param>
        /// <param name="comment">The comment to send the server, or <see langword="null" /> for none.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessagePart(IEnumerable<string> channels, string comment = null)
        {
            return WriteMessage(null, "part", string.Join(",", channels), comment);
        }

        /// <summary>
        ///     Sends an update for the modes of the specified channel.
        /// </summary>
        /// <param name="channel">The channel whose modes to update.</param>
        /// <param name="modes">The mode string that indicates the channel modes to change.</param>
        /// <param name="modeParameters">A collection of parameters to the specified <paramref name="modes" />.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageChannelMode(string channel, string modes = null,
            IEnumerable<string> modeParameters = null)
        {
            string modeParametersList = null;
            if (modeParameters != null)
            {
                var modeParametersArray = modeParameters.ToArray();
                if (modeParametersArray.Length > 3)
                    throw new ArgumentException(Resources.MessageTooManyModeParameters);
                modeParametersList = string.Join(",", modeParametersArray);
            }
            return WriteMessage(null, "mode", channel, modes, modeParametersList);
        }

        /// <summary>
        ///     Sends an update or request for the topic of the specified channel.
        /// </summary>
        /// <param name="channel">The name of the channel whose topic to change.</param>
        /// <param name="topic">The new topic to set, or <see langword="null" /> to request the current topic.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageTopic(string channel, string topic = null)
        {
            return WriteMessage(null, "topic", channel, topic);
        }

        /// <summary>
        ///     Sends a request to list all names visible to the client.
        /// </summary>
        /// <param name="channels">
        ///     A collection of the names of channels for which to list users, or
        ///     <see langword="null" /> for all channels.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageNames(IEnumerable<string> channels = null, string targetServer = null)
        {
            return WriteMessage(null, "names", channels == null ? null : string.Join(",", channels), targetServer);
        }

        /// <summary>
        ///     Sends a request to list channels and their topics.
        /// </summary>
        /// <param name="channels">
        ///     A collection of the names of channels to list, or <see langword="null" /> for all
        ///     channels.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageList(IEnumerable<string> channels = null, string targetServer = null)
        {
            return WriteMessage(null, "list", channels == null ? null : string.Join(",", channels), targetServer);
        }

        /// <summary>
        ///     Sends a request to invite the specified user to the specified channel.
        /// </summary>
        /// <param name="channel">The name of the channel to which to invite the user.</param>
        /// <param name="nickName">The nick name of the user to invite.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageInvite(string channel, string nickName)
        {
            return WriteMessage(null, "invite", nickName, channel);
        }

        /// <inheritdoc cref="SendMessageKick(IEnumerable{Tuple{string, string}}, string)" />
        /// <param name="channel">The name of the channel from which to kick the users.</param>
        /// <param name="nickNames">A collection of the nick names of the users to kick from the channel.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageKick(string channel, IEnumerable<string> nickNames, string comment = null)
        {
            return WriteMessage(null, "kick", channel, string.Join(",", nickNames), comment);
        }

        /// <summary>
        ///     Sends a request to kick the specifier users from the specified channel.
        /// </summary>
        /// <param name="channelsUsers">
        ///     A collection of 2-tuples of channel names and the nick names of the users to
        ///     kick from the channel.
        /// </param>
        /// <param name="comment">The comment to send the server, or <see langword="null" /> for none.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageKick(IEnumerable<Tuple<string, string>> channelsUsers, string comment = null)
        {
            return WriteMessage(null, "kick", string.Join(",", channelsUsers.Select(user => user.Item1)),
                string.Join(",", channelsUsers.Select(user => user.Item2)), comment);
        }

        /// <summary>
        ///     Sends a private message to the specified targets.
        /// </summary>
        /// <param name="targets">A collection of the targets to which to send the message.</param>
        /// <param name="text">The text of the message to send.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessagePrivateMessage(IEnumerable<string> targets, string text)
        {
            var targetsArray = targets.ToArray();
            foreach (var target in targetsArray)
            {
                if (target.Contains(","))
                    throw new ArgumentException(Resources.MessageInvalidTargetName, "arguments");
            }
            return WriteMessage(null, "privmsg", string.Join(",", targetsArray), text);
        }

        /// <summary>
        ///     Sends a notice to the specified targets.
        /// </summary>
        /// <param name="targets">A collection of the targets to which to send the message.</param>
        /// <param name="text">The text of the message to send.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageNotice(IEnumerable<string> targets, string text)
        {
            var targetsArray = targets.ToArray();
            foreach (var target in targetsArray)
            {
                if (target.Contains(","))
                    throw new ArgumentException(Resources.MessageInvalidTargetName, "arguments");
            }
            return WriteMessage(null, "notice", string.Join(",", targetsArray), text);
        }

        /// <summary>
        ///     Sends a request to receive the Message of the Day (MOTD) from the server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" /> for
        ///     the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageMotd(string targetServer = null)
        {
            return WriteMessage(null, "motd", targetServer);
        }

        /// <summary>
        ///     Sends a request to get statistics about the size of the IRC network.
        /// </summary>
        /// <param name="serverMask">
        ///     A wildcard expression for matching against the names of servers, or
        ///     <see langword="null" /> to match the entire network.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageLUsers(string serverMask = null, string targetServer = null)
        {
            return WriteMessage(null, "lusers", serverMask, targetServer);
        }

        /// <summary>
        ///     Sends a request for the version of the server program.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageVersion(string targetServer = null)
        {
            return WriteMessage(null, "version", targetServer);
        }

        /// <summary>
        ///     Sends a request to query statistics for the server.
        /// </summary>
        /// <param name="query">The query to send the server.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageStats(string query = null, string targetServer = null)
        {
            return WriteMessage(null, "stats", query, targetServer);
        }

        /// <summary>
        ///     Sends a request to list all other servers linked to the server.
        /// </summary>
        /// <param name="serverMask">A wildcard expression for matching the names of servers to list.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageLinks(string serverMask = null, string targetServer = null)
        {
            return WriteMessage(null, "links", targetServer, serverMask);
        }

        /// <summary>
        ///     Sends a request to query the local time on the server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageTime(string targetServer = null)
        {
            return WriteMessage(null, "time", targetServer);
        }

        /// <summary>
        ///     Sends a request for the server to try to connect to another server.
        /// </summary>
        /// <param name="hostName">The host name of the other server to which the server should connect.</param>
        /// <param name="port">The port on the other server to which the server should connect.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageConnect(string hostName, int port, string targetServer = null)
        {
            return WriteMessage(null, "connect", hostName, port.ToString(), targetServer);
        }

        /// <summary>
        ///     Sends a query to trace the route to the server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageTrace(string targetServer = null)
        {
            return WriteMessage(null, "trace", targetServer);
        }

        /// <summary>
        ///     Sends a request for information about the administrator of the server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageAdmin(string targetServer = null)
        {
            return WriteMessage(null, "admin", targetServer);
        }

        /// <summary>
        ///     Sends a request for general information about the server program.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageInfo(string targetServer = null)
        {
            return WriteMessage(null, "info", targetServer);
        }

        /// <summary>
        ///     Sends a request to list services currently connected to the netwrok/
        /// </summary>
        /// <param name="mask">A wildcard expression for matching against the names of services.</param>
        /// <param name="type">The type of services to list.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageServlist(string mask = null, string type = null)
        {
            return WriteMessage(null, "servlist", mask, type);
        }

        /// <summary>
        ///     Sends a query message to a service.
        /// </summary>
        /// <param name="serviceName">The name of the service.</param>
        /// <param name="text">The text of the message to send.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageSquery(string serviceName, string text)
        {
            return WriteMessage(null, "squery", serviceName, text);
        }

        /// <summary>
        ///     Sends a request to perform a Who query on users.
        /// </summary>
        /// <param name="mask">
        ///     A wildcard expression for matching against channel names; or if none can be found,
        ///     host names, server names, real names, and nick names of users. If the value is <see langword="null" />,
        ///     all users are matched.
        /// </param>
        /// <param name="onlyOperators">
        ///     <see langword="true" /> to match only server operators;
        ///     <see langword="false" /> to match all users.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageWho(string mask = null, bool onlyOperators = false)
        {
            return WriteMessage(null, "who", mask, onlyOperators ? "o" : null);
        }

        /// <summary>
        ///     Sends a WhoX query to the server targeting the specified channel or user masks.
        /// </summary>
        /// <param name="mask">
        ///     A wildcard expression for matching against channel names; or if none can be found,
        ///     host names, server names, real names, and nick names of users. If the value is <see langword="null" />,
        ///     all users are matched.
        /// </param>
        /// <param name="flags">
        ///     Flags for requesting specific data, see http://faerion.sourceforge.net/doc/irc/whox.var.
        /// </param>
        /// <param name="mask2">
        ///     A second mask field, which can contain spaces and overrides the first mask field.
        /// </param>
        protected void SendMessageWhoX(string mask = null, string flags = null, string mask2 = null)
        {
            WriteMessage(null, "who", mask, flags, mask2);
        }

        /// <summary>
        ///     Sends a request to perform a WhoIs query on users.
        /// </summary>
        /// <param name="nickNameMasks">
        ///     A collection of wildcard expressions for matching against the nick names of
        ///     users.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageWhoIs(IEnumerable<string> nickNameMasks, string targetServer = null)
        {
            return WriteMessage(null, "whois", targetServer, string.Join(",", nickNameMasks));
        }

        /// <summary>
        ///     Sends a request to perform a WhoWas query on users.
        /// </summary>
        /// <param name="nickNames">
        ///     A collection of wildcard expressions for matching against the nick names of
        ///     users.
        /// </param>
        /// <param name="entriesCount">The maximum number of (most recent) entries to return.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageWhoWas(IEnumerable<string> nickNames, int entriesCount = -1,
            string targetServer = null)
        {
            return WriteMessage(null, "whowas", string.Join(",", nickNames), entriesCount.ToString(), targetServer);
        }

        /// <summary>
        ///     Sends a request to disconnect the specified user from the server.
        /// </summary>
        /// <param name="nickName">The nick name of the user to disconnect.</param>
        /// <param name="comment">The comment to send the server.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageKill(string nickName, string comment)
        {
            return WriteMessage(null, "kill", nickName, comment);
        }

        /// <summary>
        ///     Sends a ping request to the server.
        /// </summary>
        /// <param name="server">The name of the server to which to send the request.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessagePing(string server, string targetServer = null)
        {
            return WriteMessage(null, "ping", server, targetServer);
        }

        /// <summary>
        ///     Sends a pong response (to a ping) to the server.
        /// </summary>
        /// <param name="server">The name of the server to which to send the response.</param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessagePong(string server, string targetServer = null)
        {
            return WriteMessage(null, "pong", server, targetServer);
        }

        /// <summary>
        ///     Sends an update to the server indicating that the local user is away.
        /// </summary>
        /// <param name="text">
        ///     The text of the away message. The away message is sent to any user that tries to contact
        ///     the local user while it is away.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageAway(string text = null)
        {
            return WriteMessage(null, "away", text);
        }

        /// <summary>
        ///     Sends a request to the server telling it to reprocess its configuration settings.
        /// </summary>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageRehash()
        {
            return WriteMessage(null, "rehash");
        }

        /// <summary>
        ///     Sends a request to the server telling it to shut down.
        /// </summary>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageDie()
        {
            return WriteMessage(null, "die");
        }

        /// <summary>
        ///     Sends a message to the server telling it to restart.
        /// </summary>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageRestart()
        {
            return WriteMessage(null, "restart");
        }

        /// <summary>
        ///     Sends a request to return a list of information about all users currently registered on the server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageUsers(string targetServer = null)
        {
            return WriteMessage(null, "users", targetServer);
        }

        /// <summary>
        ///     Sends a message to all connected users that have the 'w' mode set.
        /// </summary>
        /// <param name="text">The text of the message to send.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageWallops(string text)
        {
            return WriteMessage(null, "wallops", text);
        }

        /// <summary>
        ///     Sends a request to return the host names of the specified users.
        /// </summary>
        /// <param name="nickNames">A collection of the nick names of the users to query.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageUserHost(IEnumerable<string> nickNames)
        {
            return WriteMessage(null, "userhost", nickNames);
        }

        /// <summary>
        ///     Sends a request to check whether the specified users are currently online.
        /// </summary>
        /// <param name="nickNames">A collection of the nick names of the users to query.</param>
        /// <returns>The produced <see cref="IrcMessage"/></returns>
        protected IrcMessage SendMessageIsOn(IEnumerable<string> nickNames)
        {
            return WriteMessage(null, "ison", nickNames);
        }
    }
}