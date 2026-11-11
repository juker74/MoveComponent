using System;
using Game.Framework.UI;
using System.Collections.Generic;
using GameFrameX.NodeComposer.Video;

namespace Game.VideoFlow
{
    /// <summary>内置视频/UI 行为。新增节点可组合另一个工厂，不需要修改调度器。</summary>
    public sealed class VideoFlowBehaviorFactory : IFlowNodeBehaviorFactory
    {
        readonly IVideoPlaybackService videos;
        readonly IFlowUiService ui;
        public VideoFlowBehaviorFactory(IVideoPlaybackService videos,IFlowUiService ui)
        {this.videos=videos??throw new ArgumentNullException(nameof(videos));this.ui=ui??throw new ArgumentNullException(nameof(ui));}
        public bool Supports(string typeId){return typeId==VideoNodeSchema.TypeId || typeId==UiNodeSchema.TypeId;}
        public IFlowNodeBehavior Create(IFlowNode node)
        {
            if(node.TypeId==VideoNodeSchema.TypeId)return new VideoBehavior(videos);
            if(node.TypeId==UiNodeSchema.TypeId)return new UiBehavior(ui);
            throw new ArgumentException("没有对应行为: "+node.TypeId);
        }
        sealed class VideoBehavior : IFlowNodeBehavior
        {
            readonly IVideoPlaybackService service;
            IFlowNodeContext context;IVideoPlaybackSession session;
            public VideoBehavior(IVideoPlaybackService service){this.service=service;}
            public void Enter(IFlowNodeContext c)
            {
                context=c;
                session=service.Open(new VideoPlaybackRequest((string)c.Node.Properties["source"],Convert.ToSingle(c.Node.Properties["volume"]),(string)c.Node.Properties["assetGuid"]))??throw new InvalidOperationException("播放器返回 null。");
                c.BindVideo(session);
            }
            public void Update(float delta,float real)
            {
                if(session.Status==VideoPlaybackStatus.Failed)
                {if(context.HasOutputConnection("failed"))context.Complete("failed");else context.Fail("VideoPlayback",session.ErrorMessage);return;}
                if(session.Status==VideoPlaybackStatus.Stopped){context.Cancel();return;}
                if(session.Status==VideoPlaybackStatus.Completed)context.Complete("completed");
            }
            public void Exit(){session?.Stop();}
            public void Dispose(){var s=session;session=null;s?.Dispose();}
        }
        sealed class UiBehavior : IFlowNodeBehavior
        {
            readonly IFlowUiService service;IFlowNodeContext context;IFlowUiSession session;IDisposable pause;
            public UiBehavior(IFlowUiService service){this.service=service;}
            public void Enter(IFlowNodeContext c)
            {
                context=c;
                if((bool)c.Node.Properties["pauseVideo"])pause=c.PauseSourceVideo();
                session=service.Open(new FlowUiRequest(MovieUIType.Resolve((string)c.Node.Properties["uiType"]),(IReadOnlyDictionary<string,object>)c.Node.Properties["parameters"],c.ActivationId,UiBranches.Read(c.Node.Properties)))??throw new InvalidOperationException("UI 服务返回 null。");
            }
            public void Update(float delta,float real)
            {
                switch(session.Status)
                {case FlowUiStatus.Completed:
                    var id=session.SelectedBranchId;
                    var branches=UiBranches.Read(context.Node.Properties);
                    // 旧服务仅在旧配置下允许不指定结果；新分支必须显式选择。
                    object configured;
                    if (id == null && (!context.Node.Properties.TryGetValue(UiNodeSchema.BranchesProperty,out configured) || configured == null)) id="completed";
                    if (!System.Linq.Enumerable.Any(branches,b=>b.Id==id)) context.Fail("UiBranch","UI 返回了不属于本次交互的分支。");
                    else context.Complete(UiBranches.PortId(id));
                    break;case FlowUiStatus.Cancelled:context.Cancel();break;case FlowUiStatus.Failed:context.Fail("Ui",session.ErrorMessage);break;}
            }
            public void Exit(){Dispose();}
            public void Dispose()
            {
                var s=session;session=null;
                try{s?.Dispose();}finally{var p=pause;pause=null;p?.Dispose();}
            }
        }
    }
}
