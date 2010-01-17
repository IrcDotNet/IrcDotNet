using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    internal interface IIrcMessageReceiveHandler
    {
        void HandleMessageReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text);
        void HandleNoticeReceived(IIrcMessageSource source, IList<IIrcMessageTarget> targets, string text);
    }
}
