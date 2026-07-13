using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microi.net.Business
{
    /// <summary>
    /// 字段配置内存缓存。用于更新时快速取得「非更新字段」列表（IsUpdate=false），
    /// 以便通过 _NotSaveField 跳过这些字段。保存配置时失效对应缓存。
    /// </summary>
    public static class BusinessFieldConfigCache
    {
        private static readonly ConcurrentDictionary<string, List<BusinessFieldConfig>> _cache
            = new ConcurrentDictionary<string, List<BusinessFieldConfig>>();

        private static string Key(string osClient, string table) => (osClient ?? "") + "|" + (table ?? "").ToLowerInvariant();

        /// <summary>使指定表的配置缓存失效。</summary>
        public static void Invalidate(string osClient, string table)
        {
            _cache.TryRemove(Key(osClient, table), out _);
        }

        /// <summary>清空全部缓存。</summary>
        public static void Clear() => _cache.Clear();

        /// <summary>获取某表「不参与更新」的字段名集合（IsUpdate=false）。</summary>
        public static async Task<HashSet<string>> GetNonUpdatableFields(string table, string osClient)
        {
            var configs = await GetConfigs(table, osClient);
            return new HashSet<string>(
                configs.Where(c => c.IsUpdate == false).Select(c => c.FieldName),
                StringComparer.OrdinalIgnoreCase);
        }

        private static async Task<List<BusinessFieldConfig>> GetConfigs(string table, string osClient)
        {
            var key = Key(osClient, table);
            if (_cache.TryGetValue(key, out var cached)) return cached;

            var result = await MicroiEngine.FormEngine.GetTableDataAsync<BusinessFieldConfig>("business_field_config", new
            {
                OsClient = osClient,
                _Where = new object[] { new object[] { "TableName", "=", table } },
                _PageSize = 100000
            });

            var list = result?.Data ?? new List<BusinessFieldConfig>();
            _cache[key] = list;
            return list;
        }
    }
}
