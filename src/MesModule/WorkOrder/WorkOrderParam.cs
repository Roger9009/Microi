using Microi.net.Business;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单业务参数。
    /// </summary>
    public class WorkOrderParam : BusinessParam
    {
        public string SalesOrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal? PlanQty { get; set; }

        /// <summary>单据编号（新增时由服务生成）</summary>
        public string BillNo { get; set; }

        /// <summary>工单状态（新增时默认已创建）</summary>
        public int? Status { get; set; }
    }
}
