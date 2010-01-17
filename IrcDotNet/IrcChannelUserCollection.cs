using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcChannelUserCollection : ReadOnlyObservableCollection<IrcChannelUser>
    {
        private IrcChannel channel;

        internal IrcChannelUserCollection(IrcChannel channel, ObservableCollection<IrcChannelUser> list)
            : base(list)
        {
            this.channel = channel;
        }

        public IrcChannel Channel
        {
            get { return this.channel; }
        }

        public IEnumerable<IrcUser> GetUsers()
        {
            return this.Items.Select(channelUser => channelUser.User);
        }

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
