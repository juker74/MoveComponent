using System;
using System.Linq;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    public sealed class ComposerGraphAdapter
    {
        public GraphResult<GraphDocument> Capture(ComposerGraph graph)
        {
            if (graph == null) return GraphResult<GraphDocument>.Fail("Graph", "图不能为空。");
            var document = new GraphDocument { Id = graph.DocumentId, Name = graph.DocumentName };
            foreach (var node in graph.allNodes)
            {
                var composer = node as ComposerNode;
                if (composer == null) return GraphResult<GraphDocument>.Fail("Node", "图包含非配置节点。");
                var data = composer.Data;
                document.Nodes.Add(new NodeDocument { Id = data.Id, TypeId = data.TypeId, Title = data.Title, X = node.position.x, Y = node.position.y, Properties = data.Properties });
                foreach (var edge in node.outConnections)
                {
                    var connection = edge as ComposerConnection; var target = edge.targetNode as ComposerNode;
                    if (connection == null || target == null) return GraphResult<GraphDocument>.Fail("Edge", "图包含非配置连线。");
                    if (!connection.isActive) return GraphResult<GraphDocument>.Fail("DisabledEdge", "配置协议不支持禁用连线，请删除或重新启用。");
                    document.Edges.Add(new EdgeDocument { Id = connection.DocumentId, SourceNodeId = data.Id, SourcePortId = connection.SourcePortId, TargetNodeId = target.DocumentId, TargetPortId = connection.TargetPortId, Order = connection.Order });
                }
            }
            // 编码回读同时验证并深拷贝属性，返回值可独立修改。
            var codec = new GraphJsonCodec(ComposerGraph.Registry);
            var encoded = codec.Encode(document);
            return encoded.Success ? codec.Decode(encoded.Value) : GraphResult<GraphDocument>.Fail(encoded.Issues);
        }
        public GraphResult<ComposerGraph> Create(GraphDocument document)
        {
            var codec = new GraphJsonCodec(ComposerGraph.Registry);
            var encoded = codec.Encode(document);
            if (!encoded.Success) return GraphResult<ComposerGraph>.Fail(encoded.Issues);
            document = codec.Decode(encoded.Value).Value;
            var graph = ScriptableObject.CreateInstance<ComposerGraph>();
            graph.hideFlags = HideFlags.HideAndDontSave;
            try
            {
                graph.DocumentId = document.Id; graph.DocumentName = document.Name;
                foreach (var data in document.Nodes)
                {
                    var node = (ComposerNode)graph.AddNode(typeof(ComposerNode), new Vector2(data.X, data.Y));
                    node.SetDocument(data);
                }
                var nodes = graph.allNodes.OfType<ComposerNode>().ToDictionary(n => n.DocumentId);
                foreach (var edge in document.Edges)
                {
                    var connection = (ComposerConnection)graph.ConnectNodes(nodes[edge.SourceNodeId], nodes[edge.TargetNodeId]);
                    if (connection == null) throw new InvalidOperationException("插件拒绝创建已校验的连线。");
                    connection.SetDocument(edge);
                }
                graph.SelfSerialize();
                return GraphResult<ComposerGraph>.Ok(graph);
            }
            catch (Exception e)
            {
                Undo.ClearUndo(graph); UnityEngine.Object.DestroyImmediate(graph);
                return GraphResult<ComposerGraph>.Fail("BuildGraph", e.ToString());
            }
        }
        public GraphResult<ComposerGraph> Replace(ComposerGraph target, GraphDocument document)
        {
            if (target == null) return GraphResult<ComposerGraph>.Fail("Graph", "目标图不能为空。");
            var prepared = Create(document);
            if (!prepared.Success) return prepared;
            try
            {
                var replacement = prepared.Value.Serialize(null);
                target.SelfSerialize();
                var backup = target.GetSerializedJsonData();
                var references = target.GetSerializedReferencesData();
                Undo.IncrementCurrentGroup();
                var group = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("导入节点配置 JSON");
                Undo.RegisterCompleteObjectUndo(target, "导入节点配置 JSON");
                if (!target.Deserialize(replacement, null, true))
                {
                    target.Deserialize(backup, references, true);
                    Undo.RevertAllDownToGroup(group);
                    return GraphResult<ComposerGraph>.Fail("Import", "替换失败，原图已恢复。");
                }
                ComposerNode.CancelDrag();
                EditorUtility.SetDirty(target);
                Undo.CollapseUndoOperations(group);
                Undo.IncrementCurrentGroup();
                return GraphResult<ComposerGraph>.Ok(target);
            }
            finally
            {
                Undo.ClearUndo(prepared.Value);
                UnityEngine.Object.DestroyImmediate(prepared.Value);
            }
        }
    }
}
