using Microi.net.Business;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单扩展表（一对一）。
    /// 与主表共用相同 Id。自定义字段可在前端动态添加到此表，读取主单时自动合并。
    /// 对应数据表：mes_work_order_ext。
    /// </summary>
    [BusinessTable("mes_work_order_ext", Comment = "MES-生产工单扩展")]
    public class WorkOrderExt : BusinessEntity
    {
        /// <summary>优先级（1-低 2-中 3-高）</summary>
        public int? Priority { get; set; }

        /// <summary>质检员 Id</summary>
        public string QcUserId { get; set; }

        /// <summary>质检员姓名</summary>
        public string QcUserName { get; set; }

        /// <summary>附加说明（工艺要求等）</summary>
        public string ExtNote { get; set; }
    }
}
