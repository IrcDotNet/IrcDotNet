using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

#if !SILVERLIGHT
using System.Net.Security;
#endif

namespace IrcDotNet
{
    /// <inheritdoc/>
    public class StandardIrcClient : IrcClient
    {
        // Minimum duration of time to wait between sending successive raw messages.
        private const long minimumSendWaitTime = 50;

        // Size of buffer for data received by socket, in bytes.
        private const int socketReceiveBufferSize = 0xFFFF;

        // Queue of pending messages and their tokens to be sent when ready.
        private Queue<Tuple<string, object>> messageSendQueue;

        // Network (TCP) I/O.
        private Socket socket;
        private CircularBufferStream receiveStream;
        private Stream networkStream;
        private Stream dataStream;
        private StreamReader dataStreamReader;
        private SafeLineReader dataStreamLineReader;
        private Timer sendTimer;
        private AutoResetEvent disconnectedEvent;

        public StandardIrcClient()
            : base()
        {
            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            this.sendTimer = new Timer(new TimerCallback(WritePendingMessages), null,
                                       Timeout.Infinite, Timeout.Infinite);
            this.disconnectedEvent = new AutoResetEvent(false);

            this.messageSendQueue = new Queue<Tuple<string, object>>();
        }

        public override bool IsConnected {
            get
            {
                CheckDisposed();
                return this.socket != null && this.socket.Connected;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (this.socket != null)
                {
                    this.socket.Dispose();
                    this.socket = null;

                    HandleClientDisconnected();
                }
                if (this.networkStream != null)
                {
                    this.networkStream.Dispose();
                    this.networkStream = null;
                }
                if (this.receiveStream != null)
                {
                    this.receiveStream.Dispose();
                    this.receiveStream = null;
                }
                if (this.dataStream != null)
                {
                    this.dataStream.Dispose();
                    this.dataStream = null;
                }
                if (this.dataStreamReader != null)
                {
                    this.dataStreamReader.Dispose();
                    this.dataStreamReader = null;
                }
                if (this.sendTimer != null)
                {
                    this.sendTimer.Dispose();
                    this.sendTimer = null;
                }
                if (this.disconnectedEvent != null)
                {
                    this.disconnectedEvent.Close();
                    this.disconnectedEvent = null;
                }
            }
        }

