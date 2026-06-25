namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单状态（单据生命周期）。
    /// 流转：已创建 →(Release) 已下达 →(Start) 生产中 →(Complete) 已完工 →(Close) 已关闭；
    ///       已创建/已下达 →(Cancel) 已取消。
    /// </summary>
    public enum WorkOrderStatus
    {
        /// <summary>已创建</summary>
        Created = 0,

        /// <summary>已下达</summary>
        Released = 1,

        /// <summary>生产中</summary>
        InProgress = 2,

        /// <summary>已完工</summary>
        Completed = 3,

        /// <summary>已关闭</summary>
        Closed = 4,

        /// <summary>已取消</summary>
        Cancelled = 9
    }
}
