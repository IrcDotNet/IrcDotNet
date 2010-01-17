using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcServer : IIrcMessageSource
    {
        private string hostName;

        internal IrcServer(string hostName)
        {
            this.hostName = hostName;
        }

        public string HostName
        {
            get { return this.hostName; }
        }

        public override string ToString()
        {
            return this.hostName;
        }

        #region IIrcMessageSource Members

        string IIrcMessageSource.Name
        {
            get { return this.HostName; }
        }

        #endregion
    }
}
