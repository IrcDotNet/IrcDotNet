### 0.6.0

The project has been restructured around DNX and the new .NET Core libraries. This release targets .NET 4.0 and higher, as well as DNX4.5 and higher. Cross-platform compatibility is improved with the open source DNX command line tools.

Added this release is the TwitchIrcClient which supports the twitch.tv IRC chat protocol.

### 0.5.0

Various refactoring improvements have been made, along with a number of bug fixes. Some usability issues have also been resolved.

### 0.4.1

This release primarily introduces thread-safety in the IrcClient and CtcpClient classes; this is important particularly in UI scenarios. Minor stability improvements have also been made. The TwitterBot sample project has been updated to use v2 of the TweetSharp library and has several bug fixes.

 * IrcClient and CtcpClient classes are now properly thread-safe (including IDisposable implementaiton).
 * IrcClient now handles replies to STATS messages (server statistics).
 * Improved XML documentation.
 * TwitterBot sample now uses TweetSharp v2 library.
 * Created NuGet package for library and samples.
 * Minor bug fixes; see commit log.
 
### 0.4.0

The library can now be built for the Silverlight 4 framework. Asynchronous I/O is now used for network communication in order to avoid multi-threading and increase efficiency. The feature set and API remains largely unchanged, though a significant number of minor bug fixes have been made. Finally, a second sample (an IRC bot that lets each user utilise their Twitter account via messaging) has been added, with documentation.

 * All network I/O is now done asynchronously (using completion ports).
 * Added Silverlight 4 build.
 * Created and documented 'Twitter Bot' sample project.
 * Improved documentation for library.
 * Minor bug fixes; see commit log.

### 0.3.0

This release represents a major milestone in development. Full API documentation has also been included with this release. Functionality relating to the IRC protocol itself is now virtually complete and has proven to be very stable. A CTCP client that operates over the IRC client has also been added, along with a handful of the most commonly-used commands. Finally, there have been various (though largely minor) improvements to the API, and several important bug fixes.

 * Created API documentation (in Microsoft HTML Help v1 and new Microsoft Help Viewer formats).
 * Added support for CTCP (Client-To-Client protocol) over IRC. Supports messages such as PING, VERSION, TIME, and ACTION.
 * Sending and receiving of messages and notices in arbitrary text encodings is now fully supported.
 * Added new functionality for querying the server/network; can now retrieve network/server statistics and return list of channels.
 * Expanded existing unit tests and created more for new features.
 * Minor bug fixes; see commit log.

### 0.2.0

This is the first non-experimental release and shows considerable improvements in stability over previous releases. There are only a few API changes since the 0.1 version, though XML documentation is now complete. A sample project (MarkovChainTextBot), which demonstrates how the library can be used to construct a bot that operates on an arbitrary selection of networks is also included with comments and documentation. In addition, a few message message senders and processors have been added. Minor bug fixes, mainly involving message processing, have been made since the 0.1 release.
 * Completed documentation for IrcDotNet and IrcDotNet.Common assemblies.
 * Added MarkovChainTextBot sample, including comments and documentation. Sample demonstrates a Markov text generator running as a bot on multiple channels on multiple IRC networks.
 * Minor bug fixes; see commit log.
 
### 0.1.1

This is an incremental release from 0.1, but with a number of significant improvements, including the addition of many XML comments. Basic client functionality is significantly mroe stable than in the 0.1.0 release, with a number of minor new features. The API itself has no changely hugely, so there has been some minor restructuring as well as renaming of functions/types. Among the most prominent new features and changes are the following:

 * IrcClient now runs a write loop along side the read loop while the connection is active. This is used for limiting the rate of outgoing message. The IIrcFloodProtector interface (the IrcStandardFloodPreventor class is the default implementation provided) may be used for limiting the rate of an IrcClient and thus preventing flooding the server.
 * IrcLocalUser class now raises event when the local user joins or leaves a channel, rather than IrcClient.
 * Redesigned the testing framework, which now uses the TestStateManager for controlling the allowed actions.
 * Error messages are now defined as string resources in the project RESX file, rather than as constants in classes.

### 0.1.0 

This is the initial release of the library. It supports all common RFC 2812 (IRC client protocol) features, as well as several standard non-RFC ones. The source includes an ordered set of unit tests that involve communication with a real IRC server in order to test the client functionality. The features implemented so far that are stable and well-tested include the following:

 * Connecting to an IRC server on an arbitrary port. Server "bounces" are notified to the user.
 * Registration of the user with the IRC server.
 * Changing the (global) mode of the local user.
 * Changing the nick name of the local user.
 * Tracking all known users on the server, and storing information about them.
 * Joining and parting channels, including channels with keys.
 * Tracking the list of users that are currently members of a channel.
 
 * Setting and retrieving channel modes (e.g. m, I) as well as channel modes that apply to users (e.g. o, v).
 * Kicking a user from a channel.
 * Sending and receiving message and notices to/from other users, channels, and the server.
 * Performing a "whois" query on any user and storing all received information.

Note that while the API is still subject to change, the current design (which may be viewed generally from the class diagram) is quite unlikely to be significantly altered.
