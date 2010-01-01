using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcUser : INotifyPropertyChanged
    {
        private string nickName;
        private string userName;
        private string realName;
        private string host;

        private IrcClient client;

        internal IrcUser(string nickName, string userName, string realName)
            : this(nickName)
        {
            this.userName = userName;
            this.realName = realName;
        }

        internal IrcUser(string nickName)
        {
            this.nickName = nickName;
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

        public string Host
        {
            get { return this.host; }
            internal set
            {
                this.host = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Host"));
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
    }
}
