using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public interface IIrcFloodPreventer
    {
        bool CanSendMessage();

        void HandleMessageSent();
    }
}
