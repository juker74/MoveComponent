using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace GameFrameX.NodeComposer.Video
{
    /// <summary>视频节点配置定义；具体播放行为由业务模块实现。</summary>
    public sealed class VideoNodeExtension : IVideoNodeExtension
    {
        public INodeDefinition Definition { get; } = new VideoDefinition();

        sealed class VideoDefinition : INodeDefinition, INodePortProvider
        {
            readonly INodeDefinition definition = new NodeDefinition(
            VideoNodeSchema.TypeId, "视频播放", "影视",
            new[]
            {
                new PortDefinition(VideoNodeSchema.EnterPort, "进入", PortDirection.Input, PortKind.Control),
                new PortDefinition(VideoNodeSchema.CompletedPort, "播放完成", PortDirection.Output, PortKind.Control, maxConnections: 1),
                new PortDefinition(VideoNodeSchema.FailedPort, "播放失败", PortDirection.Output, PortKind.Control, maxConnections: 1)
            },
            new PropertyDefinition(VideoNodeSchema.SourceProperty, "视频地址", "string", "", required: true),
            new PropertyDefinition(VideoNodeSchema.VolumeProperty, "音量（0～1）", "float32", 1d),
            new PropertyDefinition(VideoNodeSchema.AssetGuidProperty, "视频资源 GUID", "string", ""),
            new PropertyDefinition(VideoNodeSchema.FramesProperty, "帧输出端口", "media.frames", new List<object>()));

            public string TypeId { get { return definition.TypeId; } }
            public string DisplayName { get { return definition.DisplayName; } }
            public string Category { get { return definition.Category; } }
            public IReadOnlyList<PortDefinition> Ports { get { return definition.Ports; } }
            public IReadOnlyList<PropertyDefinition> Properties { get { return definition.Properties; } }
            public IReadOnlyList<PortDefinition> GetPorts(NodeDocument node)
            {
                var result = Ports.ToList();
                object value;
                if (node.Properties != null && node.Properties.TryGetValue(VideoNodeSchema.FramesProperty, out value) && IsFrames(value))
                    foreach (var frame in ((IList<object>)value).Select(Convert.ToInt32).OrderBy(f => f))
                        result.Add(new PortDefinition(FramePortId(frame), "第 " + frame + " 帧", PortDirection.Output, PortKind.Control));
                return result.AsReadOnly();
            }
        }

        public static string FramePortId(int frame)
        {
            if (frame < 0) throw new ArgumentOutOfRangeException(nameof(frame));
            return "frame:" + frame.ToString(CultureInfo.InvariantCulture);
        }

        static bool IsFrames(object value)
        {
            var frames = value as IList<object>;
            if (frames == null) return false;
            var seen = new HashSet<int>();
            foreach (var frame in frames)
            {
                if (!(frame is int || frame is long || frame is float || frame is double)) return false;
                var number = Convert.ToDouble(frame);
                if (double.IsNaN(number) || number < 0 || number > int.MaxValue || Math.Truncate(number) != number || !seen.Add((int)number)) return false;
            }
            return true;
        }

        public void Register(NodeDefinitionRegistry registry)
        {
            if (registry == null) throw new ArgumentNullException(nameof(registry));
            INodeDefinition existing;
            if (registry.TryGet(VideoNodeSchema.TypeId, out existing) || registry.TryGet(UiNodeSchema.TypeId, out existing))
                throw new ArgumentException("视频或 UI 节点已注册。", nameof(registry));
            registry.RegisterDataType("media.frames", IsFrames);
            registry.RegisterDataType("media.parameters", value => value is IDictionary<string, object>);
            registry.Register(Definition);
            registry.RegisterDataType("media.branches", UiBranches.IsValid);
            registry.Register(new UiDefinition());
        }

        sealed class UiDefinition : INodeDefinition, INodePortProvider
        {
            readonly INodeDefinition definition = new NodeDefinition(UiNodeSchema.TypeId, "交互 UI", "影视",
                new[] { new PortDefinition(UiNodeSchema.EnterPort, "打开", PortDirection.Input, PortKind.Control) },
                new PropertyDefinition(UiNodeSchema.UiTypeProperty, "影视 UI 类", "string", ""),
                new PropertyDefinition(UiNodeSchema.ParametersProperty, "传入参数", "media.parameters", new Dictionary<string, object>()),
                new PropertyDefinition(UiNodeSchema.PauseVideoProperty, "打开时暂停来源视频", "bool", false),
                new PropertyDefinition(UiNodeSchema.BranchesProperty, "分支输出", "media.branches", null));
            public string TypeId => definition.TypeId;
            public string DisplayName => definition.DisplayName;
            public string Category => definition.Category;
            public IReadOnlyList<PortDefinition> Ports => definition.Ports;
            public IReadOnlyList<PropertyDefinition> Properties => definition.Properties;
            public IReadOnlyList<PortDefinition> GetPorts(NodeDocument node)
            {
                var ports = Ports.ToList();
                object value;
                var legacy = !node.Properties.TryGetValue(UiNodeSchema.BranchesProperty, out value) || value == null;
                if (!UiBranches.IsValid(value)) return ports.AsReadOnly();
                foreach (var branch in UiBranches.Read(node.Properties))
                    ports.Add(new PortDefinition(legacy ? UiNodeSchema.CompletedPort : UiBranches.PortId(branch.Id),
                        branch.Name, PortDirection.Output, PortKind.Control, maxConnections: 1));
                return ports.AsReadOnly();
            }
        }
    }
}
