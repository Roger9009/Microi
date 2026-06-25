namespace Microi.net.Business
{
    /// <summary>
    /// 业务字段配置（主/明细/扩展表通用）。
    /// 用于配置字段的加载与显示行为：描述、语言ID、字段类型、来源类型、主键、输入方式、
    /// 是否更新、强制隐藏、默认显示等。按 (TableName, FieldName) 唯一。
    /// 对应数据表：business_field_config（内部表，自动建表）。
    /// </summary>
    [BusinessTable("business_field_config", Comment = "业务字段配置", Internal = true)]
    public class BusinessFieldConfig : BusinessEntity
    {
        /// <summary>所属物理表名。</summary>
        public string TableName { get; set; }

        /// <summary>字段名（物理列名或虚拟字段名）。</summary>
        public string FieldName { get; set; }

        /// <summary>字段描述（显示名）。</summary>
        public string Description { get; set; }

        /// <summary>多语言 ID（关联语言资源 Key，可空）。</summary>
        public string LangId { get; set; }

        /// <summary>字段类型（逻辑类型）：string/text/int/long/decimal/double/bool/datetime/raw。</summary>
        public string FieldType { get; set; }

        /// <summary>来源类型：Physical(物理列)/Virtual(虚拟)/Relation(关联)/Formula(公式)。</summary>
        public string SourceType { get; set; }

        /// <summary>输入方式（前端组件）：Text/Textarea/NumberText/Select/MultiSelect/DatePicker/DateTimePicker/Switch/Radio/Checkbox/Upload/Editor。</summary>
        public string Component { get; set; }

        /// <summary>是否主键。</summary>
        public bool? IsPrimaryKey { get; set; }

        /// <summary>是否参与更新（false 时更新主单将忽略该字段）。</summary>
        public bool? IsUpdate { get; set; }

        /// <summary>是否强制隐藏（前端始终不展示）。</summary>
        public bool? ForceHidden { get; set; }

        /// <summary>是否默认显示（列表/表单默认可见）。</summary>
        public bool? DefaultVisible { get; set; }

        /// <summary>是否必填。</summary>
        public bool? Required { get; set; }

        /// <summary>排序号（越小越靠前）。</summary>
        public int? SortNo { get; set; }
    }
}
