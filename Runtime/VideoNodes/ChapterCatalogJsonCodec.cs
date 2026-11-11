using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GameFrameX.NodeComposer.Video
{
    /// <summary>严格的章节目录 JSON 协议。资源键为相对 bundle 的路径，由宿主解析。</summary>
    public sealed class ChapterCatalogJsonCodec
    {
        static readonly Regex Segment = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_.-]*\z", RegexOptions.CultureInvariant);
        static readonly Regex Id = new Regex(@"\A[A-Za-z0-9][A-Za-z0-9_-]{0,127}\z", RegexOptions.CultureInvariant);

        public GraphResult<ChapterCatalogDocument> Decode(string json)
        {
            var parsed = GraphJsonCodec.DecodeValue(json);
            if (!parsed.Success) return GraphResult<ChapterCatalogDocument>.Fail(parsed.Issues);
            try
            {
                var root = Fields(parsed.Value, "schemaVersion", "startChapterId", "chapters");
                var version = root["schemaVersion"];
                if (!(version is double) || (double)version != 1) throw new FormatException("仅支持 schemaVersion 1。");
                var document = new ChapterCatalogDocument { StartChapterId = Text(root["startChapterId"]) };
                var entries = root["chapters"] as IList<object>;
                if (entries == null) throw new FormatException("chapters 必须为数组。");
                foreach (var value in entries)
                {
                    var chapter = Fields(value, "chapterId", "name", "graphKey", "entryNodeId");
                    document.Chapters.Add(new ChapterDocument {
                        ChapterId = Text(chapter["chapterId"]), Name = Text(chapter["name"]),
                        GraphKey = Text(chapter["graphKey"]), EntryNodeId = Text(chapter["entryNodeId"])
                    });
                }
                var issues = Validate(document);
                return issues.Count == 0 ? GraphResult<ChapterCatalogDocument>.Ok(document) : GraphResult<ChapterCatalogDocument>.Fail(issues);
            }
            catch (FormatException e) { return GraphResult<ChapterCatalogDocument>.Fail("CatalogJson", e.Message); }
        }

        public GraphResult<string> Encode(ChapterCatalogDocument document)
        {
            var issues = Validate(document);
            if (issues.Count != 0) return GraphResult<string>.Fail(issues);
            return GraphJsonCodec.EncodeValue(new Dictionary<string, object> {
                { "schemaVersion", document.SchemaVersion }, { "startChapterId", document.StartChapterId },
                { "chapters", document.Chapters.Select(c => (object)new Dictionary<string, object> {
                    { "chapterId", c.ChapterId }, { "name", c.Name }, { "graphKey", c.GraphKey }, { "entryNodeId", c.EntryNodeId }
                }).ToList() }
            });
        }

        public IReadOnlyList<GraphIssue> Validate(ChapterCatalogDocument document)
        {
            var issues = new List<GraphIssue>();
            if (document == null) { issues.Add(new GraphIssue("Catalog", "章节目录不能为空。")); return issues; }
            if (document.SchemaVersion != 1) issues.Add(new GraphIssue("CatalogVersion", "仅支持 schemaVersion 1。"));
            if (document.Chapters == null || document.Chapters.Count == 0)
            { issues.Add(new GraphIssue("Chapters", "请至少配置一个章节。")); return issues; }
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var chapter in document.Chapters)
            {
                if (chapter == null) { issues.Add(new GraphIssue("Chapter", "章节项不能为空。")); continue; }
                var label = "章节 " + chapter.ChapterId + "：";
                if (!IsSafeId(chapter.ChapterId) || !ids.Add(chapter.ChapterId))
                    issues.Add(new GraphIssue("ChapterId", label + "ID 必须唯一，使用 1～128 个英文字母、数字、_ 或 -，并以字母或数字开头。"));
                if (string.IsNullOrWhiteSpace(chapter.Name)) issues.Add(new GraphIssue("ChapterName", label + "名称不能为空。"));
                if (!IsSafeGraphKey(chapter.GraphKey) || !keys.Add(chapter.GraphKey))
                    issues.Add(new GraphIssue("GraphKey", label + "资源键必须是唯一、安全的相对 JSON 路径。"));
                if (string.IsNullOrWhiteSpace(chapter.EntryNodeId)) issues.Add(new GraphIssue("ChapterEntry", label + "请选择入口节点。"));
            }
            if (!document.Chapters.Any(c => c != null && c.ChapterId == document.StartChapterId))
                issues.Add(new GraphIssue("StartChapter", "默认起始章节不存在，请重新选择。"));
            return issues;
        }

        public static bool IsSafeId(string value)
        { return value != null && Id.IsMatch(value) && !IsReserved(value); }

        public static bool IsSafeGraphKey(string value)
        {
            return value != null && value.Length <= 512 && value.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
                && value.Split('/').All(s => s.Length > 0 && Segment.IsMatch(s) && !s.EndsWith(".", StringComparison.Ordinal) && !IsReserved(s));
        }

        static bool IsReserved(string value)
        {
            var stem = value.Split('.')[0].ToUpperInvariant();
            return stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL"
                || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '1' && stem[3] <= '9';
        }

        static Dictionary<string, object> Fields(object value, params string[] fields)
        {
            var map = value as Dictionary<string, object>;
            if (map == null || map.Count != fields.Length || fields.Any(f => !map.ContainsKey(f)))
                throw new FormatException("章节目录字段不完整或包含未知字段。");
            return map;
        }
        static string Text(object value)
        { if (!(value is string)) throw new FormatException("章节字段必须为字符串。"); return (string)value; }
    }
}
