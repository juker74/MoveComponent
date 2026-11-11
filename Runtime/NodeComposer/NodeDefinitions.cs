using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFrameX.NodeComposer
{
    public enum PortKind { Data, Control }
    public enum PortDirection { Input, Output }

    public sealed class PortDefinition
    {
        public string Id { get; private set; }
        public string DisplayName { get; private set; }
        public PortDirection Direction { get; private set; }
        public PortKind Kind { get; private set; }
        public string DataTypeId { get; private set; }
        public int MaxConnections { get; private set; }
        public PortDefinition(string id, string name, PortDirection direction, PortKind kind, string dataTypeId = null, int maxConnections = -1)
        {
            Id = id; DisplayName = name; Direction = direction; Kind = kind;
            DataTypeId = dataTypeId; MaxConnections = maxConnections;
        }
    }

    public sealed class PropertyDefinition
    {
        public string Key { get; private set; }
        public string DisplayName { get; private set; }
        public string DataTypeId { get; private set; }
        public bool Required { get; private set; }
        public object DefaultValue { get; private set; }
        public PropertyDefinition(string key, string name, string type, object defaultValue, bool required = false)
        { Key = key; DisplayName = name; DataTypeId = type; DefaultValue = defaultValue; Required = required; }
    }

    public interface INodeDefinition
    {
        string TypeId { get; }
        string DisplayName { get; }
        string Category { get; }
        IReadOnlyList<PortDefinition> Ports { get; }
        IReadOnlyList<PropertyDefinition> Properties { get; }
    }

    /// <summary>可选的实例端口扩展，根据节点配置生成端口；不修改节点或定义。</summary>
    public interface INodePortProvider
    {
        IReadOnlyList<PortDefinition> GetPorts(NodeDocument node);
    }

    public sealed class NodeDefinition : INodeDefinition
    {
        public string TypeId { get; private set; }
        public string DisplayName { get; private set; }
        public string Category { get; private set; }
        public IReadOnlyList<PortDefinition> Ports { get; private set; }
        public IReadOnlyList<PropertyDefinition> Properties { get; private set; }
        public NodeDefinition(string typeId, string name, string category, IEnumerable<PortDefinition> ports, params PropertyDefinition[] properties)
        {
            TypeId = typeId; DisplayName = name; Category = category;
            Ports = Array.AsReadOnly(ports.ToArray()); Properties = Array.AsReadOnly(properties.ToArray());
        }
    }

    public sealed class NodeDefinitionRegistry
    {
        readonly Dictionary<string, INodeDefinition> definitions = new Dictionary<string, INodeDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string, ValueTypeDefinition> valueTypes = new Dictionary<string, ValueTypeDefinition>(StringComparer.Ordinal);
        readonly Dictionary<string, Func<object, bool>> types = new Dictionary<string, Func<object, bool>>(StringComparer.Ordinal);
        public IEnumerable<INodeDefinition> Definitions { get { return definitions.Values.OrderBy(d => d.TypeId, StringComparer.Ordinal); } }
        public NodeDefinitionRegistry()
        {
            RegisterDataType("bool", v => v is bool);
            RegisterDataType("string", v => v is string);
            RegisterDataType("int32", v => IsNumber(v) && Convert.ToDouble(v) >= int.MinValue && Convert.ToDouble(v) <= int.MaxValue && Convert.ToDouble(v) == Math.Truncate(Convert.ToDouble(v)));
            RegisterDataType("float32", v => IsNumber(v) && !double.IsNaN(Convert.ToDouble(v)) && Math.Abs(Convert.ToDouble(v)) <= float.MaxValue);
            foreach(var id in new[]{"bool","string","int32","float32"})
            {
                var type=id;
                RegisterValueType(ValueTypeDefinition.Builtin(type,v=> {
                    if(!Accepts(type,v))throw new ArgumentException("值类型不匹配或溢出: "+type);
                    if(type=="int32")return (object)Convert.ToInt32(v);
                    if(type=="float32")return (object)Convert.ToSingle(v);
                    return v;
                },type=="int32"||type=="float32"));
            }
        }
        public IEnumerable<ValueTypeDefinition> ValueTypes { get { return valueTypes.Values.OrderBy(t=>t.Id,StringComparer.Ordinal); } }
        public void RegisterValueType(ValueTypeDefinition definition)
        {
            if(definition==null || !types.ContainsKey(definition.Id) || valueTypes.ContainsKey(definition.Id))
                throw new ArgumentException("请先注册 JSON 数据类型；值类型不可重复。");
            valueTypes.Add(definition.Id,definition);
        }
        public bool TryGetValueType(string id,out ValueTypeDefinition definition)
        { definition=null;return id!=null && valueTypes.TryGetValue(id,out definition); }
        public void RegisterValueNodes(string nodeTypeId,string name,string dataTypeId,object initialValue)
        {
            if(!valueTypes.ContainsKey(dataTypeId))throw new ArgumentException("值类型未注册: "+dataTypeId);
            Register(new ValueNodeDefinition(nodeTypeId,name,dataTypeId,initialValue,ValueNodeKind.Cell));
            if(!definitions.ContainsKey("flow.modify."+dataTypeId))
            {
                Register(new ValueNodeDefinition("flow.modify."+dataTypeId,"修改 "+name,dataTypeId,initialValue,ValueNodeKind.Modify));
                Register(new ValueNodeDefinition("flow.condition."+dataTypeId,"条件 "+name,dataTypeId,initialValue,ValueNodeKind.Condition));
            }
        }
        static bool IsNumber(object v) { return v is int || v is long || v is double || v is float || v is decimal || v is short || v is byte; }
        public void RegisterDataType(string id, Func<object, bool> accepts)
        {
            if (string.IsNullOrWhiteSpace(id) || accepts == null || types.ContainsKey(id)) throw new ArgumentException("数据类型 ID 无效或重复: " + id);
            types.Add(id, accepts);
        }
        public bool Accepts(string typeId, object value)
        { Func<object, bool> accepts; return typeId != null && types.TryGetValue(typeId, out accepts) && accepts(value); }
        public bool TryGet(string typeId, out INodeDefinition definition)
        { definition = null; return typeId != null && definitions.TryGetValue(typeId, out definition); }
        public IReadOnlyList<PortDefinition> GetPorts(NodeDocument node)
        {
            INodeDefinition definition;
            if (node == null || !TryGet(node.TypeId, out definition)) return new PortDefinition[0];
            var provider = definition as INodePortProvider;
            return provider == null ? definition.Ports : provider.GetPorts(node);
        }
        public void Register(INodeDefinition definition)
        {
            if (definition == null || string.IsNullOrWhiteSpace(definition.TypeId) || definitions.ContainsKey(definition.TypeId)) throw new ArgumentException("节点类型 ID 无效或重复。");
            if (definition.Ports == null || definition.Properties == null) throw new ArgumentException("端口和属性定义不可为空。");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var p in definition.Ports)
            {
                if (p == null || string.IsNullOrWhiteSpace(p.Id) || !ids.Add(p.Id) || p.MaxConnections < -1 || !Enum.IsDefined(typeof(PortDirection), p.Direction) || !Enum.IsDefined(typeof(PortKind), p.Kind)) throw new ArgumentException("端口定义无效。");
                if (p.Kind == PortKind.Data ? p.DataTypeId == null || !types.ContainsKey(p.DataTypeId) : p.DataTypeId != null) throw new ArgumentException("端口数据类型无效: " + p.Id);
            }
            ids.Clear();
            foreach (var p in definition.Properties)
                if (p == null || string.IsNullOrWhiteSpace(p.Key) || !ids.Add(p.Key) || p.DataTypeId == null || !types.ContainsKey(p.DataTypeId) || !Accepts(p.DataTypeId, p.DefaultValue)) throw new ArgumentException("属性定义或默认值无效。");
            definitions.Add(definition.TypeId, definition);
        }
        public static NodeDefinitionRegistry CreateDefault()
        {
            var registry = new NodeDefinitionRegistry();
            registry.RegisterValueNodes("common.text","文本","string","");
            registry.RegisterValueNodes("common.number","数值","float32",0d);
            registry.RegisterValueNodes("common.boolean","布尔","bool",false);
            registry.RegisterValueNodes("common.integer","整数","int32",0);
            registry.Register(new NodeDefinition("common.control","控制","通用",new[]{
                new PortDefinition("enter","进入",PortDirection.Input,PortKind.Control),
                new PortDefinition("next","下一步",PortDirection.Output,PortKind.Control)}));
            return registry;
        }
    }
}
