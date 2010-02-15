using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    /// <summary>
    /// Represents an IRC channel that resides on a specific <see cref="IrcClient"/>.
    /// </summary>
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

        /// <summary>
        /// Gets the name of the channel.
        /// </summary>
        /// <value>The name of the channel.</value>
        public string Name
        {
            get { return this.name; }
        }

        /// <summary>
        /// Gets the type of the channel.
        /// </summary>
        /// <value>The type of the channel.</value>
        public IrcChannelType Type
        {
            get { return this.type; }
            internal set
            {
                this.type = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Type"));
            }
        }

        /// <summary>
        /// Gets a read-only collection of the modes the channel currently has.
        /// </summary>
        /// <value>The current modes of the channel.</value>
        public ReadOnlySet<char> Modes
        {
            get { return modesReadOnly; }
        }

        /// <summary>
        /// Gets the current topic of the channel.
        /// </summary>
        /// <value>The current topic of the channel.</value>
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

        /// <summary>
        /// Gets a collection of all channel users currently in the channel.
        /// </summary>
        /// <value>A collection of all users currently in the channel.</value>
        public IrcChannelUserCollection Users
        {
            get { return this.usersReadOnly; }
        }

        /// <summary>
        /// Gets the client to which the channel belongs.
        /// </summary>
        /// <value>The client to which the channel belongs.</value>
        public IrcClient Client
        {
            get { return this.client;}
            internal set
            {
                this.client = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Client"));
            }
        }

        /// <summary>
        /// Occurs when the list of users in the channel has been received.
        /// The list of users is sent initially upon joining the channel, or on the request of the client.
        /// </summary>
        public event EventHandler<EventArgs> UsersListReceived;
        /// <summary>
        /// Occurs when any of the modes of the channel have changed.
        /// </summary>
        public event EventHandler<EventArgs> ModesChanged;
        /// <summary>
        /// Occurs when the topic of the channel has changed.
        /// </summary>
        public event EventHandler<EventArgs> TopicChanged;
        /// <summary>
        /// Occurs when a user has joined the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserJoined;
        /// <summary>
        /// Occurs when a user has left the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserLeft;
        /// <summary>
        /// Occurs when a user is kicked from the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserKicked;
        /// <summary>
        /// Occurs when the channel has received a message.
        /// </summary>
        public event EventHandler<IrcMessageEventArgs> MessageReceived;
        /// <summary>
        /// Occurs when the channel has received a notice.
        /// </summary>
        public event EventHandler<IrcMessageEventArgs> NoticeReceived;
        /// <summary>
        /// Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets the <see cref="IrcChannelUser"/> in the channel that corresponds to the specified
        /// <see cref="IrcUser"/>, or <see langword="null"/> if none is found.
        /// </summary>
        /// <param name="user">The <see cref="IrcUser"/> for which to look.</param>
        /// <returns>The <see cref="IrcChannelUser"/> in the channel that corresponds to the specified
        /// <see cref="IrcUser"/>, or <see langword="null"/> if none is found.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="user"/> is <see langword="null"/>.</exception>
        public IrcChannelUser GetChannelUser(IrcUser user)
        {
            if (user == null)
                throw new ArgumentNullException("user");

            return this.users.SingleOrDefault(cu => cu.User == user);
        }

        /// <summary>
        /// Requests a list of the current modes of the channel, or if <paramref name="modes"/> is specified, the
        /// settings for the specified modes.
        /// </summary>
        /// <param name="modes">The modes for which to get the current settings, or <see langword="null"/> for all
        /// current channel modes.</param>
        public void GetModes(string modes = null)
        {
            this.client.GetChannelModes(this, modes);
        }

        /// <inheritdoc cref="SetModes(IEnumerable{char})"/>
        public void SetModes(params char[] newModes)
        {
            SetModes((IEnumerable<char>)newModes);
        }

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})"/>
        /// <param name="newModes">A collection of mode characters that should become the new modes.
        /// Any modes in the collection that are not currently set will be set, and any nodes not in the collection that
        /// are currently set will be unset.</param>
        /// <exception cref="ArgumentNullException"><paramref name="newModes"/> is <see langword="null"/>.</exception>
        public void SetModes(IEnumerable<char> newModes)
        {
            if (newModes == null)
                throw new ArgumentNullException("newModes");

            SetModes(newModes.Except(this.modes), this.modes.Except(newModes));
        }

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})"/>
        /// <exception cref="ArgumentNullException"><paramref name="setModes"/> is <see langword="null"/>. -or-
        /// <paramref name="unsetModes"/> is <see langword="null"/>.</exception>
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

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})"/>
        public void SetModes(string modes, params string[] modeParameters)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            SetModes(modes, (IEnumerable<string>)modeParameters);
        }

        /// <summary>
        /// Sets the specified modes on the channel.
        /// </summary>
        /// <param name="modes">The mode string that specifies mode changes, which takes the form
        /// `( "+" / "-" ) *( mode character )`.</param>
        /// <param name="modeParameters">A collection of parameters to he modes, or <see langword="null"/> for no
        /// parameters.</param>
        /// <exception cref="ArgumentNullException"><paramref name="modes"/> is <see langword="null"/>.</exception>
        public void SetModes(string modes, IEnumerable<string> modeParameters = null)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            this.client.SetChannelModes(this, modes, modeParameters);
        }

        /// <summary>
        /// Leaves the channel, giving the specified comment.
        /// </summary>
        /// <param name="comment">The comment to send the server upon leaving the channel, or <see langword="null"/> for
        /// no comment.</param>
        public void Leave(string comment = null)
        {
            this.client.Leave(new[] { this.name }, comment);
        }

        internal void HandleUserInitialPresence(IrcChannelUser channelUser)
        {
            this.users.Add(channelUser);
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

        internal void HandleUserLeft(IrcUser user, string comment)
        {
            HandleUserLeft(this.users.Single(u => u.User == user), comment);
        }

        internal void HandleUserLeft(IrcChannelUser channelUser, string comment)
        {
            OnUserLeft(new IrcChannelUserEventArgs(channelUser, comment));
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

        /// <summary>
        /// Raises the <see cref="UsersListReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnUsersListReceived(EventArgs e)
        {
            var handler = this.UsersListReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="ModesChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnModesChanged(EventArgs e)
        {
            var handler = this.ModesChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="TopicChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        protected virtual void OnTopicChanged(EventArgs e)
        {
            var handler = this.TopicChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="UserJoined"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs"/> instance containing the event data.</param>
        protected virtual void OnUserJoined(IrcChannelUserEventArgs e)
        {
            var handler = this.UserJoined;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="UserLeft"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs"/> instance containing the event data.</param>
        protected virtual void OnUserLeft(IrcChannelUserEventArgs e)
        {
            var handler = this.UserLeft;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="UserKicked"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs"/> instance containing the event data.</param>
        protected virtual void OnUserKicked(IrcChannelUserEventArgs e)
        {
            var handler = this.UserKicked;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="MessageReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcMessageEventArgs"/> instance containing the event data.</param>
        protected virtual void OnMessageReceived(IrcMessageEventArgs e)
        {
            var handler = this.MessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="NoticeReceived"/> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcMessageEventArgs"/> instance containing the event data.</param>
        protected virtual void OnNoticeReceived(IrcMessageEventArgs e)
        {
            var handler = this.NoticeReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        /// Raises the <see cref="PropertyChanged"/> event.
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
    
    /// <summary>
    /// Defines the types of channels. Each channel may only be of a single type at any one time.
    /// </summary>
    public enum IrcChannelType
    {
        /// <summary>
        /// The channel type is unspecified.
        /// </summary>
        Unspecified,
        /// <summary>
        /// The channel is public. The server always lists this channel.
        /// </summary>
        Public,
        /// <summary>
        /// The channel is private. The server never lists this channel.
        /// </summary>
        Private,
        /// <summary>
        /// The channel is secret. The server never lists this channel and pretends it does not exist when responding to
        /// queries.
        /// </summary>
        Secret,
    }
}
