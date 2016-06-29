using System;
using System.Reflection;
using NUnit.Common;
using NUnitLite;

namespace IrcDotNet.Test
{
    public static class Runner
    {
        public static int Main(string[] args)
        {
#if NETCOREAPP
            return new AutoRun(typeof(Runner).GetTypeInfo().Assembly).Execute(args, new ExtendedTextWrapper(Console.Out), Console.In);
#else
            return new AutoRun().Execute(args);
#endif
        }
    }
}
