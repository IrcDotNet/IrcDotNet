using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using IrcDotNet.Common.Collections;

namespace IrcDotNet
{
    public class IrcUserCollection : ReadOnlyObservableCollection<IrcUser>
    {
        private IrcClient client;

        internal IrcUserCollection(IrcClient client, ObservableCollection<IrcUser> list)
            : base(list)
        {
            this.client = client;
        }

        public IrcClient Client
        {
            get { return this.client; }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnCollectionChanged(e);

            // Unset channel of all items removed from collection to null, and set channel of all items added to collection.
            if (e.OldItems != null)
                e.OldItems.Cast<IrcUser>().ForEach(item => item.Client = null);
            if (e.NewItems != null)
                e.NewItems.Cast<IrcUser>().ForEach(item => item.Client = this.client);
        }
    }
}
