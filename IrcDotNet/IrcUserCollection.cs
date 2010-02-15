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
    /// <summary>
    /// Represents a collection of <see cref="IrcUser"/> objects.
    /// </summary>
    public class IrcUserCollection : ReadOnlyObservableCollection<IrcUser>
    {
        private IrcClient client;
        
        internal IrcUserCollection(IrcClient client, ObservableCollection<IrcUser> list)
            : base(list)
        {
            this.client = client;
        }

        /// <summary>
        /// Gets the client to which the collection of users belongs.
        /// </summary>
        /// <value>The client to which the collection of users belongs.</value>
        public IrcClient Client
        {
            get { return this.client; }
        }

        /// <summary>
        /// Raises the <see cref="CollectionChanged"/> event.
        /// </summary>
        /// <param name="e">The <see cref="NotifyCollectionChangedEventArgs"/> instance containing the event data.</param>
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
