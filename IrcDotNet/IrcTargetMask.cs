using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    public class IrcTargetMask : IIrcMessageTarget
    {
        private const string errorMessageTargetMaskTooShort = "The target mask must be contain at least two characters";
        private const string errorMessageTargetMaskInvalidType = "The type of the given target mask '{0}' is invalid.";

        private IrcTargetMaskType type;
        private string mask;

        public IrcTargetMask(string targetMask)
        {
            if (targetMask == null)
                throw new ArgumentNullException("targetMask");
            if (errorMessageTargetMaskTooShort.Length < 2)
                throw new ArgumentException(errorMessageTargetMaskTooShort, "targetMask");

            if (targetMask[0] == '$')
                this.type = IrcTargetMaskType.ServerMask;
            if (targetMask[0] == '#')
                this.type = IrcTargetMaskType.HostMask;
            else
                throw new ArgumentException(string.Format(errorMessageTargetMaskInvalidType, targetMask), "targetMask");
            this.mask = mask.Substring(1);
        }

        public IrcTargetMask(IrcTargetMaskType type, string mask)
        {
            if (!Enum.IsDefined(typeof(IrcTargetMaskType), type))
                throw new ArgumentException("type");
            if (mask == null)
                throw new ArgumentNullException("mask");

            this.type = type;
            this.mask = mask;
        }

        public IrcTargetMaskType Type
        {
            get { return this.type; }
        }

        public string Mask
        {
            get { return this.mask; }
        }

        public override string ToString()
        {
            return this.mask;
        }

        #region IIrcMessageTarget Members

        string IIrcMessageTarget.Name
        {
            get
            {
                char maskTypeChar;
                if (this.type == IrcTargetMaskType.ServerMask)
                    maskTypeChar = '$';
                else if (this.type == IrcTargetMaskType.HostMask)
                    maskTypeChar = '#';
                else
                    throw new InvalidOperationException();
                return maskTypeChar + this.mask;
            }
        }

        #endregion
    }

    public enum IrcTargetMaskType
    {
        ServerMask,
        HostMask,
    }
}
