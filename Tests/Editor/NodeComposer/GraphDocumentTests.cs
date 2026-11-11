using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GameFrameX.NodeComposer.Tests
{
    public class GraphDocumentTests
    {
        public static GraphDocument Cycle()
        {
            var graph = new GraphDocument { Id = "graph", Name = "环路" };
            graph.Nodes.Add(new NodeDocument { Id = "a", TypeId = "common.text", Title = "A", X = 12, Y = 34 });
            graph.Nodes.Add(new NodeDocument { Id = "b", TypeId = "common.text", Title = "B" });
            graph.Edges.Add(new EdgeDocument { Id = "ab", SourceNodeId = "a", SourcePortId = "valueOut", TargetNodeId = "b", TargetPortId = "valueIn" });
            graph.Edges.Add(new EdgeDocument { Id = "ba", SourceNodeId = "b", SourcePortId = "next", TargetNodeId = "a", TargetPortId = "enter" });
            return graph;
        }

        [Test]
        public void RoundTripPreservesDataControlPortsAndCycle()
        {
            var codec = new GraphJsonCodec(NodeDefinitionRegistry.CreateDefault());
            var graph = Cycle();
            graph.Nodes[0].Properties["value"] = "你好\nWorld";
            var encoded = codec.Encode(graph);
            Assert.That(encoded.Success, Is.True, encoded.Message);
            var decoded = codec.Decode(encoded.Value);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Value.Nodes.Single(n => n.Id == "a").Properties["value"], Is.EqualTo("你好\nWorld"));
            Assert.That(decoded.Value.Nodes.Single(n => n.Id == "a").X, Is.EqualTo(12));
            Assert.That(decoded.Value.Edges.Single(e => e.Id == "ba").SourcePortId, Is.EqualTo("next"));
            Assert.That(codec.Encode(decoded.Value).Value, Is.EqualTo(encoded.Value));
        }

        [Test]
        public void RejectsWrongPortTypeDirectionAndKind()
        {
            var graph = Cycle();
            graph.Nodes[1].TypeId = "common.number";
            AssertIssue(graph, "PortType");
            graph.Nodes[1].TypeId = "common.text";
            graph.Edges[0].TargetPortId = "valueOut";
            AssertIssue(graph, "PortDirection");
            graph.Edges[0].TargetPortId = "enter";
            AssertIssue(graph, "PortKind");
        }

        [Test]
        public void AllowsDifferentPortPairsBetweenSameNodesButRejectsDuplicatesAndCapacity()
        {
            var graph = Cycle();
            graph.Edges.Add(new EdgeDocument { Id = "control", SourceNodeId = "a", SourcePortId = "next", TargetNodeId = "b", TargetPortId = "enter" });
            Assert.That(new GraphValidator(NodeDefinitionRegistry.CreateDefault()).Validate(graph), Is.Empty);
            graph.Edges.Add(new EdgeDocument { Id = "duplicate", SourceNodeId = "a", SourcePortId = "valueOut", TargetNodeId = "b", TargetPortId = "valueIn", Order = 1 });
            AssertIssue(graph, "DuplicateEdge");
            AssertIssue(graph, "PortCapacity");
        }

        [Test]
        public void RejectsUnknownPortsAndSelfLoops()
        {
            var graph = Cycle();
            graph.Edges[0].SourcePortId = "missing";
            AssertIssue(graph, "UnknownPort");
            graph.Edges[0].SourcePortId = "valueOut";
            graph.Edges[0].TargetNodeId = "a";
            AssertIssue(graph, "SelfLoop");
        }

        [Test]
        public void RejectsMalformedJsonUnknownFieldsAndUnsupportedVersion()
        {
            var codec = new GraphJsonCodec(NodeDefinitionRegistry.CreateDefault());
            foreach (var json in new[] { "", "{", "[]", "{unquoted:1}", "{\"schemaVersion\":1,}", "{} trailing", "{\"a\":1,\"a\":2}" })
                Assert.That(codec.Decode(json).Success, Is.False, json);
            var valid = codec.Encode(Cycle()).Value;
            Assert.That(codec.Decode(Regex.Replace(valid, "\"schemaVersion\"\\s*:\\s*1", "\"schemaVersion\": 9")).Success, Is.False);
            Assert.That(codec.Decode(Regex.Replace(valid, "\"name\"\\s*:", "\"extra\": 1, \"name\":")).Success, Is.False);
        }

        [Test]
        public void RejectsInvalidPropertiesAndCoordinates()
        {
            var graph = Cycle();
            graph.Nodes[0].Properties["value"] = 3;
            AssertIssue(graph, "PropertyType");
            graph.Nodes[0].Properties.Clear();
            graph.Nodes[0].X = float.NaN;
            AssertIssue(graph, "Position");
        }

        [Test]
        public void EmptyGraphIsValidAndRegistryRejectsDuplicates()
        {
            var registry = NodeDefinitionRegistry.CreateDefault();
            var codec = new GraphJsonCodec(registry);
            Assert.That(codec.Decode(codec.Encode(new GraphDocument { Id = "empty", Name = "Empty" }).Value).Success, Is.True);
            INodeDefinition definition;
            registry.TryGet("common.text", out definition);
            Assert.Throws<ArgumentException>(() => registry.Register(definition));
        }

        static void AssertIssue(GraphDocument graph, string code)
        {
            Assert.That(new GraphValidator(NodeDefinitionRegistry.CreateDefault()).Validate(graph).Any(i => i.Code == code), Is.True, code);
        }
    }
}
