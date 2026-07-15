using System;
using System.Collections.Generic;
using Dos.Common;
using Dos.ORM;

namespace Microi.net
{
    /// <summary>
    /// 非空库/跨库启动时，通过底座 <see cref="IMicroiORM"/> 自动建表补列，
    /// 并写入最小种子数据，避免 FormEngine / OsClient 因缺表缺列失败。
    /// </summary>
    public static class CoreTableInitializer
    {
        public static bool EnsureTables(OsClientSecret client)
        {
            if (client?.Db == null)
            {
                Console.WriteLine("  ⚠️ 核心表初始化：DbSession 为空");
                return false;
            }

            try
            {
                EnsureCoreSchema(client);
                SeedMinimalData(client);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 核心表初始化失败：{ex.Message.Split('\n')[0]}");
                return false;
            }
        }

        /// <summary>兼容旧签名（仅 DbSession），尽量从 ClientList 反查租户。</summary>
        public static bool EnsureTables(DbSession db, string dbType)
        {
            foreach (var kv in OsClient.ClientList)
            {
                if (kv.Value?.Db == db)
                    return EnsureTables(kv.Value);
            }

            // 无法反查时构造临时上下文
            var temp = new OsClientSecret
            {
                OsClient = OsClientDefault.OsClient,
                Db = db,
                DbRead = db,
                OsClientModel = new Newtonsoft.Json.Linq.JObject
                {
                    ["DbType"] = dbType ?? "MySql",
                    ["OsClient"] = OsClientDefault.OsClient
                }
            };
            return EnsureTables(temp);
        }

