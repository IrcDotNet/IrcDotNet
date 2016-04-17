namespace IrcDotNet
{
    /// <summary>
    ///     Stores information about a particular server in an IRC network.
    /// </summary>
    /// <threadsafety static="true" instance="false" />
    public struct IrcServerInfo
    {
        /// <summary>
        ///     The host name of the server.
        /// </summary>
        private string HostName;

        /// <summary>
        ///     The hop count of the server from the local server.
        /// </summary>
        private int? HopCount;

        /// <summary>
        ///     A string containing arbitrary information about the server.
        /// </summary>
        private string Info;

        /// <summary>
        ///     Initializes a new instance of the <see cref="IrcServerInfo" /> class with the specified properties.
        /// </summary>
        /// <param name="hostName">The host name of the server.</param>
        /// <param name="hopCount">The hop count of the server from the local server.</param>
        /// <param name="info">A string containing arbitrary information about the server.</param>
        public IrcServerInfo(string hostName, int? hopCount, string info)
        {
            HostName = hostName;
            HopCount = hopCount;
            Info = info;
        }
    }
}