using System.Collections.Generic;

namespace IrcDotNet
{
    /// <summary>
    ///     Represents an object that handles messages and notices sent by an IRC client.
    /// </summary>
    internal interface IIrcMessageSendHandler
    {
        /// <summary>
        ///     Handles the specified message that was sent by the client.
        /// </summary>
        /// <param name="targets">A collection of the target of the message.</param>
        /// <param name="text">The text of the message.</param>
        void HandleMessageSent(IList<IIrcMessageTarget> targets, string text);

        /// <summary>
        ///     Handles the specified notice that was sent by the client.
        /// </summary>
        /// <param name="targets">A collection of the target of the notice.</param>
        /// <param name="text">The text of the message.</param>
        void HandleNoticeSent(IList<IIrcMessageTarget> targets, string text);
    }
}