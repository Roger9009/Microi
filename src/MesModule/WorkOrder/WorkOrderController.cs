using System.Threading.Tasks;
using Microi.net.Business.Common;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Microi.net.Mes
{
    /// <summary>
    /// 生产工单 API。
    /// 路由：api/WorkOrder/{action}
    /// 上下文自动填充由全局 BusinessContextFilter 处理，无需手动调用 FillContext。
    /// </summary>
    public class WorkOrderController : BusinessControllerBase
    {
        private readonly WorkOrderService _service;

        /// <summary>
        /// 构造函数注入 WorkOrderService（由 MesModule.ConfigureServices 注册为 Scoped）。
        /// </summary>
        public WorkOrderController(WorkOrderService service)
        {
            _service = service;
        }

        /// <summary>列表</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetList(WorkOrderParam param)
        {
            return Json(await _service.GetListAsync(param));
        }

        /// <summary>单条</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModel(WorkOrderParam param)
        {
            return Json(await _service.GetModelAsync(param));
        }

        /// <summary>单条（含扩展表合并 + 工序明细集合 Items）</summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModelWithRelations(WorkOrderParam param)
        {
            return Json(await _service.GetModelWithRelationsAsync(param));
        }

        /// <summary>新增（自动生成单据号，初始状态已创建）</summary>
        [HttpPost]
        public async Task<JsonResult> Add(WorkOrderParam param)
        {
            return Json(await _service.AddAsync(param));
        }

        /// <summary>修改</summary>
        [HttpPost]
        public async Task<JsonResult> Upt(WorkOrderParam param)
        {
            return Json(await _service.UptAsync(param));
        }

        /// <summary>删除</summary>
        [HttpPost]
        public async Task<JsonResult> Del(WorkOrderParam param)
        {
            return Json(await _service.DelAsync(param));
        }

        /// <summary>
        /// 保存生产工单（主单 + 扩展表 + 明细）。
        /// 入参为完整 JSON：{ Id, BillNo, ProductId, ...扩展字段, Items: [{...}] }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Save([FromBody] JObject data)
        {
            var (osClient, _) = await GetCurrentContext();
            data["OsClient"] = osClient;
            return Json(await _service.SaveWithRelationsAsync(data, osClient));
        }

        /// <summary>
        /// 状态流转：通过 Trigger 驱动（Release/Start/Complete/Close/Cancel）。
        /// 入参：{ Id, Trigger, OperateRemark }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Execute(WorkOrderParam param)
        {
            return Json(await _service.ExecuteTriggerAsync(param));
        }
    }
}
