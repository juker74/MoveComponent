using Game.VideoFlow;
using GameFrameX.Fsm.Runtime;

namespace Game.Framework
{
    /// <summary>
    /// 框架 FSM 的流程更新适配。先 Load/Start 驱动，再启动本状态；离开时停止流程。
    /// 视频节点间切换由驱动的独立激活管理，不依赖同一个 FsmState 类型重新进入。
    /// </summary>
    public sealed class VideoFlowRunningState : FsmState<IVideoFlowDriver>
    {
        protected override void OnUpdate(IFsm<IVideoFlowDriver> fsm,float elapseSeconds,float realElapseSeconds)
        {fsm.Owner.Tick(elapseSeconds,realElapseSeconds);}
        protected override void OnLeave(IFsm<IVideoFlowDriver> fsm,bool isShutdown)
        {fsm.Owner.Stop();}
    }
}
