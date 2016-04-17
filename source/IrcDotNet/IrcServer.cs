namespace IrcDotNet
{
    /// <summary>
    ///     Represents an IRC server from the view of a particular client.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public class IrcServer : IIrcMessageSource
    {
        internal IrcServer(string hostName)
        {
            HostName = hostName;
        }

        /// <summary>
        ///     Gets the host name of the server.
        /// </summary>
        /// <value>The host name of the server.</value>
        public string HostName { get; }

        #region IIrcMessageSource Members

        string IIrcMessageSource.Name
        {
            get { return HostName; }
        }

        #endregion

        /// <summary>
        ///     Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
        public override string ToString()
        {
            return HostName;
        }
    }
}