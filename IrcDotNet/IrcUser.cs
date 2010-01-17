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

        private IrcClient client;

        internal IrcUser(string nickName, string userName, string realName)
        {
            this.nickName = nickName;
            this.userName = userName;
            this.realName = realName;
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
