using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dos.Common;
using Dos.ORM;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务文档写入器：保存主单时，同步落库一对一扩展表与一对多明细表。
    /// 与 <see cref="BusinessDocumentReader"/> 对应，二者共同完成主-细-扩展表的读写闭环。
    /// </summary>
    public static class BusinessDocumentWriter
    {
        private static IFormEngine FormEngine => MicroiEngine.FormEngine;
        private static readonly HashSet<string> DefaultFields = new HashSet<string>(DiyCommon.DefaultFields, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 保存业务文档：主单 → 扩展表(1:1) → 明细表(1:N) 同步落库。
        /// 主单新增/更新由 <paramref name="masterData"/> 中 Id 是否为空决定；扩展表按同 Id upsert；
        /// 明细表按传入集合做 insert/update/delete 同步。
        /// 适用于不需要执行 BusinessServiceBase 生命周期钩子的场景。
        /// </summary>
        /// <param name="masterData">主单数据（JObject，可含扩展字段与明细集合）</param>
        /// <param name="masterType">主表实体类型（带 [BusinessTable] 及关系特性）</param>
        /// <param name="masterTable">主表名</param>
        /// <param name="osClient">租户</param>
        /// <param name="trans">共享事务</param>
        public static async Task<DosResult> SaveAsync(JObject masterData, Type masterType, string masterTable, string osClient, DbTrans trans = null)
        {
            if (masterData == null) return new DosResult(0, null, "主单数据不能为空。");
            if (masterType == null) return new DosResult(0, null, "主表实体类型不能为空。");

            masterData["OsClient"] = osClient;
            var id = masterData["Id"]?.ToString();
            var isNew = string.IsNullOrWhiteSpace(id);

            var masterResult = isNew
                ? await FormEngine.AddFormDataAsync(masterTable, masterData, trans)
                : await FormEngine.UptFormDataAsync(masterTable, masterData, trans);

            if (masterResult == null || masterResult.Code != 1)
                return masterResult ?? new DosResult(0, null, "主单保存失败。");

            var masterObj = masterResult.Data as JObject ?? JObject.FromObject(masterResult.Data);
            id = masterObj["Id"]?.ToString() ?? id;
            if (string.IsNullOrWhiteSpace(id))
                return new DosResult(0, null, "主单保存后未返回 Id。");

            masterData["Id"] = id;
            var relationsResult = await SaveRelationsAsync(masterData, masterType, masterTable, osClient, trans);
            if (relationsResult != null && relationsResult.Code != 1)
                return relationsResult;

            return masterResult;
        }

        /// <summary>
        /// 仅保存扩展表与明细表，不保存主单。
        /// 主单已保存后，用此方法来同步关系表。
        /// </summary>
        public static async Task<DosResult> SaveRelationsAsync(JObject masterData, Type masterType, string masterTable, string osClient, DbTrans trans = null)
        {
            if (masterData == null) return new DosResult(0, null, "主单数据不能为空。");

            var id = masterData["Id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id))
                return new DosResult(0, null, "缺少主单 Id，无法保存关联表。");

            // ── 静态扩展表（代码特性）──
            if (masterType != null)
            {
                foreach (var ext in BusinessRelationResolver.GetExtensions(masterType))
                {
                    var extTable = BusinessRelationResolver.GetTableName(ext.EntityType);
                    if (string.IsNullOrWhiteSpace(extTable)) continue;
                    var r = await UpsertExtensionAsync(masterData, extTable, masterTable, id, osClient, trans);
                    if (r != null && r.Code != 1) return r;
                }

                foreach (var detail in BusinessRelationResolver.GetDetails(masterType))
                {
                    var detailTable = BusinessRelationResolver.GetTableName(detail.EntityType);
                    if (string.IsNullOrWhiteSpace(detailTable) || string.IsNullOrWhiteSpace(detail.ForeignKey)) continue;
                    var propName = detail.PropertyName ?? detailTable;
                    var incoming = masterData[propName] as JArray ?? new JArray();
                    var r = await SyncDetailTableAsync(incoming, detailTable, detail.ForeignKey, id, osClient, trans);
                    if (r == null || r.Code != 1) return r ?? new DosResult(0, null, $"明细表[{detailTable}]保存失败。");
                }
            }

            // ── 动态关系（business_doc_relation）──
            if (!string.IsNullOrWhiteSpace(masterTable))
            {
                var dynamicRels = await new BusinessDocRelationService().GetRelationsAsync(masterTable, osClient);

                var staticExtNames = masterType != null
                    ? new HashSet<string>(BusinessRelationResolver.GetExtensions(masterType)
                        .Select(e => BusinessRelationResolver.GetTableName(e.EntityType))
                        .Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var staticDetailNames = masterType != null
                    ? new HashSet<string>(BusinessRelationResolver.GetDetails(masterType)
                        .Select(d => BusinessRelationResolver.GetTableName(d.EntityType))
                        .Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var rel in dynamicRels)
                {
                    if (string.IsNullOrWhiteSpace(rel.RelationTable)) continue;

                    if (string.Equals(rel.RelationType, "Extension", StringComparison.OrdinalIgnoreCase)
                        && !staticExtNames.Contains(rel.RelationTable))
                    {
                        var r = await UpsertExtensionAsync(masterData, rel.RelationTable, masterTable, id, osClient, trans);
                        if (r != null && r.Code != 1) return r;
                    }
                    else if (string.Equals(rel.RelationType, "Detail", StringComparison.OrdinalIgnoreCase)
                        && !staticDetailNames.Contains(rel.RelationTable)
                        && !string.IsNullOrWhiteSpace(rel.ForeignKey))
                    {
                        var propName = rel.PropertyName ?? rel.RelationTable;
                        var incoming = masterData[propName] as JArray ?? new JArray();
                        var r = await SyncDetailTableAsync(incoming, rel.RelationTable, rel.ForeignKey, id, osClient, trans);
                        if (r == null || r.Code != 1) return r ?? new DosResult(0, null, $"动态明细表[{rel.RelationTable}]保存失败。");
                    }
                }
            }

            return new DosResult(1, null, "关联表保存成功。");
        }

        private static async Task<DosResult> UpsertExtensionAsync(JObject masterData, string extTable, string masterTable, string id, string osClient, DbTrans trans)
        {
            var extObj = BuildExtensionObject(masterData, extTable, masterTable, id, osClient);
            if (extObj == null || extObj.Count == 0) return null;
            var extExists = await ExistsAsync(extTable, id, osClient, trans);
            return extExists
                ? await FormEngine.UptFormDataAsync(extTable, extObj, trans)
                : await FormEngine.AddFormDataAsync(extTable, extObj, trans);
        }

        #region 私有

        private static JObject BuildExtensionObject(JObject master, string extTable, string masterTable, string id, string osClient)
        {
            var sysFields = DefaultFields;
            var extColumns = GetColumnNames(extTable, osClient);
            if (extColumns == null || extColumns.Count == 0) return null;

            // 主表列名白名单：扩展表中与主表同名的列不写入（防止覆盖主表逻辑字段）
            var masterColumns = GetColumnNames(masterTable, osClient);
            var masterColSet = masterColumns != null
                ? new HashSet<string>(masterColumns, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var extObj = new JObject();
            extObj["Id"] = id;
            extObj["OsClient"] = osClient;

            foreach (var col in extColumns)
            {
                if (sysFields.Contains(col) || string.Equals(col, "Id", StringComparison.OrdinalIgnoreCase)) continue;
                // 跳过与主表同名的列，避免将主表字段误写入扩展表
                if (masterColSet.Contains(col)) continue;
                if (master[col] != null)
                {
                    extObj[col] = master[col];
                }
            }

            return extObj;
        }

        private static List<string> GetColumnNames(string tableName, string osClient)
        {
            var schema = new BusinessSchemaService();
            var result = schema.GetTableColumns(tableName, osClient);
            return result?.Data?.Select(c => c.Name).ToList();
        }

        private static async Task<DosResult> SyncDetailTableAsync(JArray incoming, string tableName, string foreignKey, string masterId, string osClient, DbTrans trans)
        {
            var sysFields = new HashSet<string>(DefaultFields, StringComparer.OrdinalIgnoreCase);

            var existingResult = await FormEngine.GetTableDataAsync(tableName, new
            {
                OsClient = osClient,
                _Where = new object[] { new object[] { foreignKey, "=", masterId } },
                _PageSize = 100000
            }, trans);

            var existingIds = new HashSet<string>(
                ((existingResult?.Data as IEnumerable<dynamic>)?.Select(x => (x as JObject)?["Id"]?.ToString()).Where(x => !string.IsNullOrWhiteSpace(x)) ?? new List<string>()),
                StringComparer.OrdinalIgnoreCase);

            var savedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in incoming)
            {
                var rowObj = row as JObject;
                if (rowObj == null) continue;

                rowObj[foreignKey] = masterId;
                rowObj["OsClient"] = osClient;

                foreach (var sys in sysFields)
                {
                    if (rowObj[sys] == null) continue;
                    // 保留系统字段，不删除
                }

                var rowId = rowObj["Id"]?.ToString();
                if (string.IsNullOrWhiteSpace(rowId) || !existingIds.Contains(rowId))
                {
                    if (string.IsNullOrWhiteSpace(rowId)) rowObj.Remove("Id");
                    var r = await FormEngine.AddFormDataAsync(tableName, rowObj, trans);
                    if (r == null || r.Code != 1) return r;
                    var rowObjResult = r.Data as JObject ?? JObject.FromObject(r.Data);
                    if (rowObjResult["Id"] != null) savedIds.Add(rowObjResult["Id"].ToString());
                }
                else
                {
                    var r = await FormEngine.UptFormDataAsync(tableName, rowObj, trans);
                    if (r == null || r.Code != 1) return r;
                    savedIds.Add(rowId);
                }
            }

            // 删除传入集合中不存在的旧行
            var toDelete = existingIds.Where(x => !savedIds.Contains(x)).ToList();
            foreach (var delId in toDelete)
            {
                var r = await FormEngine.DelFormDataAsync(tableName, new { Id = delId, OsClient = osClient }, trans);
                if (r == null || r.Code != 1) return r;
            }

            return new DosResult(1, null, $"明细表[{tableName}]保存成功。");
        }

        private static async Task<bool> ExistsAsync(string tableName, string id, string osClient, DbTrans trans)
        {
            var r = await FormEngine.GetFormDataAsync(tableName, new { Id = id, OsClient = osClient }, trans);
            return r?.Code == 1 && r.Data != null;
        }

        #endregion

        /// <summary>
        /// 删除主单时级联清理扩展表（1:1）与明细表（1:N），防止产生孤儿数据。
        /// 在 <see cref="BusinessServiceBase{TParam}.DelAsync"/> 的 OnAfterDelAsync 中调用。
        /// </summary>
        /// <param name="masterId">主单 Id</param>
        /// <param name="masterType">主表实体类型（带关系特性）</param>
        /// <param name="osClient">租户</param>
        /// <param name="trans">共享事务</param>
        public static async Task<DosResult> DeleteRelationsAsync(string masterId, Type masterType, string osClient, DbTrans trans = null,
            string masterTable = null)
        {
            if (string.IsNullOrWhiteSpace(masterId)) return new DosResult(0, null, "缺少主单 Id，无法清理关联表。");

            // ── 静态扩展表（代码特性）──
            if (masterType != null)
            {
                foreach (var ext in BusinessRelationResolver.GetExtensions(masterType))
                {
                    var r = await DeleteExtRowAsync(BusinessRelationResolver.GetTableName(ext.EntityType), masterId, osClient, trans);
                    if (r != null && r.Code != 1) return r;
                }
                foreach (var detail in BusinessRelationResolver.GetDetails(masterType))
                {
                    var r = await DeleteDetailRowsAsync(
                        BusinessRelationResolver.GetTableName(detail.EntityType), detail.ForeignKey, masterId, osClient, trans);
                    if (r != null && r.Code != 1) return r;
                }
            }

            // ── 动态关系（business_doc_relation）──
            var tbl = masterTable ?? (masterType != null ? BusinessRelationResolver.GetTableName(masterType) : null);
            if (!string.IsNullOrWhiteSpace(tbl))
            {
                var staticExtNames = masterType != null
                    ? new HashSet<string>(BusinessRelationResolver.GetExtensions(masterType)
                        .Select(e => BusinessRelationResolver.GetTableName(e.EntityType))
                        .Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var staticDetailNames = masterType != null
                    ? new HashSet<string>(BusinessRelationResolver.GetDetails(masterType)
                        .Select(d => BusinessRelationResolver.GetTableName(d.EntityType))
                        .Where(t => !string.IsNullOrWhiteSpace(t)), StringComparer.OrdinalIgnoreCase)
                    : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                var dynamicRels = await new BusinessDocRelationService().GetRelationsAsync(tbl, osClient);
                foreach (var rel in dynamicRels)
                {
                    if (string.IsNullOrWhiteSpace(rel.RelationTable)) continue;
                    if (string.Equals(rel.RelationType, "Extension", StringComparison.OrdinalIgnoreCase)
                        && !staticExtNames.Contains(rel.RelationTable))
                    {
                        var r = await DeleteExtRowAsync(rel.RelationTable, masterId, osClient, trans);
                        if (r != null && r.Code != 1) return r;
                    }
                    else if (string.Equals(rel.RelationType, "Detail", StringComparison.OrdinalIgnoreCase)
                        && !staticDetailNames.Contains(rel.RelationTable)
                        && !string.IsNullOrWhiteSpace(rel.ForeignKey))
                    {
                        var r = await DeleteDetailRowsAsync(rel.RelationTable, rel.ForeignKey, masterId, osClient, trans);
                        if (r != null && r.Code != 1) return r;
                    }
                }
            }

            return new DosResult(1, null, "关联表级联删除完成。");
        }

        private static async Task<DosResult> DeleteExtRowAsync(string extTable, string id, string osClient, DbTrans trans)
        {
            if (string.IsNullOrWhiteSpace(extTable)) return null;
            var exists = await ExistsAsync(extTable, id, osClient, trans);
            if (!exists) return null;
            return await FormEngine.DelFormDataAsync(extTable, new { Id = id, OsClient = osClient }, trans);
        }

        private static async Task<DosResult> DeleteDetailRowsAsync(string detailTable, string foreignKey, string masterId, string osClient, DbTrans trans)
        {
            if (string.IsNullOrWhiteSpace(detailTable) || string.IsNullOrWhiteSpace(foreignKey)) return null;
            var listResult = await FormEngine.GetTableDataAsync(detailTable, new
            {
                OsClient = osClient,
                _Where = new object[] { new object[] { foreignKey, "=", masterId } },
                _PageSize = 100000
            }, trans);
            var rows = listResult?.Data as IEnumerable<dynamic>;
            if (rows == null) return null;
            foreach (var row in rows)
            {
                var rowObj = row as JObject ?? JObject.FromObject(row);
                var rowId = rowObj["Id"]?.ToString();
                if (string.IsNullOrWhiteSpace(rowId)) continue;
                var r = await FormEngine.DelFormDataAsync(detailTable, new { Id = rowId, OsClient = osClient }, trans);
                if (r != null && r.Code != 1) return r;
            }
            return null;
        }
    }
}
