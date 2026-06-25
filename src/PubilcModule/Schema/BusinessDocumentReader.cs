using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dos.ORM;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务文档读取器：将扩展表(1:1)的列合并进主对象，并加载明细表(1:N)集合。
    /// </summary>
    public static class BusinessDocumentReader
    {
        private static IFormEngine FormEngine => MicroiEngine.FormEngine;

        /// <summary>
        /// 用主表实体类型声明的关系，丰富主对象：合并扩展表列 + 挂载明细集合。
        /// </summary>
        /// <param name="master">已加载的主表 JObject（须含 Id）</param>
        /// <param name="masterType">主表实体类型（带关系特性）</param>
        /// <param name="osClient">租户</param>
        /// <param name="trans">可选共享事务</param>
        public static async Task<JObject> EnrichAsync(JObject master, Type masterType, string osClient, DbTrans trans = null)
        {
            if (master == null || masterType == null) return master;
            var id = master["Id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id)) return master;

            var sysFields = new HashSet<string>(DiyCommon.DefaultFields, StringComparer.OrdinalIgnoreCase);

            // 1) 合并扩展表列（同 Id 一对一）
            foreach (var ext in BusinessRelationResolver.GetExtensions(masterType))
            {
                var extTable = BusinessRelationResolver.GetTableName(ext.EntityType);
                if (string.IsNullOrWhiteSpace(extTable)) continue;

                var extResult = await FormEngine.GetFormDataAsync(extTable, new { Id = id, OsClient = osClient }, trans);
                if (extResult == null || extResult.Code != 1 || extResult.Data == null) continue;

                var extObj = extResult.Data as JObject ?? JObject.FromObject(extResult.Data);
                foreach (var prop in extObj.Properties())
                {
                    if (sysFields.Contains(prop.Name)) continue;     // 不覆盖系统字段
                    if (master[prop.Name] != null) continue;         // 主表已有则不覆盖
                    master[prop.Name] = prop.Value;
                }
            }

            // 2) 加载明细集合（一对多）
            foreach (var detail in BusinessRelationResolver.GetDetails(masterType))
            {
                var detailTable = BusinessRelationResolver.GetTableName(detail.EntityType);
                if (string.IsNullOrWhiteSpace(detailTable) || string.IsNullOrWhiteSpace(detail.ForeignKey)) continue;

                var listResult = await FormEngine.GetTableDataAsync(detailTable, new
                {
                    OsClient = osClient,
                    _Where = new object[] { new object[] { detail.ForeignKey, "=", id } },
                    _PageSize = 100000
                }, trans);

                var propName = detail.PropertyName ?? detailTable;
                master[propName] = listResult?.Data != null
                    ? JArray.FromObject(listResult.Data)
                    : new JArray();
            }

            return master;
        }
    }
}
