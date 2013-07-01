using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace IrcDotNet.Samples.Common
{
    // Provides core functionality for an IRC bot that operates via multiple clients.
    public abstract class IrcBot : IDisposable
    {
        private const int clientQuitTimeout = 1000;

        // Regex for splitting space-separated list of command parts until first parameter that begins with '/'.
        private static readonly Regex commandPartsSplitRegex = new Regex("(?<! /.*) ", RegexOptions.None);

        // Dictionary of all chat command processors, keyed by name.
        private IDictionary<string, ChatCommandProcessor> chatCommandProcessors;

        // Internal and exposable collection of all clients that communicate individually with servers.
        private Collection<IrcClient> allClients;
        private ReadOnlyCollection<IrcClient> allClientsReadOnly;

        // Dictionary of all command processors, keyed by name.
        private IDictionary<string, CommandProcessor> commandProcessors;

        // True if the read loop is currently active, false if ready to terminate.
        private bool isRunning;

        private bool isDisposed = false;

        public IrcBot()
        {
            this.isRunning = false;
            this.commandProcessors = new Dictionary<string, CommandProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
            InitializeCommandProcessors();

            this.allClients = new Collection<IrcClient>();
            this.allClientsReadOnly = new ReadOnlyCollection<IrcClient>(this.allClients);
            this.chatCommandProcessors = new Dictionary<string, ChatCommandProcessor>(
                StringComparer.InvariantCultureIgnoreCase);
            InitializeChatCommandProcessors();
        }

        ~IrcBot()
        {
            Dispose(false);
        }

        public virtual string QuitMessage
        {
            get { return null; }
        }

        protected IDictionary<string, ChatCommandProcessor> ChatCommandProcessors
        {
            get { return this.chatCommandProcessors; }
        }

        public ReadOnlyCollection<IrcClient> Clients
        {
            get { return this.allClientsReadOnly; }
        }

        protected IDictionary<string, CommandProcessor> CommandProcessors
        {
            get { return this.commandProcessors; }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected void Dispose(bool disposing)
        {
            if (!this.isDisposed)
            {
                if (disposing)
                {
                    // Disconnect each client gracefully.
                    foreach (var client in allClients)
                    {
                        if (client != null)
                        {
                            client.Quit(clientQuitTimeout, this.QuitMessage);
                            client.Dispose();
                        }
                    }
                }
            }
            this.isDisposed = true;
        }

        public virtual void Run()
        {
            // Read commands from stdin until bot terminates.
            this.isRunning = true;
            while (this.isRunning)
            {
                Console.Write("> ");
                var line = Console.ReadLine();
                if (line == null)
                    break;
                if (line.Length == 0)
                    continue;

                var parts = line.Split(' ');
                var command = parts[0].ToLower();
                var parameters = parts.Skip(1).ToArray();
                ReadCommand(command, parameters);
            }
        }

        public void Stop()
        {
            this.isRunning = false;
        }

        protected abstract void InitializeCommandProcessors();

        private void ReadCommand(string command, IList<string> parameters)
        {
            CommandProcessor processor;
            if (this.commandProcessors.TryGetValue(command, out processor))
            {
                try
                {
                    processor(command, parameters);
                }
                catch (Exception ex)
                {
                    ConsoleUtilities.WriteError("Error: {0}", ex.Message);
                }
            }
            else
            {
                ConsoleUtilities.WriteError("Command '{0}' not recognized.", command);
            }
        }

        protected void Connect(string server, IrcRegistrationInfo registrationInfo)
        {
            // Create new IRC client and connect to given server.
            var client = new IrcClient();
            client.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            client.Connected += IrcClient_Connected;
            client.Disconnected += IrcClient_Disconnected;
            client.Registered += IrcClient_Registered;

            // Wait until connection has succeeded or timed out.
            using (var connectedEvent = new ManualResetEventSlim(false))
            {
                client.Connected += (sender2, e2) => connectedEvent.Set();
                client.Connect(server, false, registrationInfo);
                if (!connectedEvent.Wait(10000))
                {
                    client.Dispose();
                    ConsoleUtilities.WriteError("Connection to '{0}' timed out.", server);
                    return;
                }
            }

            // Add new client to collection.
            this.allClients.Add(client);

            Console.Out.WriteLine("Now connected to '{0}'.", server);
        }

        public void Disconnect(string server)
        {
            // Disconnect IRC client that is connected to given server.
            var client = GetClientFromServerNameMask(server);
            var serverName = client.ServerName;
            client.Quit(clientQuitTimeout, this.QuitMessage);
            client.Dispose();

            // Remove client from connection.
            this.allClients.Remove(client);

            Console.Out.WriteLine("Disconnected from '{0}'.", serverName);
        }

        protected abstract void InitializeChatCommandProcessors();

        private bool ReadChatCommand(IrcClient client, IrcMessageEventArgs eventArgs)
        {
            // Check if given message represents chat command.
            var line = eventArgs.Text;
            if (line.Length > 1 && line.StartsWith("."))
            {
                // Process command.
                var parts = commandPartsSplitRegex.Split(line.Substring(1)).Select(p => p.TrimStart('/')).ToArray();
                var command = parts.First();
                var parameters = parts.Skip(1).ToArray();
                ReadChatCommand(client, eventArgs.Source, eventArgs.Targets, command, parameters);
                return true;
            }
            return false;
        }

        private void ReadChatCommand(IrcClient client, IIrcMessageSource source, IList<IIrcMessageTarget> targets,
            string command, string[] parameters)
        {
            var defaultReplyTarget = GetDefaultReplyTarget(client, source, targets);

            ChatCommandProcessor processor;
            if (this.chatCommandProcessors.TryGetValue(command, out processor))
            {
                try
                {
                    processor(client, source, targets, command, parameters);
                }
                catch (InvalidCommandParametersException exInvalidCommandParameters)
                {
                    client.LocalUser.SendNotice(defaultReplyTarget,
                        exInvalidCommandParameters.GetMessage(command));
                }
                catch (Exception ex)
                {
                    if (source is IIrcMessageTarget)
                    {
                        client.LocalUser.SendNotice(defaultReplyTarget,
                            "Error processing '{0}' command: {1}", command, ex.Message);
                    }
                }
            }
            else
            {
                if (source is IIrcMessageTarget)
                {
                    client.LocalUser.SendNotice(defaultReplyTarget, "Command '{0}' not recognized.", command);
                }
            }
        }

        protected abstract void OnClientConnect(IrcClient client);

        protected abstract void OnClientDisconnect(IrcClient client);

        protected abstract void OnClientRegistered(IrcClient client);

        protected abstract void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e);

        protected abstract void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e);

        protected abstract void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e);

        protected abstract void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e);

        protected abstract void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e);

        protected abstract void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e);

        protected abstract void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e);

        protected abstract void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e);

        #region IRC Client Event Handlers

        private void IrcClient_Connected(object sender, EventArgs e)
        {
            var client = (IrcClient)sender;

            OnClientConnect(client);
        }

        private void IrcClient_Disconnected(object sender, EventArgs e)
        {
            var client = (IrcClient)sender;

            OnClientDisconnect(client);
        }

        private void IrcClient_Registered(object sender, EventArgs e)
        {
            var client = (IrcClient)sender;

            client.LocalUser.NoticeReceived += IrcClient_LocalUser_NoticeReceived;
            client.LocalUser.MessageReceived += IrcClient_LocalUser_MessageReceived;
            client.LocalUser.JoinedChannel += IrcClient_LocalUser_JoinedChannel;
            client.LocalUser.LeftChannel += IrcClient_LocalUser_LeftChannel;

            Console.Beep();

            OnClientRegistered(client);
        }

        private void IrcClient_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;

            OnLocalUserNoticeReceived(localUser, e);
        }

        private void IrcClient_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;

            if (e.Source is IrcUser)
            {
                // Read message and process if it is chat command.
                if (ReadChatCommand(localUser.Client, e))
                    return;
            }

            OnLocalUserMessageReceived(localUser, e);
        }

        private void IrcClient_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;

            e.Channel.UserJoined += IrcClient_Channel_UserJoined;
            e.Channel.UserLeft += IrcClient_Channel_UserLeft;
            e.Channel.MessageReceived += IrcClient_Channel_MessageReceived;
            e.Channel.NoticeReceived += IrcClient_Channel_NoticeReceived;

            OnLocalUserJoinedChannel(localUser, e);
        }

        private void IrcClient_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;

            e.Channel.UserJoined -= IrcClient_Channel_UserJoined;
            e.Channel.UserLeft -= IrcClient_Channel_UserLeft;
            e.Channel.MessageReceived -= IrcClient_Channel_MessageReceived;
            e.Channel.NoticeReceived -= IrcClient_Channel_NoticeReceived;

            OnLocalUserLeftChannel(localUser, e);
        }

        private void IrcClient_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            var channel = (IrcChannel)sender;

            OnChannelUserLeft(channel, e);
        }

        private void IrcClient_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
        {
            var channel = (IrcChannel)sender;

            OnChannelUserJoined(channel, e);
        }

        private void IrcClient_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            var channel = (IrcChannel)sender;

            OnChannelNoticeReceived(channel, e);
        }

        private void IrcClient_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            var channel = (IrcChannel)sender;

            if (e.Source is IrcUser)
            {
                // Read message and process if it is chat command.
                if (ReadChatCommand(channel.Client, e))
                    return;
            }

            OnChannelMessageReceived(channel, e);
        }

        #endregion

        protected IList<IIrcMessageTarget> GetDefaultReplyTarget(IrcClient client, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets)
        {
            if (targets.Contains(client.LocalUser) && source is IIrcMessageTarget)
                return new[] { (IIrcMessageTarget)source };
            else
                return targets;
        }

        protected IrcClient GetClientFromServerNameMask(string serverNameMask)
        {
            var client = this.Clients.SingleOrDefault(c => c.ServerName != null &&
                Regex.IsMatch(c.ServerName, serverNameMask, RegexOptions.IgnoreCase));
            if (client == null)
            {
                throw new IrcBotException(IrcBotExceptionType.NoConnection,
                    string.Format(Properties.Resources.MessageBotNoConnection, serverNameMask));
            }
            return client;
        }

        protected delegate void ChatCommandProcessor(IrcClient client, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string command, IList<string> parameters);

        protected delegate void CommandProcessor(string command, IList<string> parameters);
    }
}
