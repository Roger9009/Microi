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
        /// <summary>动态关系记录 Id（来自 business_doc_relation）；静态（代码特性）时为 null。</summary>
        public string RelationId { get; set; }
        /// <summary>是否为动态关系（前端可解绑）。</summary>
        public bool IsDynamic => !string.IsNullOrWhiteSpace(RelationId);
        public List<BusinessColumnInfo> Columns { get; set; } = new List<BusinessColumnInfo>();
    }

    /// <summary>绑定扩展/明细表到主表的请求参数。</summary>
    public class BusinessBindRelationParam : BusinessParam
    {
        /// <summary>主表名。</summary>
        public string MasterTable { get; set; }
        /// <summary>关联表名（扩展表或明细表）。</summary>
        public string RelationTable { get; set; }
        /// <summary>关联类型：Extension / Detail。</summary>
        public string RelationType { get; set; }
        /// <summary>明细表外键列名（Detail 时必填）。</summary>
        public string ForeignKey { get; set; }
        /// <summary>明细集合在 JSON 中的属性名（选填）。</summary>
        public string PropertyName { get; set; }
        /// <summary>显示标签。</summary>
        public string Label { get; set; }
    }

    /// <summary>解除关系绑定请求参数。</summary>
    public class BusinessUnbindRelationParam : BusinessParam
    {
        /// <summary>business_doc_relation 记录 Id。</summary>
        public string RelationId { get; set; }
        /// <summary>主表名（用于缓存失效）。</summary>
        public string MasterTable { get; set; }
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
