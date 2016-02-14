using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet.Samples.Common
{
    // Provides access to basic commands for controlling an IRC bot.
    public abstract class BasicIrcBot : IrcBot
    {
        public BasicIrcBot()
            : base()
        {
        }

        public abstract IrcRegistrationInfo RegistrationInfo
        {
            get;
        }

        protected override void InitializeChatCommandProcessors()
        {
            this.ChatCommandProcessors.Add("help", ProcessChatCommandHelp);
        }

        #region Chat Command Processors

        private void ProcessChatCommandHelp(IrcClient client, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string command, IList<string> parameters)
        {
            if (parameters.Count != 0)
                throw new InvalidCommandParametersException(0);

            // List all commands recognized by this bot.
            var replyTarget = GetDefaultReplyTarget(client, source, targets);
            client.LocalUser.SendMessage(replyTarget, "Commands recognized by bot:");
            client.LocalUser.SendMessage(replyTarget, string.Join(", ",
                this.ChatCommandProcessors.Select(kvPair => kvPair.Key)));
        }

        #endregion

        protected override void InitializeCommandProcessors()
        {
            this.CommandProcessors.Add("exit", ProcessCommandExit);
            this.CommandProcessors.Add("connect", ProcessCommandConnect);
            this.CommandProcessors.Add("c", ProcessCommandConnect);
            this.CommandProcessors.Add("disconnect", ProcessCommandDisconnect);
            this.CommandProcessors.Add("d", ProcessCommandDisconnect);
            this.CommandProcessors.Add("join", ProcessCommandJoin);
            this.CommandProcessors.Add("j", ProcessCommandJoin);
            this.CommandProcessors.Add("leave", ProcessCommandLeave);
            this.CommandProcessors.Add("l", ProcessCommandLeave);
            this.CommandProcessors.Add("list", ProcessCommandList);
        }

        #region Command Processors

        private void ProcessCommandExit(string command, IList<string> parameters)
        {
            Stop();
        }

        private void ProcessCommandConnect(string command, IList<string> parameters)
        {
            if (parameters.Count < 1)
                throw new ArgumentException(Properties.Resources.MessageNotEnoughArgs);

            Connect(parameters[0], this.RegistrationInfo);
        }

        private void ProcessCommandDisconnect(string command, IList<string> parameters)
        {
            if (parameters.Count < 1)
                throw new ArgumentException(Properties.Resources.MessageNotEnoughArgs);

            Disconnect(parameters[0]);
        }

        private void ProcessCommandJoin(string command, IList<string> parameters)
        {
            if (parameters.Count < 2)
                throw new ArgumentException(Properties.Resources.MessageNotEnoughArgs);

            // Join given channel on given server.
            var client = GetClientFromServerNameMask(parameters[0]);
            var channelName = parameters[1];
            client.Channels.Join(channelName);
        }

        private void ProcessCommandLeave(string command, IList<string> parameters)
        {
            if (parameters.Count < 2)
                throw new ArgumentException(Properties.Resources.MessageNotEnoughArgs);

            // Leave given channel on the given server.
            var client = GetClientFromServerNameMask(parameters[0]);
            var channelName = parameters[1];
            client.Channels.Leave(channelName);
        }

        private void ProcessCommandList(string command, IList<string> parameters)
        {
            // List all active server connections and channels of which local user is currently member.
            foreach (var client in this.Clients)
            {
                Console.Out.WriteLine("Server: {0}", client.ServerName ?? "(unknown)");
                foreach (var channel in client.Channels)
                {
                    if (channel.Users.Any(u => u.User == client.LocalUser))
                    {
                        Console.Out.WriteLine(" * {0}", channel.Name);
                    }
                }
            }
        }

        #endregion
    }
}
