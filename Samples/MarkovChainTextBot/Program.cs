using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using IrcDotNet;

namespace MarkovChainTextBox
{
    internal static class Program
    {
        // Error messages
        private const string errorMessageNotEnoughArguments = "Not enough arguments were specified for the command.";

        private const int clientQuitTimeout = 1000;
        private const string clientQuitMessage = "Andrey Markov, 1856 - 1922";

        // Variables relating to Markov chain training and generation.
        private static readonly Random random = new Random();
        private static readonly char[] sentenceSeparators = new[] {
            '.', '!', '?', ',', ';', ':' };
        private static readonly Regex cleanWordRegex = new Regex(
            @"[()\[\]{}'""`~]");

        private static List<IrcClient> allClients;
        private static MarkovChain<string> markovChain;

        // True if the read loop is currently active, false if it has been told to exit.
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
                // Write information about program.
                Console.WriteLine(AssemblyTitle);
                Console.WriteLine("Version {0}", AssemblyVersion);
                Console.WriteLine(AssemblyCopyright);
                Console.WriteLine();

                allClients = new List<IrcClient>();
                markovChain = new MarkovChain<string>();

                // Read commands from the stdin stream until the program exists.
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
                foreach (var client in allClients)
                {
                    if (client != null)
                    {
                        client.Quit(clientQuitTimeout, clientQuitMessage);
                        client.Dispose();
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
                    case "connect":
                    case "c":
                        ProcessCommandConnect(parameters);
                        break;
                    case "disconnect":
                    case "d":
                        ProcessCommandDisconnect(parameters);
                        break;
                    case "join":
                        ProcessCommandJoin(parameters);
                        break;
                    case "leave":
                        ProcessCommandLeave(parameters);
                        break;
                    case "list":
                        ProcessCommandList(parameters);
                        break;
                    default:
                        WriteError("Command '{0}' not recognised.", command);
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteError("Error: " + ex.Message);
            }
        }

        #region Command Processors

        private static void ProcessCommandExit(string[] parameters)
        {
            isRunning = false;
        }

        private static void ProcessCommandConnect(string[] parameters)
        {
            if (parameters.Length < 1)
                throw new ArgumentException(errorMessageNotEnoughArguments);

            // Create a new IRC client and connect to the specified server, waiting until the connection has been
            // established or timed out.
            var client = new IrcClient();
            var serverAddress = parameters[0];
            client.FloodPreventer = new IrcStandardFloodPreventer(4, 2000);
            client.Connected += client_Connected;
            client.Registered += client_Registered;

            using (var connectedEvent = new ManualResetEventSlim(false))
            {
                client.Connected += (sender2, e2) => connectedEvent.Set();
                client.Connect(serverAddress, false, new IrcUserRegistrationInfo()
                    {
                        NickName = "MarkovBot",
                        UserName = "MarkovBot",
                        RealName = "Markov Chain Text Bot"
                    });
                if (!connectedEvent.Wait(10000))
                {
                    client.Dispose();
                    WriteError("Connection to '{0}' timed out.", serverAddress);
                    return;
                }
            }

            allClients.Add(client);
            Console.Out.WriteLine("Connected to '{0}'.", serverAddress);
        }

        private static void ProcessCommandDisconnect(string[] parameters)
        {
            if (parameters.Length < 1)
                throw new ArgumentException(errorMessageNotEnoughArguments);

            var client = GetClientFromServerNameMask(parameters[0]);
            var serverName = client.ServerName;
            client.Quit(clientQuitTimeout, clientQuitMessage);
            client.Dispose();

            allClients.Remove(client);
            Console.Out.WriteLine("Disconnected from '{0}'.", serverName);
        }

        private static void ProcessCommandJoin(string[] parameters)
        {
            if (parameters.Length < 2)
                throw new ArgumentException(errorMessageNotEnoughArguments);

            // Joins the specified channel on the specified server.
            var client = GetClientFromServerNameMask(parameters[0]);
            var channelName = parameters[1];
            client.Channels.Join(channelName);
        }

        private static void ProcessCommandLeave(string[] parameters)
        {
            if (parameters.Length < 2)
                throw new ArgumentException(errorMessageNotEnoughArguments);

            // Leaves the specified channel on the specified server.
            var client = GetClientFromServerNameMask(parameters[0]);
            var channelName = parameters[1];
            client.Channels.Leave(channelName);
        }

        private static void ProcessCommandList(string[] parameters)
        {
            // Lists all active server connections and the channels of which the local user is currently a member.
            foreach (var client in allClients)
            {
                Console.Out.WriteLine("Server: {0}", client.ServerName ?? "(unknown)");
                foreach (var channel in client.Channels)
                {
                    if (channel.Users.Any(u => u.User == client.LocalUser))
                    {
                        Console.Out.WriteLine(" - {0}", channel.Name);
                    }
                }
            }
        }

        #endregion

        private static void WriteError(string message, params string[] args)
        {
            UseTextColour(ConsoleColor.Red, () => Console.Error.WriteLine(message, args));
        }

