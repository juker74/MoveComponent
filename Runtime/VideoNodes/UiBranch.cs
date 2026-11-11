using System;
using System.Collections.Generic;
using System.Linq;

namespace GameFrameX.NodeComposer.Video
{
    /// <summary>一个可提交的 UI 分支。Id 绑定连线；Name 只用于显示；集合顺序由节点配置决定。</summary>
    public sealed class UiBranch
    {
        public string Id { get; }
        public string Name { get; }
        public UiBranch(string id, string name)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("分支 ID 不能为空。", nameof(id));
            Id = id; Name = name ?? "";
        }
    }

    public static class UiBranches
    {
        public static string PortId(string id) { return id == "completed" ? "completed" : "branch:" + id; }
        // 旧配置保留 completed 连线；显式配置 branches 后只生成动态端口。
        public static IReadOnlyList<UiBranch> Read(IReadOnlyDictionary<string, object> properties)
        {
            object value;
            if (!properties.TryGetValue(UiNodeSchema.BranchesProperty, out value) || value == null)
                return Array.AsReadOnly(new[] { new UiBranch("completed", "交互完成") });
            return Array.AsReadOnly(((IList<object>)value).Cast<IDictionary<string, object>>()
                .Select(item => new UiBranch((string)item["id"], (string)item["name"])).ToArray());
        }
        public static bool IsValid(object value)
        {
            if (value == null) return true; // 旧图的缺省值。
            var list = value as IList<object>;
            if (list == null) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in list)
            {
                var map = item as IDictionary<string, object>;
                object id, name;
                if (map == null || map.Count != 2 || !map.TryGetValue("id", out id) || !(id is string)
                    || string.IsNullOrWhiteSpace((string)id) || !ids.Add((string)id)
                    || !map.TryGetValue("name", out name) || !(name is string)) return false;
            }
            return true;
        }
    }
}
