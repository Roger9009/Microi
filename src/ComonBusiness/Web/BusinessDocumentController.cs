using System;
using System.Threading.Tasks;
using Dos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 通用业务文档 CRUD API。
    /// 对任意已注册的业务文档（主细扩展表）提供统一的增删改查接口，
    /// 减少每个业务模块重复编写 Controller 的模板代码。
    ///
    /// 路由：api/BusinessDoc/{action}
    /// 入参以 JSON 或 form-data 传递，需包含 MasterTable 指明文档类型。
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/BusinessDoc/[action]")]
    public class BusinessDocumentController : Controller
    {
        /// <summary>
        /// 获取列表（分页查询，条件由 _Where / _PageIndex / _PageSize 控制）。
        /// POST api/BusinessDoc/GetList
        /// Body: { MasterTable, _Where, _PageIndex, _PageSize, ... }
        /// </summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetList([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            var result = await MicroiEngine.FormEngine.GetTableDataAsync(masterTable, data);
            return Json(result);
        }

        /// <summary>
        /// 获取单条。
        /// POST api/BusinessDoc/GetModel
        /// Body: { MasterTable, Id, ... }
        /// </summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModel([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            var result = await MicroiEngine.FormEngine.GetFormDataAsync(masterTable, data);
            return Json(result);
        }

        /// <summary>
        /// 获取单条并自动合并扩展表列 + 加载明细集合。
        /// 需在代码中用 [BusinessDetailTable] / [BusinessExtensionTable] 声明关系。
        /// POST api/BusinessDoc/GetModelWithRelations
        /// Body: { MasterTable, Id, ... }
        /// </summary>
        [HttpPost, HttpGet]
        public async Task<JsonResult> GetModelWithRelations([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            var result = await MicroiEngine.FormEngine.GetFormDataAsync(masterTable, data);
            if (result == null || result.Code != 1 || result.Data == null)
                return Json(result);

            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            if (masterType == null)
                return Json(result);

            var master = result.Data as JObject ?? JObject.FromObject(result.Data);
            await BusinessDocumentReader.EnrichAsync(master, masterType, osClient);
            result.Data = master;
            return Json(result);
        }

        /// <summary>
        /// 新增一条业务文档。
        /// POST api/BusinessDoc/Add
        /// Body: { MasterTable, ...业务字段 }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Add([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            var result = await MicroiEngine.FormEngine.AddFormDataAsync(masterTable, data);
            return Json(result);
        }

        /// <summary>
        /// 修改一条业务文档。
        /// POST api/BusinessDoc/Upt
        /// Body: { MasterTable, Id, ...业务字段 }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Upt([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            var result = await MicroiEngine.FormEngine.UptFormDataAsync(masterTable, data);
            return Json(result);
        }

        /// <summary>
        /// 删除一条业务文档（含级联删除扩展表与明细表）。
        /// POST api/BusinessDoc/Del
        /// Body: { MasterTable, Id }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Del([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            var id = data["Id"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            if (string.IsNullOrWhiteSpace(id))
                return Json(new DosResult(0, null, "Id 不能为空。"));
            data["OsClient"] = osClient;

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            // 先删除主单
            var result = await MicroiEngine.FormEngine.DelFormDataAsync(masterTable, data);
            if (result == null || result.Code != 1)
                return Json(result ?? new DosResult(0, null, "删除主单失败。"));

            // 级联清理关联表
            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            if (masterType != null)
            {
                await BusinessDocumentWriter.DeleteRelationsAsync(id, masterType, osClient,
                    masterTable: masterTable);
            }

            return Json(result);
        }

        /// <summary>
        /// 保存完整业务文档（主单 + 扩展表 + 明细集合 Items[]）。
        /// 入参为完整 JSON：{ MasterTable, Id?, BillNo, ..., Items: [{...}] }
        /// POST api/BusinessDoc/Save
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Save([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            data["OsClient"] = osClient;

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            if (masterType == null)
                return Json(new DosResult(0, null, $"未找到业务表[{masterTable}]对应的实体类型。"));

            var result = await BusinessDocumentWriter.SaveAsync(data, masterType, masterTable, osClient);
            return Json(result);
        }

        /// <summary>
        /// 执行状态流转（驱动单据状态机）。
        /// 由入参 Trigger 控制动作类型（如 Submit/Audit/Finish/Cancel），
        /// 需要对应业务模块的 Service 在实体类型上注册了状态机。
        /// POST api/BusinessDoc/Execute
        /// Body: { MasterTable, Id, Trigger, OperateRemark }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Execute([FromBody] JObject data)
        {
            var (osClient, currentUser) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            var id = data["Id"]?.ToString();
            var trigger = data["Trigger"]?.ToString();
            var remark = data["OperateRemark"]?.ToString();

            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            if (string.IsNullOrWhiteSpace(id))
                return Json(new DosResult(0, null, "Id 不能为空。"));
            if (string.IsNullOrWhiteSpace(trigger))
                return Json(new DosResult(0, null, "Trigger 不能为空（如 Submit/Audit/Cancel）。"));

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            // 获取主表实体类型
            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            if (masterType == null)
                return Json(new DosResult(0, null, $"未找到业务表[{masterTable}]对应的实体类型。"));

            // 先加载单据数据
            data["OsClient"] = osClient;
            var modelResult = await MicroiEngine.FormEngine.GetFormDataAsync(masterTable, data);
            if (modelResult == null || modelResult.Code != 1 || modelResult.Data == null)
                return Json(new DosResult(0, null, "单据不存在或已被删除。"));

            // 写入 Trigger / OperateRemark，让 FormEngine 的下层能够识别
            var entity = modelResult.Data as JObject ?? JObject.FromObject(modelResult.Data);
            entity["Trigger"] = trigger;
            if (!string.IsNullOrWhiteSpace(remark))
                entity["OperateRemark"] = remark;

            // 通过 FormEngine 更新（会触发 V8 事件或状态机）
            var uptResult = await MicroiEngine.FormEngine.UptFormDataAsync(masterTable, entity);
            if (uptResult == null || uptResult.Code != 1)
                return Json(uptResult ?? new DosResult(0, null, "状态流转失败。"));

            return Json(new DosResult(1, new
            {
                Id = id,
                Trigger = trigger,
                MasterTable = masterTable
            }, $"状态流转 [{trigger}] 执行完成。"));
        }

        /// <summary>
        /// 批量删除业务文档。
        /// POST api/BusinessDoc/DelBatch
        /// Body: { MasterTable, Ids: ["id1","id2","id3"] }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> DelBatch([FromBody] JObject data)
        {
            var (osClient, _) = await GetContext();
            var masterTable = data["MasterTable"]?.ToString();
            var ids = data["Ids"] as JArray;

            if (string.IsNullOrWhiteSpace(masterTable))
                return Json(new DosResult(0, null, "MasterTable 不能为空。"));
            if (ids == null || ids.Count == 0)
                return Json(new DosResult(0, null, "Ids 不能为空。"));
            if (ids.Count > 500)
                return Json(new DosResult(0, null, "单次批量删除最多 500 条。"));

            if (!IsValidBusinessTable(masterTable))
                return Json(new DosResult(0, null, $"业务表 [{masterTable}] 未在业务底座注册。"));

            var masterType = BusinessRelationResolver.GetTypeByTable(masterTable);
            var successCount = 0;
            var failCount = 0;
            var firstError = "";

            foreach (var idToken in ids)
            {
                var id = idToken?.ToString();
                if (string.IsNullOrWhiteSpace(id)) continue;

                try
                {
                    var delResult = await MicroiEngine.FormEngine.DelFormDataAsync(masterTable,
                        new { Id = id, OsClient = osClient });

                    if (delResult != null && delResult.Code == 1)
                    {
                        successCount++;
                        // 级联清理关联表
                        if (masterType != null)
                        {
                            await BusinessDocumentWriter.DeleteRelationsAsync(id, masterType, osClient,
                                masterTable: masterTable);
                        }
                    }
                    else
                    {
                        failCount++;
                        if (string.IsNullOrWhiteSpace(firstError))
                            firstError = delResult?.Msg ?? $"删除 [{id}] 失败";
                    }
                }
                catch (Exception ex)
                {
                    failCount++;
                    if (string.IsNullOrWhiteSpace(firstError))
                        firstError = $"删除 [{id}] 异常: {ex.Message}";
                }
            }

            var msg = $"批量删除完成：成功 {successCount} 条";
            if (failCount > 0) msg += $"，失败 {failCount} 条";
            if (!string.IsNullOrWhiteSpace(firstError)) msg += $"。首次错误：{firstError}";

            return Json(new DosResult(failCount == 0 ? 1 : 0,
                new { SuccessCount = successCount, FailCount = failCount },
                msg));
        }

        // ── 私有 ──────────────────────────────────────────────────────────

        private static async Task<(string OsClient, JObject CurrentUser)> GetContext()
        {
            var current = await DiyToken.GetCurrentToken();
            return (current.OsClient, current.CurrentUser);
        }

        /// <summary>
        /// 校验 MasterTable 是否属于已注册的业务文档（防访问系统内部表）。
        /// 写操作（Add/Upt/Del/Save/Execute/DelBatch）必须通过此校验。
        /// </summary>
        private static bool IsValidBusinessTable(string masterTable)
        {
            return !string.IsNullOrWhiteSpace(masterTable)
                && BusinessRelationResolver.GetTypeByTable(masterTable) != null;
        }
    }
}
