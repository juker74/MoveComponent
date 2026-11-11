using System.Collections.Generic;
using GameFrameX.NodeComposer;

namespace Game.VideoFlow
{
    /// <summary>严格解析协议并校验业务规则；失败不影响正在运行的图。</summary>
    public interface IVideoFlowLoader
    {
        GraphResult<IVideoFlowGraph> Load(string json);
    }

    /// <summary>加载后不可变的有向图；按节点和输出端口保留完整连线，支持环路。</summary>
    public interface IVideoFlowGraph
    {
        string Id { get; }
        IReadOnlyDictionary<string, IFlowNode> Nodes { get; }
        IReadOnlyList<IFlowEdge> Edges { get; }

        /// <summary>合法端口无连接时返回空集合；节点或输出端口不存在时抛 ArgumentException。</summary>
        IReadOnlyList<IFlowEdge> GetOutgoing(string nodeId, string outputPortId);
    }

    /// <summary>带数据语义的图快照；类型与节点定义在加载时固定。</summary>
    public interface IFlowValueGraph
    {
        ValueNodeDefinition GetValueNode(string nodeId);
        ValueTypeDefinition GetValueType(string typeId);
        PortDefinition GetOutput(string nodeId, string portId);
    }

    /// <summary>节点只读快照；嵌套属性值也必须复制并冻结，不暴露原始 DTO 的可变容器。</summary>
    public interface IFlowNode
    {
        string Id { get; }
        string TypeId { get; }
        string Title { get; }
        IReadOnlyDictionary<string, object> Properties { get; }
    }

    public interface IFlowEdge
    {
        string Id { get; }
        string SourceNodeId { get; }
        string SourcePortId { get; }
        string TargetNodeId { get; }
        string TargetPortId { get; }
        int Order { get; }
    }
}
