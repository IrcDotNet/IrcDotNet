using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcDotNet.Tests
{
    public abstract class InterdependentTestSet<TState> where TState : struct
    {
        private static Dictionary<string, TestDependencyAttribute> testMethodsDependencies;

        public static TState CurrentState
        {
            get;
            private set;
        }

        public static void OnClassInitialize(TestContext testContext, TState initialState)
        {
            testMethodsDependencies = new Dictionary<string, TestDependencyAttribute>();
            CurrentState = initialState;

            // Find all methods in test class that are marked by single instance of TestDependencyAttribute.
            // Add each pair of test name & test dependencies to dictionary.
            var testClassType = Type.GetType(testContext.FullyQualifiedTestClassName);
            var testMethods = testClassType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            foreach (var methodInfo in testMethods)
            {
                var testDependencyAttribute = ((TestDependencyAttribute[])methodInfo.GetCustomAttributes(
                    typeof(TestDependencyAttribute), true)).SingleOrDefault();
                if (testDependencyAttribute == null)
                    continue;
                testMethodsDependencies.Add(methodInfo.Name, testDependencyAttribute);
            }
        }

        public static void OnClassCleanup()
        {
        }

        public InterdependentTestSet()
        {
        }

        public TestContext TestContext
        {
            get;
            set;
        }

        public virtual void TestInitialize()
        {
            TestDependencyAttribute testDependencyAttribute;
            if (testMethodsDependencies.TryGetValue(this.TestContext.TestName, out testDependencyAttribute))
                CheckTestState((TState)testDependencyAttribute.RequiredState);
        }

        public virtual void TestCleanup()
        {
            TestDependencyAttribute testDependencyAttribute;
            if (testMethodsDependencies.TryGetValue(this.TestContext.TestName, out testDependencyAttribute))
            {
                switch (this.TestContext.CurrentTestOutcome)
                {
                    case UnitTestOutcome.Passed:
                        CurrentState = GetNewTestState((TState)(testDependencyAttribute.SetState ?? default(TState)),
                            (TState)(testDependencyAttribute.UnsetState ?? default(TState)));
                        break;
                    default:
                        break;
                }
            }
        }

        protected abstract void CheckTestState(TState requiredState);

        protected abstract TState GetNewTestState(TState setState, TState unsetState);
    }

    public class TestDependencyAttribute : Attribute
    {
        public TestDependencyAttribute(object requiredState)
        {
            this.RequiredState = requiredState;
            this.SetState = null;
            this.UnsetState = null;
        }

        public object RequiredState
        {
            get;
            private set;
        }

        public object SetState
        {
            get;
            set;
        }

        public object UnsetState
        {
            get;
            set;
        }
    }
}
