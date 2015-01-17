using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents an object that raises an event when a message or notice has been received.
    /// </summary>
    public interface IIrcMessageReceiver
    {
        /// <summary>
        /// Occurs when a message has been received by the object.
        /// </summary>
        event EventHandler<IrcMessageEventArgs> MessageReceived;

        /// <summary>
        /// Occurs when a notice has been received by the object.
        /// </summary>
        event EventHandler<IrcMessageEventArgs> NoticeReceived;
    }
}
