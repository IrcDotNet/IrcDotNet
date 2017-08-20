using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using IrcDotNet.Collections;
using static IrcDotNet.IrcClient;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents an IRC channel that exists on a specific <see cref="IrcClient" />.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    [DebuggerDisplay("{ToString(), nq}")]
    public class IrcChannel : INotifyPropertyChanged, IIrcMessageTarget, IIrcMessageReceiveHandler, IIrcMessageReceiver
    {
        private IrcClient client;

        // Collection of current modes of channel.
        private readonly HashSet<char> modes;

        // Current topic of channel.
        private string topic;

        private IrcChannelType type;

        // Collection of users that are currently members of this channel.
        private readonly Collection<IrcChannelUser> users;

        internal IrcChannel(string name)
        {
            Name = name;
            type = IrcChannelType.Unspecified;
            modes = new HashSet<char>();
            Modes = new ReadOnlySet<char>(modes);
            users = new Collection<IrcChannelUser>();
            Users = new IrcChannelUserCollection(this, users);
        }

        /// <summary>
        ///     Gets the name of the channel.
        /// </summary>
        /// <value>The name of the channel.</value>
        public string Name { get; }

        /// <summary>
        ///     Gets the type of the channel.
        /// </summary>
        /// <value>The type of the channel.</value>
        public IrcChannelType Type
        {
            get { return type; }
            private set
            {
                type = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Type"));
            }
        }

        /// <summary>
        ///     Gets the current topic of the channel.
        /// </summary>
        /// <value>The current topic of the channel.</value>
        public string Topic
        {
            get { return topic; }
            private set
            {
                topic = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Topic"));
            }
        }

        /// <summary>
        ///     Gets a read-only collection of the modes the channel currently has.
        /// </summary>
        /// <value>The current modes of the channel.</value>
        public ReadOnlySet<char> Modes { get; }

        /// <summary>
        ///     Gets a collection of all channel users currently in the channel.
        /// </summary>
        /// <value>A collection of all users currently in the channel.</value>
        public IrcChannelUserCollection Users { get; }

        /// <summary>
        ///     Gets the client to which the channel belongs.
        /// </summary>
        /// <value>The client to which the channel belongs.</value>
        public IrcClient Client
        {
            get { return client; }
            internal set
            {
                client = value;
                OnPropertyChanged(new PropertyChangedEventArgs("Client"));
            }
        }

        /// <summary>
        ///     Occurs when the channel has received a message.
        /// </summary>
        public event EventHandler<IrcMessageEventArgs> MessageReceived;

        /// <summary>
        ///     Occurs when the channel has received a notice.
        /// </summary>
        public event EventHandler<IrcMessageEventArgs> NoticeReceived;

        #region IIrcMessageTarget Members

        string IIrcMessageTarget.Name
        {
            get { return Name; }
        }

        #endregion

        /// <summary>
        ///     Occurs when a property value changes.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        ///     Occurs when the list of users in the channel has been received.
        ///     The list of users is sent initially upon joining the channel, or on the request of the client.
        /// </summary>
        public event EventHandler<EventArgs> UsersListReceived;

        /// <summary>
        ///     Occurs when any of the modes of the channel have changed.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> ModesChanged;

        /// <summary>
        ///     Occurs when the topic of the channel has changed.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> TopicChanged;

        /// <summary>
        ///     Occurs when a user has joined the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserJoined;

        /// <summary>
        ///     Occurs when a user has left the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserLeft;

        /// <summary>
        ///     Occurs when a user is kicked from the channel.
        /// </summary>
        public event EventHandler<IrcChannelUserEventArgs> UserKicked;

        /// <summary>
        ///     Occurs when a user is invited to join the channel.
        /// </summary>
        public event EventHandler<IrcUserEventArgs> UserInvited;

        /// <summary>
        ///     Occurs when the channel has received a message, before the <see cref="MessageReceived" /> event.
        /// </summary>
        public event EventHandler<IrcPreviewMessageEventArgs> PreviewMessageReceived;

        /// <summary>
        ///     Occurs when the channel has received a notice, before the <see cref="NoticeReceived" /> event.
        /// </summary>
        public event EventHandler<IrcPreviewMessageEventArgs> PreviewNoticeReceived;

        /// <summary>
        ///     Gets the <see cref="IrcChannelUser" /> in the channel that corresponds to the specified
        ///     <see cref="IrcUser" />, or <see langword="null" /> if none is found.
        /// </summary>
        /// <param name="user">The <see cref="IrcUser" /> for which to look.</param>
        /// <returns>
        ///     The <see cref="IrcChannelUser" /> in the channel that corresponds to the specified
        ///     <see cref="IrcUser" />, or <see langword="null" /> if none is found.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="user" /> is <see langword="null" />.</exception>
        public IrcChannelUser GetChannelUser(IrcUser user)
        {
            if (user == null)
                throw new ArgumentNullException("user");

            return users.SingleOrDefault(cu => cu.User == user);
        }

        /// <inheritdoc cref="Invite(string)" />
        /// <param name="user">The user to invite to the channel</param>
        public void Invite(IrcUser user)
        {
            Invite(user.NickName);
        }

        /// <summary>
        ///     Invites the the specified user to the channel.
        /// </summary>
        /// <param name="userNickName">The nick name of the user to invite.</param>
        public void Invite(string userNickName)
        {
            client.Invite(this, userNickName);
        }

        /// <summary>
        ///     Kicks the specified user from the channel, giving the specified comment.
        /// </summary>
        /// <param name="userNickName">The nick name of the user to kick from the channel.</param>
        /// <param name="comment">The comment to give for the kick, or <see langword="null" /> for none.</param>
        public void Kick(string userNickName, string comment = null)
        {
            client.Kick(this, new[] {userNickName}, comment);
        }

        /// <summary>
        ///     Requests the current topic of the channel.
        /// </summary>
        public void GetTopic()
        {
            client.SetTopic(Name);
        }

        /// <summary>
        ///     Sets the topic of the channel to the specified text.
        /// </summary>
        /// <param name="newTopic">The new topic to set.</param>
        public void SetTopic(string newTopic)
        {
            client.SetTopic(Name, newTopic);
        }

        /// <summary>
        ///     Requests a list of the current modes of the channel, or if <paramref name="modes" /> is specified, the
        ///     settings for the specified modes.
        /// </summary>
        /// <param name="modes">
        ///     The modes for which to get the current settings, or <see langword="null" /> for all
        ///     current channel modes.
        /// </param>
        public void GetModes(string modes = null)
        {
            client.GetChannelModes(this, modes);
        }

        /// <inheritdoc cref="SetModes(IEnumerable{char})" />
        public void SetModes(params char[] newModes)
        {
            SetModes((IEnumerable<char>) newModes);
        }

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})" />
        /// <param name="newModes">
        ///     A collection of mode characters that should become the new modes.
        ///     Any modes in the collection that are not currently set will be set, and any nodes not in the collection that
        ///     are currently set will be unset.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="newModes" /> is <see langword="null" />.</exception>
        public void SetModes(IEnumerable<char> newModes)
        {
            if (newModes == null)
                throw new ArgumentNullException("newModes");

            lock (((ICollection) Modes).SyncRoot)
                SetModes(newModes.Except(modes), modes.Except(newModes));
        }

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})" />
        /// <exception cref="ArgumentNullException"><paramref name="setModes" /> is <see langword="null" />.</exception>
        /// <exception cref="ArgumentNullException"><paramref name="unsetModes" /> is <see langword="null" />.</exception>
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

        /// <inheritdoc cref="SetModes(string, IEnumerable{string})" />
        public void SetModes(string modes, params string[] modeParameters)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            SetModes(modes, (IEnumerable<string>) modeParameters);
        }

        /// <summary>
        ///     Sets the specified modes on the channel.
        /// </summary>
        /// <param name="modes">
        ///     The mode string that specifies mode changes, which takes the form
        ///     `( "+" / "-" ) *( mode character )`.
        /// </param>
        /// <param name="modeParameters">
        ///     A collection of parameters to he modes, or <see langword="null" /> for no
        ///     parameters.
        /// </param>
        /// <exception cref="ArgumentNullException"><paramref name="modes" /> is <see langword="null" />.</exception>
        public void SetModes(string modes, IEnumerable<string> modeParameters = null)
        {
            if (modes == null)
                throw new ArgumentNullException("modes");

            client.SetChannelModes(this, modes, modeParameters);
        }

        /// <summary>
        ///     Leaves the channel, giving the specified comment.
        /// </summary>
        /// <param name="comment">
        ///     The comment to send the server upon leaving the channel, or <see langword="null" /> for
        ///     no comment.
        /// </param>
        public void Leave(string comment = null)
        {
            client.Leave(new[] {Name}, comment);
        }

        internal void HandleUserNameReply(IrcChannelUser channelUser)
        {
            lock (((ICollection) Modes).SyncRoot)
            {
                if (users.Contains(channelUser))
                {
#if SILVERLIGHT
                    Debug.Assert(false, "User already in channel.");
#else
                    Debug.Fail("User already in channel.");
#endif
                    return;
                }
            }

            channelUser.Channel = this;
            lock (((ICollection) Users).SyncRoot)
                users.Add(channelUser);
        }

        internal void HandleTypeChanged(IrcChannelType type)
        {
            Type = type;
        }

        internal void HandleUsersListReceived()
        {
            OnUsersListReceived(new EventArgs());
        }

        internal void HandleTopicChanged(IrcMessage ircMessage, IrcUser source, string newTopic)
        {
            Topic = newTopic;

            OnTopicChanged(new IrcUserEventArgs(ircMessage, source));
        }

        internal void HandleModesChanged(IrcMessage ircMessage, IrcUser source, string newModes, IEnumerable<string> newModeParameters)
        {
            lock (((ICollection) Modes).SyncRoot)
                modes.UpdateModes(newModes, newModeParameters, client.ChannelUserModes,
                    (add, mode, modeParameter) => users.Single(
                        cu => cu.User.NickName == modeParameter).HandleModeChanged(add, mode));

            OnModesChanged(new IrcUserEventArgs(ircMessage, source));
        }

        internal void HandleUserJoined(IrcMessage ircMessage, IrcChannelUser channelUser)
        {
            lock (((ICollection) Modes).SyncRoot)
            {
                if (users.Contains(channelUser))
                {
#if SILVERLIGHT
                Debug.Assert(false, "User already in channel.");
#else
                    Debug.Fail("User already in channel.");
#endif
                    return;
                }
            }

            channelUser.Channel = this;
            lock (((ICollection) Users).SyncRoot)
                users.Add(channelUser);

            OnUserJoined(new IrcChannelUserEventArgs(ircMessage, channelUser, null));
        }

        internal void HandleUserLeft(IrcMessage ircMessage, IrcUser user, string comment)
        {
            lock (((ICollection) Modes).SyncRoot)
                HandleUserLeft(ircMessage, users.Single(u => u.User == user), comment);
        }

        internal void HandleUserLeft(IrcMessage ircMessage, IrcChannelUser channelUser, string comment)
        {
            lock (((ICollection) Users).SyncRoot)
                users.Remove(channelUser);

            OnUserLeft(new IrcChannelUserEventArgs(ircMessage, channelUser, comment));
        }

        internal void HandleUserKicked(IrcMessage ircMessage, IrcUser user, string comment)
        {
            lock (((ICollection) Modes).SyncRoot)
                HandleUserKicked(ircMessage, users.Single(u => u.User == user), comment);
        }

        internal void HandleUserKicked(IrcMessage ircMessage, IrcChannelUser channelUser, string comment)
        {
            lock (((ICollection) Users).SyncRoot)
                users.Remove(channelUser);

            OnUserKicked(new IrcChannelUserEventArgs(ircMessage, channelUser, comment));
        }

        internal void HandleUserInvited(IrcMessage ircMessage, IrcUser user)
        {
            lock (((ICollection) Modes).SyncRoot)
                OnUserInvited(new IrcUserEventArgs(ircMessage, user));
        }

        internal void HandleUserQuit(IrcChannelUser channelUser, string comment)
        {
            lock (((ICollection) Users).SyncRoot)
                users.Remove(channelUser);
        }

        internal void HandleMessageReceived(IrcMessage ircMessage, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            var previewEventArgs = new IrcPreviewMessageEventArgs(ircMessage, source, targets, text, Client.TextEncoding);
            OnPreviewMessageReceived(previewEventArgs);
            if (!previewEventArgs.Handled)
                OnMessageReceived(new IrcMessageEventArgs(ircMessage, source, targets, text, Client.TextEncoding));
        }

        internal void HandleNoticeReceived(IrcMessage ircMessage, IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text)
        {
            var previewEventArgs = new IrcPreviewMessageEventArgs(ircMessage, source, targets, text, Client.TextEncoding);
            OnPreviewNoticeReceived(previewEventArgs);
            if (!previewEventArgs.Handled)
                OnNoticeReceived(new IrcMessageEventArgs(ircMessage, source, targets, text, Client.TextEncoding));
        }

        /// <summary>
        ///     Raises the <see cref="UsersListReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="EventArgs" /> instance containing the event data.</param>
        protected virtual void OnUsersListReceived(EventArgs e)
        {
            var handler = UsersListReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="ModesChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnModesChanged(IrcUserEventArgs e)
        {
            var handler = ModesChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="TopicChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnTopicChanged(IrcUserEventArgs e)
        {
            var handler = TopicChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="UserJoined" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnUserJoined(IrcChannelUserEventArgs e)
        {
            var handler = UserJoined;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="UserLeft" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnUserLeft(IrcChannelUserEventArgs e)
        {
            var handler = UserLeft;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="UserKicked" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcChannelUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnUserKicked(IrcChannelUserEventArgs e)
        {
            var handler = UserKicked;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="UserInvited" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcUserEventArgs" /> instance containing the event data.</param>
        protected virtual void OnUserInvited(IrcUserEventArgs e)
        {
            var handler = UserInvited;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="MessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnMessageReceived(IrcMessageEventArgs e)
        {
            var handler = MessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PreviewMessageReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPreviewMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPreviewMessageReceived(IrcPreviewMessageEventArgs e)
        {
            var handler = PreviewMessageReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="NoticeReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnNoticeReceived(IrcMessageEventArgs e)
        {
            var handler = NoticeReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PreviewNoticeReceived" /> event.
        /// </summary>
        /// <param name="e">The <see cref="IrcPreviewMessageEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPreviewNoticeReceived(IrcPreviewMessageEventArgs e)
        {
            var handler = PreviewNoticeReceived;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Raises the <see cref="PropertyChanged" /> event.
        /// </summary>
        /// <param name="e">The <see cref="PropertyChangedEventArgs" /> instance containing the event data.</param>
        protected virtual void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            var handler = PropertyChanged;
            if (handler != null)
                handler(this, e);
        }

        /// <summary>
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            return Name;
        }

        #region IIrcMessageReceiveHandler Members

        void IIrcMessageReceiveHandler.HandleMessageReceived(IrcMessage ircMessage, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string text)
        {
            HandleMessageReceived(ircMessage, source, targets, text);
        }

        void IIrcMessageReceiveHandler.HandleNoticeReceived(IrcMessage ircMessage, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string text)
        {
            HandleNoticeReceived(ircMessage, source, targets, text);
        }

        #endregion
    }

    /// <summary>
    ///     Defines the types of channels. Each channel may only be of a single type at any one time.
    /// </summary>
    public enum IrcChannelType
    {
        /// <summary>
        ///     The channel type is unspecified.
        /// </summary>
        Unspecified,

        /// <summary>
        ///     The channel is public. The server always lists this channel.
        /// </summary>
        Public,

        /// <summary>
        ///     The channel is private. The server never lists this channel.
        /// </summary>
        Private,

        /// <summary>
        ///     The channel is secret. The server never lists this channel and pretends it does not exist when responding to
        ///     queries.
        /// </summary>
        Secret
    }
}