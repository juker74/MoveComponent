using System.Collections.Generic;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using GameFrameX.NodeComposer.Video.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Video.Tests
{
    public abstract class AbstractEditorMovieUi : Game.Framework.UI.MovieUIForm { }
    public sealed class EditorMovieUi : AbstractEditorMovieUi { }
    public class GenericEditorMovieUi<T> : Game.Framework.UI.MovieUIForm { }
    public class VideoEditorTests
    {
        ComposerGraph graph;
        [SetUp] public void Setup() { graph = ScriptableObject.CreateInstance<ComposerGraph>(); }
        [TearDown] public void Cleanup() { Undo.ClearUndo(graph); Object.DestroyImmediate(graph); }

        [Test]
        public void DeletingUiBranchCanUndoAndRedoItsPortAndConnection()
        {
            var ui=graph.AddComposerNode(UiNodeSchema.TypeId,Vector2.zero);
            var target=graph.AddComposerNode(VideoNodeSchema.TypeId,Vector2.right*400);
            Assert.That(ui.UpdateProperties(new Dictionary<string,object>{{"branches",new List<object>{new Dictionary<string,object>{{"id","a"},{"name","继续"}}}}}).Success,Is.True);
            Assert.That(graph.ConnectPorts(ui,"branch:a",target,"enter").Success,Is.True);
            graph.SelfSerialize();Undo.ClearAll();Undo.IncrementCurrentGroup();
            Assert.That(ui.UpdateProperties(new Dictionary<string,object>{{"branches",new List<object>()}}).Success,Is.True);
            Undo.FlushUndoRecordObjects();Undo.IncrementCurrentGroup();Undo.PerformUndo();graph.SelfDeserialize();
            var restored=new ComposerGraphAdapter().Capture(graph);
            Assert.That(restored.Success,Is.True,restored.Message);Assert.That(restored.Value.Edges.Count,Is.EqualTo(1));
            Assert.That(restored.Value.Edges[0].SourcePortId,Is.EqualTo("branch:a"));
            Undo.PerformRedo();graph.SelfDeserialize();
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges,Is.Empty);
        }

        [Test]
        public void UiBranchRenameReorderAndRemovalPreserveOnlySurvivingConnections()
        {
            var ui=graph.AddComposerNode(UiNodeSchema.TypeId,Vector2.zero);
            var a=graph.AddComposerNode(VideoNodeSchema.TypeId,Vector2.right*400);
            var b=graph.AddComposerNode(VideoNodeSchema.TypeId,Vector2.right*800);
            var branches=new List<object>{new Dictionary<string,object>{{"id","a"},{"name","成功"}},new Dictionary<string,object>{{"id","b"},{"name","失败"}}};
            Assert.That(ui.UpdateProperties(new Dictionary<string,object>{{"branches",branches}}).Success,Is.True);
            Assert.That(graph.ConnectPorts(ui,"branch:a",a,"enter").Success,Is.True);
            Assert.That(graph.ConnectPorts(ui,"branch:b",b,"enter").Success,Is.True);
            branches.Reverse();((Dictionary<string,object>)branches[1])["name"]="胜利";
            Assert.That(ui.UpdateProperties(new Dictionary<string,object>{{"branches",branches}}).Success,Is.True);
            Assert.That(ui.Ports.Select(p=>p.Id),Is.EqualTo(new[]{"enter","branch:b","branch:a"}));
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Count,Is.EqualTo(2));
            branches.RemoveAt(0);
            Assert.That(ui.UpdateProperties(new Dictionary<string,object>{{"branches",branches}}).Success,Is.True);
            var captured=new ComposerGraphAdapter().Capture(graph);
            Assert.That(captured.Success,Is.True,captured.Message);
            Assert.That(captured.Value.Edges.Single().SourcePortId,Is.EqualTo("branch:a"));
            Assert.That(captured.Value.Edges.Single().TargetNodeId,Is.EqualTo(a.DocumentId));
            var json=graph.Serialize(null);Assert.That(graph.Deserialize(json,null,true),Is.True);
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Single().SourcePortId,Is.EqualTo("branch:a"));
        }

        [Test]
        public void LegacyNativeGraphCanUpgradeAndSelectUiClassWithoutLosingConnections()
        {
            var video = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
            var ui = graph.AddComposerNode(UiNodeSchema.TypeId, Vector2.right * 400);
            Assert.That(graph.ConnectPorts(video, "completed", ui, "enter").Success, Is.True);
            ui.Data.Properties.Remove("uiType");
            ui.Data.Properties["uiId"] = "old-choice";
            var id = ui.DocumentId;
            UiNodeView.UpgradeLegacyUiNodes(graph);
            Assert.That(ui.Data.Properties.ContainsKey("uiId"), Is.False);
            Assert.That(ui.Data.Properties["uiType"], Is.EqualTo(""));
            Assert.That(ui.UpdateProperties(new Dictionary<string, object> {
                { "uiType", Game.Framework.UI.MovieUIType.GetId(typeof(EditorMovieUi)) }
            }).Success, Is.True);
            var captured = new ComposerGraphAdapter().Capture(graph);
            Assert.That(captured.Success, Is.True, captured.Message);
            Assert.That(captured.Value.Edges.Single().TargetNodeId, Is.EqualTo(id));
        }

        [Test]
        public void UiClassListExcludesEditorOnlyAbstractAndGenericTypes()
        {
            var types = MovieUiTypeCatalog.Types;
            Assert.That(TypeCache.GetTypesDerivedFrom<Game.Framework.UI.MovieUIForm>(), Has.Member(typeof(EditorMovieUi)));
            Assert.That(Game.Framework.UI.MovieUIType.IsSelectable(typeof(EditorMovieUi)), Is.True);
            Assert.That(types, Has.No.Member(typeof(EditorMovieUi)));
            Assert.That(types, Has.No.Member(typeof(AbstractEditorMovieUi)));
            Assert.That(types, Has.No.Member(typeof(GenericEditorMovieUi<>)));
            Assert.That(types, Has.No.Member(typeof(Game.Framework.UI.MovieUIForm)));
            var id = Game.Framework.UI.MovieUIType.GetId(typeof(EditorMovieUi));
            Assert.That(Game.Framework.UI.MovieUIType.Resolve(id), Is.EqualTo(typeof(EditorMovieUi)));
        }

        [Test]
        public void RegistersNodeViewsAndPreservesMultipleFrameConnectionsInNativeJson()
        {
            Assert.That(ComposerNodeViews.Find(VideoNodeSchema.TypeId), Is.TypeOf<VideoNodeView>());
            Assert.That(ComposerNodeViews.Find(UiNodeSchema.TypeId), Is.TypeOf<UiNodeView>());
            var video = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
            var ui = graph.AddComposerNode(UiNodeSchema.TypeId, Vector2.right * 400);
            Assert.That(video.UpdateProperties(new Dictionary<string, object> { { "frames", new List<object> { 120, 360 } } }).Success, Is.True);
            Assert.That(graph.ConnectPorts(video, "frame:120", ui, "enter").Success, Is.True);
            Assert.That(graph.ConnectPorts(video, "frame:360", ui, "enter").Success, Is.True);
            Assert.That(ui.UpdateProperties(new Dictionary<string, object> {
                { "uiType", Game.Framework.UI.MovieUIType.GetId(typeof(EditorMovieUi)) }, { "pauseVideo", true },
                { "parameters", new Dictionary<string, object> { { "question", "开门？" } } }
            }).Success, Is.True);
            var json = graph.Serialize(null);
            Assert.That(graph.Deserialize(json, null, true), Is.True);
            var captured = new ComposerGraphAdapter().Capture(graph);
            Assert.That(captured.Success, Is.True, captured.Message);
            Assert.That(captured.Value.Nodes.Single(n => n.TypeId == UiNodeSchema.TypeId).Properties["uiType"], Is.EqualTo(Game.Framework.UI.MovieUIType.GetId(typeof(EditorMovieUi))));
            Assert.That(captured.Value.Edges.Select(e => e.SourcePortId), Is.EquivalentTo(new[] { "frame:120", "frame:360" }));
            Assert.That(captured.Value.Nodes.Single(n => n.TypeId == UiNodeSchema.TypeId).Properties["pauseVideo"], Is.EqualTo(true));
        }

        [Test]
        public void RemovingFrameRemovesOnlyItsEdgesAndUndoRestoresEverything()
        {
            var video = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
            var ui = graph.AddComposerNode(UiNodeSchema.TypeId, Vector2.right * 400);
            var frames = new List<object> { 120, 360 };
            Assert.That(video.UpdateProperties(new Dictionary<string, object> { { "frames", frames } }).Success, Is.True);
            // The patch must be copied, not retained by the graph.
            frames.Add(999);
            Assert.That(video.Ports.Any(p => p.Id == "frame:999"), Is.False);
            Assert.That(graph.ConnectPorts(video, "frame:120", ui, "enter").Success, Is.True);
            Assert.That(graph.ConnectPorts(video, "frame:360", ui, "enter").Success, Is.True);
            Undo.ClearAll(); Undo.IncrementCurrentGroup();
            Assert.That(video.UpdateProperties(new Dictionary<string, object> { { "frames", new List<object> { 360 } } }).Success, Is.True);
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Select(e => e.SourcePortId), Is.EquivalentTo(new[] { "frame:360" }));
            Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup(); Undo.PerformUndo(); graph.SelfDeserialize();
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Count, Is.EqualTo(2));
            Undo.PerformRedo(); graph.SelfDeserialize();
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Select(e => e.SourcePortId), Is.EquivalentTo(new[] { "frame:360" }));
        }

        [Test]
        public void InvalidFramePatchDoesNotChangePortsOrEdgesAndDefaultsAreNotShared()
        {
            var video = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
            var otherVideo = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.right * 400);
            Assert.That(video.UpdateProperties(new Dictionary<string, object> { { "frames", new List<object> { 10 } } }).Success, Is.True);
            Assert.That(graph.ConnectPorts(video, "frame:10", otherVideo, "enter").Success, Is.True);
            Assert.That(video.UpdateProperties(new Dictionary<string, object> { { "frames", new List<object> { -1 } } }).Success, Is.False);
            Assert.That(video.Ports.Any(p => p.Id == "frame:10"), Is.True);
            Assert.That(otherVideo.Ports.Count, Is.EqualTo(3));
            Assert.That(new ComposerGraphAdapter().Capture(graph).Value.Edges.Count, Is.EqualTo(1));
        }
    }
}
