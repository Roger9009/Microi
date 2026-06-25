using System;
using System.Threading.Tasks;
using Dos.Common;
using Microi.net.Business;
using Microi.net.Business.Common;
using Newtonsoft.Json.Linq;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单业务服务（示例）。
    /// 展示如何同时使用：实体 CRUD 生命周期钩子 + 单据状态机生命周期。
    /// </summary>
    public class SalesOrderService : BusinessStatefulServiceBase<SalesOrderParam, SalesOrderStatus>
    {
        protected override string TableKey => "erp_sales_order";

        /// <summary>主表实体类型，启用扩展表合并与明细加载（GetModelWithRelationsAsync）。</summary>
        protected override Type EntityType => typeof(SalesOrder);

        /// <summary>
        /// 声明状态流转与钩子。
        /// </summary>
        protected override void ConfigureStateMachine(BusinessStateMachine<JObject, SalesOrderStatus> sm)
        {
            sm
                // 草稿 → 提交
                .Permit(SalesOrderStatus.Draft, SalesOrderStatus.Submitted, "Submit",
                    guard: ctx =>
                    {
                        var amount = ctx.Entity.Value<decimal?>("TotalAmount") ?? 0;
                        if (amount <= 0) ctx.Reject("订单金额必须大于 0 才能提交。");
                        return Task.FromResult(!ctx.IsRejected);
                    })
                // 提交 → 审核
                .Permit(SalesOrderStatus.Submitted, SalesOrderStatus.Audited, "Audit")
                // 审核 → 完成
                .Permit(SalesOrderStatus.Audited, SalesOrderStatus.Finished, "Finish")
                // 提交/审核 → 作废
                .Permit(SalesOrderStatus.Submitted, SalesOrderStatus.Cancelled, "Cancel")
                .Permit(SalesOrderStatus.Audited, SalesOrderStatus.Cancelled, "Cancel")
                // 进入"已审核"时记录审核人与时间
                .OnEnter(SalesOrderStatus.Audited, ctx =>
                {
                    ctx.Entity["AuditorId"] = ctx.CurrentUser?["Id"]?.ToString();
                    ctx.Entity["AuditTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    return Task.CompletedTask;
                })
                // 进入"已作废"时记录作废原因
                .OnEnter(SalesOrderStatus.Cancelled, ctx =>
                {
                    ctx.Entity["Remark"] = "作废原因：" + (ctx.OperateRemark ?? "");
                    return Task.CompletedTask;
                });
        }

        /// <summary>
        /// 新增前：生成单据编号、设置初始状态为草稿。
        /// </summary>
        protected override async Task<DosResult> OnBeforeAddAsync(SalesOrderParam param)
        {
            if (string.IsNullOrWhiteSpace(param.CustomerId))
                return new DosResult(0, null, "客户不能为空。");

            var billNoService = MicroiEngine.GetService<IBillNoService>();
            param.BillNo = await billNoService.GenerateAsync("SO", param.OsClient);
            param.Status = (int)SalesOrderStatus.Draft;
            param.OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return null;
        }
    }
}
