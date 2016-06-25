using System;
using System.Linq;
using System.Reflection;

namespace IrcDotNet
{
    public static class ProgramInfo
    {
        public static string AssemblyTitle
        {
            get
            {
#if NETSTANDARD1_5
                return ((AssemblyTitleAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyTitleAttribute)).First()).Title;
#else
                return ((AssemblyTitleAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyTitleAttribute), false)[0]).Title;
#endif
            }
        }

        public static string AssemblyCopyright
        {
            get
            {
#if NETSTANDARD1_5
                return ((AssemblyCopyrightAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyCopyrightAttribute)).First()).Copyright;
#else
                return ((AssemblyCopyrightAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
#endif
            }
        }

        public static Version AssemblyVersion
        {
            get
            {
                return Assembly.GetEntryAssembly().GetName().Version;
            }
        }
    }
}
