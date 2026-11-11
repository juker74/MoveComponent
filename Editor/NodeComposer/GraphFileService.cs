using System;
using System.IO;
using System.Text;
using NodeCanvas.Editor;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    public sealed class GraphFileService
    {
        readonly GraphJsonCodec codec = new GraphJsonCodec(ComposerGraph.Registry);
        const long MaxFileBytes = 16 * 1024 * 1024;
        public GraphResult<GraphDocument> Import(string absolutePath)
        {
            try
            {
                if (!Path.IsPathRooted(absolutePath)) return GraphResult<GraphDocument>.Fail("Path", "请选择绝对路径。");
                if (new FileInfo(absolutePath).Length > MaxFileBytes) return GraphResult<GraphDocument>.Fail("FileSize", "JSON 文件超过 16 MiB。");
                return codec.Decode(File.ReadAllText(absolutePath, new UTF8Encoding(false, true)));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { return GraphResult<GraphDocument>.Fail("ReadFile", absolutePath + "\n" + e.Message); }
        }
        public GraphResult<string> Export(string absolutePath, GraphDocument document)
        {
            var encoded = codec.Encode(document);
            if (!encoded.Success) return encoded;
            string temporary = null;
            try
            {
                if (!Path.IsPathRooted(absolutePath)) return GraphResult<string>.Fail("Path", "请选择绝对路径。");
                if (Encoding.UTF8.GetByteCount(encoded.Value) > MaxFileBytes) return GraphResult<string>.Fail("FileSize", "JSON 文件超过 16 MiB。");
                temporary = Path.Combine(Path.GetDirectoryName(absolutePath), "." + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(temporary, encoded.Value, new UTF8Encoding(false, true));
                if (File.Exists(absolutePath)) File.Replace(temporary, absolutePath, null);
                else File.Move(temporary, absolutePath);
                return GraphResult<string>.Ok(absolutePath);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is ArgumentException || e is NotSupportedException)
            { return GraphResult<string>.Fail("WriteFile", absolutePath + "\n" + e.Message); }
            finally
            {
                if (temporary != null && File.Exists(temporary))
                { try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
            }
        }
        public static void ShowImport(ComposerGraph graph)
        {
            var path = EditorUtility.OpenFilePanel("导入节点配置 JSON", "", "json");
            if (string.IsNullOrEmpty(path)) return;
            var result = new GraphFileService().Import(path);
            if (!result.Success) { EditorUtility.DisplayDialog("导入失败", result.Message, "确定"); return; }
            if (graph.allNodes.Count > 0 && !EditorUtility.DisplayDialog("替换当前图", "将用文件内容替换当前图，可以撤销。", "导入", "取消")) return;
            var replaced = new ComposerGraphAdapter().Replace(graph, result.Value);
            if (!replaced.Success) EditorUtility.DisplayDialog("导入失败", replaced.Message, "确定");
            else { ComposerNode.CancelDrag(); GraphEditorUtility.activeElement = null; GraphEditorUtility.activeElements = null; GUI.changed = true; }
        }
        public static void ShowExport(ComposerGraph graph)
        {
            var result = new ComposerGraphAdapter().Capture(graph);
            if (!result.Success) { EditorUtility.DisplayDialog("导出失败", result.Message, "确定"); return; }
            var path = EditorUtility.SaveFilePanel("导出节点配置 JSON", "", graph.name, "json");
            if (string.IsNullOrEmpty(path)) return;
            var exported = new GraphFileService().Export(path, result.Value);
            if (!exported.Success) EditorUtility.DisplayDialog("导出失败", exported.Message, "确定");
            else AssetDatabase.Refresh();
        }
        [OnOpenAsset(0)]
        static bool OpenGraph(int instanceId, int line)
        {
            var graph = EditorUtility.InstanceIDToObject(instanceId) as ComposerGraph;
            if (graph == null) return false;
            GraphEditor.OpenWindow(graph); return true;
        }
        [MenuItem("Tools/GameFrameX/Node Composer/创建图")]
        public static void CreateGraph()
        {
            var graph = ScriptableObject.CreateInstance<ComposerGraph>();
            ProjectWindowUtil.CreateAsset(graph, "NodeComposerGraph.asset");
        }
        [MenuItem("Tools/GameFrameX/Node Composer/打开选中的图")]
        public static void OpenSelectedGraph()
        {
            var graph = Selection.activeObject as ComposerGraph;
            if (graph != null) GraphEditor.OpenWindow(graph);
            else EditorUtility.DisplayDialog("节点拼接工具", "请先在 Project 中选中 Node Composer 图资产，或通过创建图菜单新建。", "确定");
        }
    }
}
