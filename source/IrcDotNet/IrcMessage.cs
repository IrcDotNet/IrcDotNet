using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using IrcDotNet.Properties;

namespace IrcDotNet
{
    partial class IrcClient
    {
        /// <summary>
        ///     Represents a method that processes <see cref="IrcMessage" /> objects.
        /// </summary>
        /// <param name="message">The message to be processed.</param>
        protected delegate void MessageProcessor(IrcMessage message);

        /// <inheritdoc cref="WriteMessage(string, object)" />
        /// <summary>
        ///     Writes the specified message (prefix, command, and parameters) to the network stream.
        /// </summary>
        /// <param name="message">The message to write.</param>
        /// <exception cref="ArgumentException">
        ///     <paramref name="message" /> contains more than 15 many parameters.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The value of <see cref="IrcMessage.Command" /> of
        ///     <paramref name="message" /> is invalid.
        /// </exception>
        /// <exception cref="ArgumentException">
        ///     The value of one of the items of <see cref="IrcMessage.Parameters" /> of
        ///     <paramref name="message" /> is invalid.
        /// </exception>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(IrcMessage message)
        {
            CheckDisposed();

            if (message.Command == null)
                throw new ArgumentException(Resources.MessageInvalidCommand, nameof(message));
            if (message.Parameters.Count > MAX_PARAMS_COUNT)
                throw new ArgumentException(Resources.MessageTooManyParams, nameof(message));

            StringBuilder lineBuilder = new StringBuilder();

            // Append prefix to line, if specified.
            if (message.Prefix != null)
                lineBuilder.Append(":" + CheckPrefix(message.Prefix) + " ");

            // Append command name to line.
            lineBuilder.Append(CheckCommand(message.Command).ToUpper());

            // Append each parameter to line, adding ':' character before last parameter.
            for (int i = 0; i < message.Parameters.Count - 1; i++)
            {
                if (message.Parameters[i] != null)
                    lineBuilder.Append(" " + CheckMiddleParameter(message.Parameters[i]));
            }

            if (message.Parameters.Count > 0)
            {
                string lastParameter = message.Parameters[message.Parameters.Count - 1];
                if (lastParameter != null)
                    lineBuilder.Append(" :" + CheckTrailingParameter(lastParameter));
            }

            // Send raw message as line of text.
            string line = lineBuilder.ToString();
            var messageSentEventArgs = new IrcRawMessageEventArgs(message, line);
            WriteMessage(line, messageSentEventArgs);
        }

        /// <inheritdoc cref="WriteMessage(string, string, string[])" />
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(string prefix, string command, IEnumerable<string> parameters)
        {
            CheckDisposed();

            WriteMessage(prefix, command, parameters.ToArray());
        }

        /// <inheritdoc cref="WriteMessage(IrcMessage)" />
        /// <param name="prefix">The message prefix that represents the source of the message.</param>
        /// <param name="command">The name of the command.</param>
        /// <param name="parameters">A collection of the parameters to the command.</param>
        /// <exception cref="ObjectDisposedException">The current instance has already been disposed.</exception>
        protected void WriteMessage(string prefix, string command, params string[] parameters)
        {
            CheckDisposed();

            IrcMessage message = new IrcMessage(this, prefix, command, parameters.ToArray());
            message.Source = message.Source ?? localUser;
            WriteMessage(message);
        }

        protected virtual void WriteMessage(string line, object token = null)
        {
            CheckDisposed();

            Debug.Assert(line != null);
        }

        private void ReadMessage(IrcMessage message)
        {
            // Try to find corresponding message processor for command of given message.
            MessageProcessor messageProcessor;
            int commandCode;
            if (messageProcessors.TryGetValue(message.Command, out messageProcessor) ||
                (int.TryParse(message.Command, out commandCode) &&
                 numericMessageProcessors.TryGetValue(commandCode, out messageProcessor)))
            {
                try
                {
                    messageProcessor(message);
                }
#if !DEBUG
                catch (Exception ex)
                {
                    OnError(new IrcErrorEventArgs(ex));
                }
#else
                catch (Exception)
                {
                    throw;
                }
#endif
            }
            else
            {
                // Unknown command.
                DebugUtilities.WriteEvent("Unknown IRC message command '{0}'.", message.Command);
            }
        }

