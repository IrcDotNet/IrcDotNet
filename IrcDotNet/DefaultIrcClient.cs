using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace IrcDotNet
{
    public class DefaultIrcClient : IrcClient
    {
        // True if client can currently be disconnected.
        private bool canDisconnect;

        // Queue of messages to be sent by write loop when ready.
        private Queue<string> messageSendQueue;

        private TcpClient client;
        private AutoResetEvent disconnectedEvent;
        private Thread readThread;
        private Thread writeThread;
        private NetworkStream stream;
        private Stream dataStream;
        private Encoding dataStreamEncoding;
        private StreamWriter writer;
        private StreamReader reader;

        public DefaultIrcClient()
            : base()
        {
            this.messageSendQueue = new Queue<string>();

            this.client = new TcpClient();
            this.disconnectedEvent = new AutoResetEvent(false);
            this.readThread = new Thread(ReadLoop);
            this.writeThread = new Thread(WriteLoop);
            this.dataStreamEncoding = Encoding.Default;

            InitializeMessageProcessors();
            ResetState();
        }

        /// <summary>
        /// Gets or sets the text encoding to use for reading from and writing to the network data stream.
        /// </summary>
        /// <value>The text encoding of the data stream.</value>
        public Encoding DataStreamEncoding
        {
            get { return this.dataStreamEncoding; }
            set { this.dataStreamEncoding = value; }
        }

        public override bool IsConnected
        {
            get { return this.client.Connected; }
        }

        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                if (disposing)
                {
                    DisconnectInternal();

                    if (this.client != null)
                    {
                        this.client.Close();
                        this.client = null;
                    }
                    if (this.disconnectedEvent != null)
                    {
                        this.disconnectedEvent.Close();
                        this.disconnectedEvent = null;
                    }
                    if (this.readThread != null)
                    {
                        if (this.readThread.IsAlive)
                            this.readThread.Join(1000);
                        this.readThread = null;
                    }
                    if (this.writeThread != null)
                    {
                        if (this.writeThread.IsAlive)
                            this.writeThread.Join(1000);
                        this.writeThread = null;
                    }
                    if (this.stream != null)
                    {
                        this.stream.Close();
                        this.stream = null;
                    }
                    if (this.dataStream != null)
                    {
                        this.dataStream.Close();
                        this.dataStream = null;
                    }
                    if (this.writer != null)
                    {
                        this.writer.Close();
                        this.writer = null;
                    }
                    if (this.reader != null)
                    {
                        this.reader.Close();
                        this.reader = null;
                    }
                }
            }
            IsDisposed = true;
        }

        public override void Connect(Uri url, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");
            CheckRegistrationInfo(registrationInfo, "registrationInfo");

            // Check URL scheme and decide whether to use SSL.
            bool useSsl;
            if (url.Scheme == "irc")
                useSsl = false;
            else if (url.Scheme == "ircs")
                useSsl = true;
            else
                throw new ArgumentException(string.Format(Properties.Resources.ErrorMessageInvalidUrlScheme,
                                                          url.Scheme), "url");

            Connect(url.Host, url.Port == -1 ? DefaultPort : url.Port, useSsl, registrationInfo);
        }

        public override void Connect(string host, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");
            CheckRegistrationInfo(registrationInfo, "registrationInfo");

            DisconnectInternal();
            this.client.BeginConnect(host, port, ConnectCallback,
                                     Tuple.Create(useSsl, host, registrationInfo));
            HandleClientConnecting();
        }

        public override void Connect(IPAddress address, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");
            CheckRegistrationInfo(registrationInfo, "registrationInfo");

            DisconnectInternal();
            this.client.BeginConnect(address, port, ConnectCallback,
                                     Tuple.Create(useSsl, string.Empty, registrationInfo));
            HandleClientConnecting();
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="addresses">A collection of one or more IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public override void Connect(IPAddress[] addresses, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");
            CheckRegistrationInfo(registrationInfo, "registrationInfo");

            DisconnectInternal();
            this.client.BeginConnect(addresses, port, ConnectCallback,
                                     Tuple.Create(useSsl, string.Empty, registrationInfo));
            HandleClientConnecting();
        }

        public override void Connect(IPEndPoint remoteEP, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");
            CheckRegistrationInfo(registrationInfo, "registrationInfo");

            DisconnectInternal();
            this.client.BeginConnect(remoteEP.Address, remoteEP.Port, ConnectCallback,
                                     Tuple.Create(useSsl, string.Empty, registrationInfo));
            HandleClientConnecting();
        }

        private void ConnectCallback(IAsyncResult ar)
        {
            try
            {
                var state = (Tuple<bool, string, IrcRegistrationInfo>)ar.AsyncState;
                this.client.EndConnect(ar);

                // Set up network I/O objects.
                this.stream = this.client.GetStream();
                this.dataStream = GetDataStream(state.Item1, state.Item2);
                this.writer = new StreamWriter(this.dataStream, this.dataStreamEncoding);
                this.reader = new StreamReader(this.dataStream, this.dataStreamEncoding);

                HandleClientConnected(state.Item3);
                this.readThread.Start();
                this.writeThread.Start();

                OnConnected(new EventArgs());
            }
            catch (Exception ex)
            {
                OnConnectFailed(new IrcErrorEventArgs(ex));
            }
        }

        private Stream GetDataStream(bool useSsl, string targetHost)
        {
            if (useSsl)
            {
                // Create SSL stream over network stream, to use for data transmission.
                var sslStream = new SslStream(this.stream, true,
                                              new RemoteCertificateValidationCallback(SslUserCertificateValidationCallback));
                sslStream.AuthenticateAsClient(targetHost);
                Debug.Assert(sslStream.IsAuthenticated);
                return sslStream;
            }
            else
            {
                // Use network stream directly for data transmission.
                return this.stream;
            }
        }

        private bool SslUserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain,
                                                          SslPolicyErrors sslPolicyErrors)
        {
            // Raise an event to decide whether the certificate is valid.
            var eventArgs = new IrcValidateSslCertificateEventArgs(certificate, chain, sslPolicyErrors);
            eventArgs.IsValid = true;
            OnValidateSslCertificate(eventArgs);
            return eventArgs.IsValid;
        }

        private void HandleClientConnecting()
        {
            Debug.WriteLine("Connecting to server...");

            this.canDisconnect = true;
        }

        private void HandleClientConnected(IrcRegistrationInfo regInfo)
        {
            Debug.WriteLine(string.Format("Connected to server at '{0}'.",
                                          ((IPEndPoint)this.client.Client.RemoteEndPoint).Address));

            try
            {
                if (regInfo.Password != null)
                    SendMessagePassword(regInfo.Password);
                if (regInfo is IrcServiceRegistrationInfo)
                {
                    // Register client as service.
                    var serviceRegInfo = (IrcServiceRegistrationInfo)regInfo;
                    SendMessageService(serviceRegInfo.NickName, serviceRegInfo.Distribution,
                                       serviceRegInfo.Description);

                    this.localUser = new IrcLocalUser(serviceRegInfo.NickName, serviceRegInfo.Distribution,
                                                      serviceRegInfo.Description);
                }
                else
                {
                    // Register client as normal user.
                    var userRegInfo = (IrcUserRegistrationInfo)regInfo;
                    SendMessageNick(userRegInfo.NickName);
                    SendMessageUser(userRegInfo.UserName, GetNumericUserMode(userRegInfo.UserModes),
                                    userRegInfo.RealName);

                    this.localUser = new IrcLocalUser(userRegInfo.NickName, userRegInfo.UserName, userRegInfo.RealName,
                                                      userRegInfo.UserModes);
                }

                this.users.Add(this.localUser);
            }
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
                DisconnectInternal();
            }
        }

        private void HandleClientClosed()
        {
            Debug.WriteLine("Disconnected from server.");

            this.disconnectedEvent.Set();
            ResetState();
        }

        public override void Disconnect()
        {
            CheckDisposed();
            DisconnectInternal();
        }

        /// <summary>
        /// Disconnects from the server. Does nothing if client object has already been disposed.
        /// </summary>
        protected void DisconnectInternal()
        {
            if (this.client != null && this.client.Client.Connected)
            {
                try
                {
                    this.client.Client.Disconnect(true);
                }
                catch (SocketException exSocket)
                {
                    if (exSocket.SocketErrorCode != SocketError.NotConnected)
                        throw;
                }
            }

            if (this.canDisconnect)
            {
                this.canDisconnect = false;
                OnDisconnected(new EventArgs());
                HandleClientClosed();
            }
        }

        private void ReadLoop()
        {
            try
            {
                // Read each message from network stream, one per line, until client is disconnected.
                while (this.client != null && this.client.Connected)
                {
                    var line = this.reader.ReadLine();
                    if (line == null)
                        break;

#if DEBUG
                    Debug.WriteLine(string.Format("{0:HH:mm:ss} ({1}) >>> {2}", DateTime.Now, this.ClientId, line));
#endif

                    string prefix = null;
                    string lineAfterPrefix = null;

                    // Extract prefix from message, if it contains one.
                    if (line[0] == ':')
                    {
                        var firstSpaceIndex = line.IndexOf(' ');
                        prefix = line.Substring(1, firstSpaceIndex - 1);
                        lineAfterPrefix = line.Substring(firstSpaceIndex + 1);
                    }

                    // Extract command from message.
                    var command = lineAfterPrefix.Substring(0, lineAfterPrefix.IndexOf(' '));
                    var paramsLine = lineAfterPrefix.Substring(command.Length + 1);

                    // Extract parameters from message.
                    // Each parameter is separated by a single space, except the last one, which may contain spaces if it is prefixed by a colon.
                    var parameters = new string[maxParamsCount];
                    int paramStartIndex, paramEndIndex = -1;
                    int lineColonIndex = paramsLine.IndexOf(" :");
                    if (lineColonIndex == -1 && !paramsLine.StartsWith(":"))
                        lineColonIndex = paramsLine.Length;
                    for (int i = 0; i < parameters.Length; i++)
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

                    var message = new IrcMessage(this, prefix, command, parameters);
                    ReadMessage(message, line);
                }
            }
            catch (IOException exIO)
            {
                var socketException = exIO.InnerException as SocketException;
                if (socketException != null)
                {
                    switch (socketException.SocketErrorCode)
                    {
                    case SocketError.Interrupted:
                    case SocketError.NotConnected:
                        return;
                    }
                }

                OnError(new IrcErrorEventArgs(exIO));
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                DisconnectInternal();
            }
        }

        private void WriteLoop()
        {
            try
            {
                // Continuously write messages in send queue to network stream, within given rate limit.
                while (this.client != null && this.client.Connected)
                {
                    // Send messages in send queue until flood preventer indicates to stop.
                    while (this.messageSendQueue.Count > 0)
                    {
                        if (this.floodPreventer != null && !this.floodPreventer.CanSendMessage())
                            break;

                        var line = this.messageSendQueue.Dequeue();
                        this.writer.Write(line);

                        if (this.floodPreventer != null)
                            this.floodPreventer.HandleMessageSent();

#if DEBUG
                        Debug.WriteLine(string.Format("{0:HH:mm:ss} ({1}) <<< {2}", DateTime.Now, this.ClientId, line));
#endif
                    }
                    this.writer.Flush();

                    Thread.Sleep(50);
                }
            }
            catch (IOException exIO)
            {
                var socketException = exIO.InnerException as SocketException;
                if (socketException != null)
                {
                    switch (socketException.SocketErrorCode)
                    {
                    case SocketError.Interrupted:
                    case SocketError.NotConnected:
                        return;
                    }
                }

                OnError(new IrcErrorEventArgs(exIO));
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                DisconnectInternal();
            }
        }

        protected override void WriteMessage(string line)
        {
            CheckDisposed();

            Debug.Assert(line != null);
            messageSendQueue.Enqueue(line);
        }

        public override void Quit(int timeout, string comment = null)
        {
            CheckDisposed();
            base.Quit(timeout, comment);
            if (timeout != 0 && !this.disconnectedEvent.WaitOne(timeout))
                Disconnect();
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            if (this.client.Connected)
                return string.Format("{0}@{1}", this.localUser.UserName,
                                     this.ServerName ?? this.client.Client.RemoteEndPoint.ToString());
            else
                return "(Not connected)";
        }

    }
}

