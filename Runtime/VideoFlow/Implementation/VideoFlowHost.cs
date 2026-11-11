using System;
using UnityEngine;
using UnityEngine.Video;

namespace Game.VideoFlow
{
    /// <summary>可选场景入口；使用本组件时由它唯一调用 Tick，不再同时挂接框架 FSM 更新。</summary>
    public sealed class VideoFlowHost : MonoBehaviour
    {
        [Serializable] public sealed class ClipBinding {public string sourceOrGuid;public VideoClip clip;}
        [SerializeField] TextAsset graphJson;
        [SerializeField] string entryNodeId;
        [SerializeField] MonoBehaviour uiService;
        [SerializeField] RenderTexture videoTarget;
        [SerializeField] ClipBinding[] clips=new ClipBinding[0];
        [SerializeField] bool playOnStart;
        bool changing, destroyed;
        public IVideoFlowDriver Driver {get;private set;}
        public event Action<FlowEvent> EventRaised;
        void Start(){if(playOnStart)Play();}
        public void Play()
        {
            if(changing || destroyed)return;
            changing=true;
            try{PlayCore();}finally{changing=false;}
        }
        void PlayCore()
        {
            if(graphJson==null)throw new InvalidOperationException("请配置流程 JSON。");
            var service=uiService as IFlowUiService;
            if(service==null)throw new InvalidOperationException("UI 服务组件必须实现 IFlowUiService。");
            var driver=new VideoFlowDriver(new VideoFlowLoader(),new VideoFlowBehaviorFactory(new UnityVideoPlaybackService(transform,videoTarget,ResolveClip),service));
            var loaded=driver.Load(graphJson.text);
            if(!loaded.Success){driver.Dispose();throw new InvalidOperationException(loaded.Message);}
            Driver?.Dispose();Driver=driver;driver.EventRaised+=OnFlowEvent;
            var started=driver.Start(entryNodeId);
            if(!started.Success)throw new InvalidOperationException(started.Message);
        }
        VideoClip ResolveClip(VideoPlaybackRequest request)
        {
            foreach(var binding in clips)
                if(binding!=null && binding.clip!=null && (binding.sourceOrGuid==request.Source || (!string.IsNullOrEmpty(request.AssetGuid) && binding.sourceOrGuid==request.AssetGuid)))return binding.clip;
            return null;
        }
        void OnFlowEvent(FlowEvent evt){FlowEventDispatcher.Raise(EventRaised,evt);}
        void Update(){Driver?.Tick(Time.deltaTime,Time.unscaledDeltaTime);}
        void OnDisable(){Stop();}
        void OnDestroy(){destroyed=true;Driver?.Dispose();Driver=null;}
        public void Stop()
        {
            if(changing)return;
            changing=true;
            try{Driver?.Stop();}finally{changing=false;}
        }
    }
}
