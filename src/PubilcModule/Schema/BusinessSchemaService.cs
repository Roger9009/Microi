using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dos.Common;
using Dos.ORM;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务表结构服务：查看主/细/扩展表结构、动态加字段。
    /// 复用平台多方言 DDL（IMicroiORM）。
    /// </summary>
    public sealed class BusinessSchemaService
    {
        private sealed class DbContext
        {
            public DbSession Session;
            public DbInfo Info;
            public IMicroiORM Ddl;
        }

        private DbContext GetContext(string osClient)
        {
            var client = OsClientExtend.GetClient(osClient);
            if (client == null || client.Db == null)
                throw new Exception($"租户[{osClient}]数据库会话不可用。");
            var info = DiyCommon.GetDbInfo(client.OsClientModel["DbType"].Val<string>());
            return new DbContext { Session = client.Db, Info = info, Ddl = MicroiEngine.ORM(info.DbType) };
        }

        /// <summary>列出所有业务文档（主表）。</summary>
        public DosResultList<dynamic> ListDocuments(string osClient)
        {
            var ctx = GetContext(osClient);
            var existing = GetExistingTables(ctx, osClient);

            var list = new List<dynamic>();
            foreach (var masterType in BusinessRelationResolver.ListMasterTypes())
            {
                var tableAttr = masterType.GetCustomAttribute<BusinessTableAttribute>();
                list.Add(new
                {
                    MasterTable = tableAttr.Name,
                    Label = tableAttr.Comment ?? masterType.Name,
                    Exists = existing.Contains(tableAttr.Name),
                    DetailCount = BusinessRelationResolver.GetDetails(masterType).Count,
                    ExtensionCount = BusinessRelationResolver.GetExtensions(masterType).Count
                });
            }
            return new DosResultList<dynamic>(1, list);
        }

        /// <summary>获取一个文档的完整结构（主表 + 明细 + 扩展，含实时列）。</summary>
        public DosResult<BusinessDocumentSchema> GetDocumentSchema(string masterTable, string osClient)
        {
            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            if (masterType == null)
                return new DosResult<BusinessDocumentSchema>(0, null, $"未找到业务表[{masterTable}]对应实体。");

            var ctx = GetContext(osClient);
            var existing = GetExistingTables(ctx, osClient);
            var masterAttr = masterType.GetCustomAttribute<BusinessTableAttribute>();

            var schema = new BusinessDocumentSchema
            {
                MasterTable = masterTable,
                Label = masterAttr.Comment ?? masterType.Name,
                Master = BuildTableInfo(ctx, masterTable, masterAttr.Comment ?? masterType.Name,
                    BusinessTableRole.Master, null, null, existing)
            };

            foreach (var d in BusinessRelationResolver.GetDetails(masterType))
            {
                var name = BusinessRelationResolver.GetTableName(d.EntityType);
                if (name == null) continue;
                var label = d.EntityType.GetCustomAttribute<BusinessTableAttribute>()?.Comment ?? d.EntityType.Name;
                schema.Details.Add(BuildTableInfo(ctx, name, label, BusinessTableRole.Detail,
                    d.ForeignKey, d.PropertyName ?? name, existing));
            }

            foreach (var e in BusinessRelationResolver.GetExtensions(masterType))
            {
                var name = BusinessRelationResolver.GetTableName(e.EntityType);
                if (name == null) continue;
                var label = e.EntityType.GetCustomAttribute<BusinessTableAttribute>()?.Comment ?? e.EntityType.Name;
                schema.Extensions.Add(BuildTableInfo(ctx, name, label, BusinessTableRole.Extension,
                    null, null, existing));
            }

            return new DosResult<BusinessDocumentSchema>(1, schema);
        }

        /// <summary>获取单表的列结构。</summary>
        public DosResultList<BusinessColumnInfo> GetTableColumns(string tableName, string osClient)
        {
            var ctx = GetContext(osClient);
            return new DosResultList<BusinessColumnInfo>(1, ReadColumns(ctx, tableName));
        }

        /// <summary>
        /// 动态加字段：把字段合并到指定目标表（主/细/扩展）。
        /// 扩展表不存在时会自动创建。
        /// </summary>
        public DosResult AddField(BusinessAddFieldParam param)
        {
            if (string.IsNullOrWhiteSpace(param?.MasterTable) || string.IsNullOrWhiteSpace(param.TargetTable)
                || string.IsNullOrWhiteSpace(param.FieldName))
                return new DosResult(0, null, "MasterTable、TargetTable、FieldName 不能为空。");

            if (!IsValidIdentifier(param.FieldName))
                return new DosResult(0, null, "字段名不合法，仅允许字母、数字、下划线，且不以数字开头。");

            // 校验目标表归属该文档（主表/它的明细/扩展之一）
            var masterType = BusinessRelationResolver.GetTypeByTable(param.MasterTable);
            if (masterType == null)
                return new DosResult(0, null, $"未找到文档主表[{param.MasterTable}]。");

            var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { param.MasterTable };
            foreach (var d in BusinessRelationResolver.GetDetails(masterType))
                allowedTables.Add(BusinessRelationResolver.GetTableName(d.EntityType));
            foreach (var e in BusinessRelationResolver.GetExtensions(masterType))
                allowedTables.Add(BusinessRelationResolver.GetTableName(e.EntityType));

            if (!allowedTables.Contains(param.TargetTable))
                return new DosResult(0, null, $"目标表[{param.TargetTable}]不属于文档[{param.MasterTable}]。");

            var ctx = GetContext(param.OsClient);
            var sqlType = ResolveSqlType(param, ctx.Info.DbType);
            if (string.IsNullOrWhiteSpace(sqlType))
                return new DosResult(0, null, $"无法解析字段类型[{param.DataType}]。");

            // 目标表不存在则创建（扩展表/明细表场景）
            var existing = GetExistingTables(ctx, param.OsClient);
            if (!existing.Contains(param.TargetTable))
            {
                var addTable = ctx.Ddl.AddDiyTable(new DbServiceParam
                {
                    TableName = param.TargetTable,
                    OsClient = param.OsClient,
                    DbSession = ctx.Session,
                    DbInfo = ctx.Info
                });
                if (addTable == null || addTable.Code != 1)
                    return new DosResult(0, null, "目标表不存在且自动建表失败：" + addTable?.Msg);
            }

            // 列已存在则视为成功（幂等）
            var columns = ReadColumns(ctx, param.TargetTable);
            if (columns.Any(c => string.Equals(c.Name, param.FieldName, StringComparison.OrdinalIgnoreCase)))
                return new DosResult(1, null, "字段已存在，无需重复添加。");

            var addCol = ctx.Ddl.AddColumn(new DbServiceParam
            {
                TableName = param.TargetTable,
                FieldName = param.FieldName,
                FieldType = sqlType,
                FieldNotNull = param.NotNull,
                FieldLabel = param.Label ?? param.FieldName,
                OsClient = param.OsClient,
                DbSession = ctx.Session,
                DbInfo = ctx.Info
            });
            if (addCol == null || addCol.Code != 1)
                return new DosResult(0, null, "加字段失败：" + addCol?.Msg);

            return new DosResult(1, new { param.TargetTable, param.FieldName, SqlType = sqlType }, "字段添加成功。");
        }

        #region 私有

        private BusinessTableInfo BuildTableInfo(DbContext ctx, string tableName, string label,
            BusinessTableRole role, string foreignKey, string propertyName, HashSet<string> existing)
        {
            var info = new BusinessTableInfo
            {
                TableName = tableName,
                Label = label,
                Role = role,
                ForeignKey = foreignKey,
                PropertyName = propertyName,
                Exists = existing.Contains(tableName)
            };
            if (info.Exists)
                info.Columns = ReadColumns(ctx, tableName);
            return info;
        }

        private List<BusinessColumnInfo> ReadColumns(DbContext ctx, string tableName)
        {
            var sysFields = new HashSet<string>(DiyCommon.DefaultFields, StringComparer.OrdinalIgnoreCase);
            var result = new List<BusinessColumnInfo>();
            var colsResult = ctx.Ddl.GetColumns(new DbServiceParam
            {
                TableName = tableName,
                DbSession = ctx.Session,
                DbInfo = ctx.Info
            });
            foreach (var c in colsResult?.Data ?? new List<information_schema_columns>())
            {
                result.Add(new BusinessColumnInfo
                {
                    Name = c.column_name,
                    DataType = c.data_type,
                    ColumnType = c.column_type,
                    Comment = c.column_comment,
                    Nullable = string.Equals(c.is_nullable, "YES", StringComparison.OrdinalIgnoreCase),
                    IsPrimaryKey = string.Equals(c.column_key, "PRI", StringComparison.OrdinalIgnoreCase),
                    IsSystem = sysFields.Contains(c.column_name)
                });
            }
            return result;
        }

        private HashSet<string> GetExistingTables(DbContext ctx, string osClient)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var tables = ctx.Ddl.GetTables(new DbServiceParam { OsClient = osClient, DbSession = ctx.Session, DbInfo = ctx.Info });
            foreach (var t in tables?.Data ?? new List<string>())
                if (!string.IsNullOrWhiteSpace(t)) set.Add(t);
            return set;
        }

        private static string ResolveSqlType(BusinessAddFieldParam param, DatabaseType dbType)
        {
            var dt = (param.DataType ?? "string").Trim().ToLowerInvariant();
            switch (dt)
            {
                case "raw": return param.RawType;
                case "string": return SqlTypeMapper.Map(typeof(string), dbType, param.Length ?? 255);
                case "text": return SqlTypeMapper.Map(typeof(string), dbType, 0);
                case "int": return SqlTypeMapper.Map(typeof(int), dbType);
                case "long": return SqlTypeMapper.Map(typeof(long), dbType);
                case "decimal": return SqlTypeMapper.Map(typeof(decimal), dbType);
                case "double": return SqlTypeMapper.Map(typeof(double), dbType);
                case "bool": return SqlTypeMapper.Map(typeof(bool), dbType);
                case "datetime": return SqlTypeMapper.Map(typeof(DateTime), dbType);
                default: return null;
            }
        }

        private static bool IsValidIdentifier(string s)
        {
            return !string.IsNullOrWhiteSpace(s)
                && System.Text.RegularExpressions.Regex.IsMatch(s, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        #endregion
    }
}
