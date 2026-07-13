using Microi.net.Business;

namespace Microi.Plugin.Demo.WorkOrder
{
    [BusinessTable("mes_work_order_ext", Comment = "MES-生产工单扩展")]
    public class WorkOrderExt : BusinessEntity
    {
        public int? Priority { get; set; }
        public string QcUserId { get; set; }
        public string QcUserName { get; set; }
        public string ExtNote { get; set; }
    }
}
