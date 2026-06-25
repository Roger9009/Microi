using Microi.net.Business;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单明细行（一对多，示例）。
    /// 通过 OrderId 关联主表。对应数据表：erp_sales_order_item。
    /// </summary>
    [BusinessTable("erp_sales_order_item", Comment = "ERP-销售订单明细")]
    public class SalesOrderItem : BusinessEntity
    {
        /// <summary>主表订单 Id（外键）</summary>
        public string OrderId { get; set; }

        /// <summary>产品 Id</summary>
        public string ProductId { get; set; }

        /// <summary>产品名称</summary>
        public string ProductName { get; set; }

        /// <summary>数量</summary>
        public decimal? Qty { get; set; }

        /// <summary>单价</summary>
        public decimal? Price { get; set; }

        /// <summary>金额</summary>
        public decimal? Amount { get; set; }
    }
}
