using System.Threading.Tasks;
using Microi.net.Business.Common;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Microi.Plugin.Demo.WorkOrder
{
    public class WorkOrderController : BusinessControllerBase
    {
        private readonly WorkOrderService _service;

        public WorkOrderController(WorkOrderService service) { _service = service; }

        [HttpPost, HttpGet] public Task<JsonResult> GetList(WorkOrderParam p) => OkJson(_service.GetListAsync(p));
        [HttpPost, HttpGet] public Task<JsonResult> GetModel(WorkOrderParam p) => OkJson(_service.GetModelAsync(p));
        [HttpPost, HttpGet] public Task<JsonResult> GetModelWithRelations(WorkOrderParam p) => OkJson(_service.GetModelWithRelationsAsync(p));
        [HttpPost] public Task<JsonResult> Add(WorkOrderParam p) => OkJson(_service.AddAsync(p));
        [HttpPost] public Task<JsonResult> Upt(WorkOrderParam p) => OkJson(_service.UptAsync(p));
        [HttpPost] public Task<JsonResult> Del(WorkOrderParam p) => OkJson(_service.DelAsync(p));

        [HttpPost]
        public async Task<JsonResult> Save([FromBody] JObject data)
        {
            var (osClient, _) = await GetCurrentContext();
            data["OsClient"] = osClient;
            return OkJson(await _service.SaveWithRelationsAsync(data, osClient));
        }

        [HttpPost] public Task<JsonResult> Execute(WorkOrderParam p) => OkJson(_service.ExecuteTriggerAsync(p));

        private async Task<JsonResult> OkJson<T>(Task<T> t) => Json(await t);
    }
}
