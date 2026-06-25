using System;
using Dos.ORM;

namespace Microi.net.Business
{
    /// <summary>
    /// CLR 类型 → 各数据库方言 SQL 类型映射。
    /// 用于代码优先建表时为实体属性推断列类型。
    /// </summary>
    public static class SqlTypeMapper
    {
        /// <summary>
        /// 根据 CLR 类型与数据库类型推断 SQL 列类型。
        /// </summary>
        /// <param name="clrType">属性类型（可空类型会自动解包）</param>
        /// <param name="dbType">目标数据库类型</param>
        /// <param name="length">字符串长度（&lt;=0 表示大文本）</param>
        public static string Map(Type clrType, DatabaseType dbType, int length = 255)
        {
            var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
            if (t.IsEnum) t = Enum.GetUnderlyingType(t);

            if (t == typeof(string) || t == typeof(Guid))
                return StringType(dbType, t == typeof(Guid) ? 36 : length);
            if (t == typeof(bool))
                return BoolType(dbType);
            if (t == typeof(byte) || t == typeof(sbyte) || t == typeof(short) || t == typeof(ushort) || t == typeof(int) || t == typeof(uint))
                return "int";
            if (t == typeof(long) || t == typeof(ulong))
                return "bigint";
            if (t == typeof(decimal))
                return "decimal(18,4)";
            if (t == typeof(float))
                return dbType == DatabaseType.SqlServer ? "real" : "float";
            if (t == typeof(double))
                return dbType == DatabaseType.SqlServer ? "float" : "double";
            if (t == typeof(DateTime) || t == typeof(DateTimeOffset))
                return dbType == DatabaseType.Oracle || dbType == DatabaseType.DaMeng ? "timestamp" : "datetime";

            // 兜底：当作字符串存储
            return StringType(dbType, length);
        }

        private static string StringType(DatabaseType dbType, int length)
        {
            bool large = length <= 0;
            switch (dbType)
            {
                case DatabaseType.SqlServer:
                    return large ? "nvarchar(max)" : $"nvarchar({length})";
                case DatabaseType.Oracle:
                case DatabaseType.DaMeng:
                    return large ? "clob" : $"varchar2({length})";
                case DatabaseType.PostgreSql:
                case DatabaseType.KingBase:
                    return large ? "text" : $"varchar({length})";
                case DatabaseType.MySql:
                default:
                    return large ? "longtext" : $"varchar({length})";
            }
        }

        private static string BoolType(DatabaseType dbType)
        {
            switch (dbType)
            {
                case DatabaseType.SqlServer:
                    return "bit";
                case DatabaseType.Oracle:
                case DatabaseType.DaMeng:
                    return "number(1)";
                case DatabaseType.PostgreSql:
                case DatabaseType.KingBase:
                    return "boolean";
                case DatabaseType.MySql:
                default:
                    return "tinyint(1)";
            }
        }
    }
}
