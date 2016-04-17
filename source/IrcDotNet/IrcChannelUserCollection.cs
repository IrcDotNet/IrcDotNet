using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents a collection of <see cref="IrcChannelUser" /> objects.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    /// <seealso cref="IrcChannelUser" />
    public class IrcChannelUserCollection : ReadOnlyCollection<IrcChannelUser>
    {
        internal IrcChannelUserCollection(IrcChannel channel, IList<IrcChannelUser> list)
            : base(list)
        {
            Channel = channel;
        }

        /// <summary>
        ///     Gets the channel to which the collection of channel users belongs.
        /// </summary>
        /// <value>The channel to which the collection of channel users belongs..</value>
        public IrcChannel Channel { get; }

        /// <summary>
        ///     Gets a collection of all users that correspond to the channel users in the collection.
        /// </summary>
        /// <returns>A collection of users.</returns>
        public IEnumerable<IrcUser> GetUsers()
        {
            return Items.Select(channelUser => channelUser.User);
        }
    }
}