using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务实体属性的列映射配置（可选）。
    /// 不加此特性的公共属性也会被映射，类型按 CLR 类型自动推断。
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = false)]
    public sealed class BusinessColumnAttribute : Attribute
    {
        /// <summary>
        /// 显式指定 SQL 类型（如 "decimal(18,2)"、"varchar(100)"）。
        /// 为空则按 CLR 类型 + Length 自动推断方言类型。
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 字符串列长度（默认 255；设为 0 或负数表示使用大文本类型 text/nvarchar(max)）。
        /// 仅在未显式指定 Type 时生效。
        /// </summary>
        public int Length { get; set; } = 255;

        /// <summary>
        /// 是否 NOT NULL（默认 false，即允许为空）。
        /// </summary>
        public bool NotNull { get; set; }

        /// <summary>
        /// 列注释。
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// 是否忽略该属性（不映射为数据库列）。
        /// </summary>
        public bool Ignore { get; set; }
    }
}
