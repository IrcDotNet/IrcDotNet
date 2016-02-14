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

        // Load settings from env, or use defaults.
        static TestSettings()
        {
            MessageProtocolError = "Client {0}: protocol error {1}: {2}\nParameters: {2}";
        }
    }
}