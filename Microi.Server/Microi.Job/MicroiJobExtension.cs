using Microsoft.Extensions.DependencyInjection;
using Quartz.AspNetCore;
using Quartz;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Configuration;
using Microi.net;
using Quartz.Simpl;
using Microsoft.AspNetCore.Builder;

namespace Microi.net
{
    public static class MicroiJobExtension
    {
        public static IServiceCollection AddMicroiJob(this IServiceCollection services, string dbConn, string dbType = "SqlServer")
        {
            try
            {
                TryCreateQuartzTables(dbConn, dbType);
                services.AddQuartz(q =>
                {
                    q.UsePersistentStore(x =>
                    {
                        x.UseClustering();
                        switch (NormalizeDbType(dbType))
                        {
                            case "mysql": x.UseMySql(dbConn); break;
                            case "postgresql": x.UsePostgres(dbConn); break;
                            case "oracle": x.UseOracle(dbConn); break;
                            case "sqlite": x.UseSQLite(dbConn); break;
                            default: x.UseSqlServer(dbConn); break;
                        }
                        x.UseNewtonsoftJsonSerializer();
                        x.SetProperty("quartz.jobStore.tablePrefix", "microi_job_");
                        x.SetProperty("quartz.jobStore.performSchemaValidation", "false");
                    });
                    q.AddJobListener<MicroiJobListener>();
                    q.UseDefaultThreadPool(tp =>
                    {
                        var maxCon = Math.Max(40, Environment.ProcessorCount * 10);
                        tp.MaxConcurrency = maxCon;
                        Console.WriteLine($"Microi：【成功】配置任务调度线程池：{maxCon} 线程");
                    });
                });
                services.AddQuartzServer(o => { o.WaitForJobsToComplete = true; o.StartDelay = TimeSpan.FromSeconds(10); });
                services.AddSingleton<IMicroiJob, MicroiQuartzScheduledTask>();
                Console.WriteLine($"Microi：【成功】注入任务调度插件（{dbType}）");
                return services;
            }
            catch (Exception ex) { Console.WriteLine("Microi：【Error】任务调度注入失败：" + ex.Message); return services; }
        }

        public static IApplicationBuilder UseMicroiJob(this IApplicationBuilder app)
        {
            try { app.ApplicationServices.GetRequiredService<IMicroiJob>().SyncTaskTime(); Console.WriteLine("Microi：【成功】任务调度启动！"); }
            catch (Exception ex) { Console.WriteLine("Microi：【Error】任务调度启动失败：" + ex.Message); }
            return app;
        }

        // ══════════════════════════════════════════════════
        //  自动建表 + 缺字段自动补列
        // ══════════════════════════════════════════════════

        private static string NormalizeDbType(string t)
        {
            var l = t?.ToLower() ?? "";
            switch (l) { case "sqlserver9": case "mssql": return "sqlserver"; case "pgsql": case "npgsql": return "postgresql"; case "sqlite3": return "sqlite"; default: return l.Length > 0 ? l : "sqlserver"; }
        }

        private static void TryCreateQuartzTables(string connStr, string dbType)
        {
            var db = NormalizeDbType(dbType);
            var prefix = "microi_job_";
            var schema = GetExpectedSchema(prefix, db);
            try
            {
                var fact = GetFactory(db);
                if (fact == null) { Console.WriteLine($"Microi：【⚠️】不支持的数据库：{db}，跳过"); return; }

                using var conn = fact.CreateConnection();
                conn.ConnectionString = connStr;
                conn.Open();
                using var cmd = conn.CreateCommand();

                // Phase 1: 建表
                var exists = TableExists(cmd, db, prefix + "LOCKS");
                if (!exists)
                {
                    Console.WriteLine($"Microi：【ℹ️】建表（{db}）...");
                    foreach (var sql in GetCreateSql(prefix, db))
                        try { cmd.CommandText = sql; cmd.ExecuteNonQuery(); } catch (Exception e) { Console.WriteLine($"  ⚠️ {e.Message.Split('\n')[0]}"); }
                    Console.WriteLine("Microi：【✅】建表完成");
                }

                // Phase 2: 补缺列
                var added = 0;
                foreach (var entry in schema)
                {
                    var table = entry.Key;
                    var expected = entry.Value;
                    var existing = GetExistingColumns(cmd, db, table, prefix);
                    foreach (var col in expected)
                    {
                        if (existing.Contains(col.Key)) continue;
                        var sql = db switch
                        {
                            "sqlite"     => $"ALTER TABLE {prefix}{table} ADD COLUMN \"{col.Key}\" {col.Value}",
                            "oracle"     => $"ALTER TABLE {prefix}{table} ADD ({col.Key} {col.Value})",
                            "postgresql" => $"ALTER TABLE {prefix}{table} ADD COLUMN \"{col.Key}\" {col.Value}",
                            _            => $"ALTER TABLE {prefix}{table} ADD [{col.Key}] {col.Value}",
                        };
                        try { cmd.CommandText = sql; cmd.ExecuteNonQuery(); added++; }
                        catch (Exception e) { Console.WriteLine($"  ⚠️ 补列 {table}.{col.Key} 失败：{e.Message.Split('\n')[0]}"); }
                    }
                }
                if (added > 0) Console.WriteLine($"Microi：【✅】补列完成（+{added} 列）");
                conn.Close();
            }
            catch (Exception ex) { Console.WriteLine($"Microi：【⚠️】自检失败：{ex.Message}"); }
        }

