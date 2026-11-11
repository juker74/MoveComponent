using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFrameX.NodeComposer
{
    [Serializable]
    public sealed class GraphDocument
    {
        public int SchemaVersion = 1;
        public string Id = Guid.NewGuid().ToString("N");
        public string Name = "Graph";
        public List<NodeDocument> Nodes = new List<NodeDocument>();
        public List<EdgeDocument> Edges = new List<EdgeDocument>();
    }

    [Serializable]
    public sealed class NodeDocument
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string TypeId;
        public string Title = "";
        public float X;
        public float Y;
        public Dictionary<string, object> Properties = new Dictionary<string, object>();
    }

    [Serializable]
    public sealed class EdgeDocument
    {
        public string Id = Guid.NewGuid().ToString("N");
        public string SourceNodeId;
        public string SourcePortId;
        public string TargetNodeId;
        public string TargetPortId;
        public int Order;
    }

    public sealed class GraphIssue
    {
        public string Code { get; private set; }
        public string Message { get; private set; }
        public string NodeId { get; private set; }
        public string EdgeId { get; private set; }
        public string PropertyPath { get; private set; }
        public GraphIssue(string code, string message, string nodeId = null, string edgeId = null, string propertyPath = null)
        { Code = code; Message = message; NodeId = nodeId; EdgeId = edgeId; PropertyPath = propertyPath; }
        public override string ToString() { return Code + ": " + Message; }
    }

    public sealed class GraphResult<T>
    {
        public bool Success { get { return Issues.Count == 0; } }
        public T Value { get; private set; }
        public IReadOnlyList<GraphIssue> Issues { get; private set; }
        public string Message { get { return string.Join("\n", Issues.Select(i => i.ToString()).ToArray()); } }
        public static GraphResult<T> Ok(T value) { return new GraphResult<T> { Value = value, Issues = new GraphIssue[0] }; }
        public static GraphResult<T> Fail(IEnumerable<GraphIssue> issues) { return new GraphResult<T> { Issues = issues.ToArray() }; }
        public static GraphResult<T> Fail(string code, string message) { return Fail(new[] { new GraphIssue(code, message) }); }
    }

    public interface IGraphJsonCodec
    {
        GraphResult<GraphDocument> Decode(string json);
        GraphResult<string> Encode(GraphDocument document);
    }
}