        protected void ParseMessage(string line)
        {
            string prefix = null;
            string lineAfterPrefix;

            // Extract prefix from message line, if it contains one.
            if (line[0] == ':')
            {
                int firstSpaceIndex = line.IndexOf(' ');
                Debug.Assert(firstSpaceIndex != -1);
                prefix = line.Substring(1, firstSpaceIndex - 1);
                lineAfterPrefix = line.Substring(firstSpaceIndex + 1);
            }
            else
            {
                lineAfterPrefix = line;
            }

            // Extract command from message.
            int spaceIndex = lineAfterPrefix.IndexOf(' ');
            Debug.Assert(spaceIndex != -1);
            string command = lineAfterPrefix.Substring(0, spaceIndex);
            string paramsLine = lineAfterPrefix.Substring(command.Length + 1);

            // Extract parameters from message.
            // Each parameter is separated by single space, except last one, which may contain spaces if it
            // is prefixed by colon.
            string[] parameters = new string[MAX_PARAMS_COUNT];
            int paramEndIndex = -1;
            int lineColonIndex = paramsLine.IndexOf(" :", StringComparison.Ordinal);
            if (lineColonIndex == -1 && !paramsLine.StartsWith(":"))
            {
                lineColonIndex = paramsLine.Length;
            }

            for (int i = 0; i < parameters.Length; i++)
            {
                int paramStartIndex = paramEndIndex + 1;
                paramEndIndex = paramsLine.IndexOf(' ', paramStartIndex);

                if (paramEndIndex == -1)
                    paramEndIndex = paramsLine.Length;

                if (paramEndIndex > lineColonIndex)
                {
                    paramStartIndex++;
                    paramEndIndex = paramsLine.Length;
                }

                parameters[i] = paramsLine.Substring(paramStartIndex, paramEndIndex - paramStartIndex);
                if (paramEndIndex == paramsLine.Length)
                    break;
            }

            // Parse received IRC message.
            IrcMessage message = new IrcMessage(this, prefix, command, parameters);
            var messageReceivedEventArgs = new IrcRawMessageEventArgs(message, line);
            OnRawMessageReceived(messageReceivedEventArgs);
            ReadMessage(message);

#if DEBUG
            DebugUtilities.WriteIrcRawLine(this, ">>> " + messageReceivedEventArgs.RawContent);
#endif
        }

        /// <summary>
        ///     Represents a raw IRC message that is sent/received by <see cref="IrcClient" />.
        ///     A message contains a prefix (representing the source), a command name (a word or three-digit number),
        ///     and any number of parameters (up to a maximum of 15).
        /// </summary>
        /// <seealso cref="IrcClient" />
        [DebuggerDisplay("{" + nameof(ToString) + "(), nq}")]
        public struct IrcMessage
        {
            /// <summary>
            ///     The source of the message, which is the object represented by the value of <see cref="Prefix" />.
            /// </summary>
            public IIrcMessageSource Source;

            /// <summary>
            ///     The message prefix.
            /// </summary>
            public readonly string Prefix;

            /// <summary>
            ///     The name of the command.
            /// </summary>
            public readonly string Command;

            /// <summary>
            ///     A list of the parameters to the message.
            /// </summary>
            public readonly IList<string> Parameters;

            /// <summary>
            ///     Initializes a new instance of the <see cref="IrcMessage" /> structure.
            /// </summary>
            /// <param name="client">A client object that has sent/will receive the message.</param>
            /// <param name="prefix">The message prefix that represents the source of the message.</param>
            /// <param name="command">The command name; either an alphabetic word or 3-digit number.</param>
            /// <param name="parameters">
            ///     A list of the parameters to the message. Can contain a maximum of 15 items.
            /// </param>
            public IrcMessage(IrcClient client, string prefix, string command, IList<string> parameters)
            {
                Prefix = prefix;
                Command = command;
                Parameters = parameters;

                Source = client.GetSourceFromPrefix(prefix);
            }

            /// <summary>
            ///     Returns a string representation of this instance.
            /// </summary>
            /// <returns>A string that represents this instance.</returns>
            public override string ToString() => string.Format("{0} ({1} parameters)", Command, Parameters.Count);
        }
    }
}