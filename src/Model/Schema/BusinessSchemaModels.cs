using System.Collections.Generic;

namespace Microi.net.Business
{
    /// <summary>表在文档结构中的角色。</summary>
    public enum BusinessTableRole
    {
        /// <summary>主表</summary>
        Master = 0,

        /// <summary>明细（子）表（一对多）</summary>
        Detail = 1,

        /// <summary>扩展表（一对一）</summary>
        Extension = 2
    }

    /// <summary>一个数据库列的结构描述。</summary>
    public sealed class BusinessColumnInfo
    {
        public string Name { get; set; }
        public string DataType { get; set; }
        public string ColumnType { get; set; }
        public string Comment { get; set; }
        public bool Nullable { get; set; }
        public bool IsPrimaryKey { get; set; }
        /// <summary>是否平台系统字段（Id/CreateTime/...）。</summary>
        public bool IsSystem { get; set; }
    }

    /// <summary>文档中一张表（主/细/扩展）的结构。</summary>
    public sealed class BusinessTableInfo
    {
        public string TableName { get; set; }
        public string Label { get; set; }
        public BusinessTableRole Role { get; set; }
        /// <summary>明细表外键列名（仅 Detail 有值）。</summary>
        public string ForeignKey { get; set; }
        /// <summary>返回 JSON 中的属性名（明细集合 / 扩展合并使用）。</summary>
        public string PropertyName { get; set; }
        /// <summary>表是否已在数据库中物理存在。</summary>
        public bool Exists { get; set; }
        public List<BusinessColumnInfo> Columns { get; set; } = new List<BusinessColumnInfo>();
    }

    /// <summary>一个业务文档的完整结构（主表 + 明细 + 扩展）。</summary>
    public sealed class BusinessDocumentSchema
    {
        public string MasterTable { get; set; }
        public string Label { get; set; }
        public BusinessTableInfo Master { get; set; }
        public List<BusinessTableInfo> Details { get; set; } = new List<BusinessTableInfo>();
        public List<BusinessTableInfo> Extensions { get; set; } = new List<BusinessTableInfo>();
    }
}