        private static void EnsureCoreSchema(OsClientSecret client)
        {
            C(client, "diy_table", new[]
            {
                Col("Name", "varchar(200)", "表名"),
                Col("Description", "varchar(500)", "说明"),
                Col("OsClient", "varchar(50)"),
                Col("IsTree", "int"),
                Col("EnableCache", "int"),
                Col("CacheParentKey", "varchar(200)"),
                Col("Column", "int"),
                Col("TableInEdit", "int"),
                Col("RowAction", "mediumtext"),
                Col("Tabs", "mediumtext"),
                Col("TabsPosition", "varchar(50)"),
                Col("TableTabs", "mediumtext"),
                Col("TableTabsPosition", "varchar(50)"),
                Col("TableArticle", "int"),
                Col("FormArticle", "int"),
                Col("FormOpenType", "varchar(50)"),
                Col("FormOpenWidth", "varchar(50)"),
                Col("FormLabelPosition", "varchar(50)"),
                Col("FieldBorder", "int"),
                Col("InFormV8", "mediumtext"),
                Col("OutFormV8", "mediumtext"),
                Col("SubmitFormV8", "mediumtext"),
                Col("InputBorderStyle", "varchar(50)"),
                Col("BindRole", "mediumtext"),
                Col("AddCallbakApi", "mediumtext"),
                Col("UptCallbakApi", "mediumtext"),
                Col("DelCallbakApi", "mediumtext"),
                Col("DataEncryptSave", "int"),
                Col("DataEncryptTransfer", "int"),
                Col("ApiReplace", "mediumtext"),
                Col("IsAnonymousRead", "int"),
                Col("IsAnonymousAdd", "int"),
                Col("ServerDataV8", "mediumtext"),
                Col("TreeParentField", "varchar(200)"),
                Col("TreeParentFields", "mediumtext"),
                Col("TreeLazy", "int"),
                Col("TreeHasChildren", "varchar(200)"),
                Col("SubmitBeforeServerV8", "mediumtext"),
                Col("SubmitAfterServerV8", "mediumtext"),
                Col("DataBaseId", "varchar(36)"),
                Col("DataBaseName", "varchar(200)"),
                Col("EnableDataLog", "int"),
                Col("ReportName", "varchar(200)"),
                Col("ReportId", "varchar(36)"),
                Col("DataSourceId", "varchar(36)"),
            });

            C(client, "diy_field", new[]
            {
                Col("TableId", "varchar(36)"),
                Col("Label", "varchar(200)"),
                Col("Name", "varchar(200)"),
                Col("NameConfirm", "int"),
                Col("Type", "varchar(50)"),
                Col("Code", "varchar(50)"),
                Col("Component", "varchar(50)"),
                Col("Description", "varchar(500)"),
                Col("NotEmpty", "int"),
                Col("Visible", "int"),
                Col("Readonly", "int"),
                Col("Sort", "int"),
                Col("Tab", "varchar(200)"),
                Col("OsClient", "varchar(50)"),
                Col("Data", "mediumtext"),
                Col("Config", "mediumtext"),
                Col("FormWidth", "int"),
                Col("TableWidth", "int"),
                Col("DefaultValue", "mediumtext"),
                Col("Unique", "int"),
                Col("BindRole", "mediumtext"),
                Col("V8TmpEngineTable", "mediumtext"),
                Col("V8TmpEngineForm", "mediumtext"),
                Col("Placeholder", "varchar(200)"),
                Col("Remark", "varchar(500)"),
                Col("DataAppend", "mediumtext"),
                Col("InTableEdit", "int"),
                Col("KeyupV8Code", "mediumtext"),
                Col("IsLockField", "int"),
                Col("Encrypt", "int"),
                Col("AppVisible", "int", "移动端可见"),
            });

            C(client, "sys_config", new[]
            {
                Col("IsEnable", "int"),
                Col("ServerVersion", "varchar(50)"),
                Col("ClientVersion", "varchar(50)"),
                Col("PrintSqlToPage", "int"),
                Col("CaptchaConfig", "mediumtext"),
                Col("PwdEncode", "varchar(50)"),
                Col("GlobalServerV8Code", "mediumtext"),
                Col("OsClient", "varchar(50)"),
            });

            C(client, "sys_osclients", new[]
            {
                Col("IsEnable", "int"),
                Col("OsClient", "varchar(50)"),
                Col("OsClientType", "varchar(50)"),
                Col("OsClientNetwork", "varchar(50)"),
                Col("DbType", "varchar(20)"),
                Col("DbVersion", "varchar(50)"),
                Col("DbConn", "varchar(500)"),
                Col("DbReadType", "varchar(50)"),
                Col("DbReadConn", "varchar(500)"),
                Col("DbOracleTableSpace", "varchar(50)"),
                Col("DbMongoConnection", "varchar(500)"),
                Col("ClientName", "varchar(100)"),
                Col("DomainName", "mediumtext"),
                Col("CorsAllowOrigins", "mediumtext"),
                Col("AuthSecret", "varchar(100)"),
                Col("RedisHost", "varchar(200)"),
                Col("RedisPort", "varchar(10)"),
                Col("RedisPwd", "varchar(200)"),
                Col("RedisDataBase", "varchar(10)"),
                Col("RedisTimeout", "varchar(50)"),
                Col("CacheConnectionType", "varchar(100)"),
                Col("MqttEnable", "int"),
                Col("MqttPort", "int"),
                Col("MqttWsPort", "int"),
                Col("MqttAccount", "varchar(50)"),
                Col("MqttPwd", "varchar(50)"),
                Col("MqttApiEngine", "varchar(100)"),
                Col("MQType", "varchar(50)"),
                Col("MQHost", "varchar(200)"),
                Col("MQPort", "varchar(50)"),
                Col("MQUserName", "varchar(50)"),
                Col("MQPassword", "varchar(50)"),
                Col("MQVitrualHost", "varchar(50)"),
                Col("HDFS", "varchar(50)"),
                Col("IndexCodeApi", "mediumtext"),
            });

            C(client, "sys_menu", new[]
            {
                Col("Name", "varchar(200)"),
                Col("Description", "varchar(500)"),
                Col("EnName", "varchar(200)"),
                Col("EnDescription", "varchar(500)"),
                Col("Code", "varchar(100)"),
                Col("Url", "varchar(500)"),
                Col("Link", "varchar(500)"),
                Col("ParentId", "varchar(36)"),
                Col("Sort", "int"),
                Col("Icon", "varchar(200)"),
                Col("IconClass", "varchar(200)"),
                Col("OpenType", "varchar(50)"),
                Col("ComponentName", "varchar(200)"),
                Col("ComponentPath", "varchar(500)"),
                Col("JquerySelector", "varchar(200)"),
                Col("MultRun", "int"),
                Col("Display", "int"),
                Col("Class", "varchar(200)"),
                Col("StoreId", "varchar(36)"),
                Col("DiyTableId", "varchar(36)"),
                Col("TableDiyFieldIds", "mediumtext"),
                Col("PageTemplate", "varchar(200)"),
                Col("SearchFieldIds", "mediumtext"),
                Col("DiyConfig", "mediumtext"),
                Col("SortFieldIds", "mediumtext"),
                Col("SqlWhere", "mediumtext"),
                Col("SqlJoin", "mediumtext"),
                Col("StatisticsFields", "mediumtext"),
                Col("DefaultOrderBy", "varchar(200)"),
                Col("NotShowFields", "mediumtext"),
                Col("ImportTemplate", "mediumtext"),
                Col("ImportTemplateName", "varchar(200)"),
                Col("MoreBtns", "mediumtext"),
                Col("ImportV8", "mediumtext"),
                Col("ExportV8", "mediumtext"),
                Col("ExportMoreBtns", "mediumtext"),
                Col("DetailPageV8", "mediumtext"),
                Col("BatchSelectMoreBtns", "mediumtext"),
                Col("PageBtns", "mediumtext"),
                Col("PageTabs", "mediumtext"),
                Col("InTableEdit", "int"),
                Col("InTableEditFields", "mediumtext"),
                Col("TableHeaders", "mediumtext"),
                Col("IsMicroiService", "int"),
                Col("FormBtns", "mediumtext"),
                Col("SelectFields", "mediumtext"),
                Col("JoinTables", "mediumtext"),
                Col("RoleGroup", "mediumtext"),
                Col("ParentIds", "mediumtext"),
                Col("ReportName", "varchar(200)"),
                Col("ReportId", "varchar(36)"),
                Col("AppDisplay", "int", "移动端显示"),
            });

            C(client, "sys_apiengine", new[]
            {
                Col("ApiName", "varchar(200)"),
                Col("ApiEngineKey", "varchar(200)"),
                Col("IsEnable", "int"),
                Col("ApiRole", "mediumtext"),
                Col("ApiV8Code", "mediumtext"),
                Col("Lock", "int"),
                Col("LockKey", "varchar(200)"),
                Col("ApiRemark", "mediumtext", "接口说明"),
                Col("TestParam", "mediumtext"),
                Col("TestResult", "mediumtext"),
                Col("ApiAddress", "varchar(500)"),
                Col("AllowAnonymous", "int"),
                Col("Files", "mediumtext", "相关附件"),
                Col("GlobalServerV8Code", "mediumtext"),
                Col("OsClient", "varchar(50)"),
            });

            C(client, "diy_lang", new[]
            {
                Col("Key", "varchar(50)"),
                Col("ZhCN", "varchar(50)"),
                Col("En", "varchar(50)"),
                Col("ZhTW", "varchar(50)"),
                Col("Code", "varchar(50)"),
                Col("OsClient", "varchar(50)"),
            });

            C(client, "sys_user", new[]
            {
                Col("Account", "varchar(100)"),
                Col("Pwd", "varchar(200)"),
                Col("Name", "varchar(100)"),
                Col("Level", "int"),
                Col("PwdEncode", "varchar(50)"),
                Col("OsClient", "varchar(50)"),
                Col("IsEnable", "int"),
                Col("State", "int"),
                Col("DeptId", "varchar(36)"),
                Col("RoleIds", "mediumtext"),
            });

            C(client, "sys_role", new[]
            {
                Col("Name", "varchar(100)"),
                Col("Level", "int"),
                Col("ParentId", "varchar(36)"),
                Col("OsClient", "varchar(50)"),
            });

            C(client, "sys_rolelimit", new[]
            {
                Col("RoleId", "varchar(36)"),
                Col("FkId", "varchar(36)"),
                Col("Type", "varchar(50)"),
                Col("Customer", "varchar(200)"),
                Col("Permission", "mediumtext"),
            });
            UpgradeDdlHelper.EnsureIndex(client, "sys_rolelimit", "idx_sys_rolelimit_roleid", "RoleId");
            UpgradeDdlHelper.EnsureIndex(client, "sys_rolelimit", "idx_sys_rolelimit_fktype", "FkId,Type");
        }

