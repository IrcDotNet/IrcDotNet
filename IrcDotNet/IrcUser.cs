using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents an IRC user that resides on a specific <see cref="IrcClient"/>.
    /// </summary>
    public class IrcUser : INotifyPropertyChanged, IIrcMessageSource, IIrcMessageTarget
    {
        private bool isOnline;
        private string nickName;
        private string userName;
        private string realName;
        private string hostName;
        private string serverName;
        private string serverInfo;
        private bool isOperator;
        private bool isAway;
        private string awayMessage;
        private TimeSpan idleDuration;
        private int hopCount;

        private IrcClient client;

        internal IrcUser(bool isOnline, string nickName, string userName, string realName)
        {
            this.nickName = nickName;
            this.userName = userName;
            this.realName = realName;
            this.serverName = null;
            this.serverInfo = null;
            this.isOperator = false;
            this.isAway = false;
            this.awayMessage = null;
            this.idleDuration = TimeSpan.Zero;
            this.hopCount = 0;
        }

        internal IrcUser()
        {
        }

        public bool IsOnline
        {
            get { return this.isOnline; }
            internal set
            {
                this.isOnline = value;
                OnPropertyChanged(new PropertyChangedEventArgs("IsOnline"));
            }
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

        public bool IsAway
        {
            get { return this.isAway; }
            internal set
            {
                this.isAway = value;
                OnIsAwayChanged(new EventArgs());
                OnPropertyChanged(new PropertyChangedEventArgs("IsAway"));
            }
        }

        public string AwayMessage
        {
            get { return this.awayMessage; }
            internal set
            {
                this.awayMessage = value;
                OnPropertyChanged(new PropertyChangedEventArgs("AwayMessage"));
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
        
        public int HopCount
        {
            get { return this.hopCount; }
            internal set
            {
                this.hopCount = value;
                OnPropertyChanged(new PropertyChangedEventArgs("HopCount"));
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
        public event EventHandler<EventArgs> IsAwayChanged;
        public event EventHandler<IrcCommentEventArgs> Quit;
        public event PropertyChangedEventHandler PropertyChanged;

        public void WhoIs()
        {
            this.client.QueryWhoIs(new[] { this.nickName });
        }

        public void WhoWas(int entriesCount = -1)
        {
            this.client.QueryWhoWas(new[] { this.nickName }, entriesCount);
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

        internal void HandeQuit(string comment)
        {
            OnQuit(new IrcCommentEventArgs(comment));
        }

        /// <summary>
        /// Raises the <see cref="NickNameChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnNickNameChanged(EventArgs e)
        {
            var handler = this.NickNameChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="IsAwayChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnIsAwayChanged(EventArgs e)
        {
            var handler = this.IsAwayChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="Quit"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcCommentEventArgs"/> instance containing the event data.</param>
        protected virtual void OnQuit(IrcCommentEventArgs e)
        {
            var handler = this.Quit;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="E:PropertyChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="PropertyChangedEventArgs"/> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
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
