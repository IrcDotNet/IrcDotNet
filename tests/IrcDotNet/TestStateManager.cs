using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace IrcDotNet.Tests
{
    using Collections;

    // Manages states and dependencies of set of tests.
    public class TestStateManager<TState> where TState : struct
    {
        private HashSet<TState> currentStates;
        private ReadOnlySet<TState> currentsStateReadOnly;

        public TestStateManager()
        {
            this.currentStates = new HashSet<TState>();
            this.currentsStateReadOnly = new ReadOnlySet<TState>(currentStates);
        }

        public ReadOnlySet<TState> CurrentStates
        {
            get { return currentsStateReadOnly; }
        }

        public void HasStates(params TState[] requiredStates)
        {
            HasStates((IEnumerable<TState>)requiredStates);
        }

        public void HasStates(IEnumerable<TState> states)
        {
            Assert.IsTrue(this.currentStates.Intersect(states).Any(), string.Format(
                "Current test is not in one or more required states for execution." + Environment.NewLine +
                "Required states: {0}" + Environment.NewLine +
                "Current states: {1}",
                string.Join(", ", states), string.Join(", ", this.currentStates)));
        }

        public void HasNotStates(params TState[] states)
        {
            HasNotStates((IEnumerable<TState>)states);
        }

        public void HasNotStates(IEnumerable<TState> states)
        {
            Assert.IsFalse(this.currentStates.Intersect(states).Any(), string.Format(
                "Current test is in one or more forbidden states for execution." + Environment.NewLine +
                "Forbidden states: {0}" + Environment.NewLine +
                "Current states: {1}",
                string.Join(",", states), string.Join(", ", this.currentStates)));
        }

        public void SetStates(params TState[] states)
        {
            SetStates((IEnumerable<TState>)states);
        }

        public void SetStates(IEnumerable<TState> states)
        {
            this.currentStates.AddRange(states);
        }

        public void UnsetStates(params TState[] states)
        {
            UnsetStates((IEnumerable<TState>)states);
        }

        public void UnsetStates(IEnumerable<TState> states)
        {
            this.currentStates.RemoveRange(states);
        }
    }
}
