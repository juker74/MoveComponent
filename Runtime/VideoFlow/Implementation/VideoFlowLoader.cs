using System;
using Game.Framework.UI;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GameFrameX.NodeComposer;
using GameFrameX.NodeComposer.Video;

namespace Game.VideoFlow
{
    public sealed class VideoFlowLoader : IVideoFlowLoader
    {
        readonly NodeDefinitionRegistry registry;
        readonly IGraphJsonCodec codec;
        public VideoFlowLoader() : this(CreateRegistry()) { }
        public VideoFlowLoader(NodeDefinitionRegistry registry)
        { this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); codec = new GraphJsonCodec(registry); }
        static NodeDefinitionRegistry CreateRegistry()
        { var r = NodeDefinitionRegistry.CreateDefault(); new VideoNodeExtension().Register(r); return r; }

        public GraphResult<IVideoFlowGraph> Load(string json)
        {
            var decoded = codec.Decode(json);
            if (!decoded.Success) return GraphResult<IVideoFlowGraph>.Fail(decoded.Issues);
            var graph = decoded.Value;
            var issues = new List<GraphIssue>();
            foreach (var n in graph.Nodes)
            {
                INodeDefinition definition; registry.TryGet(n.TypeId, out definition);
                foreach (var p in definition.Properties)
                    if (!n.Properties.ContainsKey(p.Key)) n.Properties[p.Key] = p.DefaultValue;
                if (n.TypeId == VideoNodeSchema.TypeId)
                {
                    if (string.IsNullOrWhiteSpace((string)n.Properties["source"])) issues.Add(new GraphIssue("VideoSource", "视频地址不能为空。", n.Id));
                    var volume = Convert.ToDouble(n.Properties["volume"]);
                    if (volume < 0 || volume > 1) issues.Add(new GraphIssue("Volume", "音量必须在 0 到 1 之间。", n.Id));
                }
                if (n.TypeId == UiNodeSchema.TypeId && MovieUIType.Resolve((string)n.Properties[UiNodeSchema.UiTypeProperty]) == null)
                    issues.Add(new GraphIssue("UiType", "请选择有效的 MovieUIForm 非抽象子类；该 UI 的程序集必须已加载。", n.Id));
            }
            ValidateValues(graph, issues);
            foreach (var e in graph.Edges)
            {
                var source = graph.Nodes.First(n => n.Id == e.SourceNodeId);
                var target = graph.Nodes.First(n => n.Id == e.TargetNodeId);
                if (registry.GetPorts(source).First(p => p.Id == e.SourcePortId).Kind != PortKind.Control) continue;
                if (source.TypeId == VideoNodeSchema.TypeId && e.SourcePortId.StartsWith("frame:", StringComparison.Ordinal) && target.TypeId == VideoNodeSchema.TypeId)
                    issues.Add(new GraphIssue("FrameTarget", "视频帧请先连接 UI 或逻辑节点，再由结果切换视频。", edgeId:e.Id));
                if (!(source.TypeId == VideoNodeSchema.TypeId && e.SourcePortId.StartsWith("frame:", StringComparison.Ordinal)) && graph.Edges.Count(x=>x.SourceNodeId==e.SourceNodeId && x.SourcePortId==e.SourcePortId)>1)
                    issues.Add(new GraphIssue("AmbiguousRoute", "终结端口最多连接一个后继。", edgeId:e.Id));
            }
            return issues.Count == 0 ? GraphResult<IVideoFlowGraph>.Ok(new FlowGraph(graph, registry)) : GraphResult<IVideoFlowGraph>.Fail(issues);
        }

        void ValidateValues(GraphDocument graph,List<GraphIssue> issues)
        {
            foreach(var n in graph.Nodes)
            {
                INodeDefinition raw;registry.TryGet(n.TypeId,out raw);
                var definition=raw as ValueNodeDefinition;
                if(definition==null)continue;
                ValueTypeDefinition type;
                if(!registry.TryGetValueType(definition.DataTypeId,out type))
                { issues.Add(new GraphIssue("ValueType","缺少运行时值类型适配器。",n.Id));continue; }
                if(definition.Kind==ValueNodeKind.Cell)
                {
                    try { type.Normalize(Freeze(n.Properties["value"])); }
                    catch(Exception e) { issues.Add(new GraphIssue("InitialValue",e.Message,n.Id)); }
                }
                else
                {
                    var operation=(string)n.Properties["operation"];
                    if(!(definition.Kind==ValueNodeKind.Modify ? type.Modifications : type.Comparisons).Contains(operation))
                        issues.Add(new GraphIssue("ValueOperation","当前类型不支持运算: "+operation,n.Id));
                    if(definition.Kind==ValueNodeKind.Modify)
                    {
                        var target=graph.Nodes.FirstOrDefault(x=>x.Id==(string)n.Properties["target"]);
                        INodeDefinition targetRaw=null;
                        if(target!=null)registry.TryGet(target.TypeId,out targetRaw);
                        var cell=targetRaw as ValueNodeDefinition;
                        if(cell==null || cell.Kind!=ValueNodeKind.Cell || cell.DataTypeId!=definition.DataTypeId)
                            issues.Add(new GraphIssue("ValueTarget","目标必须是相同类型的数据节点。",n.Id));
                    }
                    foreach(var input in definition.Ports.Where(p=>p.Kind==PortKind.Data && p.Direction==PortDirection.Input))
                        if(!graph.Edges.Any(e=>e.TargetNodeId==n.Id && e.TargetPortId==input.Id))
                            issues.Add(new GraphIssue("ValueInput","请连接数据输入: "+input.Id,n.Id));
                }
            }
            foreach(var e in graph.Edges)
            {
                var source=graph.Nodes.First(n=>n.Id==e.SourceNodeId);
                var port=registry.GetPorts(source).First(p=>p.Id==e.SourcePortId);
                if(port.Kind!=PortKind.Data)continue;
                INodeDefinition sourceRaw;
                registry.TryGet(source.TypeId,out sourceRaw);
                var cell=sourceRaw as ValueNodeDefinition;
                if(cell==null || cell.Kind!=ValueNodeKind.Cell || port.Id!="valueOut")
                    issues.Add(new GraphIssue("ValueSource","数据连接必须从数据节点的当前值端口输出。",edgeId:e.Id));
            }
        }

