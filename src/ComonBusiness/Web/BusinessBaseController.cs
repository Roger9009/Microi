using System;
using System.Linq;
using System.Threading.Tasks;
using Dos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务底座总控台 API。
    /// 提供统一的仪表盘、状态摘要和配置管理，聚合各子系统的运行信息。
    ///
    /// 路由：api/BusinessBase/{action}
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/BusinessBase/[action]")]
    public class BusinessBaseController : Controller
    {
        private readonly BusinessModuleManager _moduleManager;
        private readonly BusinessPluginManager _pluginManager;

        /// <summary>构造函数注入（由 DI 自动提供 Manager 单例）。</summary>
        public BusinessBaseController(
            BusinessModuleManager moduleManager,
            BusinessPluginManager pluginManager)
        {
            _moduleManager = moduleManager;
            _pluginManager = pluginManager;
        }

        /// <summary>
        /// 总控台仪表盘数据聚合。
        /// GET/POST /api/BusinessBase/GetDashboard
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult GetDashboard()
        {
            try
            {
                var modules = _moduleManager.Modules;
                var plugins = _pluginManager.Plugins;

                var moduleStats = new
                {
                    Total = modules.Count,
                    Started = modules.Count(m => m.Stage == BusinessLifecycleStage.Started),
                    Faulted = modules.Count(m => m.Stage == BusinessLifecycleStage.Faulted),
                    Starting = modules.Count(m => m.Stage == BusinessLifecycleStage.Starting
                        || m.Stage == BusinessLifecycleStage.Registered)
                };

                var pluginStats = new
                {
                    Total = plugins.Count,
                    Started = plugins.Count(p => p.Stage == PluginLifecycleStage.Started),
                    Faulted = plugins.Count(p => p.Stage == PluginLifecycleStage.Faulted),
                    EngineStarted = _pluginManager.EngineStarted
                };

                var faultedModules = modules
                    .Where(m => m.Stage == BusinessLifecycleStage.Faulted)
                    .Select(m => new { m.Key, Name = m.Module.Name, m.Error })
                    .ToList();

                var faultedPlugins = plugins
                    .Where(p => p.Stage == PluginLifecycleStage.Faulted)
                    .Select(p => new { p.Key, Name = p.Plugin.Name, p.Error })
                    .ToList();

                return Json(new DosResult(1, new
                {
                    ModuleStats = moduleStats,
                    PluginStats = pluginStats,
                    FaultedModules = faultedModules,
                    FaultedPlugins = faultedPlugins,
                    ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取仪表盘数据失败，请稍后重试。"));
            }
        }

        /// <summary>
        /// 获取业务底座配置信息。
        /// 返回底座运行时可配置的参数及其当前值。
        /// GET/POST /api/BusinessBase/GetConfig
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult GetConfig()
        {
            try
            {
                return Json(new DosResult(1, new
                {
                    ModuleAutoMigrate = true,
                    PluginAutoScan = true,
                    ModuleCount = _moduleManager.Modules.Count,
                    PluginCount = _pluginManager.Plugins.Count,
                    AvailableEndpoints = new[]
                    {
                        "api/BusinessDoc/{GetList,GetModel,Add,Upt,Del,Save,Execute,DelBatch}",
                        "api/BusinessSchema/{GetDocuments,GetDocumentSchema,AddField,BindRelation}",
                        "api/BusinessMonitor/{Modules,Module,Started,Faulted,Health}",
                        "api/BusinessAuth/{Login,Verify,SetPassword,Logout}",
                        "api/BusinessBase/{GetDashboard,GetConfig}"
                    }
                }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取配置失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 健康检查（轻量级，仅验证核心组件就绪状态）。
        /// GET/POST /api/BusinessBase/Health
        /// </summary>
        [AllowAnonymous]
        [HttpGet, HttpPost]
        public JsonResult Health()
        {
            var moduleOk = _moduleManager.Modules.Count > 0;
            var anyFaulted = _moduleManager.Modules.Any(m =>
                m.Stage == BusinessLifecycleStage.Faulted);
            var engineOk = _pluginManager.Plugins.Count == 0 || _pluginManager.EngineStarted;

            var healthy = moduleOk && !anyFaulted && engineOk;

            return Json(new DosResult(healthy ? 1 : 0,
                new { ModulesHealthy = moduleOk && !anyFaulted, PluginsHealthy = engineOk },
                healthy ? "业务底座运行正常" : "业务底座存在异常"));
        }
    }
}
