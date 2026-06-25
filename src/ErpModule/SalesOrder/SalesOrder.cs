using Microi.net.Business;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单实体（示例）。
    /// 对应数据表（表单引擎 Key）：erp_sales_order。
    /// 启动时由 Schema 初始化器自动建表/补列。
    /// </summary>
    [BusinessTable("erp_sales_order", Comment = "ERP-销售订单")]
    [BusinessExtensionTable(typeof(SalesOrderExt))]
    [BusinessDetailTable(typeof(SalesOrderItem), "OrderId", PropertyName = "Items")]
    public class SalesOrder : BusinessStatefulEntity<SalesOrderStatus>
    {
        /// <summary>客户 Id</summary>
        public string CustomerId { get; set; }

        /// <summary>客户名称（冗余便于展示）</summary>
        public string CustomerName { get; set; }

        /// <summary>订单总金额</summary>
        public decimal? TotalAmount { get; set; }

        /// <summary>下单日期</summary>
        public System.DateTime? OrderDate { get; set; }

        /// <summary>审核人 Id</summary>
        public string AuditorId { get; set; }

        /// <summary>审核时间</summary>
        public System.DateTime? AuditTime { get; set; }
    }
}