        sealed class Node : IFlowNode
        {
            public string Id { get; }
            public string TypeId { get; }
            public string Title { get; }
            public IReadOnlyDictionary<string, object> Properties { get; }
            public Node(NodeDocument n) { Id=n.Id; TypeId=n.TypeId; Title=n.Title; Properties=(IReadOnlyDictionary<string,object>)Freeze(n.Properties); }
        }
        static object Freeze(object value)
        {
            var map=value as IDictionary<string,object>;
            if(map!=null) return new ReadOnlyDictionary<string,object>(map.ToDictionary(p=>p.Key,p=>Freeze(p.Value),StringComparer.Ordinal));
            var list=value as IList;
            if(list!=null) return Array.AsReadOnly(list.Cast<object>().Select(Freeze).ToArray());
            return value;
        }
        sealed class Edge : IFlowEdge
        {
            public string Id { get; } public string SourceNodeId { get; } public string SourcePortId { get; }
            public string TargetNodeId { get; } public string TargetPortId { get; } public int Order { get; }
            public Edge(EdgeDocument e) {Id=e.Id;SourceNodeId=e.SourceNodeId;SourcePortId=e.SourcePortId;TargetNodeId=e.TargetNodeId;TargetPortId=e.TargetPortId;Order=e.Order;}
        }
        internal sealed class FlowGraph : IVideoFlowGraph, IFlowValueGraph
        {
            readonly Dictionary<Tuple<string,string>,IReadOnlyList<IFlowEdge>> outgoing = new Dictionary<Tuple<string,string>,IReadOnlyList<IFlowEdge>>();
            readonly Dictionary<string,ValueNodeDefinition> valueNodes=new Dictionary<string,ValueNodeDefinition>();
            readonly Dictionary<string,ValueTypeDefinition> valueTypes;
            readonly Dictionary<Tuple<string,string>,PortDefinition> outputs=new Dictionary<Tuple<string,string>,PortDefinition>();
            public ValueNodeDefinition GetValueNode(string nodeId) { ValueNodeDefinition d;return valueNodes.TryGetValue(nodeId,out d)?d:null; }
            public ValueTypeDefinition GetValueType(string id) { return valueTypes[id]; }
            public PortDefinition GetOutput(string nodeId,string portId) { PortDefinition p;return outputs.TryGetValue(Tuple.Create(nodeId,portId),out p)?p:null; }
            public string Id { get; } public IReadOnlyDictionary<string,IFlowNode> Nodes { get; } public IReadOnlyList<IFlowEdge> Edges { get; }
            public FlowGraph(GraphDocument graph, NodeDefinitionRegistry registry)
            {
                valueTypes=registry.ValueTypes.ToDictionary(t=>t.Id,StringComparer.Ordinal);
                foreach(var n in graph.Nodes)
                {
                    INodeDefinition d;registry.TryGet(n.TypeId,out d);
                    if(d is ValueNodeDefinition)valueNodes.Add(n.Id,(ValueNodeDefinition)d);
                    foreach(var p in registry.GetPorts(n).Where(p=>p.Direction==PortDirection.Output)) outputs.Add(Tuple.Create(n.Id,p.Id),p);
                }
                Id=graph.Id; Nodes=new ReadOnlyDictionary<string,IFlowNode>(graph.Nodes.ToDictionary(n=>n.Id,n=>(IFlowNode)new Node(n),StringComparer.Ordinal));
                Edges=Array.AsReadOnly(graph.Edges.Select(e=>(IFlowEdge)new Edge(e)).ToArray());
                foreach(var n in graph.Nodes)
                    foreach(var p in registry.GetPorts(n).Where(p=>p.Direction==PortDirection.Output))
                        outgoing.Add(Tuple.Create(n.Id,p.Id),Array.AsReadOnly(Edges.Where(e=>e.SourceNodeId==n.Id && e.SourcePortId==p.Id).OrderBy(e=>e.Order).ToArray()));
            }
            public IReadOnlyList<IFlowEdge> GetOutgoing(string nodeId,string portId)
            {
                IReadOnlyList<IFlowEdge> edges;
                if(!outgoing.TryGetValue(Tuple.Create(nodeId,portId),out edges)) throw new ArgumentException("节点或输出端口不存在。");
                return edges;
            }
        }
    }
}
