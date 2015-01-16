using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    using Collections;

    /// <summary>
    /// Represents a collection of <see cref="IrcChannelUser"/> objects.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    /// <seealso cref="IrcChannelUser"/>
    public class IrcChannelUserCollection : ReadOnlyCollection<IrcChannelUser>
    {
        private IrcChannel channel;
        
        internal IrcChannelUserCollection(IrcChannel channel, IList<IrcChannelUser> list)
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
    }
}
