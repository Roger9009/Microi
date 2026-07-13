using Microi.net.Business;

namespace Microi.Plugin.Demo.WorkOrder
{
    [BusinessTable("mes_work_order", Comment = "MES-生产工单")]
    [BusinessExtensionTable(typeof(WorkOrderExt))]
    [BusinessDetailTable(typeof(WorkOrderItem), "WorkOrderId", PropertyName = "Items")]
    public class WorkOrder : BusinessStatefulEntity<WorkOrderStatus>
    {
        public string SalesOrderId { get; set; }
        public string ProductId { get; set; }
        public string ProductName { get; set; }
        public decimal? PlanQty { get; set; }
        public decimal? CompletedQty { get; set; }
        public System.DateTime? PlanStartTime { get; set; }
        public System.DateTime? ActualStartTime { get; set; }
        public System.DateTime? ActualEndTime { get; set; }
    }
}
