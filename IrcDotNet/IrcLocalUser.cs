using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcLocalUser : IrcUser, IIrcMessageSendHandler, IIrcMessageReceiveHandler, IIrcMessageReceiver
    {
        // Internal and exposable collections of current modes of user.
        private HashSet<char> modes;
        private ReadOnlySet<char> modesReadOnly;

        internal IrcLocalUser(string nickName, string userName, string realName, IEnumerable<char> modes = null)
            : base(true, nickName, userName, realName)
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

        /// <summary>
        /// Occurs when the modes of the local user have changed.
        /// </summary>
        public event EventHandler<EventArgs> ModesChanged;
        /// <summary>
        /// Occurs when the local user has joined a channel.
        /// </summary>
        public event EventHandler<IrcChannelEventArgs> JoinedChannel;
        /// <summary>
        /// Occurs when the local user has left a channel.
        /// </summary>
        public event EventHandler<IrcChannelEventArgs> LeftChannel;
        public event EventHandler<IrcMessageEventArgs> MessageSent;
        public event EventHandler<IrcMessageEventArgs> MessageReceived;
        public event EventHandler<IrcMessageEventArgs> NoticeSent;
        public event EventHandler<IrcMessageEventArgs> NoticeReceived;

        public void SendMessage(IIrcMessageTarget target, string text)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            SendMessage(new[] { target }, text);
        }

        public void SendMessage(IEnumerable<IIrcMessageTarget> targets, string text)
        {
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (text == null)
                throw new ArgumentNullException("text");

            SendMessage(targets.Select(t => t.Name), text);
        }

        public void SendMessage(string target, string text)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            SendMessage(new[] { target }, text);
        }

        public void SendMessage(IEnumerable<string> targets, string text)
        {
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (text == null)
                throw new ArgumentNullException("text");

            this.Client.SendPrivateMessage(targets, text);
        }

        public void SendNotice(IIrcMessageTarget target, string text)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            SendNotice(new[] { target }, text);
        }

        public void SendNotice(IEnumerable<IIrcMessageTarget> targets, string text)
        {
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (text == null)
                throw new ArgumentNullException("text");

            SendNotice(targets.Select(t => t.Name), text);
        }

        public void SendNotice(string target, string text)
        {
            if (target == null)
                throw new ArgumentNullException("target");
            if (text == null)
                throw new ArgumentNullException("text");

            SendNotice(new[] { target }, text);
        }

        public void SendNotice(IEnumerable<string> targets, string text)
        {
            if (targets == null)
                throw new ArgumentNullException("targets");
            if (text == null)
                throw new ArgumentNullException("text");

            this.Client.SendNotice(targets, text);
        }

        public void SetNickName(string nickName)
        {
            if (nickName == null)
                throw new ArgumentNullException("nickName");

            this.Client.SetNickName(nickName);
        }

        public void SetAway(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            this.Client.SetAway(text);
        }

        public void UnsetAway()
        {
            this.Client.UnsetAway();
        }

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
            if (newModes == null)
                throw new ArgumentNullException("newModes");

            SetModes(newModes.Except(this.modes), this.modes.Except(newModes));
        }

        public void SetModes(IEnumerable<char> setModes, IEnumerable<char> unsetModes)
        {
            if (setModes == null)
                throw new ArgumentNullException("setModes");
            if (unsetModes == null)
                throw new ArgumentNullException("unsetModes");

            SetModes("+" + string.Join(string.Empty, setModes) + "-" + string.Join(string.Empty, unsetModes));
        }

        public void SetModes(string modes)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            this.Client.SetLocalUserModes(this, modes);
        }

        internal void HandleModesChanged(string newModes)
        {
            this.modes.UpdateModes(newModes);
            OnModesChanged(new EventArgs());
        }

        internal void HandleJoinedChannel(IrcChannel channel)
        {
            OnJoinedChannel(new IrcChannelEventArgs(channel, null));
        }

        internal void HandleLeftChannel(IrcChannel channel)
        {
            OnLeftChannel(new IrcChannelEventArgs(channel, null));
        }

        internal void HandleMessageSent(IList<IIrcMessageTarget> targets, string text)
        {
            OnMessageSent(new IrcMessageEventArgs(this, targets, text));
        }

        internal void HandleNoticeSent(IList<IIrcMessageTarget> targets, string text)
        {
            OnNoticeSent(new IrcMessageEventArgs(this, targets, text));
        }

        internal void HandleMessageReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            OnMessageReceived(new IrcMessageEventArgs(source, targets, text));
        }

        internal void HandleNoticeReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            OnNoticeReceived(new IrcMessageEventArgs(source, targets, text));
        }

        protected virtual void OnModesChanged(EventArgs e)
        {
            var handler = this.ModesChanged;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnJoinedChannel(IrcChannelEventArgs e)
        {
            var handler = this.JoinedChannel;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnLeftChannel(IrcChannelEventArgs e)
        {
            var handler = this.LeftChannel;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnMessageSent(IrcMessageEventArgs e)
        {
            var handler = this.MessageSent;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnMessageReceived(IrcMessageEventArgs e)
        {
            var handler = this.MessageReceived;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnNoticeSent(IrcMessageEventArgs e)
        {
            var handler = this.NoticeSent;
            if (handler != null)
                handler(this, e);
        }

        protected virtual void OnNoticeReceived(IrcMessageEventArgs e)
        {
            var handler = this.NoticeReceived;
            if (handler != null)
                handler(this, e);
        }

        #region IIrcMessageSendHandler Members

        void IIrcMessageSendHandler.HandleMessageSent(IList<IIrcMessageTarget> targets, string text)
        {
            HandleMessageSent(targets, text);
        }

        void IIrcMessageSendHandler.HandleNoticeSent(IList<IIrcMessageTarget> targets, string text)
        {
            HandleNoticeSent(targets, text);
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
}
