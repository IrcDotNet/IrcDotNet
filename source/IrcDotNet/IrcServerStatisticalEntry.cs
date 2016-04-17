using System.Collections.Generic;

namespace IrcDotNet
{
    /// <summary>
    ///     Stores a statistical entry for an IRC server.
    /// </summary>
    public struct IrcServerStatisticalEntry
    {
        /// <summary>
        ///     The type of the statistical entry.
        /// </summary>
        public int Type;

        /// <summary>
        ///     The list of parameters of the statistical entry.
        /// </summary>
        public IList<string> Parameters;
    }

    /// <summary>
    ///     Defines the types of statistical entries for an IRC server.
    /// </summary>
    /// <remarks>
    ///     These entry types correspond to the STATS replies described in the RFC for the IRC protocol.
    /// </remarks>
    public enum IrcServerStatisticalEntryCommonType
    {
        /// <summary>
        ///     An active connection to the server.
        /// </summary>
        Connection,

        /// <summary>
        ///     A command supported by the server.
        /// </summary>
        Command,

        /// <summary>
        ///     A server to which the local server may connect.
        /// </summary>
        AllowedServerConnect,

        /// <summary>
        ///     A server from which the local server may accept connections.
        /// </summary>
        AllowedServerAccept,

        /// <summary>
        ///     A client that may connect to the server.
        /// </summary>
        AllowedClient,

        /// <summary>
        ///     A client that is banned from connecting to the server.
        /// </summary>
        BannedClient,

        /// <summary>
        ///     A connection class defined by the server.
        /// </summary>
        ConnectionClass,

        /// <summary>
        ///     The leaf depth of a server in the network.
        /// </summary>
        LeafDepth,

        /// <summary>
        ///     The uptime of the server.
        /// </summary>
        Uptime,

        /// <summary>
        ///     An operator on the server.
        /// </summary>
        AllowedOperator,

        /// <summary>
        ///     A hub server within the network.
        /// </summary>
        HubServer
    }
}