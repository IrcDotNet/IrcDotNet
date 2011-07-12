using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace IrcDotNet.Tests
{

    public class OrderedTest
    {

        public static OrderedTest Load(string path)
        {
            using (var stream = File.OpenRead(path))
                return Load(stream);
        }

        public static OrderedTest Load(Stream stream)
        {
            var doc = XDocument.Load(stream);
            return new OrderedTest(doc);
        }

        private OrderedTest(XDocument document)
            : this()
        {
            Load(document);
        }

        private OrderedTest()
        {
            this.ChildTests = new List<OrderedTestChildTest>();
        }

        public string Name
        {
            get;
            set;
        }

        public string StoragePath
        {
            get;
            set;
        }

        public Guid Id
        {
            get;
            set;
        }

        public OrderedTestExecution Execution
        {
            get;
            set;
        }

        public IList<OrderedTestChildTest> ChildTests
        {
            get;
            private set;
        }

        public void Run()
        {
            foreach (var test in this.ChildTests)
            {
                // TODO: Run each test.
            }
        }

        private void Load(XDocument document)
        {
            var rootNs = (XNamespace)"http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

            var orderedTestElement = document.Element(rootNs + "OrderedTest");
            this.Name = orderedTestElement.Attribute("name").Value;
            this.StoragePath = orderedTestElement.Attribute("storage").Value;
            this.Id = new Guid(orderedTestElement.Attribute("id").Value);
            
            var executionElement = orderedTestElement.Element(rootNs + "Execution");
            this.Execution = new OrderedTestExecution()
                {
                    Id = new Guid(executionElement.Attribute("id").Value),
                };

            var testLinksElement = orderedTestElement.Element(rootNs + "TestLinks");
            foreach (var curTestLinkElement in testLinksElement.Elements(rootNs + "TestLink"))
            {
                this.ChildTests.Add(new OrderedTestChildTest()
                    {
                        Id = new Guid(curTestLinkElement.Attribute("id").Value),
                        Name = curTestLinkElement.Attribute("name").Value,
                        StoragePath = curTestLinkElement.Attribute("storage").Value,
                        TypeName = curTestLinkElement.Attribute("type").Value,
                    });
            }
        }

    }

    public struct OrderedTestExecution
    {

        public Guid Id;

    }

    public struct OrderedTestChildTest
    {

        public Guid Id;

        public string Name;

        public string StoragePath;

        public string TypeName;

    }

}
