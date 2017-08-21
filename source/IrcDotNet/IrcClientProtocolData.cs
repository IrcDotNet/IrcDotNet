using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace IrcDotNet
{
    partial class IrcClient
    {        
        // Internal collection of all known servers.
        private Collection<IrcServer> servers;

        // True if connection has been registered with server;
        protected bool isRegistered;

        // Stores information about local user.
        protected IrcLocalUser localUser;

        // Dictionary of protocol features supported by server.
        private Dictionary<string, string> serverSupportedFeatures;

        // Collection of channel modes that apply to users in a channel.
        private Collection<char> channelUserModes;

        // Dictionary of nick name prefixes (keys) and their corresponding channel modes.
        private Dictionary<char, char> channelUserModesPrefixes;

        // Builds MOTD (message of the day) string as it is received from server.
        protected StringBuilder motdBuilder;

        // Information about the IRC network given by the server.
        private IrcNetworkInfo networkInformation;

        // Collection of all currently joined channels.
        private Collection<IrcChannel> channels;

        // Collection of all known users.
        private Collection<IrcUser> users;

        // List of information about channels, returned by server in response to last LIST message.
        private List<IrcChannelInfo> listedChannels;

        // List of other servers to which server links, returned by server in response to last LINKS message.
        private List<IrcServerInfo> listedServerLinks;

        // List of statistical entries, returned by server in response to last STATS message.
        private List<IrcServerStatisticalEntry> listedStatsEntries;
    }
}
