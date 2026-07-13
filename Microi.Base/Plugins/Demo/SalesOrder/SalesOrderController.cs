using System.Threading.Tasks;
using Microi.net.Business.Common;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Microi.Plugin.Demo.SalesOrder
{
    /// <summary>
    /// 销售订单 API（Demo 示例）。
    /// 路由：api/SalesOrder/{action}
    /// </summary>
    public class SalesOrderController : BusinessControllerBase
    {
        private readonly SalesOrderService _service;

        public SalesOrderController(SalesOrderService service)
        {
            _service = service;
        }

        [HttpPost, HttpGet] public Task<JsonResult> GetList(SalesOrderParam p) => OkJson(_service.GetListAsync(p));
        [HttpPost, HttpGet] public Task<JsonResult> GetModel(SalesOrderParam p) => OkJson(_service.GetModelAsync(p));
        [HttpPost, HttpGet] public Task<JsonResult> GetModelWithRelations(SalesOrderParam p) => OkJson(_service.GetModelWithRelationsAsync(p));
        [HttpPost] public Task<JsonResult> Add(SalesOrderParam p) => OkJson(_service.AddAsync(p));
        [HttpPost] public Task<JsonResult> Upt(SalesOrderParam p) => OkJson(_service.UptAsync(p));
        [HttpPost] public Task<JsonResult> Del(SalesOrderParam p) => OkJson(_service.DelAsync(p));

        [HttpPost]
        public async Task<JsonResult> Save([FromBody] JObject data)
        {
            var (osClient, _) = await GetCurrentContext();
            data["OsClient"] = osClient;
            return OkJson(await _service.SaveWithRelationsAsync(data, osClient));
        }

        [HttpPost] public Task<JsonResult> Execute(SalesOrderParam p) => OkJson(_service.ExecuteTriggerAsync(p));

        private async Task<JsonResult> OkJson<T>(Task<T> t) => Json(await t);
    }
}
