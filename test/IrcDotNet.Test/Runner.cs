using NUnitLite;

namespace IrcDotNet.Test
{
    public static class Runner
    {
        public static int Main(string[] args)
        {
            return new AutoRun().Execute(args);
        }
    }
}
