using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using IrcDotNet.Collections;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    partial class IrcClient
    {
        /// <summary>
        ///     Gets the target of a message from the specified name.
        ///     A message target may be an <see cref="IrcUser" />, <see cref="IrcChannel" />, or <see cref="IrcTargetMask" />.
        /// </summary>
        /// <param name="targetName">The name of the target.</param>
        /// <returns>The target object that corresponds to the given name.</returns>
        /// <exception cref="ArgumentException">
        ///     <paramref name="targetName" /> does not represent a valid message target.
        /// </exception>
        protected IIrcMessageTarget GetMessageTarget(string targetName)
        {
            if (targetName == null)
                throw new ArgumentNullException(nameof(targetName));
            if (targetName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(targetName));

            // Check whether target name represents channel, user, or target mask.
            Match targetNameMatch = Regex.Match(targetName, RegexMessageTarget);
            string channelName = targetNameMatch.Groups["channel"].GetValue();
            string nickName = targetNameMatch.Groups["nick"].GetValue();
            string userName = targetNameMatch.Groups["user"].GetValue();
            string hostName = targetNameMatch.Groups["host"].GetValue();
            string serverName = targetNameMatch.Groups["server"].GetValue();
            string targetMask = targetNameMatch.Groups["targetMask"].GetValue();
            if (channelName != null)
            {
                return GetChannelFromName(channelName);
            }
            if (nickName != null)
            {
                // Find user by nick name. If no user exists in list, create it and set its properties.
                IrcUser user = GetUserFromNickName(nickName);
                if (user.UserName == null)
                    user.UserName = userName;
                if (user.HostName == null)
                    user.HostName = hostName;

                return user;
            }
            if (userName != null)
            {
                // Find user by user  name. If no user exists in list, create it and set its properties.
                IrcUser user = GetUserFromUserName(userName);
                if (user.HostName == null)
                    user.HostName = hostName;

                return user;
            }
            if (targetMask != null)
            {
                return new IrcTargetMask(targetMask);
            }
            throw new ArgumentException(string.Format(
                Resources.MessageInvalidSource, targetName), nameof(targetName));
        }



        /// <summary>
        ///     Handles the specified statistical entry for the server, received in response to a STATS message.
        /// </summary>
        /// <param name="type">The type of the statistical entry for the server.</param>
        /// <param name="message">The message that contains the statistical entry.</param>
        protected void HandleStatsEntryReceived(int type, IrcMessage message)
        {
            // Add statistical entry to temporary list.
            listedStatsEntries.Add(new IrcServerStatisticalEntry
            {
                Type = type,
                Parameters = message.Parameters.Skip(1).ToArray()
            });
        }

        /// <summary>
        ///     Handles the specified parameter value of an ISUPPORT message, received from the server upon registration.
        /// </summary>
        /// <param name="paramName">The name of the parameter.</param>
        /// <param name="paramValue">
        ///     The value of the parameter, or <see langword="null" /> if it does not have a value.
        /// </param>
        protected bool HandleISupportParameter(string paramName, string paramValue)
        {
            if (paramName == null)
                throw new ArgumentNullException(nameof(paramName));
            if (paramName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(paramName));

            // Check name of parameter.
            switch (paramName.ToLowerInvariant())
            {
                case "prefix":
                    Match prefixValueMatch = Regex.Match(paramValue, IsupportPrefix);
                    string prefixes = prefixValueMatch.Groups["prefixes"].GetValue();
                    string modes = prefixValueMatch.Groups["modes"].GetValue();

                    if (prefixes.Length != modes.Length)
                        throw new ProtocolViolationException(Resources.MessageISupportPrefixInvalid);

                    lock (((ICollection)ChannelUserModes).SyncRoot)
                    {
                        channelUserModes.Clear();
                        channelUserModes.AddRange(modes);
                    }

                    channelUserModesPrefixes.Clear();

                    for (int i = 0; i < prefixes.Length; i++)
                        channelUserModesPrefixes.Add(prefixes[i], modes[i]);

                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        ///     Extracts the the mode and nick name of a user from the specified value.
        /// </summary>
        /// <param name="input">The input value, containing a nick name optionally prefixed by a mode character.</param>
        /// <returns>A 2-tuple of the nick name and user mode.</returns>
        protected Tuple<string, string> GetUserModeAndNickName(string input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));
            if (input.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(input));

            if (channelUserModesPrefixes.TryGetValue(input[0], out char mode))
                return Tuple.Create(input.Substring(1), mode.ToString());
            return Tuple.Create(input, string.Empty);
        }

        /// <summary>
        ///     Gets a collection of mode characters and mode parameters from the specified mode parameters.
        ///     Combines multiple mode strings into a single mode string.
        /// </summary>
        /// <param name="messageParameters">
        ///     A collection of message parameters, which consists of mode strings and mode
        ///     parameters. A mode string is of the form `( "+" / "-" ) *( mode character )`, and specifies mode changes.
        ///     A mode parameter is arbitrary text associated with a certain mode.
        /// </param>
        /// <returns>
        ///     A 2-tuple of a single mode string and a collection of mode parameters.
        ///     Each mode parameter corresponds to a single mode character, in the same order.
        /// </returns>
        protected Tuple<string, IEnumerable<string>> GetModeAndParameters(IEnumerable<string> messageParameters)
        {
            if (messageParameters == null)
                throw new ArgumentNullException(nameof(messageParameters));

            StringBuilder modes = new StringBuilder();
            List<string> modeParameters = new List<string>();
            foreach (string p in messageParameters)
            {
                if (p == null)
                    break;
                if (p.Length == 0)
                    continue;
                if (p[0] == '+' || p[0] == '-')
                    modes.Append(p);
                else
                    modeParameters.Add(p);
            }
            return Tuple.Create(modes.ToString(), (IEnumerable<string>)modeParameters.AsReadOnly());
        }

        /// <summary>
        ///     Gets a list of channel objects from the specified comma-separated list of channel names.
        /// </summary>
        /// <param name="namesList">A value that contains a comma-separated list of names of channels.</param>
        /// <returns>A list of channel objects that corresponds to the given list of channel names.</returns>
        protected IEnumerable<IrcChannel> GetChannelsFromList(string namesList)
        {
            if (namesList == null)
                throw new ArgumentNullException(nameof(namesList));

            return namesList.Split(',').Select(GetChannelFromName);
        }

        /// <summary>
        ///     Gets a list of user objects from the specified comma-separated list of nick names.
        /// </summary>
        /// <param name="nickNamesList">A value that contains a comma-separated list of nick names of users.</param>
        /// <returns>A list of user objects that corresponds to the given list of nick names.</returns>
        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
            if (nickNamesList == null)
                throw new ArgumentNullException(nameof(nickNamesList));

            lock (((ICollection)Users).SyncRoot)
                return nickNamesList.Split(',').Select(n => users.Single(u => u.NickName == n));
        }

        /// <summary>
        ///     Determines whether the specified name refers to a channel.
        /// </summary>
        /// <param name="name">The name to check.</param>
        /// <returns>
        ///     <see langword="true" /> if the specified name represents a channel; <see langword="false" />,
        ///     otherwise.
        /// </returns>
        protected bool IsChannelName(string name)
        {
            if (name == null)
                throw new ArgumentNullException(nameof(name));

            return Regex.IsMatch(name, RegexChannelName);
        }

        /// <summary>
        ///     Gets the type of the channel from the specified character.
        /// </summary>
        /// <param name="type">
        ///     A character that represents the type of the channel.
        ///     The character may be one of the following:
        ///     <list type="bullet">
        ///         <listheader>
        ///             <term>Character</term>
        ///             <description>Channel type</description>
        ///         </listheader>
        ///         <item>
        ///             <term>=</term>
        ///             <description>Public channel</description>
        ///         </item>
        ///         <item>
        ///             <term>*</term>
        ///             <description>Private channel</description>
        ///         </item>
        ///         <item>
        ///             <term>@</term>
        ///             <description>Secret channel</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <returns>The channel type that corresponds to the specified character.</returns>
        /// <exception cref="ArgumentException">
        ///     <paramref name="type" /> does not correspond to any known channel type.
        /// </exception>
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
                    throw new ArgumentException(string.Format(
                        Resources.MessageInvalidChannelType, type), nameof(type));
            }
        }
        
        protected void CheckRegistrationInfo(IrcRegistrationInfo registrationInfo)
        {
            // Check that given registration info is valid.
            if (registrationInfo is IrcUserRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcUserRegistrationInfo)registrationInfo).UserName == null)
                    throw new ArgumentException(Resources.MessageInvalidUserRegistrationInfo, nameof(registrationInfo));
            }
            else if (registrationInfo is IrcServiceRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcServiceRegistrationInfo)registrationInfo).Description == null)
                    throw new ArgumentException(Resources.MessageInvalidServiceRegistrationInfo, nameof(registrationInfo));
            }
            else
            {
                throw new ArgumentException(Resources.MessageInvalidRegistrationInfoObject, nameof(registrationInfo));
            }
        }

        private static bool IsInvalidMessageChar(char value) => value == '\0' || value == '\r' || value == '\n';


        private string CheckPrefix(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(Resources.MessageInvalidPrefix, value), nameof(value));
            }

            return value;
        }

        private string CheckCommand(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(Resources.MessageInvalidCommand, value), nameof(value));
            }

            return value;
        }

        private string CheckMiddleParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(c => IsInvalidMessageChar(c) || c == ' ') || value[0] == ':')
            {
                throw new ArgumentException(string.Format(Resources.MessageInvalidMiddleParameter, value), nameof(value));
            }

            return value;
        }

        private string CheckTrailingParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(Resources.MessageInvalidMiddleParameter, value), nameof(value));
            }

            return value;
        }


        /// <summary>
        ///     Gets the source of a message from the specified prefix.
        ///     A message source may be a <see cref="IrcUser" /> or <see cref="IrcServer" />.
        /// </summary>
        /// <param name="prefix">The raw prefix of the message.</param>
        /// <returns>
        ///     The message source that corresponds to the specified prefix. The object is an instance of
        ///     <see cref="IrcUser" /> or <see cref="IrcServer" />.
        /// </returns>
        /// <exception cref="ArgumentException">
        ///     <paramref name="prefix" /> does not represent a valid message source.
        /// </exception>
        protected IIrcMessageSource GetSourceFromPrefix(string prefix)
        {
            if (prefix == null)
                return null;
            if (prefix.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(prefix));

            // Check whether prefix represents server or user.
            Match prefixMatch = Regex.Match(prefix, RegexMessagePrefix);
            string serverName = prefixMatch.Groups["server"].GetValue();
            string nickName = prefixMatch.Groups["nick"].GetValue();
            string userName = prefixMatch.Groups["user"].GetValue();
            string hostName = prefixMatch.Groups["host"].GetValue();
            if (serverName != null)
            {
                return GetServerFromHostName(serverName);
            }

            if (nickName == null)
                throw new ArgumentException(string.Format(
                    Resources.MessageInvalidSource, prefix), nameof(prefix));

            // Find user by nick name. If no user exists in list, create it and set its properties.
            IrcUser user = GetUserFromNickName(nickName);
            if (user.UserName == null)
                user.UserName = userName;
            if (user.HostName == null)
                user.HostName = hostName;

            return user;
        }

        /// <inheritdoc cref="GetServerFromHostName(string, out bool)" />
        protected IrcServer GetServerFromHostName(string hostName)
        {
            return GetServerFromHostName(hostName, out _);
        }

        /// <summary>
        ///     Gets the server with the specified host name, creating it if necessary.
        /// </summary>
        /// <param name="hostName">The host name of the server.</param>
        /// <param name="createdNew">
        ///     <see langword="true" /> if the server object was created during the call;
        ///     <see langword="false" />, otherwise.
        /// </param>
        /// <returns>The server object that corresponds to the specified host name.</returns>
        protected IrcServer GetServerFromHostName(string hostName, out bool createdNew)
        {
            if (hostName == null)
                throw new ArgumentNullException(nameof(hostName));
            if (hostName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(hostName));

            // Search for server with given name in list of known servers. If it does not exist, add it.
            IrcServer server = servers.SingleOrDefault(s => s.HostName == hostName);
            if (server == null)
            {
                server = new IrcServer(hostName);
                servers.Add(server);

                createdNew = true;
            }
            else
            {
                createdNew = false;
            }
            return server;
        }

        /// <inheritdoc cref="GetChannelFromName(string, out bool)" />
        protected IrcChannel GetChannelFromName(string channelName)
        {
            return GetChannelFromName(channelName, out _);
        }

        /// <summary>
        ///     Gets the channel with the specified name, creating it if necessary.
        /// </summary>
        /// <param name="channelName">The name of the channel.</param>
        /// <param name="createdNew">
        ///     <see langword="true" /> if the channel object was created during the call;
        ///     <see langword="false" />, otherwise.
        /// </param>
        /// <returns>The channel object that corresponds to the specified name.</returns>
        protected IrcChannel GetChannelFromName(string channelName, out bool createdNew)
        {
            if (channelName == null)
                throw new ArgumentNullException(nameof(channelName));
            if (channelName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(channelName));

            // Search for channel with given name in list of known channel. If it does not exist, add it.
            lock (((ICollection)Channels).SyncRoot)
            {
                var channel = channels.SingleOrDefault(c => c.Name == channelName);
                if (channel == null)
                {
                    channel = new IrcChannel(channelName) { Client = this };
                    channels.Add(channel);
                    createdNew = true;
                }
                else
                {
                    createdNew = false;
                }

                return channel;
            }
        }

        /// <inheritdoc cref="GetUserFromNickName(string, bool, out bool)" />
        protected IrcUser GetUserFromNickName(string nickName, bool isOnline = true)
        {
            return GetUserFromNickName(nickName, isOnline, out _);
        }

        /// <summary>
        ///     Gets the user with the specified nick name, creating it if necessary.
        /// </summary>
        /// <param name="nickName">The nick name of the user.</param>
        /// <param name="isOnline">
        ///     <see langword="true" /> if the user is currently online;
        ///     <see langword="false" />, if the user is currently offline.
        ///     The <see cref="IrcUser.IsOnline" /> property of the user object is set to this value.
        /// </param>
        /// <param name="createdNew">
        ///     <see langword="true" /> if the user object was created during the call;
        ///     <see langword="false" />, otherwise.
        /// </param>
        /// <returns>The user object that corresponds to the specified nick name.</returns>
        protected IrcUser GetUserFromNickName(string nickName, bool isOnline, out bool createdNew)
        {
            if (nickName == null)
                throw new ArgumentNullException(nameof(nickName));
            if (nickName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(nickName));

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            IrcUser user;
            lock (((ICollection)Users).SyncRoot)
            {
                user = users.SingleOrDefault(u => u.NickName == nickName);
                if (user == null)
                {
                    user = new IrcUser
                    {
                        Client = this,
                        NickName = nickName
                    };
                    users.Add(user);
                    createdNew = true;
                }
                else
                {
                    createdNew = false;
                }
            }
            user.IsOnline = isOnline;
            return user;
        }

        /// <inheritdoc cref="GetUserFromUserName(string, out bool)" />
        protected IrcUser GetUserFromUserName(string userName)
        {
            return GetUserFromUserName(userName, out _);
        }

        /// <summary>
        ///     Gets the user with the specified user name, creating it if necessary.
        /// </summary>
        /// <param name="userName">The user name of the user.</param>
        /// <param name="createdNew">
        ///     <see langword="true" /> if the user object was created during the call;
        ///     <see langword="false" />, otherwise.
        /// </param>
        /// <returns>The user object that corresponds to the specified user name.</returns>
        protected IrcUser GetUserFromUserName(string userName, out bool createdNew)
        {
            if (userName == null)
                throw new ArgumentNullException(nameof(userName));
            if (userName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, nameof(userName));

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            IrcUser user;
            lock (((ICollection)Users).SyncRoot)
            {
                user = users.SingleOrDefault(u => u.UserName == userName);
                if (user == null)
                {
                    user = new IrcUser
                    {
                        Client = this,
                        UserName = userName
                    };
                    users.Add(user);

                    createdNew = true;
                }
                else
                {
                    createdNew = false;
                }
            }
            return user;
        }

        protected static int GetNumericUserMode(ICollection<char> modes)
        {
            int value = 0;
            if (modes == null)
                return value;
            if (modes.Contains('w'))
                value |= 0x02;
            if (modes.Contains('i'))
                value |= 0x04;
            return value;
        }

        protected virtual void ResetState()
        {
            // Reset fully state of client.
            servers = new Collection<IrcServer>();
            isRegistered = false;
            localUser = null;
            serverSupportedFeatures = new Dictionary<string, string>();
            ServerSupportedFeatures = new Collections.ReadOnlyDictionary<string, string>(serverSupportedFeatures);
            channelUserModes = new Collection<char>
            {
                'o',
                'v'
            };
            ChannelUserModes = new ReadOnlyCollection<char>(channelUserModes);
            channelUserModesPrefixes = new Dictionary<char, char>
            {
                {'@', 'o'},
                {'+', 'v'}
            };
            motdBuilder = new StringBuilder();
            networkInformation = new IrcNetworkInfo();
            channels = new Collection<IrcChannel>();
            Channels = new IrcChannelCollection(this, channels);
            users = new Collection<IrcUser>();
            Users = new IrcUserCollection(this, users);
            listedChannels = new List<IrcChannelInfo>();
            listedServerLinks = new List<IrcServerInfo>();
            listedStatsEntries = new List<IrcServerStatisticalEntry>();
        }

        protected void InitializeMessageProcessors()
        {
            // Find each method defined as processor for IRC message.
            var methods = this.GetAttributedMethods<MessageProcessorAttribute, MessageProcessor>();
            foreach (var method in methods)
            {
                MessageProcessorAttribute attribute = method.Item1;
                MessageProcessor methodDelegate = method.Item2;

                string[] commandRangeParts = attribute.CommandName.Split('-');
                switch (commandRangeParts.Length)
                {
                    case 2:
                        // Numeric command range was defined.
                        if (!int.TryParse(commandRangeParts[0], out int commandRangeStart))
                        {
                            throw new ProtocolViolationException(string.Format(
                                Resources.MessageInvalidCommandDefinition, attribute.CommandName));
                        }

                        if (!int.TryParse(commandRangeParts[1], out int commandRangeEnd))
                        {
                            throw new ProtocolViolationException(string.Format(
                                Resources.MessageInvalidCommandDefinition, attribute.CommandName));
                        }

                        for (int code = commandRangeStart; code <= commandRangeEnd; code++)
                        {
                            numericMessageProcessors.Add(code, methodDelegate);
                        }
                        break;
                    case 1:
                        // Single command name was defined. Check whether it is numeric or alphabetic.
                        if (int.TryParse(attribute.CommandName, out int commandCode))
                        {
                            // Command is numeric.
                            numericMessageProcessors.Add(commandCode, methodDelegate);
                        }
                        else
                        {
                            // Command is alphabetic.
                            messageProcessors.Add(attribute.CommandName, methodDelegate);
                        }
                        break;
                    default:
                        throw new ProtocolViolationException(string.Format(
                            Resources.MessageInvalidCommandDefinition, attribute.CommandName));
                }
            }
        }
    }
}