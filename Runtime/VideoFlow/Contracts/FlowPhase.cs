using System;
using System.Collections.Generic;
using System.Linq;

namespace Game.VideoFlow
{
    /// <summary>业务主流程阶段。图节点类型不等于同级状态；UI 打开不会替换 PlayingVideo。</summary>
    public enum FlowPhase
    {
        Idle, PreparingVideo, PlayingVideo, SwitchingVideo, Interacting, Completed, Stopped, Faulted
    }

    /// <summary>当前视频执行上下文的不可变快照；暂停持有和 UI 归属绑定一次视频激活。</summary>
    public sealed class VideoExecutionSnapshot
    {
        public string NodeId { get; }
        public long ActivationId { get; }
        public IReadOnlyList<long> UiActivationIds { get; }
        public int PauseCount { get; }
        public VideoExecutionSnapshot(string nodeId, long activationId, IEnumerable<long> uiActivationIds, int pauseCount)
        {
            NodeId = nodeId;
            ActivationId = activationId;
            UiActivationIds = Array.AsReadOnly(uiActivationIds.ToArray());
            PauseCount = pauseCount;
        }
    }
}
