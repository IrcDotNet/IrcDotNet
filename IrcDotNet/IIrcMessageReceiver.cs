using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public interface IIrcMessageReceiver
    {
        event EventHandler<IrcMessageEventArgs> MessageReceived;
        event EventHandler<IrcMessageEventArgs> NoticeReceived;
    }
}
