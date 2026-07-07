using System.Threading.Tasks;
using Dos.Common;
using Microi.net.Business;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务表结构 API：查看主/细/扩展表结构、动态加字段。
    /// 路由：api/BusinessSchema/{action}
    /// Schema 服务通过构造函数注入（由 CommonBusinessModule 注册为 Scoped）。
    /// </summary>
    public class BusinessSchemaController : BusinessControllerBase
    {
        private readonly BusinessSchemaService _service;
        private readonly BusinessFieldConfigService _fieldConfigService;
        private readonly BusinessDocRelationService _relationService;

        /// <summary>
        /// 构造函数注入 Schema 服务。
        /// </summary>
        public BusinessSchemaController(
            BusinessSchemaService service,
            BusinessFieldConfigService fieldConfigService,
            BusinessDocRelationService relationService)
        {
            _service = service;
            _fieldConfigService = fieldConfigService;
            _relationService = relationService;
        }

        /// <summary>列出所有业务文档（主表）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetDocuments(BusinessSchemaQueryParam param)
        {
            return Json(_service.ListDocuments(param.OsClient));
        }

        /// <summary>获取一个文档的完整结构（主表 + 明细 + 扩展）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetDocumentSchema(BusinessSchemaQueryParam param)
        {
            return Json(_service.GetDocumentSchema(param.MasterTable, param.OsClient));
        }

        /// <summary>获取单表列结构。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetTableColumns(BusinessSchemaQueryParam param)
        {
            return Json(_service.GetTableColumns(param.TableName, param.OsClient));
        }

        /// <summary>动态加字段（合并到主表/明细表/扩展表）。</summary>
        [HttpPost]
        public async Task<JsonResult> AddField(BusinessAddFieldParam param)
        {
            return Json(_service.AddField(param));
        }

        /// <summary>获取某表已解析的字段定义（物理列 + 字段配置）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetFieldConfigs(BusinessSchemaQueryParam param)
        {
            return Json(await _fieldConfigService.GetResolvedFields(param.TableName, param.OsClient));
        }

        /// <summary>批量保存字段配置。</summary>
        [HttpPost]
        public async Task<JsonResult> SaveFieldConfigs(BusinessFieldConfigSaveParam param)
        {
            return Json(await _fieldConfigService.SaveConfigs(param));
        }

        /// <summary>删除某字段的配置。</summary>
        [HttpPost]
        public async Task<JsonResult> DeleteFieldConfig(BusinessSchemaQueryParam param)
        {
            return Json(await _fieldConfigService.DeleteConfig(param.TableName, param.FieldName, param.OsClient));
        }

        /// <summary>
        /// 导出某表的字段配置快照（JSON 格式）。
        /// 用于跨环境迁移或版本备份。
        /// </summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> ExportFieldConfigs(BusinessSchemaQueryParam param)
        {
            return Json(await _fieldConfigService.ExportConfigs(param.TableName, param.OsClient));
        }

        /// <summary>
        /// 导入字段配置（按 TableName+FieldName upsert）。
        /// 支持跨环境同步：先从源环境 ExportFieldConfigs，再在目标环境调用此接口。
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> ImportFieldConfigs(BusinessFieldConfigImportParam param)
        {
            return Json(await _fieldConfigService.ImportConfigs(param.Configs, param.OsClient));
        }

        // ── 动态关系绑定 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 绑定扩展表或明细表到主文档（纯前端新建关系，无需改 C# 代码）。
        /// RelationType = Extension：1:1 扩展；Detail：1:N 明细（需传 ForeignKey）。
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> BindRelation(BusinessBindRelationParam param)
        {
            if (string.Equals(param.RelationType, "Extension", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(await _relationService.BindExtensionAsync(
                    param.MasterTable, param.RelationTable, param.Label, param.OsClient));
            }
            else if (string.Equals(param.RelationType, "Detail", System.StringComparison.OrdinalIgnoreCase))
            {
                return Json(await _relationService.BindDetailAsync(
                    param.MasterTable, param.RelationTable, param.ForeignKey,
                    param.PropertyName, param.Label, param.OsClient));
            }
            return Json(new DosResult(0, null, "RelationType 必须为 Extension 或 Detail。"));
        }

        /// <summary>解除动态关系绑定（按 business_doc_relation.Id 删除）。</summary>
        [HttpPost]
        public async Task<JsonResult> UnbindRelation(BusinessUnbindRelationParam param)
        {
            return Json(await _relationService.UnbindAsync(param.RelationId, param.MasterTable, param.OsClient));
        }
    }
}
