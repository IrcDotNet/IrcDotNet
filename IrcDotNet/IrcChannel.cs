using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcChannel : INotifyPropertyChanged
    {
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
        public event PropertyChangedEventHandler PropertyChanged;

        public IrcChannelUser GetChannelUser(IrcUser user)
        {
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
            SetModes(newModes.Except(this.modes), this.modes.Except(newModes));
        }

        public void SetModes(IEnumerable<char> setModes, IEnumerable<char> unsetModes,
            IEnumerable<string> modeParameters = null)
        {
            SetModes("+" + string.Join(string.Empty, setModes) + "-" + string.Join(string.Empty, unsetModes),
                modeParameters);
        }

        public void SetModes(string modes, params string[] modeParameters)
        {
            SetModes(modes, (IEnumerable<string>)modeParameters);
        }

        public void SetModes(string modes, IEnumerable<string> modeParameters = null)
        {
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

        internal void HandleModesChanged(string newModes)
        {
            this.modes.UpdateModes(newModes);
            // TODO: Handle mode changes of channel users.
            OnModesChanged(new EventArgs());
        }

        internal void HandleUserJoined(IrcChannelUser channelUser)
        {
            this.users.Add(channelUser);
            OnUserJoined(new IrcChannelUserEventArgs(channelUser));
        }

        internal bool HandleUserParted(IrcUser user)
        {
            return HandleUserParted(this.users.Single(u => u.User == user));
        }

        internal bool HandleUserParted(IrcChannelUser channelUser)
        {
            OnUserParted(new IrcChannelUserEventArgs(channelUser));
            return this.users.Remove(channelUser); ;
        }

        internal bool HandleUserKicked(IrcUser user)
        {
            return HandleUserKicked(this.users.Single(u => u.User == user));
        }

        internal bool HandleUserKicked(IrcChannelUser channelUser)
        {
            OnUserKicked(new IrcChannelUserEventArgs(channelUser));
            return  this.users.Remove(channelUser);
        }

        public readonly Guid foo = System.Guid.NewGuid();

        protected virtual void OnUsersListReceived(EventArgs e)
        {
            if (this.UsersListReceived != null)
                this.UsersListReceived(this, e);
        }

        protected virtual void OnModesChanged(EventArgs e)
        {
            if (this.ModesChanged != null)
                this.ModesChanged(this, e);
        }

        protected virtual void OnTopicChanged(EventArgs e)
        {
            if (this.TopicChanged != null)
                this.TopicChanged(this, e);
        }

        protected virtual void OnUserJoined(IrcChannelUserEventArgs e)
        {
            if (this.UserJoined != null)
                this.UserJoined(this, e);
        }

        protected virtual void OnUserParted(IrcChannelUserEventArgs e)
        {
            if (this.UserParted != null)
                this.UserParted(this, e);
        }

        protected virtual void OnUserKicked(IrcChannelUserEventArgs e)
        {
            if (this.UserKicked != null)
                this.UserKicked(this, e);
        }

        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            if (this.PropertyChanged != null)
                this.PropertyChanged(this, e);
        }
    }

    public enum IrcChannelType
    {
        Unspecified,
        Public,
        Private,
        Secret,
    }
}
