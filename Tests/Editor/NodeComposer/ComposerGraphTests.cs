using System;
using System.IO;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Tests
{
    public class ComposerGraphTests
    {
        ComposerGraph graph;
        readonly ComposerGraphAdapter adapter = new ComposerGraphAdapter();
        [TearDown] public void Cleanup() { if (graph != null) UnityEngine.Object.DestroyImmediate(graph); }

        ComposerGraph CreateGraph()
        {
            var created = adapter.Create(GraphDocumentTests.Cycle());
            Assert.That(created.Success, Is.True, created.Message);
            return created.Value;
        }

        [Test]
        public void ValueNodesPreserveTypedConnectionsAndStableWriteTargetInNativeJson()
        {
            graph=ScriptableObject.CreateInstance<ComposerGraph>();
            var cell=graph.AddComposerNode("common.number",Vector2.zero);
            var operand=graph.AddComposerNode("common.number",Vector2.one*50);
            var modify=graph.AddComposerNode("flow.modify.float32",Vector2.one*100);
            var condition=graph.AddComposerNode("flow.condition.float32",Vector2.one*150);
            Assert.That(modify.UpdateProperties(new System.Collections.Generic.Dictionary<string,object>{{"target",cell.DocumentId},{"operation","subtract"}}).Success,Is.True);
            Assert.That(graph.ConnectPorts(operand,"valueOut",modify,"operand").Success,Is.True);
            Assert.That(graph.ConnectPorts(modify,"next",condition,"enter").Success,Is.True);
            Assert.That(graph.ConnectPorts(cell,"valueOut",condition,"left").Success,Is.True);
            Assert.That(graph.ConnectPorts(operand,"valueOut",condition,"right").Success,Is.True);
            var targetId=cell.DocumentId;cell.name="重命名数据";
            var json=graph.Serialize(null);Assert.That(graph.Deserialize(json,null,true),Is.True);
            var captured=adapter.Capture(graph);Assert.That(captured.Success,Is.True,captured.Message);
            Assert.That(captured.Value.Edges.Count,Is.EqualTo(4));
            Assert.That(captured.Value.Nodes.Single(n=>n.TypeId=="flow.modify.float32").Properties["target"],Is.EqualTo(targetId));
            Assert.That(captured.Value.Nodes.Single(n=>n.Id==targetId).Title,Is.EqualTo("重命名数据"));
            var protocol=new GraphJsonCodec(ComposerGraph.Registry).Encode(captured.Value);
            Assert.That(protocol.Success,Is.True,protocol.Message);
        }

        [Test]
        public void PluginRoundTripPreservesPortsAndCycle()
        {
            var created = adapter.Create(GraphDocumentTests.Cycle());
            Assert.That(created.Success, Is.True, created.Message);
            graph = created.Value;
            var json = graph.Serialize(null);
            Assert.That(graph.Deserialize(json, null, true), Is.True);
            var captured = adapter.Capture(graph);
            Assert.That(captured.Success, Is.True, captured.Message);
            Assert.That(captured.Value.Id, Is.EqualTo("graph"));
            Assert.That(captured.Value.Edges.Single(e => e.Id == "ba").TargetPortId, Is.EqualTo("enter"));
            Assert.That(captured.Value.Nodes.Single(n => n.Id == "a").X, Is.EqualTo(12));
        }

        [Test]
        public void InvalidImportLeavesGraphUnchanged()
        {
            graph = CreateGraph();
            var before = new GraphJsonCodec(ComposerGraph.Registry).Encode(adapter.Capture(graph).Value).Value;
            var invalid = GraphDocumentTests.Cycle(); invalid.Edges[0].TargetPortId = "missing";
            Assert.That(adapter.Replace(graph, invalid).Success, Is.False);
            var after = new GraphJsonCodec(ComposerGraph.Registry).Encode(adapter.Capture(graph).Value).Value;
            Assert.That(after, Is.EqualTo(before));
        }

        [Test]
        public void CanUndoSuccessfulReplacement()
        {
            graph = CreateGraph();
            graph.hideFlags = HideFlags.None;
            Undo.ClearAll();
            Assert.That(adapter.Replace(graph, new GraphDocument { Id = "replacement", Name = "Empty" }).Success, Is.True);
            Assert.That(graph.allNodes.Count, Is.EqualTo(0));
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            graph.SelfDeserialize();
            Assert.That(graph.allNodes.Count, Is.EqualTo(2));
            Assert.That(graph.DocumentId, Is.EqualTo("graph"));
            Undo.PerformRedo();
            graph.SelfDeserialize();
            Assert.That(graph.allNodes.Count, Is.EqualTo(0));
            Assert.That(graph.DocumentId, Is.EqualTo("replacement"));
        }

        [Test]
        public void DuplicateNodeReceivesNewDocumentId()
        {
            graph = CreateGraph();
            var original = graph.allNodes.OfType<ComposerNode>().First();
            var duplicate = (ComposerNode)original.Duplicate(graph);
            Assert.That(duplicate.DocumentId, Is.Not.EqualTo(original.DocumentId));
            Assert.That(adapter.Capture(graph).Success, Is.True);
        }

        [Test]
        public void AddingAndConnectingNodesValidatesBeforeChangingGraph()
        {
            graph = ScriptableObject.CreateInstance<ComposerGraph>();
            var first = graph.AddComposerNode("common.text", Vector2.zero);
            var second = graph.AddComposerNode("common.text", Vector2.right * 400);
            var number = graph.AddComposerNode("common.number", Vector2.up * 200);
            Assert.That(graph.ConnectPorts(first, "valueOut", second, "valueIn").Success, Is.True);
            Assert.That(graph.ConnectPorts(second, "next", first, "enter").Success, Is.True);
            Assert.That(graph.ConnectPorts(first, "next", second, "enter").Success, Is.True);
            Assert.That(graph.ConnectPorts(number, "valueOut", second, "valueIn").Success, Is.False);
            Assert.That(graph.ConnectPorts(first, "next", second, "enter").Success, Is.False);
            Assert.That(adapter.Capture(graph).Value.Edges.Count, Is.EqualTo(3));
        }

        [Test]
        public void SavedAssetReloadPreservesNodePropertiesAndConnections()
        {
            var path = "Assets/NodeComposer-Test-" + Guid.NewGuid().ToString("N") + ".asset";
            try
            {
                graph = CreateGraph();
                var first = graph.allNodes.OfType<ComposerNode>().Single(n => n.DocumentId == "a");
                first.Data.Properties["value"] = "持久化";
                graph.SelfSerialize();
                graph.hideFlags = HideFlags.None;
                AssetDatabase.CreateAsset(graph, path);
                AssetDatabase.SaveAssetIfDirty(graph);
                Resources.UnloadAsset(graph);
                graph = AssetDatabase.LoadAssetAtPath<ComposerGraph>(path);
                var captured = adapter.Capture(graph);
                Assert.That(captured.Success, Is.True, captured.Message);
                Assert.That(captured.Value.Edges.Count, Is.EqualTo(2));
                Assert.That(captured.Value.Nodes.Single(n => n.Id == "a").Properties["value"], Is.EqualTo("持久化"));
            }
            finally { AssetDatabase.DeleteAsset(path); graph = null; }
        }

        [Test]
        public void RejectsDetachedNodeReferencesAfterImport()
        {
            graph = CreateGraph();
            var source = graph.allNodes.OfType<ComposerNode>().Single(n => n.DocumentId == "a");
            var target = graph.allNodes.OfType<ComposerNode>().Single(n => n.DocumentId == "b");
            Assert.That(adapter.Replace(graph, GraphDocumentTests.Cycle()).Success, Is.True);
            source = graph.allNodes.OfType<ComposerNode>().Single(n => n.DocumentId == "a");
            Assert.That(graph.ConnectPorts(source, "next", target, "enter").Success, Is.False);
            Assert.That(graph.allNodes.Count, Is.EqualTo(2));
        }

        [Test]
        public void FileExportRoundTripsAndInvalidExportPreservesExistingFile()
        {
            string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var service = new GraphFileService();
                Assert.That(service.Export(path, GraphDocumentTests.Cycle()).Success, Is.True);
                Assert.That(service.Import(path).Value.Edges.Count, Is.EqualTo(2));
                var before = File.ReadAllText(path);
                var invalid = GraphDocumentTests.Cycle(); invalid.Nodes[0].TypeId = "unknown";
                Assert.That(service.Export(path, invalid).Success, Is.False);
                Assert.That(File.ReadAllText(path), Is.EqualTo(before));
                Assert.That(service.Export(path, GraphDocumentTests.Cycle()).Success, Is.True);
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
