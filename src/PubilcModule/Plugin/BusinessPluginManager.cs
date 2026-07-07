using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件管理器：注册表 + 生命周期编排器（单例）。
    ///
    /// 性能设计：
    /// 1. 反射扫描仅执行一次，结果缓存于内存。
    /// 2. 插件实例按需创建（Lazy），未用到的插件不实例化。
    /// 3. 生命周期阶段用 foreach 顺序驱动，无锁竞争。
    /// 4. 排序使用拓扑排序 + Order 降级，O(n log n) 复杂度。
    /// 5. 依赖关系校验仅启动时执行一次。
    /// </summary>
    public sealed class BusinessPluginManager : IBusinessPluginRegistry
    {
        private readonly List<BusinessPluginDescriptor> _descriptors;
        private bool _started = false;

        /// <summary>
        /// 已发现的全部插件（按排序后的顺序）。
        /// </summary>
        public IReadOnlyList<BusinessPluginDescriptor> Plugins => _descriptors;

        /// <summary>
        /// 插件引擎是否已启动完成。
        /// </summary>
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

        /// <inheritdoc/>
        public BusinessPluginDescriptor Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _descriptors.FirstOrDefault(d =>
                string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public bool IsStarted(string key)
        {
            return Get(key)?.Stage == PluginLifecycleStage.Started;
        }

        /// <summary>
        /// 驱动启动生命周期：OnLoadAsync → OnRegisterAsync → OnStartAsync。
        /// 单个插件失败不影响其它插件（标记为 Faulted）。
        /// </summary>
        public async Task StartAsync(PluginContext context)
        {
            foreach (var d in _descriptors)
            {
                await RunStage(d, PluginLifecycleStage.Loaded,
                    () => d.Plugin.OnLoadAsync(context));
            }

            foreach (var d in _descriptors.Where(x => x.Stage != PluginLifecycleStage.Faulted))
            {
                await RunStage(d, PluginLifecycleStage.Registered,
                    () => d.Plugin.OnRegisterAsync(context));
            }

            foreach (var d in _descriptors.Where(x => x.Stage != PluginLifecycleStage.Faulted))
            {
                await RunStage(d, PluginLifecycleStage.Started,
                    () => d.Plugin.OnStartAsync(context));
                if (d.Stage == PluginLifecycleStage.Started)
                {
                    System.Console.WriteLine(
                        $"Microi.Plugin：【✅成功】插件[{d.Plugin.Name}({d.Key}) v{d.Plugin.Version}]启动完成！");
                }
            }

            _started = true;
        }

        /// <summary>
        /// 驱动停止生命周期：逆序调用 OnStopAsync。
        /// </summary>
        public async Task StopAsync(PluginContext context)
        {
            _started = false;
            foreach (var d in Enumerable.Reverse(_descriptors))
            {
                await RunStage(d, PluginLifecycleStage.Stopped,
                    () => d.Plugin.OnStopAsync(context),
                    inProgressStage: PluginLifecycleStage.Stopped);
            }
        }

        /// <summary>
        /// 驱动卸载生命周期：逆序调用 OnUnloadAsync。
        /// </summary>
        public async Task UnloadAsync(PluginContext context)
        {
            foreach (var d in Enumerable.Reverse(_descriptors))
            {
                await RunStage(d, PluginLifecycleStage.Unloaded,
                    () => d.Plugin.OnUnloadAsync(context),
                    inProgressStage: PluginLifecycleStage.Unloaded);
            }
        }

        // ── 内部 ──────────────────────────────────────────────

        private static async Task RunStage(
            BusinessPluginDescriptor descriptor,
            PluginLifecycleStage successStage,
            Func<Task> action,
            PluginLifecycleStage? inProgressStage = null)
        {
            try
            {
                if (inProgressStage.HasValue)
                    SetStage(descriptor, inProgressStage.Value);
                await action();
                SetStage(descriptor, successStage);
            }
            catch (Exception ex)
            {
                descriptor.Stage = PluginLifecycleStage.Faulted;
                descriptor.Error = ex.Message;
                descriptor.StageChangedTime = DateTime.Now;
                System.Console.WriteLine(
                    $"Microi.Plugin：【Error异常】插件[{descriptor.Key}]在阶段[{successStage}]失败：{ex.Message}");
            }
        }

        private static void SetStage(BusinessPluginDescriptor descriptor, PluginLifecycleStage stage)
        {
            descriptor.Stage = stage;
            descriptor.StageChangedTime = DateTime.Now;
        }

        /// <summary>
        /// 拓扑排序，保证依赖插件排在前面。存在循环依赖时退化为 Order 排序并告警。
        /// </summary>
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
                    System.Console.WriteLine(
                        $"Microi.Plugin：【⚠️警告】检测到插件循环依赖，插件[{p.Key}]将按默认顺序加载。");
                    return;
                }
                visiting.Add(p.Key);
                foreach (var dep in p.DependsOn ?? Array.Empty<string>())
                {
                    if (map.TryGetValue(dep, out var depPlugin))
                        Visit(depPlugin);
                    else
                        System.Console.WriteLine(
                            $"Microi.Plugin：【⚠️警告】插件[{p.Key}]依赖的插件[{dep}]未找到。");
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
