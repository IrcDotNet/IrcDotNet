using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcDotNet.Tests
{
    // TODO: Test Kick and Ban methods of IrcChannelUser.
    [TestClass()]
    public class IrcClientTestSet : InterdependentTestSet<IrcClientTestState>
    {
        // Test parameters specific to IRC network.
        private const string serverHost = "irc.freenode.net";
        private const string serverPassword = null;
        private const string realName = "IRC.NET Test Bot";

        // Threading events used to signify when client raises event.
        private static AutoResetEvent connectedEvent = null;
        private static AutoResetEvent disconnectedEvent = null;
        private static AutoResetEvent errorEvent = null;
        private static AutoResetEvent registeredEvent = null;
        private static AutoResetEvent motdReceivedEvent = null;
        private static AutoResetEvent nickNameChangedEvent = null;
        private static AutoResetEvent localUserModeChangedEvent = null;
        private static AutoResetEvent channelJoinedEvent = null;
        private static AutoResetEvent channelPartedEvent = null;
        private static AutoResetEvent channelUsersListReceivedEvent = null;
        private static AutoResetEvent channelModeChangedEvent = null;
        private static AutoResetEvent channelUserKickedEvent = null;

        private static IrcClient client;
        private static string nickName;
        private static string userName;
        private static string testChannelName;

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            client = new IrcClient();
            client.Connected += client_Connected;
            client.ConnectFailed += client_ConnectFailed;
            client.Disconnected += client_Disconnected;
            client.Error += client_Error;
            client.ProtocolError += client_ProtocolError;
            client.Registered += client_Registered;
            client.MotdReceived += client_MotdReceived;
            client.ChannelJoined += client_ChannelJoined;
            client.ChannelParted += client_ChannelParted;

            // Initialise wait handles for all events.
            GetAllWaitHandlesFields().ForEach(fieldInfo => fieldInfo.SetValue(null, new AutoResetEvent(false)));

            // Nick name length limit on irc.freenode.net is 16 chars.
            var userRandomId = Guid.NewGuid().ToString().Substring(0, 8);
            nickName = string.Format("itb-{0}", userRandomId);
            userName = string.Format("itb-{0}", userRandomId);
            client.Connect(serverHost, serverPassword, nickName, userName, realName);

            OnClassInitialize(testContext, IrcClientTestState.Initialised);
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            if (client != null)
            {
                client.Dispose();
                client = null;
            }

            // Dispose wait handles for all events.
            GetAllWaitHandlesFields().ForEach(fieldInfo => ((IDisposable)fieldInfo.GetValue(null)).Dispose());

            OnClassCleanup();
        }

        private static IEnumerable<FieldInfo> GetAllWaitHandlesFields()
        {
            return typeof(IrcClientTestSet).GetFields(BindingFlags.Static | BindingFlags.NonPublic)
                .Where(fieldInfo => typeof(EventWaitHandle).IsAssignableFrom(fieldInfo.FieldType));
        }

        private static void client_Connected(object sender, EventArgs e)
        {
            if (connectedEvent != null)
                connectedEvent.Set();
        }

        private static void client_ConnectFailed(object sender, IrcErrorEventArgs e)
        {
            if (connectedEvent != null)
                connectedEvent.Set();
        }

        private static void client_Disconnected(object sender, EventArgs e)
        {
            if (disconnectedEvent != null)
                disconnectedEvent.Set();
        }

        private static void client_Error(object sender, IrcErrorEventArgs e)
        {
            if (errorEvent != null)
                errorEvent.Set();

            Debug.WriteLine("Protocol error: " + e.Error.Message);
        }

        private static void client_ProtocolError(object sender, EventArgs e)
        {
            if (errorEvent != null)
                errorEvent.Set();
        }

        private static void client_Registered(object sender, EventArgs e)
        {
            client.LocalUser.ModesChanged += new EventHandler<EventArgs>(client_LocalUser_ModesChanged);
            client.LocalUser.NickNameChanged += new EventHandler<EventArgs>(client_LocalUser_NickNameChanged);

            if (registeredEvent != null)
                registeredEvent.Set();
        }

        private static void client_MotdReceived(object sender, EventArgs e)
        {
            if (motdReceivedEvent != null)
                motdReceivedEvent.Set();
        }

        private static void client_ChannelJoined(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived += Channel_UsersListReceived;
            e.Channel.ModesChanged += Channel_ModesChanged;
            e.Channel.TopicChanged += Channel_TopicChanged;
            e.Channel.UserJoined += Channel_UserJoined;
            e.Channel.UserParted += Channel_UserParted;
            e.Channel.UserKicked += Channel_UserKicked;

            if (channelJoinedEvent != null)
                channelJoinedEvent.Set();
        }

        private static void client_ChannelParted(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived -= Channel_UsersListReceived;
            e.Channel.ModesChanged -= Channel_ModesChanged;
            e.Channel.TopicChanged -= Channel_TopicChanged;
            e.Channel.UserJoined -= Channel_UserJoined;
            e.Channel.UserParted -= Channel_UserParted;
            e.Channel.UserKicked -= Channel_UserKicked;

            if (channelPartedEvent != null)
                channelPartedEvent.Set();
        }

        private static void client_LocalUser_ModesChanged(object sender, EventArgs e)
        {
            if (localUserModeChangedEvent != null)
                localUserModeChangedEvent.Set();
        }

        private static void client_LocalUser_NickNameChanged(object sender, EventArgs e)
        {
            if (nickNameChangedEvent != null)
                nickNameChangedEvent.Set();
        }

        private static void Channel_UsersListReceived(object sender, EventArgs e)
        {
            if (channelUsersListReceivedEvent != null)
                channelUsersListReceivedEvent.Set();
        }

        private static void Channel_ModesChanged(object sender, EventArgs e)
        {
            if (channelModeChangedEvent != null)
                channelModeChangedEvent.Set();
        }

        private static void Channel_TopicChanged(object sender, EventArgs e)
        {
            //
        }

        private static void Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void Channel_UserParted(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void Channel_UserKicked(object sender, IrcChannelUserEventArgs e)
        {
            if (channelUserKickedEvent != null)
                channelUserKickedEvent.Set();
        }

        public IrcClientTestSet()
            : base()
        {
        }

        [TestInitialize()]
        public override void TestInitialize()
        {
            base.TestInitialize();
        }

        [TestCleanup()]
        public override void TestCleanup()
        {
            base.TestCleanup();
        }

        [TestMethod(), TestDependency(IrcClientTestState.Initialised, SetState = IrcClientTestState.Connected)]
        public void ConnectTest()
        {
            Assert.IsTrue(WaitForEventOrDisconnected(connectedEvent, 5000), "Connection to server timed out.");
            Assert.IsTrue(client.IsConnected, "Failed to connect to server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Connected, UnsetState = IrcClientTestState.Connected)]
        public void DisconnectTest()
        {
            client.Disconnect();
            Assert.IsTrue(disconnectedEvent.WaitOne(5000), "Failed to disconnect from server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Connected, SetState = IrcClientTestState.Registered)]
        public void RegisterTest()
        {
            Assert.IsTrue(WaitForEventOrDisconnected(registeredEvent, 20000),
                "Failed to register connection with server.");
            Assert.AreEqual(nickName, client.LocalUser.NickName, "Nick name was not correctly set.");
            Assert.AreEqual(userName, client.LocalUser.UserName, "User name was not correctly set.");
            Assert.AreEqual(realName, client.LocalUser.RealName, "Real name was not correctly set.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void MotdTest()
        {
            Assert.IsTrue(WaitForEventOrDisconnected(motdReceivedEvent, 5000), "Did not receive MOTD from server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void ChangeNickTest()
        {
            Assert.AreEqual(nickName, client.NickName, "Nick name before update is incorrect.");
            nickName += "-2";
            client.NickName = nickName;
            Assert.IsTrue(WaitForEventOrDisconnected(nickNameChangedEvent, 10000), "Failed to change nick name.");
            Assert.AreEqual(nickName, client.NickName, "Updated nick name is incorrect.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void LocalUserModeTest()
        {
            Assert.IsTrue(client.LocalUser.Modes.Count == 0);
            client.LocalUser.SetModes("+w");
            Assert.IsTrue(WaitForEventOrDisconnected(localUserModeChangedEvent, 10000),
                "Failed to change local user mode.");
            Assert.IsTrue(client.LocalUser.Modes.Contains('w'), "Local user mode is unchanged.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered, SetState = IrcClientTestState.InChannel)]
        public void JoinChannelTest()
        {
            // Generate random name of channel that is highly like to be empty.
            testChannelName = string.Format("#ircsil-test-{0}", Guid.NewGuid().ToString().Substring(0, 13));
            client.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForEventOrDisconnected(channelJoinedEvent, 10000), "Could not join channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelUsersListReceivedTest()
        {
            var channel = client.Channels.Single(c => c.Name == testChannelName);
            Assert.IsTrue(WaitForEventOrDisconnected(channelUsersListReceivedEvent, 10000),
                "Did not receive users list from channel.");
            Assert.IsTrue(channel.Users.Count == 1, "Channel has unexpected number of users.");
            Assert.AreEqual(client.LocalUser, channel.Users[0].User, "Local user does not appear in channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelModeTest()
        {
            var channel = client.Channels.Single(c => c.Name == testChannelName);
            Assert.IsTrue(WaitForEventOrDisconnected(channelModeChangedEvent, 10000),
                "Channel mode was not initialised.");
            Assert.IsTrue(channel.Modes.Contains('n') && channel.Modes.Contains('s'),
                "Channel mode is not initially 'ns'.");
            Assert.IsFalse(channel.Modes.Contains('i'), "Channel already has mode 'i'.");
            channel.SetModes("+i");
            Assert.IsTrue(WaitForEventOrDisconnected(channelModeChangedEvent, 10000), "Channel mode was not changed.");
            Assert.IsTrue(channel.Modes.Contains('i'), "Channel does not have mode 'i'.");
            channel.SetModes("-i");
            Assert.IsTrue(WaitForEventOrDisconnected(channelModeChangedEvent, 10000), "Channel mode was not changed.");
            Assert.IsFalse(channel.Modes.Contains('i'), "Channel still has mode 'i'.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelLocalUserModeTest()
        {
            // Local user should already have 'o' mode on join.
            var channel = client.Channels.Single(c => c.Name == testChannelName);
            Assert.IsTrue(channel.Users[0].Modes.Contains('o'),
                "Local user does not initially have 'o' mode in channel.");
            channel.Users[0].Voice();
            Assert.IsFalse(channel.Users[0].Modes.Contains('v'),
                "Local user does not have 'v' mode in channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel, UnsetState = IrcClientTestState.InChannel)]
        public void ChannelLocalUserKickTest()
        {
            var channel = client.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.GetChannelUser(client.LocalUser);
            Assert.IsTrue(channel != null, "Cannot find local user in channel.");
            channelUser.Kick();
            Assert.IsTrue(WaitForEventOrDisconnected(channelUserKickedEvent, 10000),
                "Could not kick self from channel.");
            CollectionAssert.DoesNotContain(client.Channels, channel,
                "Collection of channels still contain channel from which kicked.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered, SetState = IrcClientTestState.InChannel)]
        public void RejoinChannelTest()
        {
            client.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForEventOrDisconnected(channelJoinedEvent, 10000), "Could not rejoin channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel, UnsetState = IrcClientTestState.InChannel)]
        public void PartChannelTest()
        {
            client.Channels.Part(testChannelName);
            Assert.IsTrue(WaitForEventOrDisconnected(channelPartedEvent, 10000), "Could not part channel.");
        }

        protected override void CheckTestState(IrcClientTestState requiredState)
        {
            Assert.IsTrue((CurrentState & requiredState) == requiredState, string.Format(
                "Test run is not in reqired state, '{0}'.", requiredState));
        }

        protected override IrcClientTestState GetNewTestState(IrcClientTestState setState,
            IrcClientTestState unsetState)
        {
            var newState = CurrentState;
            if (setState != default(IrcClientTestState))
                newState |= setState;
            if (unsetState != default(IrcClientTestState))
                newState &= ~unsetState;
            return newState;
        }

        private bool WaitForEventOrDisconnected(WaitHandle eventHandle, int millisecondsTimeout = Timeout.Infinite)
        {
            // Wait for specified event, disconnection, or timout (whichever occurs first).
            var setEventIndex = WaitHandle.WaitAny(new[] { eventHandle, disconnectedEvent }, millisecondsTimeout);
            // Fail test if timeout occurred or client was disconnected.
            if (setEventIndex == WaitHandle.WaitTimeout)
                Assert.Fail("Timed out while waiting for event.");
            else if (setEventIndex == 1)
                Assert.Fail("Unexpectedly disconnected from server.");
            return setEventIndex != WaitHandle.WaitTimeout;
        }
    }

    [Flags()]
    public enum IrcClientTestState
    {
        None = 0,
        Initialised = 1 << 0,
        Connected = 1 << 1,
        Registered = 1 << 2,
        InChannel = 1 << 3,
    }
}
