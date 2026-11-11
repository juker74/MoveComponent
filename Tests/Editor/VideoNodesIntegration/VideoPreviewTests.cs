using System;
using System.Collections;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using GameFrameX.NodeComposer.Video.Editor;
using NodeCanvas.Editor;
using NodeCanvas.Framework;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace GameFrameX.NodeComposer.Video.Tests
{
    public class VideoPreviewTests
    {
        const string Fixture = "Packages/com.gameframex.unity.movecomponent/Tests/Editor/VideoNodesIntegration/Fixtures/preview.mp4";
        ComposerGraph graph;
        PreviewWindow window;
        IGraphElement oldActive;

        public class PreviewWindow : EditorWindow
        {
            public ComposerNode Node;
            public int DrawCount;
            readonly VideoNodeView view = new VideoNodeView();
            void OnGUI() { DrawCount++; if (Node != null) view.DrawNode(Node); }
        }

        [SetUp]
        public void Setup()
        {
            graph = ScriptableObject.CreateInstance<ComposerGraph>();
            oldActive = GraphEditorUtility.activeElement;
        }
        [TearDown]
        public void Cleanup()
        {
            if (window != null) window.Close();
            GraphEditorUtility.activeElement = oldActive;
            Undo.ClearUndo(graph); Object.DestroyImmediate(graph);
        }

        [Test]
        public void AssigningClipStoresPortableReferenceAndResolvesMovedAssetByGuid()
        {
            var path = "Assets/__VideoNodeTest_" + Guid.NewGuid().ToString("N") + ".mp4";
            var moved = path.Replace(".mp4", "_moved.mp4");
            try
            {
                Assert.That(AssetDatabase.CopyAsset(Fixture, path), Is.True);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(path);
                Assert.That(clip, Is.Not.Null);
                var node = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
                Assert.That(VideoNodeView.AssignClip(node, clip).Success, Is.True);
                Assert.That(node.Data.Properties[VideoNodeSchema.SourceProperty], Is.EqualTo(path));
                Assert.That(node.Data.Properties[VideoNodeSchema.AssetGuidProperty], Is.EqualTo(AssetDatabase.AssetPathToGUID(path)));
                Assert.That(AssetDatabase.MoveAsset(path, moved), Is.Empty);
                Assert.That(VideoNodePreview.ResolveClip(node), Is.EqualTo(AssetDatabase.LoadAssetAtPath<VideoClip>(moved)));
                Assert.That(new ComposerGraphAdapter().Capture(graph).Success, Is.True);
            }
            finally { AssetDatabase.DeleteAsset(path); AssetDatabase.DeleteAsset(moved); }
        }

        [Test]
        public void PackageVideoPathResolvesToExistingPhysicalFile()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(Fixture);
            Assert.That(package, Is.Not.Null);
            var expected = System.IO.Path.GetFullPath(System.IO.Path.Combine(package.resolvedPath,
                "Tests/Editor/VideoNodesIntegration/Fixtures/preview.mp4"));
            var actual = System.IO.Path.GetFullPath(Fixture);
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(System.IO.File.Exists(actual), Is.True);
        }

        [UnityTest]
        public IEnumerator PreviewDecodesSeeksAndReleasesPlayerAfterSelectionChanges()
        {
            var clip = AssetDatabase.LoadAssetAtPath<VideoClip>(Fixture);
            Assert.That(clip, Is.Not.Null);
            var node = graph.AddComposerNode(VideoNodeSchema.TypeId, Vector2.zero);
            Assert.That(VideoNodeView.AssignClip(node, clip).Success, Is.True);
            GraphEditorUtility.activeElement = node;
            window = ScriptableObject.CreateInstance<PreviewWindow>();
            window.Node = node;
            window.ShowUtility(); window.Repaint();
            var timeout = EditorApplication.timeSinceStartup + 15;
            while ((!VideoNodePreview.IsOwner(node) || !VideoNodePreview.HasImage) && EditorApplication.timeSinceStartup < timeout)
            { window.Repaint(); yield return null; }
            Assert.That(VideoNodePreview.IsOwner(node) && VideoNodePreview.HasImage, Is.True,
                "视频未能解码预览帧。Draws=" + window.DrawCount + ", selected=" + (GraphEditorUtility.activeElement == node)
                + ", error=" + VideoNodePreview.Error + ", clipFrames=" + clip.frameCount
                + ", players=" + string.Join(";", Resources.FindObjectsOfTypeAll<VideoPlayer>().Select(p => p.name + ":prepared=" + p.isPrepared + ",playing=" + p.isPlaying + ",frame=" + p.frame + ",active=" + p.isActiveAndEnabled)));
            yield return null;
            var pixel = PreviewPixel();
            Assert.That(pixel.r, Is.GreaterThan(0.6f), "第一帧应显示红色视频画面。");
            VideoNodePreview.Seek(40);
            timeout = EditorApplication.timeSinceStartup + 10;
            while (VideoNodePreview.Frame != 40 && EditorApplication.timeSinceStartup < timeout)
            { window.Repaint(); yield return null; }
            Assert.That(VideoNodePreview.Frame, Is.EqualTo(40));
            yield return null;
            pixel = PreviewPixel();
            Assert.That(pixel.b, Is.GreaterThan(0.6f), "定位后应显示蓝色视频画面。");
            GraphEditorUtility.activeElement = null;
            timeout = EditorApplication.timeSinceStartup + 3;
            while (Resources.FindObjectsOfTypeAll<VideoPlayer>().Any(p => p.gameObject.name == "NodeComposer Video Preview") && EditorApplication.timeSinceStartup < timeout)
                yield return null;
            Assert.That(Resources.FindObjectsOfTypeAll<VideoPlayer>().Any(p => p.gameObject.name == "NodeComposer Video Preview"), Is.False);
        }

        static Color PreviewPixel()
        {
            var texture = Resources.FindObjectsOfTypeAll<RenderTexture>().Single(t => t.name == "NodeComposer Video Preview Texture");
            var previous = RenderTexture.active;
            var sample = new Texture2D(1, 1);
            try
            {
                RenderTexture.active = texture;
                sample.ReadPixels(new Rect(texture.width / 2, texture.height / 2, 1, 1), 0, 0);
                sample.Apply();
                return sample.GetPixel(0, 0);
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(sample); }
        }
    }
}
