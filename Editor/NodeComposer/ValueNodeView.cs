using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;

namespace GameFrameX.NodeComposer.Editor
{
    /// <summary>通用值节点配置；目标以 ID 保存，重命名不改变修改目标。</summary>
    public sealed class ValueNodeView : IComposerNodeView
    {
        public static readonly ValueNodeView Instance=new ValueNodeView();
        sealed class Draft { public string Saved,Text,Error; }
        readonly ConditionalWeakTable<ComposerNode,Draft> drafts=new ConditionalWeakTable<ComposerNode,Draft>();
        static string Text(ComposerNode node,string key,string fallback="")
        { object value;return node.Data.Properties.TryGetValue(key,out value)?value as string:fallback; }
        static void Save(ComposerNode node,string key,object value)
        {
            var result=node.UpdateProperties(new Dictionary<string,object>{{key,value}});
            if(!result.Success)Debug.LogWarning(result.Message);
        }
        static ValueNodeDefinition Definition(ComposerNode node)
        { INodeDefinition d;ComposerGraph.Registry.TryGet(node.Data.TypeId,out d);return (ValueNodeDefinition)d; }
        public void DrawNode(ComposerNode node)
        {
            var d=Definition(node);
            GUILayout.Label(d.DataTypeId+(d.Kind==ValueNodeKind.Cell ? " · 运行时共享当前值" : " · "+Label(Text(node,"operation",d.Kind==ValueNodeKind.Modify?"assign":"equal"))),EditorStyles.miniLabel);
            if(d.Kind==ValueNodeKind.Modify)
            {
                var target=node.graph.allNodes.OfType<ComposerNode>().FirstOrDefault(n=>n.DocumentId==Text(node,"target"));
                GUILayout.Label("目标："+(target==null?"未选择或已删除":target.name),EditorStyles.miniLabel);
            }
        }
        static string Label(string operation)
        {
            switch(operation)
            {
                case "assign":return "赋值";case "add":return "增加";case "subtract":return "减少";
                case "equal":return "等于";case "notEqual":return "不等于";case "less":return "小于";
                case "lessOrEqual":return "小于等于";case "greater":return "大于";case "greaterOrEqual":return "大于等于";
                default:return operation;
            }
        }
        public bool DrawInspector(ComposerNode node)
        {
            var definition=Definition(node);
            if(definition.Kind==ValueNodeKind.Cell)
            {
                EditorGUILayout.HelpBox("初始值在每次启动流程时恢复；视频循环保持当前值。数据输出读取当前值，控制进入时才将已连接的输入写入当前值。",MessageType.Info);
                if(new[]{"string","bool","int32","float32"}.Contains(definition.DataTypeId))return false;
                object value;
                if(!node.Data.Properties.TryGetValue("value",out value))value=definition.Properties.First(p=>p.Key=="value").DefaultValue;
                var encoded=GraphJsonCodec.EncodeValue(value);
                var draft=drafts.GetValue(node,n=>new Draft());
                if(draft.Saved!=encoded.Value){draft.Saved=encoded.Value;draft.Text=encoded.Value;draft.Error=null;}
                EditorGUILayout.LabelField("初始值（JSON）");
                draft.Text=EditorGUILayout.TextArea(draft.Text??"",GUILayout.MinHeight(70));
                if(GUILayout.Button("应用初始值"))
                {
                    var parsed=GraphJsonCodec.DecodeValue(draft.Text);
                    if(!parsed.Success)draft.Error=parsed.Message;
                    else
                    {
                        var saved=node.UpdateProperties(new Dictionary<string,object>{{"value",parsed.Value}});
                        draft.Error=saved.Success?null:saved.Message;
                    }
                }
                if(!string.IsNullOrEmpty(draft.Error))EditorGUILayout.HelpBox(draft.Error,MessageType.Error);
                return true;
            }
            ValueTypeDefinition type;
            if(!ComposerGraph.Registry.TryGetValueType(definition.DataTypeId,out type))
            { EditorGUILayout.HelpBox("缺少值类型适配器。",MessageType.Error);return true; }
            if(definition.Kind==ValueNodeKind.Modify)
            {
                var cells=node.graph.allNodes.OfType<ComposerNode>().Where(n=> {
                    INodeDefinition raw;ComposerGraph.Registry.TryGet(n.Data.TypeId,out raw);var d=raw as ValueNodeDefinition;
                    return d!=null&&d.Kind==ValueNodeKind.Cell&&d.DataTypeId==definition.DataTypeId;
                }).OrderBy(n=>n.name,StringComparer.Ordinal).ToArray();
                var ids=new List<string>{""};var labels=new List<string>{"请选择数据节点"};
                foreach(var cell in cells){ids.Add(cell.DocumentId);labels.Add(cell.name+" ["+cell.DocumentId+"]");}
                var target=Text(node,"target");
                if(!ids.Contains(target)){ids.Add(target);labels.Add("目标已删除："+target);}
                EditorGUI.BeginChangeCheck();
                var next=EditorGUILayout.Popup("修改目标",ids.IndexOf(target),labels.ToArray());
                if(EditorGUI.EndChangeCheck())Save(node,"target",ids[next]);
            }
            var operations=(definition.Kind==ValueNodeKind.Modify?type.Modifications:type.Comparisons).ToList();
            var selected=Text(node,"operation",definition.Kind==ValueNodeKind.Modify?"assign":"equal");
            if(!operations.Contains(selected))operations.Add(selected);
            EditorGUI.BeginChangeCheck();
            var choice=EditorGUILayout.Popup("运算",operations.IndexOf(selected),operations.Select(Label).ToArray());
            if(EditorGUI.EndChangeCheck())Save(node,"operation",operations[choice]);
            EditorGUILayout.HelpBox(definition.Kind==ValueNodeKind.Modify
                ? "连接操作值的数据输入；进入后修改所选数据节点的当前值，再沿下一步执行。"
                : "连接左右两个数据输入；进入时比较当前值，沿成立或不成立端口执行。",MessageType.Info);
            return true;
        }
    }
}
