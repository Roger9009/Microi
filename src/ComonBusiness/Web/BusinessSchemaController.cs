using System.Threading.Tasks;
using Microi.net.Business;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务表结构 API：查看主/细/扩展表结构、动态加字段。
    /// 路由：api/BusinessSchema/{action}
    /// </summary>
    public class BusinessSchemaController : BusinessControllerBase
    {
        private readonly BusinessSchemaService _service = new BusinessSchemaService();
        private readonly BusinessFieldConfigService _fieldConfigService = new BusinessFieldConfigService();

        /// <summary>列出所有业务文档（主表）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetDocuments(BusinessSchemaQueryParam param)
        {
            await FillContext(param);
            return Json(_service.ListDocuments(param.OsClient));
        }

        /// <summary>获取一个文档的完整结构（主表 + 明细 + 扩展）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetDocumentSchema(BusinessSchemaQueryParam param)
        {
            await FillContext(param);
            return Json(_service.GetDocumentSchema(param.MasterTable, param.OsClient));
        }

        /// <summary>获取单表列结构。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetTableColumns(BusinessSchemaQueryParam param)
        {
            await FillContext(param);
            return Json(_service.GetTableColumns(param.TableName, param.OsClient));
        }

        /// <summary>动态加字段（合并到主表/明细表/扩展表）。</summary>
        [HttpPost]
        public async Task<JsonResult> AddField(BusinessAddFieldParam param)
        {
            await FillContext(param);
            return Json(_service.AddField(param));
        }

        /// <summary>获取某表已解析的字段定义（物理列 + 字段配置）。</summary>
        [HttpGet, HttpPost]
        public async Task<JsonResult> GetFieldConfigs(BusinessSchemaQueryParam param)
        {
            await FillContext(param);
            return Json(await _fieldConfigService.GetResolvedFields(param.TableName, param.OsClient));
        }

        /// <summary>批量保存字段配置。</summary>
        [HttpPost]
        public async Task<JsonResult> SaveFieldConfigs(BusinessFieldConfigSaveParam param)
        {
            await FillContext(param);
            return Json(await _fieldConfigService.SaveConfigs(param));
        }

        /// <summary>删除某字段的配置。</summary>
        [HttpPost]
        public async Task<JsonResult> DeleteFieldConfig(BusinessSchemaQueryParam param)
        {
            await FillContext(param);
            return Json(await _fieldConfigService.DeleteConfig(param.TableName, param.FieldName, param.OsClient));
        }
    }
}
