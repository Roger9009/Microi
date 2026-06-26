using System.Collections.Generic;

namespace Microi.net.Business
{
    /// <summary>
    /// 已解析的字段定义：物理列信息 + 字段配置，合并后用于前端加载/显示与配置编辑。
    /// </summary>
    public sealed class BusinessFieldDef
    {
        /// <summary>字段名。</summary>
        public string Name { get; set; }

        // ---- 物理列信息 ----
        /// <summary>物理列是否存在。</summary>
        public bool PhysicalExists { get; set; }
        /// <summary>物理 data_type。</summary>
        public string DataType { get; set; }
        /// <summary>物理 column_type。</summary>
        public string ColumnType { get; set; }
        /// <summary>物理列注释。</summary>
        public string PhysicalComment { get; set; }
        /// <summary>是否平台系统字段。</summary>
        public bool IsSystem { get; set; }

        // ---- 配置信息（可编辑）----
        /// <summary>配置记录 Id（已有配置时有值）。</summary>
        public string ConfigId { get; set; }
        /// <summary>是否已存在配置记录。</summary>
        public bool HasConfig { get; set; }

        /// <summary>字段描述（显示名）。</summary>
        public string Description { get; set; }
        /// <summary>多语言 ID。</summary>
        public string LangId { get; set; }
        /// <summary>逻辑字段类型。</summary>
        public string FieldType { get; set; }
        /// <summary>来源类型。</summary>
        public string SourceType { get; set; }
        /// <summary>输入方式（组件）。</summary>
        public string Component { get; set; }
        /// <summary>是否主键。</summary>
        public bool IsPrimaryKey { get; set; }
        /// <summary>是否参与更新。</summary>
        public bool IsUpdate { get; set; }
        /// <summary>是否强制隐藏。</summary>
        public bool ForceHidden { get; set; }
        /// <summary>是否默认显示。</summary>
        public bool DefaultVisible { get; set; }
        /// <summary>是否必填。</summary>
        public bool Required { get; set; }
        /// <summary>排序号。</summary>
        public int SortNo { get; set; }
    }

    /// <summary>
    /// 字段配置批量保存参数。
    /// </summary>
    public class BusinessFieldConfigSaveParam : BusinessParam
    {
        /// <summary>目标表名。</summary>
        public string TableName { get; set; }

        /// <summary>字段配置列表。</summary>
        public List<BusinessFieldConfig> Fields { get; set; } = new List<BusinessFieldConfig>();
    }

    /// <summary>
    /// 字段配置批量导入参数。
    /// </summary>
    public class BusinessFieldConfigImportParam : BusinessParam
    {
        /// <summary>
        /// 要导入的字段配置列表（从 ExportFieldConfigs 接口获取的 Data 数组直接传入）。
        /// 每条记录必须包含 TableName + FieldName，其余字段按实际填写。
        /// </summary>
        public List<BusinessFieldConfig> Configs { get; set; } = new List<BusinessFieldConfig>();
    }
}
