using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using IrcDotNet;

namespace MarkovChainTextBox
{
    internal static class Program
    {
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

            client.Channels.Join("#ircsil");
        }

        private static void client_LocalUser_JoinedChannel(object sender, IrcChannelEventArgs e)
        {
            client.LocalUser.SendMessage(e.Channel, "This is the Markov Chain Text Box, ready for service.");

            for (int i = 0; i < 40; i++)
            {
                //
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
            switch (command)
            {
                case "exit":
                    ProcessCommandExit(parameters);
                    break;
                default:
                    UseTextColour(ConsoleColor.Red, () => Console.WriteLine("Unrecognised command."));
                    break;
            }
        }

        #region Command Processors

        private static void ProcessCommandExit(string[] parameters)
        {
            isRunning = false;
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
