namespace Game.VideoFlow
{
    public enum FlowEventKind
    {
        NodeEntered, NodeUpdated, NodeExited, FlowCompleted, FlowStopped, FlowFaulted, PhaseChanged
    }

    /// <summary>
    /// 不可变生命周期通知。ActivationId 标识一次进入；不适用字段为 null 或零。
    /// 进入通知先于 Enter；更新通知晚于 Update；退出通知晚于 Exit/Dispose。
    /// </summary>
    public sealed class FlowEvent
    {
        public FlowEventKind Kind { get; }
        public FlowPhase Phase { get; }
        public string GraphId { get; }
        public string NodeId { get; }
        public long ActivationId { get; }
        public string InputPortId { get; }
        public string OutputPortId { get; }
        public float DeltaTime { get; }
        public float RealDeltaTime { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }

        public FlowEvent(FlowEventKind kind, string graphId, string nodeId = null,
            long activationId = 0, string inputPortId = null, string outputPortId = null,
            float deltaTime = 0, float realDeltaTime = 0,
            string errorCode = null, string errorMessage = null, FlowPhase phase = FlowPhase.Idle)
        {
            Kind = kind;
            Phase = phase;
            GraphId = graphId;
            NodeId = nodeId;
            ActivationId = activationId;
            InputPortId = inputPortId;
            OutputPortId = outputPortId;
            DeltaTime = deltaTime;
            RealDeltaTime = realDeltaTime;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }
    }
}
