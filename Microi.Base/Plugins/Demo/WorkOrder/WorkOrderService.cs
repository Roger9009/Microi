using System;
using System.Threading.Tasks;
using Dos.Common;
using Microi.net.Business;
using Microi.net.Business.Common;
using Newtonsoft.Json.Linq;

namespace Microi.Plugin.Demo.WorkOrder
{
    public class WorkOrderService : BusinessStatefulServiceBase<WorkOrderParam, WorkOrderStatus>
    {
        private readonly IBillNoService _billNoService;

        public WorkOrderService(IBillNoService billNoService)
        {
            _billNoService = billNoService;
        }

        protected override string TableKey => "mes_work_order";
        protected override Type EntityType => typeof(WorkOrder);

        protected override void ConfigureStateMachine(BusinessStateMachine<JObject, WorkOrderStatus> sm)
        {
            sm
                .Permit(WorkOrderStatus.Created, WorkOrderStatus.Released, "Release",
                    guard: ctx =>
                    {
                        var qty = ctx.Entity.Value<decimal?>("PlanQty") ?? 0;
                        if (qty <= 0) ctx.Reject("计划数量必须大于 0 才能下达。");
                        return Task.FromResult(!ctx.IsRejected);
                    })
                .Permit(WorkOrderStatus.Released, WorkOrderStatus.InProgress, "Start")
                .Permit(WorkOrderStatus.InProgress, WorkOrderStatus.Completed, "Complete")
                .Permit(WorkOrderStatus.Completed, WorkOrderStatus.Closed, "Close")
                .Permit(WorkOrderStatus.Created, WorkOrderStatus.Cancelled, "Cancel")
                .Permit(WorkOrderStatus.Released, WorkOrderStatus.Cancelled, "Cancel")
                .OnEnter(WorkOrderStatus.InProgress, ctx =>
                {
                    ctx.Entity["ActualStartTime"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    return Task.CompletedTask;
                })
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
            param.BillNo = await _billNoService.GenerateAsync("WO", param.OsClient);
            param.Status = (int)WorkOrderStatus.Created;
            return null;
        }
    }
}
