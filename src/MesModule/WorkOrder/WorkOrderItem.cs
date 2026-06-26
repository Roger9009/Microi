using Microi.net.Business;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单工序明细（一对多）。
    /// 通过 WorkOrderId 关联主表。对应数据表：mes_work_order_item。
    /// </summary>
    [BusinessTable("mes_work_order_item", Comment = "MES-生产工单工序明细")]
    public class WorkOrderItem : BusinessEntity
    {
        /// <summary>主表工单 Id（外键）</summary>
        public string WorkOrderId { get; set; }

        /// <summary>工序编号</summary>
        public string ProcessNo { get; set; }

        /// <summary>工序名称</summary>
        public string ProcessName { get; set; }

        /// <summary>工作中心 Id</summary>
        public string WorkCenterId { get; set; }

        /// <summary>工作中心名称</summary>
        public string WorkCenterName { get; set; }

        /// <summary>计划工时（分钟）</summary>
        public decimal? PlanHours { get; set; }

        /// <summary>实际工时（分钟）</summary>
        public decimal? ActualHours { get; set; }

        /// <summary>工序顺序</summary>
        public int? SortNo { get; set; }
    }
}
