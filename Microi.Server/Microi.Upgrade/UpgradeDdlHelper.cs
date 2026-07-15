using System;
using System.Collections.Generic;
using System.Linq;
using Dos.Common;
using Dos.ORM;

namespace Microi.net
{
    /// <summary>
    /// 升级/空库初始化用的跨库 DDL 助手。
    /// 统一走底座 <see cref="IMicroiORM"/>（AddDiyTable / AddColumn / AddIndex），
    /// 由各数据库方言服务生成 DDL，避免手写 SQL。
    /// </summary>
    public static class UpgradeDdlHelper
    {
        private static readonly HashSet<string> SystemFields = new HashSet<string>(
            DiyCommon.DefaultFields ?? new List<string> { "Id", "CreateTime", "UpdateTime", "UserId", "UserName", "IsDeleted" },
            StringComparer.OrdinalIgnoreCase);

        public sealed class ColumnSpec
        {
            public string Name { get; set; }
            /// <summary>逻辑类型：int / varchar(n) / mediumtext / datetime / bit 等（与 FormEngine 一致）</summary>
            public string Type { get; set; }
            public string Label { get; set; }
            public bool NotNull { get; set; }
        }

        public static DbInfo ResolveDbInfo(OsClientSecret client)
        {
            var dbType = client?.OsClientModel?["DbType"]?.ToString() ?? "MySql";
            return DiyCommon.GetDbInfo(dbType);
        }

        public static IMicroiORM ResolveDdl(OsClientSecret client) =>
            MicroiEngine.ORM(ResolveDbInfo(client).DbType);

        private static DbServiceParam BaseParam(OsClientSecret client, string tableName = null)
        {
            var dbInfo = ResolveDbInfo(client);
            return new DbServiceParam
            {
                OsClient = client.OsClient,
                OsClientModel = client,
                DbSession = client.Db,
                DbInfo = dbInfo,
                TableName = tableName
            };
        }

        /// <summary>
        /// 表不存在则创建（仅系统字段 Id/CreateTime/.../IsDeleted）。
        /// </summary>
        public static DosResult EnsureTable(OsClientSecret client, string tableName)
        {
            if (client?.Db == null || string.IsNullOrWhiteSpace(tableName))
                return new DosResult(0, null, "EnsureTable 参数无效");

            try
            {
                if (client.Db.TableExists(tableName))
                    return new DosResult(1, null, "表已存在");

                var ddl = ResolveDdl(client);
                var param = BaseParam(client, tableName);
                var result = ddl.AddDiyTable(param);
                if (result != null && result.Code == 1)
                    Console.WriteLine($"Microi：【DDL】已创建表 {tableName}");
                return result ?? new DosResult(0, null, "AddDiyTable 返回空");
            }
            catch (Exception ex)
            {
                // 并发建表等：再次探测
                if (client.Db.TableExists(tableName))
                    return new DosResult(1, null, "表已存在");
                return new DosResult(0, null, $"EnsureTable[{tableName}]：{ex.Message}");
            }
        }

