using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    public class IrcChannelCollection : ReadOnlyObservableCollection<IrcChannel>
    {
        private IrcClient client;

        internal IrcChannelCollection(IrcClient client, ObservableCollection<IrcChannel> list)
            : base(list)
        {
            this.client = client;
        }

        public IrcClient Client
        {
            get { return this.client; }
        }

        public void Join(params string[] channels)
        {
            Join((IEnumerable<string>)channels);
        }
        
        public void Join(IEnumerable<string> channels)
        {
            this.Client.Join(channels);
        }

        public void Join(params Tuple<string, string>[] channels)
        {
            Join((IEnumerable<Tuple<string, string>>)channels);
        }

        public void Join(IEnumerable<Tuple<string, string>> channels)
        {
            this.Client.Join(channels);
        }

        public void Part(params string[] channels)
        {
            Part((IEnumerable<string>)channels);
        }

        public void Part(IEnumerable<string> channels, string comment = null)
        {
            this.Client.Leave(channels, comment);
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            // Unset channel of all items removed from collection to null, and set channel of all items added to collection.
            if (e.OldItems != null)
                e.OldItems.Cast<IrcChannel>().ForEach(item => item.Client = null);
            if (e.NewItems != null)
                e.NewItems.Cast<IrcChannel>().ForEach(item => item.Client = this.client);
        }
    }
}
