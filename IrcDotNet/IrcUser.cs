using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcUser : INotifyPropertyChanged, IIrcMessageSource, IIrcMessageTarget
    {
        private string nickName;
        private string userName;
        private string realName;
        private string hostName;
        private string serverName;
        private string serverInfo;
        private bool isOperator;
        private TimeSpan idleDuration;

        private IrcClient client;

        internal IrcUser(string nickName, string userName, string realName)
        {
            this.nickName = nickName;
            this.userName = userName;
            this.realName = realName;
            this.serverName = null;
            this.serverInfo = null;
            this.isOperator = false;
            this.idleDuration = TimeSpan.Zero;
        }

        internal IrcUser()
        {
        }

        public string NickName
        {
            get { return this.nickName; }
            internal set
            {
                this.nickName = value;
                OnNickNameChanged(new EventArgs());
                OnPropertyChanged(new PropertyChangedEventArgs("NickName"));
            }
        }

        public string UserName
        {
            get { return this.userName; }
            internal set
            {
                this.userName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("UserName"));
            }
        }

        public string RealName
        {
            get { return this.realName; }
            internal set
            {
                this.realName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("RealName"));
            }
        }

        public string HostName
        {
            get { return this.hostName; }
            internal set
            {
                this.hostName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HostName"));
            }
        }

        public string ServerName
        {
            get { return this.serverName; }
            internal set
            {
                this.serverName = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ServerName"));
            }
        }

        public string ServerInfo
        {
            get { return this.serverInfo; }
            internal set
            {
                this.serverInfo = value;
                OnPropertyChanged(new PropertyChangedEventArgs("ServerInfo"));
            }
        }

        public bool IsOperator
        {
            get { return this.isOperator; }
            internal set
            {
                this.isOperator = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsOperator"));
            }
        }

        public TimeSpan IdleDuration
        {
            get { return this.idleDuration; }
            internal set
            {
                this.idleDuration = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IdleDuration"));
            }
        }

        public IrcClient Client
        {
            get { return this.client; }
            internal set
            {
                this.client = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Client"));
            }
        }

        public event EventHandler<EventArgs> NickNameChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void WhoIs()
        {
            this.client.WhoIs(new[] { this.nickName });
        }

        public void WhoWas(int entriesCount = -1)
        {
            this.client.WhoWas(new[] { this.nickName }, entriesCount);
        }

        public IEnumerable<IrcChannelUser> GetChannelUsers()
        {
            // Get each channel user corresponding to this user that is member of any channel.
            foreach (var channel in this.client.Channels)
            {
                foreach (var channelUser in channel.Users)
                {
                    if (channelUser.User == this)
                        yield return channelUser;
                }
            }
        }

        protected virtual void OnNickNameChanged(EventArgs e)
        {
            if (this.NickNameChanged != null)
                this.NickNameChanged(this, e);
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, e);
        }

        public override string ToString()
        {
            return this.nickName;
        }

        #region IIrcMessageSource Members

        string IIrcMessageSource.Name
        {
            get { return this.NickName; }
        }

        #endregion

        #region IIrcMessageTarget Members

        string IIrcMessageTarget.Name
        {
            get { return this.NickName; }
        }

        #endregion
    }
}
