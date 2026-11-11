using System;
using System.Linq;
using NUnit.Framework;

namespace GameFrameX.NodeComposer.Video.Tests
{
    public class VideoNodeExtensionTests
    {
        static IVideoNodeExtension CreateExtension()
        {
            var type = typeof(IVideoNodeExtension).Assembly.GetType("GameFrameX.NodeComposer.Video.VideoNodeExtension");
            Assert.That(type, Is.Not.Null, "缺少视频节点扩展实现。");
            return (IVideoNodeExtension)Activator.CreateInstance(type);
        }

        static NodeDefinitionRegistry Registry()
        {
            var registry = NodeDefinitionRegistry.CreateDefault();
            CreateExtension().Register(registry);
            return registry;
        }

        static GraphDocument Cycle()
        {
            var graph = new GraphDocument();
            foreach (var id in new[] { "a", "b", "c" })
            {
                var node = new NodeDocument { Id = id, TypeId = VideoNodeSchema.TypeId, Title = "视频 " + id };
                node.Properties[VideoNodeSchema.SourceProperty] = id + ".mp4";
                node.Properties[VideoNodeSchema.VolumeProperty] = 0.5d;
                graph.Nodes.Add(node);
            }
            graph.Edges.Add(new EdgeDocument { SourceNodeId = "a", SourcePortId = VideoNodeSchema.CompletedPort, TargetNodeId = "b", TargetPortId = VideoNodeSchema.EnterPort });
            graph.Edges.Add(new EdgeDocument { SourceNodeId = "b", SourcePortId = VideoNodeSchema.CompletedPort, TargetNodeId = "a", TargetPortId = VideoNodeSchema.EnterPort });
            return graph;
        }

        [Test]
        public void RegistersControlPortsAndEditableProperties()
        {
            INodeDefinition definition;
            Assert.That(Registry().TryGet(VideoNodeSchema.TypeId, out definition), Is.True);
            Assert.That(definition.Ports.Count, Is.EqualTo(3));
            Assert.That(definition.Ports.All(p => p.Kind == PortKind.Control && p.DataTypeId == null), Is.True);
            Assert.That(definition.Ports.Single(p => p.Id == VideoNodeSchema.EnterPort).MaxConnections, Is.EqualTo(-1));
            Assert.That(definition.Ports.Where(p => p.Direction == PortDirection.Output).All(p => p.MaxConnections == 1), Is.True);
            Assert.That(definition.Properties.Single(p => p.Key == VideoNodeSchema.SourceProperty).Required, Is.True);
            Assert.That(definition.Properties.Single(p => p.Key == VideoNodeSchema.VolumeProperty).DefaultValue, Is.EqualTo(1d));
        }

        [Test]
        public void VideoCycleRoundTripsWithFailureRoute()
        {
            var graph = Cycle();
            graph.Edges.Add(new EdgeDocument { SourceNodeId = "a", SourcePortId = VideoNodeSchema.FailedPort, TargetNodeId = "c", TargetPortId = VideoNodeSchema.EnterPort });
            var codec = new GraphJsonCodec(Registry());
            var encoded = codec.Encode(graph);
            Assert.That(encoded.Success, Is.True, encoded.Message);
            var decoded = codec.Decode(encoded.Value);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Value.Edges.Count, Is.EqualTo(3));
            Assert.That(decoded.Value.Nodes[0].Properties[VideoNodeSchema.SourceProperty], Is.EqualTo("a.mp4"));
            Assert.That(decoded.Value.Nodes[0].Properties[VideoNodeSchema.VolumeProperty], Is.EqualTo(0.5d));
            Assert.That(codec.Encode(decoded.Value).Value, Is.EqualTo(encoded.Value));
        }

        [Test]
        public void RejectsMultipleCompletedTargetsAndMissingSource()
        {
            var graph = Cycle();
            graph.Edges.Add(new EdgeDocument { SourceNodeId = "a", SourcePortId = VideoNodeSchema.CompletedPort, TargetNodeId = "c", TargetPortId = VideoNodeSchema.EnterPort, Order = 1 });
            graph.Nodes[0].Properties.Remove(VideoNodeSchema.SourceProperty);
            var issues = new GraphValidator(Registry()).Validate(graph);
            Assert.That(issues.Any(i => i.Code == "PortCapacity"), Is.True);
            Assert.That(issues.Any(i => i.Code == "RequiredProperty"), Is.True);
        }

        [Test]
        public void EmptyAddressCanBeSavedWhileEditingAndDuplicateRegistrationFails()
        {
            var registry = Registry();
            var graph = Cycle();
            graph.Nodes[0].Properties[VideoNodeSchema.SourceProperty] = "";
            Assert.That(new GraphJsonCodec(registry).Encode(graph).Success, Is.True);
            Assert.Throws<ArgumentException>(() => CreateExtension().Register(registry));
        }
    }
}
