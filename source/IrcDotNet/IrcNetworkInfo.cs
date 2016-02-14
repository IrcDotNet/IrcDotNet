using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Stores information about a specific IRC network.
    /// </summary>
    public struct IrcNetworkInfo
    {
        /// <summary>
        /// The number of visible users on the network.
        /// </summary>
        public int? VisibleUsersCount;

        /// <summary>
        /// The number of invisible users on the network.
        /// </summary>
        public int? InvisibleUsersCount;

        /// <summary>
        /// The number of servers in the network.
        /// </summary>
        public int? ServersCount;

        /// <summary>
        /// The number of operators on the network.
        /// </summary>
        public int? OperatorsCount;

        /// <summary>
        /// The number of unknown connections to the network.
        /// </summary>
        public int? UnknownConnectionsCount;

        /// <summary>
        /// The number of channels that currently exist on the network.
        /// </summary>
        public int? ChannelsCount;

        /// <summary>
        /// The number of clients connected to the server.
        /// </summary>
        public int? ServerClientsCount;

        /// <summary>
        /// The number of others servers connected to the server.
        /// </summary>
        public int? ServerServersCount;

        /// <summary>
        /// The number of services connected to the server.
        /// </summary>
        public int? ServerServicesCount;
    }
}