        protected override void WriteMessage(string line, object token)
        {
            // Add message line to send queue.
            messageSendQueue.Enqueue(Tuple.Create(line + Environment.NewLine, token));
        }

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)"/>
        /// <summary>
        /// Connects to a server using the specified URL and user information.
        /// </summary>
        public void Connect(Uri url, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            // Check URL scheme and decide whether to use SSL.
            bool useSsl;
            if (url.Scheme == "irc")
                useSsl = false;
            else if (url.Scheme == "ircs")
                useSsl = true;
            else
                throw new ArgumentException(string.Format(Properties.Resources.MessageInvalidUrlScheme,
                                                          url.Scheme), "url");

            Connect(url.Host, url.Port == -1 ? DefaultPort : url.Port, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)"/>
        public void Connect(string hostName, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(hostName, DefaultPort, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(EndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="hostName">The name of the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(string hostName, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(new DnsEndPoint(hostName, port), useSsl, registrationInfo, hostName);
        }

        /// <inheritdoc cref="Connect(IPAddress, int, bool, IrcRegistrationInfo)"/>
        public void Connect(IPAddress address, bool useSsl, IrcRegistrationInfo registrationInfo, string targetHost="")
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(address, DefaultPort, useSsl, registrationInfo, targetHost);
        }

        /// <inheritdoc cref="Connect(EndPoint, bool, IrcRegistrationInfo)"/>
        /// <param name="address">An IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(IPAddress address, int port, bool useSsl, IrcRegistrationInfo registrationInfo, string targetHost="")
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(new IPEndPoint(address, port), useSsl, registrationInfo, targetHost);
        }

        /// <summary>
        /// Connects asynchronously to the specified server.
        /// </summary>
        /// <param name="remoteEndPoint">The network endpoint (IP address and port) of the server to which to connect.
        /// </param>
        /// <param name="useSsl"><see langword="true"/> to connect to the server via SSL; <see langword="false"/>,
        /// otherwise</param>
        /// <param name="registrationInfo">The information used for registering the client.
        /// The type of the object may be either <see cref="IrcUserRegistrationInfo"/> or
        /// <see cref="IrcServiceRegistrationInfo"/>.</param>
        /// <exception cref="ArgumentNullException"><paramref name="registrationInfo"/> is <see langword="null"/>.
        /// </exception>
        /// <exception cref="ArgumentException"><paramref name="registrationInfo"/> does not specify valid registration
        /// information.</exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public void Connect(EndPoint remoteEndPoint, bool useSsl, IrcRegistrationInfo registrationInfo, string targetHost="")
        {
            Connect(registrationInfo);
            // Connect socket to remote host.
            ConnectAsync(remoteEndPoint, Tuple.Create(useSsl, targetHost, registrationInfo));

            HandleClientConnecting();
        }

        public override void Quit(int timeout, string comment)
        {
            base.Quit(timeout, comment);
            if (!this.disconnectedEvent.WaitOne(timeout))
                Disconnect();
        }

        protected override void ResetState()
        {
            base.ResetState();

            // Reset network I/O objects.
            if (this.networkStream != null)
                this.networkStream.Dispose();
            if (this.receiveStream != null)
                this.receiveStream.Dispose();
            if (this.dataStream != null)
                this.dataStream.Dispose();
            if (this.dataStreamReader != null)
                this.dataStreamReader = null;
        }

        private void WritePendingMessages(object state)
        {
            try
            {
                // Send pending messages in queue until flood preventer indicates to stop.
                long sendDelay = 0;

                while (this.messageSendQueue.Count > 0)
                {
                    Debug.Assert(this.messageSendQueue.Count < 100);
                    // Check that flood preventer currently permits sending of messages.
                    if (FloodPreventer != null)
                    {
                        sendDelay = FloodPreventer.GetSendDelay();
                        if (sendDelay > 0)
                            break;
                    }

                    // Send next message in queue.
                    var message = this.messageSendQueue.Dequeue();
                    var line = message.Item1;
                    var token = message.Item2;
                    var lineBuffer = TextEncoding.GetBytes(line);
                    SendAsync(lineBuffer, token);

                    // Tell flood preventer mechanism that message has just been sent.
                    if (FloodPreventer != null)
                        FloodPreventer.HandleMessageSent();
                }

                // Make timer fire when next message in send queue should be written.
                this.sendTimer.Change(Math.Max(sendDelay, minimumSendWaitTime), Timeout.Infinite);
            }
            catch (SocketException exSocket)
            {
                HandleSocketError(exSocket);
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
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

        public override void Disconnect()
        {
            base.Disconnect();

            DisconnectAsync();
        }

        private void SendAsync(byte[] buffer, object token = null)
        {
            SendAsync(buffer, 0, buffer.Length, token);
        }

        private void SendAsync(byte[] buffer, int offset, int count, object token = null)
        {
            // Write data from buffer to socket asynchronously.
            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(buffer, offset, count);
            sendEventArgs.UserToken = token;
            //sendEventArgs.Completed += SendCompleted;

            this.networkStream.BeginWrite(buffer, offset, count,
                SendCompleted, sendEventArgs);
        }

        private void SendCompleted(IAsyncResult ar)
        {
            var e = (SocketAsyncEventArgs)(ar.AsyncState);
            try
            {
                this.networkStream.EndWrite(ar);

                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                // Handle sent IRC message.
                Debug.Assert(e.UserToken != null);
                var messageSentEventArgs = (IrcRawMessageEventArgs)e.UserToken;
                OnRawMessageSent(messageSentEventArgs);

#if DEBUG
                DebugUtilities.WriteIrcRawLine(this, "<<< " + messageSentEventArgs.RawContent);
#endif
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                e.Dispose();
            }
        }

        byte[] receiveAsyncBuffer;
        private void ReceiveAsync()
        {
            // Read data received from socket to buffer asynchronously.
            var receiveEventArgs = new SocketAsyncEventArgs();
            Debug.Assert(this.receiveStream.Buffer.Length - (int)this.receiveStream.WritePosition > 0);
            //receiveEventArgs.SetBuffer(this.receiveStream.Buffer, (int)this.receiveStream.WritePosition,
            //                           this.receiveStream.Buffer.Length - (int)this.receiveStream.WritePosition);
            //receiveEventArgs.Completed += ReceiveCompleted;

            if (receiveAsyncBuffer == null)
            {
                receiveAsyncBuffer = new byte[1024];
            }

            this.networkStream.BeginRead(receiveAsyncBuffer, 0,
                receiveAsyncBuffer.Length, ReceiveCompleted, receiveEventArgs);
        }

        private void ReceiveCompleted(IAsyncResult ar)
        {
            var e = (SocketAsyncEventArgs)(ar.AsyncState);
            try
            {
                var bytesTransferred = this.networkStream.EndRead(ar);

                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                // Check if remote host has closed connection.
                if (bytesTransferred == 0)
                {
                    Disconnect();
                    return;
                }

                // Take the data we just received and write it to the receiveStream
                this.receiveStream.Write(receiveAsyncBuffer, 0, bytesTransferred);
                this.dataStreamReader.DiscardBufferedData();

                // Read each terminated line of characters from data stream.
                while (true)
                {
                    // Read next line from data stream.
                    var line = this.dataStreamLineReader.ReadLine();
                    if (line == null)
                        break;
                    if (line.Length == 0)
                        continue;

                    ParseMessage(line);
                }

                // Continue reading data from socket.
                ReceiveAsync();
            }
            catch (SocketException exSocket)
            {
                HandleSocketError(exSocket);
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                e.Dispose();
            }
        }

        private void ConnectAsync(EndPoint remoteEndPoint, object token = null)
        {
            // Connect socket to remote endpoint asynchronously.
            var connectEventArgs = new SocketAsyncEventArgs();
            connectEventArgs.RemoteEndPoint = remoteEndPoint;
            connectEventArgs.UserToken = token;
            connectEventArgs.Completed += ConnectCompleted;

            if (!this.socket.ConnectAsync(connectEventArgs))
                ((EventHandler<SocketAsyncEventArgs>)ConnectCompleted).BeginInvoke(
                    this.socket, connectEventArgs, null, null);
        }

        private void ConnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                Debug.Assert(e.UserToken != null);
                var token = (Tuple<bool, string, IrcRegistrationInfo>)e.UserToken;

                // Create stream for received data. Use SSL stream on top of network stream, if specified.
                this.receiveStream = new CircularBufferStream(socketReceiveBufferSize);
                this.dataStream = this.receiveStream;
#if SILVERLIGHT
                this.networkStream = new NetworkStream(this.socket);
#else
                this.networkStream = GetNetworkStream(token.Item1, token.Item2);
#endif
                this.dataStreamReader = new StreamReader(this.dataStream, TextEncoding);
                this.dataStreamLineReader = new SafeLineReader(this.dataStreamReader);

                // Start sending and receiving data to/from server.
                this.sendTimer.Change(0, Timeout.Infinite);
                ReceiveAsync();

                HandleClientConnected(token.Item3);
            }
            catch (SocketException exSocket)
            {
                HandleSocketError(exSocket);
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnConnectFailed(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                e.Dispose();
            }
        }

        private void DisconnectAsync()
        {
            // Connect socket to remote endpoint asynchronously.
            var disconnectEventArgs = new SocketAsyncEventArgs();
            disconnectEventArgs.Completed += DisconnectCompleted;

#if SILVERLIGHT
            this.socket.Shutdown(SocketShutdown.Both);
            disconnectEventArgs.SocketError = SocketError.Success;
            ((EventHandler<SocketAsyncEventArgs>)DisconnectCompleted).BeginInvoke(
                this.socket, disconnectEventArgs, null, null);
#else
            disconnectEventArgs.DisconnectReuseSocket = true;
            if (!this.socket.DisconnectAsync(disconnectEventArgs))
                ((EventHandler<SocketAsyncEventArgs>)DisconnectCompleted).BeginInvoke(
                    this.socket, disconnectEventArgs, null, null);
#endif
        }

        private void DisconnectCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                HandleClientDisconnected();
            }
            catch (SocketException exSocket)
            {
                HandleSocketError(exSocket);
            }
            catch (ObjectDisposedException)
            {
                // Ignore.
            }
#if !DEBUG
            catch (Exception ex)
            {
                OnError(new IrcErrorEventArgs(ex));
            }
#endif
            finally
            {
                e.Dispose();
            }
        }

#if !SILVERLIGHT

        private Stream GetNetworkStream(bool useSsl, string targetHost)
        {
            var socketStream = new NetworkStream(this.socket);
            if (useSsl)
            {
                // Create SSL stream over network stream to use for data transmission.
                var sslStream = new SslStream(socketStream, true,
                                              new RemoteCertificateValidationCallback(SslUserCertificateValidationCallback));
                sslStream.AuthenticateAsClient(targetHost);
                Debug.Assert(sslStream.IsAuthenticated);
                return sslStream;
            }
            else
            {
                // Use network stream directly for data transmission.
                return socketStream;
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

#endif

        protected override void HandleClientConnected(IrcRegistrationInfo regInfo)
        {
            DebugUtilities.WriteEvent(string.Format("Connected to server at '{0}'.",
                ((IPEndPoint)this.socket.RemoteEndPoint).Address));

            base.HandleClientConnected(regInfo);
        }

        protected override void HandleClientDisconnected()
        {
            // Ensure that client has not already handled disconnection.
            if (this.disconnectedEvent.WaitOne(0))
                return;

            DebugUtilities.WriteEvent("Disconnected from server.");

            // Stop sending messages immediately.
            this.sendTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Set that client has disconnected.
            this.disconnectedEvent.Set();

            base.HandleClientDisconnected();
        }

        private void HandleSocketError(SocketError error)
        {
            HandleSocketError(new SocketException((int)error));
        }

        private void HandleSocketError(SocketException exception)
        {
            switch (exception.SocketErrorCode)
            {
            case SocketError.NotConnected:
            case SocketError.ConnectionReset:
                HandleClientDisconnected();
                return;
            default:
                OnError(new IrcErrorEventArgs(exception));
                return;
            }
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            if (!this.IsDisposed && this.IsConnected)
                return string.Format("{0}@{1}", LocalUser.UserName,
                                     this.ServerName ?? this.socket.RemoteEndPoint.ToString());
            else
                return "(Not connected)";
        }
    }
}

