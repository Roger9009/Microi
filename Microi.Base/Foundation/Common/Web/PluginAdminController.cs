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
    /// 插件管理控制器。
    /// 提供插件列表、启停、卸载、日志查询 API。
    /// 路由：api/BusinessBase/Plugin/{action}
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/BusinessBase/Plugin/[action]")]
    public class PluginAdminController : Controller
    {
        private readonly BusinessPluginManager _pluginManager;

        public PluginAdminController(BusinessPluginManager pluginManager)
        {
            _pluginManager = pluginManager;
        }

        /// <summary>
        /// 获取全部插件状态与元数据。
        /// GET/POST /api/BusinessBase/Plugin/List
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult List()
        {
            try
            {
                var plugins = _pluginManager.Plugins.Select(d => new
                {
                    Key = d.Key,
                    Name = d.Plugin.Name,
                    Version = d.Plugin.Version,
                    Order = d.Plugin.Order,
                    Stage = d.Stage.ToString(),
                    IsRunning = d.IsRunning,
                    IsStopped = d.IsStopped,
                    IsFaulted = d.IsFaulted,
                    Error = d.Error ?? "",
                    DllPath = d.DllPath,
                    StageChangedTime = d.StageChangedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    DependsOn = d.Plugin.DependsOn ?? Array.Empty<string>()
                }).ToList();

                return Json(new DosResult(1, new
                {
                    Plugins = plugins,
                    Total = plugins.Count,
                    EngineStarted = _pluginManager.EngineStarted
                }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取插件列表失败：" + ex.Message));
            }
        }

        /// <summary>
        /// 停止指定插件。
        /// POST /api/BusinessBase/Plugin/Stop
        /// Body: { Key: "plugin-key" }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Stop([FromBody] System.Dynamic.ExpandoObject body)
        {
            try
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)body;
                var key = dict?.ContainsKey("Key") == true ? dict["Key"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(key))
                    return Json(new DosResult(0, null, "Key 不能为空。"));

                var ok = await _pluginManager.StopPluginAsync(key);
                return Json(new DosResult(ok ? 1 : 0, null,
                    ok ? $"插件 [{key}] 已停止。" : $"插件 [{key}] 停止失败（可能未在运行中）。"));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "停止插件失败：" + ex.Message));
            }
        }

        /// <summary>
        /// 重新启动指定插件（从 Stopped 状态）。
        /// POST /api/BusinessBase/Plugin/Start
        /// Body: { Key: "plugin-key" }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Start([FromBody] System.Dynamic.ExpandoObject body)
        {
            try
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)body;
                var key = dict?.ContainsKey("Key") == true ? dict["Key"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(key))
                    return Json(new DosResult(0, null, "Key 不能为空。"));

                var ok = await _pluginManager.StartPluginAsync(key);
                return Json(new DosResult(ok ? 1 : 0, null,
                    ok ? $"插件 [{key}] 已重新启动。" : $"插件 [{key}] 启动失败（必须处于 Stopped 状态）。"));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "启动插件失败：" + ex.Message));
            }
        }

        /// <summary>
        /// 卸载指定插件（从 Stopped 状态，卸载后可替换 DLL）。
        /// POST /api/BusinessBase/Plugin/Unload
        /// Body: { Key: "plugin-key" }
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Unload([FromBody] System.Dynamic.ExpandoObject body)
        {
            try
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)body;
                var key = dict?.ContainsKey("Key") == true ? dict["Key"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(key))
                    return Json(new DosResult(0, null, "Key 不能为空。"));

                var ok = await _pluginManager.UnloadPluginAsync(key);
                return Json(new DosResult(ok ? 1 : 0, null,
                    ok ? $"插件 [{key}] 已卸载，可安全替换 DLL。" : $"插件 [{key}] 卸载失败（必须处于 Stopped 状态）。"));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "卸载插件失败：" + ex.Message));
            }
        }

        /// <summary>
        /// 获取指定插件的运行日志。
        /// POST /api/BusinessBase/Plugin/Logs
        /// Body: { Key: "plugin-key" }
        /// </summary>
        [HttpPost]
        public JsonResult Logs([FromBody] System.Dynamic.ExpandoObject body)
        {
            try
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)body;
                var key = dict?.ContainsKey("Key") == true ? dict["Key"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(key))
                    return Json(new DosResult(0, null, "Key 不能为空。"));

                var logs = _pluginManager.GetPluginLogs(key);
                return Json(new DosResult(1, new
                {
                    Key = key,
                    Logs = logs,
                    Count = logs.Count
                }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取日志失败：" + ex.Message));
            }
        }

        /// <summary>
        /// 清空指定插件的运行日志。
        /// POST /api/BusinessBase/Plugin/ClearLogs
        /// Body: { Key: "plugin-key" }
        /// </summary>
        [HttpPost]
        public JsonResult ClearLogs([FromBody] System.Dynamic.ExpandoObject body)
        {
            try
            {
                var dict = (System.Collections.Generic.IDictionary<string, object>)body;
                var key = dict?.ContainsKey("Key") == true ? dict["Key"]?.ToString() : null;
                if (string.IsNullOrWhiteSpace(key))
                    return Json(new DosResult(0, null, "Key 不能为空。"));

                _pluginManager.ClearPluginLogs(key);
                return Json(new DosResult(1, null, $"插件 [{key}] 日志已清空。"));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "清空日志失败：" + ex.Message));
            }
        }
    }
}
