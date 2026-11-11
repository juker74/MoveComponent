using System;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Game.VideoFlow
{
    /// <summary>每次 Open 创建独立播放器。资源映射由宿主提供，不使用 AssetDatabase。</summary>
    public sealed class UnityVideoPlaybackService : IVideoPlaybackService
    {
        readonly Transform parent;
        readonly RenderTexture target;
        readonly Func<VideoPlaybackRequest, VideoClip> resolveClip;
        readonly Func<VideoPlaybackRequest, string> resolveUrl;
        public UnityVideoPlaybackService(Transform parent = null, RenderTexture target = null,
            Func<VideoPlaybackRequest, VideoClip> resolveClip = null, Func<VideoPlaybackRequest, string> resolveUrl = null)
        { this.parent=parent;this.target=target;this.resolveClip=resolveClip;this.resolveUrl=resolveUrl; }
        public IVideoPlaybackSession Open(VideoPlaybackRequest request)
        {
            if(request==null)throw new ArgumentNullException(nameof(request));
            var clip=resolveClip?.Invoke(request);
            var url=clip==null?(resolveUrl!=null?resolveUrl(request):request.Source):null;
            if(clip==null && (string.IsNullOrWhiteSpace(url) || url.StartsWith("Assets/",StringComparison.Ordinal) || url.StartsWith("Packages/",StringComparison.Ordinal)))
                throw new InvalidOperationException("项目视频路径需要映射到 VideoClip 或可播放 URL: "+request.Source);
            return new Session(parent,target,request,clip,url);
        }
        sealed class Session : IVideoPlaybackSession
        {
            VideoPlayer player;GameObject owner;bool disposed,paused;
            public VideoPlaybackStatus Status {get;private set;}=VideoPlaybackStatus.Preparing;
            public string ErrorMessage {get;private set;}
            public long Frame {get{return player!=null && player.isPrepared?player.frame:-1;}}
            public Session(Transform parent,RenderTexture target,VideoPlaybackRequest request,VideoClip clip,string url)
            {
                try
                {
                    owner=new GameObject("VideoFlow Playback");owner.transform.SetParent(parent,false);
                    player=owner.AddComponent<VideoPlayer>();player.playOnAwake=false;player.isLooping=false;
                    player.audioOutputMode=VideoAudioOutputMode.Direct;
                    player.renderMode=target!=null?VideoRenderMode.RenderTexture:VideoRenderMode.APIOnly;player.targetTexture=target;
                    if(clip!=null){player.source=VideoSource.VideoClip;player.clip=clip;}else{player.source=VideoSource.Url;player.url=url;}
                    player.prepareCompleted+=Prepared;player.loopPointReached+=Completed;player.errorReceived+=Failed;
                    volume=request.Volume;player.Prepare();
                }
                catch {Dispose();throw;}
            }
            readonly float volume;
            void Prepared(VideoPlayer sender)
            {
                if(disposed || Status!=VideoPlaybackStatus.Preparing)return;
                for(ushort i=0;i<sender.audioTrackCount;i++)sender.SetDirectAudioVolume(i,volume);
                Status=VideoPlaybackStatus.Playing;if(!paused)sender.Play();
            }
            void Completed(VideoPlayer sender){if(!disposed && Status==VideoPlaybackStatus.Playing)Status=VideoPlaybackStatus.Completed;}
            void Failed(VideoPlayer sender,string error){if(!disposed && (Status==VideoPlaybackStatus.Preparing || Status==VideoPlaybackStatus.Playing)){ErrorMessage=error;Status=VideoPlaybackStatus.Failed;}}
            public void Pause(){if(disposed)return;paused=true;if(player!=null && player.isPrepared)player.Pause();}
            public void Resume(){if(disposed)return;paused=false;if(Status==VideoPlaybackStatus.Playing)player.Play();}
            public void Stop()
            {
                if(disposed)return;
                if(Status!=VideoPlaybackStatus.Completed && Status!=VideoPlaybackStatus.Failed)Status=VideoPlaybackStatus.Stopped;
                if(player!=null)player.Stop();
            }
            public void Dispose()
            {
                if(disposed)return;
                try{Stop();}
                finally
                {
                    disposed=true;
                    if(player!=null){player.prepareCompleted-=Prepared;player.loopPointReached-=Completed;player.errorReceived-=Failed;player.targetTexture=null;}
                    if(owner!=null){if(Application.isPlaying)Object.Destroy(owner);else Object.DestroyImmediate(owner);}
                    player=null;owner=null;
                }
            }
        }
    }
}
