using Microi.net.Business;

namespace Microi.Plugin.Demo.WorkOrder
{
    [BusinessTable("mes_work_order_item", Comment = "MES-生产工单工序明细")]
    public class WorkOrderItem : BusinessEntity
    {
        public string WorkOrderId { get; set; }
        public string ProcessNo { get; set; }
        public string ProcessName { get; set; }
        public string WorkCenterId { get; set; }
        public string WorkCenterName { get; set; }
        public decimal? PlanHours { get; set; }
        public decimal? ActualHours { get; set; }
        public int? SortNo { get; set; }
    }
}
