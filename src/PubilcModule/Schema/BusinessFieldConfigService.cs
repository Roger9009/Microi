using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dos.Common;
using Dos.ORM;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务字段配置服务：解析字段（物理列 + 配置）、保存/删除字段配置。
    /// 配置存储于内部表 business_field_config，按 (TableName, FieldName) 唯一。
    /// </summary>
    public sealed class BusinessFieldConfigService
    {
        private const string ConfigTable = "business_field_config";
        private IFormEngine FormEngine => MicroiEngine.FormEngine;
        private readonly BusinessSchemaService _schemaService = new BusinessSchemaService();

        /// <summary>获取某表已存在的字段配置记录。</summary>
        public async Task<DosResultList<BusinessFieldConfig>> GetConfigs(string tableName, string osClient, DbTrans trans = null)
        {
            return await QueryConfigs(tableName, osClient, trans);
        }

        /// <summary>
        /// 解析某表的字段定义：物理列与配置合并，缺配置的列填充默认值，便于前端直接编辑。
        /// 同时附加无物理列的虚拟/关联字段（SourceType != Physical）。
        /// </summary>
        public async Task<DosResultList<BusinessFieldDef>> GetResolvedFields(string tableName, string osClient, DbTrans trans = null)
        {
            if (string.IsNullOrWhiteSpace(tableName))
                return new DosResultList<BusinessFieldDef>(0, null) { Msg = "TableName 不能为空。" };

            var columnsResult = _schemaService.GetTableColumns(tableName, osClient);
            var columns = columnsResult?.Data ?? new List<BusinessColumnInfo>();

            var configResult = await QueryConfigs(tableName, osClient, trans);
            var configs = configResult?.Data ?? new List<BusinessFieldConfig>();
            var configMap = configs
                .GroupBy(c => c.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var defs = new List<BusinessFieldDef>();
            var handled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int ordinal = 0;

            foreach (var col in columns)
            {
                configMap.TryGetValue(col.Name, out var cfg);
                defs.Add(BuildDef(col, cfg, ordinal++));
                handled.Add(col.Name);
            }

            // 虚拟/关联字段（仅有配置、无物理列）
            foreach (var cfg in configs.Where(c => !handled.Contains(c.FieldName)))
            {
                defs.Add(BuildDef(null, cfg, ordinal++));
            }

            return new DosResultList<BusinessFieldDef>(1, defs.OrderBy(d => d.SortNo).ToList());
        }

        /// <summary>批量保存字段配置（按 TableName+FieldName upsert）。</summary>
        public async Task<DosResult> SaveConfigs(BusinessFieldConfigSaveParam param, DbTrans trans = null)
        {
            if (string.IsNullOrWhiteSpace(param?.TableName) || param.Fields == null || param.Fields.Count == 0)
                return new DosResult(0, null, "TableName 与 Fields 不能为空。");

            var existResult = await QueryConfigs(param.TableName, param.OsClient, trans);
            var existMap = (existResult?.Data ?? new List<BusinessFieldConfig>())
                .GroupBy(c => c.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            int added = 0, updated = 0;
            foreach (var f in param.Fields)
            {
                if (string.IsNullOrWhiteSpace(f.FieldName)) continue;
                f.TableName = param.TableName;

                if (existMap.TryGetValue(f.FieldName, out var exist))
                {
                    var uptParam = new
                    {
                        OsClient = param.OsClient,
                        Id = exist.Id,
                        f.Description, f.LangId, f.FieldType, f.SourceType, f.Component,
                        f.IsPrimaryKey, f.IsUpdate, f.ForceHidden, f.DefaultVisible, f.Required, f.SortNo
                    };
                    var r = await FormEngine.UptFormDataAsync(ConfigTable, uptParam, trans);
                    if (r != null && r.Code == 1) updated++;
                }
                else
                {
                    var addParam = new
                    {
                        OsClient = param.OsClient,
                        f.TableName, f.FieldName, f.Description, f.LangId, f.FieldType, f.SourceType, f.Component,
                        f.IsPrimaryKey, f.IsUpdate, f.ForceHidden, f.DefaultVisible, f.Required, f.SortNo
                    };
                    var r = await FormEngine.AddFormDataAsync(ConfigTable, addParam, trans);
                    if (r != null && r.Code == 1) added++;
                }
            }

            BusinessFieldConfigCache.Invalidate(param.OsClient, param.TableName);
            return new DosResult(1, new { added, updated }, $"已保存：新增 {added}，更新 {updated}。");
        }

        /// <summary>删除某字段的配置。</summary>
        public async Task<DosResult> DeleteConfig(string tableName, string fieldName, string osClient, DbTrans trans = null)
        {
            if (string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(fieldName))
                return new DosResult(0, null, "TableName 与 FieldName 不能为空。");

            var existResult = await QueryConfigs(tableName, osClient, trans);
            var target = (existResult?.Data ?? new List<BusinessFieldConfig>())
                .FirstOrDefault(c => string.Equals(c.FieldName, fieldName, StringComparison.OrdinalIgnoreCase));
            if (target == null) return new DosResult(1, null, "无配置可删除。");

            var r = await FormEngine.DelFormDataAsync(ConfigTable, new { OsClient = osClient, Id = target.Id }, trans);
            BusinessFieldConfigCache.Invalidate(osClient, tableName);
            return r ?? new DosResult(0, null, "删除失败。");
        }

        #region 私有

        private async Task<DosResultList<BusinessFieldConfig>> QueryConfigs(string tableName, string osClient, DbTrans trans)
        {
            return await FormEngine.GetTableDataAsync<BusinessFieldConfig>(ConfigTable, new
            {
                OsClient = osClient,
                _Where = new object[] { new object[] { "TableName", "=", tableName } },
                _PageSize = 100000
            }, trans);
        }

        private static BusinessFieldDef BuildDef(BusinessColumnInfo col, BusinessFieldConfig cfg, int ordinal)
        {
            var name = col?.Name ?? cfg?.FieldName;
            var isSystem = col?.IsSystem ?? false;
            var inferredType = InferFieldType(col);

            return new BusinessFieldDef
            {
                Name = name,
                PhysicalExists = col != null,
                DataType = col?.DataType,
                ColumnType = col?.ColumnType,
                PhysicalComment = col?.Comment,
                IsSystem = isSystem,

                ConfigId = cfg?.Id,
                HasConfig = cfg != null,

                Description = cfg?.Description ?? (string.IsNullOrWhiteSpace(col?.Comment) ? name : col.Comment),
                LangId = cfg?.LangId ?? "",
                FieldType = cfg?.FieldType ?? inferredType,
                SourceType = cfg?.SourceType ?? "Physical",
                Component = cfg?.Component ?? InferComponent(cfg?.FieldType ?? inferredType),
                IsPrimaryKey = cfg?.IsPrimaryKey ?? (col?.IsPrimaryKey ?? false),
                IsUpdate = cfg?.IsUpdate ?? !isSystem,
                ForceHidden = cfg?.ForceHidden ?? false,
                DefaultVisible = cfg?.DefaultVisible ?? !isSystem,
                Required = cfg?.Required ?? false,
                SortNo = cfg?.SortNo ?? ordinal
            };
        }

        private static string InferFieldType(BusinessColumnInfo col)
        {
            var dt = (col?.DataType ?? "").ToLowerInvariant();
            if (dt.Contains("char") || dt.Contains("varchar2")) return "string";
            if (dt.Contains("text") || dt.Contains("clob")) return "text";
            if (dt == "int" || dt == "integer" || dt.Contains("tinyint") || dt.Contains("smallint")) return "int";
            if (dt.Contains("bigint") || dt == "number") return "long";
            if (dt.Contains("decimal") || dt.Contains("numeric")) return "decimal";
            if (dt.Contains("double") || dt.Contains("float") || dt.Contains("real")) return "double";
            if (dt == "bit" || dt == "boolean") return "bool";
            if (dt.Contains("date") || dt.Contains("time")) return "datetime";
            return "string";
        }

        private static string InferComponent(string fieldType)
        {
            switch ((fieldType ?? "string").ToLowerInvariant())
            {
                case "text": return "Textarea";
                case "int":
                case "long":
                case "decimal":
                case "double": return "NumberText";
                case "bool": return "Switch";
                case "datetime": return "DateTimePicker";
                default: return "Text";
            }
        }

        #endregion
    }
}
