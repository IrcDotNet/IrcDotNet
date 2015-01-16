using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents an object that handles messages and notices received by an IRC client.
    /// </summary>
    internal interface IIrcMessageReceiveHandler
    {
        /// <summary>
        /// Handles the specified message that was received by the client.
        /// </summary>
        /// <param name="source">The source of the message.</param>
        /// <param name="targets">A collection of the target of the message.</param>
        /// <param name="text">The text of the message.</param>
        void HandleMessageReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text);

        /// <summary>
        /// Handles the specified notice that was received by the client.
        /// </summary>
        /// <param name="source">The source of the notice.</param>
        /// <param name="targets">A collection of the target of the notice.</param>
        /// <param name="text">The text of the message.</param>
        void HandleNoticeReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text);
    
    }
}
