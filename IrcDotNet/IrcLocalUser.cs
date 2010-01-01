using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    // TODO: Raise event when modes are changed.
    public class IrcLocalUser : IrcUser
    {
        // Internal and exposable collections of current modes of user.
        private HashSet<char> modes;
        private ReadOnlySet<char> modesReadOnly;

        public IrcLocalUser(string nickName, string userName, string realName, IEnumerable<char> modes = null)
            : base(nickName, userName, realName)
        {
            this.modes = new HashSet<char>();
            this.modesReadOnly = new ReadOnlySet<char>(this.modes);
            if (modes != null)
                this.modes.AddRange(modes);
        }

        public ReadOnlySet<char> Modes
        {
            get { return this.modesReadOnly; }
        }

        public event EventHandler<EventArgs> ModesChanged;

        public void GetModes()
        {
            this.Client.GetLocalUserModes(this);
        }

        public void SetModes(params char[] newModes)
        {
            SetModes((IEnumerable<char>)newModes);
        }

        public void SetModes(IEnumerable<char> newModes)
        {
            SetModes(newModes.Except(this.modes), this.modes.Except(newModes));
        }

        public void SetModes(IEnumerable<char> setModes, IEnumerable<char> unsetModes)
        {
            SetModes("+" + string.Join(string.Empty, setModes) + "-" + string.Join(string.Empty, unsetModes));
        }

        public void SetModes(string modes)
        {
            this.Client.SetLocalUserModes(this, modes);
        }

        internal void HandleModeChanged(string newModes)
        {
            this.modes.UpdateModes(newModes);
            OnModesChanged(new EventArgs());
        }

        protected virtual void OnModesChanged(EventArgs e)
        {
            if (this.ModesChanged != null)
                this.ModesChanged(this, e);
        }
    }
}
