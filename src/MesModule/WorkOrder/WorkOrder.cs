using Microi.net.Business;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单实体（示例）。
    /// 对应数据表（表单引擎 Key）：mes_work_order。
    /// 启动时由 Schema 初始化器自动建表/补列。
    /// </summary>
    [BusinessTable("mes_work_order", Comment = "MES-生产工单")]
    public class WorkOrder : BusinessStatefulEntity<WorkOrderStatus>
    {
        /// <summary>关联销售订单 Id（与 ERP 联动，可选）</summary>
        public string SalesOrderId { get; set; }

        /// <summary>产品 Id</summary>
        public string ProductId { get; set; }

        /// <summary>产品名称</summary>
        public string ProductName { get; set; }

        /// <summary>计划数量</summary>
        public decimal? PlanQty { get; set; }

        /// <summary>已完工数量</summary>
        public decimal? CompletedQty { get; set; }

        /// <summary>计划开工时间</summary>
        public System.DateTime? PlanStartTime { get; set; }

        /// <summary>实际开工时间</summary>
        public System.DateTime? ActualStartTime { get; set; }

        /// <summary>实际完工时间</summary>
        public System.DateTime? ActualEndTime { get; set; }
    }
}
