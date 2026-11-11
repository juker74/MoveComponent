using System;
using System.Linq;
using NodeCanvas.Framework;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    [Serializable]
    public sealed class ComposerConnection : Connection
    {
        [SerializeField] string documentId = Guid.NewGuid().ToString("N");
        [SerializeField] string sourcePortId;
        [SerializeField] string targetPortId;
        [SerializeField] int order;
        public string DocumentId { get { return documentId; } }
        public string SourcePortId { get { return sourcePortId; } }
        public string TargetPortId { get { return targetPortId; } }
        public int Order { get { return order; } }
        public void SetDocument(EdgeDocument document)
        { documentId = document.Id; sourcePortId = document.SourcePortId; targetPortId = document.TargetPortId; order = document.Order; }
        public override void OnValidate(int sourceIndex, int targetIndex)
        {
            // 插件的复制操作会复制自有字段；为复制后的边分配新的协议 ID。
            if (sourceNode != null && graph != null && graph.allNodes.SelectMany(n => n.outConnections).OfType<ComposerConnection>().Any(c => c != this && c.DocumentId == documentId))
                documentId = Guid.NewGuid().ToString("N");
        }
        protected override void OnConnectionInspectorGUI()
        {
            EditorGUILayout.LabelField("输出端口", sourcePortId ?? "未设置");
            EditorGUILayout.LabelField("输入端口", targetPortId ?? "未设置");
            EditorGUILayout.LabelField("连接 ID", documentId);
            var value = EditorGUILayout.IntField("同端口顺序", order);
            if (value != order)
            {
                bool duplicate = sourceNode.outConnections.OfType<ComposerConnection>().Any(c => c != this && c.SourcePortId == sourcePortId && c.Order == value);
                if (value >= 0 && !duplicate)
                { graph.SelfSerialize(); Undo.RecordObject(graph, "修改连线顺序"); order = value; graph.SelfSerialize(); EditorUtility.SetDirty(graph); }
            }
        }
    }
}
