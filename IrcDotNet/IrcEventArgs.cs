using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcMessageEventArgs : EventArgs
    {
        public IrcMessageEventArgs(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
            : base()
        {
            if (targets == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            this.Source = source;
            this.Targets = new ReadOnlyCollection<IIrcMessageTarget>(targets);
            this.Text = text;
        }

        public IIrcMessageSource Source
        {
            get;
            private set;
        }

        public IList<IIrcMessageTarget> Targets
        {
            get;
            private set;
        }

        public string Text
        {
            get;
            private set;
        }
    }

    public class IrcChannelUserEventArgs : EventArgs
    {
        public IrcChannelUserEventArgs(IrcChannelUser channelUser)
            : base()
        {
            if (channelUser == null)
                throw new ArgumentNullException("channelUser");

            this.ChannelUser = channelUser;
        }

        public IrcChannelUser ChannelUser
        {
            get;
            private set;
        }
    }

    public class IrcChannelEventArgs : EventArgs
    {
        public IrcChannelEventArgs(IrcChannel channel)
            : base()
        {
            if (channel == null)
                throw new ArgumentNullException("channel");

            this.Channel = channel;
        }

        public IrcChannel Channel
        {
            get;
            private set;
        }
    }

    public class IrcUserEventArgs : EventArgs
    {
        public IrcUserEventArgs(IrcUser user)
            : base()
        {
            if (user == null)
                throw new ArgumentNullException("user");

            this.User = user;
        }

        public IrcUser User
        {
            get;
            private set;
        }
    }

    public class IrcPingOrPongReceivedEventArgs : EventArgs
    {
        public IrcPingOrPongReceivedEventArgs(string server)
        {
            if (server == null)
                throw new ArgumentNullException("server");

            this.Server = server;
        }

        public string Server
        {
            get;
            private set;
        }
    }

    public class IrcServerInfoEventArgs : EventArgs
    {
        public IrcServerInfoEventArgs(string address, int port)
            : base()
        {
            if (address == null)
                throw new ArgumentNullException("address");
            if (port <= 0)
                throw new ArgumentOutOfRangeException("port");

            this.Address = address;
            this.Port = port;
        }

        public string Address
        {
            get;
            private set;
        }

        public int Port
        {
            get;
            private set;
        }
    }

    public class IrcErrorMessageEventArgs : EventArgs
    {
        public IrcErrorMessageEventArgs(string errorMessage)
            : base()
        {
            if (errorMessage == null)
                throw new ArgumentNullException("errorMessage");

            this.ErrorMessage = errorMessage;
        }

        public string ErrorMessage
        {
            get;
            private set;
        }
    }

    public class IrcProtocolErrorEventArgs : EventArgs
    {
        public IrcProtocolErrorEventArgs(int code, IList<string> parameters, string message)
            : base()
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");
            if (message == null)
                throw new ArgumentNullException("message");

            this.Code = code;
            this.Parameters = new ReadOnlyCollection<string>(parameters);
            this.Message = message;
        }

        public int Code
        {
            get;
            private set;
        }

        public IList<string> Parameters
        {
            get;
            private set;
        }

        public string Message
        {
            get;
            private set;
        }
    }

    public class IrcErrorEventArgs : EventArgs
    {
        public IrcErrorEventArgs(Exception error)
            : base()
        {
            if (error == null)
                throw new ArgumentNullException("error");

            this.Error = error;
        }

        public Exception Error
        {
            get;
            private set;
        }
    }
}
