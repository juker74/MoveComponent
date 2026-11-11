using System;
using System.Linq;
using NodeCanvas.Framework;
using ParadoxNotion;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    [CreateAssetMenu(menuName = "GameFrameX/Node Composer Graph", fileName = "NodeComposerGraph")]
    public sealed class ComposerGraph : Graph
    {
        public static NodeDefinitionRegistry Registry { get; } = NodeDefinitionRegistry.CreateDefault();
        [SerializeField] string documentId = Guid.NewGuid().ToString("N");
        [SerializeField] string documentName = "Graph";
        public string DocumentId { get { return documentId; } set { documentId = value; } }
        public string DocumentName { get { return documentName; } set { documentName = value; } }
        public override Type baseNodeType { get { return typeof(ComposerNodeBase); } }
        public override bool requiresAgent { get { return false; } }
        public override bool requiresPrimeNode { get { return false; } }
        public override bool isTree { get { return false; } }
        public override bool allowBlackboardOverrides { get { return false; } }
        public override bool canAcceptVariableDrops { get { return false; } }
        public override PlanarDirection flowDirection { get { return PlanarDirection.Horizontal; } }

        [Serializable] public sealed class Metadata { public string id; public string name; }
        public override object OnDerivedDataSerialization() { return new Metadata { id = documentId, name = documentName }; }
        public override void OnDerivedDataDeserialization(object data)
        {
            var metadata = data as Metadata;
            if (metadata != null) { documentId = metadata.id; documentName = metadata.name; }
        }
        protected override void OnGraphStarted() { Stop(false); }

        public ComposerNode AddComposerNode(string typeId, Vector2 position)
        {
            INodeDefinition definition;
            if (!Registry.TryGet(typeId, out definition)) throw new ArgumentException("未知节点类型: " + typeId);
            var document = new GraphDocument();
            var data = new NodeDocument { TypeId = typeId, Title = definition.DisplayName, X = position.x, Y = position.y };
            foreach (var property in definition.Properties) data.Properties[property.Key] = property.DefaultValue;
            document.Nodes.Add(data);
            var codec = new GraphJsonCodec(Registry);
            var encoded = codec.Encode(document);
            if (!encoded.Success) throw new ArgumentException(encoded.Message);
            data = codec.Decode(encoded.Value).Value.Nodes[0];
            SelfSerialize();
            var node = (ComposerNode)AddNode(typeof(ComposerNode), position);
            node.SetDocument(data);
            SelfSerialize(); EditorUtility.SetDirty(this);
            return node;
        }
        public GraphResult<ComposerConnection> ConnectPorts(ComposerNode source, string sourcePortId, ComposerNode target, string targetPortId)
        {
            if (source == null || target == null || source.graph != this || target.graph != this || !allNodes.Contains(source) || !allNodes.Contains(target)) return GraphResult<ComposerConnection>.Fail("Graph", "端点必须属于当前图。");
            var adapter = new ComposerGraphAdapter();
            var captured = adapter.Capture(this);
            if (!captured.Success) return GraphResult<ComposerConnection>.Fail(captured.Issues);
            var edge = new EdgeDocument { SourceNodeId = source.DocumentId, SourcePortId = sourcePortId, TargetNodeId = target.DocumentId, TargetPortId = targetPortId };
            edge.Order = captured.Value.Edges.Where(e => e.SourceNodeId == edge.SourceNodeId && e.SourcePortId == edge.SourcePortId).Select(e => e.Order + 1).DefaultIfEmpty(0).Max();
            captured.Value.Edges.Add(edge);
            var issues = new GraphValidator(Registry).Validate(captured.Value);
            if (issues.Count != 0) return GraphResult<ComposerConnection>.Fail(issues);
            SelfSerialize();
            var connection = (ComposerConnection)ConnectNodes(source, target);
            if (connection == null) return GraphResult<ComposerConnection>.Fail("Connect", "插件拒绝连接。");
            connection.SetDocument(edge);
            SelfSerialize(); EditorUtility.SetDirty(this);
            return GraphResult<ComposerConnection>.Ok(connection);
        }
        protected override GenericMenu OnCanvasContextMenu(GenericMenu menu, Vector2 position)
        {
            foreach (var definition in Registry.Definitions)
            {
                var typeId = definition.TypeId;
                menu.AddItem(new GUIContent("配置节点/" + definition.Category + "/" + definition.DisplayName), false, () => AddComposerNode(typeId, position));
            }
            return menu;
        }
        protected override void OnGraphEditorToolbar()
        {
            if (GUILayout.Button("添加节点", EditorStyles.toolbarDropDown))
            {
                var menu = new GenericMenu();
                foreach (var definition in Registry.Definitions)
                {
                    var typeId = definition.TypeId;
                    menu.AddItem(new GUIContent(definition.Category + "/" + definition.DisplayName), false, () => AddComposerNode(typeId, new Vector2(100 + allNodes.Count * 25, 100 + allNodes.Count * 25)));
                }
                menu.ShowAsContext();
            }
            if (GUILayout.Button("导入配置 JSON", EditorStyles.toolbarButton)) GraphFileService.ShowImport(this);
            if (GUILayout.Button("导出配置 JSON", EditorStyles.toolbarButton)) GraphFileService.ShowExport(this);
        }
    }
}
