using System;
using System.Threading.Tasks;
using Dos.Common;
using Microi.net.Business;
using Microi.net.Business.Common;
using Newtonsoft.Json.Linq;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单业务服务（示例）。
    /// </summary>
    public class WorkOrderService : BusinessStatefulServiceBase<WorkOrderParam, WorkOrderStatus>
    {
        protected override string TableKey => "mes_work_order";

        protected override void ConfigureStateMachine(BusinessStateMachine<JObject, WorkOrderStatus> sm)
        {
            sm
                // 创建 → 下达
                .Permit(WorkOrderStatus.Created, WorkOrderStatus.Released, "Release",
                    guard: ctx =>
                    {
                        var qty = ctx.Entity.Value<decimal?>("PlanQty") ?? 0;
                        if (qty <= 0) ctx.Reject("计划数量必须大于 0 才能下达。");
                        return Task.FromResult(!ctx.IsRejected);
                    })
                // 下达 → 生产中
                .Permit(WorkOrderStatus.Released, WorkOrderStatus.InProgress, "Start")
                // 生产中 → 完工
                .Permit(WorkOrderStatus.InProgress, WorkOrderStatus.Completed, "Complete")
                // 完工 → 关闭
                .Permit(WorkOrderStatus.Completed, WorkOrderStatus.Closed, "Close")
                // 创建/下达 → 取消
                .Permit(WorkOrderStatus.Created, WorkOrderStatus.Cancelled, "Cancel")
                .Permit(WorkOrderStatus.Released, WorkOrderStatus.Cancelled, "Cancel")
                // 进入生产中：记录实际开工时间
                .OnEnter(WorkOrderStatus.InProgress, ctx =>
                {
                    ctx.Entity["ActualStartTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    return Task.CompletedTask;
                })
                // 进入完工：记录实际完工时间
                .OnEnter(WorkOrderStatus.Completed, ctx =>
                {
                    ctx.Entity["ActualEndTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    return Task.CompletedTask;
                });
        }

        protected override async Task<DosResult> OnBeforeAddAsync(WorkOrderParam param)
        {
            if (string.IsNullOrWhiteSpace(param.ProductId))
                return new DosResult(0, null, "产品不能为空。");

            var billNoService = MicroiEngine.GetService<IBillNoService>();
            param.BillNo = await billNoService.GenerateAsync("WO", param.OsClient);
            param.Status = (int)WorkOrderStatus.Created;
            return null;
        }
    }
}
