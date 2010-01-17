using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    internal interface IIrcMessageSendHandler
    {
        void HandleMessageSent(IList<IIrcMessageTarget> targets, string text);
        void HandleNoticeSent(IList<IIrcMessageTarget> targets, string text);
    }
}
