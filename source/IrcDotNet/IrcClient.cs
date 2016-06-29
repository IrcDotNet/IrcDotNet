﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using IrcDotNet.Collections;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents a client that communicates with a server using the IRC (Internet Relay Chat) protocol.
    ///     Do not inherit this class unless the protocol itself is being extended.
    /// </summary>
    /// <remarks>
    ///     All collection objects must be locked on the <see cref="ICollection.SyncRoot" /> object for thread-safety.
    ///     They can however be used safely without locking within event handlers.
    /// </remarks>
    /// <threadsafety static="true" instance="true" />
    [DebuggerDisplay("{ToString(), nq}")]
    public abstract partial class IrcClient : IDisposable
    {
        // Maximum number of parameters that can be sent in single raw message.        
        private const int maxParamsCount = 15;

        // Default port on which to connect to IRC server.
        public static readonly int DefaultPort = 6667;

        // Regular expressions used for extracting information from protocol messages.
        protected static readonly string regexNickName;
        protected static readonly string regexUserName;
        protected static readonly string regexHostName;
        protected static readonly string regexChannelName;
        protected static readonly string regexTargetMask;
        protected static readonly string regexServerName;
        protected static readonly string regexNickNameId;
        protected static readonly string regexUserNameId;
        protected static readonly string regexMessagePrefix;
        protected static readonly string regexMessageTarget;

        protected static readonly string isupportPrefix;

        // Non-zero if object has been disposed or is currently being disposed.
        private int disposedFlag;

        // Prevents client from flooding server with messages by limiting send rate.

        // Dictionary of message processor routines, keyed by their command names.
        private readonly Dictionary<string, MessageProcessor> messageProcessors;

        // Dictionary of message processor routines, keyed by their numeric codes (000 to 999).
        private readonly Dictionary<int, MessageProcessor> numericMessageProcessors;

        static IrcClient()
        {
            regexNickName = @"(?<nick>[^!@]+)";
            regexUserName = @"(?<user>[^!@]+)";
            regexHostName = @"(?<host>[^%@]+)";
            regexChannelName = @"(?<channel>[#+!&].+)";
            regexTargetMask = @"(?<targetMask>[$#].+)";
            regexServerName = @"(?<server>[^%@]+?\.[^%@]*)";
            regexNickNameId = string.Format(@"{0}(?:(?:!{1})?@{2})?", regexNickName, regexUserName, regexHostName);
            regexUserNameId = string.Format(@"{0}(?:(?:%{1})?@{2}|%{1})", regexUserName, regexHostName,
                regexServerName);
            regexMessagePrefix = string.Format(@"^(?:{0}|{1})$", regexServerName, regexNickNameId);
            regexMessageTarget = string.Format(@"^(?:{0}|{1}|{2}|{3})$", regexChannelName, regexUserNameId,
                regexTargetMask, regexNickNameId);

            isupportPrefix = @"\((?<modes>.*)\)(?<prefixes>.*)";
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcClient" /> class.
        /// </summary>
        public IrcClient()
        {
            TextEncoding = Encoding.UTF8;
            messageProcessors = new Dictionary<string, MessageProcessor>(
                StringComparer.OrdinalIgnoreCase);
            numericMessageProcessors = new Dictionary<int, MessageProcessor>(1000);
            FloodPreventer = null;

            InitializeMessageProcessors();
        }

#if DEBUG
        public string ClientId { get; set; }
#endif

        /// <summary>
        ///     Gets whether the client connection has been registered with the server.
        /// </summary>
        /// <value>
        ///     <see langword="true" /> if the connection has been registered; <see langword="false" />, otherwise.
        /// </value>
        public bool IsRegistered
        {
            get { return isRegistered; }
        }

        /// <summary>
        ///     Gets the local user. The local user is the user managed by this client connection.
        /// </summary>
        /// <value>The local user.</value>
        public IrcLocalUser LocalUser
        {
            get { return localUser; }
        }

        /// <summary>
        ///     Gets the 'Welcome' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Welcome' message received from the server..</value>
        public string WelcomeMessage { get; protected set; }

        /// <summary>
        ///     Gets the 'Your Host' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Your Host' message received from the server.</value>
        public string YourHostMessage { get; private set; }

        /// <summary>
        ///     Gets the 'Created' message sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The 'Created' message received from the server.</value>
        public string ServerCreatedMessage { get; private set; }

        /// <summary>
        ///     Gets the host name of the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The host name given received from the server.</value>
        public string ServerName { get; private set; }

        /// <summary>
        ///     Gets the version of the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The version given received from the server.</value>
        public string ServerVersion { get; private set; }

        /// <summary>
        ///     Gets a collection of the user modes available on the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of user modes available on the server.</value>
        public IEnumerable<char> ServerAvailableUserModes { get; private set; }

        /// <summary>
        ///     Gets a collection of the channel modes available on the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A list of channel modes available on the server.</value>
        public IEnumerable<char> ServerAvailableChannelModes { get; private set; }

        /// <summary>
        ///     Gets a dictionary of the features supported by the server, keyed by feature name, as returned by the
        ///     ISUPPORT message.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>A dictionary of features supported by the server.</value>
        public Collections.ReadOnlyDictionary<string, string> ServerSupportedFeatures { get; private set; }

        /// <summary>
        ///     Gets a collection of channel modes that apply to users in a channel.
        /// </summary>
        /// <value>A collection of channel modes that apply to users.</value>
        public ReadOnlyCollection<char> ChannelUserModes { get; private set; }

        /// <summary>
        ///     Gets the Message of the Day (MOTD) sent by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public string MessageOfTheDay
        {
            get { return motdBuilder.ToString(); }
        }

        /// <summary>
        ///     Gets information about the IRC network that is given by the server.
        ///     This value is set after successful registration of the connection.
        /// </summary>
        /// <value>The Message of the Day sent by the server.</value>
        public IrcNetworkInfo? NetworkInformation
        {
            get { return networkInformation; }
        }

        /// <summary>
        ///     Gets a collection of all channels known to the client.
        /// </summary>
        /// <value>A collection of known channels.</value>
        public IrcChannelCollection Channels { get; private set; }

        /// <summary>
        ///     Gets a collection of all users known to the client, including the local user.
        /// </summary>
        /// <value>A collection of known users.</value>
        public IrcUserCollection Users { get; private set; }

        /// <summary>
        ///     Gets or sets an object that limits the rate of outgoing messages in order to prevent flooding the server.
        ///     The value is <see langword="null" /> by default, which indicates that no flood prevention should be
        ///     performed.
        /// </summary>
        /// <value>A flood preventer object.</value>
        public IIrcFloodPreventer FloodPreventer { get; set; }

        /// <summary>
        ///     Gets or sets the text encoding to use for reading from and writing to the network data stream.
        /// </summary>
        /// <value>The text encoding of the network stream.</value>
        public Encoding TextEncoding { get; set; }

        /// <summary>
        ///     Gets whether the client is currently connected to a server.
        /// </summary>
        /// <value><see langword="true" /> if the client is connected; <see langword="false" />, otherwise.</value>
        public abstract bool IsConnected { get; }

        /// <summary>
        ///     Gets whether the <see cref="IrcClient" /> object has been disposed.
        /// </summary>
        /// <value>
        ///     <see langword="true" /> if the <see cref="IrcClient" /> object has been disposed;
        ///     <see langword="false" />, otherwise.
        /// </value>
        protected bool IsDisposed
        {
            get { return Interlocked.CompareExchange(ref disposedFlag, 0, 0) > 0; }
        }

        /// <summary>
        ///     Releases all resources used by the <see cref="IrcClient" /> object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        ///     Finalizes an instance of the <see cref="IrcClient" /> class.
        /// </summary>
        ~IrcClient()
        {
            Dispose(false);
        }

        /// <summary>
        ///     Releases all resources used by the <see cref="IrcClient" />.
        /// </summary>
        /// <param name="disposing">
        ///     <see langword="true" /> if the consumer is actively disposing the object;
        ///     <see langword="false" /> if the garbage collector is finalizing the object.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref disposedFlag, 1, 0) > 0)
                return;
        }

        /// <summary>
        ///     Occurs when the client has connected to the server.
        /// </summary>
        /// <remarks>
        ///     Note that the <see cref="LocalUser" /> object is not yet set when this event occurs, but is only accessible
        ///     when the <see cref="Registered" /> event is raised.
        /// </remarks>
        public event EventHandler<EventArgs> Connected;

        /// <summary>
        ///     Occurs when the client has failed to connect to the server.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> ConnectFailed;

        /// <summary>
        ///     Occurs when the client has disconnected from the server.
        /// </summary>
        public event EventHandler<EventArgs> Disconnected;

        /// <summary>
        ///     Occurs when the client encounters an error during execution, while connected.
        /// </summary>
        public event EventHandler<IrcErrorEventArgs> Error;

#if !SILVERLIGHT

        /// <summary>
        ///     Occurs when the SSL certificate received from the server should be validated.
        ///     The certificate is automatically validated if this event is not handled.
        /// </summary>
        public event EventHandler<IrcValidateSslCertificateEventArgs> ValidateSslCertificate;

#endif

        /// <summary>
        ///     Occurs when a raw message has been sent to the server.
        /// </summary>
        public event EventHandler<IrcRawMessageEventArgs> RawMessageSent;

        /// <summary>
        ///     Occurs when a raw message has been received from the server.
        /// </summary>
        public event EventHandler<IrcRawMessageEventArgs> RawMessageReceived;

        /// <summary>
        ///     Occurs when a protocol (numeric) error is received from the server.
        /// </summary>
        public event EventHandler<IrcProtocolErrorEventArgs> ProtocolError;

        /// <summary>
        ///     Occurs when an error message (ERROR command) is received from the server.
        /// </summary>
        public event EventHandler<IrcErrorMessageEventArgs> ErrorMessageReceived;

        /// <summary>
        ///     Occurs when the connection has been registered.
        /// </summary>
        /// <remarks>
        ///     The <see cref="LocalUser" /> object is set when this event occurs.
        /// </remarks>
        public event EventHandler<EventArgs> Registered;

        /// <summary>
        ///     Occurs when the client information has been received from the server, following registration.
        /// </summary>
        /// <remarks>
        ///     Client information is accessible via <see cref="WelcomeMessage" />, <see cref="YourHostMessage" />,
        ///     <see cref="ServerCreatedMessage" />, <see cref="ServerName" />, <see cref="ServerVersion" />,
        ///     <see cref="ServerAvailableUserModes" />, and <see cref="ServerAvailableChannelModes" />.
        /// </remarks>
        public event EventHandler<EventArgs> ClientInfoReceived;

        /// <summary>
        ///     Occurs when a bounce message is received from the server, telling the client to connect to a new server.
        /// </summary>
        public event EventHandler<IrcServerInfoEventArgs> ServerBounce;

        /// <summary>
        ///     Occurs when a list of features supported by the server (ISUPPORT) has been received.
        ///     This event may be raised more than once after registration, depending on the size of the list received.
        /// </summary>
        public event EventHandler<EventArgs> ServerSupportedFeaturesReceived;

        /// <summary>
        ///     Occurs when a ping query is received from the server.
        ///     The client automatically replies to pings from the server; this event is only a notification.
        /// </summary>
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PingReceived;

        /// <summary>
        ///     Occurs when a pong reply is received from the server.
        /// </summary>
        public event EventHandler<IrcPingOrPongReceivedEventArgs> PongReceived;

        /// <summary>
        ///     Occurs when the Message of the Day (MOTD) has been received from the server.
        /// </summary>
        public event EventHandler<EventArgs> MotdReceived;

        /// <summary>
        ///     Occurs when information about the IRC network has been received from the server.
        /// </summary>
        public event EventHandler<IrcCommentEventArgs> NetworkInformationReceived;

        /// <summary>
        ///     Occurs when information about a specific server on the IRC network has been received from the server.
        /// </summary>
        public event EventHandler<IrcServerVersionInfoEventArgs> ServerVersionInfoReceived;

        /// <summary>
        ///     Occurs when the local date/time for a specific server has been received from the server.
        /// </summary>
        public event EventHandler<IrcServerTimeEventArgs> ServerTimeReceived;

        /// <summary>
        ///     Occurs when a list of server links has been received from the server.
        /// </summary>
        public event EventHandler<IrcServerLinksListReceivedEventArgs> ServerLinksListReceived;

        /// <summary>
        ///     Occurs when server statistics have been received from the server.
        /// </summary>
        public event EventHandler<IrcServerStatsReceivedEventArgs> ServerStatsReceived;

        /// <summary>
        ///     Occurs when a reply to a Who query has been received from the server.
        /// </summary>
        public event EventHandler<IrcNameEventArgs> WhoReplyReceived;

        /// <summary>
        ///     Occurs when a reply to a Who Is query has been received from the server.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> WhoIsReplyReceived;

        /// <summary>
        ///     Occurs when a reply to a Who Was query has been received from the server.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> WhoWasReplyReceived;

        /// <summary>
        ///     Occurs when a list of channels has been received from the server in response to a query.
        /// </summary>
        public event EventHandler<IrcChannelListReceivedEventArgs> ChannelListReceived;

        /// <inheritdoc cref="ListChannels(IEnumerable{string})" />
        public void ListChannels(params string[] channelNames)
        {
            CheckDisposed();

            if (channelNames == null)
                throw new ArgumentNullException("channelNames");

            SendMessageList(channelNames);
        }

        /// <summary>
        ///     Requests a list of information about the specified (or all) channels on the network.
        /// </summary>
        /// <param name="channelNames">
        ///     The names of the channels to list, or <see langword="null" /> to list all channels
        ///     on the network.
        /// </param>
        public void ListChannels(IEnumerable<string> channelNames = null)
        {
            CheckDisposed();

            SendMessageList(channelNames);
        }

        /// <summary>
        ///     Requests the Message of the Day (MOTD) from the specified server.
        /// </summary>
        /// <param name="targetServer">
        ///     The name of the server from which to request the MOTD, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetMessageOfTheDay(string targetServer = null)
        {
            CheckDisposed();

            SendMessageMotd(targetServer);
        }

        /// <summary>
        ///     Requests statistics about the connected IRC network.
        ///     If <paramref name="serverMask" /> is specified, then the server only returns information about the part of
        ///     the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        ///     network
        /// </summary>
        /// <param name="serverMask">
        ///     A wildcard expression for matching against server names, or <see langword="null" />
        ///     to match the entire network.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the message, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetNetworkInfo(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLUsers(serverMask, targetServer);
        }

        /// <summary>
        ///     Requests the version of the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose version to request.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerVersion(string targetServer = null)
        {
            CheckDisposed();

            SendMessageVersion(targetServer);
        }

        /// <summary>
        ///     Requests statistics about the specified server.
        /// </summary>
        /// <param name="query">
        ///     The query character that indicates which server statistics to return.
        ///     The set of valid query characters is dependent on the implementation of the particular IRC server.
        /// </param>
        /// <param name="targetServer">The name of the server whose statistics to request.</param>
        /// <remarks>
        ///     The server may not accept the command if <paramref name="query" /> is unspecified.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerStatistics(char? query = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageStats(query == null ? null : query.Value.ToString(), targetServer);
        }

        /// <summary>
        ///     Requests a list of all servers known by the target server.
        ///     If <paramref name="serverMask" /> is specified, then the server only returns information about the part of
        ///     the network formed by the servers whose names match the mask; otherwise, the information concerns the whole
        ///     network.
        /// </summary>
        /// <param name="serverMask">
        ///     A wildcard expression for matching against server names, or <see langword="null" />
        ///     to match the entire network.
        /// </param>
        /// <param name="targetServer">
        ///     The name of the server to which to forward the request, or <see langword="null" />
        ///     for the current server.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerLinks(string serverMask = null, string targetServer = null)
        {
            CheckDisposed();

            SendMessageLinks(serverMask, targetServer);
        }

        /// <summary>
        ///     Requests the local time on the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server whose local time to request.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void GetServerTime(string targetServer = null)
        {
            CheckDisposed();

            SendMessageTime(targetServer);
        }

        /// <summary>
        ///     Sends a ping to the specified server.
        /// </summary>
        /// <param name="targetServer">The name of the server to ping.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void Ping(string targetServer = null)
        {
            CheckDisposed();

            SendMessagePing(localUser.NickName, targetServer);
        }

        /// <summary>
        ///     Sends a Who query to the server targeting the specified channel or user masks.
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
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void QueryWho(string mask = null, bool onlyOperators = false)
        {
            CheckDisposed();

            SendMessageWho(mask, onlyOperators);
        }

        /// <inheritdoc cref="QueryWhoIs(IEnumerable{string})" />
        public void QueryWhoIs(params string[] nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException("nickNames");

            QueryWhoIs((IEnumerable<string>) nickNameMasks);
        }

        /// <overloads>Sends a Who Is query to the server.</overloads>
        /// <summary>
        ///     Sends a Who Is query to server targeting the specified nick name masks.
        /// </summary>
        /// <param name="nickNameMasks">
        ///     A collection of wildcard expressions for matching against nick names of users.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNameMasks" /> is <see langword="null" />.</exception>
        public void QueryWhoIs(IEnumerable<string> nickNameMasks)
        {
            CheckDisposed();

            if (nickNameMasks == null)
                throw new ArgumentNullException("nickNames");

            SendMessageWhoIs(nickNameMasks);
        }

        /// <inheritdoc cref="QueryWhoWas(IEnumerable{string}, int)" />
        public void QueryWhoWas(params string[] nickNames)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException("nickNames");

            QueryWhoWas((IEnumerable<string>) nickNames);
        }

        /// <summary>
        ///     Sends a Who Was query to server targeting the specified nick names.
        /// </summary>
        /// <param name="nickNames">The nick names of the users to query.</param>
        /// <param name="entriesCount">
        ///     The maximum number of entries to return from the query. A negative value
        ///     specifies to return an unlimited number of entries.
        /// </param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="nickNames" /> is <see langword="null" />.</exception>
        public void QueryWhoWas(IEnumerable<string> nickNames, int entriesCount = -1)
        {
            CheckDisposed();

            if (nickNames == null)
                throw new ArgumentNullException("nickNames");

            SendMessageWhoWas(nickNames, entriesCount);
        }

        /// <inheritdoc cref="Quit(string)" />
        /// <summary>
        ///     Quits the server, giving the specified comment. Waits the specified duration of time before forcibly
        ///     disconnecting.
        /// </summary>
        /// <param name="timeout">The number of milliseconds to wait before forcibly disconnecting.</param>
        /// <remarks>
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public virtual void Quit(int timeout, string comment = null)
        {
            SendMessageQuit(comment);
        }

        /// <summary>
        ///     Quits the server, giving the specified comment.
        /// </summary>
        /// <param name="comment">The comment to send to the server.</param>
        /// <remarks>
        ///     Note that because this message is not sent immediately, calling <see cref="Disconnect" /> immediately after
        ///     this will likely disconnect the client before it has a chance to quit the server properly.
        ///     Quitting the server should automatically disconnect the client.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void Quit(string comment = null)
        {
            Quit(0, comment);
        }

        /// <summary>
        ///     Sends the specified raw message to the server.
        /// </summary>
        /// <param name="message">The text (single line) of the message to send the server.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="message" /> is <see langword="null" />.</exception>
        public void SendRawMessage(string message)
        {
            CheckDisposed();

            if (message == null)
                throw new ArgumentNullException("message");

            WriteMessage(message);
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
                throw new ArgumentNullException("paramName");
            if (paramName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "paramName");

            // Check name of parameter.
            switch (paramName.ToLowerInvariant())
            {
                case "prefix":
                    var prefixValueMatch = Regex.Match(paramValue, isupportPrefix);
                    ;
                    var prefixes = prefixValueMatch.Groups["prefixes"].GetValue();
                    var modes = prefixValueMatch.Groups["modes"].GetValue();

                    if (prefixes.Length != modes.Length)
                        throw new ProtocolViolationException(Resources.MessageISupportPrefixInvalid);

                    lock (((ICollection) ChannelUserModes).SyncRoot)
                    {
                        channelUserModes.Clear();
                        channelUserModes.AddRange(modes);
                    }

                    channelUserModesPrefixes.Clear();
                    for (var i = 0; i < prefixes.Length; i++)
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
                throw new ArgumentNullException("input");
            if (input.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "input");

            char mode;
            if (channelUserModesPrefixes.TryGetValue(input[0], out mode))
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
                throw new ArgumentNullException("messageParameters");

            var modes = new StringBuilder();
            var modeParameters = new List<string>();
            foreach (var p in messageParameters)
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
            return Tuple.Create(modes.ToString(), (IEnumerable<string>) modeParameters.AsReadOnly());
        }

        /// <summary>
        ///     Gets a list of channel objects from the specified comma-separated list of channel names.
        /// </summary>
        /// <param name="namesList">A value that contains a comma-separated list of names of channels.</param>
        /// <returns>A list of channel objects that corresponds to the given list of channel names.</returns>
        protected IEnumerable<IrcChannel> GetChannelsFromList(string namesList)
        {
            if (namesList == null)
                throw new ArgumentNullException("namesList");

            return namesList.Split(',').Select(n => GetChannelFromName(n));
        }

        /// <summary>
        ///     Gets a list of user objects from the specified comma-separated list of nick names.
        /// </summary>
        /// <param name="nickNamesList">A value that contains a comma-separated list of nick names of users.</param>
        /// <returns>A list of user objects that corresponds to the given list of nick names.</returns>
        protected IEnumerable<IrcUser> GetUsersFromList(string nickNamesList)
        {
            if (nickNamesList == null)
                throw new ArgumentNullException("nickNamesList");

            lock (((ICollection) Users).SyncRoot)
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
                throw new ArgumentNullException("name");

            return Regex.IsMatch(name, regexChannelName);
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
                        Resources.MessageInvalidChannelType, type), "type");
            }
        }

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
                throw new ArgumentNullException("targetName");
            if (targetName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "targetName");

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
            if (nickName != null)
            {
                // Find user by nick name. If no user exists in list, create it and set its properties.
                var user = GetUserFromNickName(nickName, true);
                if (user.UserName == null)
                    user.UserName = userName;
                if (user.HostName == null)
                    user.HostName = hostName;

                return user;
            }
            if (userName != null)
            {
                // Find user by user  name. If no user exists in list, create it and set its properties.
                var user = GetUserFromNickName(nickName, true);
                if (user.HostName == null)
                    user.HostName = hostName;

                return user;
            }
            if (targetMask != null)
            {
                return new IrcTargetMask(targetMask);
            }
            throw new ArgumentException(string.Format(
                Resources.MessageInvalidSource, targetName), "targetName");
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
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "prefix");

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
            if (nickName != null)
            {
                // Find user by nick name. If no user exists in list, create it and set its properties.
                var user = GetUserFromNickName(nickName, true);
                if (user.UserName == null)
                    user.UserName = userName;
                if (user.HostName == null)
                    user.HostName = hostName;

                return user;
            }
            throw new ArgumentException(string.Format(
                Resources.MessageInvalidSource, prefix), "prefix");
        }

        /// <inheritdoc cref="GetServerFromHostName(string, out bool)" />
        protected IrcServer GetServerFromHostName(string hostName)
        {
            bool createdNew;
            return GetServerFromHostName(hostName, out createdNew);
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
                throw new ArgumentNullException("hostName");
            if (hostName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "hostName");

            // Search for server with given name in list of known servers. If it does not exist, add it.
            var server = servers.SingleOrDefault(s => s.HostName == hostName);
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
            bool createdNew;
            return GetChannelFromName(channelName, out createdNew);
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
                throw new ArgumentNullException("channelName");
            if (channelName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "channelName");

            // Search for channel with given name in list of known channel. If it does not exist, add it.
            lock (((ICollection) Channels).SyncRoot)
            {
                var channel = channels.SingleOrDefault(c => c.Name == channelName);
                if (channel == null)
                {
                    channel = new IrcChannel(channelName);
                    channel.Client = this;
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
            bool createdNew;
            return GetUserFromNickName(nickName, isOnline, out createdNew);
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
                throw new ArgumentNullException("nickName");
            if (nickName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "nickName");

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            IrcUser user;
            lock (((ICollection) Users).SyncRoot)
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
            bool createdNew;
            return GetUserFromUserName(userName, out createdNew);
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
                throw new ArgumentNullException("userName");
            if (userName.Length == 0)
                throw new ArgumentException(Resources.MessageValueCannotBeEmptyString, "userName");

            // Search for user with given nick name in list of known users. If it does not exist, add it.
            lock (((ICollection) Users).SyncRoot)
            {
                var user = users.SingleOrDefault(u => u.UserName == userName);
                if (user == null)
                {
                    user = new IrcUser();
                    user.Client = this;
                    user.UserName = userName;
                    users.Add(user);

                    createdNew = true;
                }
                else
                {
                    createdNew = false;
                }

                return user;
            }
        }

        protected int GetNumericUserMode(ICollection<char> modes)
        {
            var value = 0;
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
            foreach (var method in this.GetAttributedMethods<MessageProcessorAttribute, MessageProcessor>())
            {
                var attribute = method.Item1;
                var methodDelegate = method.Item2;

                var commandRangeParts = attribute.CommandName.Split('-');
                if (commandRangeParts.Length == 2)
                {
                    // Numeric command range was defined.
                    var commandRangeStart = int.Parse(commandRangeParts[0]);
                    var commandRangeEnd = int.Parse(commandRangeParts[1]);
                    for (var code = commandRangeStart; code <= commandRangeEnd; code++)
                        numericMessageProcessors.Add(code, methodDelegate);
                }
                else if (commandRangeParts.Length == 1)
                {
                    // Single command name was defined. Check whether it is numeric or alphabetic.
                    int commandCode;
                    if (int.TryParse(attribute.CommandName, out commandCode))
                        // Command is numeric.
                        numericMessageProcessors.Add(commandCode, methodDelegate);
                    else
                    // Command is alphabetic.
                        messageProcessors.Add(attribute.CommandName, methodDelegate);
                }
                else
                {
                    throw new ProtocolViolationException(string.Format(
                        Resources.MessageInvalidCommandDefinition, attribute.CommandName));
                }
            }
            ;
        }

        /// <inheritdoc cref="WriteMessage(string, string, string[])" />
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            CheckDisposed();

            WriteMessage(prefix, command, parameters.ToArray());
        }

        /// <inheritdoc cref="WriteMessage(IrcMessage)" />
        /// <param name="prefix">The message prefix that represents the source of the message.</param>
        /// <param name="command">The name of the command.</param>
        /// <param name="parameters">A collection of the parameters to the command.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            CheckDisposed();

            var message = new IrcMessage(this, prefix, command, parameters.ToArray());
            if (message.Source == null)
                message.Source = localUser;
            WriteMessage(message);
        }

        /// <inheritdoc cref="WriteMessage(string, object)" />
        /// <summary>
        ///     Writes the specified message (prefix, command, and parameters) to the network stream.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <exception cref="ArgumentException">
        ///     <paramref name="message" /> contains more than 15 many parameters.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The value of <see cref="IrcMessage.Command" /> of
        ///     <paramref name="message" /> is invalid.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The value of one of the items of <see cref="IrcMessage.Parameters" /> of
        ///     <paramref name="message" /> is invalid.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(IrcMessage message)
        {
            CheckDisposed();

            if (message.Command == null)
                throw new ArgumentException(Resources.MessageInvalidCommand, "message");
            if (message.Parameters.Count > maxParamsCount)
                throw new ArgumentException(Resources.MessageTooManyParams, "parameters");

            var lineBuilder = new StringBuilder();

            // Append prefix to line, if specified.
            if (message.Prefix != null)
                lineBuilder.Append(":" + CheckPrefix(message.Prefix) + " ");

            // Append command name to line.
            lineBuilder.Append(CheckCommand(message.Command).ToUpper());

            // Append each parameter to line, adding ':' character before last parameter.
            for (var i = 0; i < message.Parameters.Count - 1; i++)
            {
                if (message.Parameters[i] != null)
                    lineBuilder.Append(" " + CheckMiddleParameter(message.Parameters[i]));
            }
            if (message.Parameters.Count > 0)
            {
                var lastParameter = message.Parameters[message.Parameters.Count - 1];
                if (lastParameter != null)
                    lineBuilder.Append(" :" + CheckTrailingParameter(lastParameter));
            }

            // Send raw message as line of text.
            var line = lineBuilder.ToString();
            var messageSentEventArgs = new IrcRawMessageEventArgs(message, line);
            WriteMessage(line, messageSentEventArgs);
        }

        protected virtual void WriteMessage(string line, object token = null)
        {
            CheckDisposed();

            Debug.Assert(line != null);
        }

        private void ReadMessage(IrcMessage message, string line)
        {
            // Try to find corresponding message processor for command of given message.
            MessageProcessor messageProcessor;
            int commandCode;
            if (messageProcessors.TryGetValue(message.Command, out messageProcessor) ||
                (int.TryParse(message.Command, out commandCode) &&
                 numericMessageProcessors.TryGetValue(commandCode, out messageProcessor)))
            {
                try
                {
                    messageProcessor(message);
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
                DebugUtilities.WriteEvent("Unknown IRC message command '{0}'.", message.Command);
            }
        }

        private string CheckPrefix(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Resources.MessageInvalidPrefix, value), "value");
            }

            return value;
        }

        private string CheckCommand(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(IsInvalidMessageChar))
            {
                throw new ArgumentException(string.Format(
                    Resources.MessageInvalidCommand, value), "value");
            }

            return value;
        }

        private string CheckMiddleParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Length == 0 || value.Any(c => IsInvalidMessageChar(c) || c == ' ') || value[0] == ':')
            {
                throw new ArgumentException(string.Format(
                    Resources.MessageInvalidMiddleParameter, value), "value");
            }

            return value;
        }

        private string CheckTrailingParameter(string value)
        {
            Debug.Assert(value != null);

            if (value.Any(c => IsInvalidMessageChar(c)))
            {
                throw new ArgumentException(string.Format(
                    Resources.MessageInvalidMiddleParameter, value), "value");
            }

            return value;
        }

        private bool IsInvalidMessageChar(char value)
        {
            return value == '\0' || value == '\r' || value == '\n';
        }

        protected void Connect(IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            CheckRegistrationInfo(registrationInfo, "registrationInfo");
            ResetState();
        }

        protected void CheckRegistrationInfo(IrcRegistrationInfo registrationInfo, string registrationInfoParamName)
        {
            // Check that given registration info is valid.
            if (registrationInfo is IrcUserRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcUserRegistrationInfo) registrationInfo).UserName == null)
                    throw new ArgumentException(Resources.MessageInvalidUserRegistrationInfo,
                        registrationInfoParamName);
            }
            else if (registrationInfo is IrcServiceRegistrationInfo)
            {
                if (registrationInfo.NickName == null ||
                    ((IrcServiceRegistrationInfo) registrationInfo).Description == null)
                    throw new ArgumentException(Resources.MessageInvalidServiceRegistrationInfo,
                        registrationInfoParamName);
            }
            else
            {
                throw new ArgumentException(Resources.MessageInvalidRegistrationInfoObject,
                    registrationInfoParamName);
            }
        }

        /// <summary>
        ///     Disconnects asynchronously from the server.
        /// </summary>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        /// <remarks>
        ///     This method closes the client connection immediately and forcibly, and does not send a quit message to the
        ///     server. To disconnect from the IRC server gracefully, call <see cref="Quit(string)" /> and wait for the
        ///     connection to be closed.
        /// </remarks>
        public virtual void Disconnect()
        {
            CheckDisposed();
        }

        protected void ParseMessage(string line)
        {
            string prefix = null;
            string lineAfterPrefix = null;

            // Extract prefix from message line, if it contains one.
            if (line[0] == ':')
            {
                var firstSpaceIndex = line.IndexOf(' ');
                Debug.Assert(firstSpaceIndex != -1);
                prefix = line.Substring(1, firstSpaceIndex - 1);
                lineAfterPrefix = line.Substring(firstSpaceIndex + 1);
            }
            else
            {
                lineAfterPrefix = line;
            }

            // Extract command from message.
            var spaceIndex = lineAfterPrefix.IndexOf(' ');
            Debug.Assert(spaceIndex != -1);
            var command = lineAfterPrefix.Substring(0, spaceIndex);
            var paramsLine = lineAfterPrefix.Substring(command.Length + 1);

            // Extract parameters from message.
            // Each parameter is separated by single space, except last one, which may contain spaces if it
            // is prefixed by colon.
            var parameters = new string[maxParamsCount];
            int paramStartIndex, paramEndIndex = -1;
            var lineColonIndex = paramsLine.IndexOf(" :");
            if (lineColonIndex == -1 && !paramsLine.StartsWith(":"))
                lineColonIndex = paramsLine.Length;
            for (var i = 0; i < parameters.Length; i++)
            {
                paramStartIndex = paramEndIndex + 1;
                paramEndIndex = paramsLine.IndexOf(' ', paramStartIndex);
                if (paramEndIndex == -1)
                    paramEndIndex = paramsLine.Length;
                if (paramEndIndex > lineColonIndex)
                {
                    paramStartIndex++;
                    paramEndIndex = paramsLine.Length;
                }
                parameters[i] = paramsLine.Substring(paramStartIndex, paramEndIndex - paramStartIndex);
                if (paramEndIndex == paramsLine.Length)
                    break;
            }

            // Parse received IRC message.
            var message = new IrcMessage(this, prefix, command, parameters);
            var messageReceivedEventArgs = new IrcRawMessageEventArgs(message, line);
            OnRawMessageReceived(messageReceivedEventArgs);
            ReadMessage(message, line);

#if DEBUG
            DebugUtilities.WriteIrcRawLine(this, ">>> " + messageReceivedEventArgs.RawContent);
#endif
        }

        protected void HandleClientConnecting()
        {
            DebugUtilities.WriteEvent("Connecting to server...");
        }

        protected virtual void HandleClientConnected(IrcRegistrationInfo regInfo)
        {
            if (regInfo.Password != null)
                // Authenticate with server using password.
                SendMessagePassword(regInfo.Password);

            // Check if client is registering as service or normal user.
            if (regInfo is IrcServiceRegistrationInfo)
            {
                // Register client as service.
                var serviceRegInfo = (IrcServiceRegistrationInfo) regInfo;
                SendMessageService(serviceRegInfo.NickName, serviceRegInfo.Distribution,
                    serviceRegInfo.Description);

                localUser = new IrcLocalUser(serviceRegInfo.NickName, serviceRegInfo.Distribution,
                    serviceRegInfo.Description);
            }
            else
            {
                // Register client as normal user.
                var userRegInfo = (IrcUserRegistrationInfo) regInfo;
                SendMessageNick(userRegInfo.NickName);
                SendMessageUser(userRegInfo.UserName, GetNumericUserMode(userRegInfo.UserModes),
                    userRegInfo.RealName);

                localUser = new IrcLocalUser(userRegInfo.NickName, userRegInfo.UserName, userRegInfo.RealName,
                    userRegInfo.UserModes);
            }
            localUser.Client = this;

            // Add local user to list of known users.
            lock (((ICollection) Users).SyncRoot)
                users.Add(localUser);

            OnConnected(new EventArgs());
        }

        protected virtual void HandleClientDisconnected()
        {
            OnDisconnected(new EventArgs());
        }

        /// <summary>
        ///     Raises the <see cref="Connected" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnConnected(EventArgs e)
        {
            var handler = Connected;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ConnectFailed" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnConnectFailed(IrcErrorEventArgs e)
        {
            var handler = ConnectFailed;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Disconnected" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnDisconnected(EventArgs e)
        {
            var handler = Disconnected;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Error" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnError(IrcErrorEventArgs e)
        {
            var handler = Error;
            if (handler != null)
                handler(this, e);
        }

#if !SILVERLIGHT

        /// <summary>
        ///     Raises the <see cref="ValidateSslCertificate" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcValidateSslCertificateEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnValidateSslCertificate(IrcValidateSslCertificateEventArgs e)
        {
            var handler = ValidateSslCertificate;
            if (handler != null)
                handler(this, e);
        }

#endif

        /// <summary>
        ///     Raises the <see cref="RawMessageSent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageSent(IrcRawMessageEventArgs e)
        {
            var handler = RawMessageSent;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="RawMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageReceived(IrcRawMessageEventArgs e)
        {
            var handler = RawMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ProtocolError" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcProtocolErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnProtocolError(IrcProtocolErrorEventArgs e)
        {
            var handler = ProtocolError;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ErrorMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnErrorMessageReceived(IrcErrorMessageEventArgs e)
        {
            var handler = ErrorMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ClientInfoReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnClientInfoReceived(EventArgs e)
        {
            var handler = ClientInfoReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Registered" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnRegistered(EventArgs e)
        {
            var handler = Registered;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerBounce" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerInfoEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerBounce(IrcServerInfoEventArgs e)
        {
            var handler = ServerBounce;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerSupportedFeaturesReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerSupportedFeaturesReceived(EventArgs e)
        {
            var handler = ServerSupportedFeaturesReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PingReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPingReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = PingReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PongReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPongReceived(IrcPingOrPongReceivedEventArgs e)
        {
            var handler = PongReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="MotdReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnMotdReceived(EventArgs e)
        {
            var handler = MotdReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="NetworkInformationReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcCommentEventArgs" /> instance containing the event data.</param>
        protected virtual void OnNetworkInformationReceived(IrcCommentEventArgs e)
        {
            var handler = NetworkInformationReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerVersionInfoReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerVersionInfoEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerVersionInfoReceived(IrcServerVersionInfoEventArgs e)
        {
            var handler = ServerVersionInfoReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerTimeReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerTimeEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerTimeReceived(IrcServerTimeEventArgs e)
        {
            var handler = ServerTimeReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerLinksListReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcServerLinksListReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnServerLinksListReceived(IrcServerLinksListReceivedEventArgs e)
        {
            var handler = ServerLinksListReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerStatsReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcServerStatsReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnServerStatsReceived(IrcServerStatsReceivedEventArgs e)
        {
            var handler = ServerStatsReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcNameEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoReplyReceived(IrcNameEventArgs e)
        {
            var handler = WhoReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoIsReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoIsReplyReceived(IrcUserEventArgs e)
        {
            var handler = WhoIsReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoWasReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoWasReplyReceived(IrcUserEventArgs e)
        {
            var handler = WhoWasReplyReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ChannelListReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcChannelListReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnChannelListReceived(IrcChannelListReceivedEventArgs e)
        {
            var handler = ChannelListReceived;
            if (handler != null)
                handler(this, e);
        }

        protected void CheckDisposed()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        /// <summary>
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            if (!IsDisposed && IsConnected)
                return string.Format("{0}@{1}", localUser.UserName,
                    ServerName);
            return "(Not connected)";
        }

        /// <summary>
        ///     Represents a method that processes <see cref="IrcMessage" /> objects.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        protected delegate void MessageProcessor(IrcMessage message);

        /// <summary>
        ///     Represents a raw IRC message that is sent/received by <see cref="IrcClient" />.
        ///     A message contains a prefix (representing the source), a command name (a word or three-digit number),
        ///     and any number of parameters (up to a maximum of 15).
        /// </summary>
        /// <seealso cref="IrcClient" />
        [DebuggerDisplay("{ToString(), nq}")]
        public struct IrcMessage
        {
            /// <summary>
            ///     The source of the message, which is the object represented by the value of <see cref="Prefix" />.
            /// </summary>
            public IIrcMessageSource Source;

            /// <summary>
            ///     The message prefix.
            /// </summary>
            public string Prefix;

            /// <summary>
            ///     The name of the command.
            /// </summary>
            public string Command;

            /// <summary>
            ///     A list of the parameters to the message.
            /// </summary>
            public IList<string> Parameters;

            /// <summary>
            ///     Initializes a new instance of the <see cref="IrcMessage" /> structure.
            /// </summary>
            /// <param name="client">A client object that has sent/will receive the message.</param>
            /// <param name="prefix">The message prefix that represents the source of the message.</param>
            /// <param name="command">The command name; either an alphabetic word or 3-digit number.</param>
            /// <param name="parameters">
            ///     A list of the parameters to the message. Can contain a maximum of 15 items.
            /// </param>
            public IrcMessage(IrcClient client, string prefix, string command, IList<string> parameters)
            {
                Prefix = prefix;
                Command = command;
                Parameters = parameters;

                Source = client.GetSourceFromPrefix(prefix);
            }

            /// <summary>
            ///     Returns a string representation of this instance.
            /// </summary>
            /// <returns>A string that represents this instance.</returns>
            public override string ToString()
            {
                return string.Format("{0} ({1} parameters)", Command, Parameters.Count);
            }
        }

        #region Protocol Data

        // Internal collection of all known servers.
        private Collection<IrcServer> servers;

        // True if connection has been registered with server;
        protected bool isRegistered;

        // Stores information about local user.
        protected IrcLocalUser localUser;

        // Dictionary of protocol features supported by server.
        private Dictionary<string, string> serverSupportedFeatures;

        // Collection of channel modes that apply to users in a channel.
        private Collection<char> channelUserModes;

        // Dictionary of nick name prefixes (keys) and their corresponding channel modes.
        private Dictionary<char, char> channelUserModesPrefixes;

        // Builds MOTD (message of the day) string as it is received from server.
        protected StringBuilder motdBuilder;

        // Information about the IRC network given by the server.
        private IrcNetworkInfo networkInformation;

        // Collection of all currently joined channels.
        private Collection<IrcChannel> channels;

        // Collection of all known users.
        private Collection<IrcUser> users;

        // List of information about channels, returned by server in response to last LIST message.
        private List<IrcChannelInfo> listedChannels;

        // List of other servers to which server links, returned by server in response to last LINKS message.
        private List<IrcServerInfo> listedServerLinks;

        // List of statistical entries, returned by server in response to last STATS message.
        private List<IrcServerStatisticalEntry> listedStatsEntries;

        #endregion

        #region Proxy Methods

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

        internal void Join(IEnumerable<string> channels)
        {
            SendMessageJoin(channels);
        }

        internal void Join(IEnumerable<Tuple<string, string>> channels)
        {
            SendMessageJoin(channels);
        }

        internal void Leave(IEnumerable<string> channels, string comment = null)
        {
            SendMessagePart(channels, comment);
        }

        internal void SendPrivateMessage(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
            SendMessagePrivateMessage(targetsNamesArray, text);
            localUser.HandleMessageSent(targets, text);
        }

        internal void SendNotice(IEnumerable<string> targetsNames, string text)
        {
            var targetsNamesArray = targetsNames.ToArray();
            var targets = targetsNamesArray.Select(n => GetMessageTarget(n)).ToArray();
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

        #endregion
    }
}