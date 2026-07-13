using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 标记一个实体为业务数据表，启动时由 Schema 初始化器据此自动建表/补列。
    /// 系统字段（Id/CreateTime/UpdateTime/UserId/UserName/IsDeleted）由平台 DDL 自动创建，无需声明。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class BusinessTableAttribute : Attribute
    {
        /// <summary>
        /// 表名（即表单引擎 Key），如 "erp_sales_order"。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 表注释/说明。
        /// </summary>
        public string Comment { get; set; }

        /// <summary>
        /// 是否内部/基础设施表（如字段配置表）。
        /// 内部表仍会自动建表，但不作为业务文档出现在结构查看列表中。
        /// </summary>
        public bool Internal { get; set; }

        public BusinessTableAttribute(string name)
        {
            Name = name;
        }
    }
}
