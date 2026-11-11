using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using NodeCanvas.Editor;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace GameFrameX.NodeComposer.Video.Editor
{
    [CustomEditor(typeof(ChapterCatalogAsset))]
    public sealed class ChapterCatalogInspector : UnityEditor.Editor
    {
        ReorderableList list;
        string message;
        MessageType messageType;
        static float Line { get { return EditorGUIUtility.singleLineHeight; } }

        void OnEnable()
        {
            var chapters = serializedObject.FindProperty("Chapters");
            list = new ReorderableList(serializedObject, chapters, true, true, true, true) {
                elementHeight = (Line + 4) * 4 + 8,
                drawHeaderCallback = r => EditorGUI.LabelField(r, "章节列表（拖动排序）"),
                drawElementCallback = DrawChapter
            };
            list.onAddCallback = l => {
                var index = chapters.arraySize++;
                var entry = chapters.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("ChapterId").stringValue = Guid.NewGuid().ToString("N");
                entry.FindPropertyRelative("Name").stringValue = "新章节";
                entry.FindPropertyRelative("Graph").objectReferenceValue = null;
                entry.FindPropertyRelative("EntryNodeId").stringValue = "";
                if (chapters.arraySize == 1) serializedObject.FindProperty("StartChapterId").stringValue = entry.FindPropertyRelative("ChapterId").stringValue;
                l.index = index;
            };
            list.onRemoveCallback = l => {
                if (l.index < 0 || l.index >= chapters.arraySize) return;
                var entry = chapters.GetArrayElementAtIndex(l.index);
                var start = serializedObject.FindProperty("StartChapterId");
                if (entry.FindPropertyRelative("ChapterId").stringValue == start.stringValue) start.stringValue = "";
                chapters.DeleteArrayElementAtIndex(l.index);
                l.index = Math.Min(l.index, chapters.arraySize - 1);
            };
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("一张 Graph 对应一章。排序决定默认章节顺序；名称和顺序变化不会改变章节 ID。", MessageType.Info);
            var entries = serializedObject.FindProperty("Chapters");
            var starts = new List<KeyValuePair<string, string>>();
            for (var i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                starts.Add(new KeyValuePair<string, string>(entry.FindPropertyRelative("ChapterId").stringValue, (i + 1) + ". " + entry.FindPropertyRelative("Name").stringValue));
            }
            DrawSelection(EditorGUILayout.GetControlRect(), "默认起始章节", serializedObject.FindProperty("StartChapterId"), starts);
            list.DoLayoutList();
            if (serializedObject.ApplyModifiedProperties()) message = null;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("校验目录"))
                {
                    var result = new ChapterCatalogFileService().Validate((ChapterCatalogAsset)target);
                    message = result.Success ? "校验通过，共 " + result.Value.Chapters.Count + " 章。" : result.Message;
                    messageType = result.Success ? MessageType.Info : MessageType.Error;
                }
                if (GUILayout.Button("导出全部 JSON")) ShowExport((ChapterCatalogAsset)target);
                if (GUILayout.Button("导入为新目录")) ShowImport();
            }
            if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, messageType);
            EditorGUILayout.HelpBox("导出目标须为空目录或本工具之前导出的专用目录。导入总是创建新资产，不替换当前目录。", MessageType.None);
        }

        void DrawChapter(Rect rect, int index, bool active, bool focused)
        {
            var entry = list.serializedProperty.GetArrayElementAtIndex(index);
            var row = new Rect(rect.x, rect.y + 4, rect.width, Line);
            EditorGUI.PropertyField(row, entry.FindPropertyRelative("Name"), new GUIContent("章节名称"));
            row.y += Line + 4;
            using (new EditorGUI.DisabledScope(true)) EditorGUI.TextField(row, "章节 ID", entry.FindPropertyRelative("ChapterId").stringValue);
            row.y += Line + 4;
            var graphProperty = entry.FindPropertyRelative("Graph");
            var entryProperty = entry.FindPropertyRelative("EntryNodeId");
            var graphRect = new Rect(row.x, row.y, Math.Max(0, row.width - 66), Line);
            EditorGUI.BeginChangeCheck();
            var graph = (ComposerGraph)EditorGUI.ObjectField(graphRect, "章节 Graph", graphProperty.objectReferenceValue, typeof(ComposerGraph), false);
            if (EditorGUI.EndChangeCheck()) { graphProperty.objectReferenceValue = graph; entryProperty.stringValue = ""; }
            using (new EditorGUI.DisabledScope(graph == null))
                if (GUI.Button(new Rect(row.xMax - 60, row.y, 60, Line), "打开图")) GraphEditor.OpenWindow(graph);
            row.y += Line + 4;
            var nodes = graph == null ? new ComposerNode[0] : graph.allNodes.OfType<ComposerNode>().Where(n => ChapterCatalogFileService.IsEntryNode(n.Data)).ToArray();
            var options = nodes.Select(n => new KeyValuePair<string, string>(n.DocumentId, n.name + " [" + n.DocumentId + "]")).ToList();
            DrawSelection(row, "入口节点", entryProperty, options);
        }

        static void DrawSelection(Rect rect, string label, SerializedProperty property, List<KeyValuePair<string, string>> options)
        {
            options.Insert(0, new KeyValuePair<string, string>("", "请选择"));
            var selected = options.FindIndex(p => p.Key == property.stringValue);
            if (selected < 0)
            {
                selected = options.Count;
                options.Add(new KeyValuePair<string, string>(property.stringValue, "已失效：" + property.stringValue));
            }
            EditorGUI.BeginChangeCheck();
            var next = EditorGUI.Popup(rect, label, selected, options.Select(p => p.Value).ToArray());
            if (EditorGUI.EndChangeCheck()) property.stringValue = options[next].Key;
        }

        [MenuItem("Tools/GameFrameX/Node Composer/创建章节目录")]
        public static void CreateCatalog()
        { ProjectWindowUtil.CreateAsset(CreateInstance<ChapterCatalogAsset>(), "ChapterCatalog.asset"); }

        [MenuItem("Tools/GameFrameX/Node Composer/导入章节目录 JSON")]
        public static void ShowImport()
        {
            var source = EditorUtility.OpenFilePanel("选择章节目录 catalog.json", "", "json");
            if (string.IsNullOrEmpty(source)) return;
            var parent = EditorUtility.OpenFolderPanel("选择 Assets 内的导入父目录（将新建子目录）", Application.dataPath, "");
            if (string.IsNullOrEmpty(parent)) return;
            var relative = FileUtil.GetProjectRelativePath(parent);
            if (!AssetDatabase.IsValidFolder(relative) || !(relative == "Assets" || relative.StartsWith("Assets/", StringComparison.Ordinal)))
            { EditorUtility.DisplayDialog("导入失败", "请选择当前工程 Assets 内的目录。", "确定"); return; }
            var destination = AssetDatabase.GenerateUniqueAssetPath(relative + "/ImportedChapters");
            var result = new ChapterCatalogFileService().Import(source, destination);
            if (!result.Success) EditorUtility.DisplayDialog("导入失败", result.Message, "确定");
            else { Selection.activeObject = result.Value; EditorGUIUtility.PingObject(result.Value); }
        }

        static void ShowExport(ChapterCatalogAsset asset)
        {
            var result = new ChapterCatalogFileService().Validate(asset);
            if (!result.Success) { EditorUtility.DisplayDialog("导出失败", result.Message, "确定"); return; }
            var folder = EditorUtility.SaveFolderPanel("选择专用的章节导出目录", "", asset.name + "_Bundle");
            if (string.IsNullOrEmpty(folder)) return;
            var exported = new ChapterCatalogFileService().Export(folder, asset);
            if (!exported.Success) EditorUtility.DisplayDialog("导出失败", exported.Message, "确定");
            else { AssetDatabase.Refresh(); EditorUtility.RevealInFinder(Path.Combine(exported.Value, "catalog.json")); }
        }
    }
}
