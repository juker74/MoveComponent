using System;

namespace Game.VideoFlow
{
    public enum VideoPlaybackStatus { Preparing, Playing, Completed, Failed, Stopped }

    public sealed class VideoPlaybackRequest
    {
        public string Source { get; }
        public float Volume { get; }
        public string AssetGuid { get; }

        public VideoPlaybackRequest(string source, float volume = 1f, string assetGuid = "")
        {
            if (string.IsNullOrWhiteSpace(source)) throw new ArgumentException("视频地址不能为空。", nameof(source));
            if (float.IsNaN(volume) || volume < 0 || volume > 1) throw new ArgumentOutOfRangeException(nameof(volume));
            Source = source;
            Volume = volume;
            AssetGuid = assetGuid ?? "";
        }
    }

    /// <summary>播放器适配边界，由业务注入节点行为，独立于具体播放器组件。</summary>
    public interface IVideoPlaybackService
    {
        /// <summary>立即返回独立会话；准备完成后自动播放。创建失败抛异常，不返回 null。</summary>
        IVideoPlaybackSession Open(VideoPlaybackRequest request);
    }

    /// <summary>状态在主线程更新；旧会话的回调不得修改后继会话。</summary>
    public interface IVideoPlaybackSession : IDisposable
    {
        VideoPlaybackStatus Status { get; }
        string ErrorMessage { get; }
        /// <summary>当前已呈现帧，准备期间为 -1。</summary>
        long Frame { get; }
        void Pause();
        void Resume();
        /// <summary>幂等停止；Completed/Failed 终态保留。Dispose 停止并释放资源且幂等。</summary>
        void Stop();
    }
}