        private static void UseTextColour(ConsoleColor colour, Action action)
        {
            var prevForegroundColor = Console.ForegroundColor;
            Console.ForegroundColor = colour;
            action();
            Console.ForegroundColor = prevForegroundColor;
        }

        private static bool ReadChatCommand(IrcClient client, IIrcMessageTarget target, string line)
        {
            // Check if line is chat command; if so, process it.
            if (line.Length > 1 && line.StartsWith("!"))
            {
                var parts = line.Substring(1).Split(' ');
                var command = parts[0];
                var parameters = parts.Skip(1).ToArray();
                ReadChatCommand(client, target, command, parameters);
                return true;
            }
            return false;
        }

        private static void ReadChatCommand(IrcClient client, IIrcMessageTarget target, string command,
            string[] parameters)
        {
            try
            {
                switch (command)
                {
                    case "talk":
                        ProcessChatCommandTalk(client, target, parameters);
                        break;
                    default:
                        client.LocalUser.SendNotice(target, string.Format(
                            "Command '{0}' not recognised.", command));
                        break;
                }
            }
            catch (Exception ex)
            {
                client.LocalUser.SendNotice(target, string.Format(
                    "Error processing '{0}' command: {1}", command, ex.Message));
            }
        }

        #region Chat Command Processors

        private static void ProcessChatCommandTalk(IrcClient client, IIrcMessageTarget target, string[] parameters)
        {
            // Sends random message (generated from Markov chain) to specified target.
            int numSentences = -1;
            if (parameters.Length >= 1)
                numSentences = int.Parse(parameters[0]);
            string higlightNickName = null;
            if (parameters.Length >= 2)
                higlightNickName = parameters[1] + ": ";
            SendRandomMessage(client, target, higlightNickName, numSentences);
        }

        #endregion

        private static void SendRandomMessage(IrcClient client, IIrcMessageTarget target, string textPrefix,
            int numSentences = -1)
        {
            if (markovChain.Nodes.Count == 0)
            {
                client.LocalUser.SendNotice(target, "Bot has not yet been trained.");
                return;
            }

            var textBuilder = new StringBuilder();
            if (textPrefix != null)
                textBuilder.Append(textPrefix);

            // Use Markov chain to generate random message, composed of one or more sentences.
            if (numSentences == -1)
                numSentences = random.Next(1, 4);
            for (int i = 0; i < numSentences; i++)
                textBuilder.Append(GenerateRandomSentence());

            client.LocalUser.SendMessage(target, textBuilder.ToString());
        }

        private static string GenerateRandomSentence()
        {
            // Generate sentence by using Markov chain to produce sequence of random words.
            // There should ideally be at least 3 words in the sentence.
            int trials = 0;
            string[] words;
            do
            {
                words = markovChain.GenerateSequence().ToArray();
            }
            while (words.Length < 3 && trials++ < 10);
            return string.Join(" ", words) + ". ";
        }

        private static IrcClient GetClientFromServerNameMask(string serverNameMask)
        {
            return allClients.Single(c => c.ServerName != null &&
                Regex.IsMatch(c.ServerName, serverNameMask, RegexOptions.IgnoreCase));
        }

        private static void client_Connected(object sender, EventArgs e)
        {
            //
        }

        private static void client_Registered(object sender, EventArgs e)
        {
            var client = (IrcClient)sender;
            client.LocalUser.MessageReceived += client_LocalUser_MessageReceived;
            client.LocalUser.JoinedChannel += client_LocalUser_JoinedChannel;
            client.LocalUser.LeftChannel += client_LocalUser_LeftChannel;
        }

        private static void client_LocalUser_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            var localUser = (IrcLocalUser)sender;
            var client = localUser.Client;

            if (e.Source is IrcUser)
            {
                var sourceUser = (IrcUser)e.Source;

                // If message is chat command, process it.
                if (ReadChatCommand(client, sourceUser, e.Text))
                    return;
            }
        }

        private static void client_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.MessageReceived += client_Channel_MessageReceived;
            e.Channel.NoticeReceived += client_Channel_NoticeReceived;
        }

        private static void client_LocalUser_LeftChannel(object sender, IrcChannelEventArgs e)
        {
            e.Channel.MessageReceived -= client_Channel_MessageReceived;
            e.Channel.NoticeReceived -= client_Channel_NoticeReceived;
        }

        private static void client_Channel_NoticeReceived(object sender, IrcMessageEventArgs e)
        {
            //
        }

        private static void client_Channel_MessageReceived(object sender, IrcMessageEventArgs e)
        {
            var channel = (IrcChannel)sender;
            var client = channel.Client;

            if (e.Source is IrcUser)
            {
                // If message is chat command, process it.
                if (ReadChatCommand(client, channel, e.Text))
                    return;

                // Train Markov generator from received message, assuming it is composed of one or more coherent
                // sentences, which themselves are composed of words.
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
}
