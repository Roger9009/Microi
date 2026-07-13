using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件管理器：注册表 + 生命周期编排器（单例）。
    /// 支持单插件级别的启动/停止/卸载，以及日志查询。
    /// </summary>
    public sealed class BusinessPluginManager : IBusinessPluginRegistry
    {
        private readonly List<BusinessPluginDescriptor> _descriptors;
        private bool _started = false;
        private PluginContext _context;

        public IReadOnlyList<BusinessPluginDescriptor> Plugins => _descriptors;
        public bool EngineStarted => _started;

        public BusinessPluginManager(IEnumerable<IBusinessPlugin> plugins)
        {
            var enabled = (plugins ?? Enumerable.Empty<IBusinessPlugin>())
                .Where(p => p != null && p.Enabled)
                .ToList();

            _descriptors = SortPlugins(enabled)
                .Select(p => new BusinessPluginDescriptor(p))
                .ToList();
        }

        // ── 全局生命周期 ──────────────────────────────────

        public async Task StartAsync(PluginContext context)
        {
            _context = context;
            foreach (var d in _descriptors)
            {
                await RunStage(d, PluginLifecycleStage.Loaded, () => d.Plugin.OnLoadAsync(context));
            }
            foreach (var d in _descriptors.Where(x => x.Stage != PluginLifecycleStage.Faulted))
            {
                await RunStage(d, PluginLifecycleStage.Registered, () => d.Plugin.OnRegisterAsync(context));
            }
            foreach (var d in _descriptors.Where(x => x.Stage != PluginLifecycleStage.Faulted))
            {
                await RunStage(d, PluginLifecycleStage.Started, () => d.Plugin.OnStartAsync(context));
                if (d.Stage == PluginLifecycleStage.Started)
                    Log(d, "✅ 插件启动完成");
            }
            _started = true;
        }

        public async Task StopAsync(PluginContext context)
        {
            _started = false;
            foreach (var d in Enumerable.Reverse(_descriptors))
            {
                await RunStage(d, PluginLifecycleStage.Stopped, () => d.Plugin.OnStopAsync(context),
                    inProgressStage: PluginLifecycleStage.Stopped);
            }
        }

        public async Task UnloadAsync(PluginContext context)
        {
            foreach (var d in Enumerable.Reverse(_descriptors))
            {
                await RunStage(d, PluginLifecycleStage.Unloaded, () => d.Plugin.OnUnloadAsync(context),
                    inProgressStage: PluginLifecycleStage.Unloaded);
            }
        }

        // ── 单插件管理 ──────────────────────────────────

        /// <summary>停止单个插件（维护时可调用）</summary>
        public async Task<bool> StopPluginAsync(string key)
        {
            var d = Get(key);
            if (d == null || !d.IsRunning) return false;
            await RunStage(d, PluginLifecycleStage.Stopped, () => d.Plugin.OnStopAsync(_context),
                inProgressStage: PluginLifecycleStage.Stopped);
            Log(d, "⏹ 插件已手动停止");
            return d.Stage == PluginLifecycleStage.Stopped;
        }

        /// <summary>重新启动单个插件（从 Stopped 状态）</summary>
        public async Task<bool> StartPluginAsync(string key)
        {
            var d = Get(key);
            if (d == null || !d.IsStopped) return false;
            await RunStage(d, PluginLifecycleStage.Started, () => d.Plugin.OnStartAsync(_context));
            Log(d, "▶ 插件已手动重新启动");
            return d.Stage == PluginLifecycleStage.Started;
        }

        /// <summary>卸载单个插件（从 Stopped 状态）</summary>
        public async Task<bool> UnloadPluginAsync(string key)
        {
            var d = Get(key);
            if (d == null || !d.IsStopped)
            {
                d?.AppendLog("⚠ 只能在 Stopped 状态下执行卸载，请先停止插件。");
                return false;
            }
            await RunStage(d, PluginLifecycleStage.Unloaded, () => d.Plugin.OnUnloadAsync(_context),
                inProgressStage: PluginLifecycleStage.Unloaded);
            Log(d, "🗑 插件已卸载，可替换 DLL 后重新加载");
            return d.Stage == PluginLifecycleStage.Unloaded;
        }

        /// <summary>获取指定插件的日志</summary>
        public IReadOnlyList<string> GetPluginLogs(string key)
        {
            return Get(key)?.GetLogs() ?? Array.Empty<string>();
        }

        /// <summary>清空指定插件的日志</summary>
        public void ClearPluginLogs(string key)
        {
            Get(key)?.ClearLogs();
        }

        // ── IBusinessPluginRegistry ──────────────────────

        public BusinessPluginDescriptor Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _descriptors.FirstOrDefault(d =>
                string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsStarted(string key) => Get(key)?.Stage == PluginLifecycleStage.Started;

        // ── 内部 ──────────────────────────────────────────

        private static async Task RunStage(
            BusinessPluginDescriptor d, PluginLifecycleStage successStage, Func<Task> action,
            PluginLifecycleStage? inProgressStage = null)
        {
            try
            {
                if (inProgressStage.HasValue) SetStage(d, inProgressStage.Value);
                await action();
                SetStage(d, successStage);
            }
            catch (Exception ex)
            {
                d.Stage = PluginLifecycleStage.Faulted;
                d.Error = ex.Message;
                d.StageChangedTime = DateTime.Now;
                d.AppendLog($"❌ 阶段 [{successStage}] 失败：{ex.Message}");
            }
        }

        private static void SetStage(BusinessPluginDescriptor d, PluginLifecycleStage stage)
        {
            d.Stage = stage;
            d.StageChangedTime = DateTime.Now;
        }

        private static void Log(BusinessPluginDescriptor d, string msg)
        {
            d.AppendLog(msg);
        }

        private static List<IBusinessPlugin> SortPlugins(List<IBusinessPlugin> plugins)
        {
            var byOrder = plugins.OrderBy(p => p.Order).ThenBy(p => p.Key, StringComparer.OrdinalIgnoreCase).ToList();
            var map = byOrder.ToDictionary(p => p.Key, p => p, StringComparer.OrdinalIgnoreCase);
            var result = new List<IBusinessPlugin>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(IBusinessPlugin p)
            {
                if (visited.Contains(p.Key)) return;
                if (visiting.Contains(p.Key))
                {
                    Console.WriteLine($"Microi.Plugin：【⚠️】插件循环依赖 [{p.Key}]，按默认顺序加载。");
                    return;
                }
                visiting.Add(p.Key);
                foreach (var dep in p.DependsOn ?? Array.Empty<string>())
                {
                    if (map.TryGetValue(dep, out var depPlugin)) Visit(depPlugin);
                    else Console.WriteLine($"Microi.Plugin：【⚠️】插件 [{p.Key}] 依赖 [{dep}] 未找到。");
                }
                visiting.Remove(p.Key);
                visited.Add(p.Key);
                result.Add(p);
            }

            foreach (var p in byOrder) Visit(p);
            return result;
        }
    }
}
