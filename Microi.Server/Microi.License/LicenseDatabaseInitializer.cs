using Dos.Common;
using Dos.ORM;
using Microi.net;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Microi.License
{
    /// <summary>
    /// 授权中心独立数据库初始化器。
    /// 仅创建 License 业务表，不依赖 Microi 框架库的 diy_table/diy_field 元数据。
    /// </summary>
    internal static class LicenseDatabaseInitializer
    {
        private static readonly object SyncRoot = new object();
        private static string _initializedConnection;

        private sealed class ColumnSpec
        {
            public string Name { get; set; }
            public string Type { get; set; }
        }

        public static void Ensure(DbSession db, string dbTypeName, string connectionString)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (string.Equals(_initializedConnection, connectionString, StringComparison.Ordinal)) return;

            lock (SyncRoot)
            {
                if (string.Equals(_initializedConnection, connectionString, StringComparison.Ordinal)) return;

                var dbInfo = DiyCommon.GetDbInfo(dbTypeName);
                var client = new OsClientSecret
                {
                    OsClient = "__LicenseAdmin__",
                    Db = db,
                    DbRead = db,
                    OsClientModel = JObject.FromObject(new { DbType = dbTypeName })
                };
                var ddl = MicroiEngine.ORM(dbInfo.DbType);

                EnsureTable(ddl, client, dbInfo, "diy_license", new[]
                {
                    Col("HID", "varchar(128)"),
                    Col("Company", "varchar(200)"),
                    Col("Name", "varchar(100)"),
                    Col("Phone", "varchar(50)"),
                    Col("IP", "varchar(100)"),
                    Col("ProductType", "varchar(50)"),
                    Col("Status", "varchar(20)"),
                    Col("LicenseContent", "mediumtext"),
                    Col("IssuedAt", "datetime"),
                    Col("ExpirationDate", "datetime"),
                    Col("UpdateExpirationDate", "datetime"),
                    Col("RejectReason", "varchar(500)"),
                    Col("Remark", "varchar(1000)")
                });
                EnsureIndex(ddl, client, dbInfo, "diy_license", "idx_diy_license_hid", "HID", true);
                EnsureIndex(ddl, client, dbInfo, "diy_license", "idx_diy_license_status", "Status", false);

                EnsureTable(ddl, client, dbInfo, "diy_license_log", new[]
                {
                    Col("HID", "varchar(128)"),
                    Col("Action", "varchar(20)"),
                    Col("Operator", "varchar(100)"),
                    Col("OperatorIP", "varchar(100)"),
                    Col("Detail", "varchar(1000)")
                });
                EnsureIndex(ddl, client, dbInfo, "diy_license_log", "idx_diy_license_log_hid", "HID", false);
                EnsureIndex(ddl, client, dbInfo, "diy_license_log", "idx_diy_license_log_time", "CreateTime", false);

                _initializedConnection = connectionString;
            }
        }

        private static ColumnSpec Col(string name, string type) => new ColumnSpec { Name = name, Type = type };

        private static DbServiceParam Param(OsClientSecret client, DbInfo dbInfo, string tableName) =>
            new DbServiceParam
            {
                OsClient = client.OsClient,
                OsClientModel = client,
                DbSession = client.Db,
                DbInfo = dbInfo,
                TableName = tableName
            };

        private static void EnsureTable(IMicroiORM ddl, OsClientSecret client, DbInfo dbInfo,
            string tableName, IEnumerable<ColumnSpec> columns)
        {
            if (!client.Db.TableExists(tableName))
            {
                var result = ddl.AddDiyTable(Param(client, dbInfo, tableName));
                if (result == null || result.Code != 1)
                    throw new InvalidOperationException($"创建授权表 {tableName} 失败：{result?.Msg}");
            }

            var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columnResult = ddl.GetColumns(Param(client, dbInfo, tableName));
            foreach (var column in columnResult?.Data ?? new List<information_schema_columns>())
            {
                if (!string.IsNullOrWhiteSpace(column?.column_name))
                    existing.Add(column.column_name);
            }

            foreach (var column in columns)
            {
                if (existing.Contains(column.Name)) continue;
                var result = ddl.AddColumn(new DbServiceParam
                {
                    OsClient = client.OsClient,
                    OsClientModel = client,
                    DbSession = client.Db,
                    DbInfo = dbInfo,
                    TableName = tableName,
                    FieldName = column.Name,
                    FieldType = NormalizeType(column.Type, dbInfo.DbType),
                    FieldLabel = column.Name
                });
                if (result == null || result.Code != 1)
                    throw new InvalidOperationException($"创建授权字段 {tableName}.{column.Name} 失败：{result?.Msg}");
            }
        }

        private static void EnsureIndex(IMicroiORM ddl, OsClientSecret client, DbInfo dbInfo,
            string tableName, string indexName, string columns, bool unique)
        {
            try
            {
                ddl.AddIndex(new DbServiceParam
                {
                    OsClient = client.OsClient,
                    OsClientModel = client,
                    DbSession = client.Db,
                    DbInfo = dbInfo,
                    TableName = tableName,
                    IndexName = indexName,
                    IndexColumns = columns,
                    IndexUnique = unique
                });
            }
            catch (Exception ex) when (ex.Message.Contains("已存在", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
            {
                // 幂等：索引已存在。
            }
        }

        private static string NormalizeType(string type, DatabaseType dbType)
        {
            if (string.Equals(type, "mediumtext", StringComparison.OrdinalIgnoreCase))
                return dbType == DatabaseType.SqlServer || dbType == DatabaseType.SqlServer9
                    ? "nvarchar(max)"
                    : dbType == DatabaseType.PostgreSql ? "text" : "mediumtext";
            if (type.StartsWith("varchar", StringComparison.OrdinalIgnoreCase)
                && (dbType == DatabaseType.SqlServer || dbType == DatabaseType.SqlServer9))
                return "n" + type;
            return type;
        }
    }
}