        private static void SeedMinimalData(OsClientSecret client)
        {
            // 种子数据仍用 DML；建表/补列已走底座 DDL
            try
            {
                var db = client.Db;
                void SeedTable(string id, string name, string desc)
                {
                    try
                    {
                        var dbInfo = UpgradeDdlHelper.ResolveDbInfo(client);
                        var L = dbInfo.L.ToString();
                        var R = dbInfo.R.ToString();
                        var exists = db.FromSql($"SELECT COUNT(1) FROM {L}diy_table{R} WHERE {L}Name{R}=@p0")
                            .AddInParameter("@p0", name).ToScalar();
                        if (Convert.ToInt32(exists) > 0) return;
                        db.FromSql($@"INSERT INTO {L}diy_table{R} ({L}Id{R}, {L}Name{R}, {L}Description{R}, {L}CreateTime{R}, {L}IsDeleted{R}, {L}OsClient{R}, {L}Column{R}, {L}RowAction{R}, {L}BindRole{R}, {L}ApiReplace{R})
VALUES (@p0, @p1, @p2, @p3, 0, @p4, 2, @p5, @p6, @p7)")
                            .AddInParameter("@p0", id)
                            .AddInParameter("@p1", name)
                            .AddInParameter("@p2", desc)
                            .AddInParameter("@p3", DateTime.UtcNow)
                            .AddInParameter("@p4", client.OsClient ?? "iTdos")
                            .AddInParameter("@p5", "[]")
                            .AddInParameter("@p6", "[]")
                            .AddInParameter("@p7", "{}")
                            .ExecuteNonQuery();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  ⚠️ 种子 diy_table[{name}]：{ex.Message.Split('\n')[0]}");
                    }
                }

                SeedTable("00000000-0000-0000-0000-000000000001", "diy_table", "表单引擎-表");
                SeedTable("00000000-0000-0000-0000-000000000002", "diy_field", "表单引擎-字段");
                SeedTable("c8570fa6-c10f-4014-8cb4-4b046e7ba69c", "sys_config", "系统设置");
                SeedTable("00000000-0000-0000-0000-000000000003", "sys_osclients", "租户引擎");
                SeedTable("00000000-0000-0000-0000-000000000004", "sys_menu", "菜单引擎");
                SeedTable("cf389aef-72cc-4980-9c5b-143123561ac0", "sys_apiengine", "接口引擎");
                SeedTable("00000000-0000-0000-0000-000000000006", "sys_user", "用户");
                SeedTable("00000000-0000-0000-0000-000000000007", "sys_role", "角色");
                SeedTable("00000000-0000-0000-0000-000000000008", "sys_rolelimit", "角色权限");
                // diy_lang 元数据由 UpgradeLang 数据脚本写入（固定 Id）

                try
                {
                    var cfg = db.FromSql("SELECT COUNT(1) FROM sys_config WHERE IsEnable=1 AND IsDeleted<>1").ToScalar();
                    if (Convert.ToInt32(cfg) == 0)
                    {
                        db.FromSql(@"INSERT INTO sys_config (Id, IsDeleted, IsEnable, ServerVersion, ClientVersion, PrintSqlToPage, PwdEncode, CreateTime, OsClient)
VALUES (@p0, 0, 1, @p1, @p2, 0, @p3, @p4, @p5)")
                            .AddInParameter("@p0", "c8570fa6-c10f-4014-8cb4-4b046e7ba69c")
                            .AddInParameter("@p1", "")
                            .AddInParameter("@p2", "")
                            .AddInParameter("@p3", "DES")
                            .AddInParameter("@p4", DateTime.UtcNow)
                            .AddInParameter("@p5", client.OsClient ?? "iTdos")
                            .ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 种子 sys_config：{ex.Message.Split('\n')[0]}");
                }

                try
                {
                    var os = client.OsClient ?? "iTdos";
                    var adminCount = db.FromSql(
                            "SELECT COUNT(1) FROM sys_user WHERE Account=@p0 AND IsDeleted<>1")
                        .AddInParameter("@p0", "admin")
                        .ToScalar();
                    if (Convert.ToInt32(adminCount) == 0)
                    {
                        var initialPassword = Environment.GetEnvironmentVariable("MICROI_INITIAL_ADMIN_PASSWORD");
                        if (string.IsNullOrWhiteSpace(initialPassword))
                        {
                            initialPassword = "demo123456";
                        }

                        db.FromSql(@"INSERT INTO sys_user
(Id, Account, Pwd, Name, Level, PwdEncode, OsClient, IsEnable, State, RoleIds, IsDeleted, CreateTime)
VALUES
(@p0, @p1, @p2, @p3, 9999, @p4, @p5, 1, 1, @p6, 0, @p7)")
                            .AddInParameter("@p0", "c74d669c-a3d4-11e5-b60d-b870f43edd03")
                            .AddInParameter("@p1", "admin")
                            .AddInParameter("@p2", EncryptHelper.DESEncode(initialPassword))
                            .AddInParameter("@p3", "管理员")
                            .AddInParameter("@p4", "DES")
                            .AddInParameter("@p5", os)
                            .AddInParameter("@p6", "[]")
                            .AddInParameter("@p7", DateTime.UtcNow)
                            .ExecuteNonQuery();
                        Console.WriteLine("Microi：【✅】已创建初始管理员账号 admin。");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 种子 sys_user：admin 初始化失败：{ex.Message.Split('\n')[0]}");
                }

                try
                {
                    var os = client.OsClient ?? "iTdos";
                    var osType = client.OsClientModel?["OsClientType"]?.ToString() ?? "Product";
                    var osNet = client.OsClientModel?["OsClientNetwork"]?.ToString() ?? "Internal";
                    var dbType = client.OsClientModel?["DbType"]?.ToString() ?? "MySql";
                    var dbConn = client.OsClientModel?["DbConn"]?.ToString() ?? "";
                    var dbReadType = client.OsClientModel?["DbReadType"]?.ToString();
                    var dbReadConn = client.OsClientModel?["DbReadConn"]?.ToString();
                    var redisHost = client.OsClientModel?["RedisHost"]?.ToString() ?? "";
                    var redisPort = client.OsClientModel?["RedisPort"]?.ToString() ?? "";
                    var redisPwd = client.OsClientModel?["RedisPwd"]?.ToString() ?? "";
                    var redisDataBase = client.OsClientModel?["RedisDataBase"]?.ToString() ?? "";
                    if (string.IsNullOrWhiteSpace(dbReadType)) dbReadType = dbType;
                    if (string.IsNullOrWhiteSpace(dbReadConn)) dbReadConn = dbConn;
                    var cnt = db.FromSql("SELECT COUNT(1) FROM sys_osclients WHERE OsClient=@p0")
                        .AddInParameter("@p0", os).ToScalar();
                    if (Convert.ToInt32(cnt) == 0)
                    {
                        db.FromSql(@"INSERT INTO sys_osclients
(Id, IsDeleted, IsEnable, OsClient, OsClientType, OsClientNetwork, DbType, DbConn, DbReadType, DbReadConn,
 RedisHost, RedisPort, RedisPwd, RedisDataBase, CreateTime)
VALUES (@p0, 0, 1, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9, @p10, @p11, @p12)")
                            .AddInParameter("@p0", Guid.NewGuid().ToString())
                            .AddInParameter("@p1", os)
                            .AddInParameter("@p2", osType)
                            .AddInParameter("@p3", osNet)
                            .AddInParameter("@p4", dbType)
                            .AddInParameter("@p5", dbConn)
                            .AddInParameter("@p6", dbReadType)
                            .AddInParameter("@p7", dbReadConn)
                            .AddInParameter("@p8", redisHost)
                            .AddInParameter("@p9", redisPort)
                            .AddInParameter("@p10", redisPwd)
                            .AddInParameter("@p11", redisDataBase)
                            .AddInParameter("@p12", DateTime.UtcNow)
                            .ExecuteNonQuery();
                    }
                    else
                    {
                        // 修复早期最小种子行缺少连接配置，确保随后 EnsureHydrated 可完整挂载。
                        db.FromSql(@"UPDATE sys_osclients SET
DbType=@p1, DbConn=@p2, DbReadType=@p3, DbReadConn=@p4,
RedisHost=@p5, RedisPort=@p6, RedisPwd=@p7, RedisDataBase=@p8
WHERE OsClient=@p0")
                            .AddInParameter("@p0", os)
                            .AddInParameter("@p1", dbType)
                            .AddInParameter("@p2", dbConn)
                            .AddInParameter("@p3", dbReadType)
                            .AddInParameter("@p4", dbReadConn)
                            .AddInParameter("@p5", redisHost)
                            .AddInParameter("@p6", redisPort)
                            .AddInParameter("@p7", redisPwd)
                            .AddInParameter("@p8", redisDataBase)
                            .ExecuteNonQuery();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  ⚠️ 种子 sys_osclients：{ex.Message.Split('\n')[0]}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️ 核心种子数据：{ex.Message.Split('\n')[0]}");
            }
        }

        private static void C(OsClientSecret client, string table, UpgradeDdlHelper.ColumnSpec[] cols) =>
            UpgradeDdlHelper.EnsureTableWithColumns(client, table, cols);

        private static UpgradeDdlHelper.ColumnSpec Col(string name, string type, string label = null, bool notNull = false) =>
            new UpgradeDdlHelper.ColumnSpec { Name = name, Type = type, Label = label ?? name, NotNull = notNull };
    }
}
