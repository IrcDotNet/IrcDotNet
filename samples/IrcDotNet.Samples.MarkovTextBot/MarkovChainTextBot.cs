using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using IrcDotNet;
using IrcDotNet.Samples.Common;

namespace MarkovChainTextBox
{
    public class MarkovChainTextBot : BasicIrcBot
    {
        private const string quitMessage = "Andrey Markov, 1856 - 1922";

        // Bot statistics
        private DateTime launchTime;
        private int numTrainingMessagesReceived;
        private int numTrainingWordsReceived;

        // Markov chain training and generation
        private readonly Random random = new Random();
        private readonly char[] sentenceSeparators = new[] {
            '.', '!', '?', ',', ';', ':' };
        private readonly Regex cleanWordRegex = new Regex(
            @"[()\[\]{}'""`~]");

        // Markov chain object
        private MarkovChain<string> markovChain;

        public MarkovChainTextBot()
            : base()
        {
            this.markovChain = new MarkovChain<string>();

            this.launchTime = DateTime.Now;
            this.numTrainingMessagesReceived = 0;
            this.numTrainingWordsReceived = 0;
        }

        public override IrcRegistrationInfo RegistrationInfo
        {
            get
            {
                return new IrcUserRegistrationInfo()
                    {
                        NickName = "MarkovBot",
                        UserName = "MarkovBot",
                        RealName = "Markov Chain Text Bot"
                    };
            }
        }

        public override string QuitMessage
        {
            get { return quitMessage; }
        }

        protected override void OnClientConnect(IrcClient client)
        {
            //
        }

        protected override void OnClientDisconnect(IrcClient client)
        {
            //
        }

        protected override void OnClientRegistered(IrcClient client)
        {
            //
        }

        protected override void OnLocalUserJoinedChannel(IrcLocalUser localUser, IrcChannelEventArgs e)
        {
            //
        }

        protected override void OnLocalUserLeftChannel(IrcLocalUser localUser, IrcChannelEventArgs e)
        {
            //
        }

        protected override void OnLocalUserNoticeReceived(IrcLocalUser localUser, IrcMessageEventArgs e)
        {
            //
        }

        protected override void OnLocalUserMessageReceived(IrcLocalUser localUser, IrcMessageEventArgs e)
        {
            //
        }

        protected override void OnChannelUserJoined(IrcChannel channel, IrcChannelUserEventArgs e)
        {
            //
        }

        protected override void OnChannelUserLeft(IrcChannel channel, IrcChannelUserEventArgs e)
        {
            //
        }

        protected override void OnChannelNoticeReceived(IrcChannel channel, IrcMessageEventArgs e)
        {
            //
        }

        protected override void OnChannelMessageReceived(IrcChannel channel, IrcMessageEventArgs e)
        {
            var client = channel.Client;

            if (e.Source is IrcUser)
            {
                // Train Markov generator from received message text.
                // Assume it is composed of one or more coherent sentences that are themselves are composed of words.
                var sentences = e.Text.Split(sentenceSeparators);
                foreach (var s in sentences)
                {
                    string lastWord = null;
                    foreach (var w in s.Split(' ').Select(w => cleanWordRegex.Replace(w, string.Empty)))
                    {
                        if (w.Length == 0)
                            continue;
                        // Ignore word if it is first in sentence and same as nick name.
                        if (lastWord == null && channel.Users.Any(cu => cu.User.NickName.Equals(w,
                            StringComparison.InvariantCultureIgnoreCase)))
                            break;

                        markovChain.Train(lastWord, w);
                        lastWord = w;
                        this.numTrainingWordsReceived++;
                    }
                    markovChain.Train(lastWord, null);
                }

                this.numTrainingMessagesReceived++;
            }
        }

        protected override void InitializeChatCommandProcessors()
        {
            base.InitializeChatCommandProcessors();

            this.ChatCommandProcessors.Add("talk", ProcessChatCommandTalk);
            this.ChatCommandProcessors.Add("stats", ProcessChatCommandStats);
        }

        #region Chat Command Processors

        private void ProcessChatCommandTalk(IrcClient client, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string command, IList<string> parameters)
        {
            // Send reply containing random message text (generated from Markov chain).
            int numSentences = -1;
            if (parameters.Count >= 1)
                numSentences = int.Parse(parameters[0]);
            string higlightNickName = null;
            if (parameters.Count >= 2)
                higlightNickName = parameters[1] + ": ";
            SendRandomMessage(client, GetDefaultReplyTarget(client, source, targets),
                higlightNickName, numSentences);
        }

        private void ProcessChatCommandStats(IrcClient client, IIrcMessageSource source,
            IList<IIrcMessageTarget> targets, string command, IList<string> parameters)
        {
            // Send reply with bot statistics.
            var replyTargets = GetDefaultReplyTarget(client, source, targets);

            client.LocalUser.SendNotice(replyTargets, "Bot launch time: {0:f} ({1:g} ago)",
                this.launchTime,
                DateTime.Now - this.launchTime);
            client.LocalUser.SendNotice(replyTargets, "Number of training messages received: {0:#,#0} ({1:#,#0} words)",
                this.numTrainingMessagesReceived,
                this.numTrainingWordsReceived);
            client.LocalUser.SendNotice(replyTargets, "Number of unique words in vocabulary: {0:#,#0}",
                this.markovChain.Nodes.Count);
        }

        #endregion

        private void SendRandomMessage(IrcClient client, IList<IIrcMessageTarget> targets, string textPrefix,
            int numSentences = -1)
        {
            if (this.markovChain.Nodes.Count == 0)
            {
                client.LocalUser.SendNotice(targets, "Bot has not yet been trained.");
                return;
            }

            var textBuilder = new StringBuilder();
            if (textPrefix != null)
                textBuilder.Append(textPrefix);

            // Use Markov chain to generate random message, composed of one or more sentences.
            if (numSentences == -1)
                numSentences = this.random.Next(1, 4);
            for (int i = 0; i < numSentences; i++)
                textBuilder.Append(GenerateRandomSentence());

            client.LocalUser.SendMessage(targets, textBuilder.ToString());
        }

        private string GenerateRandomSentence()
        {
            // Generate sentence by using Markov chain to produce sequence of random words.
            // Note: There must be at least three words in sentence.
            int trials = 0;
            string[] words;
            do
            {
                words = this.markovChain.GenerateSequence().ToArray();
            }
            while (words.Length < 3 && trials++ < 10);

            return string.Join(" ", words) + ". ";
        }

        protected override void InitializeCommandProcessors()
        {
            base.InitializeCommandProcessors();
        }

        #region Command Processors

        //

        #endregion
    }
}
