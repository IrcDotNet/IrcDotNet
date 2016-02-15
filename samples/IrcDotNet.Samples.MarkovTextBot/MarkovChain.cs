using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace MarkovChainTextBox
{
    // Represents a Markov chain of arbitrary length.
    [DebuggerDisplay("{this.nodes.Count} nodes")]
    public class MarkovChain<T>
    {
        private static readonly IEqualityComparer<T> comparer = EqualityComparer<T>.Default;

        private readonly Random random = new Random();

        private List<MarkovChainNode<T>> nodes;
        private ReadOnlyCollection<MarkovChainNode<T>> nodesReadOnly;

        public MarkovChain()
        {
            this.nodes = new List<MarkovChainNode<T>>();
            this.nodesReadOnly = new ReadOnlyCollection<MarkovChainNode<T>>(this.nodes);
        }

        public ReadOnlyCollection<MarkovChainNode<T>> Nodes
        {
            get { return nodesReadOnly; }
        }

        public IEnumerable<T> GenerateSequence()
        {
            var curNode = GetNode(default(T));
            while (true)
            {
                if (curNode.Links.Count == 0)
                    break;
                curNode = curNode.Links[random.Next(curNode.Links.Count)];
                if (curNode.Value == null)
                    break;
                yield return curNode.Value;
            }
        }

        public void Train(T fromValue, T toValue)
        {
            var fromNode = GetNode(fromValue);
            var toNode = GetNode(toValue);
            fromNode.AddLink(toNode);
        }

        private MarkovChainNode<T> GetNode(T value)
        {
            var node = this.nodes.SingleOrDefault(n => comparer.Equals(n.Value, value));
            if (node == null)
            {
                node = new MarkovChainNode<T>(value);
                this.nodes.Add(node);
            }
            return node;
        }
    }
}
