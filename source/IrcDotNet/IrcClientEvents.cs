using System;
using System.Collections;

namespace IrcDotNet
{
    partial class IrcClient
    {
        protected void HandleClientConnecting()
        {
            DebugUtilities.WriteEvent("Connecting to server...");
        }

        protected virtual void HandleClientConnected(IrcRegistrationInfo regInfo)
        {
            if (regInfo.Password != null)
            {
                // Authenticate with server using password.
                SendMessagePassword(regInfo.Password);
            }

            // Check if client is registering as service or normal user.
            var info = regInfo as IrcServiceRegistrationInfo;
            if (info != null)
            {
                // Register client as service.
                var serviceRegInfo = info;
                SendMessageService(serviceRegInfo.NickName, serviceRegInfo.Distribution, serviceRegInfo.Description);

                localUser = new IrcLocalUser(serviceRegInfo.NickName, serviceRegInfo.Distribution, serviceRegInfo.Description);
            }
            else
            {
                // Register client as normal user.
                var userRegInfo = regInfo as IrcUserRegistrationInfo;
                if (userRegInfo == null)
                {
                    throw new ArgumentException(nameof(regInfo));
                }

                SendMessageNick(userRegInfo.NickName);
                SendMessageUser(userRegInfo.UserName, GetNumericUserMode(userRegInfo.UserModes), userRegInfo.RealName);

                localUser = new IrcLocalUser(userRegInfo.NickName, userRegInfo.UserName, userRegInfo.RealName, userRegInfo.UserModes);
            }
            localUser.Client = this;

            // Add local user to list of known users.
            lock (((ICollection)Users).SyncRoot)
                users.Add(localUser);

            OnConnected(new EventArgs());
        }

        protected virtual void HandleClientDisconnected()
        {
            OnDisconnected(new EventArgs());
        }

        #region Event Definitions
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
        #endregion

        #region Event Raising Methods
        /// <summary>
        ///     Raises the <see cref="Connected" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnConnected(EventArgs e)
        {
            Connected?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ConnectFailed" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnConnectFailed(IrcErrorEventArgs e)
        {
            ConnectFailed?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Disconnected" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnDisconnected(EventArgs e)
        {
            Disconnected?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Error" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnError(IrcErrorEventArgs e)
        {
            Error?.Invoke(this, e);
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
            ValidateSslCertificate?.Invoke(this, e);
        }

#endif

        /// <summary>
        ///     Raises the <see cref="RawMessageSent" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageSent(IrcRawMessageEventArgs e)
        {
            RawMessageSent?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="RawMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcRawMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnRawMessageReceived(IrcRawMessageEventArgs e)
        {
            RawMessageReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ProtocolError" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcProtocolErrorEventArgs" /> instance containing the event data.</param>
        protected virtual void OnProtocolError(IrcProtocolErrorEventArgs e)
        {
            ProtocolError?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ErrorMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcErrorMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnErrorMessageReceived(IrcErrorMessageEventArgs e)
        {
            ErrorMessageReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ClientInfoReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnClientInfoReceived(EventArgs e)
        {
            ClientInfoReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="Registered" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnRegistered(EventArgs e)
        {
            Registered?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerBounce" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerInfoEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerBounce(IrcServerInfoEventArgs e)
        {
            ServerBounce?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerSupportedFeaturesReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerSupportedFeaturesReceived(EventArgs e)
        {
            ServerSupportedFeaturesReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PingReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPingReceived(IrcPingOrPongReceivedEventArgs e)
        {
            PingReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PongReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPingOrPongReceivedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPongReceived(IrcPingOrPongReceivedEventArgs e)
        {
            PongReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="MotdReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnMotdReceived(EventArgs e)
        {
            MotdReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="NetworkInformationReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcCommentEventArgs" /> instance containing the event data.</param>
        protected virtual void OnNetworkInformationReceived(IrcCommentEventArgs e)
        {
            NetworkInformationReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerVersionInfoReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerVersionInfoEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerVersionInfoReceived(IrcServerVersionInfoEventArgs e)
        {
            ServerVersionInfoReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerTimeReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcServerTimeEventArgs" /> instance containing the event data.</param>
        protected virtual void OnServerTimeReceived(IrcServerTimeEventArgs e)
        {
            ServerTimeReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerLinksListReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcServerLinksListReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnServerLinksListReceived(IrcServerLinksListReceivedEventArgs e)
        {
            ServerLinksListReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ServerStatsReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcServerStatsReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnServerStatsReceived(IrcServerStatsReceivedEventArgs e)
        {
            ServerStatsReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcNameEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoReplyReceived(IrcNameEventArgs e)
        {
            WhoReplyReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoIsReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoIsReplyReceived(IrcUserEventArgs e)
        {
            WhoIsReplyReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="WhoWasReplyReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnWhoWasReplyReceived(IrcUserEventArgs e)
        {
            WhoWasReplyReceived?.Invoke(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ChannelListReceived" /> event.
        /// </summary>
        /// <param name="e">
        ///     The <see cref="IrcChannelListReceivedEventArgs" /> instance containing the event data.
        /// </param>
        protected virtual void OnChannelListReceived(IrcChannelListReceivedEventArgs e)
        {
            ChannelListReceived?.Invoke(this, e);
        }
        #endregion
    }
}