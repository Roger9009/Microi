using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Microi.net.Business
{
    /// <summary>
    /// 解析业务实体的主表/明细表/扩展表关系。
    /// 通过扫描已加载程序集中带 [BusinessTable] 的实体及其关系特性构建映射，结果缓存。
    /// </summary>
    public static class BusinessRelationResolver
    {
        private sealed class Cache
        {
            public Dictionary<string, Type> TableToType;     // 表名(小写) → 实体类型
            public HashSet<string> NonMasterTables;          // 作为他人明细/扩展的表名(小写)
        }

        private static Cache _cache;
        private static readonly object _lock = new object();

        /// <summary>强制重建缓存（新增模块/热加载后调用）。</summary>
        public static void Reset() { lock (_lock) { _cache = null; } }

        private static Cache Get()
        {
            if (_cache != null) return _cache;
            lock (_lock)
            {
                if (_cache != null) return _cache;

                var tableToType = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
                var nonMaster = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

                    foreach (var t in types)
                    {
                        var tableAttr = t.GetCustomAttribute<BusinessTableAttribute>();
                        if (tableAttr == null) continue;
                        tableToType[tableAttr.Name] = t;

                        foreach (var d in t.GetCustomAttributes<BusinessDetailTableAttribute>())
                        {
                            var name = GetTableName(d.EntityType);
                            if (name != null) nonMaster.Add(name);
                        }
                        foreach (var e in t.GetCustomAttributes<BusinessExtensionTableAttribute>())
                        {
                            var name = GetTableName(e.EntityType);
                            if (name != null) nonMaster.Add(name);
                        }
                    }
                }

                _cache = new Cache { TableToType = tableToType, NonMasterTables = nonMaster };
                return _cache;
            }
        }

        /// <summary>获取实体类型对应的表名（无 [BusinessTable] 返回 null）。</summary>
        public static string GetTableName(Type entityType)
        {
            return entityType?.GetCustomAttribute<BusinessTableAttribute>()?.Name;
        }

        /// <summary>按表名获取实体类型。</summary>
        public static Type GetTypeByTable(string tableName)
        {
            if (string.IsNullOrWhiteSpace(tableName)) return null;
            return Get().TableToType.TryGetValue(tableName, out var t) ? t : null;
        }

        /// <summary>列出所有"文档主表"（不作为他人明细/扩展、且非内部表的业务表）。</summary>
        public static IReadOnlyList<Type> ListMasterTypes()
        {
            var c = Get();
            return c.TableToType
                .Where(kv => !c.NonMasterTables.Contains(kv.Key))
                .Where(kv => !(kv.Value.GetCustomAttribute<BusinessTableAttribute>()?.Internal ?? false))
                .Select(kv => kv.Value)
                .Distinct()
                .ToList();
        }

        /// <summary>获取主表的明细表关系。</summary>
        public static IReadOnlyList<BusinessDetailTableAttribute> GetDetails(Type masterType)
        {
            return masterType?.GetCustomAttributes<BusinessDetailTableAttribute>().ToList()
                ?? new List<BusinessDetailTableAttribute>();
        }

        /// <summary>获取主表的扩展表关系。</summary>
        public static IReadOnlyList<BusinessExtensionTableAttribute> GetExtensions(Type masterType)
        {
            return masterType?.GetCustomAttributes<BusinessExtensionTableAttribute>().ToList()
                ?? new List<BusinessExtensionTableAttribute>();
        }
    }
}
