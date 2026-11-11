using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Game.VideoFlow;
using GameFrameX.NodeComposer.Editor;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Video.Editor
{
    /// <summary>将目录与章图作为一个完整 bundle 交换；不修改现有图资产。</summary>
    public sealed class ChapterCatalogFileService
    {
        const string Marker = ".movecomponent-chapters";
        const string MarkerText = "MoveComponent Chapter Bundle v1\n";
        const long MaxFileBytes = 16 * 1024 * 1024;
        static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        readonly ChapterCatalogJsonCodec catalogCodec = new ChapterCatalogJsonCodec();
        readonly GraphJsonCodec graphCodec = new GraphJsonCodec(ComposerGraph.Registry);
        readonly ComposerGraphAdapter adapter = new ComposerGraphAdapter();

        sealed class Bundle
        {
            public ChapterCatalogDocument Catalog;
            public readonly List<GraphDocument> Graphs = new List<GraphDocument>();
            public readonly Dictionary<string, string> Files = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        public GraphResult<ChapterCatalogDocument> Validate(ChapterCatalogAsset asset)
        {
            var prepared = Capture(asset);
            return prepared.Success ? GraphResult<ChapterCatalogDocument>.Ok(prepared.Value.Catalog)
                : GraphResult<ChapterCatalogDocument>.Fail(prepared.Issues);
        }

        public static bool IsEntryNode(NodeDocument node)
        {
            INodeDefinition definition;
            if (node == null || !ComposerGraph.Registry.TryGet(node.TypeId, out definition)) return false;
            var value = definition as ValueNodeDefinition;
            return (value == null || value.Kind != ValueNodeKind.Cell)
                && ComposerGraph.Registry.GetPorts(node).Any(p => p.Kind == PortKind.Control && p.Direction == PortDirection.Input);
        }

        GraphResult<Bundle> Capture(ChapterCatalogAsset asset)
        {
            if (asset == null || asset.Chapters == null) return GraphResult<Bundle>.Fail("Catalog", "章节目录不能为空。");
            var bundle = new Bundle { Catalog = new ChapterCatalogDocument { StartChapterId = asset.StartChapterId } };
            var issues = new List<GraphIssue>();
            foreach (var entry in asset.Chapters)
            {
                if (entry == null) { bundle.Catalog.Chapters.Add(null); continue; }
                bundle.Catalog.Chapters.Add(new ChapterDocument {
                    ChapterId = entry.ChapterId, Name = entry.Name, EntryNodeId = entry.EntryNodeId,
                    GraphKey = "chapters/" + entry.ChapterId + ".json"
                });
                var captured = adapter.Capture(entry.Graph);
                if (!captured.Success) { AddIssues(issues, entry.Name, captured.Issues); continue; }
                bundle.Graphs.Add(captured.Value);
                ValidateGraph(captured.Value, entry.Name, entry.EntryNodeId, issues);
            }
            issues.AddRange(catalogCodec.Validate(bundle.Catalog));
            ValidateGraphIds(bundle.Graphs, issues);
            if (issues.Count != 0) return GraphResult<Bundle>.Fail(issues);
            return Encode(bundle);
        }

        void ValidateGraph(GraphDocument graph, string name, string entryId, List<GraphIssue> issues)
        {
            var encoded = graphCodec.Encode(graph);
            if (!encoded.Success) { AddIssues(issues, name, encoded.Issues); return; }
            var loaded = new VideoFlowLoader(ComposerGraph.Registry).Load(encoded.Value);
            if (!loaded.Success) AddIssues(issues, name, loaded.Issues);
            var entry = graph.Nodes.FirstOrDefault(n => n.Id == entryId);
            if (!IsEntryNode(entry)) issues.Add(new GraphIssue("ChapterEntry", "章节「" + name + "」：入口不存在或不是可执行的控制节点。", entryId));
        }

        static void AddIssues(List<GraphIssue> target, string chapter, IEnumerable<GraphIssue> issues)
        {
            target.AddRange(issues.Select(i => new GraphIssue(i.Code, "章节「" + chapter + "」：" + i.Message, i.NodeId, i.EdgeId, i.PropertyPath)));
        }

        static void ValidateGraphIds(IEnumerable<GraphDocument> graphs, List<GraphIssue> issues)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var graph in graphs)
                if (!ids.Add(graph.Id)) issues.Add(new GraphIssue("ChapterGraph", "一个目录中的 Graph ID 不可重复：" + graph.Id + "。请使用独立的章节图。"));
        }

        GraphResult<Bundle> Encode(Bundle bundle)
        {
            var catalog = catalogCodec.Encode(bundle.Catalog);
            if (!catalog.Success) return GraphResult<Bundle>.Fail(catalog.Issues);
            bundle.Files.Add("catalog.json", catalog.Value);
            for (var i = 0; i < bundle.Graphs.Count; i++)
            {
                var graph = graphCodec.Encode(bundle.Graphs[i]);
                if (!graph.Success) return GraphResult<Bundle>.Fail(graph.Issues);
                bundle.Files.Add(bundle.Catalog.Chapters[i].GraphKey, graph.Value);
            }
            if (bundle.Files.Values.Any(s => Utf8.GetByteCount(s) > MaxFileBytes))
                return GraphResult<Bundle>.Fail("FileSize", "单个 JSON 文件不可超过 16 MiB。");
            return GraphResult<Bundle>.Ok(bundle);
        }

        public GraphResult<string> Export(string absoluteDirectory, ChapterCatalogAsset asset)
        {
            var prepared = Capture(asset);
            if (!prepared.Success) return GraphResult<string>.Fail(prepared.Issues);
            string stage = null, backup = null, destination = null;
            try
            {
                if (!Path.IsPathRooted(absoluteDirectory)) throw new IOException("请选择绝对路径。");
                destination = Path.GetFullPath(absoluteDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var parent = Path.GetDirectoryName(destination);
                if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent)) throw new IOException("导出目录的父目录必须存在。");
                var assets = Path.GetFullPath(Application.dataPath);
                if (assets.Equals(destination, StringComparison.OrdinalIgnoreCase) || assets.StartsWith(destination + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("请选择独立的导出子目录，不能替换工程或 Assets 根目录。");
                if (File.Exists(destination)) throw new IOException("导出目标已经是文件。");
                if (Directory.Exists(destination)) CheckOwnedDirectory(destination);
                stage = Path.Combine(parent, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".tmp");
                Directory.CreateDirectory(stage);
                File.WriteAllText(Path.Combine(stage, Marker), MarkerText, Utf8);
                foreach (var file in prepared.Value.Files)
                {
                    var path = Path.Combine(stage, file.Key);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllText(path, file.Value, Utf8);
                    // Keep existing Unity JSON metadata when exporting inside Assets.
                    var previousMeta = Path.Combine(destination, file.Key) + ".meta";
                    if (File.Exists(previousMeta)) File.Copy(previousMeta, path + ".meta");
                }
                if (File.Exists(Path.Combine(destination, "chapters.meta")))
                    File.Copy(Path.Combine(destination, "chapters.meta"), Path.Combine(stage, "chapters.meta"));
                if (Directory.Exists(destination))
                {
                    backup = Path.Combine(parent, "." + Path.GetFileName(destination) + "." + Guid.NewGuid().ToString("N") + ".backup");
                    Directory.Move(destination, backup);
                }
                try { Directory.Move(stage, destination); stage = null; }
                catch
                {
                    if (backup != null) { Directory.Move(backup, destination); backup = null; }
                    throw;
                }
                if (backup != null) { DeleteTemporary(backup); backup = null; }
                return GraphResult<string>.Ok(destination);
            }
            catch (Exception e)
            {
                var recovery = backup == null ? "" : "\n原目录备份保留在：" + backup;
                return GraphResult<string>.Fail("ChapterExport", e.Message + recovery);
            }
            finally { if (stage != null) DeleteTemporary(stage); }
        }

        void CheckOwnedDirectory(string directory)
        {
            RejectLink(directory);
            if (Directory.GetFileSystemEntries(directory).Length == 0) return;
            var marker = Path.Combine(directory, Marker);
            if (!File.Exists(marker) || ReadText(marker) != MarkerText)
                throw new IOException("已有目录不属于章节导出，请选择一个尚不存在的专用子目录。");
            var decoded = catalogCodec.Decode(ReadText(Path.Combine(directory, "catalog.json")));
            if (!decoded.Success) throw new IOException("已有导出目录的清单无效，请选择新的子目录。\n" + decoded.Message);
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Marker, Marker + ".meta", "catalog.json", "catalog.json.meta", "chapters.meta" };
            foreach (var chapter in decoded.Value.Chapters) { allowed.Add(chapter.GraphKey); allowed.Add(chapter.GraphKey + ".meta"); }
            foreach (var child in Directory.GetDirectories(directory))
            {
                RejectLink(child);
                if (Path.GetFileName(child) != "chapters" || Directory.GetDirectories(child).Length != 0)
                    throw new IOException("已有目录包含其他文件夹，已取消替换。");
            }
            foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
            {
                RejectLink(file);
                var relative = file.Substring(directory.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                if (!allowed.Contains(relative)) throw new IOException("已有目录包含其他文件，已取消替换：" + relative);
            }
        }

        public GraphResult<ChapterCatalogAsset> Import(string absoluteCatalogPath, string newAssetFolder)
        {
            var prepared = ReadBundle(absoluteCatalogPath);
            if (!prepared.Success) return GraphResult<ChapterCatalogAsset>.Fail(prepared.Issues);
            var graphs = new List<ComposerGraph>();
            ChapterCatalogAsset catalog = null;
            var createdFolder = false;
            try
            {
                if (string.IsNullOrEmpty(newAssetFolder) || !newAssetFolder.StartsWith("Assets/", StringComparison.Ordinal)
                    || newAssetFolder.Contains('\\') || newAssetFolder.Split('/').Any(s => s == "" || s == "." || s == ".."))
                    throw new IOException("导入目标必须是 Assets 内尚不存在的子目录。");
                var absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), newAssetFolder));
                if (!absolute.StartsWith(Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    throw new IOException("导入目标必须位于当前工程 Assets 内。");
                if (Directory.Exists(absolute) || File.Exists(absolute) || File.Exists(absolute + ".meta"))
                    throw new IOException("导入目标已存在，请选择新的子目录。");
                var parent = Path.GetDirectoryName(newAssetFolder).Replace('\\', '/');
                if (!AssetDatabase.IsValidFolder(parent)) throw new IOException("导入目标的父目录不存在。");
                var assetsRoot = Path.GetFullPath(Application.dataPath);
                for (var existing = Path.GetDirectoryName(absolute); existing != null && existing.Length >= assetsRoot.Length; existing = Path.GetDirectoryName(existing))
                    RejectLink(existing);
                var temporaryGraphs = new List<ComposerGraph>();
                var nativeJson = new List<string>();
                var nativeReferences = new List<List<UnityEngine.Object>>();
                // NodeCanvas records both complete-object and diff Undo during
                // construction. Isolate and revert that work before asset creation.
                Undo.FlushUndoRecordObjects();
                Undo.IncrementCurrentGroup();
                var constructionGroup = Undo.GetCurrentGroup();
                try
                {
                    foreach (var document in prepared.Value.Graphs)
                    {
                        var graph = adapter.Create(document);
                        if (!graph.Success) return GraphResult<ChapterCatalogAsset>.Fail(graph.Issues);
                        temporaryGraphs.Add(graph.Value);
                        var references = new List<UnityEngine.Object>();
                        nativeJson.Add(graph.Value.Serialize(references));
                        nativeReferences.Add(references);
                    }
                }
                finally
                {
                    Undo.FlushUndoRecordObjects();
                    Undo.RevertAllDownToGroup(constructionGroup);
                    Undo.IncrementCurrentGroup();
                    foreach (var graph in temporaryGraphs) UnityEngine.Object.DestroyImmediate(graph);
                }
                for (var i = 0; i < nativeJson.Count; i++)
                {
                    var graph = ScriptableObject.CreateInstance<ComposerGraph>();
                    graphs.Add(graph);
                    if (!graph.Deserialize(nativeJson[i], nativeReferences[i], true))
                        throw new IOException("无法恢复导入章节图。");
                }
                var guid = AssetDatabase.CreateFolder(parent, Path.GetFileName(newAssetFolder));
                if (string.IsNullOrEmpty(guid)) throw new IOException("无法创建导入目录。");
                createdFolder = true;
                if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(newAssetFolder, "Graphs"))) throw new IOException("无法创建章节图目录。");
                catalog = ScriptableObject.CreateInstance<ChapterCatalogAsset>();
                catalog.StartChapterId = prepared.Value.Catalog.StartChapterId;
                for (var i = 0; i < graphs.Count; i++)
                {
                    var chapter = prepared.Value.Catalog.Chapters[i];
                    CreateAssetChecked(graphs[i], newAssetFolder + "/Graphs/" + chapter.ChapterId + ".asset");
                    catalog.Chapters.Add(new ChapterCatalogEntry { ChapterId = chapter.ChapterId, Name = chapter.Name, Graph = graphs[i], EntryNodeId = chapter.EntryNodeId });
                }
                CreateAssetChecked(catalog, newAssetFolder + "/ChapterCatalog.asset");
                AssetDatabase.SaveAssetIfDirty(catalog);
                return GraphResult<ChapterCatalogAsset>.Ok(catalog);
            }
            catch (Exception e)
            {
                var message = e.Message;
                if (createdFolder)
                {
                    try
                    {
                        if (!AssetDatabase.DeleteAsset(newAssetFolder)) message += "\n无法清理导入目录，请检查：" + newAssetFolder;
                    }
                    catch (Exception cleanup) { message += "\n导入目录保留在 " + newAssetFolder + "：" + cleanup.Message; }
                }
                return GraphResult<ChapterCatalogAsset>.Fail("ChapterImport", message);
            }
            finally
            {
                foreach (var graph in graphs)
                    if (graph != null && !AssetDatabase.Contains(graph)) UnityEngine.Object.DestroyImmediate(graph);
                if (catalog != null && !AssetDatabase.Contains(catalog)) UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        GraphResult<Bundle> ReadBundle(string absoluteCatalogPath)
        {
            try
            {
                if (!Path.IsPathRooted(absoluteCatalogPath)) throw new IOException("请选择绝对路径。");
                var root = Path.GetDirectoryName(Path.GetFullPath(absoluteCatalogPath));
                RejectLink(root);
                var decoded = catalogCodec.Decode(ReadText(absoluteCatalogPath));
                if (!decoded.Success) return GraphResult<Bundle>.Fail(decoded.Issues);
                var bundle = new Bundle { Catalog = decoded.Value };
                var issues = new List<GraphIssue>();
                foreach (var chapter in bundle.Catalog.Chapters)
                {
                    var path = root;
                    foreach (var segment in chapter.GraphKey.Split('/')) { path = Path.Combine(path, segment); RejectLink(path); }
                    var graph = graphCodec.Decode(ReadText(path));
                    if (!graph.Success) { AddIssues(issues, chapter.Name, graph.Issues); continue; }
                    bundle.Graphs.Add(graph.Value);
                    ValidateGraph(graph.Value, chapter.Name, chapter.EntryNodeId, issues);
                }
                ValidateGraphIds(bundle.Graphs, issues);
                return issues.Count == 0 ? GraphResult<Bundle>.Ok(bundle) : GraphResult<Bundle>.Fail(issues);
            }
            catch (Exception e) { return GraphResult<Bundle>.Fail("ChapterRead", e.Message); }
        }

        static string ReadText(string path)
        {
            RejectLink(path);
            if (new FileInfo(path).Length > MaxFileBytes) throw new IOException("JSON 文件超过 16 MiB：" + path);
            return File.ReadAllText(path, Utf8);
        }

        static void CreateAssetChecked(UnityEngine.Object asset, string path)
        {
            AssetDatabase.CreateAsset(asset, path);
            if (!AssetDatabase.Contains(asset) || AssetDatabase.GetAssetPath(asset) != path || AssetDatabase.LoadMainAssetAtPath(path) != asset)
                throw new IOException("未能保存导入资产：" + path);
        }

        static void RejectLink(string path)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("章节文件与目录不能使用符号链接：" + path);
        }

        static void DeleteTemporary(string path)
        {
            try { if (Directory.Exists(path)) Directory.Delete(path, true); }
            catch (IOException e) { Debug.LogWarning("章节临时目录保留在 " + path + "：" + e.Message); }
            catch (UnauthorizedAccessException e) { Debug.LogWarning("章节临时目录保留在 " + path + "：" + e.Message); }
        }
    }
}
