using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace Microi.net.Business
{
    /// <summary>
    /// 模块管理器：注册表 + 生命周期编排器（单例）。
    /// 负责按 Order 与依赖关系排序模块，并依次驱动各生命周期阶段。
    /// </summary>
    public sealed class BusinessModuleManager : IBusinessModuleRegistry
    {
        private readonly List<BusinessModuleDescriptor> _descriptors;
        private readonly BusinessSchemaInitializer _schemaInitializer = new BusinessSchemaInitializer();

        /// <summary>是否启用启动时自动建表。</summary>
        public bool AutoMigrate { get; set; } = true;

        /// <summary>执行自动建表的租户列表（空=主租户）。</summary>
        public List<string> MigrateOsClients { get; } = new List<string>();

        public BusinessModuleManager(IEnumerable<IBusinessModule> modules)
        {
            var enabled = (modules ?? Enumerable.Empty<IBusinessModule>())
                .Where(m => m != null && m.Enabled)
                .ToList();

            _descriptors = SortModules(enabled)
                .Select(m => new BusinessModuleDescriptor(m))
                .ToList();
        }

        /// <inheritdoc/>
        public IReadOnlyList<BusinessModuleDescriptor> Modules => _descriptors;

        /// <inheritdoc/>
        public BusinessModuleDescriptor Get(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) return null;
            return _descriptors.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        }

        /// <inheritdoc/>
        public bool IsStarted(string key)
        {
            return Get(key)?.Stage == BusinessLifecycleStage.Started;
        }

        /// <summary>
        /// 驱动启动生命周期：OnRegisterAsync → OnStartingAsync → OnStartedAsync。
        /// 单个模块失败不影响其它模块（标记为 Faulted）。
        /// </summary>
        public async Task StartAsync(BusinessModuleContext context)
        {
            // 阶段 1：注册
            foreach (var d in _descriptors)
            {
                await RunStage(d, BusinessLifecycleStage.Registered,
                    () => d.Module.OnRegisterAsync(context));
            }

            // 阶段 1.5：代码优先自动建表/补列
            if (AutoMigrate)
            {
                MigrateSchema();
            }

            // 阶段 2：启动前
            foreach (var d in _descriptors.Where(x => x.Stage != BusinessLifecycleStage.Faulted))
            {
                await RunStage(d, BusinessLifecycleStage.Starting,
                    () => d.Module.OnStartingAsync(context));
            }

            // 阶段 3：启动后
            foreach (var d in _descriptors.Where(x => x.Stage != BusinessLifecycleStage.Faulted))
            {
                await RunStage(d, BusinessLifecycleStage.Started,
                    () => d.Module.OnStartedAsync(context));
                if (d.Stage == BusinessLifecycleStage.Started)
                {
                    Console.WriteLine($"Microi.Business：【✅成功】业务模块[{d.Module.Name}({d.Key}) v{d.Module.Version}]启动完成！");
                }
            }
        }

        /// <summary>
        /// 驱动停止生命周期：按启动逆序调用 OnStoppingAsync。
        /// </summary>
        public async Task StopAsync(BusinessModuleContext context)
        {
            foreach (var d in Enumerable.Reverse(_descriptors))
            {
                await RunStage(d, BusinessLifecycleStage.Stopped,
                    () => d.Module.OnStoppingAsync(context),
                    inProgressStage: BusinessLifecycleStage.Stopping);
            }
        }

        /// <summary>
        /// 对每个启用自动建表的模块，扫描其程序集中的 [BusinessTable] 实体并同步到数据库。
        /// 同步失败不阻断启动（仅告警）。
        /// </summary>
        private void MigrateSchema()
        {
            // 解析目标租户：未配置则使用主租户
            var osClients = MigrateOsClients.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().ToList();
            if (osClients.Count == 0)
            {
                try { osClients.Add(OsClientExtend.GetConfigOsClient()); }
                catch (Exception ex) { Console.WriteLine($"Microi.Business：【⚠️警告】解析主租户失败，跳过自动建表：{ex.Message}"); return; }
            }

            foreach (var d in _descriptors.Where(x => x.Module.AutoMigrate && x.Stage != BusinessLifecycleStage.Faulted))
            {
                Type[] entityTypes;
                try { entityTypes = d.Module.GetType().Assembly.GetTypes(); }
                catch (ReflectionTypeLoadException ex) { entityTypes = ex.Types.Where(t => t != null).ToArray(); }
                catch (Exception ex) { Console.WriteLine($"Microi.Business：【⚠️警告】模块[{d.Key}]扫描实体失败：{ex.Message}"); continue; }

                foreach (var osClient in osClients)
                {
                    try
                    {
                        var result = _schemaInitializer.EnsureSchema(entityTypes, osClient);
                        if (result != null && result.Code == 1)
                        {
                            Console.WriteLine($"Microi.Business：【自动建表】模块[{d.Key}] {result.Msg}");
                        }
                        else
                        {
                            Console.WriteLine($"Microi.Business：【⚠️警告】模块[{d.Key}]自动建表未完全成功：{result?.Msg}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Microi.Business：【Error异常】模块[{d.Key}]租户[{osClient}]自动建表异常：{ex.Message}");
                    }
                }
            }
        }

        private static async Task RunStage(
            BusinessModuleDescriptor descriptor,
            BusinessLifecycleStage successStage,
            Func<Task> action,
            BusinessLifecycleStage? inProgressStage = null)
        {
            try
            {
                if (inProgressStage.HasValue) SetStage(descriptor, inProgressStage.Value);
                await action();
                SetStage(descriptor, successStage);
            }
            catch (Exception ex)
            {
                descriptor.Stage = BusinessLifecycleStage.Faulted;
                descriptor.Error = ex.Message;
                descriptor.StageChangedTime = DateTime.Now;
                Console.WriteLine($"Microi.Business：【Error异常】业务模块[{descriptor.Key}]在阶段[{successStage}]失败：{ex.Message}");
            }
        }

        private static void SetStage(BusinessModuleDescriptor descriptor, BusinessLifecycleStage stage)
        {
            descriptor.Stage = stage;
            descriptor.StageChangedTime = DateTime.Now;
        }

        /// <summary>
        /// 按 Order 升序排序，并保证依赖模块排在前面（拓扑排序）。
        /// 存在循环依赖时退化为 Order 排序并告警。
        /// </summary>
        private static List<IBusinessModule> SortModules(List<IBusinessModule> modules)
        {
            var byOrder = modules.OrderBy(m => m.Order).ThenBy(m => m.Key, StringComparer.OrdinalIgnoreCase).ToList();
            var map = byOrder.ToDictionary(m => m.Key, m => m, StringComparer.OrdinalIgnoreCase);

            var result = new List<IBusinessModule>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(IBusinessModule m)
            {
                if (visited.Contains(m.Key)) return;
                if (visiting.Contains(m.Key))
                {
                    Console.WriteLine($"Microi.Business：【⚠️警告】检测到模块循环依赖，模块[{m.Key}]将按默认顺序加载。");
                    return;
                }
                visiting.Add(m.Key);
                foreach (var dep in m.DependsOn ?? Array.Empty<string>())
                {
                    if (map.TryGetValue(dep, out var depModule))
                    {
                        Visit(depModule);
                    }
                    else
                    {
                        Console.WriteLine($"Microi.Business：【⚠️警告】模块[{m.Key}]依赖的模块[{dep}]未找到。");
                    }
                }
                visiting.Remove(m.Key);
                visited.Add(m.Key);
                result.Add(m);
            }

            foreach (var m in byOrder) Visit(m);
            return result;
        }
    }
}
