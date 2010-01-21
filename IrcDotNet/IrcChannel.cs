using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcChannel : INotifyPropertyChanged, IIrcMessageTarget, IIrcMessageReceiveHandler, IIrcMessageReceiver
    {
        private readonly char[] channelUserModes = new char[] { 'o', 'v' };

        private string name;
        private IrcChannelType type;
        private HashSet<char> modes;
        private ReadOnlySet<char> modesReadOnly;
        private string topic;
        private ObservableCollection<IrcChannelUser> users;
        private IrcChannelUserCollection usersReadOnly;

        private IrcClient client;

        internal IrcChannel(string name)
        {
            this.name = name;
            this.type = IrcChannelType.Unspecified;
            this.modes = new HashSet<char>();
            this.modesReadOnly = new ReadOnlySet<char>(this.modes);
            this.users = new ObservableCollection<IrcChannelUser>();
            this.usersReadOnly = new IrcChannelUserCollection(this, this.users);
        }

        public string Name
        {
            get { return this.name; }
        }

        public IrcChannelType Type
        {
            get { return this.type; }
            internal set
            {
                this.type = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Type"));
            }
        }

        public ReadOnlySet<char> Modes
        {
            get { return modesReadOnly; }
        }

        public string Topic
        {
            get { return this.topic; }
            internal set
            {
                this.topic = value;
                OnTopicChanged(new EventArgs());
                OnPropertyChanged(new PropertyChangedEventArgs("Topic"));
            }
        }

        public IrcChannelUserCollection Users
        {
            get { return this.usersReadOnly; }
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

        public event EventHandler<EventArgs> UsersListReceived;
        public event EventHandler<EventArgs> ModesChanged;
        public event EventHandler<EventArgs> TopicChanged;
        public event EventHandler<IrcChannelUserEventArgs> UserJoined;
        public event EventHandler<IrcChannelUserEventArgs> UserParted;
        public event EventHandler<IrcChannelUserEventArgs> UserKicked;
        public event EventHandler<IrcMessageEventArgs> MessageReceived;
        public event EventHandler<IrcMessageEventArgs> NoticeReceived;
        public event PropertyChangedEventHandler PropertyChanged;

        public IrcChannelUser GetChannelUser(IrcUser user)
        {
            if (user == null)
                throw new ArgumentNullException("user");

            return this.users.SingleOrDefault(cu => cu.User == user);
        }

        public void GetModes(string modes = null)
        {
            this.client.GetChannelModes(this, null);
        }

        public void SetModes(params char[] newModes)
        {
            SetModes((IEnumerable<char>)newModes);
        }

        public void SetModes(IEnumerable<char> newModes)
        {
            if (newModes == null)
                throw new ArgumentNullException("newModes");

            SetModes(newModes.Except(this.modes), this.modes.Except(newModes));
        }

        public void SetModes(IEnumerable<char> setModes, IEnumerable<char> unsetModes,
            IEnumerable<string> modeParameters = null)
        {
            if (setModes == null)
                throw new ArgumentNullException("setModes");
            if (unsetModes == null)
                throw new ArgumentNullException("unsetModes");

            SetModes("+" + string.Join(string.Empty, setModes) + "-" + string.Join(string.Empty, unsetModes),
                modeParameters);
        }

        public void SetModes(string modes, params string[] modeParameters)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            SetModes(modes, (IEnumerable<string>)modeParameters);
        }

        public void SetModes(string modes, IEnumerable<string> modeParameters = null)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            this.client.SetChannelModes(this, modes, modeParameters);
        }

        public void Part(string comment = null)
        {
            this.client.Part(new[] { this.name }, comment);
        }

        internal void HandleUsersListReceived()
        {
            OnUsersListReceived(new EventArgs());
        }

        internal void HandleModesChanged(string newModes, IEnumerable<string> newModeParameters)
        {
            this.modes.UpdateModes(newModes, newModeParameters, channelUserModes, (add, mode, modeParameter) =>
                this.users.Single(cu => cu.User.NickName == modeParameter).HandleModeChanged(add, mode));
            OnModesChanged(new EventArgs());
        }

        internal void HandleUserJoined(IrcChannelUser channelUser)
        {
            this.users.Add(channelUser);
            OnUserJoined(new IrcChannelUserEventArgs(channelUser, null));
        }

        internal void HandleUserParted(IrcUser user, string comment)
        {
            HandleUserParted(this.users.Single(u => u.User == user), comment);
        }

        internal void HandleUserParted(IrcChannelUser channelUser, string comment)
        {
            OnUserParted(new IrcChannelUserEventArgs(channelUser, comment));
            this.users.Remove(channelUser); ;
        }

        internal void HandleUserKicked(IrcUser user, string comment)
        {
            HandleUserKicked(this.users.Single(u => u.User == user), comment);
        }

        internal void HandleUserKicked(IrcChannelUser channelUser, string comment)
        {
            OnUserKicked(new IrcChannelUserEventArgs(channelUser, comment));
            this.users.Remove(channelUser);
        }

        internal void HandleMessageReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            OnMessageReceived(new IrcMessageEventArgs(source, targets, text));
        }

        internal void HandleNoticeReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            OnNoticeReceived(new IrcMessageEventArgs(source, targets, text));
        }

        protected virtual void OnUsersListReceived(EventArgs e)
        {
            var handler = this.UsersListReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnModesChanged(EventArgs e)
        {
            var handler = this.ModesChanged;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnTopicChanged(EventArgs e)
        {
            var handler = this.TopicChanged;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnUserJoined(IrcChannelUserEventArgs e)
        {
            var handler = this.UserJoined;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnUserParted(IrcChannelUserEventArgs e)
        {
            var handler = this.UserParted;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnUserKicked(IrcChannelUserEventArgs e)
        {
            var handler = this.UserKicked;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnMessageReceived(IrcMessageEventArgs e)
        {
            var handler = this.MessageReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnNoticeReceived(IrcMessageEventArgs e)
        {
            var handler = this.NoticeReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = this.PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        public override string ToString()
        {
            return this.name;
        }

        #region IIrcMessageTarget Members

        string IIrcMessageTarget.Name
        {
            get { return this.Name; }
        }

        #endregion

        #region IIrcMessageReceiveHandler Members

        void IIrcMessageReceiveHandler.HandleMessageReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets,
            string text)
        {
            HandleMessageReceived(source, targets, text);
        }

        void IIrcMessageReceiveHandler.HandleNoticeReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets,
            string text)
        {
            HandleNoticeReceived(source, targets, text);
        }

        #endregion
    }

    public enum IrcChannelType
    {
        Unspecified,
        Public,
        Private,
        Secret,
    }
}
