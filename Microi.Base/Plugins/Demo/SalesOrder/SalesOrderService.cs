using System;
using System.Threading.Tasks;
using Dos.Common;
using Microi.net.Business;
using Microi.net.Business.Common;
using Newtonsoft.Json.Linq;

namespace Microi.Plugin.Demo.SalesOrder
{
    /// <summary>
    /// 销售订单业务服务（Demo 示例）。
    /// 展示实体 CRUD + 单据状态机 + DI 注入的完整用法。
    /// </summary>
    public class SalesOrderService : BusinessStatefulServiceBase<SalesOrderParam, SalesOrderStatus>
    {
        private readonly IBillNoService _billNoService;

        public SalesOrderService(IBillNoService billNoService)
        {
            _billNoService = billNoService;
        }

        protected override string TableKey => "erp_sales_order";
        protected override Type EntityType => typeof(SalesOrder);

        protected override void ConfigureStateMachine(BusinessStateMachine<JObject, SalesOrderStatus> sm)
        {
            sm
                .Permit(SalesOrderStatus.Draft, SalesOrderStatus.Submitted, "Submit",
                    guard: ctx =>
                    {
                        var amount = ctx.Entity.Value<decimal?>("TotalAmount") ?? 0;
                        if (amount <= 0) ctx.Reject("订单金额必须大于 0 才能提交。");
                        return Task.FromResult(!ctx.IsRejected);
                    })
                .Permit(SalesOrderStatus.Submitted, SalesOrderStatus.Audited, "Audit")
                .Permit(SalesOrderStatus.Audited, SalesOrderStatus.Finished, "Finish")
                .Permit(SalesOrderStatus.Submitted, SalesOrderStatus.Cancelled, "Cancel")
                .Permit(SalesOrderStatus.Audited, SalesOrderStatus.Cancelled, "Cancel")
                .OnEnter(SalesOrderStatus.Audited, ctx =>
                {
                    ctx.Entity["AuditorId"] = ctx.CurrentUser?["Id"]?.ToString();
                    ctx.Entity["AuditTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    return Task.CompletedTask;
                })
                .OnEnter(SalesOrderStatus.Cancelled, ctx =>
                {
                    ctx.Entity["Remark"] = "作废原因：" + (ctx.OperateRemark ?? "");
                    return Task.CompletedTask;
                });
        }

        protected override async Task<DosResult> OnBeforeAddAsync(SalesOrderParam param)
        {
            if (string.IsNullOrWhiteSpace(param.CustomerId))
                return new DosResult(0, null, "客户不能为空。");
            param.BillNo = await _billNoService.GenerateAsync("SO", param.OsClient);
            param.Status = (int)SalesOrderStatus.Draft;
            param.OrderDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            return null;
        }
    }
}
