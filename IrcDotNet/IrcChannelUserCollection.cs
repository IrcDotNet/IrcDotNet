using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    /// <summary>
    /// Represents a collection of <see cref="IrcChannelUser"/> objects.
    /// </summary>
    /// <seealso cref="IrcChannelUser"/>
    public class IrcChannelUserCollection : ReadOnlyObservableCollection<IrcChannelUser>
    {
        private IrcChannel channel;
        
        internal IrcChannelUserCollection(IrcChannel channel, ObservableCollection<IrcChannelUser> list)
            : base(list)
        {
            this.channel = channel;
        }

        /// <summary>
        /// Gets the channel to which the collection of channel users belongs.
        /// </summary>
        /// <value>The channel to which the collection of channel users belongs..</value>
        public IrcChannel Channel
        {
            get { return this.channel; }
        }

        /// <summary>
        /// Gets a collection of all users that correspond to the channel users in the collection.
        /// </summary>
        /// <returns>A collection of users.</returns>
        public IEnumerable<IrcUser> GetUsers()
        {
            return this.Items.Select(channelUser => channelUser.User);
        }

        /// <summary>
        /// Raises the <see cref="ReadOnlyObservableCollection{T}.CollectionChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            // Unset channel of all items removed from collection to null, and set channel of all items added to collection.
            if (e.OldItems != null)
                e.OldItems.Cast<IrcChannelUser>().ForEach(item => item.Channel = null);
            if (e.NewItems != null)
                e.NewItems.Cast<IrcChannelUser>().ForEach(item => item.Channel = this.channel);
        }
    }
}
