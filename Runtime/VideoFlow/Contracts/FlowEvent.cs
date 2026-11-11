using System;

namespace Game.VideoFlow
{
    public enum FlowEventKind
    {
        NodeEntered, NodeUpdated, NodeExited, FlowCompleted, FlowStopped, FlowFaulted, PhaseChanged, Diagnostic
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
        public string ChapterId { get; }
        public string NodeId { get; }
        public long ActivationId { get; }
        public string InputPortId { get; }
        public string OutputPortId { get; }
        public float DeltaTime { get; }
        public float RealDeltaTime { get; }
        public string ErrorCode { get; }
        public string ErrorMessage { get; }
        /// <summary>原始异常；通过 Fail 提交的业务错误可以没有异常。</summary>
        public Exception Exception { get; }
        /// <summary>监听异常对应的原始事件类型；独立诊断为 null。</summary>
        public FlowEventKind? RelatedEventKind { get; }

        public FlowEvent(FlowEventKind kind, string graphId, string nodeId = null,
            long activationId = 0, string inputPortId = null, string outputPortId = null,
            float deltaTime = 0, float realDeltaTime = 0,
            string errorCode = null, string errorMessage = null, FlowPhase phase = FlowPhase.Idle, string chapterId = null,
            Exception exception = null, FlowEventKind? relatedEventKind = null)
        {
            Kind = kind;
            Phase = phase;
            GraphId = graphId;
            ChapterId = chapterId;
            NodeId = nodeId;
            ActivationId = activationId;
            InputPortId = inputPortId;
            OutputPortId = outputPortId;
            DeltaTime = deltaTime;
            RealDeltaTime = realDeltaTime;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            Exception = exception;
            RelatedEventKind = relatedEventKind;
        }

        public FlowEvent AsDiagnostic(string code, Exception exception)
        {
            return new FlowEvent(FlowEventKind.Diagnostic, GraphId, NodeId, ActivationId, InputPortId,
                OutputPortId, DeltaTime, RealDeltaTime, code, exception?.Message, Phase, ChapterId, exception, Kind);
        }
    }

    /// <summary>可选通知，不选择日志后端。诊断监听异常被隔离，避免递归上报。</summary>
    public static class FlowEventDispatcher
    {
        public static void Raise(Action<FlowEvent> handlers, FlowEvent evt)
        {
            if (handlers == null) return;
            var listeners = handlers.GetInvocationList();
            foreach (Action<FlowEvent> listener in listeners)
            {
                try { listener(evt); }
                catch (Exception error)
                {
                    if (evt.Kind == FlowEventKind.Diagnostic) continue;
                    var diagnostic = evt.AsDiagnostic("ObserverException", error);
                    foreach (Action<FlowEvent> observer in listeners)
                    {
                        if (observer == listener) continue;
                        try { observer(diagnostic); } catch { /* 诊断失败不再生成诊断。 */ }
                    }
                }
            }
        }
    }
}
