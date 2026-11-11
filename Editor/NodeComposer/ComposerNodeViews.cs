using System;
using System.Collections.Generic;

namespace GameFrameX.NodeComposer.Editor
{
    /// <summary>按类型扩展画布与属性面板，核心编辑器不依赖具体业务或媒体类型。</summary>
    public interface IComposerNodeView
    {
        void DrawNode(ComposerNode node);
        /// <returns>true 表示已绘制该类型的属性，跳过默认属性编辑器。</returns>
        bool DrawInspector(ComposerNode node);
    }

    public static class ComposerNodeViews
    {
        static readonly Dictionary<string, IComposerNodeView> views = new Dictionary<string, IComposerNodeView>(StringComparer.Ordinal);
        public static void Register(string typeId, IComposerNodeView view)
        {
            if (string.IsNullOrWhiteSpace(typeId) || view == null) throw new ArgumentException("节点视图类型和实例不可为空。");
            views.Add(typeId, view);
        }
        public static IComposerNodeView Find(string typeId)
        {
            IComposerNodeView view;
            return typeId != null && views.TryGetValue(typeId, out view) ? view : null;
        }
    }
}
