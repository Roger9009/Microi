using Microi.net.Business;

namespace Microi.Plugin.Demo.SalesOrder
{
    [BusinessTable("erp_sales_order_ext", Comment = "ERP-销售订单扩展")]
    public class SalesOrderExt : BusinessEntity
    {
        public string InvoiceTitle { get; set; }
    }
}
