using System;
using System.Linq;
using GameFrameX.NodeComposer.Video;
using System.Collections.Generic;

namespace Game.VideoFlow
{
    public enum FlowUiStatus { Open, Completed, Cancelled, Failed }

    public sealed class FlowUiRequest
    {
        public Type UiType { get; }
        public IReadOnlyDictionary<string, object> Parameters { get; }
        public IReadOnlyList<UiBranch> Branches { get; }
        public long ActivationId { get; }
        public FlowUiRequest(Type uiType, IReadOnlyDictionary<string, object> parameters, long activationId, IReadOnlyList<UiBranch> branches = null)
        {
            UiType = uiType; Parameters = parameters; ActivationId = activationId;
            var items = branches == null ? new[] { new UiBranch("completed", "交互完成") } : branches.ToArray();
            if (items.Any(b => b == null) || items.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("UI 分支不能为空或重复。", nameof(branches));
            Branches = Array.AsReadOnly(items);
        }
    }

    /// <summary>由业务 UI 系统实现。每次 Open 返回独立会话；仅正常交互设置 Completed。</summary>
    public interface IFlowUiService { IFlowUiSession Open(FlowUiRequest request); }
    public interface IFlowUiSession : IDisposable
    {
        FlowUiStatus Status { get; }
        string SelectedBranchId { get; }
        string ErrorMessage { get; }
    }
}
