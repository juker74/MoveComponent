using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using GameFrameX.NodeComposer.Video.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameFrameX.NodeComposer.Video.Tests
{
    public sealed class ChapterCatalogTests
    {
        string temporary;
        string assetFolder;
        ChapterCatalogAsset catalog;
        readonly List<ComposerGraph> graphs = new List<ComposerGraph>();

        [SetUp]
        public void Setup()
        {
            temporary = Path.Combine(Path.GetTempPath(), "MoveComponentChapterTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            assetFolder = "Assets/ChapterCatalogTests_" + Guid.NewGuid().ToString("N");
            catalog = ScriptableObject.CreateInstance<ChapterCatalogAsset>();
            AddChapter("second"); AddChapter("first");
            catalog.StartChapterId = "second";
        }

        [TearDown]
        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) AssetDatabase.DeleteAsset(assetFolder);
            foreach (var graph in graphs) { Undo.ClearUndo(graph); Object.DestroyImmediate(graph); }
            graphs.Clear(); Undo.ClearUndo(catalog); Object.DestroyImmediate(catalog);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }

        void AddChapter(string id)
        {
            var graph = ScriptableObject.CreateInstance<ComposerGraph>(); graphs.Add(graph);
            var entry = graph.AddComposerNode("common.control", Vector2.zero);
            catalog.Chapters.Add(new ChapterCatalogEntry { ChapterId = id, Name = id, Graph = graph, EntryNodeId = entry.DocumentId });
        }

        ChapterCatalogDocument Document()
        {
            return new ChapterCatalogDocument {
                StartChapterId = catalog.StartChapterId,
                Chapters = catalog.Chapters.Select(c => new ChapterDocument {
                    ChapterId = c.ChapterId, Name = c.Name, GraphKey = "chapters/" + c.ChapterId + ".json", EntryNodeId = c.EntryNodeId
                }).ToList()
            };
        }

        string Export()
        {
            var destination = Path.Combine(temporary, "Bundle");
            var result = new ChapterCatalogFileService().Export(destination, catalog);
            Assert.That(result.Success, Is.True, result.Message); return destination;
        }

        [Test]
        public void CatalogJsonRoundTripPreservesChapterOrderAndStableIds()
        {
            var codec = new ChapterCatalogJsonCodec(); var encoded = codec.Encode(Document());
            Assert.That(encoded.Success, Is.True, encoded.Message);
            var decoded = codec.Decode(encoded.Value);
            Assert.That(decoded.Success, Is.True, decoded.Message);
            Assert.That(decoded.Value.StartChapterId, Is.EqualTo("second"));
            Assert.That(decoded.Value.Chapters.Select(c => c.ChapterId), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(decoded.Value.Chapters.Select(c => c.EntryNodeId), Is.EqualTo(catalog.Chapters.Select(c => c.EntryNodeId)));
        }

        [TestCase("../escape.json")]
        [TestCase("/absolute.json")]
        [TestCase("chapters/../../escape.json")]
        [TestCase("chapters\\escape.json")]
        [TestCase("C:/escape.json")]
        [TestCase("chapters/CON.json")]
        public void CatalogRejectsUnsafeResourceKeys(string graphKey)
        {
            var document = Document(); document.Chapters[0].GraphKey = graphKey;
            Assert.That(new ChapterCatalogJsonCodec().Encode(document).Success, Is.False);
        }

        [Test]
        public void CatalogRejectsDuplicateIdsEvenWhenOnlyCaseDiffers()
        {
            var document = Document(); document.Chapters[1].ChapterId = "SECOND";
            Assert.That(new ChapterCatalogJsonCodec().Encode(document).Success, Is.False);
        }

        [Test]
        public void CatalogRejectsMissingStartAndUnknownFieldsOrMalformedJson()
        {
            var codec = new ChapterCatalogJsonCodec(); var document = Document(); document.StartChapterId = "missing";
            Assert.That(codec.Encode(document).Success, Is.False);
            var json = codec.Encode(Document()).Value;
            Assert.That(codec.Decode(json.Replace("\"schemaVersion\"", "\"unknown\"")).Success, Is.False);
            var duplicate = json.Insert(json.IndexOf('{') + 1, "\"schemaVersion\":1,");
            Assert.That(duplicate, Is.Not.EqualTo(json));
            Assert.That(codec.Decode(duplicate).Success, Is.False);
            Assert.That(codec.Decode("{\"schemaVersion\":1 \"startChapterId\":\"second\",\"chapters\":[]}").Success, Is.False);
        }

        [Test]
        public void InspectorEditsKeepChapterIdentityAndCanUndo()
        {
            var inspector = UnityEditor.Editor.CreateEditor(catalog);
            try { Assert.That(inspector, Is.TypeOf<ChapterCatalogInspector>()); }
            finally { Object.DestroyImmediate(inspector); }
            Undo.IncrementCurrentGroup(); var serialized = new SerializedObject(catalog);
            serialized.FindProperty("Chapters").MoveArrayElement(0, 1);
            serialized.FindProperty("Chapters").GetArrayElementAtIndex(0).FindPropertyRelative("Name").stringValue = "Renamed";
            serialized.ApplyModifiedProperties(); Undo.FlushUndoRecordObjects(); Undo.IncrementCurrentGroup();
            Assert.That(catalog.Chapters.Select(c => c.ChapterId), Is.EqualTo(new[] { "first", "second" }));
            Assert.That(catalog.StartChapterId, Is.EqualTo("second")); Undo.PerformUndo();
            Assert.That(catalog.Chapters.Select(c => c.ChapterId), Is.EqualTo(new[] { "second", "first" }));
            Assert.That(catalog.Chapters[1].Name, Is.EqualTo("first"));
        }

        [Test]
        public void CatalogValidationRejectsMissingEntryPureDataAndDuplicateGraph()
        {
            var service = new ChapterCatalogFileService(); var original = catalog.Chapters[0].EntryNodeId;
            catalog.Chapters[0].EntryNodeId = "missing";
            Assert.That(service.Validate(catalog).Issues.Select(i => i.Code), Has.Member("ChapterEntry"));
            var data = graphs[0].AddComposerNode("common.integer", Vector2.one); catalog.Chapters[0].EntryNodeId = data.DocumentId;
            Assert.That(service.Validate(catalog).Issues.Select(i => i.Code), Has.Member("ChapterEntry"));
            catalog.Chapters[0].EntryNodeId = original; catalog.Chapters[1].Graph = graphs[0]; catalog.Chapters[1].EntryNodeId = original;
            Assert.That(service.Validate(catalog).Issues.Select(i => i.Code), Has.Member("ChapterGraph"));
        }

        [Test]
        public void CatalogUsesRuntimeVideoValidationBeforeExport()
        {
            var video = graphs[0].AddComposerNode(VideoNodeSchema.TypeId, Vector2.one); catalog.Chapters[0].EntryNodeId = video.DocumentId;
            var result = new ChapterCatalogFileService().Export(Path.Combine(temporary, "Invalid"), catalog);
            Assert.That(result.Success, Is.False); Assert.That(result.Issues.Select(i => i.Code), Has.Member("VideoSource"));
            Assert.That(Directory.Exists(Path.Combine(temporary, "Invalid")), Is.False);
        }

        [Test]
        public void ExportImportPreservesCatalogAndGraphIdentities()
        {
            var imported = new ChapterCatalogFileService().Import(Path.Combine(Export(), "catalog.json"), assetFolder);
            Assert.That(imported.Success, Is.True, imported.Message);
            Assert.That(imported.Value.StartChapterId, Is.EqualTo(catalog.StartChapterId));
            Assert.That(imported.Value.Chapters.Select(c => c.ChapterId), Is.EqualTo(catalog.Chapters.Select(c => c.ChapterId)));
            var codec = new GraphJsonCodec(ComposerGraph.Registry);
            for (var i = 0; i < catalog.Chapters.Count; i++)
            {
                var original = new ComposerGraphAdapter().Capture(catalog.Chapters[i].Graph);
                var restored = new ComposerGraphAdapter().Capture(imported.Value.Chapters[i].Graph);
                Assert.That(restored.Success, Is.True, restored.Message);
                Assert.That(codec.Encode(restored.Value).Value, Is.EqualTo(codec.Encode(original.Value).Value));
                Assert.That(imported.Value.Chapters[i].EntryNodeId, Is.EqualTo(catalog.Chapters[i].EntryNodeId));
                Assert.That(AssetDatabase.Contains(imported.Value.Chapters[i].Graph), Is.True);
            }
            var catalogPath = AssetDatabase.GetAssetPath(imported.Value);
            var graphAssets = imported.Value.Chapters.Select(c => c.Graph).ToArray();
            Resources.UnloadAsset(imported.Value);
            foreach (var graph in graphAssets) Resources.UnloadAsset(graph);
            var reloaded = AssetDatabase.LoadAssetAtPath<ChapterCatalogAsset>(catalogPath);
            Assert.That(reloaded, Is.Not.Null);
            Assert.That(reloaded.StartChapterId, Is.EqualTo(catalog.StartChapterId));
            Assert.That(reloaded.Chapters.Select(c => c.ChapterId), Is.EqualTo(catalog.Chapters.Select(c => c.ChapterId)));
            for (var i = 0; i < catalog.Chapters.Count; i++)
            {
                var original = new ComposerGraphAdapter().Capture(catalog.Chapters[i].Graph);
                var restored = new ComposerGraphAdapter().Capture(reloaded.Chapters[i].Graph);
                Assert.That(restored.Success, Is.True, restored.Message);
                Assert.That(codec.Encode(restored.Value).Value, Is.EqualTo(codec.Encode(original.Value).Value));
                Assert.That(reloaded.Chapters[i].EntryNodeId, Is.EqualTo(catalog.Chapters[i].EntryNodeId));
            }
        }

        [Test]
        public void ExportCanReplaceOwnedBundleAndKeepsExistingMetadata()
        {
            var bundle = Export(); File.WriteAllText(Path.Combine(bundle, "catalog.json.meta"), "test-metadata");
            catalog.Chapters.Reverse(); catalog.Chapters[0].Name = "New title";
            var result = new ChapterCatalogFileService().Export(bundle, catalog);
            Assert.That(result.Success, Is.True, result.Message);
            var loaded = new ChapterCatalogJsonCodec().Decode(File.ReadAllText(Path.Combine(bundle, "catalog.json")));
            Assert.That(loaded.Value.Chapters[0].ChapterId, Is.EqualTo("first")); Assert.That(loaded.Value.Chapters[0].Name, Is.EqualTo("New title"));
            Assert.That(File.ReadAllText(Path.Combine(bundle, "catalog.json.meta")), Is.EqualTo("test-metadata"));
            Assert.That(Directory.GetDirectories(temporary), Is.EqualTo(new[] { bundle }));
        }

        [Test]
        public void FailedExportLeavesPreviousBundleAndUnrelatedFilesUntouched()
        {
            var bundle = Export(); var previous = File.ReadAllText(Path.Combine(bundle, "catalog.json"));
            catalog.Chapters[0].EntryNodeId = "missing";
            Assert.That(new ChapterCatalogFileService().Export(bundle, catalog).Success, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(bundle, "catalog.json")), Is.EqualTo(previous));
            catalog.Chapters[0].EntryNodeId = ((ComposerNode)graphs[0].allNodes[0]).DocumentId;
            File.WriteAllText(Path.Combine(bundle, "UserNotes.txt"), "keep");
            Assert.That(new ChapterCatalogFileService().Export(bundle, catalog).Success, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(bundle, "UserNotes.txt")), Is.EqualTo("keep"));
            Assert.That(File.ReadAllText(Path.Combine(bundle, "catalog.json")), Is.EqualTo(previous));
        }

        [Test]
        public void ImportRejectsMalformedSecondGraphWithoutCreatingPartialAssets()
        {
            var bundle = Export(); File.WriteAllText(Path.Combine(bundle, "chapters/first.json"), "{}");
            var result = new ChapterCatalogFileService().Import(Path.Combine(bundle, "catalog.json"), assetFolder);
            Assert.That(result.Success, Is.False); Assert.That(Directory.Exists(assetFolder), Is.False); Assert.That(File.Exists(assetFolder + ".meta"), Is.False);
        }

        [Test]
        public void ImportRejectsTraversalAndExistingAssetFolder()
        {
            var bundle = Export(); var path = Path.Combine(bundle, "catalog.json"); var json = File.ReadAllText(path);
            File.WriteAllText(path, json.Replace("chapters/second.json", "../outside.json"));
            Assert.That(new ChapterCatalogFileService().Import(path, assetFolder).Success, Is.False); Assert.That(Directory.Exists(assetFolder), Is.False);
            File.WriteAllText(path, json); AssetDatabase.CreateFolder("Assets", Path.GetFileName(assetFolder));
            File.WriteAllText(Path.Combine(assetFolder, "Keep.txt"), "keep");
            Assert.That(new ChapterCatalogFileService().Import(path, assetFolder).Success, Is.False);
            Assert.That(File.ReadAllText(Path.Combine(assetFolder, "Keep.txt")), Is.EqualTo("keep"));
        }

        [Test]
        public void ExportAcceptsEmptyDedicatedDirectory()
        {
            var path = Path.Combine(temporary, "Empty"); Directory.CreateDirectory(path);
            var result = new ChapterCatalogFileService().Export(path, catalog);
            Assert.That(result.Success, Is.True, result.Message); Assert.That(File.Exists(Path.Combine(path, "catalog.json")), Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImportedGraphsHaveNoConstructionUndoAndPreservePreviousUserUndo(bool withImport)
        {
            var bundle = Export();
            foreach (var graph in graphs) Undo.ClearUndo(graph);
            Undo.ClearUndo(catalog);
            Undo.IncrementCurrentGroup();
            Undo.RecordObject(catalog, "Rename existing chapter");
            catalog.Chapters[0].Name = "User rename";
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            ChapterCatalogAsset importedAsset = null;
            if (withImport)
            {
                var imported = new ChapterCatalogFileService().Import(Path.Combine(bundle, "catalog.json"), assetFolder);
                Assert.That(imported.Success, Is.True, imported.Message);
                importedAsset = imported.Value;
            }
            var importedGraphs = importedAsset == null ? new ComposerGraph[0] : importedAsset.Chapters.Select(c => c.Graph).ToArray();
            var codec = new GraphJsonCodec(ComposerGraph.Registry);
            var before = importedGraphs.Select(g => codec.Encode(new ComposerGraphAdapter().Capture(g).Value).Value).ToArray();
            Undo.FlushUndoRecordObjects();
            Undo.IncrementCurrentGroup();
            Undo.PerformUndo();
            Assert.That(catalog.Chapters[0].Name, Is.EqualTo("second"), "Import must retain the user's preceding undo operation.");
            for (var i = 0; i < importedGraphs.Length; i++)
            {
                importedGraphs[i].SelfDeserialize();
                var after = new ComposerGraphAdapter().Capture(importedGraphs[i]);
                Assert.That(after.Success, Is.True, after.Message);
                Assert.That(codec.Encode(after.Value).Value, Is.EqualTo(before[i]));
            }
        }
    }
}
