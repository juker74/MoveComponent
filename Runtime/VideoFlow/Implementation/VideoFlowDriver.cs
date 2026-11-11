using System;
using System.Collections.Generic;
using GameFrameX.Movie.Runtime;
using GameFrameX.NodeComposer;

namespace Game.VideoFlow
{
    /// <summary>兼容独立宿主入口；执行状态和资源统一由未注册的 MovieManager 实例拥有。</summary>
    public sealed class VideoFlowDriver : IVideoFlowDriver
    {
        readonly MovieManager manager;
        public VideoFlowDriver(IVideoFlowLoader loader, IFlowNodeBehaviorFactory factory)
        {
            manager = new MovieManager();
            manager.SetDependencies(loader, factory);
        }
        public FlowStatus Status => manager.Status;
        public FlowPhase Phase => manager.Phase;
        public string CurrentNodeId => manager.CurrentNodeId;
        public VideoExecutionSnapshot CurrentVideo => manager.CurrentVideo;
        public IReadOnlyList<FlowActivation> ActiveNodes => manager.ActiveNodes;
        public IReadOnlyDictionary<string, object> Values => manager.Values;
        public event Action<FlowEvent> EventRaised { add { manager.EventRaised += value; } remove { manager.EventRaised -= value; } }
        public GraphResult<bool> Load(string json) { return manager.LegacyLoad(json); }
        public GraphResult<bool> Start(string entryNodeId) { return manager.LegacyStart(entryNodeId); }
        public void Tick(float deltaTime, float realDeltaTime) { manager.LegacyTick(deltaTime, realDeltaTime); }
        public void Stop() { manager.Stop(); }
        public void Dispose() { manager.LegacyDispose(); }
    }
}
