using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using IrcDotNet;
using System.Text.RegularExpressions;

namespace MarkovChainTextBox
{
    internal static class Program
    {
        private static readonly Random random = new Random();
        private static readonly char[] sentenceSeparators = new[] {
            '.', '!', '?', ',', ';', ':' };
        private static readonly Regex cleanWordRegex = new Regex(@"[()\[\]{}'""`~]");

        private static IrcClient client;
        private static MarkovChain<string> markovChain;

        private static bool isRunning;

        #region Assembly Info

        public static string AssemblyTitle
        {
            get
            {
                return ((AssemblyTitleAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(
                    typeof(AssemblyTitleAttribute), false)[0]).Title;
            }
        }

        public static string AssemblyCopyright
        {
            get
            {
                return ((AssemblyCopyrightAttribute)Assembly.GetExecutingAssembly().GetCustomAttributes(
                    typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
            }
        }

        public static Version AssemblyVersion
        {
            get
            {
                return Assembly.GetExecutingAssembly().GetName().Version;
            }
        }

        #endregion

        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine(AssemblyTitle);
                Console.WriteLine("Version {0}", AssemblyVersion);
                Console.WriteLine(AssemblyCopyright);
                Console.WriteLine();

                client = new IrcClient();
                client.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
                client.Registered += client_Registered;
                client.Connect("irc.freenode.net", new IrcUserRegistrationInfo()
                    {
                        NickName = "MarkovBot",
                        UserName = "MarkovBot",
                        RealName = "Markov Chain Text Bot"
                    });

                markovChain = new MarkovChain<string>();

                ReadLoop();
            }
#if !DEBUG
            catch (Exception ex)
            {
                WriteError("Fatal error: " + ex.Message);
                Environment.ExitCode = 1;
            }
#endif
            finally
            {
                if (client != null)
                {
                    client.Quit(1000, "Shutting down.");
                    client.Dispose();
                }
            }
        }

        private static void client_Registered(object sender, EventArgs e)
        {
            client.LocalUser.JoinedChannel += client_LocalUser_JoinedChannel;
        }

        private static void client_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.MessageReceived += client_Channel_MessageReceived;
            e.Channel.NoticeReceived += client_Channel_NoticeReceived;
        }

        private static void client_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            //
        }

        private static void client_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            foreach (var target in e.Targets)
            {
                var channel = target as IrcChannel;
                if (e.Source is IrcUser && channel != null)
                {
                    // Train Markov generator from received message, assuming it is composed of one or more coherent
                    // sentences which themselves are composed of words.
                    var sentences = e.Text.Split(sentenceSeparators);
                    foreach (var s in sentences)
                    {
                        string lastWord = null;
                        foreach (var word in s.Split(' ').Select(w => cleanWordRegex.Replace(w, string.Empty)))
                        {
                            if (word.Length == 0)
                                continue;
                            // Ignore word if it is first in sentence and same as nick name.
                            if (lastWord == null && channel.Users.Any(cu => cu.User.NickName.Equals(word,
                                StringComparison.InvariantCultureIgnoreCase)))
                                break;

                            markovChain.Train(lastWord, word);
                            lastWord = word;
                        }
                        markovChain.Train(lastWord, null);
                    }
                }
            }
        }

        private static void ReadLoop()
        {
            string line;
            isRunning = true;
            while (isRunning)
            {
                Console.Write("> ");
                line = Console.ReadLine();
                if (line == null)
                    break;
                if (line.Length == 0)
                    continue;

                var parts = line.Split(' ');
                var command = parts[0].ToLower();
                var parameters = parts.Skip(1).ToArray();
                ReadCommand(command, parameters);
            }
        }

        private static void ReadCommand(string command, string[] parameters)
        {
            try
            {
                switch (command)
                {
                    case "exit":
                        ProcessCommandExit(parameters);
                        break;
                    case "join":
                        ProcessCommandJoin(parameters);
                        break;
                    case "talk":
                        ProcessCommandTalk(parameters);
                        break;
                    default:
                        UseTextColour(ConsoleColor.Red, () => Console.WriteLine("Unrecognised command."));
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError(ex.Message);
            }
        }

        #region Command Processors

        private static void ProcessCommandExit(string[] parameters)
        {
            isRunning = false;
        }

        private static void ProcessCommandJoin(string[] parameters)
        {
            if (parameters.Length < 1)
                throw new ArgumentException("Channel name was not specified.");

            var channelName = parameters[0];
            client.Channels.Join(channelName);
        }

        private static void ProcessCommandLeave(string[] parameters)
        {
            if (parameters.Length < 1)
                throw new ArgumentException("Channel name was not specified.");

            var channelName = parameters[0];
            client.Channels.Leave(channelName);
        }

        private static void ProcessCommandTalk(string[] parameters)
        {
            if (parameters.Length < 1)
                throw new ArgumentException("Channel name was not specified.");

            // Use Markov chain to generate random message, composed of one or more sentences.
            var messageTextBuilder = new StringBuilder();
            var numSentences = parameters.Length >= 2 ? int.Parse(parameters[1]) : random.Next(1, 4);
            for (int i = 0; i < numSentences; i++)
                messageTextBuilder.Append(GenerateRandomSentence());

            var channelName = parameters[0];
            client.LocalUser.SendMessage(channelName, messageTextBuilder.ToString());
        }

        private static string GenerateRandomSentence()
        {
            int trials = 0;
            string[] words;
            do
            {
                words = markovChain.GenerateSequence().ToArray();
            }
            while (words.Length < 3 && trials < 10);
            return string.Join(" ", words) + ". ";
        }

        #endregion

        private static void WriteError(string message)
        {
            UseTextColour(ConsoleColor.Red, () => Console.Error.WriteLine(message));
        }

        private static void UseTextColour(ConsoleColor colour, Action action)
        {
            var prevForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            action();
            Console.ForegroundColor = prevForegroundColor;
        }
    }
}
