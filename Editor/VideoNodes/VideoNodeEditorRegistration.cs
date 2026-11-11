using GameFrameX.NodeComposer.Editor;
using UnityEditor;

namespace GameFrameX.NodeComposer.Video.Editor
{
    /// <summary>编辑器域加载时注册，运行时模块不依赖本程序集。</summary>
    [InitializeOnLoad]
    public static class VideoNodeEditorRegistration
    {
        static VideoNodeEditorRegistration()
        {
            new VideoNodeExtension().Register(ComposerGraph.Registry);
            ComposerNodeViews.Register(VideoNodeSchema.TypeId, new VideoNodeView());
            ComposerNodeViews.Register(UiNodeSchema.TypeId, new UiNodeView());
        }
    }
}
