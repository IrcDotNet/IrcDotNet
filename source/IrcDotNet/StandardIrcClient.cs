using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using IrcDotNet.Properties;
#if !SILVERLIGHT
using System.Net.Security;

#endif

namespace IrcDotNet
{
    /// <inheritdoc />
    public class StandardIrcClient : IrcClient
    {
        // Minimum duration of time to wait between sending successive raw messages.
        private const long minimumSendWaitTime = 50;

        // Size of buffer for data received by socket, in bytes.
        private const int socketReceiveBufferSize = 0xFFFF;
        private Stream dataStream;
        private SafeLineReader dataStreamLineReader;
        private StreamReader dataStreamReader;
        private AutoResetEvent disconnectedEvent;

        // Queue of pending messages and their tokens to be sent when ready.
        private readonly Queue<Tuple<string, object>> messageSendQueue;
        private CircularBufferStream receiveStream;
        private Timer sendTimer;

        // Network (TCP) I/O.
        private TcpClient tcpClient;

        public StandardIrcClient()
        {
            tcpClient = new TcpClient();
            sendTimer = new Timer(WritePendingMessages, null,
                Timeout.Infinite, Timeout.Infinite);
            disconnectedEvent = new AutoResetEvent(false);

            messageSendQueue = new Queue<Tuple<string, object>>();
        }

        public override bool IsConnected
        {
            get
            {
                CheckDisposed();
                return tcpClient != null && tcpClient.Connected;
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                if (tcpClient != null)
                {
                    tcpClient.Dispose();
                    tcpClient = null;

                    HandleClientDisconnected();
                }
                if (receiveStream != null)
                {
                    receiveStream.Dispose();
                    receiveStream = null;
                }
                if (dataStream != null)
                {
                    dataStream.Dispose();
                    dataStream = null;
                }
                if (dataStreamReader != null)
                {
                    dataStreamReader.Dispose();
                    dataStreamReader = null;
                }
                if (sendTimer != null)
                {
                    sendTimer.Dispose();
                    sendTimer = null;
                }
                if (disconnectedEvent != null)
                {
                    disconnectedEvent.Dispose();
                    disconnectedEvent = null;
                }
            }
        }

        protected override void WriteMessage(string line, object token)
        {
            // Add message line to send queue.
            messageSendQueue.Enqueue(Tuple.Create(line + Environment.NewLine, token));
        }

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)" />
        /// <summary>
        ///     Connects to a server using the specified URL and user information.
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
                throw new ArgumentException(string.Format(Resources.MessageInvalidUrlScheme,
                    url.Scheme), "url");

