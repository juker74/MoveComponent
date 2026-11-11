using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Video;

namespace GameFrameX.NodeComposer.Video.Editor
{
    /// <summary>
    /// Unity 2022.3 编辑模式视频会话。场景 VideoPlayer 在 EditMode 不推进准备流程，
    /// 使用引擎的原生预览解码器并显式更新；内部 API 只封装在此处，缺失时显示错误。
    /// </summary>
    sealed class EditorVideoSession : IDisposable
    {
        readonly object manager;
        object playback;
        readonly Type managerType;
        readonly Type playbackType;
        bool disposed;
        bool pauseOnFirstFrame = true;
        public bool Ready { get; private set; }
        public bool HasImage { get; private set; }
        public string Error { get; private set; }
        public long Frame { get; private set; }
        public RenderTexture Texture { get; private set; }
        public bool Playing { get { return !disposed && playback != null && Ready && (bool)Call("IsPlaying"); } }

        public EditorVideoSession(string path)
        {
            var assembly = typeof(VideoPlayer).Assembly;
            managerType = assembly.GetType("UnityEngineInternal.Video.VideoPlaybackMgr", true);
            playbackType = assembly.GetType("UnityEngineInternal.Video.VideoPlayback", true);
            manager = Activator.CreateInstance(managerType);
            try
            {
                var create = managerType.GetMethod("CreateVideoPlayback");
                if (create == null) throw new NotSupportedException("当前 Unity 缺少视频预览创建接口。");
                var parameters = create.GetParameters();
                if (parameters.Length != 5) throw new NotSupportedException("当前 Unity 的视频预览接口不兼容。");
                Action<string> onError = message => { if (!disposed) Error = message; };
                Action onReady = () => { if (!disposed) Ready = true; };
                Action onEnd = () => { };
                playback = create.Invoke(manager, new[] { (object)path,
                    ConvertCallback(parameters[1].ParameterType, onError),
                    ConvertCallback(parameters[2].ParameterType, onReady),
                    ConvertCallback(parameters[3].ParameterType, onEnd), false });
                if (playback == null) throw new InvalidOperationException("无法创建视频预览。");
            }
            catch { Dispose(); throw; }
        }

        static Delegate ConvertCallback(Type type, Delegate action)
        { return Delegate.CreateDelegate(type, action.Target, action.Method); }

        object Call(string method, params object[] args)
        {
            var member = playbackType.GetMethod(method);
            if (member == null) throw new NotSupportedException("当前 Unity 缺少预览接口：" + method);
            return member.Invoke(playback, args);
        }

        public void Update()
        {
            if (disposed || playback == null || !string.IsNullOrEmpty(Error)) return;
            managerType.GetMethod("Update").Invoke(manager, null);
            if (!Ready) return;
            if (Texture == null)
            {
                Texture = new RenderTexture((int)(uint)Call("GetWidth"), (int)(uint)Call("GetHeight"), 0)
                { name = "NodeComposer Video Preview Texture", hideFlags = HideFlags.HideAndDontSave };
                Texture.Create();
                Call("SetLoop", false);
                for (ushort i = 0; i < (ushort)Call("GetAudioTrackCount"); i++)
                    Call("SetAudioTarget", i, false, false, null);
                Play();
            }
            var args = new object[] { Texture, 0L };
            if ((bool)Call("GetTexture", args))
            {
                Frame = (long)args[1];
                HasImage = true;
                if (pauseOnFirstFrame) { pauseOnFirstFrame = false; Pause(); }
            }
        }

        public void Play() { if (Ready && !disposed) Call("StartPlayback"); }
        public void Pause() { if (Ready && !disposed) Call("PausePlayback"); }
        public void Seek(long frame, Action completed)
        {
            if (!Ready || disposed) return;
            Pause();
            var method = playbackType.GetMethod("SeekToFrame");
            var callback = ConvertCallback(method.GetParameters()[1].ParameterType, completed);
            method.Invoke(playback, new object[] { frame, callback });
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try
            {
                if (playback != null) managerType.GetMethod("ReleaseVideoPlayback").Invoke(manager, new[] { playback });
            }
            finally
            {
                playback = null;
                ((IDisposable)manager).Dispose();
                if (Texture != null) { Texture.Release(); UnityEngine.Object.DestroyImmediate(Texture); Texture = null; }
            }
        }
    }
}
