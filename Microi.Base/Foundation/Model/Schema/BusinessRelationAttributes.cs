using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 声明主表的一对多明细（子）表。标注在主表实体上，可多次标注。
    /// 明细表实体本身也需标注 [BusinessTable]，并包含指向主表的外键列。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class BusinessDetailTableAttribute : Attribute
    {
        /// <summary>明细表实体类型（须标注 [BusinessTable]）。</summary>
        public Type EntityType { get; }

        /// <summary>明细表中指向主表 Id 的外键列名，如 "OrderId"。</summary>
        public string ForeignKey { get; }

        /// <summary>读取主单时，明细集合在返回 JSON 中的属性名（默认取明细表名）。</summary>
        public string PropertyName { get; set; }

        public BusinessDetailTableAttribute(Type entityType, string foreignKey)
        {
            EntityType = entityType;
            ForeignKey = foreignKey;
        }
    }

    /// <summary>
    /// 声明主表的一对一扩展表。标注在主表实体上，可多次标注。
    /// 扩展表实体本身也需标注 [BusinessTable]，与主表共用相同的 Id（主键即外键）。
    /// 读取主单时，扩展表的列会自动合并进主对象。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public sealed class BusinessExtensionTableAttribute : Attribute
    {
        /// <summary>扩展表实体类型（须标注 [BusinessTable]）。</summary>
        public Type EntityType { get; }

        public BusinessExtensionTableAttribute(Type entityType)
        {
            EntityType = entityType;
        }
    }
}