            Connect(url.Host, url.Port == -1 ? DefaultPort : url.Port, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(string, int, bool, IrcRegistrationInfo)" />
        public void Connect(string hostName, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(hostName, DefaultPort, useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)" />
        /// <param name="hostName">The name of the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(string hostName, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            var dnsTask = Dns.GetHostAddressesAsync(hostName);
            var addresses = dnsTask.Result;

            Connect(new IPEndPoint(addresses[0], port), useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPAddress, int, bool, IrcRegistrationInfo)" />
        public void Connect(IPAddress address, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(new IPEndPoint(address, DefaultPort), useSsl, registrationInfo);
        }

        /// <inheritdoc cref="Connect(IPEndPoint, bool, IrcRegistrationInfo)" />
        /// <param name="address">An IP addresses that designates the remote host.</param>
        /// <param name="port">The port number of the remote host.</param>
        public void Connect(IPEndPoint address, int port, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(new IPEndPoint(address.Address, port), useSsl, registrationInfo);
        }

        public virtual void Connect(EndPoint endpoint, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            CheckDisposed();

            if (registrationInfo == null)
                throw new ArgumentNullException("registrationInfo");

            Connect(new IPEndPoint(((IPEndPoint)endpoint).Address, DefaultPort), useSsl, registrationInfo);
        }

        /// <summary>
        ///     Connects asynchronously to the specified server.
        /// </summary>
        /// <param name="remoteEndPoint">
        ///     The network endpoint (IP address and port) of the server to which to connect.
        /// </param>
        /// <param name="useSsl">
        ///     <see langword="true" /> to connect to the server via SSL; <see langword="false" />,
        ///     otherwise
        /// </param>
        /// <param name="registrationInfo">
        ///     The information used for registering the client.
        ///     The type of the object may be either <see cref="IrcUserRegistrationInfo" /> or
        ///     <see cref="IrcServiceRegistrationInfo" />.
        /// </param>
        /// <exception cref="ArgumentNullException">
        ///     <paramref name="registrationInfo" /> is <see langword="null" />.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     <paramref name="registrationInfo" /> does not specify valid registration
        ///     information.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        public virtual void Connect(IPEndPoint remoteEndPoint, bool useSsl, IrcRegistrationInfo registrationInfo)
        {
            Connect(registrationInfo);
            // Connect socket to remote host.
            ConnectAsync(remoteEndPoint, Tuple.Create(useSsl, string.Empty, registrationInfo));

            HandleClientConnecting();
        }

        public override void Quit(int timeout, string comment)
        {
            base.Quit(timeout, comment);
            if (!disconnectedEvent.WaitOne(timeout))
                Disconnect();
        }

        protected override void ResetState()
        {
            base.ResetState();

            // Reset network I/O objects.
            if (receiveStream != null)
                receiveStream.Dispose();
            if (dataStream != null)
                dataStream.Dispose();
            if (dataStreamReader != null)
                dataStreamReader = null;
        }

        private void WritePendingMessages(object state)
        {
            try
            {
                // Send pending messages in queue until flood preventer indicates to stop.
                long sendDelay = 0;

                while (messageSendQueue.Count > 0)
                {
                    Debug.Assert(messageSendQueue.Count < 100);
                    // Check that flood preventer currently permits sending of messages.
                    if (FloodPreventer != null)
                    {
                        sendDelay = FloodPreventer.GetSendDelay();
                        if (sendDelay > 0)
                            break;
                    }

                    // Send next message in queue.
                    var message = messageSendQueue.Dequeue();
                    var line = message.Item1;
                    var token = message.Item2;
                    var lineBuffer = TextEncoding.GetBytes(line);
                    SendAsync(lineBuffer, token);

                    // Tell flood preventer mechanism that message has just been sent.
                    if (FloodPreventer != null)
                        FloodPreventer.HandleMessageSent();
                }

                // Make timer fire when next message in send queue should be written.
                sendTimer.Change((int)Math.Max(sendDelay, minimumSendWaitTime), Timeout.Infinite);
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

        private async void SendAsync(byte[] buffer, int offset, int count, object token = null)
        {
            // Write data from buffer to socket asynchronously.
            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.UserToken = token;
            sendEventArgs.Completed += SendCompleted;

            try
            {
                await dataStream.WriteAsync(buffer, offset, count);
                sendEventArgs.SocketError = SocketError.Success;
            }
            catch (SocketException e)
            {
                sendEventArgs.SocketError = e.SocketErrorCode;
            }
            catch
            {
                sendEventArgs.SocketError = SocketError.ConnectionReset;
            }
            finally
            {
                SendCompleted(tcpClient, sendEventArgs);
            }
        }

        private void SendCompleted(object sender, SocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                // Handle sent IRC message.
                Debug.Assert(e.UserToken != null);
                var messageSentEventArgs = (IrcRawMessageEventArgs) e.UserToken;
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

        private async void ReceiveAsync()
        {
            // Read data received from socket to buffer asynchronously.
            var receiveEventArgs = new SyntheticSocketAsyncEventArgs(); //can't set BytesTransferred on the SocketAsyncEventArgs so use synthetic instead
            Debug.Assert(receiveStream.Buffer.Length - (int)receiveStream.WritePosition > 0);

            try
            {
                var bytesReceived = await dataStream.ReadAsync(receiveStream.Buffer,
                    (int)receiveStream.WritePosition, receiveStream.Buffer.Length - (int)receiveStream.WritePosition);

                receiveEventArgs.SocketError = SocketError.Success;
                receiveEventArgs.BytesTransferred = bytesReceived;

            }
            catch (SocketException e)
            {
                receiveEventArgs.SocketError = e.SocketErrorCode;
            }
            catch
            {
                receiveEventArgs.SocketError = SocketError.ConnectionReset;
            }
            finally
            {
                ReceiveCompleted(tcpClient, receiveEventArgs);
            }
        }

        private void ReceiveCompleted(object sender, SyntheticSocketAsyncEventArgs e)
        {
            try
            {
                if (e.SocketError != SocketError.Success)
                {
                    HandleSocketError(e.SocketError);
                    return;
                }

                // Check if remote host has closed connection.
                if (e.BytesTransferred == 0)
                {
                    Disconnect();
                    return;
                }

                // Indicate that block of data has been read into receive buffer.
                receiveStream.WritePosition += e.BytesTransferred;
                dataStreamReader.DiscardBufferedData();

                // Read each terminated line of characters from data stream.
                while (true)
                {
                    // Read next line from data stream.
                    var line = dataStreamLineReader.ReadLine();
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

        private async void ConnectAsync(IPEndPoint remoteEndPoint, object token = null)
        {
            // Connect socket to remote endpoint asynchronously.
            var connectEventArgs = new SocketAsyncEventArgs();
            connectEventArgs.RemoteEndPoint = remoteEndPoint;
            connectEventArgs.UserToken = token;
            connectEventArgs.Completed += ConnectCompleted;

            try
            {
                await tcpClient.ConnectAsync(remoteEndPoint.Address, remoteEndPoint.Port);
                connectEventArgs.SocketError = SocketError.Success;
            }
            catch (SocketException e)
            {
                connectEventArgs.SocketError = e.SocketErrorCode;
            }
            catch
            {
                connectEventArgs.SocketError = SocketError.ConnectionReset;
            }
            finally
            {
                ConnectCompleted(tcpClient, connectEventArgs);
            }
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
                var token = (Tuple<bool, string, IrcRegistrationInfo>) e.UserToken;

                // Create stream for received data. Use SSL stream on top of network stream, if specified.
                receiveStream = new CircularBufferStream(socketReceiveBufferSize);
#if SILVERLIGHT
                this.dataStream = this.receiveStream;
#else
                dataStream = GetDataStream(token.Item1, token.Item2);
#endif
                dataStreamReader = new StreamReader(receiveStream, TextEncoding);
                dataStreamLineReader = new SafeLineReader(dataStreamReader);

                // Start sending and receiving data to/from server.
                sendTimer.Change(0, Timeout.Infinite);
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
            disconnectEventArgs.SocketError = SocketError.Success;

            tcpClient.Client.Shutdown(SocketShutdown.Both);
            DisconnectCompleted(tcpClient, disconnectEventArgs);
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

        protected override void HandleClientConnected(IrcRegistrationInfo regInfo)
        {
            DebugUtilities.WriteEvent(string.Format("Connected to server at '{0}'.",
                ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address));

            base.HandleClientConnected(regInfo);
        }

        protected override void HandleClientDisconnected()
        {
            // Ensure that client has not already handled disconnection.
            if (disconnectedEvent.WaitOne(0))
                return;

            DebugUtilities.WriteEvent("Disconnected from server.");

            // Stop sending messages immediately.
            sendTimer.Change(Timeout.Infinite, Timeout.Infinite);

            // Set that client has disconnected.
            disconnectedEvent.Set();

            base.HandleClientDisconnected();
        }

        private void HandleSocketError(SocketError error)
        {
            HandleSocketError(new SocketException((int) error));
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
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            if (!IsDisposed && IsConnected)
                return string.Format("{0}@{1}", LocalUser.UserName,
                    ServerName ?? tcpClient.Client.RemoteEndPoint.ToString());
            return "(Not connected)";
        }

#if !SILVERLIGHT

        private Stream GetDataStream(bool useSsl, string targetHost)
        {
            if (useSsl)
            {
                // Create SSL stream over network stream to use for data transmission.
                var sslStream = new SslStream(tcpClient.GetStream(), true,
                    SslUserCertificateValidationCallback);

#if NETSTANDARD1_5
                var authTask = sslStream.AuthenticateAsClientAsync(targetHost);
                authTask.Wait();
#else
                sslStream.AuthenticateAsClient(targetHost);
#endif
                Debug.Assert(sslStream.IsAuthenticated);
                return sslStream;
            }
            // Use network stream directly for data transmission.
            return tcpClient.GetStream();
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
            }
}