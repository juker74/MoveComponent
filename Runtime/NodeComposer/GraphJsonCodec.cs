using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using SimpleJSON;

namespace GameFrameX.NodeComposer
{
    /// <summary>独立的配置 JSON 协议，不使用插件内部的 CLR 类型序列化格式。</summary>
    public sealed class GraphJsonCodec : IGraphJsonCodec
    {
        readonly GraphValidator validator;
        public GraphJsonCodec(NodeDefinitionRegistry registry) { validator = new GraphValidator(registry); }
        /// <summary>为扩展属性面板解析独立 JSON 值，执行与文件相同的严格语法校验。</summary>
        public static GraphResult<object> DecodeValue(string json)
        {
            try { JsonSyntax.Check(json); return GraphResult<object>.Ok(ReadValue(JSON.Parse(json))); }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { return GraphResult<object>.Fail("JsonValue", e.Message); }
        }
        public static GraphResult<string> EncodeValue(object value)
        {
            try
            {
                var json = WriteValue(value, 0).ToString(2);
                JsonSyntax.Check(json);
                return GraphResult<string>.Ok(json);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { return GraphResult<string>.Fail("JsonValue", e.Message); }
        }
        public GraphResult<GraphDocument> Decode(string json)
        {
            try
            {
                JsonSyntax.Check(json);
                var root = JSON.Parse(json);
                Fields(root, "schemaVersion", "id", "name", "nodes", "edges");
                var graph = new GraphDocument { SchemaVersion = Integer(root["schemaVersion"]), Id = Text(root["id"]), Name = Text(root["name"]) };
                if (!root["nodes"].IsArray || !root["edges"].IsArray) throw new FormatException("nodes 和 edges 必须是数组。");
                foreach (JSONNode node in root["nodes"].Children)
                {
                    Fields(node, "id", "typeId", "title", "x", "y", "properties");
                    var n = new NodeDocument { Id = Text(node["id"]), TypeId = Text(node["typeId"]), Title = Text(node["title"]), X = Number(node["x"]), Y = Number(node["y"]) };
                    if (!node["properties"].IsObject) throw new FormatException("properties 必须是对象。");
                    n.Properties = (Dictionary<string, object>)ReadValue(node["properties"]);
                    graph.Nodes.Add(n);
                }
                foreach (JSONNode edge in root["edges"].Children)
                {
                    Fields(edge, "id", "sourceNodeId", "sourcePortId", "targetNodeId", "targetPortId", "order");
                    graph.Edges.Add(new EdgeDocument { Id = Text(edge["id"]), SourceNodeId = Text(edge["sourceNodeId"]), SourcePortId = Text(edge["sourcePortId"]), TargetNodeId = Text(edge["targetNodeId"]), TargetPortId = Text(edge["targetPortId"]), Order = Integer(edge["order"]) });
                }
                var issues = validator.Validate(graph);
                return issues.Count == 0 ? GraphResult<GraphDocument>.Ok(graph) : GraphResult<GraphDocument>.Fail(issues);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { return GraphResult<GraphDocument>.Fail("Json", e.Message); }
        }
        public GraphResult<string> Encode(GraphDocument document)
        {
            var issues = validator.Validate(document);
            if (issues.Count > 0) return GraphResult<string>.Fail(issues);
            try
            {
                var root = new JSONObject();
                root["schemaVersion"] = document.SchemaVersion; root["id"] = document.Id; root["name"] = document.Name;
                var nodes = new JSONArray(); root["nodes"] = nodes;
                foreach (var n in document.Nodes.OrderBy(n => n.Id, StringComparer.Ordinal))
                {
                    var node = new JSONObject();
                    node["id"] = n.Id; node["typeId"] = n.TypeId; node["title"] = n.Title; node["x"] = n.X; node["y"] = n.Y;
                    node["properties"] = WriteValue(n.Properties, 0); nodes.Add(node);
                }
                var edges = new JSONArray(); root["edges"] = edges;
                foreach (var e in document.Edges.OrderBy(e => e.SourceNodeId, StringComparer.Ordinal).ThenBy(e => e.SourcePortId, StringComparer.Ordinal).ThenBy(e => e.Order).ThenBy(e => e.Id, StringComparer.Ordinal))
                {
                    var edge = new JSONObject();
                    edge["id"] = e.Id; edge["sourceNodeId"] = e.SourceNodeId; edge["sourcePortId"] = e.SourcePortId;
                    edge["targetNodeId"] = e.TargetNodeId; edge["targetPortId"] = e.TargetPortId; edge["order"] = e.Order; edges.Add(edge);
                }
                var json = root.ToString(2);
                // 同时限制导出值深度／大小，使导出的文件必定可回读。
                JsonSyntax.Check(json);
                return GraphResult<string>.Ok(json);
            }
            catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException)
            { return GraphResult<string>.Fail("JsonValue", e.Message); }
        }
        static void Fields(JSONNode node, params string[] fields)
        {
            if (node == null || !node.IsObject) throw new FormatException("需要 JSON 对象。");
            foreach (var pair in node)
                if (!fields.Contains(pair.Key)) throw new FormatException("未知字段: " + pair.Key);
            foreach (var field in fields)
                if (!node.HasKey(field)) throw new FormatException("缺少字段: " + field);
        }
        static string Text(JSONNode node) { if (!node.IsString) throw new FormatException("需要字符串。"); return node.Value; }
        static float Number(JSONNode node)
        {
            if (!node.IsNumber || double.IsNaN(node.AsDouble) || Math.Abs(node.AsDouble) > float.MaxValue) throw new FormatException("需要有限数值。");
            return (float)node.AsDouble;
        }
        static int Integer(JSONNode node)
        {
            if (!node.IsNumber || double.IsNaN(node.AsDouble) || node.AsDouble < int.MinValue || node.AsDouble > int.MaxValue || Math.Truncate(node.AsDouble) != node.AsDouble) throw new FormatException("需要 int32 整数。");
            return (int)node.AsDouble;
        }
        static object ReadValue(JSONNode node)
        {
            if (node.IsNull) return null;
            if (node.IsString) return node.Value;
            if (node.IsBoolean) return node.AsBool;
            if (node.IsNumber)
            {
                if (double.IsNaN(node.AsDouble) || double.IsInfinity(node.AsDouble)) throw new FormatException("JSON 不支持非有限数字。");
                return node.AsDouble;
            }
            if (node.IsArray) return node.Children.Select(ReadValue).ToList();
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (var pair in node) result.Add(pair.Key, ReadValue(pair.Value));
            return result;
        }
        static JSONNode WriteValue(object value, int depth)
        {
            if (depth > 64) throw new FormatException("JSON 嵌套过深或包含循环对象引用。");
            if (value == null) return JSONNull.CreateOrGet();
            if (value is string) return new JSONString((string)value);
            if (value is bool) return new JSONBool((bool)value);
            if (value is IDictionary<string, object>)
            {
                var result = new JSONObject();
                foreach (var pair in ((IDictionary<string, object>)value).OrderBy(p => p.Key, StringComparer.Ordinal))
                { if (pair.Key == null) throw new FormatException("JSON 键不能为空。"); result.Add(pair.Key, WriteValue(pair.Value, depth + 1)); }
                return result;
            }
            if (value is IList)
            {
                var result = new JSONArray(); foreach (var item in (IList)value) result.Add(WriteValue(item, depth + 1)); return result;
            }
            if (value is byte || value is short || value is int || value is long || value is float || value is double || value is decimal)
            {
                var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number)) throw new FormatException("JSON 不支持非有限数字。");
                if ((value is long && Math.Abs((decimal)(long)value) > 9007199254740991m) || value is decimal) throw new FormatException("超出精确支持范围的数字请使用字符串保存。");
                return new JSONNumber(number);
            }
            throw new FormatException("属性包含不支持的 JSON 值类型: " + value.GetType().Name);
        }
    }

    // SimpleJSON 的解析器容许缺失逗号等扩展语法；在文件边界先验证标准 JSON。
    internal sealed class JsonSyntax
    {
        static readonly Regex Token = new Regex(@"\G[ \t\r\n]*(""(?:[^""\\\x00-\x1F]|\\(?:[""\\/bfnrt]|u[0-9a-fA-F]{4}))*""|-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?|true|false|null|[{}\[\],:])", RegexOptions.CultureInvariant);
        readonly string json;
        int offset;
        string current;
        JsonSyntax(string json) { this.json = json; Next(); }
        public static void Check(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16 * 1024 * 1024) throw new FormatException("JSON 为空或超过 16 MiB 限制。");
            var syntax = new JsonSyntax(json); syntax.Value(0);
            if (syntax.current != null) throw new FormatException("JSON 根值后有多余内容。");
        }
        void Next()
        {
            var match = Token.Match(json, offset);
            if (!match.Success)
            {
                if (json.Substring(offset).Trim(' ', '\t', '\r', '\n').Length > 0) throw new FormatException("非法 JSON，位置: " + offset);
                current = null; return;
            }
            offset = match.Index + match.Length; current = match.Groups[1].Value;
        }
        void Expect(string token) { if (current != token) throw new FormatException("JSON 需要 " + token + "，位置: " + offset); Next(); }
        void Value(int depth)
        {
            if (depth > 64 || current == null) throw new FormatException("JSON 值缺失或嵌套超过 64 层。");
            if (current == "{" || current == "[")
            {
                bool obj = current == "{"; string end = obj ? "}" : "]";
                Next(); var keys = new HashSet<string>(StringComparer.Ordinal);
                if (current == end) { Next(); return; }
                while (true)
                {
                    if (obj)
                    {
                        if (current == null || !current.StartsWith("\"", StringComparison.Ordinal)) throw new FormatException("JSON 对象键必须为字符串。");
                        var key = JSON.Parse("[" + current + "]")[0].Value;
                        if (!keys.Add(key)) throw new FormatException("重复 JSON 字段: " + key);
                        Next(); Expect(":");
                    }
                    Value(depth + 1);
                    if (current == end) { Next(); return; }
                    Expect(",");
                }
            }
            if (current == "}" || current == "]" || current == "," || current == ":") throw new FormatException("JSON 值无效。");
            Next();
        }
    }
}
