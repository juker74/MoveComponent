using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameFrameX.NodeComposer.Video;
using GameFrameX.UI.UGUI.Runtime;

namespace Game.Framework.UI
{
    /// <summary>影视交互 UI 基类。打开后由业务桥绑定一次交互，具体 UI 只负责选择分支。</summary>
    public abstract class MovieUIForm : UGUI
    {
        static readonly IReadOnlyList<UiBranch> emptyBranches = Array.AsReadOnly(new UiBranch[0]);
        static readonly IReadOnlyDictionary<string, object> emptyParameters = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());
        Interaction interaction;
        protected IReadOnlyList<UiBranch> Branches => interaction?.Branches ?? emptyBranches;
        protected IReadOnlyDictionary<string, object> Parameters => interaction?.Parameters ?? emptyParameters;

        /// <summary>保留 UGUI 原有 OnOpen；桥在取得实际 UI 后绑定，并调用 OnInteractionBound 构建交互内容。</summary>
        public IDisposable BindInteraction(IReadOnlyList<UiBranch> branches, IReadOnlyDictionary<string, object> parameters,
            Func<UiBranch, bool> finish, Func<bool> cancel)
        {
            if (interaction != null) throw new InvalidOperationException("该 UI 仍绑定着另一次交互。");
            if (branches == null || finish == null || cancel == null) throw new ArgumentNullException();
            var items = branches.ToArray();
            if (items.Any(b => b == null) || items.Select(b => b.Id).Distinct(StringComparer.Ordinal).Count() != items.Length)
                throw new ArgumentException("分支集合无效。", nameof(branches));
            var binding = new Interaction(this, Array.AsReadOnly(items), parameters ?? emptyParameters, finish, cancel);
            interaction = binding;
            try { OnInteractionBound(); return binding; }
            catch { binding.Dispose(); throw; }
        }
        protected virtual void OnInteractionBound() { }
        protected bool TryFinish(UiBranch branch) { return interaction != null && interaction.Finish(branch); }
        protected bool TryCancel() { return interaction != null && interaction.Cancel(); }

        /// <summary>按钮、计时器和异步回调应捕获此函数；旧绑定关闭后调用始终返回 false。</summary>
        protected Func<bool> CaptureFinish(UiBranch branch)
        {
            var binding = interaction;
            return () => binding != null && binding.Finish(branch);
        }
        public override void OnClose(bool isShutdown, object userData)
        {
            try { TryCancel(); }
            finally { interaction?.Dispose(); base.OnClose(isShutdown, userData); }
        }
        public override void Dispose()
        {
            try { TryCancel(); }
            finally { interaction?.Dispose(); base.Dispose(); }
        }
        public override void OnRecycle()
        {
            try { TryCancel(); }
            finally { interaction?.Dispose(); base.OnRecycle(); }
        }
        sealed class Interaction : IDisposable
        {
            MovieUIForm owner;
            Func<UiBranch, bool> finish;
            Func<bool> cancel;
            bool submitted, submitting;
            public IReadOnlyList<UiBranch> Branches { get; }
            public IReadOnlyDictionary<string, object> Parameters { get; }
            public Interaction(MovieUIForm owner, IReadOnlyList<UiBranch> branches, IReadOnlyDictionary<string, object> parameters,
                Func<UiBranch, bool> finish, Func<bool> cancel)
            { this.owner=owner; Branches=branches; Parameters=parameters; this.finish=finish; this.cancel=cancel; }
            public bool Finish(UiBranch branch)
            {
                if (owner == null || submitted || submitting || branch == null || !Branches.Any(b => ReferenceEquals(b, branch))) return false;
                submitting = true;
                try { var accepted=finish(branch); if (accepted) submitted=true; return accepted; }
                finally { submitting=false; }
            }
            public bool Cancel()
            {
                if (owner == null || submitted || submitting) return false;
                submitting=true;
                try { var accepted=cancel(); if (accepted) submitted=true; return accepted; }
                finally { submitting=false; }
            }
            public void Dispose()
            {
                if (owner != null && ReferenceEquals(owner.interaction, this)) owner.interaction=null;
                owner=null; finish=null; cancel=null; submitted=true;
            }
        }
    }
}
