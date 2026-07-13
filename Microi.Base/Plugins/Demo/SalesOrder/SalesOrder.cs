using Microi.net.Business;

namespace Microi.Plugin.Demo.SalesOrder
{
    /// <summary>
    /// 销售订单实体（Demo 示例）。
    /// 对应数据表（表单引擎 Key）：erp_sales_order。
    /// </summary>
    [BusinessTable("erp_sales_order", Comment = "ERP-销售订单")]
    [BusinessExtensionTable(typeof(SalesOrderExt))]
    [BusinessDetailTable(typeof(SalesOrderItem), "OrderId", PropertyName = "Items")]
    public class SalesOrder : BusinessStatefulEntity<SalesOrderStatus>
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal? TotalAmount { get; set; }
        public System.DateTime? OrderDate { get; set; }
        public string AuditorId { get; set; }
        public System.DateTime? AuditTime { get; set; }
    }
}
