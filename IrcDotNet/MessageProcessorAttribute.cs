using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Indicates that a method processes messages for a given command.
    /// </summary>
    internal class MessageProcessorAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MessageProcessorAttribute"/> class.
        /// </summary>
        /// <param name="command">The name of the command for which messages are processed.</param>
        public MessageProcessorAttribute(string command)
        {
            this.Command = command;
        }

        /// <summary>
        /// Gets the name of the command for which messages are processed.
        /// </summary>
        /// <value>The name of the command.</value>
        public string Command
        {
            get;
            private set;
        }
    }
}
