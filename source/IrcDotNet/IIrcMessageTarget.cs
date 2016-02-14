using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents the target of a message or notice sent by an IRC client.
    /// </summary>
    public interface IIrcMessageTarget
    {
        /// <summary>
        /// Gets the name of the source, as understood by the IRC protocol.
        /// </summary>
        /// <value>The name of the target.</value>
        string Name { get; }
    }
}
