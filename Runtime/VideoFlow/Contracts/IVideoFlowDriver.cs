using System;
using System.Collections.Generic;
using GameFrameX.NodeComposer;

namespace Game.VideoFlow
{
    public enum FlowStatus { Empty, Ready, Running, Completed, Stopped, Faulted }

    /// <summary>主线程调用的图流程驱动；视频与多个 UI 激活可并存。实现位于 Game，不依赖 Editor。</summary>
    public interface IVideoFlowDriver : IDisposable
    {
        FlowStatus Status { get; }
        FlowPhase Phase { get; }
        /// <summary>当前主视频上下文快照；UI 是该上下文的附属交互，不是主状态。</summary>
        VideoExecutionSnapshot CurrentVideo { get; }
        /// <summary>没有活动节点时为 null。</summary>
        string CurrentNodeId { get; }
        IReadOnlyList<FlowActivation> ActiveNodes { get; }
        /// <summary>当前运行值快照，以数据节点 ID 为键。Start 重置，图内循环保留。</summary>
        IReadOnlyDictionary<string, object> Values { get; }
        event Action<FlowEvent> EventRaised;

        /// <summary>Running 时拒绝加载；失败保留原图；成功进入 Ready。</summary>
        GraphResult<bool> Load(string json);
        /// <summary>显式入口，不依据入度猜测。仅 Ready/Completed/Stopped 可启动。</summary>
        GraphResult<bool> Start(string entryNodeId);
        /// <summary>
        /// 更新活动行为并发布 NodeUpdated，仅消费本帧开始时的请求，避免环路递归。
        /// 非 Running 时无操作；时间必须非负且有限；框架和宿主不能重复调用。
        /// </summary>
        void Tick(float deltaTime, float realDeltaTime);
        /// <summary>幂等退出并清理活动行为；事件回调中调用延迟执行，优先于完成请求。</summary>
        void Stop();
    }

    public enum FlowActivationState { Entering, Active, Exiting, Exited }

    public sealed class FlowActivation
    {
        public long ActivationId { get; }
        public string NodeId { get; }
        public FlowActivationState State { get; }
        public long? SourceVideoActivationId { get; }
        public FlowActivation(long id, string nodeId, FlowActivationState state, long? sourceVideoActivationId = null)
        { ActivationId = id; NodeId = nodeId; State = state; SourceVideoActivationId = sourceVideoActivationId; }
    }

    public interface IFlowNodeBehaviorFactory
    {
        bool Supports(string typeId);
        /// <summary>每次进入创建独立实例；未知类型抛 ArgumentException，不返回 null。</summary>
        IFlowNodeBehavior Create(IFlowNode node);
    }

    /// <summary>节点行为。Exit 清理部分进入的节点，Dispose 幂等释放资源。</summary>
    public interface IFlowNodeBehavior : IDisposable
    {
        void Enter(IFlowNodeContext context);
        void Update(float deltaTime, float realDeltaTime);
        void Exit();
    }

    /// <summary>绑定一次进入实例。退出后所有终结请求无效，避免迟到回调影响新节点。</summary>
    public interface IFlowNodeContext
    {
        IFlowNode Node { get; }
        /// <summary>初始入口为 null，经连线进入时为目标输入端口 ID。</summary>
        string InputPortId { get; }
        long ActivationId { get; }
        /// <summary>读取当前节点的数据输入。未连接返回 false；退出后不可读取或写入。</summary>
        bool TryReadInput(string inputPortId, out object value);
        object ReadValue(string dataNodeId);
        bool WriteValue(string dataNodeId, object value);
        bool HasOutputConnection(string outputPortId);
        /// <summary>发布非终结端口（例如视频帧），不退出当前节点；下一 Tick 执行。</summary>
        bool Emit(string outputPortId);
        /// <summary>取消本次激活，不走完成连线。</summary>
        bool Cancel();
        IVideoPlaybackSession SourceVideo { get; }
        /// <summary>视频行为绑定自己的播放会话；每次激活最多一次。</summary>
        void BindVideo(IVideoPlaybackSession session);
        /// <summary>引用计数暂停来源视频；释放租约后，仅最后一个持有者恢复。</summary>
        IDisposable PauseSourceVideo();
        /// <summary>
        /// 首次请求返回 true，只登记结果，不递归切换；重复或过期请求返回 false。
        /// 合法输出无后继时退出当前激活；无其他激活时流程结束。未知端口作为运行错误处理。
        /// </summary>
        bool Complete(string outputPortId);
        /// <summary>登记流程故障。首次请求有效；重复或过期请求返回 false。</summary>
        bool Fail(string code, string message);
    }
}
