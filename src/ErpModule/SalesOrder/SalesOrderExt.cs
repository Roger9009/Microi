using Microi.net.Business;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单扩展表（一对一，示例）。
    /// 与主表共用相同 Id。自定义字段可在前端动态添加到此表，读取主单时自动合并。
    /// 对应数据表：erp_sales_order_ext。
    /// </summary>
    [BusinessTable("erp_sales_order_ext", Comment = "ERP-销售订单扩展")]
    public class SalesOrderExt : BusinessEntity
    {
        /// <summary>示例扩展字段：发票抬头</summary>
        public string InvoiceTitle { get; set; }
    }
}
