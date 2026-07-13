using Microi.net.Business;

namespace Microi.Plugin.Demo.WorkOrder
{
    public class WorkOrderParam : BusinessParam
    {
        public string SalesOrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal? PlanQty { get; set; }
        public string BillNo { get; set; }
        public int? Status { get; set; }
    }
}
