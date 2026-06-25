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
            if (masterType == null) return new DosResult(0, null, "主表实体类型不能为空。");

            var id = masterData["Id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id))
                return new DosResult(0, null, "缺少主单 Id，无法保存关联表。");

            // 1) 保存扩展表（1:1，同 Id）
            foreach (var ext in BusinessRelationResolver.GetExtensions(masterType))
            {
                var extTable = BusinessRelationResolver.GetTableName(ext.EntityType);
                if (string.IsNullOrWhiteSpace(extTable)) continue;

                var extObj = BuildExtensionObject(masterData, extTable, id, osClient);
                if (extObj == null || extObj.Count == 0) continue;

                var extExists = await ExistsAsync(extTable, id, osClient, trans);
                var extResult = extExists
                    ? await FormEngine.UptFormDataAsync(extTable, extObj, trans)
                    : await FormEngine.AddFormDataAsync(extTable, extObj, trans);

                if (extResult == null || extResult.Code != 1)
                    return extResult ?? new DosResult(0, null, $"扩展表[{extTable}]保存失败。");
            }

            // 2) 保存明细表（1:N）
            foreach (var detail in BusinessRelationResolver.GetDetails(masterType))
            {
                var detailTable = BusinessRelationResolver.GetTableName(detail.EntityType);
                if (string.IsNullOrWhiteSpace(detailTable) || string.IsNullOrWhiteSpace(detail.ForeignKey)) continue;

                var propName = detail.PropertyName ?? detailTable;
                var incoming = masterData[propName] as JArray ?? new JArray();
                var detailResult = await SyncDetailTableAsync(incoming, detailTable, detail.ForeignKey, id, osClient, trans);
                if (detailResult == null || detailResult.Code != 1)
                    return detailResult ?? new DosResult(0, null, $"明细表[{detailTable}]保存失败。");
            }

            return new DosResult(1, null, "关联表保存成功。");
        }

        #region 私有

        private static JObject BuildExtensionObject(JObject master, string extTable, string id, string osClient)
        {
            var sysFields = DefaultFields;
            var columns = GetColumnNames(extTable, osClient);
            if (columns == null || columns.Count == 0) return null;

            var extObj = new JObject();
            extObj["Id"] = id;
            extObj["OsClient"] = osClient;

            foreach (var col in columns)
            {
                if (sysFields.Contains(col) || string.Equals(col, "Id", StringComparison.OrdinalIgnoreCase)) continue;
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
    }
}
