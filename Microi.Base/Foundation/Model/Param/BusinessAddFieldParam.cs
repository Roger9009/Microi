namespace Microi.net.Business
{
    /// <summary>
    /// 业务表结构查询参数。
    /// </summary>
    public class BusinessSchemaQueryParam : BusinessParam
    {
        /// <summary>文档主表名。</summary>
        public string MasterTable { get; set; }

        /// <summary>单表名（查单表列结构 / 字段配置时使用）。</summary>
        public string TableName { get; set; }

        /// <summary>字段名（删除字段配置时使用）。</summary>
        public string FieldName { get; set; }
    }

    /// <summary>
    /// 动态加字段参数。
    /// </summary>
    public class BusinessAddFieldParam : BusinessParam
    {
        /// <summary>文档主表名（用于校验目标表归属该文档）。</summary>
        public string MasterTable { get; set; }

        /// <summary>目标表名（可为主表、明细表或扩展表）。</summary>
        public string TargetTable { get; set; }

        /// <summary>字段名（仅字母/数字/下划线）。</summary>
        public string FieldName { get; set; }

        /// <summary>
        /// 字段数据类型预设：string/text/int/long/decimal/double/bool/datetime/raw。
        /// 为 raw 时使用 RawType 指定原始 SQL 类型。
        /// </summary>
        public string DataType { get; set; }

        /// <summary>字符串长度（DataType=string 时生效，默认 255）。</summary>
        public int? Length { get; set; }

        /// <summary>原始 SQL 类型（DataType=raw 时使用，如 "decimal(18,2)"）。</summary>
        public string RawType { get; set; }

        /// <summary>是否 NOT NULL。</summary>
        public bool NotNull { get; set; }

        /// <summary>字段注释/显示名。</summary>
        public string Label { get; set; }
    }
}
