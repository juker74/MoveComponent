using System;
using System.Collections.Generic;
using System.Linq;
using NodeCanvas.Editor;
using NodeCanvas.Framework;
using ParadoxNotion;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    // NodeCanvas.AddNode 要求节点是 baseNodeType 的严格子类。
    public abstract class ComposerNodeBase : Node { }

    [Serializable]
    public sealed class ComposerNode : ComposerNodeBase
    {
        [SerializeField] NodeDocument data = new NodeDocument { TypeId = "common.control", Title = "控制" };
        [NonSerialized] readonly Dictionary<string, float> portRows = new Dictionary<string, float>();
        static ComposerNode draggingNode;
        static string draggingPort;
        static ComposerNode() { Undo.undoRedoPerformed += CancelDrag; }
        public static void CancelDrag() { draggingNode = null; draggingPort = null; }
        public NodeDocument Data { get { return data; } }
        public string DocumentId { get { return data.Id; } }
        public override string name { get { return string.IsNullOrEmpty(data.Title) ? data.TypeId : data.Title; } set { data.Title = value; } }
        public override int maxInConnections { get { return -1; } }
        public override int maxOutConnections { get { return -1; } }
        public override Type outConnectionType { get { return typeof(ComposerConnection); } }
        public override bool allowAsPrime { get { return false; } }
        public override bool canSelfConnect { get { return false; } }
        public override Alignment2x2 commentsAlignment { get { return Alignment2x2.Bottom; } }
        public override Alignment2x2 iconAlignment { get { return Alignment2x2.Left; } }
        public void SetDocument(NodeDocument document) { data = document; position = new Vector2(document.X, document.Y); }
        public override void OnValidate(Graph assignedGraph)
        {
            if (assignedGraph.allNodes.OfType<ComposerNode>().Any(n => n != this && n.DocumentId == DocumentId)) data.Id = Guid.NewGuid().ToString("N");
        }
        public override void OnActiveRelinkEnd(Connection connection) { }
        INodeDefinition Definition
        { get { INodeDefinition definition; ComposerGraph.Registry.TryGet(data.TypeId, out definition); return definition; } }
        public IReadOnlyList<PortDefinition> Ports { get { return ComposerGraph.Registry.GetPorts(data); } }
        static string PortLabel(PortDefinition port)
        { return port.DisplayName + "  [" + (port.Kind == PortKind.Control ? "控制" : port.DataTypeId) + "]"; }
        protected override void OnNodeGUI()
        {
            var definition = Definition;
            if (definition == null) { GUILayout.Label("未知类型: " + data.TypeId); return; }
            var view = ComposerNodeViews.Find(data.TypeId) ?? (definition is ValueNodeDefinition ? ValueNodeView.Instance : null);
            if (view != null) view.DrawNode(this);
            var inputs = Ports.Where(p => p.Direction == PortDirection.Input).ToArray();
            var outputs = Ports.Where(p => p.Direction == PortDirection.Output).ToArray();
            for (int i = 0; i < Math.Max(inputs.Length, outputs.Length); i++)
            {
                var row = GUILayoutUtility.GetRect(290, 24);
                if (i < inputs.Length)
                {
                    GUI.Label(new Rect(row.x + 5, row.y, row.width / 2 - 10, row.height), PortLabel(inputs[i]), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleLeft });
                    if (Event.current.type == EventType.Repaint) portRows[inputs[i].Id] = row.center.y;
                }
                if (i < outputs.Length)
                {
                    GUI.Label(new Rect(row.center.x, row.y, row.width / 2 - 5, row.height), PortLabel(outputs[i]), new GUIStyle(EditorStyles.miniLabel) { alignment = TextAnchor.MiddleRight });
                    if (Event.current.type == EventType.Repaint) portRows[outputs[i].Id] = row.center.y;
                }
            }
        }
        protected override void OnNodeInspectorGUI()
        {
            EditorGUILayout.LabelField("类型", data.TypeId);
            EditorGUILayout.LabelField("ID", data.Id);
            EditorGUI.BeginChangeCheck();
            var title = EditorGUILayout.TextField("名称", data.Title);
            if (EditorGUI.EndChangeCheck()) Change(() => data.Title = title);
            var definition = Definition;
            if (definition == null) { EditorGUILayout.HelpBox("节点定义未注册。", MessageType.Error); return; }
            var view = ComposerNodeViews.Find(data.TypeId) ?? (definition is ValueNodeDefinition ? ValueNodeView.Instance : null);
            if (view != null && view.DrawInspector(this)) return;
            foreach (var property in definition.Properties)
            {
                object value; if (!data.Properties.TryGetValue(property.Key, out value)) value = property.DefaultValue;
                object next = value;
                EditorGUI.BeginChangeCheck();
                if (property.DataTypeId == "string") next = EditorGUILayout.TextField(property.DisplayName, (string)value);
                else if (property.DataTypeId == "bool") next = EditorGUILayout.Toggle(property.DisplayName, Convert.ToBoolean(value));
                else if (property.DataTypeId == "int32") next = EditorGUILayout.IntField(property.DisplayName, Convert.ToInt32(value));
                else if (property.DataTypeId == "float32") next = (double)EditorGUILayout.FloatField(property.DisplayName, Convert.ToSingle(value));
                else EditorGUILayout.HelpBox(property.DisplayName + "：自定义类型请通过 JSON 配置。", MessageType.Info);
                if (EditorGUI.EndChangeCheck()) { var captured = next; Change(() => data.Properties[property.Key] = captured); }
            }
            EditorGUILayout.HelpBox("从输出端口拖至输入端口建立连接；右键端口断开连线。允许跨节点环路。", MessageType.Info);
        }
        void Change(Action action)
        { graph.SelfSerialize(); Undo.RecordObject(graph, "修改节点属性"); action(); graph.SelfSerialize(); EditorUtility.SetDirty(graph); }
        /// <summary>校验并深拷贝属性补丁，移除消失的端口连线；失败保留原图，成功可撤销。</summary>
        public GraphResult<bool> UpdateProperties(IDictionary<string, object> patch)
        {
            if (patch == null || graph == null || !graph.allNodes.Contains(this))
                return GraphResult<bool>.Fail("Node", "属性补丁或节点无效。");
            var captured = new ComposerGraphAdapter().Capture((ComposerGraph)graph);
            if (!captured.Success) return GraphResult<bool>.Fail(captured.Issues);
            var candidate = captured.Value.Nodes.Single(n => n.Id == DocumentId);
            foreach (var pair in patch) candidate.Properties[pair.Key] = pair.Value;
            var portIds = new HashSet<string>(ComposerGraph.Registry.GetPorts(candidate).Select(p => p.Id));
            var removed = captured.Value.Edges.Where(e => e.SourceNodeId == DocumentId && !portIds.Contains(e.SourcePortId)
                || e.TargetNodeId == DocumentId && !portIds.Contains(e.TargetPortId)).Select(e => e.Id).ToArray();
            captured.Value.Edges.RemoveAll(e => removed.Contains(e.Id));
            var codec = new GraphJsonCodec(ComposerGraph.Registry);
            var encoded = codec.Encode(captured.Value);
            if (!encoded.Success) return GraphResult<bool>.Fail(encoded.Issues);
            candidate = codec.Decode(encoded.Value).Value.Nodes.Single(n => n.Id == DocumentId);
            graph.SelfSerialize();
            Undo.RegisterCompleteObjectUndo(graph, "修改节点配置");
            CancelDrag();
            data.Properties = candidate.Properties;
            foreach (var connection in outConnections.Concat(inConnections).OfType<ComposerConnection>().Where(c => removed.Contains(c.DocumentId)).ToArray())
                graph.RemoveConnection(connection);
            portRows.Clear();
            graph.SelfSerialize(); EditorUtility.SetDirty(graph);
            return GraphResult<bool>.Ok(true);
        }
        Vector2 PortPosition(PortDefinition port)
        {
            float row; if (!portRows.TryGetValue(port.Id, out row)) row = 40 + Array.FindIndex(Ports.Where(p => p.Direction == port.Direction).ToArray(), p => p.Id == port.Id) * 24;
            return new Vector2(port.Direction == PortDirection.Input ? rect.xMin - 5 : rect.xMax + 5, rect.yMin + row);
        }
        protected override void DrawNodeConnections(Rect drawCanvas, bool fullDrawPass, Vector2 canvasMousePos, float zoomFactor)
        {
            var definition = Definition;
            if (definition == null) return;
            var e = Event.current;
            foreach (var port in Ports)
            {
                var pos = PortPosition(port);
                var hit = new Rect(pos.x - 9, pos.y - 9, 18, 18);
                var color = port.Kind == PortKind.Control ? Color.white : new Color(.3f, .75f, 1);
                EditorGUI.DrawRect(new Rect(pos.x - 5, pos.y - 5, 10, 10), color);
                if (!GraphEditorUtility.allowClick) continue;
                EditorGUIUtility.AddCursorRect(hit, MouseCursor.Link);
                if (e.type == EventType.MouseDown && e.button == 0 && hit.Contains(e.mousePosition))
                { draggingNode = this; draggingPort = port.Id; e.Use(); }
                if (e.type == EventType.MouseUp && e.button == 0 && draggingNode != null && hit.Contains(e.mousePosition))
                {
                    var origin = draggingNode; var originPort = draggingPort; draggingNode = null; draggingPort = null;
                    GraphResult<ComposerConnection> result;
                    if (port.Direction == PortDirection.Input) result = ((ComposerGraph)graph).ConnectPorts(origin, originPort, this, port.Id);
                    else result = ((ComposerGraph)graph).ConnectPorts(this, port.Id, origin, originPort);
                    if (!result.Success) Debug.LogWarning(result.Message);
                    e.Use(); GUI.changed = true;
                }
                if (e.type == EventType.ContextClick && hit.Contains(e.mousePosition))
                {
                    var menu = new GenericMenu();
                    var edges = (port.Direction == PortDirection.Output ? outConnections : inConnections).OfType<ComposerConnection>().Where(c => port.Direction == PortDirection.Output ? c.SourcePortId == port.Id : c.TargetPortId == port.Id).ToArray();
                    foreach (var connection in edges)
                    {
                        var captured = connection;
                        menu.AddItem(new GUIContent("断开/" + connection.DocumentId), false, () => { graph.RemoveConnection(captured); graph.SelfSerialize(); });
                    }
                    if (edges.Length == 0) menu.AddDisabledItem(new GUIContent("没有连线"));
                    GraphEditorUtility.PostGUI += () => menu.ShowAsContext(); e.Use();
                }
            }
            foreach (var connection in outConnections.OfType<ComposerConnection>().ToArray())
            {
                var target = connection.targetNode as ComposerNode;
                var fromPort = Ports.FirstOrDefault(p => p.Id == connection.SourcePortId);
                var toPort = target != null ? target.Ports.FirstOrDefault(p => p.Id == connection.TargetPortId) : null;
                if (fromPort == null || toPort == null) continue;
                var from = PortPosition(fromPort); var to = target.PortPosition(toPort);
                var distance = Mathf.Max(60, Mathf.Abs(to.x - from.x) * .5f);
                var tangentA = from + Vector2.right * distance; var tangentB = to + Vector2.left * distance;
                var color = GraphEditorUtility.activeElement == connection ? Color.yellow : fromPort.Kind == PortKind.Control ? Color.white : new Color(.3f, .75f, 1);
                Handles.DrawBezier(from, to, tangentA, tangentB, color, null, 3);
                Handles.color = color;
                Handles.DrawAAPolyLine(2, new Vector3(to.x - 9, to.y - 4), new Vector3(to.x, to.y), new Vector3(to.x - 9, to.y + 4));
                if (GraphEditorUtility.allowClick && (e.type == EventType.MouseDown || e.type == EventType.ContextClick) && Vector2.Distance(e.mousePosition, from) > 12 && Vector2.Distance(e.mousePosition, to) > 12 && HandleUtility.DistancePointBezier(e.mousePosition, from, to, tangentA, tangentB) < 7)
                {
                    GraphEditorUtility.activeElement = connection;
                    if (e.type == EventType.ContextClick)
                    {
                        var captured = connection; var menu = new GenericMenu();
                        menu.AddItem(new GUIContent("删除连线"), false, () => { graph.RemoveConnection(captured); graph.SelfSerialize(); });
                        GraphEditorUtility.PostGUI += () => menu.ShowAsContext();
                    }
                    e.Use();
                }
            }
            if (draggingNode == this && draggingNode.graph == graph)
            {
                var port = Ports.FirstOrDefault(p => p.Id == draggingPort);
                if (port != null)
                {
                    var start = PortPosition(port); var sign = port.Direction == PortDirection.Output ? 1 : -1;
                    Handles.DrawBezier(start, e.mousePosition, start + Vector2.right * 60 * sign, e.mousePosition - Vector2.right * 60 * sign, Color.yellow, null, 2);
                    GUI.changed = true;
                }
                if (e.rawType == EventType.MouseUp || e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
                { GraphEditorUtility.PostGUI += CancelDrag; }
            }
        }
    }
}
