using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using IrcDotNet.Common.Collections;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcDotNet.Tests
{
    // Set of all tests for IRC client.
    [TestClass()]
    public class IrcClientTestSet
    {
        // Test parameters specific to IRC network.
        private const string serverHostName = "irc.freenode.net";
        private const string serverPassword = null;
        private const string realName = "IRC.NET Test Bot";

        // Data used for testing.
        private const string quitMessage = "Client 2 quitting test.";
        private const string testMessage1 = "This is the first test message.";
        private const string testMessage2 = "This is the second test message.";
        private const string spamMessage = "This message is part of an attempt to spam the channel and get booted from the server";
        
        // Threading events used to signify when client raises event.
#pragma warning disable 0649
        private static AutoResetEvent client1ConnectedEvent;
        private static AutoResetEvent client1DisconnectedEvent;
        private static AutoResetEvent client1ErrorEvent;
        private static AutoResetEvent client1RegisteredEvent;
        private static AutoResetEvent client1MotdReceivedEvent;
        private static AutoResetEvent client1LocalUserNickNameChangedEvent;
        private static AutoResetEvent client1LocalUserModeChangedEvent;
        private static AutoResetEvent client1LocalUserIsAwayChangedEvent;
        private static AutoResetEvent client1LocalUserMessageSentEvent;
        private static AutoResetEvent client1LocalUserNoticeSentEvent;
        private static AutoResetEvent client1LocalUserMessageReceivedEvent;
        private static AutoResetEvent client1LocalUserNoticeReceivedEvent;
        private static AutoResetEvent client1ChannelJoinedEvent;
        private static AutoResetEvent client1ChannelLeftEvent;
        private static AutoResetEvent client1WhoReplyReceived;
        private static AutoResetEvent client1WhoIsReplyReceived;
        private static AutoResetEvent client1WhoWasReplyReceived;
        private static AutoResetEvent client1UserQuitEvent;
        private static AutoResetEvent client1ChannelUsersListReceivedEvent;
        private static AutoResetEvent client1ChannelModeChangedEvent;
        private static AutoResetEvent client1ChannelTopicChangedEvent;
        private static AutoResetEvent client1ChannelUserModeChangedEvent;
        private static AutoResetEvent client1ChannelUserJoinedEvent;
        private static AutoResetEvent client1ChannelUserLeftEvent;
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
        private static AutoResetEvent client2ChannelLeftEvent;
        private static AutoResetEvent client2ChannelMessageReceivedEvent;
        private static AutoResetEvent client2ChannelNoticeReceivedEvent;
#pragma warning restore 0649

        // Data received from tests.
        private static string client1UserQuitComment;

        // Primary and secondary client, with associated user information.
        private static IrcClient client1, client2;
        private static string nickName1, nickName2;
        private static string userName1, userName2;
        private static string testChannelName;

        private static TestStateManager<IrcClientTestState> stateManager;

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            stateManager = new TestStateManager<IrcClientTestState>();

            client1 = new IrcClient();
#if DEBUG
            client1.ClientId = "1";
#endif
            client1.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            client1.Connected += client1_Connected;
            client1.ConnectFailed += client1_ConnectFailed;
            client1.Disconnected += client1_Disconnected;
            client1.Error += client1_Error;
            client1.ProtocolError += client1_ProtocolError;
            client1.Registered += client1_Registered;
            client1.MotdReceived += client1_MotdReceived;
            client1.WhoReplyReceived += client1_WhoReplyReceived;
            client1.WhoIsReplyReceived += client1_WhoIsReplyReceived;
            client1.WhoWasReplyReceived += client1_WhoWasReplyReceived;

            client2 = new IrcClient();
#if DEBUG
            client2.ClientId = "2";
#endif
            client2.Connected += client2_Connected;
            client2.ConnectFailed += client2_ConnectFailed;
            client2.Disconnected += client2_Disconnected;
            client2.Error += client2_Error;
            client2.ProtocolError += client2_ProtocolError;
            client2.Registered += client2_Registered;

            // Initialise wait handles for all events.
            GetAllWaitHandlesFields().ForEach(fieldInfo => fieldInfo.SetValue(null, new AutoResetEvent(false)));

            // Nick name length limit on irc.freenode.net is 16 chars.
            Func<string> getRandomUserId = () => Guid.NewGuid().ToString().Substring(0, 8);
            nickName1 = userName1 = string.Format("itb-{0}", getRandomUserId());
            nickName2 = userName2 = string.Format("itb-{0}", getRandomUserId());
            Debug.WriteLine("Cllient 1 user has nick name '{0}' and user name '{1}'.", nickName1, userName1);
            Debug.WriteLine("Cllient 2 user has nick name '{0}' and user name '{1}'.", nickName2, userName2);

            stateManager.SetStates(IrcClientTestState.Client1Initialised, IrcClientTestState.Client2Initialised);
            client1.Connect(serverHostName, new IrcUserRegistrationInfo()
                {
                    Password = serverPassword,
                    NickName = nickName1,
                    UserName = userName1,
                    RealName = realName
                });
            client2.Connect(serverHostName, new IrcUserRegistrationInfo()
                {
                    Password = serverPassword,
                    NickName = nickName2,
                    UserName = userName2,
                    RealName = realName
                });
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

            // Dispose all event wait handles in class.
            GetAllWaitHandlesFields().ForEach(fieldInfo => ((IDisposable)fieldInfo.GetValue(null)).Dispose());
        }

        private static IEnumerable<FieldInfo> GetAllWaitHandlesFields()
        {
            // Return collection of all static event wait handles in class.
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
            
            Debug.Assert(false, "Protocol error: " + e.Error.Message);
        }

        private static void client1_ProtocolError(object sender, EventArgs e)
        {
            //
        }

        private static void client1_Registered(object sender, EventArgs e)
        {
            client1.LocalUser.ModesChanged += client1_LocalUser_ModesChanged;
            client1.LocalUser.NickNameChanged += client1_LocalUser_NickNameChanged;
            client1.LocalUser.IsAwayChanged += client1_LocalUser_IsAwayChanged;
            client1.LocalUser.JoinedChannel += client1_LocalUser_JoinedChannel;
            client1.LocalUser.LeftChannel += client1_LocalUser_LeftChannel;
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

        private static void client1_WhoReplyReceived(object sender, EventArgs e)
        {
            if (client1WhoReplyReceived != null)
                client1WhoReplyReceived.Set();
        }

        private static void client1_WhoIsReplyReceived(object sender, IrcUserEventArgs e)
        {
            if (client1WhoIsReplyReceived != null)
                client1WhoIsReplyReceived.Set();
        }

        private static void client1_WhoWasReplyReceived(object sender, IrcUserEventArgs e)
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

        private static void client1_LocalUser_IsAwayChanged(object sender, EventArgs e)
        {
            if (client1LocalUserIsAwayChangedEvent != null)
                client1LocalUserIsAwayChangedEvent.Set();
        }

        private static void client1_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived += client1_Channel_UsersListReceived;
            e.Channel.ModesChanged += client1_Channel_ModesChanged;
            e.Channel.TopicChanged += client1_Channel_TopicChanged;
            e.Channel.UserJoined += client1_Channel_UserJoined;
            e.Channel.UserLeft += client1_Channel_UserLeft;
            e.Channel.UserKicked += client1_Channel_UserKicked;
            e.Channel.MessageReceived += client1_Channel_MessageReceived;
            e.Channel.NoticeReceived += client1_Channel_NoticeReceived;

            if (client1ChannelJoinedEvent != null)
                client1ChannelJoinedEvent.Set();
        }

        private static void client1_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived -= client1_Channel_UsersListReceived;
            e.Channel.ModesChanged -= client1_Channel_ModesChanged;
            e.Channel.TopicChanged -= client1_Channel_TopicChanged;
            e.Channel.UserJoined -= client1_Channel_UserJoined;
            e.Channel.UserLeft -= client1_Channel_UserLeft;
            e.Channel.UserKicked -= client1_Channel_UserKicked;
            e.Channel.MessageReceived -= client1_Channel_MessageReceived;

            if (client1ChannelLeftEvent != null)
                client1ChannelLeftEvent.Set();
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

        private static void client2_User_Quit(object sender, IrcCommentEventArgs e)
        {
            client1UserQuitComment = e.Comment;

            if (client1UserQuitEvent != null)
                client1UserQuitEvent.Set();
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
            if (client1ChannelTopicChangedEvent != null)
                client1ChannelTopicChangedEvent.Set();
        }

        private static void client1_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            e.ChannelUser.User.Quit += client2_User_Quit;

            if (client1ChannelUserJoinedEvent != null)
                client1ChannelUserJoinedEvent.Set();
        }

        private static void client1_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
        {
            e.ChannelUser.User.Quit -= client2_User_Quit;

            if (client1ChannelUserLeftEvent != null)
                client1ChannelUserLeftEvent.Set();
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
            client2.LocalUser.JoinedChannel += client2_LocalUser_JoinedChannel;
            client2.LocalUser.LeftChannel += client2_LocalUser_LeftChannel;
            client2.LocalUser.MessageSent += client2_LocalUser_MessageSent;
            client2.LocalUser.NoticeSent += client2_LocalUser_NoticeSent;
            client2.LocalUser.MessageReceived += client2_LocalUser_MessageReceived;
            client2.LocalUser.NoticeReceived += client2_LocalUser_NoticeReceived;

            if (client2RegisteredEvent != null)
                client2RegisteredEvent.Set();
        }

        private static void client2_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined += client2_Channel_UserJoined;
            e.Channel.UserLeft += client2_Channel_UserLeft;
            e.Channel.MessageReceived += client2_Channel_MessageReceived;
            e.Channel.NoticeReceived += client2_Channel_NoticeReceived;

            if (client2ChannelJoinedEvent != null)
                client2ChannelJoinedEvent.Set();
        }

        private static void client2_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined -= client2_Channel_UserJoined;
            e.Channel.UserLeft -= client2_Channel_UserLeft;
            e.Channel.MessageReceived -= client2_Channel_MessageReceived;
            e.Channel.NoticeReceived -= client2_Channel_NoticeReceived;

            if (client2ChannelLeftEvent != null)
                client2ChannelLeftEvent.Set();
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

        private static void client2_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
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
        public void TestInitialize()
        {
        }

        [TestCleanup()]
        public void TestCleanup()
        {
        }

        [TestMethod()]
        public void ConnectTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Initialised);
            Assert.IsTrue(WaitForClientEvent(client1ConnectedEvent, 5000), "Client 1 connection to server timed out.");
            Assert.IsTrue(client1.IsConnected, "Client 1 failed to connect to server.");
            stateManager.SetStates(IrcClientTestState.Client1Connected);

            stateManager.HasStates(IrcClientTestState.Client2Initialised);
            Assert.IsTrue(WaitForClientEvent(client2ConnectedEvent, 5000), "Client 2 connection to server timed out.");
            Assert.IsTrue(client2.IsConnected, "Client 2 failed to connect to server.");
            stateManager.SetStates(IrcClientTestState.Client2Connected);
        }

        [TestMethod()]
        public void DisconnectTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Connected);
            client1.Disconnect();
            Assert.IsTrue(client1DisconnectedEvent.WaitOne(5000), "Client 1 failed to disconnect from server.");
            stateManager.UnsetStates(IrcClientTestState.Client1Connected);
        }

        [TestMethod()]
        public void QuitTest()
        {
            stateManager.HasStates(IrcClientTestState.Client2Connected);
            client2.Quit(quitMessage);
            Assert.IsTrue(client1UserQuitEvent.WaitOne(10000), "Client 2 failed to quit from server.");
            Assert.IsTrue(client2DisconnectedEvent.WaitOne(5000), "Client 2 failed to disconnect from server.");
            stateManager.UnsetStates(IrcClientTestState.Client2Connected);
        }

        [TestMethod(),]
        public void RegisterTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Connected);
            Assert.IsTrue(WaitForClientEvent(client1RegisteredEvent, 20000),
                "Client 1 failed to register connection with server.");
            stateManager.SetStates(IrcClientTestState.Client1Registered);
            Assert.AreEqual(nickName1, client1.LocalUser.NickName, "Client 1 nick name was not correctly set.");
            Assert.AreEqual(userName1, client1.LocalUser.UserName, "Client 1 user name was not correctly set.");
            Assert.AreEqual(realName, client1.LocalUser.RealName, "Client 1 real name was not correctly set.");

            stateManager.HasStates(IrcClientTestState.Client2Connected);
            Assert.IsTrue(WaitForClientEvent(client2RegisteredEvent, 20000),
                "Client 2 failed to register connection with server.");
            stateManager.SetStates(IrcClientTestState.Client2Registered);
            Assert.AreEqual(nickName2, client2.LocalUser.NickName, "Client 2 nick name was not correctly set.");
            Assert.AreEqual(userName2, client2.LocalUser.UserName, "Client 2 user name was not correctly set.");
            Assert.AreEqual(realName, client2.LocalUser.RealName, "Client 2 real name was not correctly set.");
        }

        [TestMethod()]
        public void MotdTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsTrue(WaitForClientEvent(client1MotdReceivedEvent, 10000),
                "Client 1 did not receive MOTD from server.");
        }

        [TestMethod()]
        public void LocalUserChangeNickNameTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.AreEqual(nickName1, client1.LocalUser.NickName,
                "Client 1 local user nick name is incorrect before update.");
            nickName1 += "-2";
            client1.LocalUser.SetNickName(nickName1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserNickNameChangedEvent, 10000),
                "Client 1 failed to change local user nick name.");
            Assert.AreEqual(nickName1, client1.LocalUser.NickName,
                "Client 1 local user nick name is incorrect after update.");
        }

        [TestMethod()]
        public void LocalUserModeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserModeChangedEvent, 10000),
                "Client 1 failed to receive initial local user mode.");
            Assert.IsTrue(client1.LocalUser.Modes.Contains('i'),
                "Client 1 local user does not initially have mode 'i'.");
            Assert.IsFalse(client1.LocalUser.Modes.Contains('w'), "Client 1 local user already has mode 'w'.");
            client1.LocalUser.SetModes("+w");
            Assert.IsTrue(WaitForClientEvent(client1LocalUserModeChangedEvent, 10000),
                "Client 1 failed to change local user mode.");
            Assert.IsTrue(client1.LocalUser.Modes.Contains('w'), "Client 1 local user modes are unchanged.");
        }

        [TestMethod()]
        public void LocalUserAwayTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsFalse(client1.LocalUser.IsAway, "Client 1 local user is already away.");
            client1.LocalUser.SetAway("I'm away now.");
            Assert.IsTrue(WaitForClientEvent(client1LocalUserIsAwayChangedEvent, 10000),
                "Client 1 local user did not change Away status.");
            Assert.IsTrue(client1.LocalUser.IsAway, "Client 1 local user is not away.");
            client1.LocalUser.UnsetAway();
            Assert.IsTrue(WaitForClientEvent(client1LocalUserIsAwayChangedEvent, 10000),
                "Client 1 local user did not change Away status.");
            Assert.IsFalse(client1.LocalUser.IsAway, "Client 1 local user is still away.");
        }

        [TestMethod()]
        public void JoinChannelTest()
        {
            // Generate random name of channel that has very low probability of already existing.
            testChannelName = string.Format("#ircsil-test-{0}", Guid.NewGuid().ToString().Substring(0, 13));

            stateManager.HasStates(IrcClientTestState.Client1Registered);
            client1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Client 1 could not join channel.");
            stateManager.SetStates(IrcClientTestState.Client1InChannel);

            stateManager.HasStates(IrcClientTestState.Client2Registered);
            client2.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client2ChannelJoinedEvent, 10000), "Client 2 could not join channel.");
            stateManager.SetStates(IrcClientTestState.Client2InChannel);

            // Check that client 1 has seen both local user and client 2 user join channel.
            var client1Channel = client1.Channels.First();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUsersListReceivedEvent, 10000),
                "Client 1 did not receive initial list of users.");
            Assert.IsTrue(client1Channel.Users.Count >= 1 &&
                client1Channel.Users[0].User == client1.LocalUser,
                "Client 1 does not see local user in channel.");
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserJoinedEvent, 10000),
                "Client 1 did not see client 2 user join channel.");
            Assert.IsTrue(client1Channel.Users.Count >= 2 &&
                client1Channel.Users[1].User.NickName == client2.LocalUser.NickName,
                "Client 1 does not see client 2 user in channel.");
        }

        [TestMethod()]
        public void RejoinChannelTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            client1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Client 1 could not rejoin channel.");
            stateManager.SetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void PartChannelTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            client1.Channels.Leave(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelLeftEvent, 10000), "Client 1 could not part channel.");
            stateManager.UnsetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void ChannelUsersListReceivedTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = client1.Channels.Single(c => c.Name == testChannelName);

            Assert.IsTrue(channel.Users.Count == 1 || channel.Users.Count == 2,
                "Client 1 channel has unexpected number of users.");
            Assert.AreEqual(client1.LocalUser, channel.Users[0].User,
                "Client 1 local user does not appear in channel.");
        }

        [TestMethod()]
        public void ChannelLocalUserModeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.Users.First();

            // Local user should already have 'o' mode on joining channel.
            Assert.IsTrue(channelUser.Modes.Contains('o'),
                "Client 1 local user does not initially have 'o' mode in channel.");

            channelUser.ModesChanged += (sender, e) =>
                {
                    if (client1ChannelUserModeChangedEvent != null)
                        client1ChannelUserModeChangedEvent.Set();
                };
            channelUser.Voice();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserModeChangedEvent, 10000),
                "Client 1 channel mode of local user was not changed.");
            Assert.IsTrue(channelUser.Modes.Contains('v'),
                "Client 1 local user does not have 'v' mode in channel.");
        }

        [TestMethod()]
        public void ChannelModeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = client1.Channels.Single(c => c.Name == testChannelName);

            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Client 1 channel mode was not initialised.");
            Assert.IsTrue(channel.Modes.Contains('n'),
                "Client 1 channel does not initially have mode 'n'.");

            Assert.IsFalse(channel.Modes.Contains('m'), "Client 1 channel already has mode 'm'.");
            channel.SetModes("+m");
            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Client 1 channel mode was not changed.");
            Assert.IsTrue(channel.Modes.Contains('m'), "Client 1 channel does not have mode 'm'.");
            channel.SetModes("-m");
            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Client 1 channel mode was not changed to '-m'.");
            Assert.IsFalse(channel.Modes.Contains('m'), "Client 1 channel still has mode 'm'.");
        }

        [TestMethod()]
        public void ChannelSendAndReceiveMessage()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);

            // Send private message to user of client 2.
            client1.LocalUser.SendMessage(client2.LocalUser.NickName, testMessage1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserMessageSentEvent, 5000),
                "Client 1 local user did not send private message to client 2 user.");
            Assert.IsTrue(WaitForClientEvent(client2LocalUserMessageReceivedEvent, 5000),
                "Client 2 local user did not receive private message from client 1 user.");

            // Send private message to channel.
            var client1Channel = client1.Channels.Single(c => c.Name == testChannelName);
            var client2Channel = client2.Channels.Single(c => c.Name == testChannelName);
            client1.LocalUser.SendMessage(client1Channel, testMessage2);
            Assert.IsTrue(WaitForClientEvent(client2ChannelMessageReceivedEvent, 5000),
                "Client 2 channel did not receive private message from client 1 user.");
        }

        [TestMethod()]
        public void ChannelSendAndReceiveNotice()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);

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

        [TestMethod()]
        public void ChannelFloodPreventionTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);

            // Attempt to rapidly send many messages to channel.
            const int messageCount = 10;
            for (int i = 1; i <= messageCount; i++)
            {
                client1.LocalUser.SendMessage(testChannelName, spamMessage);
            }

            // Check that all sent messages are eventually received.
            var messageWaitPeriod = ((IrcStandardFloodPreventer)client1.FloodPreventer).CounterPeriod * 2;
            for (int i = 1; i <= messageCount; i++)
            {
                Assert.IsTrue(WaitForClientEvent(client2ChannelMessageReceivedEvent, messageWaitPeriod),
                    "Client 1 channel did not receive message {0} of {1} for flood prevention test",
                    i, messageCount);
            }

            // If user does not get booted from server for "excess flood", then flood prevention is working correctly.
            Assert.IsTrue(client1.IsConnected,
                "Client 1 is no longer connected to the server after attempting to flood the test channel.");
            Assert.IsTrue(client1.Channels.Any(c => c.Name == testChannelName),
                "Client 1 is no longer a member of the test channel after trying to flood it.");
        }

        [TestMethod()]
        public void ChannelLocalUserKickTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = client1.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.GetChannelUser(client1.LocalUser);
            Assert.IsTrue(channel != null, "Cannot find local user in channel.");

            channelUser.Kick();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserKickedEvent, 10000),
                "Client 1 could not kick local user from channel.");
            Assert.IsFalse(client1.Channels.Contains(channel),
                "Client 1 collection of channels still contains channel from which local user kicked.");
            stateManager.UnsetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void WhoTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            stateManager.HasStates(IrcClientTestState.Client2InChannel);
            client1.QueryWho(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1WhoReplyReceived, 10000),
                "Client 1 did not receive reply to Who query.");

            var user1 = client1.Users.Single(u => u.NickName == nickName1);
            Assert.AreEqual(realName, user1.RealName);
            Assert.IsTrue(user1.HostName != null && user1.HostName.Length > 1);
            Assert.AreEqual(client1.ServerName, user1.ServerName);
            Assert.IsFalse(user1.IsOperator);
            Assert.IsFalse(user1.IsAway);
            Assert.AreEqual(user1.HopCount, 0);
            var user1Channel = client1.Channels.First();
            Assert.IsTrue(user1.GetChannelUsers().Any(cu => cu.Channel == user1Channel));

            var user2 = client1.Users.Single(u => u.NickName == nickName2);
            Assert.AreEqual(realName, user2.RealName);
            Assert.IsTrue(user2.HostName != null && user2.HostName.Length > 1);
            Assert.AreEqual(client2.ServerName, user2.ServerName);
            Assert.IsFalse(user2.IsOperator);
            Assert.IsFalse(user2.IsAway);
            Assert.AreEqual(user2.HopCount, 0);
            var user2Channel = client1.Channels.First();
            Assert.IsTrue(user2.GetChannelUsers().Any(cu => cu.Channel == user2Channel));
        }

        [TestMethod()]
        public void WhoIsTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            stateManager.HasStates(IrcClientTestState.Client2InChannel);
            var whoIsUser = client1.Users.Single(u => u.NickName == client2.LocalUser.NickName);

            whoIsUser.WhoIs();
            Assert.IsTrue(WaitForClientEvent(client1WhoIsReplyReceived, 10000),
                "Client 1 did not receive reply to WhoIs query.");
            Assert.AreEqual(realName, whoIsUser.RealName);
            Assert.IsTrue(whoIsUser.HostName != null && whoIsUser.HostName.Length > 1);
            Assert.AreEqual(client2.ServerName, whoIsUser.ServerName);
            Assert.IsFalse(whoIsUser.IsOperator);
            var channel = client1.Channels.First();
            Assert.IsTrue(whoIsUser.GetChannelUsers().First().Channel == channel);
        }

        [TestMethod()]
        public void WhoWasTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            stateManager.HasNotStates(IrcClientTestState.Client2Connected);
            client1.QueryWhoWas(nickName2);
            Assert.IsTrue(WaitForClientEvent(client1WhoWasReplyReceived, 10000),
                "Client 1 did not receive reply to WhoWas query.");
            var whoWasUser = client1.Users.SingleOrDefault(u => u.NickName == nickName2);
            Assert.IsNotNull(whoWasUser, "Client 1 does not contain user of WhoWas query in user list.");
            Assert.AreEqual(userName2, whoWasUser.NickName);
            Assert.AreEqual(realName, whoWasUser.RealName);
            Assert.IsTrue(whoWasUser.HostName != null && whoWasUser.HostName.Length > 1);
            Assert.AreEqual(client2.ServerName, whoWasUser.ServerName);
        }

        private bool WaitForClientEvent(WaitHandle eventHandle, int millisecondsTimeout = Timeout.Infinite)
        {
            // Wait for specified event, disconnection, error, or timeout (whichever occurs first).
            if (Debugger.IsAttached)
                millisecondsTimeout = Timeout.Infinite;
            var setEventIndex = WaitHandle.WaitAny(new[] { eventHandle, client1DisconnectedEvent, client1ErrorEvent,
                client2DisconnectedEvent, client2ErrorEvent}, millisecondsTimeout);

            // Fail test if timeout occurred, client was disconnected, or client error occurred.
            switch (setEventIndex)
            {
                case WaitHandle.WaitTimeout:
                    break;
                case 0:
                    // Event was successfully detected.
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

    // Defines the set of test states managed by the TestStateManager. Any number of states may be set at any time.
    public enum IrcClientTestState
    {
        None,
        Client1Initialised,
        Client2Initialised,
        Client1Connected,
        Client2Connected,
        Client1Registered,
        Client2Registered,
        Client1InChannel,
        Client2InChannel,
    }
}