        /// <summary>
        /// 缺列则补列（幂等）。系统字段跳过。
        /// </summary>
        public static DosResult EnsureColumn(OsClientSecret client, string tableName, string fieldName, string fieldType, string label = null, bool notNull = false)
        {
            if (client?.Db == null || string.IsNullOrWhiteSpace(tableName) || string.IsNullOrWhiteSpace(fieldName))
                return new DosResult(0, null, "EnsureColumn 参数无效");
            if (SystemFields.Contains(fieldName))
                return new DosResult(1, null, "系统字段跳过");

            try
            {
                if (!client.Db.TableExists(tableName))
                {
                    var t = EnsureTable(client, tableName);
                    if (t.Code != 1) return t;
                }

                var ddl = ResolveDdl(client);
                var dbInfo = ResolveDbInfo(client);
                var existing = GetExistingColumns(ddl, client, tableName);
                if (existing.Contains(fieldName))
                    return new DosResult(1, null, "列已存在");

                var sqlType = NormalizeFieldType(fieldType, dbInfo.DbType);
                var result = ddl.AddColumn(new DbServiceParam
                {
                    OsClient = client.OsClient,
                    OsClientModel = client,
                    DbSession = client.Db,
                    DbInfo = dbInfo,
                    TableName = tableName,
                    FieldName = fieldName,
                    FieldType = sqlType,
                    FieldLabel = label ?? fieldName,
                    FieldNotNull = notNull
                });

                if (result != null && result.Code == 1
                    && !string.Equals(result.Msg, "列已存在", StringComparison.OrdinalIgnoreCase))
                    Console.WriteLine($"Microi：【DDL】补列 {tableName}.{fieldName} ({sqlType})");
                return result ?? new DosResult(0, null, "AddColumn 返回空");
            }
            catch (Exception ex)
            {
                var msg = ex.Message ?? "";
                if (msg.IndexOf("多次指定了列名", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("Duplicate column", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new DosResult(1, null, "列已存在");
                return new DosResult(0, null, $"EnsureColumn[{tableName}.{fieldName}]：{ex.Message}");
            }
        }

        public static void EnsureColumns(OsClientSecret client, string tableName, IEnumerable<ColumnSpec> columns)
        {
            foreach (var col in columns ?? Enumerable.Empty<ColumnSpec>())
            {
                if (col == null || string.IsNullOrWhiteSpace(col.Name)) continue;
                var r = EnsureColumn(client, tableName, col.Name, col.Type ?? "varchar(255)", col.Label, col.NotNull);
                if (r.Code != 1)
                    Console.WriteLine($"Microi：【⚠️】补列失败 {tableName}.{col.Name}：{r.Msg}");
            }
        }

        /// <summary>
        /// 先建表再补齐全部业务列。
        /// </summary>
        public static void EnsureTableWithColumns(OsClientSecret client, string tableName, IEnumerable<ColumnSpec> columns)
        {
            var t = EnsureTable(client, tableName);
            if (t.Code != 1)
            {
                Console.WriteLine($"Microi：【⚠️】建表失败 {tableName}：{t.Msg}");
                return;
            }
            EnsureColumns(client, tableName, columns);
        }

        public static DosResult EnsureIndex(OsClientSecret client, string tableName, string indexName, string columns, bool unique = false)
        {
            try
            {
                var ddl = ResolveDdl(client);
                var result = ddl.AddIndex(new DbServiceParam
                {
                    OsClient = client.OsClient,
                    OsClientModel = client,
                    DbSession = client.Db,
                    DbInfo = ResolveDbInfo(client),
                    TableName = tableName,
                    IndexName = indexName,
                    IndexColumns = columns,
                    IndexUnique = unique
                });
                // 索引已存在多数方言会抛错，视为成功
                if (result != null && result.Code != 1)
                {
                    var msg = result.Msg ?? "";
                    if (msg.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                        || msg.IndexOf("已存在", StringComparison.OrdinalIgnoreCase) >= 0
                        || msg.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                        return new DosResult(1);
                }
                return result ?? new DosResult(1);
            }
            catch (Exception ex)
            {
                var msg = ex.Message ?? "";
                if (msg.IndexOf("already", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("已存在", StringComparison.OrdinalIgnoreCase) >= 0
                    || msg.IndexOf("duplicate", StringComparison.OrdinalIgnoreCase) >= 0)
                    return new DosResult(1);
                return new DosResult(0, null, ex.Message);
            }
        }

        private static HashSet<string> GetExistingColumns(IMicroiORM ddl, OsClientSecret client, string tableName)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var cols = ddl.GetColumns(BaseParam(client, tableName));
                foreach (var c in cols?.Data ?? new List<information_schema_columns>())
                {
                    if (!string.IsNullOrWhiteSpace(c?.column_name))
                        set.Add(c.column_name);
                }
            }
            catch { /* 忽略，后续 AddColumn 幂等兜底 */ }
            return set;
        }

        /// <summary>
        /// 将 FormEngine/升级脚本常用的逻辑类型映射到当前库方言。
        /// </summary>
        public static string NormalizeFieldType(string fieldType, DatabaseType dbType)
        {
            if (string.IsNullOrWhiteSpace(fieldType))
                return dbType == DatabaseType.SqlServer || dbType == DatabaseType.SqlServer9 ? "nvarchar(255)" : "varchar(255)";

            var t = fieldType.Trim().ToLowerInvariant();

            // 大文本
            if (t == "text" || t == "mediumtext" || t == "longtext" || t == "clob")
            {
                switch (dbType)
                {
                    case DatabaseType.SqlServer:
                    case DatabaseType.SqlServer9: return "nvarchar(max)";
                    case DatabaseType.Oracle:
                    case DatabaseType.DaMeng: return "clob";
                    case DatabaseType.PostgreSql:
                    case DatabaseType.KingBase: return "text";
                    default: return "longtext";
                }
            }

            if (t == "int" || t == "integer")
                return dbType == DatabaseType.Oracle || dbType == DatabaseType.DaMeng ? "number(10)" : "int";

            if (t == "bit" || t == "bool" || t == "boolean" || t == "tinyint(1)")
            {
                switch (dbType)
                {
                    case DatabaseType.SqlServer:
                    case DatabaseType.SqlServer9: return "bit";
                    case DatabaseType.Oracle:
                    case DatabaseType.DaMeng: return "number(1)";
                    case DatabaseType.PostgreSql:
                    case DatabaseType.KingBase: return "boolean";
                    default: return "tinyint(1)";
                }
            }

            if (t == "datetime" || t == "datetime2" || t == "timestamp")
            {
                switch (dbType)
                {
                    case DatabaseType.SqlServer9: return "datetime2";
                    case DatabaseType.Oracle:
                    case DatabaseType.DaMeng: return "timestamp";
                    case DatabaseType.PostgreSql:
                    case DatabaseType.KingBase: return "timestamp";
                    default: return "datetime";
                }
            }

            // varchar(n) / nvarchar(n)
            if (t.StartsWith("varchar") || t.StartsWith("nvarchar"))
            {
                var len = 255;
                var lp = t.IndexOf('(');
                var rp = t.IndexOf(')');
                if (lp > 0 && rp > lp && int.TryParse(t.Substring(lp + 1, rp - lp - 1), out var n))
                    len = n;

                switch (dbType)
                {
                    case DatabaseType.SqlServer:
                    case DatabaseType.SqlServer9: return $"nvarchar({len})";
                    case DatabaseType.Oracle:
                    case DatabaseType.DaMeng: return $"varchar2({len})";
                    default: return $"varchar({len})";
                }
            }

            return fieldType;
        }
    }
}
