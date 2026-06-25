namespace Microi.net.Business
{
    /// <summary>
    /// 业务参数基类，继承平台 BaseParam，复用 _Where / 分页 / _CurrentUser / OsClient 等能力。
    /// 所有业务模块的入参都应继承此类。
    /// </summary>
    public class BusinessParam : BaseParam
    {
        /// <summary>
        /// 状态机触发动作名（如：Submit、Audit、Finish、Cancel）。
        /// 用于驱动单据状态流转。
        /// </summary>
        public string Trigger { get; set; }

        /// <summary>
        /// 状态流转/操作时的附言（如审核意见、作废原因）。
        /// </summary>
        public string OperateRemark { get; set; }

        /// <summary>
        /// 不参与更新的字段列表（用于 FormEngine.UptFormDataAsync）。
        /// </summary>
        public System.Collections.Generic.List<string> _NotSaveField { get; set; }
    }
}
