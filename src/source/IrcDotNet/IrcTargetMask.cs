using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IrcDotNet
{
    /// <summary>
    /// Represents a mask of an IRC server name or host name, used for specifying the targets of a message.
    /// </summary>
    /// <threadsafety static="true" instance="false"/>
    public class IrcTargetMask : IIrcMessageTarget
    {
        private IrcTargetMaskType type;

        private string mask;

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcTargetMask"/> class with the specified target mask
        /// identifier.
        /// </summary>
        /// <param name="targetMask">A wildcard expression for matching against server names or host names.
        /// If the first character is '$', the mask is a server mask; if the first character is '#', the mask is a host
        /// mask.</param>
        /// <exception cref="ArgumentNullException"><paramref name="targetMask"/> is <see langword="null"/></exception>
        /// <exception cref="ArgumentException">The length of <paramref name="targetMask"/> is too short.</exception>
        /// <exception cref="ArgumentException"><paramref name="targetMask"/> does not represent a known mask type.
        /// </exception>
        public IrcTargetMask(string targetMask)
        {
            if (targetMask == null)
                throw new ArgumentNullException("targetMask");
            if (Properties.Resources.MessageTargetMaskTooShort.Length < 2)
                throw new ArgumentException(Properties.Resources.MessageTargetMaskTooShort, "targetMask");

            if (targetMask[0] == '$')
                this.type = IrcTargetMaskType.ServerMask;
            else if (targetMask[0] == '#')
                this.type = IrcTargetMaskType.HostMask;
            else
                throw new ArgumentException(string.Format(
                    Properties.Resources.MessageTargetMaskInvalidType, targetMask), "targetMask");
            this.mask = mask.Substring(1);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="IrcTargetMask"/> class with the specified type and mask.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="mask">The mask.</param>
        public IrcTargetMask(IrcTargetMaskType type, string mask)
        {
            if (!Enum.IsDefined(typeof(IrcTargetMaskType), type))
                throw new ArgumentException("type");
            if (mask == null)
                throw new ArgumentNullException("mask");

            this.type = type;
            this.mask = mask;
        }

        /// <summary>
        /// Gets the type of the target mask; either a server mask or channel mask.
        /// </summary>
        /// <value>The type of the mask.</value>
        public IrcTargetMaskType Type
        {
            get { return this.type; }
        }

        /// <summary>
        /// Gets a wildcard expression for matching against target names.
        /// The <see cref="Type"/> property determines the type of the mask.
        /// </summary>
        /// <value>The target mask.</value>
        public string Mask
        {
            get { return this.mask; }
        }

        /// <summary>
        /// Returns a string representation of this instance.
        /// </summary>
        /// <returns>A string that represents this instance.</returns>
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

    /// <summary>
    /// Defines the types of a target mask.
    /// </summary>
    public enum IrcTargetMaskType
    {
        /// <summary>
        /// A mask of a server name.
        /// </summary>
        ServerMask,

        /// <summary>
        /// A mask of a host name.
        /// </summary>
        HostMask,
    }
}
