using System;
using System.Diagnostics;

namespace IrcDotNet
{
    public class InvalidCommandParametersException : Exception
    {
        public InvalidCommandParametersException(int minParameters, int? maxParameters = null)
            : base()
        {
            Debug.Assert(minParameters >= 0,
                "minParameters must be at least zero.");
            Debug.Assert(maxParameters == null || maxParameters >= minParameters,
                "maxParameters must be at least minParameters.");

            this.MinParameters = minParameters;
            this.MaxParameters = maxParameters ?? minParameters;
        }

        public int MinParameters
        {
            get;
            private set;
        }

        public int MaxParameters
        {
            get;
            private set;
        }

        public override string Message
        {
            get
            {
                throw new NotSupportedException();
            }
        }

        public string GetMessage(string command)
        {
            if (this.MinParameters == 0 && this.MaxParameters == 0)
                return string.Format("Command {0} takes no arguments.", command);
            else if (this.MinParameters == this.MaxParameters)
                return string.Format("Command {0} takes {1} arguments.", command,
                    this.MinParameters);
            else
                return string.Format("Command {0} takes {1} to {2} arguments.", command,
                    this.MinParameters, this.MaxParameters);
        }
    }
}
