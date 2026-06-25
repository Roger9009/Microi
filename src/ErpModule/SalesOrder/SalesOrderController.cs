using System.Threading.Tasks;
using Microi.net.Business.Common;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Erp
{
    /// <summary>
    /// 销售订单 API（示例）。
    /// 路由：api/SalesOrder/{action}
    /// </summary>
    public class SalesOrderController : BusinessControllerBase
    {
        private readonly SalesOrderService _service = new SalesOrderService();

        /// <summary>列表</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetList(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.GetListAsync(param));
        }

        /// <summary>单条</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModel(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.GetModelAsync(param));
        }

        /// <summary>单条（含扩展表合并 + 明细集合 Items）</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModelWithRelations(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.GetModelWithRelationsAsync(param));
        }

        /// <summary>新增（自动生成单据号，初始状态草稿）</summary>
        [HttpPost]
        public async Task<JsonResult> Add(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.AddAsync(param));
        }

        /// <summary>修改</summary>
        [HttpPost]
        public async Task<JsonResult> Upt(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.UptAsync(param));
        }

        /// <summary>删除</summary>
        [HttpPost]
        public async Task<JsonResult> Del(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.DelAsync(param));
        }

        /// <summary>
        /// 状态流转：通过 Trigger 驱动（Submit/Audit/Finish/Cancel）。
        /// 入参：{ Id, Trigger, OperateRemark }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Execute(SalesOrderParam param)
        {
            await FillContext(param);
            return Json(await _service.ExecuteTriggerAsync(param));
        }
    }
}
