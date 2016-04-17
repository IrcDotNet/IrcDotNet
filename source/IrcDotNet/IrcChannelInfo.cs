namespace IrcDotNet
{
    /// <summary>
    ///     Stores information about a particular channel on an IRC network.
    /// </summary>
    public struct IrcChannelInfo
    {
        /// <summary>
        ///     The name of the channel.
        /// </summary>
        public string Name;

        /// <summary>
        ///     The number of visible users in the channel.
        /// </summary>
        public int? VisibleUsersCount;

        /// <summary>
        ///     The current topic of the channel.
        /// </summary>
        public string Topic;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcChannelInfo" /> structure with the specified properties.
        /// </summary>
        /// <param name="name">The name of the channel.</param>
        /// <param name="visibleUsersCount">The number of visible users in the channel.</param>
        /// <param name="topic">The current topic of the channel.</param>
        public IrcChannelInfo(string name, int? visibleUsersCount, string topic)
        {
            Name = name;
            VisibleUsersCount = visibleUsersCount;
            Topic = topic;
        }
    }
}