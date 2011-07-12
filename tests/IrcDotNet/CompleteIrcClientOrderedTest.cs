using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Silverlight.Testing;

namespace IrcDotNet.Tests
{

    [TestClass()]
    [Tag("Integration")]
    public class CompleteIrcClientOrderedTest
    {

        private OrderedTest orderedTest;

        public CompleteIrcClientOrderedTest()
            : base()
        {
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                typeof(CompleteIrcClientOrderedTest), "CompleteIrcClient.orderedtest"))
            {
                this.orderedTest = OrderedTest.Load(stream);
            }
        }

        public TestContext TestContext
        {
            get;
            set;
        }

        [TestInitialize()]
        public void TestInitialize()
        {
        }

        [TestCleanup()]
        public void TestCleanup()
        {
        }

        [TestMethod()]
        public void Test()
        {
            this.orderedTest.Run();
        }

    }

}
