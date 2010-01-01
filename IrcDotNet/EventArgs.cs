using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcChannelUserEventArgs : EventArgs
    {
        public IrcChannelUserEventArgs(IrcChannelUser channelUser)
            : base()
        {
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

    public class IrcErrorEventArgs : EventArgs
    {
        public IrcErrorEventArgs(Exception error)
            : base()
        {
            this.Error = error;
        }

        public Exception Error
        {
            get;
            private set;
        }
    }
}
