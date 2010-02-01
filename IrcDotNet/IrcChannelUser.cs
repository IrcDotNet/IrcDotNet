using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcChannelUser : INotifyPropertyChanged
    {
        // Internal and exposable collections of channel modes currently active on user.
        private HashSet<char> modes;
        private ReadOnlySet<char> modesReadOnly;

        private IrcChannel channel;
        private IrcUser user;

        internal IrcChannelUser(IrcUser user, IEnumerable<char> modes = null)
        {
            this.user = user;

            this.modes = new HashSet<char>();
            this.modesReadOnly = new ReadOnlySet<char>(this.modes);
            if (modes != null)
                this.modes.AddRange(modes);
        }

        public bool IsAway
        {
            get { return this.modes.Contains('a'); }
        }

        public ReadOnlySet<char> Modes
        {
            get { return this.modesReadOnly; }
        }

        public IrcChannel Channel
        {
            get { return this.channel; }
            internal set
            {
                this.channel = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Channel"));
            }
        }

        public IrcUser User
        {
            get { return this.user; }
        }

        public event EventHandler<EventArgs> ModesChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public void Kick(string comment = null)
        {
            this.channel.Client.Kick(new[] { this }, comment);
        }

        public void Op()
        {
            this.channel.SetModes("+o", this.user.NickName);
        }

        public void DeOp()
        {
            this.channel.SetModes("-o", this.user.NickName);
        }

        public void Voice()
        {
            this.channel.SetModes("+v", this.user.NickName);
        }

        public void DeVoice()
        {
            this.channel.SetModes("-v", this.user.NickName);
        }

        internal void HandleModeChanged(bool add, char mode)
        {
            if (add)
                this.modes.Add(mode);
            else
                this.modes.Remove(mode);
            OnModesChanged(new EventArgs());
        }

        protected virtual void OnModesChanged(EventArgs e)
        {
            var handler = this.ModesChanged;
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
            return this.channel.Name + "/" + this.user.NickName;
        }
    }
}
