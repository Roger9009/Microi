using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Microi.net
{
    /// <summary>
    /// 升级 SQL 方言转换器：MySQL → 目标数据库。
    /// 在执行升级 SQL 之前调用 Convert()，确保 SQL 兼容当前数据库。
    /// </summary>
    public static class UpgradeSqlConverter
    {
        /// <summary>
        /// 将 MySQL 方言 SQL 转换为目标数据库方言。
        /// </summary>
        public static string Convert(string sql, string dbType)
        {
            if (string.IsNullOrWhiteSpace(sql)) return sql;
            var db = (dbType ?? "").ToLower();
            if (db == "mysql") return sql;

            sql = RemoveMySqlOnly(sql);

            switch (db)
            {
                case "sqlserver":
                case "sqlserver9":
                case "mssql":
                    return ToSqlServer(sql);
                case "postgresql":
                    return ToPostgreSql(sql);
                case "oracle":
                    return ToOracle(sql);
                case "sqlite":
                case "sqlite3":
                    return ToSqlite(sql);
                default:
                    return ToSqlServer(sql);
            }
        }

        // ══════════════════════════════════════════════
        //  通用清理
        // ══════════════════════════════════════════════

        private static string RemoveMySqlOnly(string sql)
        {
            sql = Regex.Replace(sql, @"SET\s+NAMES\s+\w+;?", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"SET\s+FOREIGN_KEY_CHECKS\s*=\s*\d+;?", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"BEGIN\s*;", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"^\s*COMMIT\s*;?\s*$", "", RegexOptions.Multiline | RegexOptions.IgnoreCase);
            // 仅移除整行注释，避免误伤字符串内的 --
            sql = Regex.Replace(sql, @"^\s*--.*$", "", RegexOptions.Multiline);
            sql = Regex.Replace(sql, @"/\*[\s\S]*?\*/", "");
            sql = Regex.Replace(sql, @"ENGINE\s*=\s*\w+", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"DEFAULT\s+CHARSET\s*=\s*\w+", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"CHARACTER\s+SET\s+\w+", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"COLLATE\s+\w+", "", RegexOptions.IgnoreCase);
            // 表级 COMMENT='...' / COMMENT = '...'
            sql = Regex.Replace(sql, @"\s*COMMENT\s*=\s*'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @",\s*\)", ")");
            sql = Regex.Replace(sql, @";\s*;+", ";");
            return sql;
        }

        // ══════════════════════════════════════════════
        //  SQL Server
        // ══════════════════════════════════════════════

        private static string ToSqlServer(string sql)
        {
            // MySQL 字符串转义 \' → SQL Server ''
            sql = ConvertMysqlStringEscapes(sql);

            // bit 字面量必须在保护字符串之前处理，否则 b'0' 中的 '0' 会被当成字符串吃掉
            sql = Regex.Replace(sql, @"b'0'", "0");
            sql = Regex.Replace(sql, @"b'1'", "1");

            // 保护字符串字面量，避免类型替换误伤 JSON/文本内容（如 "Text"、can't）
            var protectedSql = ProtectStrings(sql, out var strings);

            protectedSql = Regex.Replace(protectedSql, @"`([^`]*)`", @"[$1]");

            // ALTER TABLE ... ADD [COLUMN] → 幂等（避免重复列）
            protectedSql = Regex.Replace(
                protectedSql,
                @"(?<!NULL\s)ALTER\s+TABLE\s+\[(\w+)\]\s+ADD\s+(?:COLUMN\s+)?\[(\w+)\]\s+([^;]+);",
                m => $"IF COL_LENGTH('{m.Groups[1].Value}', '{m.Groups[2].Value}') IS NULL ALTER TABLE [{m.Groups[1].Value}] ADD [{m.Groups[2].Value}] {m.Groups[3].Value.Trim()};",
                RegexOptions.IgnoreCase);

            // 数据类型（仅在非字符串区域）
            protectedSql = Regex.Replace(protectedSql, @"\bLONGTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bMEDIUMTEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bTINYTEXT\b", "NVARCHAR(255)", RegexOptions.IgnoreCase);
            // TEXT 作为类型：前面通常是列定义空白/逗号，避免替换标识符中的片段
            protectedSql = Regex.Replace(protectedSql, @"(?<=[\s,\[\(])TEXT\b", "NVARCHAR(MAX)", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bDATETIME\b(?!2)", "DATETIME2", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bTINYINT\b", "SMALLINT", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bBIT\s*\(\s*\d+\s*\)", "BIT", RegexOptions.IgnoreCase);
            protectedSql = Regex.Replace(protectedSql, @"\bVARCHAR\b", "NVARCHAR", RegexOptions.IgnoreCase);

            // 列级 COMMENT '...'
            protectedSql = Regex.Replace(protectedSql, @"\s+COMMENT\s+__STR\d+__", "", RegexOptions.IgnoreCase);
            // 若 COMMENT 未进入占位符（少见），再清一次
            protectedSql = Regex.Replace(protectedSql, @"\s+COMMENT\s+'([^']|'')*'", "", RegexOptions.IgnoreCase);

            protectedSql = Regex.Replace(protectedSql, @"\bAUTO_INCREMENT\b", "IDENTITY(1,1)", RegexOptions.IgnoreCase);

            // CREATE TABLE IF NOT EXISTS
            protectedSql = Regex.Replace(protectedSql,
                @"CREATE\s+TABLE\s+IF\s+NOT\s+EXISTS\s+\[(\w+)\]",
                m => $"IF OBJECT_ID(N'[{m.Groups[1].Value}]','U') IS NULL CREATE TABLE [{m.Groups[1].Value}]",
                RegexOptions.IgnoreCase);

            // 普通 CREATE TABLE（无 IF NOT EXISTS）也做成幂等，避免二次启动失败
            protectedSql = Regex.Replace(protectedSql,
                @"(?<!NULL\s)CREATE\s+TABLE\s+\[(\w+)\]",
                m => $"IF OBJECT_ID(N'[{m.Groups[1].Value}]','U') IS NULL CREATE TABLE [{m.Groups[1].Value}]",
                RegexOptions.IgnoreCase);

            // UNIQUE KEY → CONSTRAINT UNIQUE
            protectedSql = Regex.Replace(protectedSql,
                @"UNIQUE\s+KEY\s+\[(\w+)\]\s*\(\[(\w+)\]\)",
                m => $"CONSTRAINT [{m.Groups[1].Value}] UNIQUE([{m.Groups[2].Value}])",
                RegexOptions.IgnoreCase);

            // 内联 KEY/INDEX 不能写在 SQL Server CREATE TABLE 里，抽成事后 CREATE INDEX
            var indexSql = new StringBuilder();
            protectedSql = Regex.Replace(protectedSql,
                @",?\s*\b(?:KEY|INDEX)\s+\[(\w+)\]\s*\(\[(\w+)\]\)",
                m =>
                {
                    // 需要所属表名：在最近的 CREATE TABLE [xxx] 中查找（简化：后处理整段）
                    return $"/*__IDX__{m.Groups[1].Value}__{m.Groups[2].Value}__*/";
                },
                RegexOptions.IgnoreCase);

            protectedSql = RestoreStrings(protectedSql, strings);

            // 将 INDEX 占位符转为独立 CREATE INDEX，并绑定到最近的表名
            protectedSql = MaterializeInlineIndexes(protectedSql);

            // INSERT 按主键幂等，避免重复跑升级脚本时 PK 冲突中断整批
            protectedSql = MakeInsertIdempotent(protectedSql);

            protectedSql = Regex.Replace(protectedSql, @"\n\s*\n\s*\n", "\n\n");
            return protectedSql;
        }

        /// <summary>
        /// INSERT INTO [t] ([Id],...) VALUES ('id',...) → IF NOT EXISTS ... INSERT
        /// </summary>
        private static string MakeInsertIdempotent(string sql)
        {
            return Regex.Replace(sql,
                @"INSERT\s+INTO\s+\[(\w+)\]\s*\(\s*\[Id\]([^)]*)\)\s*VALUES\s*\(\s*'([^']+)'",
                m => $"IF NOT EXISTS (SELECT 1 FROM [{m.Groups[1].Value}] WHERE [Id]=N'{m.Groups[3].Value}') INSERT INTO [{m.Groups[1].Value}] ([Id]{m.Groups[2].Value}) VALUES (N'{m.Groups[3].Value}'",
                RegexOptions.IgnoreCase);
        }

        /// <summary>
        /// 将 /*__IDX__name__col__*/ 占位符替换为对该 CREATE TABLE 的 CREATE INDEX 语句。
        /// </summary>
        private static string MaterializeInlineIndexes(string sql)
        {
            var sb = new StringBuilder();
            var parts = Regex.Split(sql, @"(?=IF OBJECT_ID\(N'\[\w+\]','U'\) IS NULL CREATE TABLE)");
            foreach (var part in parts)
            {
                if (string.IsNullOrWhiteSpace(part))
                {
                    sb.Append(part);
                    continue;
                }

                var tableMatch = Regex.Match(part, @"CREATE TABLE \[(\w+)\]", RegexOptions.IgnoreCase);
                var tableName = tableMatch.Success ? tableMatch.Groups[1].Value : null;
                var indexes = new List<(string Name, string Col)>();

                var cleaned = Regex.Replace(part, @"/\*__IDX__(\w+)__(\w+)__\*/", m =>
                {
                    indexes.Add((m.Groups[1].Value, m.Groups[2].Value));
                    return "";
                });

                // 清理可能残留的多余逗号
                cleaned = Regex.Replace(cleaned, @",\s*\)", ")");
                cleaned = Regex.Replace(cleaned, @",\s*,", ",");

                sb.Append(cleaned);

                if (tableName != null)
                {
                    foreach (var idx in indexes)
                    {
                        sb.AppendLine();
                        sb.Append($"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'{idx.Name}' AND object_id = OBJECT_ID(N'[{tableName}]')) CREATE INDEX [{idx.Name}] ON [{tableName}]([{idx.Col}]);");
                    }
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// MySQL 字符串内 \' → SQL Server ''
        /// </summary>
        private static string ConvertMysqlStringEscapes(string sql)
        {
            var sb = new StringBuilder(sql.Length);
            var inString = false;
            for (int i = 0; i < sql.Length; i++)
            {
                var c = sql[i];
                if (!inString)
                {
                    if (c == '\'')
                    {
                        inString = true;
                        sb.Append(c);
                    }
                    else sb.Append(c);
                    continue;
                }

                // 已在字符串内
                if (c == '\\' && i + 1 < sql.Length)
                {
                    var next = sql[i + 1];
                    if (next == '\'')
                    {
                        sb.Append("''"); // MySQL \' → MSSQL ''
                        i++;
                        continue;
                    }
                    if (next == '\\')
                    {
                        sb.Append('\\');
                        i++;
                        continue;
                    }
                }

                if (c == '\'')
                {
                    // SQL 标准 '' 转义，或字符串结束
                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        sb.Append("''");
                        i++;
                        continue;
                    }
                    inString = false;
                    sb.Append(c);
                    continue;
                }

                sb.Append(c);
            }
            return sb.ToString();
        }

        private static string ProtectStrings(string sql, out List<string> strings)
        {
            strings = new List<string>();
            var sb = new StringBuilder(sql.Length);
            var inString = false;
            var current = new StringBuilder();

            for (int i = 0; i < sql.Length; i++)
            {
                var c = sql[i];
                if (!inString)
                {
                    if (c == '\'')
                    {
                        inString = true;
                        current.Clear();
                        current.Append(c);
                    }
                    else sb.Append(c);
                    continue;
                }

                current.Append(c);
                if (c == '\'')
                {
                    if (i + 1 < sql.Length && sql[i + 1] == '\'')
                    {
                        current.Append(sql[++i]); // 保留 ''
                        continue;
                    }
                    // 字符串结束
                    inString = false;
                    var idx = strings.Count;
                    strings.Add(current.ToString());
                    sb.Append($"__STR{idx}__");
                }
            }

            if (inString)
            {
                // 未闭合，原样追加，避免吞掉内容
                sb.Append(current);
            }

            return sb.ToString();
        }

        private static string RestoreStrings(string sql, List<string> strings)
        {
            for (int i = 0; i < strings.Count; i++)
                sql = sql.Replace($"__STR{i}__", strings[i]);
            return sql;
        }

        // ══════════════════════════════════════════════
        //  PostgreSQL / Oracle / SQLite
        // ══════════════════════════════════════════════

        private static string ToPostgreSql(string sql)
        {
            sql = ConvertMysqlStringEscapes(sql);
            sql = BacktickToQuote(sql);
            sql = Regex.Replace(sql, @"\bLONGTEXT\b", "TEXT", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bBIT\s*\(\s*\d+\s*\)", "BOOLEAN", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'0'", "FALSE", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'1'", "TRUE", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s+COMMENT\s+'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s*COMMENT\s*=\s*'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bAUTO_INCREMENT\b", "GENERATED BY DEFAULT AS IDENTITY", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bDATETIME\b", "TIMESTAMP", RegexOptions.IgnoreCase);
            return sql;
        }

        private static string ToOracle(string sql)
        {
            sql = ConvertMysqlStringEscapes(sql);
            sql = BacktickToQuote(sql);
            sql = Regex.Replace(sql, @"\bLONGTEXT\b", "CLOB", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bTEXT\b", "CLOB", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bBIT\s*\(\s*\d+\s*\)", "NUMBER(1)", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'0'", "0", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'1'", "1", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s+COMMENT\s+'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s*COMMENT\s*=\s*'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bAUTO_INCREMENT\b", "GENERATED BY DEFAULT AS IDENTITY", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bDATETIME\b", "TIMESTAMP", RegexOptions.IgnoreCase);
            return sql;
        }

        private static string ToSqlite(string sql)
        {
            sql = ConvertMysqlStringEscapes(sql);
            sql = BacktickToQuote(sql);
            sql = Regex.Replace(sql, @"\bLONGTEXT\b", "TEXT", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bBIT\s*\(\s*\d+\s*\)", "INTEGER", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'0'", "0", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"b'1'", "1", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s+COMMENT\s+'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\s*COMMENT\s*=\s*'([^']|'')*'", "", RegexOptions.IgnoreCase);
            sql = Regex.Replace(sql, @"\bAUTO_INCREMENT\b", "AUTOINCREMENT", RegexOptions.IgnoreCase);
            return sql;
        }

        private static string BacktickToQuote(string sql)
        {
            return sql.Replace('`', '"');
        }
    }
}
