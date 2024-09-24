using System.Diagnostics;

namespace MapMyMess;

internal class SolutionGraph
{
    public readonly Dictionary<string, Node> nodes = [];

    private Node AddNode(string node_name)
    {
        if (nodes.TryGetValue(node_name, out Node? value))
            return value;

        Node node = new(node_name);
        nodes.Add(node_name, node);
        return node;
    }

    public void AddEdge(string from, string to)
    {
        Node node_from = AddNode(from);
        Node node_to = AddNode(to);
        node_from.AddTo(node_to);
        node_to.AddFrom(node_from);
    }

    internal static bool ParentIsAncestor(string name, Node node)
    {
        IEnumerable<Node> grand_parents = node.From.SelectMany(x => x.From);
        if (grand_parents.Any(x => AncestorExists(name, x)))
            return true;

        return false;
    }
    internal static bool AncestorExists(string name, Node ancestor)
    {
        if (ancestor.Name == name)
            return true;

        if (ancestor.From.Any(x => AncestorExists(name, x)))
            return true;

        return false;
    }

    internal static void TransitiveReduction(SolutionGraph graph)
    {
        foreach (var node in graph.nodes.Values)
        {
            foreach (var parent in node.From.ToArray())
            {
                if (ParentIsAncestor(parent.Name, node))
                {
                    parent.To.Remove(node);
                    node.From.Remove(parent);
                    continue;
                }
            }
        }
    }

    [DebuggerDisplay("{Name}")]
    internal class Node
    {
        public readonly List<Node> From = [];
        public readonly List<Node> To = [];

        public Node(string node)
        {
            Name = node;
        }

        public string Name { get; }


        public void AddFrom(Node node)
        {
            From.Add(node);
        }
        public void AddTo(Node node)
        {
            To.Add(node);
        }
    }
}
