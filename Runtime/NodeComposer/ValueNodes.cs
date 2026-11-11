using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFrameX.NodeComposer
{
    /// <summary>JSON 表示到不可变运行值的转换，以及该类型明确支持的运算。注册后不可变。</summary>
    public sealed class ValueTypeDefinition
    {
        readonly Func<object, object> normalize;
        readonly IReadOnlyDictionary<string, Func<object, object, object>> modifications;
        readonly IReadOnlyDictionary<string, Func<object, object, bool>> comparisons;
        public string Id { get; }
        public IEnumerable<string> Modifications { get { return modifications.Keys; } }
        public IEnumerable<string> Comparisons { get { return comparisons.Keys; } }
        public ValueTypeDefinition(string id, Func<object, object> normalize,
            IDictionary<string, Func<object, object, object>> modifications = null,
            IDictionary<string, Func<object, object, bool>> comparisons = null)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("值类型 ID 不能为空。");
            Id=id;this.normalize=normalize??throw new ArgumentNullException(nameof(normalize));
            var writes=modifications==null ? new Dictionary<string,Func<object,object,object>>() : new Dictionary<string,Func<object,object,object>>(modifications,StringComparer.Ordinal);
            writes["assign"]=(a,b)=>b;
            this.modifications=new System.Collections.ObjectModel.ReadOnlyDictionary<string,Func<object,object,object>>(writes);
            var tests=comparisons==null ? new Dictionary<string,Func<object,object,bool>>() : new Dictionary<string,Func<object,object,bool>>(comparisons,StringComparer.Ordinal);
            if (!tests.ContainsKey("equal")) tests["equal"]=(a,b)=>Equals(a,b);
            if (!tests.ContainsKey("notEqual")) tests["notEqual"]=(a,b)=>!tests["equal"](a,b);
            this.comparisons=new System.Collections.ObjectModel.ReadOnlyDictionary<string,Func<object,object,bool>>(tests);
            if(writes.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Value==null)||tests.Any(p=>string.IsNullOrWhiteSpace(p.Key)||p.Value==null))
                throw new ArgumentException("运算名称和实现不能为空。");
        }
        public object Normalize(object value)
        {
            var result=normalize(value);
            if(result==null || !(result is string || result.GetType().IsValueType))
                throw new ArgumentException("运行值必须是值类型或 string；自定义结构体应保持不可变。");
            return result;
        }
        public object Modify(string operation,object current,object operand)
        {
            Func<object,object,object> apply;
            if(!modifications.TryGetValue(operation,out apply))throw new ArgumentException("不支持修改运算: "+operation);
            return Normalize(apply(Normalize(current),Normalize(operand)));
        }
        public bool Compare(string operation,object left,object right)
        {
            Func<object,object,bool> apply;
            if(!comparisons.TryGetValue(operation,out apply))throw new ArgumentException("不支持比较运算: "+operation);
            return apply(Normalize(left),Normalize(right));
        }
        internal static ValueTypeDefinition Builtin(string id,Func<object,object> normalize,bool number)
        {
            if(!number)return new ValueTypeDefinition(id,normalize);
            var writes=new Dictionary<string,Func<object,object,object>> {
                {"add",(a,b)=>Convert.ToDouble(a)+Convert.ToDouble(b)},
                {"subtract",(a,b)=>Convert.ToDouble(a)-Convert.ToDouble(b)} };
            var tests=new Dictionary<string,Func<object,object,bool>> {
                {"less",(a,b)=>Convert.ToDouble(a)<Convert.ToDouble(b)},
                {"lessOrEqual",(a,b)=>Convert.ToDouble(a)<=Convert.ToDouble(b)},
                {"greater",(a,b)=>Convert.ToDouble(a)>Convert.ToDouble(b)},
                {"greaterOrEqual",(a,b)=>Convert.ToDouble(a)>=Convert.ToDouble(b)} };
            return new ValueTypeDefinition(id,normalize,writes,tests);
        }
    }

    public enum ValueNodeKind { Cell, Modify, Condition }
    /// <summary>运行时无需识别特定节点类型名称，通过定义读取值语义。</summary>
    public sealed class ValueNodeDefinition : INodeDefinition
    {
        public string TypeId { get; }
        public string DisplayName { get; }
        public string Category { get { return Kind==ValueNodeKind.Cell ? "数据" : "流程"; } }
        public string DataTypeId { get; }
        public ValueNodeKind Kind { get; }
        public IReadOnlyList<PortDefinition> Ports { get; }
        public IReadOnlyList<PropertyDefinition> Properties { get; }
        public ValueNodeDefinition(string typeId,string name,string dataTypeId,object initialValue,ValueNodeKind kind)
        {
            TypeId=typeId;DisplayName=name;DataTypeId=dataTypeId;Kind=kind;
            var ports=new List<PortDefinition>{new PortDefinition("enter","进入",PortDirection.Input,PortKind.Control)};
            var props=new List<PropertyDefinition>();
            if(kind==ValueNodeKind.Cell)
            {
                ports.Add(new PortDefinition("valueIn","写入值",PortDirection.Input,PortKind.Data,dataTypeId,1));
                ports.Add(new PortDefinition("valueOut","当前值",PortDirection.Output,PortKind.Data,dataTypeId));
                props.Add(new PropertyDefinition("value","初始值",dataTypeId,initialValue));
            }
            else
            {
                props.Add(new PropertyDefinition("operation","运算","string",kind==ValueNodeKind.Modify ? "assign" : "equal"));
                if(kind==ValueNodeKind.Modify)
                {
                    props.Add(new PropertyDefinition("target","目标数据节点","string",""));
                    ports.Add(new PortDefinition("operand","操作值",PortDirection.Input,PortKind.Data,dataTypeId,1));
                }
                else
                {
                    ports.Add(new PortDefinition("left","左值",PortDirection.Input,PortKind.Data,dataTypeId,1));
                    ports.Add(new PortDefinition("right","右值",PortDirection.Input,PortKind.Data,dataTypeId,1));
                }
            }
            if(kind==ValueNodeKind.Condition)
            {
                ports.Add(new PortDefinition("true","成立",PortDirection.Output,PortKind.Control, maxConnections:1));
                ports.Add(new PortDefinition("false","不成立",PortDirection.Output,PortKind.Control, maxConnections:1));
            }
            else ports.Add(new PortDefinition("next","下一步",PortDirection.Output,PortKind.Control,maxConnections:kind==ValueNodeKind.Cell?-1:1));
            Ports=ports.AsReadOnly();Properties=props.AsReadOnly();
        }
    }
}
