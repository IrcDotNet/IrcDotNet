using System.Collections.Generic;

namespace IrcDotNet
{
    public static class IrcBotUtilities
    {
        public static void SendMessage(this IrcLocalUser localUser, IIrcMessageTarget target, string format,
            params object[] args)
        {
            SendMessage(localUser, new[] { target }, format, args);
        }

        public static void SendMessage(this IrcLocalUser localUser, IList<IIrcMessageTarget> targets, string format,
            params object[] args)
        {
            localUser.SendMessage(targets, string.Format(format, args));
        }

        public static void SendNotice(this IrcLocalUser localUser, IIrcMessageTarget target, string format,
            params object[] args)
        {
            SendNotice(localUser, new[] { target }, format, args);
        }

        public static void SendNotice(this IrcLocalUser localUser, IList<IIrcMessageTarget> targets, string format,
            params object[] args)
        {
            localUser.SendNotice(targets, string.Format(format, args));
        }
    }
}
