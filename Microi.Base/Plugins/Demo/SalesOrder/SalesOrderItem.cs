using Microi.net.Business;

namespace Microi.Plugin.Demo.SalesOrder
{
    [BusinessTable("erp_sales_order_item", Comment = "ERP-销售订单明细")]
    public class SalesOrderItem : BusinessEntity
    {
        public string OrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal? Qty { get; set; }
        public decimal? Price { get; set; }
        public decimal? Amount { get; set; }
    }
}
