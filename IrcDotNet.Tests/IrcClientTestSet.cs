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
    // Set of all tests for IRC client.
    [TestClass()]
    public class IrcClientTestSet : InterdependentTestSet<IrcClientTestState>
    {
        // Test parameters specific to IRC network.
        private const string serverHostName = "irc.freenode.net";
        private const string serverPassword = null;
        private const string realName = "IRC.NET Test Bot";

        // Data used for testing.
        private const string testMessage1 = "This is the first test message.";
        private const string testMessage2 = "This is the second test message.";

        // Threading events used to signify when client raises event.
#pragma warning disable 0649
        private static AutoResetEvent client1ConnectedEvent;
        private static AutoResetEvent client1DisconnectedEvent;
        private static AutoResetEvent client1ErrorEvent;
        private static AutoResetEvent client1RegisteredEvent;
        private static AutoResetEvent client1MotdReceivedEvent;
        private static AutoResetEvent client1LocalUserNickNameChangedEvent;
        private static AutoResetEvent client1LocalUserModeChangedEvent;
        private static AutoResetEvent client1LocalUserMessageSentEvent;
        private static AutoResetEvent client1LocalUserNoticeSentEvent;
        private static AutoResetEvent client1LocalUserMessageReceivedEvent;
        private static AutoResetEvent client1LocalUserNoticeReceivedEvent;
        private static AutoResetEvent client1ChannelJoinedEvent;
        private static AutoResetEvent client1ChannelPartedEvent;
        private static AutoResetEvent client1WhoIsReplyReceived;
        private static AutoResetEvent client1WhoWasReplyReceived;
        private static AutoResetEvent client1ChannelUsersListReceivedEvent;
        private static AutoResetEvent client1ChannelModeChangedEvent;
        private static AutoResetEvent client1ChannelUserModeChangedEvent;
        private static AutoResetEvent client1ChannelUserKickedEvent;
        private static AutoResetEvent client1ChannelMessageReceivedEvent;
        private static AutoResetEvent client1ChannelNoticeReceivedEvent;
        
        private static AutoResetEvent client2ConnectedEvent;
        private static AutoResetEvent client2DisconnectedEvent;
        private static AutoResetEvent client2ErrorEvent;
        private static AutoResetEvent client2RegisteredEvent;
        private static AutoResetEvent client2LocalUserMessageSentEvent;
        private static AutoResetEvent client2LocalUserNoticeSentEvent;
        private static AutoResetEvent client2LocalUserMessageReceivedEvent;
        private static AutoResetEvent client2LocalUserNoticeReceivedEvent;
        private static AutoResetEvent client2ChannelJoinedEvent;
        private static AutoResetEvent client2ChannelPartedEvent;
        private static AutoResetEvent client2ChannelMessageReceivedEvent;
        private static AutoResetEvent client2ChannelNoticeReceivedEvent;
#pragma warning restore 0649

        // Primary and secondary client, with associated user information.
        private static IrcClient client1, client2;
        private static string nickName1, nickName2;
        private static string userName1, userName2;
        private static string testChannelName;

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            client1 = new IrcClient();
            client1.Connected += client1_Connected;
            client1.ConnectFailed += client1_ConnectFailed;
            client1.Disconnected += client1_Disconnected;
            client1.Error += client1_Error;
            client1.ProtocolError += client1_ProtocolError;
            client1.Registered += client1_Registered;
            client1.MotdReceived += client1_MotdReceived;
            client1.ChannelJoined += client1_ChannelJoined;
            client1.ChannelParted += client1_ChannelParted;
            client1.WhoIsReplyReceived += client1_WhoIsReplyReceived;
            client1.WhoWasReplyReceived += client1_WhoWasReplyReceived;

            client2 = new IrcClient();
            client2.Connected += client2_Connected;
            client2.ConnectFailed += client2_ConnectFailed;
            client2.Disconnected += client2_Disconnected;
            client2.Error += client2_Error;
            client2.ProtocolError += client2_ProtocolError;
            client2.Registered += client2_Registered;
            client2.ChannelJoined += client2_ChannelJoined;
            client2.ChannelParted += client2_ChannelParted;

            // Initialise wait handles for all events.
            GetAllWaitHandlesFields().ForEach(fieldInfo => fieldInfo.SetValue(null, new AutoResetEvent(false)));

            // Nick name length limit on irc.freenode.net is 16 chars.
            Func<string> getRandomUserId = () => Guid.NewGuid().ToString().Substring(0, 8);
            nickName1 = userName1 = string.Format("itb-{0}", getRandomUserId());
            nickName2 = userName2 = string.Format("itb-{0}", getRandomUserId());
            Debug.WriteLine("Cllient 1 user has nick name '{0}' and user name '{1}'.", nickName1, userName1);
            Debug.WriteLine("Cllient 2 user has nick name '{0}' and user name '{1}'.", nickName2, userName2);
            client1.Connect(serverHostName, serverPassword, nickName1, userName1, realName);
            client2.Connect(serverHostName, serverPassword, nickName2, userName2, realName);

            OnClassInitialize(testContext, IrcClientTestState.Initialised);
        }

        [ClassCleanup()]
        public static void ClassCleanup()
        {
            if (client1 != null)
            {
                client1.Dispose();
                client1 = null;
            }
            if (client2 != null)
            {
                client2.Dispose();
                client2 = null;
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

        #region Client 1 Event Handlers

        private static void client1_Connected(object sender, EventArgs e)
        {
            if (client1ConnectedEvent != null)
                client1ConnectedEvent.Set();
        }

        private static void client1_ConnectFailed(object sender, IrcErrorEventArgs e)
        {
            if (client1ConnectedEvent != null)
                client1ConnectedEvent.Set();
        }

        private static void client1_Disconnected(object sender, EventArgs e)
        {
            if (client1DisconnectedEvent != null)
                client1DisconnectedEvent.Set();
        }

        private static void client1_Error(object sender, IrcErrorEventArgs e)
        {
            if (client1ErrorEvent != null)
                client1ErrorEvent.Set();

            Debug.Fail("Protocol error: " + e.Error.Message);
        }

        private static void client1_ProtocolError(object sender, EventArgs e)
        {
            //
        }

        private static void client1_Registered(object sender, EventArgs e)
        {
            client1.LocalUser.ModesChanged += client1_LocalUser_ModesChanged;
            client1.LocalUser.NickNameChanged += client1_LocalUser_NickNameChanged;
            client1.LocalUser.MessageSent += client1_LocalUser_MessageSent;
            client1.LocalUser.NoticeSent += client1_LocalUser_NoticeSent;
            client1.LocalUser.MessageReceived += client1_LocalUser_MessageReceived;
            client1.LocalUser.NoticeReceived += client1_LocalUser_NoticeReceived;

            if (client1RegisteredEvent != null)
                client1RegisteredEvent.Set();
        }

        private static void client1_MotdReceived(object sender, EventArgs e)
        {
            if (client1MotdReceivedEvent != null)
                client1MotdReceivedEvent.Set();
        }

        private static void client1_ChannelJoined(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived += client1_Channel_UsersListReceived;
            e.Channel.ModesChanged += client1_Channel_ModesChanged;
            e.Channel.TopicChanged += client1_Channel_TopicChanged;
            e.Channel.UserJoined += client1_Channel_UserJoined;
            e.Channel.UserParted += client1_Channel_UserParted;
            e.Channel.UserKicked += client1_Channel_UserKicked;
            e.Channel.MessageReceived += client1_Channel_MessageReceived;
            e.Channel.NoticeReceived += client1_Channel_NoticeReceived;

            if (client1ChannelJoinedEvent != null)
                client1ChannelJoinedEvent.Set();
        }

        private static void client1_ChannelParted(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived -= client1_Channel_UsersListReceived;
            e.Channel.ModesChanged -= client1_Channel_ModesChanged;
            e.Channel.TopicChanged -= client1_Channel_TopicChanged;
            e.Channel.UserJoined -= client1_Channel_UserJoined;
            e.Channel.UserParted -= client1_Channel_UserParted;
            e.Channel.UserKicked -= client1_Channel_UserKicked;
            e.Channel.MessageReceived -= client1_Channel_MessageReceived;

            if (client1ChannelPartedEvent != null)
                client1ChannelPartedEvent.Set();
        }

        private static void client1_WhoIsReplyReceived(object sender, IrcUserEventArgs e)
        {
            if (client1WhoIsReplyReceived != null)
                client1WhoIsReplyReceived.Set();
        }

        private static void client1_WhoWasReplyReceived(object sender, EventArgs e)
        {
            if (client1WhoWasReplyReceived != null)
                client1WhoWasReplyReceived.Set();
        }

        private static void client1_LocalUser_ModesChanged(object sender, EventArgs e)
        {
            if (client1LocalUserModeChangedEvent != null)
                client1LocalUserModeChangedEvent.Set();
        }

        private static void client1_LocalUser_NickNameChanged(object sender, EventArgs e)
        {
            if (client1LocalUserNickNameChangedEvent != null)
                client1LocalUserNickNameChangedEvent.Set();
        }

        private static void client1_LocalUser_MessageSent(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserMessageSentEvent != null)
                client1LocalUserMessageSentEvent.Set();
        }

        private static void client1_LocalUser_NoticeSent(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserNoticeSentEvent != null)
                client1LocalUserNoticeSentEvent.Set();
        }

        private static void client1_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserMessageReceivedEvent != null)
                client1LocalUserMessageReceivedEvent.Set();
        }

        private static void client1_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserNoticeReceivedEvent != null)
                client1LocalUserNoticeReceivedEvent.Set();
        }

        private static void client1_Channel_UsersListReceived(object sender, EventArgs e)
        {
            if (client1ChannelUsersListReceivedEvent != null)
                client1ChannelUsersListReceivedEvent.Set();
        }

        private static void client1_Channel_ModesChanged(object sender, EventArgs e)
        {
            if (client1ChannelModeChangedEvent != null)
                client1ChannelModeChangedEvent.Set();
        }

        private static void client1_Channel_TopicChanged(object sender, EventArgs e)
        {
            //
        }

        private static void client1_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void client1_Channel_UserParted(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void client1_Channel_UserKicked(object sender, IrcChannelUserEventArgs e)
        {
            if (client1ChannelUserKickedEvent != null)
                client1ChannelUserKickedEvent.Set();
        }

        private static void client1_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1ChannelMessageReceivedEvent != null)
                client1ChannelMessageReceivedEvent.Set();
        }

        private static void client1_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1ChannelNoticeReceivedEvent != null)
                client1ChannelNoticeReceivedEvent.Set();
        }

        #endregion

        #region Client 2 Event Handlers

        private static void client2_Connected(object sender, EventArgs e)
        {
            if (client2ConnectedEvent != null)
                client2ConnectedEvent.Set();
        }

        private static void client2_ConnectFailed(object sender, IrcErrorEventArgs e)
        {
            if (client2ConnectedEvent != null)
                client2ConnectedEvent.Set();
        }

        private static void client2_Disconnected(object sender, EventArgs e)
        {
            if (client2DisconnectedEvent != null)
                client2DisconnectedEvent.Set();
        }

        private static void client2_Error(object sender, IrcErrorEventArgs e)
        {
            if (client2ErrorEvent != null)
                client2ErrorEvent.Set();
        }

        private static void client2_ProtocolError(object sender, IrcProtocolErrorEventArgs e)
        {
            //
        }

        private static void client2_Registered(object sender, EventArgs e)
        {
            client2.LocalUser.MessageSent += client2_LocalUser_MessageSent;
            client2.LocalUser.NoticeSent += client2_LocalUser_NoticeSent;
            client2.LocalUser.MessageReceived += client2_LocalUser_MessageReceived;
            client2.LocalUser.NoticeReceived += client2_LocalUser_NoticeReceived;

            if (client2RegisteredEvent != null)
                client2RegisteredEvent.Set();
        }

        private static void client2_ChannelJoined(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined += client2_Channel_UserJoined;
            e.Channel.UserParted += client2_Channel_UserParted;
            e.Channel.MessageReceived += client2_Channel_MessageReceived;
            e.Channel.NoticeReceived += client2_Channel_NoticeReceived;

            if (client2ChannelJoinedEvent != null)
                client2ChannelJoinedEvent.Set();
        }

        private static void client2_ChannelParted(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined -= client2_Channel_UserJoined;
            e.Channel.UserParted -= client2_Channel_UserParted;
            e.Channel.MessageReceived -= client2_Channel_MessageReceived;
            e.Channel.NoticeReceived -= client2_Channel_NoticeReceived;

            if (client2ChannelPartedEvent != null)
                client2ChannelPartedEvent.Set();
        }

        private static void client2_LocalUser_MessageSent(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserMessageSentEvent != null)
                client2LocalUserMessageSentEvent.Set();
        }

        private static void client2_LocalUser_NoticeSent(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserNoticeSentEvent != null)
                client2LocalUserNoticeSentEvent.Set();
        }

        private static void client2_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserMessageReceivedEvent != null)
                client2LocalUserMessageReceivedEvent.Set();
        }

        private static void client2_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserNoticeReceivedEvent != null)
                client2LocalUserNoticeReceivedEvent.Set();
        }

        private static void client2_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void client2_Channel_UserParted(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void client2_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2ChannelMessageReceivedEvent != null)
                client2ChannelMessageReceivedEvent.Set();
        }

        private static void client2_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2ChannelNoticeReceivedEvent != null)
                client2ChannelNoticeReceivedEvent.Set();
        }
        #endregion

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
            Assert.IsTrue(WaitForClientEvent(client1ConnectedEvent, 5000), "Client 1 connection to server timed out.");
            Assert.IsTrue(client1.IsConnected, "Client 1 failed to connect to server.");

            Assert.IsTrue(WaitForClientEvent(client2ConnectedEvent, 5000), "Client 2 connection to server timed out.");
            Assert.IsTrue(client2.IsConnected, "Client 2 failed to connect to server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Connected, UnsetState = IrcClientTestState.Connected)]
        public void DisconnectTest()
        {
            client1.Disconnect();
            Assert.IsTrue(client1DisconnectedEvent.WaitOne(5000), "Client 1 failed to disconnect from server.");

            client2.Disconnect();
            Assert.IsTrue(client2DisconnectedEvent.WaitOne(5000), "Client 2 failed to disconnect from server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Connected, SetState = IrcClientTestState.Registered)]
        public void RegisterTest()
        {
            Assert.IsTrue(WaitForClientEvent(client1RegisteredEvent, 20000),
                "Client 1 failed to register connection with server.");
            Assert.AreEqual(nickName1, client1.LocalUser.NickName, "Client 1 nick name was not correctly set.");
            Assert.AreEqual(userName1, client1.LocalUser.UserName, "Client 1 user name was not correctly set.");
            Assert.AreEqual(realName, client1.LocalUser.RealName, "Client 1 real name was not correctly set.");

            Assert.IsTrue(WaitForClientEvent(client2RegisteredEvent, 20000),
                "Client 2 failed to register connection with server.");
            Assert.AreEqual(nickName2, client2.LocalUser.NickName, "Client 2 nick name was not correctly set.");
            Assert.AreEqual(userName2, client2.LocalUser.UserName, "Client 2 user name was not correctly set.");
            Assert.AreEqual(realName, client2.LocalUser.RealName, "Client 2 real name was not correctly set.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void MotdTest()
        {
            Assert.IsTrue(WaitForClientEvent(client1MotdReceivedEvent, 5000), "Did not receive MOTD from server.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void ChangeNickTest()
        {
            Assert.AreEqual(nickName1, client1.LocalUser.NickName, "Nick name before update is incorrect.");
            nickName1 += "-2";
            client1.LocalUser.SetNickName(nickName1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserNickNameChangedEvent, 10000), "Failed to change nick name.");
            Assert.AreEqual(nickName1, client1.LocalUser.NickName, "Updated nick name is incorrect.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void LocalUserModeTest()
        {
            Assert.IsTrue(client1.LocalUser.Modes.Count == 0);
            client1.LocalUser.SetModes("+w");
            Assert.IsTrue(WaitForClientEvent(client1LocalUserModeChangedEvent, 10000),
                "Failed to change local user mode.");
            Assert.IsTrue(client1.LocalUser.Modes.Contains('w'), "Local user mode is unchanged.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered, SetState = IrcClientTestState.InChannel)]
        public void JoinChannelTest()
        {
            // Generate random name of channel that has very low probability of already existing.
            testChannelName = string.Format("#ircsil-test-{0}", Guid.NewGuid().ToString().Substring(0, 13));

            client1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Client 1 could not join channel.");
            client2.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client2ChannelJoinedEvent, 10000), "Client 2 could not join channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelUsersListReceivedTest()
        {
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelUsersListReceivedEvent, 10000),
                "Did not receive users list from channel.");
            Assert.IsTrue(channel.Users.Count == 1 || channel.Users.Count == 2, "Channel has unexpected number of users.");
            Assert.AreEqual(client1.LocalUser, channel.Users[0].User, "Local user does not appear in channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelLocalUserModeTest()
        {
            // Local user should already have 'o' mode on joining channel.
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.Users.First();
            Assert.IsTrue(channelUser.Modes.Contains('o'),
                "Local user does not initially have 'o' mode in channel.");
            channelUser.ModesChanged += (sender, e) =>
                {
                    if (client1ChannelUsersListReceivedEvent != null)
                        client1ChannelUsersListReceivedEvent.Set();
                };
            channelUser.Voice();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUsersListReceivedEvent, 10000),
                "Channel mode of local user was not changed to '+v'.");
            Assert.IsTrue(channelUser.Modes.Contains('v'),
                "Local user does not have 'v' mode in channel.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelModeTest()
        {
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Channel mode was not initialised.");
            Assert.IsTrue(channel.Modes.Contains('n') && channel.Modes.Contains('s'),
                "Channel mode is not initially 'ns'.");
            Assert.IsFalse(channel.Modes.Contains('m'), "Channel already has mode 'm'.");
            channel.SetModes("+m");
            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Channel mode was not changed to '+m'.");
            Assert.IsTrue(channel.Modes.Contains('m'), "Channel does not have mode 'm'.");
            channel.SetModes("-m");
            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Channel mode was not changed to '-m'.");
            Assert.IsFalse(channel.Modes.Contains('m'), "Channel still has mode 'm'.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelSendAndReceiveMessage()
        {
            // Send private message to user of client 2.
            client1.LocalUser.SendMessage(client2.LocalUser.NickName, testMessage1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserMessageSentEvent, 5000),
                "Client 1 user did not send private message to client 2 user.");
            Assert.IsTrue(WaitForClientEvent(client2LocalUserMessageReceivedEvent, 5000),
                "Client 2 user did not receive private message from client 1 user.");

            // Send private message to channel.
            var client1Channel = client1.Channels.Single(c => c.Name == testChannelName);
            var client2Channel = client2.Channels.Single(c => c.Name == testChannelName);
            client1.LocalUser.SendMessage(client1Channel, testMessage2);
            Assert.IsTrue(WaitForClientEvent(client2ChannelMessageReceivedEvent, 5000),
                "Client 2 channel did not receive private message from client 1 user.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel)]
        public void ChannelSendAndReceiveNotice()
        {
            // Send notice to user of client 2.
            client1.LocalUser.SendNotice(client2.LocalUser.NickName, testMessage1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserNoticeSentEvent, 5000),
                "Client 1 user did not send notice to client 2 user.");
            Assert.IsTrue(WaitForClientEvent(client2LocalUserNoticeReceivedEvent, 5000),
                "Client 2 user did not receive notice from client 1 user.");

            // Send notice to channel.
            var client1Channel = client1.Channels.Single(c => c.Name == testChannelName);
            var client2Channel = client2.Channels.Single(c => c.Name == testChannelName);
            client1.LocalUser.SendNotice(client1Channel, testMessage2);
            Assert.IsTrue(WaitForClientEvent(client2ChannelNoticeReceivedEvent, 5000),
                "Client 2 channel did not receive notice from client 1 user.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel, UnsetState = IrcClientTestState.InChannel)]
        public void ChannelLocalUserKickTest()
        {
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.GetChannelUser(client1.LocalUser);
            Assert.IsTrue(channel != null, "Cannot find local user in channel.");
            channelUser.Kick();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserKickedEvent, 10000),
                "Could not kick self from channel.");
            CollectionAssert.DoesNotContain(client1.Channels, channel,
                "Collection of channels still contain channel from which kicked.");
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered, SetState = IrcClientTestState.InChannel)]
        public void RejoinChannelTest()
        {
            client1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Could not rejoin channel.");
        }

        // Test requires that client 2 user is currently member of channel.
        [TestMethod(), TestDependency(IrcClientTestState.Registered | IrcClientTestState.InChannel)]
        public void WhoIsTest()
        {
            var whoIsUser = client1.Users.Single(u => u.NickName == client2.LocalUser.NickName);
            client1.WhoIs(whoIsUser.NickName);
            Assert.IsTrue(WaitForClientEvent(client1WhoIsReplyReceived, 10000), "Client 1 did not receive WhoIs reply.");
            Assert.AreEqual(userName2, whoIsUser.NickName);
            Assert.AreEqual(realName, whoIsUser.RealName);
            Assert.IsTrue(whoIsUser.HostName != null && whoIsUser.HostName.Length > 1);
            Assert.AreEqual(serverHostName, whoIsUser.ServerName);
            Assert.IsFalse(whoIsUser.IsOperator);
            var channel = client1.Channels.First();
            Assert.IsTrue(whoIsUser.GetChannelUsers().First().Channel == channel);
        }

        [TestMethod(), TestDependency(IrcClientTestState.Registered)]
        public void WhoWasTest()
        {
            // TODO: Test WhoWas command.
        }

        [TestMethod(), TestDependency(IrcClientTestState.InChannel, UnsetState = IrcClientTestState.InChannel)]
        public void PartChannelTest()
        {
            client1.Channels.Part(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelPartedEvent, 10000), "Client 1 could not part channel.");
            client2.Channels.Part(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client2ChannelPartedEvent, 10000), "Client 2 could not part channel.");
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

        private bool WaitForClientEvent(WaitHandle eventHandle, int millisecondsTimeout = Timeout.Infinite)
        {
            // Wait for specified event, disconnection, error, or timeout (whichever occurs first).
            var setEventIndex = WaitHandle.WaitAny(new[] { eventHandle, client1DisconnectedEvent, client1ErrorEvent,
                client2DisconnectedEvent, client2ErrorEvent}, millisecondsTimeout);

            // Fail test if timeout occurred, client was disconnected, or client error occurred.
            switch (setEventIndex)
            {
                case WaitHandle.WaitTimeout:
                    Assert.Fail("Timed out while waiting for event.");
                    break;
                case 0:
                    return true;
                case 1:
                    Assert.Fail("Client 1 unexpectedly disconnected from server.");
                    break;
                case 2:
                    Assert.Fail("Client 1 encountered local error.");
                    break;
                case 3:
                    Assert.Fail("Client 2 unexpectedly disconnected from server.");
                    break;
                case 4:
                    Assert.Fail("Client 2 encountered local error.");
                    break;
            }
            return false;
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
