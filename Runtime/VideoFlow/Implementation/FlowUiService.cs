using System;
using Game.Framework.UI;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.VideoFlow
{
    /// <summary>UI 接入桥。Opened 创建 UI 后调用 Bind；Closed 关闭实例。绑定使用会话身份防止旧回调误操作新会话。</summary>
    public sealed class FlowUiService : MonoBehaviour, IFlowUiService
    {
        readonly Dictionary<long, Session> sessions = new Dictionary<long, Session>();
        public event Action<FlowUiRequest> Opened;
        public event Action<long> Closed;
        public IFlowUiSession Open(FlowUiRequest request)
        {
            if (request == null || !MovieUIType.IsSelectable(request.UiType)) throw new ArgumentException("UI 请求无效。");
            var session = new Session(this, request);
            sessions.Add(request.ActivationId, session);
            try { Opened?.Invoke(request); return session; }
            catch { session.Dispose(); throw; }
        }
        /// <summary>异步打开时请使用此重载：旧请求即使 ActivationId 被复用，也无法绑定新会话。</summary>
        public bool Bind(FlowUiRequest request, MovieUIForm form)
        {
            Session session;
            return request != null && sessions.TryGetValue(request.ActivationId, out session)
                && ReferenceEquals(session.Request, request) && BindSession(session, form);
        }
        public bool Bind(long activationId, MovieUIForm form)
        {
            Session session;
            return sessions.TryGetValue(activationId, out session) && BindSession(session, form);
        }
        bool BindSession(Session session, MovieUIForm form)
        {
            if (form == null || session.Status != FlowUiStatus.Open || session.Binding != null || session.BindingInProgress
                || form.GetType() != session.Request.UiType) return false;
            session.BindingInProgress = true;
            try
            {
                var binding = form.BindInteraction(session.Request.Branches, session.Request.Parameters,
                    branch => CompleteSession(session, branch.Id), () => Finish(session, FlowUiStatus.Cancelled, null));
                // UI 可在 OnInteractionBound 内立即完成；也可能被外部同步停止。
                if (session.Owner != this) { binding.Dispose(); return false; }
                session.Binding = binding;
                return true;
            }
            finally { session.BindingInProgress = false; }
        }
        public bool Complete(long activationId, string branchId)
        {
            Session session;
            return sessions.TryGetValue(activationId, out session) && CompleteSession(session, branchId);
        }
        // 兼容旧 completed 分支，配置了其他分支时必须显式选择。
        public bool Complete(long activationId) { return Complete(activationId, "completed"); }
        bool CompleteSession(Session session, string branchId)
        {
            if (!session.Request.Branches.Any(b => b.Id == branchId)) return false;
            if (!Finish(session, FlowUiStatus.Completed, null)) return false;
            session.SelectedBranchId = branchId;
            return true;
        }
        public bool Cancel(long activationId) { Session s; return sessions.TryGetValue(activationId, out s) && Finish(s, FlowUiStatus.Cancelled, null); }
        public bool Fail(long activationId, string error) { Session s; return sessions.TryGetValue(activationId, out s) && Finish(s, FlowUiStatus.Failed, error); }
        bool Finish(Session session, FlowUiStatus status, string error)
        {
            if (session.Owner != this || session.Status != FlowUiStatus.Open) return false;
            session.Status=status; session.ErrorMessage=error; return true;
        }
        void OnDisable()
        {
            foreach (var session in sessions.Values.ToArray())
                try { session.Dispose(); } catch (Exception e) { Debug.LogException(e, this); }
        }
        sealed class Session : IFlowUiSession
        {
            public FlowUiService Owner;
            public readonly FlowUiRequest Request;
            public IDisposable Binding;
            public bool BindingInProgress;
            public FlowUiStatus Status { get; set; }
            public string SelectedBranchId { get; set; }
            public string ErrorMessage { get; set; }
            public Session(FlowUiService owner, FlowUiRequest request) { Owner=owner; Request=request; }
            public void Dispose()
            {
                var service=Owner; if (service==null) return; Owner=null;
                if (Status==FlowUiStatus.Open) Status=FlowUiStatus.Cancelled;
                Binding?.Dispose(); Binding=null;
                service.sessions.Remove(Request.ActivationId);
                service.Closed?.Invoke(Request.ActivationId);
            }
        }
    }
}
