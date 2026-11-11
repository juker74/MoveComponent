using System;
using System.Collections.Generic;
using System.Linq;
using GameFrameX.NodeComposer.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace GameFrameX.NodeComposer.Video.Editor
{
    public sealed class VideoNodeView : IComposerNodeView
    {
        public void DrawNode(ComposerNode node)
        {
            var rect = GUILayoutUtility.GetRect(290, 163);
            var clip = VideoNodePreview.ResolveClip(node);
            HandleDrop(node, rect);
            VideoNodePreview.Draw(node, rect, clip);
        }

        static void HandleDrop(ComposerNode node, Rect rect)
        {
            var e = Event.current;
            if (!rect.Contains(e.mousePosition) || (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
            var clip = DragAndDrop.objectReferences.Length == 1 ? DragAndDrop.objectReferences[0] as VideoClip : null;
            DragAndDrop.visualMode = clip != null ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (e.type == EventType.DragPerform && clip != null)
            {
                DragAndDrop.AcceptDrag();
                var result = AssignClip(node, clip);
                if (!result.Success) Debug.LogWarning(result.Message);
            }
            e.Use();
        }

        public static GraphResult<bool> AssignClip(ComposerNode node, VideoClip clip)
        {
            if (node == null || node.Data.TypeId != VideoNodeSchema.TypeId || clip == null)
                return GraphResult<bool>.Fail("VideoAsset", "请选择视频节点与 Project 中的视频资源。");
            var path = AssetDatabase.GetAssetPath(clip);
            if (string.IsNullOrEmpty(path)) return GraphResult<bool>.Fail("VideoAsset", "视频必须是工程资源。");
            return node.UpdateProperties(new Dictionary<string, object> {
                { VideoNodeSchema.SourceProperty, path },
                { VideoNodeSchema.AssetGuidProperty, AssetDatabase.AssetPathToGUID(path) }
            });
        }

        public bool DrawInspector(ComposerNode node)
        {
            EditorGUI.BeginChangeCheck();
            var clip = (VideoClip)EditorGUILayout.ObjectField("视频资源", VideoNodePreview.ResolveClip(node), typeof(VideoClip), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (clip != null) Report(AssignClip(node, clip));
                else Set(node, new Dictionary<string, object> { { VideoNodeSchema.SourceProperty, "" }, { VideoNodeSchema.AssetGuidProperty, "" } });
            }
            var source = Text(node, VideoNodeSchema.SourceProperty);
            EditorGUI.BeginChangeCheck();
            var next = EditorGUILayout.TextField("视频地址／资源路径", source);
            if (EditorGUI.EndChangeCheck())
                Set(node, new Dictionary<string, object> { { VideoNodeSchema.SourceProperty, next }, { VideoNodeSchema.AssetGuidProperty, "" } });
            EditorGUI.BeginChangeCheck();
            object volumeValue;
            var volume = node.Data.Properties.TryGetValue(VideoNodeSchema.VolumeProperty, out volumeValue) ? Convert.ToSingle(volumeValue) : 1;
            volume = EditorGUILayout.Slider("音量", volume, 0, 1);
            if (EditorGUI.EndChangeCheck()) Set(node, new Dictionary<string, object> { { VideoNodeSchema.VolumeProperty, (double)volume } });

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("帧输出端口", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("可添加多个触发帧；每帧端口可连接多个 UI。帧连线表示打开附属交互，不退出当前视频状态。删除或修改帧会移除旧端口连线，可撤销。", MessageType.Info);
            var frames = Frames(node);
            for (int i = 0; i < frames.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    var frame = Math.Max(0, EditorGUILayout.IntField("触发帧", frames[i]));
                    if (EditorGUI.EndChangeCheck())
                    {
                        if (!frames.Contains(frame)) { frames[i] = frame; SaveFrames(node, frames); }
                        else Debug.LogWarning("这个帧输出端口已经存在。");
                    }
                    if (GUILayout.Button("删除", GUILayout.Width(45)))
                    { frames.RemoveAt(i); SaveFrames(node, frames); return true; }
                }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("添加帧端口"))
                {
                    var frame = 0;
                    while (frames.Contains(frame)) frame++;
                    frames.Add(frame); SaveFrames(node, frames);
                }
                using (new EditorGUI.DisabledScope(!VideoNodePreview.IsOwner(node)))
                    if (GUILayout.Button("添加当前预览帧"))
                    {
                        var frame = (int)Math.Min(int.MaxValue, VideoNodePreview.Frame);
                        if (!frames.Contains(frame)) { frames.Add(frame); SaveFrames(node, frames); }
                    }
            }
            var currentClip = VideoNodePreview.ResolveClip(node);
            if (currentClip != null && frames.Any(f => (ulong)f >= currentClip.frameCount))
                EditorGUILayout.HelpBox("存在超出视频总帧数的端口，该触发点不会在正常播放中到达。", MessageType.Warning);
            EditorGUILayout.HelpBox("预览不执行 UI。URL 仅保存配置；拖入本地 VideoClip 可预览并定位帧。", MessageType.None);
            return true;
        }

        static List<int> Frames(ComposerNode node)
        {
            object value;
            return node.Data.Properties.TryGetValue(VideoNodeSchema.FramesProperty, out value)
                ? ((IList<object>)value).Select(Convert.ToInt32).OrderBy(f => f).ToList() : new List<int>();
        }
        static void SaveFrames(ComposerNode node, List<int> frames)
        { Set(node, new Dictionary<string, object> { { VideoNodeSchema.FramesProperty, frames.OrderBy(f => f).Select(f => (object)f).ToList() } }); }
        internal static string Text(ComposerNode node, string key)
        { object value; return node.Data.Properties.TryGetValue(key, out value) ? value as string ?? "" : ""; }
        internal static void Set(ComposerNode node, IDictionary<string, object> patch) { Report(node.UpdateProperties(patch)); }
        static void Report(GraphResult<bool> result) { if (!result.Success) Debug.LogWarning(result.Message); }
    }
}
