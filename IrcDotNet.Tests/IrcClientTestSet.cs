using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcDotNet.Tests
{
    using Common.Collections;
    using Ctcp;

    // Set of all tests for IRC client.
    [TestClass()]
    public class IrcClientTestSet
    {
        // Test parameters for IRC connections.
        private const string serverHostName = "irc.freenode.net";
        private const string serverPassword = null;
        private const string realName = "IRC.NET Test Bot";

        // Information used for sending messages, to be used in tests.
        private const string clientVersionInfo = "IRC.NET Test Bot";
        private const string quitMessage = "Client 2 quitting test.";
        private const string testMessage1 = "This is the first test message.";
        private const string testMessage2 = "This is the second test message.";
        private const string testComment1 = "This is the first test comment.";
        private const string spamMessage = "This message is part of an attempt to spam the channel and get booted from the server.";

        // Information received from server, to be used in tests.
        private static IList<IrcChannelInfo> client1ListedChannels;
        private static string client1UserQuitComment;
        private static string client1ChannelLeaveComment;

        private static TimeSpan client2PingTime;
        private static string client2ReceivedTimeInfo;
        private static string client2ReceivedVersionInfo;
        private static string client2ReceivedActionText;

        // Threading events used to signify when client raises event.
#pragma warning disable 0649

        private static AutoResetEvent client1ConnectedEvent;
        private static AutoResetEvent client1DisconnectedEvent;
        private static AutoResetEvent client1ErrorEvent;
        private static AutoResetEvent client1RegisteredEvent;
        private static AutoResetEvent client1MotdReceivedEvent;
        private static AutoResetEvent client1NetworkInfoReceivedEvent;
        private static AutoResetEvent client1ServerVersionInfoReceivedEvent;
        private static AutoResetEvent client1ServerTimeReceivedEvent;
        private static AutoResetEvent client1LocalUserNickNameChangedEvent;
        private static AutoResetEvent client1LocalUserModeChangedEvent;
        private static AutoResetEvent client1LocalUserIsAwayChangedEvent;
        private static AutoResetEvent client1LocalUserMessageSentEvent;
        private static AutoResetEvent client1LocalUserNoticeSentEvent;
        private static AutoResetEvent client1LocalUserMessageReceivedEvent;
        private static AutoResetEvent client1LocalUserNoticeReceivedEvent;
        private static AutoResetEvent client1ChannelJoinedEvent;
        private static AutoResetEvent client1ChannelLeftEvent;
        private static AutoResetEvent client1WhoReplyReceivedEvent;
        private static AutoResetEvent client1WhoIsReplyReceivedEvent;
        private static AutoResetEvent client1WhoWasReplyReceivedEvent;
        private static AutoResetEvent client1ChannelListReceivedEvent;
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

        private static AutoResetEvent ctcpClient1PingResponseReceivedEvent;
        private static AutoResetEvent ctcpClient1VersionResponseReceivedEvent;
        private static AutoResetEvent ctcpClient1TimeResponseReceivedEvent;
        private static AutoResetEvent ctcpClient1ActionReceivedEvent;

        private static AutoResetEvent ctcpClient2PingResponseReceivedEvent;
        private static AutoResetEvent ctcpClient2VersionResponseReceivedEvent;
        private static AutoResetEvent ctcpClient2TimeResponseReceivedEvent;
        private static AutoResetEvent ctcpClient2ActionReceivedEvent;

#pragma warning restore 0649

        // Primary and secondary client, with associated user information.
        private static IrcClient ircClient1, ircClient2;
        private static CtcpClient ctcpClient1, ctcpClient2;
        private static string nickName1, nickName2;
        private static string userName1, userName2;
        private static string testChannelName;

        private static TestStateManager<IrcClientTestState> stateManager;

        [ClassInitialize()]
        public static void ClassInitialize(TestContext testContext)
        {
            stateManager = new TestStateManager<IrcClientTestState>();

            // Create IRC clients.
            ircClient1 = new IrcClient();
#if DEBUG
            ircClient1.ClientId = "1";
#endif
            ircClient1.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            ircClient1.Connected += ircClient1_Connected;
            ircClient1.ConnectFailed += ircClient1_ConnectFailed;
            ircClient1.Disconnected += ircClient1_Disconnected;
            ircClient1.Error += ircClient1_Error;
            ircClient1.ProtocolError += ircClient1_ProtocolError;
            ircClient1.Registered += ircClient1_Registered;
            ircClient1.MotdReceived += ircClient1_MotdReceived;
            ircClient1.NetworkInformationReceived += ircClient1_NetworkInformationReceived;
            ircClient1.ServerVersionInfoReceived += ircClient1_ServerVersionInfoReceived;
            ircClient1.ServerTimeReceived += ircClient1_ServerTimeReceived;
            ircClient1.WhoReplyReceived += ircClient1_WhoReplyReceived;
            ircClient1.WhoIsReplyReceived += ircClient1_WhoIsReplyReceived;
            ircClient1.WhoWasReplyReceived += ircClient1_WhoWasReplyReceived;
            ircClient1.ChannelListReceived += ircClient1_ChannelListReceived;

            ircClient2 = new IrcClient();
#if DEBUG
            ircClient2.ClientId = "2";
#endif
            ircClient2.Connected += ircClient2_Connected;
            ircClient2.ConnectFailed += ircClient2_ConnectFailed;
            ircClient2.Disconnected += ircClient2_Disconnected;
            ircClient2.Error += ircClient2_Error;
            ircClient2.ProtocolError += ircClient2_ProtocolError;
            ircClient2.Registered += ircClient2_Registered;

            // Create CTCP clients over IRC clients.
            ctcpClient1 = new CtcpClient(ircClient1);
            ctcpClient1.ClientVersion = clientVersionInfo;
            ctcpClient1.PingResponseReceived += ctcpClient1_PingResponseReceived;
            ctcpClient1.VersionResponseReceived += ctcpClient1_VersionResponseReceived;
            ctcpClient1.TimeResponseReceived += ctcpClient1_TimeResponseReceived;
            ctcpClient1.ActionReceived += ctcpClient1_ActionReceived;

            ctcpClient2 = new CtcpClient(ircClient2);
            ctcpClient2.ClientVersion = clientVersionInfo;
            ctcpClient2.PingResponseReceived += ctcpClient2_PingResponseReceived;
            ctcpClient2.VersionResponseReceived += ctcpClient2_VersionResponseReceived;
            ctcpClient2.TimeResponseReceived += ctcpClient2_TimeResponseReceived;
            ctcpClient2.ActionReceived += ctcpClient2_ActionReceived;

            // Initialize wait handles for all events.
            GetAllWaitHandlesFields().ForEach(fieldInfo => fieldInfo.SetValue(null, new AutoResetEvent(false)));

            // Nick name length limit on irc.freenode.net is 16 chars.
            Func<string> getRandomUserId = () => Guid.NewGuid().ToString().Substring(0, 8);
            nickName1 = userName1 = string.Format("itb-{0}", getRandomUserId());
            nickName2 = userName2 = string.Format("itb-{0}", getRandomUserId());
            Debug.WriteLine("Cllient 1 user has nick name '{0}' and user name '{1}'.", nickName1, userName1);
            Debug.WriteLine("Cllient 2 user has nick name '{0}' and user name '{1}'.", nickName2, userName2);

            stateManager.SetStates(IrcClientTestState.Client1Initialized, IrcClientTestState.Client2Initialized);
            ircClient1.Connect(serverHostName, false, new IrcUserRegistrationInfo()
                {
                    Password = serverPassword,
                    NickName = nickName1,
                    UserName = userName1,
                    RealName = realName
                });
            ircClient2.Connect(serverHostName, false, new IrcUserRegistrationInfo()
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
            if (ircClient1 != null)
            {
                ircClient1.Dispose();
                ircClient1 = null;
            }
            if (ircClient2 != null)
            {
                ircClient2.Dispose();
                ircClient2 = null;
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

        #region IRC Client 1 Event Handlers

        private static void ircClient1_Connected(object sender, EventArgs e)
        {
            if (client1ConnectedEvent != null)
                client1ConnectedEvent.Set();
        }

        private static void ircClient1_ConnectFailed(object sender, IrcErrorEventArgs e)
        {
            if (client1ConnectedEvent != null)
                client1ConnectedEvent.Set();
        }

        private static void ircClient1_Disconnected(object sender, EventArgs e)
        {
            if (client1DisconnectedEvent != null)
                client1DisconnectedEvent.Set();
        }

        private static void ircClient1_Error(object sender, IrcErrorEventArgs e)
        {
            if (client1ErrorEvent != null)
                client1ErrorEvent.Set();

            Debug.Assert(false, "Protocol error: " + e.Error.Message);
        }

        private static void ircClient1_ProtocolError(object sender, EventArgs e)
        {
            // Ignore.
        }

        private static void ircClient1_Registered(object sender, EventArgs e)
        {
            ircClient1.LocalUser.ModesChanged += ircClient1_LocalUser_ModesChanged;
            ircClient1.LocalUser.NickNameChanged += ircClient1_LocalUser_NickNameChanged;
            ircClient1.LocalUser.IsAwayChanged += ircClient1_LocalUser_IsAwayChanged;
            ircClient1.LocalUser.JoinedChannel += ircClient1_LocalUser_JoinedChannel;
            ircClient1.LocalUser.LeftChannel += ircClient1_LocalUser_LeftChannel;
            ircClient1.LocalUser.MessageSent += ircClient1_LocalUser_MessageSent;
            ircClient1.LocalUser.NoticeSent += ircClient1_LocalUser_NoticeSent;
            ircClient1.LocalUser.MessageReceived += ircClient1_LocalUser_MessageReceived;
            ircClient1.LocalUser.NoticeReceived += ircClient1_LocalUser_NoticeReceived;

            if (client1RegisteredEvent != null)
                client1RegisteredEvent.Set();
        }

        private static void ircClient1_MotdReceived(object sender, EventArgs e)
        {
            if (client1MotdReceivedEvent != null)
                client1MotdReceivedEvent.Set();
        }

        private static void ircClient1_NetworkInformationReceived(object sender, EventArgs e)
        {
            if (client1NetworkInfoReceivedEvent != null)
                client1NetworkInfoReceivedEvent.Set();
        }

        private static void ircClient1_ServerVersionInfoReceived(object sender, IrcServerVersionInfoEventArgs e)
        {
            if (client1ServerVersionInfoReceivedEvent != null)
                client1ServerVersionInfoReceivedEvent.Set();
        }

        private static void ircClient1_ServerTimeReceived(object sender, IrcServerTimeEventArgs e)
        {
            if (client1ServerTimeReceivedEvent != null)
                client1ServerTimeReceivedEvent.Set();
        }

        private static void ircClient1_WhoReplyReceived(object sender, EventArgs e)
        {
            if (client1WhoReplyReceivedEvent != null)
                client1WhoReplyReceivedEvent.Set();
        }

        private static void ircClient1_WhoIsReplyReceived(object sender, IrcUserEventArgs e)
        {
            if (client1WhoIsReplyReceivedEvent != null)
                client1WhoIsReplyReceivedEvent.Set();
        }

        private static void ircClient1_WhoWasReplyReceived(object sender, IrcUserEventArgs e)
        {
            if (client1WhoWasReplyReceivedEvent != null)
                client1WhoWasReplyReceivedEvent.Set();
        }

        private static void ircClient1_ChannelListReceived(object sender, IrcChannelListReceivedEventArgs e)
        {
            client1ListedChannels = e.Channels;

            if (client1ChannelListReceivedEvent != null)
                client1ChannelListReceivedEvent.Set();
        }

        private static void ircClient1_LocalUser_ModesChanged(object sender, EventArgs e)
        {
            if (client1LocalUserModeChangedEvent != null)
                client1LocalUserModeChangedEvent.Set();
        }

        private static void ircClient1_LocalUser_NickNameChanged(object sender, EventArgs e)
        {
            if (client1LocalUserNickNameChangedEvent != null)
                client1LocalUserNickNameChangedEvent.Set();
        }

        private static void ircClient1_LocalUser_IsAwayChanged(object sender, EventArgs e)
        {
            if (client1LocalUserIsAwayChangedEvent != null)
                client1LocalUserIsAwayChangedEvent.Set();
        }

        private static void ircClient1_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived += ircClient1_Channel_UsersListReceived;
            e.Channel.ModesChanged += ircClient1_Channel_ModesChanged;
            e.Channel.TopicChanged += ircClient1_Channel_TopicChanged;
            e.Channel.UserJoined += ircClient1_Channel_UserJoined;
            e.Channel.UserLeft += ircClient1_Channel_UserLeft;
            e.Channel.UserKicked += ircClient1_Channel_UserKicked;
            e.Channel.MessageReceived += ircClient1_Channel_MessageReceived;
            e.Channel.NoticeReceived += ircClient1_Channel_NoticeReceived;

            if (client1ChannelJoinedEvent != null)
                client1ChannelJoinedEvent.Set();
        }

        private static void ircClient1_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UsersListReceived -= ircClient1_Channel_UsersListReceived;
            e.Channel.ModesChanged -= ircClient1_Channel_ModesChanged;
            e.Channel.TopicChanged -= ircClient1_Channel_TopicChanged;
            e.Channel.UserJoined -= ircClient1_Channel_UserJoined;
            e.Channel.UserLeft -= ircClient1_Channel_UserLeft;
            e.Channel.UserKicked -= ircClient1_Channel_UserKicked;
            e.Channel.MessageReceived -= ircClient1_Channel_MessageReceived;

            client1ChannelLeaveComment = e.Comment;

            if (client1ChannelLeftEvent != null)
                client1ChannelLeftEvent.Set();
        }

        private static void ircClient1_LocalUser_MessageSent(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserMessageSentEvent != null)
                client1LocalUserMessageSentEvent.Set();
        }

        private static void ircClient1_LocalUser_NoticeSent(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserNoticeSentEvent != null)
                client1LocalUserNoticeSentEvent.Set();
        }

        private static void ircClient1_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserMessageReceivedEvent != null)
                client1LocalUserMessageReceivedEvent.Set();
        }

        private static void ircClient1_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1LocalUserNoticeReceivedEvent != null)
                client1LocalUserNoticeReceivedEvent.Set();
        }

        private static void ircClient2_User_Quit(object sender, IrcCommentEventArgs e)
        {
            client1UserQuitComment = e.Comment;

            if (client1UserQuitEvent != null)
                client1UserQuitEvent.Set();
        }

        private static void ircClient1_Channel_UsersListReceived(object sender, EventArgs e)
        {
            if (client1ChannelUsersListReceivedEvent != null)
                client1ChannelUsersListReceivedEvent.Set();
        }

        private static void ircClient1_Channel_ModesChanged(object sender, EventArgs e)
        {
            if (client1ChannelModeChangedEvent != null)
                client1ChannelModeChangedEvent.Set();
        }

        private static void ircClient1_Channel_TopicChanged(object sender, EventArgs e)
        {
            if (client1ChannelTopicChangedEvent != null)
                client1ChannelTopicChangedEvent.Set();
        }

        private static void ircClient1_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            e.ChannelUser.User.Quit += ircClient2_User_Quit;

            if (client1ChannelUserJoinedEvent != null)
                client1ChannelUserJoinedEvent.Set();
        }

        private static void ircClient1_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
        {
            e.ChannelUser.User.Quit -= ircClient2_User_Quit;

            if (client1ChannelUserLeftEvent != null)
                client1ChannelUserLeftEvent.Set();
        }

        private static void ircClient1_Channel_UserKicked(object sender, IrcChannelUserEventArgs e)
        {
            if (client1ChannelUserKickedEvent != null)
                client1ChannelUserKickedEvent.Set();
        }

        private static void ircClient1_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1ChannelMessageReceivedEvent != null)
                client1ChannelMessageReceivedEvent.Set();
        }

        private static void ircClient1_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client1ChannelNoticeReceivedEvent != null)
                client1ChannelNoticeReceivedEvent.Set();
        }

        #endregion

        #region IRC Client 2 Event Handlers

        private static void ircClient2_Connected(object sender, EventArgs e)
        {
            if (client2ConnectedEvent != null)
                client2ConnectedEvent.Set();
        }

        private static void ircClient2_ConnectFailed(object sender, IrcErrorEventArgs e)
        {
            if (client2ConnectedEvent != null)
                client2ConnectedEvent.Set();
        }

        private static void ircClient2_Disconnected(object sender, EventArgs e)
        {
            if (client2DisconnectedEvent != null)
                client2DisconnectedEvent.Set();
        }

        private static void ircClient2_Error(object sender, IrcErrorEventArgs e)
        {
            if (client2ErrorEvent != null)
                client2ErrorEvent.Set();
        }

        private static void ircClient2_ProtocolError(object sender, IrcProtocolErrorEventArgs e)
        {
            // Ignore.
        }

        private static void ircClient2_Registered(object sender, EventArgs e)
        {
            ircClient2.LocalUser.JoinedChannel += ircClient2_LocalUser_JoinedChannel;
            ircClient2.LocalUser.LeftChannel += ircClient2_LocalUser_LeftChannel;
            ircClient2.LocalUser.MessageSent += ircClient2_LocalUser_MessageSent;
            ircClient2.LocalUser.NoticeSent += ircClient2_LocalUser_NoticeSent;
            ircClient2.LocalUser.MessageReceived += ircClient2_LocalUser_MessageReceived;
            ircClient2.LocalUser.NoticeReceived += ircClient2_LocalUser_NoticeReceived;

            if (client2RegisteredEvent != null)
                client2RegisteredEvent.Set();
        }

        private static void ircClient2_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined += ircClient2_Channel_UserJoined;
            e.Channel.UserLeft += ircClient2_Channel_UserLeft;
            e.Channel.MessageReceived += ircClient2_Channel_MessageReceived;
            e.Channel.NoticeReceived += ircClient2_Channel_NoticeReceived;

            if (client2ChannelJoinedEvent != null)
                client2ChannelJoinedEvent.Set();
        }

        private static void ircClient2_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.UserJoined -= ircClient2_Channel_UserJoined;
            e.Channel.UserLeft -= ircClient2_Channel_UserLeft;
            e.Channel.MessageReceived -= ircClient2_Channel_MessageReceived;
            e.Channel.NoticeReceived -= ircClient2_Channel_NoticeReceived;

            if (client2ChannelLeftEvent != null)
                client2ChannelLeftEvent.Set();
        }

        private static void ircClient2_LocalUser_MessageSent(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserMessageSentEvent != null)
                client2LocalUserMessageSentEvent.Set();
        }

        private static void ircClient2_LocalUser_NoticeSent(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserNoticeSentEvent != null)
                client2LocalUserNoticeSentEvent.Set();
        }

        private static void ircClient2_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserMessageReceivedEvent != null)
                client2LocalUserMessageReceivedEvent.Set();
        }

        private static void ircClient2_LocalUser_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2LocalUserNoticeReceivedEvent != null)
                client2LocalUserNoticeReceivedEvent.Set();
        }

        private static void ircClient2_Channel_UserJoined(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void ircClient2_Channel_UserLeft(object sender, IrcChannelUserEventArgs e)
        {
            //
        }

        private static void ircClient2_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2ChannelMessageReceivedEvent != null)
                client2ChannelMessageReceivedEvent.Set();
        }

        private static void ircClient2_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            if (client2ChannelNoticeReceivedEvent != null)
                client2ChannelNoticeReceivedEvent.Set();
        }

        #endregion

        #region CTCP Client 1 Event Handlers

        private static void ctcpClient1_PingResponseReceived(object sender, CtcpPingResponseReceivedEventArgs e)
        {
            if (e.User.NickName == ircClient2.LocalUser.NickName)
                client2PingTime = e.PingTime;

            if (ctcpClient1PingResponseReceivedEvent != null)
                ctcpClient1PingResponseReceivedEvent.Set();
        }

        private static void ctcpClient1_VersionResponseReceived(object sender, CtcpVersionResponseReceivedEventArgs e)
        {
            if (e.User.NickName == ircClient2.LocalUser.NickName)
                client2ReceivedVersionInfo = e.VersionInfo;

            if (ctcpClient1VersionResponseReceivedEvent != null)
                ctcpClient1VersionResponseReceivedEvent.Set();
        }

        private static void ctcpClient1_TimeResponseReceived(object sender, CtcpTimeResponseReceivedEventArgs e)
        {
            if (e.User.NickName == ircClient2.LocalUser.NickName)
                client2ReceivedTimeInfo = e.DateTime;

            if (ctcpClient1TimeResponseReceivedEvent != null)
                ctcpClient1TimeResponseReceivedEvent.Set();
        }

        private static void ctcpClient1_ActionReceived(object sender, CtcpMessageEventArgs e)
        {
            if (e.Source.NickName == ircClient2.LocalUser.NickName)
                client2ReceivedActionText = e.Text;

            if (ctcpClient1ActionReceivedEvent != null)
                ctcpClient1ActionReceivedEvent.Set();
        }

        #endregion

        #region CTCP Client 2 Event Handlers

        private static void ctcpClient2_PingResponseReceived(object sender, CtcpPingResponseReceivedEventArgs e)
        {
            if (ctcpClient2PingResponseReceivedEvent != null)
                ctcpClient2PingResponseReceivedEvent.Set();
        }

        private static void ctcpClient2_VersionResponseReceived(object sender, CtcpVersionResponseReceivedEventArgs e)
        {
            if (ctcpClient2VersionResponseReceivedEvent != null)
                ctcpClient2VersionResponseReceivedEvent.Set();
        }

        private static void ctcpClient2_TimeResponseReceived(object sender, CtcpTimeResponseReceivedEventArgs e)
        {
            if (ctcpClient2TimeResponseReceivedEvent != null)
                ctcpClient2TimeResponseReceivedEvent.Set();
        }

        private static void ctcpClient2_ActionReceived(object sender, CtcpMessageEventArgs e)
        {
            if (ctcpClient2ActionReceivedEvent != null)
                ctcpClient2ActionReceivedEvent.Set();
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
            stateManager.HasStates(IrcClientTestState.Client1Initialized);
            Assert.IsTrue(WaitForClientEvent(client1ConnectedEvent, 5000), "Client 1 connection to server timed out.");
            Assert.IsTrue(ircClient1.IsConnected, "Client 1 failed to connect to server.");
            stateManager.SetStates(IrcClientTestState.Client1Connected);

            stateManager.HasStates(IrcClientTestState.Client2Initialized);
            Assert.IsTrue(WaitForClientEvent(client2ConnectedEvent, 5000), "Client 2 connection to server timed out.");
            Assert.IsTrue(ircClient2.IsConnected, "Client 2 failed to connect to server.");
            stateManager.SetStates(IrcClientTestState.Client2Connected);
        }

        [TestMethod()]
        public void DisconnectTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Connected);
            ircClient1.Disconnect();
            Assert.IsTrue(client1DisconnectedEvent.WaitOne(5000), "Client 1 failed to disconnect from server.");
            stateManager.UnsetStates(IrcClientTestState.Client1Connected);
        }

        [TestMethod()]
        public void QuitTest()
        {
            stateManager.HasStates(IrcClientTestState.Client2Connected);
            ircClient2.Quit(quitMessage);
            Assert.IsTrue(client1UserQuitEvent.WaitOne(10000), "Client 2 failed to quit from server.");
            Assert.IsTrue(client2DisconnectedEvent.WaitOne(5000), "Client 2 failed to disconnect from server.");
            stateManager.UnsetStates(IrcClientTestState.Client2Connected);
        }

        [TestMethod()]
        public void RegisterTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Connected);
            Assert.IsTrue(WaitForClientEvent(client1RegisteredEvent, 20000),
                "Client 1 failed to register connection with server.");
            stateManager.SetStates(IrcClientTestState.Client1Registered);
            Assert.AreEqual(nickName1, ircClient1.LocalUser.NickName, "Client 1 nick name was not correctly set.");
            Assert.AreEqual(userName1, ircClient1.LocalUser.UserName, "Client 1 user name was not correctly set.");
            Assert.AreEqual(realName, ircClient1.LocalUser.RealName, "Client 1 real name was not correctly set.");

            stateManager.HasStates(IrcClientTestState.Client2Connected);
            Assert.IsTrue(WaitForClientEvent(client2RegisteredEvent, 20000),
                "Client 2 failed to register connection with server.");
            stateManager.SetStates(IrcClientTestState.Client2Registered);
            Assert.AreEqual(nickName2, ircClient2.LocalUser.NickName, "Client 2 nick name was not correctly set.");
            Assert.AreEqual(userName2, ircClient2.LocalUser.UserName, "Client 2 user name was not correctly set.");
            Assert.AreEqual(realName, ircClient2.LocalUser.RealName, "Client 2 real name was not correctly set.");
        }

        [TestMethod()]
        public void MotdTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsTrue(WaitForClientEvent(client1MotdReceivedEvent, 10000),
                "Client 1 did not receive MOTD from server.");
            Assert.IsTrue(ircClient1.MessageOfTheDay.Length > 0,
                "MOTD received from server is empty.");
        }

        [TestMethod()]
        public void NetworkInfoTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            ircClient1.GetNetworkInfo();
            Assert.IsTrue(WaitForClientEvent(client1NetworkInfoReceivedEvent, 10000),
                "Client 1 did not receive network information from server.");
        }

        [TestMethod()]
        public void ServerVersionTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            ircClient1.GetServerVersion();
            Assert.IsTrue(WaitForClientEvent(client1ServerVersionInfoReceivedEvent, 10000),
                "Client 1 did not receive version information from server.");
        }

        [TestMethod()]
        public void ServerTimeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            ircClient1.GetServerTime();
            Assert.IsTrue(WaitForClientEvent(client1ServerTimeReceivedEvent, 10000),
                "Client 1 did not receive date/time info from server.");
        }

        [TestMethod()]
        public void LocalUserChangeNickNameTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.AreEqual(nickName1, ircClient1.LocalUser.NickName,
                "Client 1 local user nick name is incorrect before update.");
            nickName1 += "-2";
            ircClient1.LocalUser.SetNickName(nickName1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserNickNameChangedEvent, 10000),
                "Client 1 failed to change local user nick name.");
            Assert.AreEqual(nickName1, ircClient1.LocalUser.NickName,
                "Client 1 local user nick name is incorrect after update.");
        }

        [TestMethod()]
        public void LocalUserModeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserModeChangedEvent, 10000),
                "Client 1 failed to receive initial local user mode.");
            Assert.IsTrue(ircClient1.LocalUser.Modes.Contains('i'),
                "Client 1 local user does not initially have mode 'i'.");
            Assert.IsFalse(ircClient1.LocalUser.Modes.Contains('w'), "Client 1 local user already has mode 'w'.");
            ircClient1.LocalUser.SetModes("+w");
            Assert.IsTrue(WaitForClientEvent(client1LocalUserModeChangedEvent, 10000),
                "Client 1 failed to change local user mode.");
            Assert.IsTrue(ircClient1.LocalUser.Modes.Contains('w'), "Client 1 local user modes are unchanged.");
        }

        [TestMethod()]
        public void LocalUserAwayTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            Assert.IsFalse(ircClient1.LocalUser.IsAway, "Client 1 local user is already away.");
            ircClient1.LocalUser.SetAway("I'm away now.");
            Assert.IsTrue(WaitForClientEvent(client1LocalUserIsAwayChangedEvent, 10000),
                "Client 1 local user did not change Away status.");
            Assert.IsTrue(ircClient1.LocalUser.IsAway, "Client 1 local user is not away.");
            ircClient1.LocalUser.UnsetAway();
            Assert.IsTrue(WaitForClientEvent(client1LocalUserIsAwayChangedEvent, 10000),
                "Client 1 local user did not change Away status.");
            Assert.IsFalse(ircClient1.LocalUser.IsAway, "Client 1 local user is still away.");
        }

        [TestMethod()]
        public void JoinChannelTest()
        {
            // Generate random name of channel that has very low probability of already existing.
            testChannelName = string.Format("#ircsil-test-{0}", Guid.NewGuid().ToString().Substring(0, 13));

            stateManager.HasStates(IrcClientTestState.Client1Registered);
            ircClient1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Client 1 could not join channel.");
            stateManager.SetStates(IrcClientTestState.Client1InChannel);

            stateManager.HasStates(IrcClientTestState.Client2Registered);
            ircClient2.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client2ChannelJoinedEvent, 10000), "Client 2 could not join channel.");
            stateManager.SetStates(IrcClientTestState.Client2InChannel);

            // Check that client 1 has seen both local user and client 2 user join channel.
            var client1Channel = ircClient1.Channels.First();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUsersListReceivedEvent, 10000),
                "Client 1 did not receive initial list of users.");
            Assert.IsTrue(client1Channel.Users.Count >= 1 &&
                client1Channel.Users[0].User == ircClient1.LocalUser,
                "Client 1 does not see local user in channel.");
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserJoinedEvent, 10000),
                "Client 1 did not see client 2 user join channel.");
            Assert.IsTrue(client1Channel.Users.Count >= 2 &&
                client1Channel.Users[1].User.NickName == ircClient2.LocalUser.NickName,
                "Client 1 does not see client 2 user in channel.");
        }

        [TestMethod()]
        public void RejoinChannelTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            ircClient1.Channels.Join(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelJoinedEvent, 10000), "Client 1 could not rejoin channel.");
            stateManager.SetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void PartChannelTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            client1ChannelLeftEvent.Reset();
            ircClient1.Channels.Leave(new[] { testChannelName }, testComment1);
            Assert.IsTrue(WaitForClientEvent(client1ChannelLeftEvent, 10000), "Client 1 could not leave channel.");
            // Ignore channel leve comment, since it is not handled if client has not been connected long enough.
            stateManager.UnsetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void ChannelUsersListReceivedTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = ircClient1.Channels.Single(c => c.Name == testChannelName);

            Assert.IsTrue(channel.Users.Count == 1 || channel.Users.Count == 2,
                "Client 1 channel has unexpected number of users.");
            Assert.AreEqual(ircClient1.LocalUser, channel.Users[0].User,
                "Client 1 local user does not appear in channel.");
        }

        [TestMethod()]
        public void ChannelLocalUserModeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = ircClient1.Channels.Single(c => c.Name == testChannelName);
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
            var channel = ircClient1.Channels.Single(c => c.Name == testChannelName);

            Assert.IsTrue(WaitForClientEvent(client1ChannelModeChangedEvent, 10000),
                "Client 1 channel mode was not initialized.");
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
            ircClient1.LocalUser.SendMessage(ircClient2.LocalUser.NickName, testMessage1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserMessageSentEvent, 5000),
                "Client 1 local user did not send private message to client 2 user.");
            Assert.IsTrue(WaitForClientEvent(client2LocalUserMessageReceivedEvent, 5000),
                "Client 2 local user did not receive private message from client 1 user.");

            // Send private message to channel.
            var client1Channel = ircClient1.Channels.Single(c => c.Name == testChannelName);
            var client2Channel = ircClient2.Channels.Single(c => c.Name == testChannelName);
            ircClient1.LocalUser.SendMessage(client1Channel, testMessage2);
            Assert.IsTrue(WaitForClientEvent(client2ChannelMessageReceivedEvent, 5000),
                "Client 2 channel did not receive private message from client 1 user.");
        }

        [TestMethod()]
        public void ChannelSendAndReceiveNotice()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);

            // Send notice to user of client 2.
            ircClient1.LocalUser.SendNotice(ircClient2.LocalUser.NickName, testMessage1);
            Assert.IsTrue(WaitForClientEvent(client1LocalUserNoticeSentEvent, 5000),
                "Client 1 user did not send notice to client 2 user.");
            Assert.IsTrue(WaitForClientEvent(client2LocalUserNoticeReceivedEvent, 5000),
                "Client 2 user did not receive notice from client 1 user.");

            // Send notice to channel.
            var client1Channel = ircClient1.Channels.Single(c => c.Name == testChannelName);
            var client2Channel = ircClient2.Channels.Single(c => c.Name == testChannelName);
            ircClient1.LocalUser.SendNotice(client1Channel, testMessage2);
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
                ircClient1.LocalUser.SendMessage(testChannelName, spamMessage);
            }

            // Check that all sent messages are eventually received.
            var messageWaitPeriod = ((IrcStandardFloodPreventer)ircClient1.FloodPreventer).CounterPeriod * 2;
            for (int i = 1; i <= messageCount; i++)
            {
                Assert.IsTrue(WaitForClientEvent(client2ChannelMessageReceivedEvent, messageWaitPeriod),
                    "Client 1 channel did not receive message {0} of {1} for flood prevention test",
                    i, messageCount);
            }

            // If user does not get booted from server for "excess flood", then flood prevention is working correctly.
            Assert.IsTrue(ircClient1.IsConnected,
                "Client 1 is no longer connected to the server after attempting to flood the test channel.");
            Assert.IsTrue(ircClient1.Channels.Any(c => c.Name == testChannelName),
                "Client 1 is no longer a member of the test channel after trying to flood it.");
        }

        [TestMethod()]
        public void ChannelLocalUserKickTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            var channel = ircClient1.Channels.Single(c => c.Name == testChannelName);
            var channelUser = channel.GetChannelUser(ircClient1.LocalUser);
            Assert.IsTrue(channel != null, "Cannot find local user in channel.");

            channelUser.Kick();
            Assert.IsTrue(WaitForClientEvent(client1ChannelUserKickedEvent, 10000),
                "Client 1 could not kick local user from channel.");
            Assert.IsFalse(ircClient1.Channels.Contains(channel),
                "Client 1 collection of channels still contains channel from which local user kicked.");
            stateManager.UnsetStates(IrcClientTestState.Client1InChannel);
        }

        [TestMethod()]
        public void ListChannelsTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            stateManager.HasStates(IrcClientTestState.Client2InChannel);

            ircClient1.ListChannels(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1ChannelListReceivedEvent, 10000),
                "Client 1 did not receive channel list from server.");

            Assert.IsTrue(client1ListedChannels.Count == 1,
                "Client 1 received an unexpected number of listed channels.");
            var channel = ircClient1.Channels.Single(c => c.Name == testChannelName);
            var channelInfo1 = client1ListedChannels[0];
            Assert.AreEqual(channel.Name, channelInfo1.Name);
            Assert.AreEqual(channel.Topic ?? string.Empty, channelInfo1.Topic);
            Assert.AreEqual(channel.Users.Count, channelInfo1.VisibleUsersCount);
        }

        [TestMethod()]
        public void WhoTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1InChannel);
            stateManager.HasStates(IrcClientTestState.Client2InChannel);
            ircClient1.QueryWho(testChannelName);
            Assert.IsTrue(WaitForClientEvent(client1WhoReplyReceivedEvent, 10000),
                "Client 1 did not receive reply to Who query.");

            var user1 = ircClient1.Users.Single(u => u.NickName == nickName1);
            Assert.AreEqual(realName, user1.RealName);
            Assert.IsTrue(user1.HostName != null && user1.HostName.Length > 1);
            Assert.AreEqual(ircClient1.ServerName, user1.ServerName);
            Assert.IsFalse(user1.IsOperator);
            Assert.IsFalse(user1.IsAway);
            Assert.AreEqual(user1.HopCount, 0);
            var user1Channel = ircClient1.Channels.First();
            Assert.IsTrue(user1.GetChannelUsers().Any(cu => cu.Channel == user1Channel));

            var user2 = ircClient1.Users.Single(u => u.NickName == nickName2);
            Assert.AreEqual(realName, user2.RealName);
            Assert.IsTrue(user2.HostName != null && user2.HostName.Length > 1);
            Assert.AreEqual(ircClient2.ServerName, user2.ServerName);
            Assert.IsFalse(user2.IsOperator);
            Assert.IsFalse(user2.IsAway);
            Assert.AreEqual(user2.HopCount, 0);
            var user2Channel = ircClient1.Channels.First();
            Assert.IsTrue(user2.GetChannelUsers().Any(cu => cu.Channel == user2Channel));
        }

        [TestMethod()]
        public void WhoIsTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            stateManager.HasStates(IrcClientTestState.Client2InChannel);
            var whoIsUser = ircClient1.Users.Single(u => u.NickName == ircClient2.LocalUser.NickName);

            whoIsUser.WhoIs();
            Assert.IsTrue(WaitForClientEvent(client1WhoIsReplyReceivedEvent, 10000),
                "Client 1 did not receive reply to WhoIs query.");
            Assert.AreEqual(realName, whoIsUser.RealName);
            Assert.IsTrue(whoIsUser.HostName != null && whoIsUser.HostName.Length > 1);
            Assert.AreEqual(ircClient2.ServerName, whoIsUser.ServerName);
            Assert.IsFalse(whoIsUser.IsOperator);
            var channel = ircClient1.Channels.First();
            Assert.IsTrue(whoIsUser.GetChannelUsers().First().Channel == channel);
        }

        [TestMethod()]
        public void WhoWasTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered);
            stateManager.HasNotStates(IrcClientTestState.Client2Connected);
            ircClient1.QueryWhoWas(nickName2);
            Assert.IsTrue(WaitForClientEvent(client1WhoWasReplyReceivedEvent, 10000),
                "Client 1 did not receive reply to WhoWas query.");
            var whoWasUser = ircClient1.Users.SingleOrDefault(u => u.NickName == nickName2);
            Assert.IsNotNull(whoWasUser, "Client 1 does not contain user of WhoWas query in user list.");
            Assert.AreEqual(userName2, whoWasUser.NickName);
            Assert.AreEqual(realName, whoWasUser.RealName);
            Assert.IsTrue(whoWasUser.HostName != null && whoWasUser.HostName.Length > 1);
            Assert.AreEqual(ircClient2.ServerName, whoWasUser.ServerName);
        }

        [TestMethod()]
        public void CtcpPingTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered, IrcClientTestState.Client2Registered);
            ctcpClient1.Ping(ircClient2.LocalUser);
            const int pingTimeoutMilliseconds = 10000;
            Assert.IsTrue(WaitForClientEvent(ctcpClient1PingResponseReceivedEvent, pingTimeoutMilliseconds));
            Assert.IsTrue(client2PingTime.TotalMilliseconds > 0d &&
                client2PingTime.TotalMilliseconds < pingTimeoutMilliseconds);
        }

        [TestMethod()]
        public void CtcpVersionTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered, IrcClientTestState.Client2Registered);
            ctcpClient1.GetVersion(ircClient2.LocalUser);
            Assert.IsTrue(WaitForClientEvent(ctcpClient1VersionResponseReceivedEvent, 10000));
            Assert.AreEqual(clientVersionInfo, client2ReceivedVersionInfo);
        }

        [TestMethod()]
        public void CtcpTimeTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered, IrcClientTestState.Client2Registered);
            ctcpClient1.GetTime(ircClient2.LocalUser);
            Assert.IsTrue(WaitForClientEvent(ctcpClient1TimeResponseReceivedEvent, 10000));
            DateTimeOffset client2LocalDateTime;
            Assert.IsTrue(DateTimeOffset.TryParse(client2ReceivedTimeInfo, out client2LocalDateTime));
        }

        [TestMethod()]
        public void CtcpActionTest()
        {
            stateManager.HasStates(IrcClientTestState.Client1Registered, IrcClientTestState.Client2Registered);
            ctcpClient2.SendAction(ircClient1.LocalUser, testMessage1);
            Assert.IsTrue(WaitForClientEvent(ctcpClient1ActionReceivedEvent, 10000));
            Assert.AreEqual(testMessage1, client2ReceivedActionText);
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

    // Defines set of test states managed by TestStateManager. Any number of states may be set at any time.
    public enum IrcClientTestState
    {
        None,
        Client1Initialized,
        Client2Initialized,
        Client1Connected,
        Client2Connected,
        Client1Registered,
        Client2Registered,
        Client1InChannel,
        Client2InChannel,
    }
}
