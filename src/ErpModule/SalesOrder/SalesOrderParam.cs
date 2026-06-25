using Microi.net.Business;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单业务参数。
    /// </summary>
    public class SalesOrderParam : BusinessParam
    {
        public string CustomerId { get; set; }
        public string CustomerName { get; set; }
        public decimal? TotalAmount { get; set; }

        /// <summary>单据编号（新增时由服务生成）</summary>
        public string BillNo { get; set; }

        /// <summary>订单状态（新增时默认草稿）</summary>
        public int? Status { get; set; }

        /// <summary>下单日期</summary>
        public string OrderDate { get; set; }
    }
}
