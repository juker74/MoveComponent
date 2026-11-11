using System;
using System.Collections.Generic;
using GameFrameX.NodeComposer.Editor;
using NodeCanvas.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;

namespace GameFrameX.NodeComposer.Video.Editor
{
    /// <summary>仅编辑器使用的单视频预览，不执行任何业务节点。</summary>
    [InitializeOnLoad]
    static class VideoNodePreview
    {
        static EditorVideoSession session;
        static ComposerNode owner;
        static VideoClip clip;
        static string error;
        static double lastDraw;
        static double lastRepaint;
        static bool pendingSeek;
        static long requestedFrame = -1;
        static long cachedFrame = -1;
        static readonly Dictionary<VideoClip, Texture2D> posters = new Dictionary<VideoClip, Texture2D>();
        public static long Frame { get { return session != null ? session.Frame : 0; } }
        public static bool HasImage { get { return session != null && session.HasImage; } }
        public static string Error { get { return error ?? (session != null ? session.Error : null); } }

        static VideoNodePreview()
        {
            EditorApplication.update += Update;
            AssemblyReloadEvents.beforeAssemblyReload += Clear;
            EditorApplication.quitting += Clear;
            EditorApplication.playModeStateChanged += state => Clear();
            Undo.undoRedoPerformed += Release;
        }

        public static VideoClip ResolveClip(ComposerNode node)
        {
            object value;
            var guid = node.Data.Properties.TryGetValue(VideoNodeSchema.AssetGuidProperty, out value) ? value as string : null;
            var path = !string.IsNullOrEmpty(guid) ? AssetDatabase.GUIDToAssetPath(guid)
                : node.Data.Properties.TryGetValue(VideoNodeSchema.SourceProperty, out value) ? value as string : null;
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<VideoClip>(path);
        }

        public static void Draw(ComposerNode node, Rect rect, VideoClip video)
        {
            if (owner == node && video == null) Release();
            var selected = GraphEditorUtility.activeElement == node;
            if (selected && video != null && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                if (owner != node || clip != video) Open(node, video);
                lastDraw = EditorApplication.timeSinceStartup;
            }
            Texture picture = owner == node && HasImage ? session.Texture : null;
            Texture2D poster;
            if (picture == null && video != null && posters.TryGetValue(video, out poster)) picture = poster;
            if (picture == null && video != null) picture = AssetPreview.GetAssetPreview(video);
            EditorGUI.DrawRect(rect, new Color(.08f, .08f, .08f));
            if (picture != null) GUI.DrawTexture(rect, picture, ScaleMode.ScaleToFit);
            else GUI.Label(rect, video == null ? "拖入 Project 中的视频资源\n或在属性面板填写地址" : selected ? "正在准备视频预览…" : "选中节点生成视频画面预览", Centered());
            if (owner == node && !string.IsNullOrEmpty(Error)) GUI.Label(rect, "预览失败：" + Error, Centered());
            if (!selected || video == null) return;
            // Decoder readiness can change between Layout and Repaint; keep the control tree stable.
            using (new EditorGUI.DisabledScope(session == null || owner != node || !session.Ready))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(session != null && session.Playing ? "暂停预览" : "播放预览"))
                    {
                        if (session.Playing) session.Pause();
                        else
                        {
                            if (Frame >= (long)video.frameCount - 1) Seek(0);
                            else { requestedFrame = -1; session.Play(); }
                        }
                    }
                    GUILayout.Label("帧 " + Frame + " / " + Math.Max(0, (long)video.frameCount - 1), EditorStyles.miniLabel);
                }
                var maxFrame = Math.Min(int.MaxValue, Math.Max(0, (long)video.frameCount - 1));
                EditorGUI.BeginChangeCheck();
                var frame = EditorGUILayout.IntSlider((int)Math.Min(Frame, int.MaxValue), 0, (int)maxFrame);
                if (EditorGUI.EndChangeCheck()) Seek(frame);
            }
        }

        static GUIStyle Centered() { return new GUIStyle(EditorStyles.centeredGreyMiniLabel) { wordWrap = true }; }
        public static bool IsOwner(ComposerNode node) { return owner == node && session != null && session.Ready; }

        static void Open(ComposerNode node, VideoClip video)
        {
            Release(); owner = node; clip = video; lastDraw = EditorApplication.timeSinceStartup;
            try { session = new EditorVideoSession(System.IO.Path.GetFullPath(AssetDatabase.GetAssetPath(video))); }
            catch (Exception e) { error = e.GetBaseException().Message; }
        }

        public static void Seek(long frame)
        {
            if (session == null || !session.Ready) return;
            session.Pause(); requestedFrame = frame;
            if (!pendingSeek) ApplySeek();
        }
        static void ApplySeek()
        {
            if (Frame == requestedFrame) return;
            pendingSeek = true;
            session.Seek(requestedFrame, () => pendingSeek = false);
        }

        static void Update()
        {
            if (owner == null) return;
            if (owner.graph == null || !owner.graph.allNodes.Contains(owner)
                || GraphEditorUtility.activeElement != owner || EditorApplication.timeSinceStartup - lastDraw > 1)
            { Release(); return; }
            if (session == null || !string.IsNullOrEmpty(error)) return;
            try
            {
                session.Update();
                if (HasImage && cachedFrame != Frame && !session.Playing && clip != null)
                {
                    cachedFrame = Frame;
                    var source = session.Texture;
                    var scale = Mathf.Min(1f, 320f / Mathf.Max(source.width, source.height));
                    var width = Mathf.Max(1, Mathf.RoundToInt(source.width * scale));
                    var height = Mathf.Max(1, Mathf.RoundToInt(source.height * scale));
                    Texture2D poster;
                    if (posters.TryGetValue(clip, out poster) && (poster.width != width || poster.height != height))
                    {
                        UnityEngine.Object.DestroyImmediate(poster);
                        posters.Remove(clip);
                    }
                    if (!posters.TryGetValue(clip, out poster))
                    {
                        if (posters.Count >= 16) ClearPosters();
                        poster = new Texture2D(width, height, TextureFormat.RGB24, false) { hideFlags = HideFlags.HideAndDontSave };
                        posters.Add(clip, poster);
                    }
                    var previous = RenderTexture.active;
                    var scaled = RenderTexture.GetTemporary(width, height, 0);
                    try
                    {
                        Graphics.Blit(source, scaled);
                        RenderTexture.active = scaled;
                        poster.ReadPixels(new Rect(0, 0, width, height), 0, 0); poster.Apply();
                    }
                    finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(scaled); }
                }
                if (session.Ready && !pendingSeek && !session.Playing && requestedFrame >= 0 && Frame != requestedFrame)
                    ApplySeek();
                if (session.Playing) requestedFrame = -1;
                if (EditorApplication.timeSinceStartup - lastRepaint > (session.Playing ? 1d / 30 : .15))
                {
                    lastRepaint = EditorApplication.timeSinceStartup;
                    UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
                }
            }
            catch (Exception e) { error = e.GetBaseException().Message; session.Dispose(); session = null; }
        }

        static void Release()
        {
            if (session != null) session.Dispose();
            session = null; owner = null; clip = null; error = null;
            pendingSeek = false; requestedFrame = -1; cachedFrame = -1;
        }
        static void ClearPosters()
        {
            foreach (var poster in posters.Values) UnityEngine.Object.DestroyImmediate(poster);
            posters.Clear();
        }
        static void Clear() { Release(); ClearPosters(); }
    }
}
