using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using IrcDotNet;
using IrcDotNet.Samples.Common;
using TweetSharp;
using TweetSharp.Twitter;
using TweetSharp.Twitter.Fluent;

namespace TwitterBot
{
    public class TwitterBot : IrcBot
    {
        public TwitterBot()
            : base()
        {
        }

        protected override void OnClientConnect(IrcClient client)
        {
            throw new NotImplementedException();
        }

        protected override void OnClientDisconnect(IrcClient client)
        {
            throw new NotImplementedException();
        }

        protected override void OnClientRegistered(IrcClient client)
        {
            throw new NotImplementedException();
        }

        protected override void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected override void InitializeChatCommandProcessors()
        {
            //
        }

        #region Chat Command Processors

        //

        #endregion

        protected override void InitializeCommandProcessors()
        {
            this.CommandProcessors.Add("exit", ProcessCommandExit);
            this.CommandProcessors.Add("connect", ProcessCommandConnect);
            this.CommandProcessors.Add("c", ProcessCommandConnect);
            this.CommandProcessors.Add("disconnect", ProcessCommandDisconnect);
            this.CommandProcessors.Add("d", ProcessCommandDisconnect);
        }

        #region Command Processors

        private void ProcessCommandExit(string command, IList<string> parameters)
        {
            Stop();
        }

        private void ProcessCommandConnect(string command, IList<string> parameters)
        {
            if (parameters.Count < 1)
                throw new ArgumentException(Properties.Resources.ErrorMessageNotEnoughArgs);

            Connect(parameters[0], new IrcUserRegistrationInfo()
            {
                NickName = "TwitterBot",
                UserName = "TwitterBot",
                RealName = "Twitter Bot"
            });
        }

        private void ProcessCommandDisconnect(string command, IList<string> parameters)
        {
            if (parameters.Count < 1)
                throw new ArgumentException(Properties.Resources.ErrorMessageNotEnoughArgs);

            Disconnect(parameters[0]);
        }

        #endregion
    }
}
