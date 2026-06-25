namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单状态（单据生命周期）。
    /// 流转：草稿 →(Submit) 已提交 →(Audit) 已审核 →(Finish) 已完成；
    ///       已提交/已审核 →(Cancel) 已作废。
    /// </summary>
    public enum SalesOrderStatus
    {
        /// <summary>草稿</summary>
        Draft = 0,

        /// <summary>已提交（待审核）</summary>
        Submitted = 1,

        /// <summary>已审核</summary>
        Audited = 2,

        /// <summary>已完成</summary>
        Finished = 3,

        /// <summary>已作废</summary>
        Cancelled = 9
    }
}
