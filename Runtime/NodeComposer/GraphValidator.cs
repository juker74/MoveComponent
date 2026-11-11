using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFrameX.NodeComposer
{
    public sealed class GraphValidator
    {
        readonly NodeDefinitionRegistry registry;
        public GraphValidator(NodeDefinitionRegistry registry) { this.registry = registry ?? throw new ArgumentNullException(nameof(registry)); }
        public IReadOnlyList<GraphIssue> Validate(GraphDocument graph)
        {
            var issues = new List<GraphIssue>();
            if (graph == null) { issues.Add(new GraphIssue("Document", "图不能为空。")); return issues; }
            if (graph.SchemaVersion != 1) issues.Add(new GraphIssue("SchemaVersion", "仅支持 schemaVersion=1。"));
            if (string.IsNullOrWhiteSpace(graph.Id) || graph.Name == null) issues.Add(new GraphIssue("Document", "图 ID 或名称无效。"));
            if (graph.Nodes == null || graph.Edges == null) { issues.Add(new GraphIssue("Document", "nodes 和 edges 必须是数组。")); return issues; }
            var nodes = new Dictionary<string, NodeDocument>(StringComparer.Ordinal);
            foreach (var n in graph.Nodes)
            {
                if (n == null || string.IsNullOrWhiteSpace(n.Id) || nodes.ContainsKey(n.Id)) { issues.Add(new GraphIssue("NodeId", "节点 ID 为空或重复。")); continue; }
                nodes.Add(n.Id, n);
                if (n.Title == null || float.IsNaN(n.X) || float.IsInfinity(n.X) || float.IsNaN(n.Y) || float.IsInfinity(n.Y)) issues.Add(new GraphIssue("Position", "节点标题或坐标无效。", n.Id));
                INodeDefinition definition;
                if (!registry.TryGet(n.TypeId, out definition)) { issues.Add(new GraphIssue("UnknownType", "未知节点类型: " + n.TypeId, n.Id)); continue; }
                if (n.Properties == null) { issues.Add(new GraphIssue("Properties", "properties 必须是对象。", n.Id)); continue; }
                foreach (var pair in n.Properties)
                {
                    var property = definition.Properties.FirstOrDefault(p => p.Key == pair.Key);
                    if (property == null) issues.Add(new GraphIssue("UnknownProperty", "未知属性: " + pair.Key, n.Id, propertyPath: pair.Key));
                    else if (!registry.Accepts(property.DataTypeId, pair.Value)) issues.Add(new GraphIssue("PropertyType", "属性类型不匹配: " + pair.Key, n.Id, propertyPath: pair.Key));
                }
                foreach (var p in definition.Properties)
                    if (p.Required && !n.Properties.ContainsKey(p.Key)) issues.Add(new GraphIssue("RequiredProperty", "缺少属性: " + p.Key, n.Id, propertyPath: p.Key));
            }
            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            var pairs = new HashSet<Tuple<string, string, string, string>>();
            var orders = new HashSet<Tuple<string, string, int>>();
            var counts = new Dictionary<Tuple<string, string>, int>();
            foreach (var e in graph.Edges)
            {
                if (e == null) { issues.Add(new GraphIssue("Edge", "连线不能为空。")); continue; }
                Action<string, string> error = (code, message) => issues.Add(new GraphIssue(code, message, edgeId: e.Id));
                if (string.IsNullOrWhiteSpace(e.Id) || !edgeIds.Add(e.Id)) error("EdgeId", "连线 ID 为空或重复。");
                if (e.SourceNodeId == e.TargetNodeId) error("SelfLoop", "不允许节点连接自身。");
                if (!pairs.Add(Tuple.Create(e.SourceNodeId, e.SourcePortId, e.TargetNodeId, e.TargetPortId))) error("DuplicateEdge", "同一对端口已有连线。");
                if (e.Order < 0 || !orders.Add(Tuple.Create(e.SourceNodeId, e.SourcePortId, e.Order))) error("EdgeOrder", "同一源端口下 order 重复或为负数。");
                var source = FindPort(nodes, e.SourceNodeId, e.SourcePortId);
                var target = FindPort(nodes, e.TargetNodeId, e.TargetPortId);
                if (source == null || target == null) { error("UnknownPort", "源或目标节点／端口不存在。"); continue; }
                if (source.Direction != PortDirection.Output || target.Direction != PortDirection.Input) error("PortDirection", "必须从输出连接到输入。");
                if (source.Kind != target.Kind) error("PortKind", "数据端口与控制端口不可互连。");
                else if (source.Kind == PortKind.Data && source.DataTypeId != target.DataTypeId) error("PortType", "端口数据类型不匹配。");
                foreach (var endpoint in new[] { Tuple.Create(e.SourceNodeId, source), Tuple.Create(e.TargetNodeId, target) })
                {
                    var key = Tuple.Create(endpoint.Item1, endpoint.Item2.Id);
                    int count; counts.TryGetValue(key, out count); counts[key] = ++count;
                    if (endpoint.Item2.MaxConnections >= 0 && count > endpoint.Item2.MaxConnections) error("PortCapacity", "端口连接数量超过上限: " + endpoint.Item2.DisplayName);
                }
            }
            return issues;
        }
        PortDefinition FindPort(Dictionary<string, NodeDocument> nodes, string nodeId, string portId)
        {
            NodeDocument node; INodeDefinition definition;
            return nodeId != null && nodes.TryGetValue(nodeId, out node) && registry.TryGet(node.TypeId, out definition) ? registry.GetPorts(node).FirstOrDefault(p => p.Id == portId) : null;
        }
    }
}
