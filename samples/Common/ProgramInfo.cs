using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace IrcDotNet.Samples.Common
{
    public static class ProgramInfo
    {
        public static string AssemblyTitle
        {
            get
            {
                return ((AssemblyTitleAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyTitleAttribute), false)[0]).Title;
            }
        }

        public static string AssemblyCopyright
        {
            get
            {
                return ((AssemblyCopyrightAttribute)Assembly.GetEntryAssembly().GetCustomAttributes(
                    typeof(AssemblyCopyrightAttribute), false)[0]).Copyright;
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
