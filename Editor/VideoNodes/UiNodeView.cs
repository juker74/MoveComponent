using System;
using System.Collections.Generic;
using System.Linq;
using Game.Framework.UI;
using System.Runtime.CompilerServices;
using GameFrameX.NodeComposer.Editor;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace GameFrameX.NodeComposer.Video.Editor
{
    public static class MovieUiTypeCatalog
    {
        static readonly HashSet<string> playerAssemblies = new HashSet<string>(
            CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies).Select(a => a.name), StringComparer.Ordinal);
        public static IReadOnlyList<Type> Types { get; } = Array.AsReadOnly(
            TypeCache.GetTypesDerivedFrom<MovieUIForm>().Where(t => MovieUIType.IsSelectable(t) && playerAssemblies.Contains(t.Assembly.GetName().Name))
                .OrderBy(t => t.FullName, StringComparer.Ordinal)
                .ThenBy(t => t.Assembly.GetName().Name, StringComparer.Ordinal).ToArray());
    }

    public sealed class UiNodeView : IComposerNodeView
    {
        sealed class Draft { public string LastValue; public string Text; public string Error; }
        readonly ConditionalWeakTable<ComposerNode, Draft> drafts = new ConditionalWeakTable<ComposerNode, Draft>();
        public void DrawNode(ComposerNode node)
        {
            var id = VideoNodeView.Text(node, UiNodeSchema.UiTypeProperty);
            var type = MovieUIType.Resolve(id);
            GUILayout.Label("UI：" + (type != null && MovieUiTypeCatalog.Types.Contains(type) ? type.Name : string.IsNullOrEmpty(id) ? "未选择" : "类型不可用"));
            object pause;
            var paused = node.Data.Properties.TryGetValue(UiNodeSchema.PauseVideoProperty, out pause) && (bool)pause;
            GUILayout.Label(paused ? "交互时暂停来源视频" : "交互时视频继续播放", EditorStyles.miniLabel);
        }
        /// <summary>显式升级原生编辑图，旧 ID 无法推断为类，清空选择并保留所有节点和连线。</summary>
        public static void UpgradeLegacyUiNodes(ComposerGraph graph)
        {
            var nodes = graph.allNodes.OfType<ComposerNode>()
                .Where(n => n.Data.TypeId == UiNodeSchema.TypeId && n.Data.Properties.ContainsKey("uiId")).ToArray();
            if (nodes.Length == 0) return;
            graph.SelfSerialize();
            Undo.RegisterCompleteObjectUndo(graph, "升级影视 UI 类配置");
            foreach (var node in nodes)
            {
                node.Data.Properties.Remove("uiId");
                if (!node.Data.Properties.ContainsKey(UiNodeSchema.UiTypeProperty))
                    node.Data.Properties[UiNodeSchema.UiTypeProperty] = "";
            }
            graph.SelfSerialize();
            EditorUtility.SetDirty(graph);
        }
        static void SaveBranches(ComposerNode node, List<object> branches)
        {
            VideoNodeView.Set(node, new Dictionary<string, object> { { UiNodeSchema.BranchesProperty, branches } });
        }
        static void DrawBranches(ComposerNode node)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("分支输出端口", EditorStyles.boldLabel);
            var branches = UiBranches.Read(node.Data.Properties).Select(b => (object)new Dictionary<string, object> {
                { "id", b.Id }, { "name", b.Name } }).ToList();
            for (var i=0; i<branches.Count; i++)
            {
                var item=(Dictionary<string,object>)branches[i];
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField((i+1).ToString(), GUILayout.Width(22));
                EditorGUI.BeginChangeCheck();
                var name=EditorGUILayout.TextField((string)item["name"]);
                var renamed=EditorGUI.EndChangeCheck();
                bool up, down, remove;
                using (new EditorGUI.DisabledScope(i==0)) up=GUILayout.Button("↑",GUILayout.Width(25));
                using (new EditorGUI.DisabledScope(i==branches.Count-1)) down=GUILayout.Button("↓",GUILayout.Width(25));
                remove=GUILayout.Button("删除",GUILayout.Width(42));
                EditorGUILayout.EndHorizontal();
                if (renamed) { item["name"]=name; SaveBranches(node,branches); return; }
                if (up || down)
                {
                    var next=i+(up?-1:1);var other=branches[next];branches[next]=item;branches[i]=other;
                    SaveBranches(node,branches);return;
                }
                if (remove) { branches.RemoveAt(i);SaveBranches(node,branches);return; }
            }
            if (GUILayout.Button("添加分支"))
            {
                branches.Add(new Dictionary<string,object>{{"id",Guid.NewGuid().ToString("N")},{"name","分支 "+(branches.Count+1)}});
                SaveBranches(node,branches);
            }
            EditorGUILayout.HelpBox("按列表顺序将所有分支传入 UI。名称不决定路由；改名、排序保留连线，删除分支会移除对应连线。未连接的分支只结束当前 UI。",MessageType.Info);
        }
        public bool DrawInspector(ComposerNode node)
        {
            var graph = (ComposerGraph)node.graph;
            if (graph.allNodes.OfType<ComposerNode>().Any(n => n.Data.TypeId == UiNodeSchema.TypeId && n.Data.Properties.ContainsKey("uiId")))
            {
                EditorGUILayout.HelpBox("此图仍包含旧 UI ID。升级会保留节点、参数和连线；旧 ID 无法对应到类，需要重新选择。", MessageType.Warning);
                if (GUILayout.Button("升级此图的旧 UI 配置")) UpgradeLegacyUiNodes(graph);
                else return true;
            }
            var id = VideoNodeView.Text(node, UiNodeSchema.UiTypeProperty);
            var types = MovieUiTypeCatalog.Types;
            var ids = new List<string> { "" };
            var labels = new List<string> { "请选择影视 UI 类" };
            foreach (var type in types)
            {
                ids.Add(MovieUIType.GetId(type));
                labels.Add(type.FullName + " (" + type.Assembly.GetName().Name + ")");
            }
            var selected = ids.IndexOf(id);
            var unavailable = selected < 0;
            if (unavailable)
            {
                selected = ids.Count;
                ids.Add(id);
                labels.Add("类型不可用：" + id);
            }
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.Popup("影视 UI 类", selected, labels.ToArray());
            if (EditorGUI.EndChangeCheck())
                VideoNodeView.Set(node, new Dictionary<string, object> { { UiNodeSchema.UiTypeProperty, ids[next] } });
            if (!string.IsNullOrEmpty(id) && unavailable)
                EditorGUILayout.HelpBox("所选类型缺失、不继承 MovieUIForm 或仅在编辑器中可用，请重新选择。原配置已保留。", MessageType.Error);
            if (types.Count == 0)
                EditorGUILayout.HelpBox("尚未找到影视 UI。请创建继承 Game.Framework.UI.MovieUIForm 的非抽象类。", MessageType.Info);
            object value;
            var pause = node.Data.Properties.TryGetValue(UiNodeSchema.PauseVideoProperty, out value) && (bool)value;
            EditorGUI.BeginChangeCheck();
            pause = EditorGUILayout.Toggle("打开时暂停来源视频", pause);
            if (EditorGUI.EndChangeCheck())
                VideoNodeView.Set(node, new Dictionary<string, object> { { UiNodeSchema.PauseVideoProperty, pause } });

            DrawBranches(node);

            if (!node.Data.Properties.TryGetValue(UiNodeSchema.ParametersProperty, out value)) value = new Dictionary<string, object>();
            var encoded = GraphJsonCodec.EncodeValue(value);
            var draft = drafts.GetValue(node, key => new Draft());
            if (draft.LastValue != encoded.Value) { draft.LastValue = encoded.Value; draft.Text = encoded.Value; draft.Error = null; }
            EditorGUILayout.LabelField("传入参数（JSON 对象）");
            draft.Text = EditorGUILayout.TextArea(draft.Text ?? "{}", GUILayout.MinHeight(95));
            if (GUILayout.Button("应用参数"))
            {
                var parsed = GraphJsonCodec.DecodeValue(draft.Text);
                if (!parsed.Success) draft.Error = parsed.Message;
                else if (!(parsed.Value is IDictionary<string, object>)) draft.Error = "参数必须是 JSON 对象，例如 {\"text\":\"请选择\"}。";
                else
                {
                    var result = node.UpdateProperties(new Dictionary<string, object> { { UiNodeSchema.ParametersProperty, parsed.Value } });
                    draft.Error = result.Success ? null : result.Message;
                }
            }
            if (!string.IsNullOrEmpty(draft.Error)) EditorGUILayout.HelpBox(draft.Error, MessageType.Error);
            EditorGUILayout.HelpBox("图节点不等于同级运行状态。从视频帧端口连接“打开”，业务会在保留来源视频的情况下打开附属 UI；UI 提交分支后由该分支连线决定下一步。编辑器预览不执行业务。", MessageType.Info);
            return true;
        }
    }
}
