using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public interface IIrcMessageTarget
    {
        string Name { get; }
    }
}
