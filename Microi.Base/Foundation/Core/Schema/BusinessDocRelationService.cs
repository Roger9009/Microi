using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dos.Common;
using Dos.ORM;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务文档动态关系服务。
    /// 管理通过前端创建的主表 → 扩展表/明细表动态绑定，与代码特性声明的关系合并使用。
    /// </summary>
    public sealed class BusinessDocRelationService
    {
        private const string RelationTable = "business_doc_relation";
        private static IFormEngine FormEngine => MicroiEngine.FormEngine;

        /// <summary>获取某主表的所有动态关系（带内存缓存）。</summary>
        public async Task<List<BusinessDocRelation>> GetRelationsAsync(string masterTable, string osClient)
        {
            if (string.IsNullOrWhiteSpace(masterTable)) return new List<BusinessDocRelation>();

            var cacheKey = BuildCacheKey(osClient, masterTable);
            var cached = BusinessDocRelationCache.Get(cacheKey);
            if (cached != null) return cached;

            var result = await FormEngine.GetTableDataAsync<BusinessDocRelation>(RelationTable, new
            {
                OsClient = osClient,
                _Where = new object[] { new object[] { "MasterTable", "=", masterTable } },
                _PageSize = 1000
            });
            var list = result?.Data ?? new List<BusinessDocRelation>();
            BusinessDocRelationCache.Set(cacheKey, list);
            return list;
        }

        /// <summary>绑定一个扩展表到主表（1:1 Extension）。</summary>
        public async Task<DosResult> BindExtensionAsync(string masterTable, string extTable, string label, string osClient)
        {
            if (string.IsNullOrWhiteSpace(masterTable) || string.IsNullOrWhiteSpace(extTable))
                return new DosResult(0, null, "MasterTable 与 RelationTable 不能为空。");

            // 检查是否已绑定
            var existing = await GetRelationsAsync(masterTable, osClient);
            if (existing.Any(r => string.Equals(r.RelationTable, extTable, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(r.RelationType, "Extension", StringComparison.OrdinalIgnoreCase)))
                return new DosResult(1, null, "扩展表已绑定，无需重复操作。");

            var r = await FormEngine.AddFormDataAsync(RelationTable, new
            {
                OsClient = osClient,
                MasterTable = masterTable,
                RelationTable = extTable,
                RelationType = "Extension",
                Label = label ?? extTable
            });
            if (r?.Code == 1) BusinessDocRelationCache.Invalidate(BuildCacheKey(osClient, masterTable));
            return r ?? new DosResult(0, null, "绑定失败。");
        }

        /// <summary>绑定一个明细表到主表（1:N Detail）。</summary>
        public async Task<DosResult> BindDetailAsync(string masterTable, string detailTable,
            string foreignKey, string propertyName, string label, string osClient)
        {
            if (string.IsNullOrWhiteSpace(masterTable) || string.IsNullOrWhiteSpace(detailTable)
                || string.IsNullOrWhiteSpace(foreignKey))
                return new DosResult(0, null, "MasterTable、RelationTable、ForeignKey 不能为空。");

            var existing = await GetRelationsAsync(masterTable, osClient);
            if (existing.Any(r => string.Equals(r.RelationTable, detailTable, StringComparison.OrdinalIgnoreCase)
                               && string.Equals(r.RelationType, "Detail", StringComparison.OrdinalIgnoreCase)))
                return new DosResult(1, null, "明细表已绑定。");

            var r = await FormEngine.AddFormDataAsync(RelationTable, new
            {
                OsClient = osClient,
                MasterTable = masterTable,
                RelationTable = detailTable,
                RelationType = "Detail",
                ForeignKey = foreignKey,
                PropertyName = propertyName ?? detailTable,
                Label = label ?? detailTable
            });
            if (r?.Code == 1) BusinessDocRelationCache.Invalidate(BuildCacheKey(osClient, masterTable));
            return r ?? new DosResult(0, null, "绑定失败。");
        }

        /// <summary>解除绑定（按关系记录 Id）。</summary>
        public async Task<DosResult> UnbindAsync(string relationId, string masterTable, string osClient)
        {
            if (string.IsNullOrWhiteSpace(relationId))
                return new DosResult(0, null, "relationId 不能为空。");

            var r = await FormEngine.DelFormDataAsync(RelationTable, new { Id = relationId, OsClient = osClient });
            if (r?.Code == 1) BusinessDocRelationCache.Invalidate(BuildCacheKey(osClient, masterTable));
            return r ?? new DosResult(0, null, "解除绑定失败。");
        }

        private static string BuildCacheKey(string osClient, string masterTable)
            => $"{(osClient ?? "").ToLowerInvariant()}|{(masterTable ?? "").ToLowerInvariant()}";
    }

    /// <summary>动态关系内存缓存（进程内，写后失效）。</summary>
    internal static class BusinessDocRelationCache
    {
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, List<BusinessDocRelation>>
            _store = new System.Collections.Concurrent.ConcurrentDictionary<string, List<BusinessDocRelation>>(StringComparer.OrdinalIgnoreCase);

        public static List<BusinessDocRelation> Get(string key)
            => _store.TryGetValue(key, out var v) ? v : null;

        public static void Set(string key, List<BusinessDocRelation> value)
            => _store[key] = value;

        public static void Invalidate(string key)
            => _store.TryRemove(key, out _);

        public static void InvalidateAll()
            => _store.Clear();
    }
}
