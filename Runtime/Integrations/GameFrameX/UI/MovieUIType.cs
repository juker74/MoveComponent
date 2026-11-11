using System;

namespace Game.Framework.UI
{
    /// <summary>影视 UI 类型标识：命名空间.类名, 程序集简单名称，不包含程序集版本。</summary>
    public static class MovieUIType
    {
        public static bool IsSelectable(Type type)
        {
            return type != null && type.IsClass && !type.IsAbstract && !type.IsGenericType && !type.ContainsGenericParameters
                && typeof(MovieUIForm).IsAssignableFrom(type);
        }

        public static string GetId(Type type)
        {
            if (!IsSelectable(type)) throw new ArgumentException("请选择非抽象的 MovieUIForm 子类。", nameof(type));
            return type.FullName + ", " + type.Assembly.GetName().Name;
        }

        /// <summary>仅查找已加载的程序集。热更新程序集应在加载流程图之前加载。</summary>
        public static Type Resolve(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            var separator = id.IndexOf(',');
            if (separator <= 0) return null;
            var name = id.Substring(0, separator).Trim();
            var assemblyName = id.Substring(separator + 1).Trim();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly.GetName().Name != assemblyName) continue;
                try
                {
                    var type = assembly.GetType(name, false);
                    return IsSelectable(type) ? type : null;
                }
                catch (ArgumentException) { return null; }
                catch (TypeLoadException) { return null; }
            }
            return null;
        }
    }
}
