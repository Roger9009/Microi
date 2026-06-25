using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Dos.Common;
using Dos.ORM;

namespace Microi.net.Business
{
    /// <summary>
    /// 代码优先（Code-First）Schema 初始化器。
    /// 根据带 [BusinessTable] 的实体，自动在目标租户数据库中：
    ///   1) 表不存在 → 创建表（含平台系统字段 Id/CreateTime/UpdateTime/UserId/UserName/IsDeleted）；
    ///   2) 表已存在但缺列 → 增量补列。
    /// 全程复用平台多方言 DDL 服务（IMicroiORM），支持 MySQL/SqlServer/Oracle/PostgreSQL/达梦/人大金仓。
    /// </summary>
    public sealed class BusinessSchemaInitializer
    {
        /// <summary>
        /// 同步一批实体到指定租户数据库。幂等：可重复执行。
        /// </summary>
        public DosResult EnsureSchema(IEnumerable<Type> entityTypes, string osClient)
        {
            var types = (entityTypes ?? Enumerable.Empty<Type>())
                .Where(t => t != null && t.GetCustomAttribute<BusinessTableAttribute>() != null)
                .ToList();
            if (types.Count == 0)
                return new DosResult(1, null, "无需同步的实体。");

            DbSession dbSession;
            DbInfo dbInfo;
            IMicroiORM ddl;
            try
            {
                var client = OsClientExtend.GetClient(osClient);
                if (client == null || client.Db == null)
                    return new DosResult(0, null, $"租户[{osClient}]数据库会话不可用，跳过自动建表。");
                dbSession = client.Db;
                var dbTypeStr = client.OsClientModel["DbType"].Val<string>();
                dbInfo = DiyCommon.GetDbInfo(dbTypeStr);
                ddl = MicroiEngine.ORM(dbInfo.DbType);
            }
            catch (Exception ex)
            {
                return new DosResult(0, null, $"初始化数据库上下文失败：{ex.Message}");
            }

            // 已存在的表（小写）
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var tablesResult = ddl.GetTables(new DbServiceParam { OsClient = osClient, DbSession = dbSession });
                foreach (var t in tablesResult?.Data ?? new List<string>())
                {
                    if (!string.IsNullOrWhiteSpace(t)) existingTables.Add(t);
                }
            }
            catch (Exception ex)
            {
                return new DosResult(0, null, $"读取数据库表列表失败：{ex.Message}");
            }

            int createdTables = 0, addedColumns = 0;
            var errors = new List<string>();

            foreach (var type in types)
            {
                var tableAttr = type.GetCustomAttribute<BusinessTableAttribute>();
                var tableName = tableAttr.Name;

                try
                {
                    // 1) 建表
                    if (!existingTables.Contains(tableName))
                    {
                        var addTableResult = ddl.AddDiyTable(new DbServiceParam
                        {
                            TableName = tableName,
                            OsClient = osClient,
                            DbSession = dbSession,
                            DbInfo = dbInfo
                        });
                        if (addTableResult == null || addTableResult.Code != 1)
                        {
                            errors.Add($"[{tableName}] 建表失败：{addTableResult?.Msg}");
                            continue;
                        }
                        createdTables++;
                        existingTables.Add(tableName);
                        Console.WriteLine($"Microi.Business：【建表】[{osClient}] 已创建表 {tableName}");
                    }

                    // 2) 补列：读现有列，比对实体属性
                    var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    var colsResult = ddl.GetColumns(new DbServiceParam
                    {
                        TableName = tableName,
                        OsClient = osClient,
                        DbSession = dbSession,
                        DbInfo = dbInfo
                    });
                    foreach (var c in colsResult?.Data ?? new List<information_schema_columns>())
                    {
                        if (!string.IsNullOrWhiteSpace(c?.column_name)) existingColumns.Add(c.column_name);
                    }

                    foreach (var col in GetMappedColumns(type, dbInfo.DbType))
                    {
                        if (existingColumns.Contains(col.Name)) continue;

                        var addColResult = ddl.AddColumn(new DbServiceParam
                        {
                            TableName = tableName,
                            FieldName = col.Name,
                            FieldType = col.SqlType,
                            FieldNotNull = col.NotNull,
                            FieldLabel = col.Label,
                            OsClient = osClient,
                            DbSession = dbSession,
                            DbInfo = dbInfo
                        });
                        if (addColResult == null || addColResult.Code != 1)
                        {
                            errors.Add($"[{tableName}.{col.Name}] 补列失败：{addColResult?.Msg}");
                            continue;
                        }
                        addedColumns++;
                        existingColumns.Add(col.Name);
                        Console.WriteLine($"Microi.Business：【补列】[{osClient}] {tableName}.{col.Name} {col.SqlType}");
                    }
                }
                catch (Exception ex)
                {
                    errors.Add($"[{tableName}] 同步异常：{ex.Message}");
                }
            }

            var msg = $"租户[{osClient}]：新建表 {createdTables}，新增列 {addedColumns}"
                + (errors.Count > 0 ? $"，错误 {errors.Count} 项：{string.Join("；", errors)}" : "。");
            return new DosResult(errors.Count == 0 ? 1 : 0, new { createdTables, addedColumns, errors }, msg);
        }

        private sealed class ColumnDef
        {
            public string Name;
            public string SqlType;
            public bool NotNull;
            public string Label;
        }

        /// <summary>
        /// 解析实体的映射列（排除系统字段与 Ignore 属性）。
        /// </summary>
        private static IEnumerable<ColumnDef> GetMappedColumns(Type type, DatabaseType dbType)
        {
            var sysFields = new HashSet<string>(DiyCommon.DefaultFields, StringComparer.OrdinalIgnoreCase);

            foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0) continue;     // 跳过索引器
                if (!prop.CanRead || !prop.CanWrite) continue;
                if (sysFields.Contains(prop.Name)) continue;            // 系统字段由建表创建

                var colAttr = prop.GetCustomAttribute<BusinessColumnAttribute>();
                if (colAttr != null && colAttr.Ignore) continue;

                var sqlType = colAttr != null && !string.IsNullOrWhiteSpace(colAttr.Type)
                    ? colAttr.Type
                    : SqlTypeMapper.Map(prop.PropertyType, dbType, colAttr?.Length ?? 255);

                yield return new ColumnDef
                {
                    Name = prop.Name,
                    SqlType = sqlType,
                    NotNull = colAttr?.NotNull ?? false,
                    Label = colAttr?.Label ?? prop.Name
                };
            }
        }
    }
}
