namespace Microi.net.Business
{
    /// <summary>
    /// 业务文档动态关系（内部表）。
    /// 存储通过前端「新建扩展表/绑定关系」创建的主表→扩展表/明细表关联，
    /// 与代码特性 [BusinessExtensionTable]/[BusinessDetailTable] 互补，二者合并使用。
    /// 对应数据表：business_doc_relation（自动建表）。
    /// </summary>
    [BusinessTable("business_doc_relation", Comment = "业务文档动态关系", Internal = true)]
    public class BusinessDocRelation : BusinessEntity
    {
        /// <summary>主表名（如 erp_sales_order）。</summary>
        public string MasterTable { get; set; }

        /// <summary>关联表名（扩展表或明细表）。</summary>
        public string RelationTable { get; set; }

        /// <summary>关联类型：Extension（1:1 扩展） / Detail（1:N 明细）。</summary>
        public string RelationType { get; set; }

        /// <summary>明细表外键列名（仅 Detail 时有值，如 OrderId）。</summary>
        public string ForeignKey { get; set; }

        /// <summary>明细集合在 JSON 中的属性名（默认取 RelationTable）。</summary>
        public string PropertyName { get; set; }

        /// <summary>显示标签（中文名）。</summary>
        public string Label { get; set; }
    }
}
