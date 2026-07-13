using System;
using System.Linq;
using Dos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务模块健康诊断与管理 API。
    /// 运行时可查询各业务模块（ERP/MES/Common 等）的生命周期状态、诊断问题。
    /// 路由：api/BusinessMonitor/{action}
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/BusinessMonitor/[action]")]
    public class BusinessModuleMonitorController : Controller
    {
        private readonly BusinessModuleManager _manager;

        /// <summary>
        /// 构造函数注入（由 DI 自动提供 BusinessModuleManager 单例）。
        /// </summary>
        public BusinessModuleMonitorController(BusinessModuleManager manager)
        {
            _manager = manager;
        }

        /// <summary>
        /// 查询所有业务模块的状态概览。
        /// GET / POST  api/BusinessMonitor/Modules
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult Modules()
        {
            var list = _manager.Modules.Select(d => new
            {
                d.Key,
                Name = d.Module.Name,
                Version = d.Module.Version,
                d.Stage,
                StageName = d.Stage.ToString(),
                d.Error,
                StageChangedTime = d.StageChangedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                d.Module.Order,
                d.Module.Enabled,
                DependsOn = d.Module.DependsOn ?? Array.Empty<string>(),
                AutoMigrate = d.Module.AutoMigrate
            }).ToList();

            return Json(new DosResult(1, list, $"共 {list.Count} 个模块"));
        }

        /// <summary>
        /// 查询指定模块的详细信息。
        /// GET / POST  api/BusinessMonitor/Module?key=erp
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult Module(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return Json(new DosResult(0, null, "参数 key 不能为空。"));

            var d = _manager.Get(key);
            if (d == null)
                return Json(new DosResult(0, null, $"未找到模块 [{key}]。"));

            return Json(new DosResult(1, new
            {
                d.Key,
                Name = d.Module.Name,
                Version = d.Module.Version,
                d.Stage,
                StageName = d.Stage.ToString(),
                d.Error,
                StageChangedTime = d.StageChangedTime.ToString("yyyy-MM-dd HH:mm:ss"),
                d.Module.Order,
                d.Module.Enabled,
                DependsOn = d.Module.DependsOn ?? Array.Empty<string>(),
                AutoMigrate = d.Module.AutoMigrate
            }));
        }

        /// <summary>
        /// 查询已启动完成的模块列表（可用于依赖检查、健康检查）。
        /// GET / POST  api/BusinessMonitor/Started
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult Started()
        {
            var started = _manager.Modules
                .Where(d => d.Stage == Microi.net.Business.BusinessLifecycleStage.Started)
                .Select(d => new
                {
                    d.Key,
                    Name = d.Module.Name,
                    StageChangedTime = d.StageChangedTime.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

            return Json(new DosResult(1, started, $"已启动 {started.Count} 个模块"));
        }

        /// <summary>
        /// 查询有错误的模块列表。
        /// GET / POST  api/BusinessMonitor/Faulted
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult Faulted()
        {
            var faulted = _manager.Modules
                .Where(d => d.Stage == Microi.net.Business.BusinessLifecycleStage.Faulted)
                .Select(d => new
                {
                    d.Key,
                    Name = d.Module.Name,
                    d.Error,
                    StageChangedTime = d.StageChangedTime.ToString("yyyy-MM-dd HH:mm:ss")
                }).ToList();

            return Json(new DosResult(1, faulted, faulted.Count > 0
                ? $"发现 {faulted.Count} 个异常模块"
                : "所有模块运行正常"));
        }

        /// <summary>
        /// 简单健康检查端点（用于 k8s/负载均衡探活）。
        /// GET / POST  api/BusinessMonitor/Health
        /// 返回：{ Code:1, Msg:"Healthy", Data:{ ModuleCount, StartedCount } }
        /// </summary>
        [AllowAnonymous]
        [HttpGet, HttpPost]
        public JsonResult Health()
        {
            var total = _manager.Modules.Count;
            var started = _manager.Modules.Count(d =>
                d.Stage == Microi.net.Business.BusinessLifecycleStage.Started);
            var faulted = _manager.Modules.Count(d =>
                d.Stage == Microi.net.Business.BusinessLifecycleStage.Faulted);

            return Json(new DosResult(faulted == 0 ? 1 : 0,
                new { total, started, faulted },
                faulted == 0 ? "Healthy" : $"Unhealthy — {faulted} module(s) faulted"));
        }
    }
}
