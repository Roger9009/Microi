using System.Threading.Tasks;
using Microi.net.Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单 API（示例）。
    /// 路由：api/WorkOrder/{action}
    /// </summary>
    public class WorkOrderController : BusinessControllerBase
    {
        private readonly WorkOrderService _service = new WorkOrderService();

        /// <summary>列表</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetList(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.GetListAsync(param));
        }

        /// <summary>单条</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModel(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.GetModelAsync(param));
        }

        /// <summary>新增（自动生成单据号，初始状态已创建）</summary>
        [HttpPost]
        public async Task<JsonResult> Add(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.AddAsync(param));
        }

        /// <summary>修改</summary>
        [HttpPost]
        public async Task<JsonResult> Upt(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.UptAsync(param));
        }

        /// <summary>删除</summary>
        [HttpPost]
        public async Task<JsonResult> Del(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.DelAsync(param));
        }

        /// <summary>
        /// 状态流转：通过 Trigger 驱动（Release/Start/Complete/Close/Cancel）。
        /// 入参：{ Id, Trigger, OperateRemark }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Execute(WorkOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.ExecuteTriggerAsync(param));
        }
    }
}
