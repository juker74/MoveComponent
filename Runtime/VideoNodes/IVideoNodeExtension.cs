namespace GameFrameX.NodeComposer.Video
{
    /// <summary>视频节点的稳定交换协议标识，编辑器和业务运行时共同使用。</summary>
    public static class VideoNodeSchema
    {
        public const string TypeId = "media.video";
        public const string EnterPort = "enter";
        public const string CompletedPort = "completed";
        public const string FailedPort = "failed";
        public const string SourceProperty = "source";
        public const string VolumeProperty = "volume";
        public const string FramesProperty = "frames";
        public const string AssetGuidProperty = "assetGuid";
    }

    public static class UiNodeSchema
    {
        public const string TypeId = "media.ui";
        public const string EnterPort = "enter";
        public const string CompletedPort = "completed";
        public const string BranchesProperty = "branches";
        public const string UiTypeProperty = "uiType";
        public const string ParametersProperty = "parameters";
        public const string PauseVideoProperty = "pauseVideo";
    }

    /// <summary>工具的视频扩展入口，不负责视频播放或业务执行。</summary>
    public interface IVideoNodeExtension
    {
        /// <summary>视频定义：source/string、volume/float32；enter/completed/failed 控制端口。</summary>
        INodeDefinition Definition { get; }

        /// <summary>
        /// 向编辑器或运行时注册表注册同一份定义。每个注册表只初始化一次，重复注册报错。
        /// enter 不限入边；completed、failed 各最多一条出边。
        /// </summary>
        void Register(NodeDefinitionRegistry registry);
    }
}
