using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace IrcDotNet.Test
{
    public static class TestSettings
    {
        /// <summary>
        /// Server host name
        /// </summary>
        public static string ServerHostName { get; set; }

        /// <summary>
        /// Server port
        /// </summary>
        public static int ServerPort { get; set; }

        /// <summary>
        /// Test server password.
        /// </summary>
        public static string ServerPassword { get; set; }

        /// <summary>
        /// Test real name.
        /// </summary>
        public static string RealName { get; set; }

        /// <summary>
        /// Nickname format
        /// </summary>
        public static string NickNameFormat { get; set; }

        /// <summary>
        /// Message protocol error
        /// </summary>
        public static string MessageProtocolError { get; set; }

        private static string Env(string key)
        {
            var result = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(result))
                result = null;
            return result;
        }

        // Load settings from env, or use defaults.
        static TestSettings()
        {
            MessageProtocolError = Env("MessageProtocolError") ?? "Client {0}: protocol error {1}: {2}\nParameters: {2}";
            NickNameFormat = Env("NickNameFormat") ?? "itb-{0}";
            RealName = Env("RealName") ?? "IRC.Net Tester";
            ServerHostName = Env("ServerHostName") ?? "irc.freenode.net";
            ServerPort = Env("ServerPort") == null ? 8000 : Int32.Parse(Env("ServerPort"));  //Appveyor blocks port 6667
            ServerPassword = Env("ServerPassword") ?? "";
        }
    }
}