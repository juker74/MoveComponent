using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace GameFrameX.NodeComposer.Video.Tests
{
    public class VideoFrameAndUiTests
    {
        static NodeDefinitionRegistry Registry()
        {
            var registry = NodeDefinitionRegistry.CreateDefault();
            new VideoNodeExtension().Register(registry);
            return registry;
        }

        static GraphDocument Document()
        {
            var document = new GraphDocument();
            var video = new NodeDocument { Id = "video", TypeId = "media.video" };
            video.Properties["source"] = "Assets/Videos/demo.mp4";
            video.Properties["frames"] = new List<object> { 120d, 360d };
            video.Properties["assetGuid"] = "test-guid";
            var ui = new NodeDocument { Id = "ui", TypeId = "media.ui" };
            ui.Properties["uiType"] = "Game.UI.ChoiceUI, Assembly-CSharp";
            ui.Properties["parameters"] = new Dictionary<string, object> {
                { "options", new List<object> { "开门", "离开" } }, { "count", 2d } };
            ui.Properties["pauseVideo"] = true;
            document.Nodes.Add(video); document.Nodes.Add(ui);
            document.Edges.Add(new EdgeDocument { SourceNodeId = "video", SourcePortId = "frame:120", TargetNodeId = "ui", TargetPortId = "enter" });
            return document;
        }

        [Test]
        public void UiBranchesRoundTripAndGenerateNamedSingleTargetPorts()
        {
            var doc=Document();var ui=doc.Nodes.Single(n=>n.Id=="ui");
            ui.Properties["branches"]=new List<object> {
                new Dictionary<string,object>{{"id","a"},{"name","成功"}},
                new Dictionary<string,object>{{"id","b"},{"name","失败"}} };
            var registry=Registry();var codec=new GraphJsonCodec(registry);
            var encoded=codec.Encode(doc);Assert.That(encoded.Success,Is.True,encoded.Message);
            var result=codec.Decode(encoded.Value);Assert.That(result.Success,Is.True,result.Message);
            var ports=registry.GetPorts(result.Value.Nodes.Single(n=>n.Id=="ui"));
            Assert.That(ports.Select(p=>p.Id),Is.EqualTo(new[]{"enter","branch:a","branch:b"}));
            Assert.That(ports[1].DisplayName,Is.EqualTo("成功"));
            Assert.That(ports[1].MaxConnections,Is.EqualTo(1));
        }

        [Test]
        public void RejectsDuplicateOrBlankUiBranchIds()
        {
            foreach(var ids in new[]{new[]{"a","a"},new[]{"","b"}})
            {
                var doc=Document();doc.Nodes.Single(n=>n.Id=="ui").Properties["branches"]=ids.Select(id=>(object)new Dictionary<string,object>{{"id",id},{"name","分支"}}).ToList();
                Assert.That(new GraphJsonCodec(Registry()).Encode(doc).Success,Is.False);
            }
        }

        [Test]
        public void FramePortsAndUiParametersRoundTripWithoutExtraIds()
        {
            var codec = new GraphJsonCodec(Registry());
            var encoded = codec.Encode(Document());
            Assert.That(encoded.Success, Is.True, encoded.Message);
            var decoded = codec.Decode(encoded.Value);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            var ui = decoded.Value.Nodes.Single(n => n.Id == "ui");
            Assert.That(ui.Properties["pauseVideo"], Is.EqualTo(true));
            var parameters = (Dictionary<string, object>)ui.Properties["parameters"];
            Assert.That(((List<object>)parameters["options"])[1], Is.EqualTo("离开"));
            Assert.That(decoded.Value.Nodes.Single(n => n.Id == "video").Properties["assetGuid"], Is.EqualTo("test-guid"));
            Assert.That(decoded.Value.Edges[0].SourcePortId, Is.EqualTo("frame:120"));
        }

        [Test]
        public void RejectsDeletedFramePortAndInvalidFrameLists()
        {
            var registry = Registry();
            foreach (var frames in new[] {
                new List<object> { -1d }, new List<object> { 0.5d }, new List<object> { 120d, 120d },
                new List<object> { double.PositiveInfinity }, new List<object> { "120" } })
            {
                var doc = Document(); doc.Nodes[0].Properties["frames"] = frames;
                Assert.That(new GraphValidator(registry).Validate(doc).Any(i => i.Code == "PropertyType"), Is.True);
            }
            var deleted = Document(); deleted.Nodes[0].Properties["frames"] = new List<object>();
            Assert.That(new GraphValidator(registry).Validate(deleted).Any(i => i.Code == "UnknownPort"), Is.True);
        }

        [Test]
        public void OneFrameCanTriggerMultipleUiNodesAndUiDefaultsAreIndependent()
        {
            var registry = Registry();
            var doc = Document();
            doc.Nodes.Add(new NodeDocument { Id = "ui2", TypeId = "media.ui" });
            doc.Edges.Add(new EdgeDocument { SourceNodeId = "video", SourcePortId = "frame:120", TargetNodeId = "ui2", TargetPortId = "enter", Order = 1 });
            Assert.That(new GraphValidator(registry).Validate(doc), Is.Empty);
            INodeDefinition definition;
            Assert.That(registry.TryGet("media.ui", out definition), Is.True);
            Assert.That(definition.Properties.Single(p => p.Key == "pauseVideo").DefaultValue, Is.EqualTo(false));
        }

        [Test]
        public void RejectsNonObjectUiParametersAndPreservesLegacyVideoDocument()
        {
            var registry = Registry();
            var doc = Document(); doc.Nodes[1].Properties["parameters"] = new List<object>();
            Assert.That(new GraphValidator(registry).Validate(doc).Any(i => i.Code == "PropertyType"), Is.True);
            doc = new GraphDocument();
            var video = new NodeDocument { TypeId = "media.video" }; video.Properties["source"] = "demo.mp4"; doc.Nodes.Add(video);
            Assert.That(new GraphJsonCodec(registry).Encode(doc).Success, Is.True);
        }

        [Test]
        public void PropertyEditorJsonUsesStrictParsingAndSupportsIndependentValues()
        {
            Assert.That(GraphJsonCodec.DecodeValue("{\"a\":1,}").Success, Is.False);
            Assert.That(GraphJsonCodec.DecodeValue("{\"a\":1,\"a\":2}").Success, Is.False);
            Assert.That(GraphJsonCodec.DecodeValue("\"hello\"").Value, Is.EqualTo("hello"));
            Assert.That(GraphJsonCodec.DecodeValue("42").Value, Is.EqualTo(42d));
            Assert.That(GraphJsonCodec.DecodeValue("true").Value, Is.EqualTo(true));
            Assert.That(GraphJsonCodec.DecodeValue("null").Success, Is.True);
            var encoded = GraphJsonCodec.EncodeValue(new Dictionary<string, object> { { "text", "你好" } });
            Assert.That(encoded.Success, Is.True, encoded.Message);
            Assert.That(((Dictionary<string, object>)GraphJsonCodec.DecodeValue(encoded.Value).Value)["text"], Is.EqualTo("你好"));
        }
    }
}