        // ── 表存在检测 ──
        private static bool TableExists(DbCommand cmd, string db, string fullName)
        {
            cmd.CommandText = db switch
            {
                "sqlite" => $"SELECT 1 FROM {fullName} LIMIT 1",
                "oracle" => $"SELECT 1 FROM {fullName} WHERE ROWNUM <= 1",
                _ => $"SELECT 1 FROM {fullName}",
            };
            try { cmd.ExecuteScalar(); return true; } catch { return false; }
        }

        // ── 获取已有列 ──
        private static HashSet<string> GetExistingColumns(DbCommand cmd, string db, string table, string prefix)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                cmd.CommandText = db switch
                {
                    "sqlite"     => $"PRAGMA table_info('{prefix}{table}')",
                    "oracle"     => $"SELECT COLUMN_NAME FROM USER_TAB_COLUMNS WHERE TABLE_NAME = '{prefix}{table}'",
                    _            => $"SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = '{prefix}{table}'",
                };
                using var r = cmd.ExecuteReader();
                while (r.Read()) set.Add(r.GetString(0));
            }
            catch { /* 表不存在时忽略 */ }
            return set;
        }

        // ── 期望的列定义 ──
        private static Dictionary<string, Dictionary<string, string>> GetExpectedSchema(string p, string db)
        {
            // 类型宏
            string V(int n) => db == "sqlserver" ? $"NVARCHAR({n})" : db == "oracle" ? $"NVARCHAR2({n})" : db == "mysql" ? $"VARCHAR({n})" : $"VARCHAR({n})";
            string big = db == "sqlserver" ? "BIGINT" : db == "postgresql" ? "BIGINT" : db == "sqlite" ? "INTEGER" : db == "oracle" ? "NUMBER(19)" : "BIGINT";
            string blb = db == "sqlserver" ? "VARBINARY(MAX)" : db == "postgresql" ? "BYTEA" : "BLOB";
            string bit = db == "sqlserver" ? "BIT" : db == "postgresql" ? "BOOLEAN" : db == "oracle" ? "NUMBER(1)" : "INTEGER";
            string dec = db == "sqlserver" ? "DECIMAL(13,4)" : db == "postgresql" ? "DECIMAL(13,4)" : db == "oracle" ? "NUMBER(13,4)" : "REAL";

            return new Dictionary<string, Dictionary<string, string>>
                        {
                            ["LOCKS"] = new Dictionary<string, string> { ["SCHED_NAME"] = V(120), ["LOCK_NAME"] = V(40) },
                            ["JOB_DETAILS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["JOB_NAME"] = V(200), ["JOB_GROUP"] = V(200),
                    ["DESCRIPTION"] = V(250), ["JOB_CLASS_NAME"] = V(250),
                    ["IS_DURABLE"] = bit, ["IS_NONCONCURRENT"] = bit, ["IS_UPDATE_DATA"] = bit, ["REQUESTS_RECOVERY"] = bit,
                    ["JOB_DATA"] = blb
                },
                ["TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200),
                    ["JOB_NAME"] = V(200), ["JOB_GROUP"] = V(200), ["DESCRIPTION"] = V(250),
                    ["NEXT_FIRE_TIME"] = big, ["PREV_FIRE_TIME"] = big, ["PRIORITY"] = "INT",
                    ["TRIGGER_STATE"] = V(16), ["TRIGGER_TYPE"] = V(8),
                    ["START_TIME"] = big, ["END_TIME"] = big, ["CALENDAR_NAME"] = V(200), ["MISFIRE_INSTR"] = "INT",
                    ["JOB_DATA"] = blb
                },
                ["SIMPLE_TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200),
                    ["REPEAT_COUNT"] = big, ["REPEAT_INTERVAL"] = big, ["TIMES_TRIGGERED"] = big
                },
                ["CRON_TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200),
                    ["CRON_EXPRESSION"] = V(120), ["TIME_ZONE_ID"] = V(80)
                },
                ["BLOB_TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200), ["BLOB_DATA"] = blb
                },
                ["SIMPROP_TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200),
                    ["STR_PROP_1"] = V(512), ["STR_PROP_2"] = V(512), ["STR_PROP_3"] = V(512),
                    ["INT_PROP_1"] = "INT", ["INT_PROP_2"] = "INT",
                    ["LONG_PROP_1"] = big, ["LONG_PROP_2"] = big,
                    ["DEC_PROP_1"] = dec, ["DEC_PROP_2"] = dec,
                    ["BOOL_PROP_1"] = bit, ["BOOL_PROP_2"] = bit
                },
                ["CALENDARS"] = new Dictionary<string, string> { ["SCHED_NAME"] = V(120), ["CALENDAR_NAME"] = V(200), ["CALENDAR"] = blb },
                ["PAUSED_TRIGGER_GRPS"] = new Dictionary<string, string> { ["SCHED_NAME"] = V(120), ["TRIGGER_GROUP"] = V(200) },
                ["FIRED_TRIGGERS"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["ENTRY_ID"] = V(95), ["TRIGGER_NAME"] = V(200), ["TRIGGER_GROUP"] = V(200),
                    ["INSTANCE_NAME"] = V(200), ["FIRED_TIME"] = big, ["SCHED_TIME"] = big, ["PRIORITY"] = "INT",
                    ["STATE"] = V(16), ["JOB_NAME"] = V(200), ["JOB_GROUP"] = V(200),
                    ["IS_NONCONCURRENT"] = bit, ["REQUESTS_RECOVERY"] = bit
                },
                ["SCHEDULER_STATE"] = new Dictionary<string, string> {
                    ["SCHED_NAME"] = V(120), ["INSTANCE_NAME"] = V(200),
                    ["LAST_CHECKIN_TIME"] = big, ["CHECKIN_INTERVAL"] = big
                },
            };
        }

        // ── Factory ──
        private static DbProviderFactory GetFactory(string db) => db switch
        {
            "mysql"      => Resolve("MySql.Data.MySqlClient.MySqlClientFactory, MySql.Data"),
            "postgresql" => Resolve("Npgsql.NpgsqlFactory, Npgsql"),
            "oracle"     => Resolve("Oracle.ManagedDataAccess.Client.OracleClientFactory, Oracle.ManagedDataAccess"),
            "sqlite"     => Resolve("Microsoft.Data.Sqlite.SqliteFactory, Microsoft.Data.Sqlite"),
            _            => Resolve("Microsoft.Data.SqlClient.SqlClientFactory, Microsoft.Data.SqlClient")
                         ?? Resolve("System.Data.SqlClient.SqlClientFactory, System.Data.SqlClient"),
        };
        private static DbProviderFactory Resolve(string n) { try { var t = Type.GetType(n); return t?.GetField("Instance")?.GetValue(null) as DbProviderFactory; } catch { return null; } }

        // ── CREATE TABLE SQL ──
        private static string[] GetCreateSql(string p, string db)
        {
            var (big, blb, bit, dec, nv) = db switch
            {
                "postgresql" => ("BIGINT", "BYTEA", "BOOLEAN", "DECIMAL(13,4)", "VARCHAR"),
                "oracle"     => ("NUMBER(19)", "BLOB", "NUMBER(1)", "NUMBER(13,4)", "NVARCHAR2"),
                "sqlite"     => ("INTEGER", "BLOB", "INTEGER", "REAL", "TEXT"),
                _            => ("BIGINT", "VARBINARY(MAX)", "BIT", "DECIMAL(13,4)", "NVARCHAR"),
            };
            string V(int n) => db == "sqlserver" ? $"{nv}({n})" : $"VARCHAR({n})";
            string T(string n) => db == "sqlserver" ? $"IF OBJECT_ID('{p}{n}','U') IS NULL CREATE TABLE {p}{n}" : $"CREATE TABLE IF NOT EXISTS {p}{n}";
            string pk = "PRIMARY KEY";
            string fk = "REFERENCES";

            return new[] {
                $"{T("LOCKS")} (SCHED_NAME {V(120)} NOT NULL, LOCK_NAME {V(40)} NOT NULL, CONSTRAINT PK_{p}LOCKS {pk}(SCHED_NAME,LOCK_NAME))",
                $"{T("JOB_DETAILS")} (SCHED_NAME {V(120)} NOT NULL, JOB_NAME {V(200)} NOT NULL, JOB_GROUP {V(200)} NOT NULL, DESCRIPTION {V(250)}, JOB_CLASS_NAME {V(250)} NOT NULL, IS_DURABLE {bit} NOT NULL, IS_NONCONCURRENT {bit} NOT NULL, IS_UPDATE_DATA {bit} NOT NULL, REQUESTS_RECOVERY {bit} NOT NULL, JOB_DATA {blb}, CONSTRAINT PK_{p}JD {pk}(SCHED_NAME,JOB_NAME,JOB_GROUP))",
                $"{T("TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, JOB_NAME {V(200)} NOT NULL, JOB_GROUP {V(200)} NOT NULL, DESCRIPTION {V(250)}, NEXT_FIRE_TIME {big}, PREV_FIRE_TIME {big}, PRIORITY INT, TRIGGER_STATE {V(16)} NOT NULL, TRIGGER_TYPE {V(8)} NOT NULL, START_TIME {big} NOT NULL, END_TIME {big}, CALENDAR_NAME {V(200)}, MISFIRE_INSTR INT, JOB_DATA {blb}, CONSTRAINT PK_{p}TRG {pk}(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP), CONSTRAINT FK_{p}TRG FOREIGN KEY(SCHED_NAME,JOB_NAME,JOB_GROUP) {fk} {p}JOB_DETAILS(SCHED_NAME,JOB_NAME,JOB_GROUP))",
                $"{T("SIMPLE_TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, REPEAT_COUNT {big} NOT NULL, REPEAT_INTERVAL {big} NOT NULL, TIMES_TRIGGERED {big} NOT NULL, CONSTRAINT PK_{p}ST {pk}(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP))",
                $"{T("CRON_TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, CRON_EXPRESSION {V(120)} NOT NULL, TIME_ZONE_ID {V(80)}, CONSTRAINT PK_{p}CT {pk}(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP))",
                $"{T("BLOB_TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, BLOB_DATA {blb}, CONSTRAINT PK_{p}BT {pk}(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP))",
                $"{T("SIMPROP_TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, STR_PROP_1 {V(512)}, STR_PROP_2 {V(512)}, STR_PROP_3 {V(512)}, INT_PROP_1 INT, INT_PROP_2 INT, LONG_PROP_1 {big}, LONG_PROP_2 {big}, DEC_PROP_1 {dec}, DEC_PROP_2 {dec}, BOOL_PROP_1 {bit}, BOOL_PROP_2 {bit}, CONSTRAINT PK_{p}SPT {pk}(SCHED_NAME,TRIGGER_NAME,TRIGGER_GROUP))",
                $"{T("CALENDARS")} (SCHED_NAME {V(120)} NOT NULL, CALENDAR_NAME {V(200)} NOT NULL, CALENDAR {blb} NOT NULL, CONSTRAINT PK_{p}CAL {pk}(SCHED_NAME,CALENDAR_NAME))",
                $"{T("PAUSED_TRIGGER_GRPS")} (SCHED_NAME {V(120)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, CONSTRAINT PK_{p}PTG {pk}(SCHED_NAME,TRIGGER_GROUP))",
                $"{T("FIRED_TRIGGERS")} (SCHED_NAME {V(120)} NOT NULL, ENTRY_ID {V(95)} NOT NULL, TRIGGER_NAME {V(200)} NOT NULL, TRIGGER_GROUP {V(200)} NOT NULL, INSTANCE_NAME {V(200)} NOT NULL, FIRED_TIME {big} NOT NULL, SCHED_TIME {big} NOT NULL, PRIORITY INT NOT NULL, STATE {V(16)} NOT NULL, JOB_NAME {V(200)}, JOB_GROUP {V(200)}, IS_NONCONCURRENT {bit}, REQUESTS_RECOVERY {bit}, CONSTRAINT PK_{p}FT {pk}(SCHED_NAME,ENTRY_ID))",
                $"{T("SCHEDULER_STATE")} (SCHED_NAME {V(120)} NOT NULL, INSTANCE_NAME {V(200)} NOT NULL, LAST_CHECKIN_TIME {big} NOT NULL, CHECKIN_INTERVAL {big} NOT NULL, CONSTRAINT PK_{p}SS {pk}(SCHED_NAME,INSTANCE_NAME))",
            };
        }
    }
}
