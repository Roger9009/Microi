using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 【示例插件】数据同步插件（带后台循环的完整插件模板）。
    ///
    /// 展示 IBusinessPlugin 的一种常见模式：
    /// 在 OnStartAsync 中启动后台循环，在 OnStopAsync 中安全停止。
    ///
    /// 开发者可参考此模式实现自己的后台任务插件。
    /// </summary>
    public class DataSyncPlugin : BusinessPluginBase
    {
        public override string Key => "data-sync";
        public override string Name => "数据同步插件";
        public override string Version => "1.0.0";
        public override int Order => 150;
        public override string[] DependsOn => new[] { "audit-log" };

        // ── 后台任务控制 ──
        private CancellationTokenSource _cts;
        private Task _backgroundTask;
        private readonly object _lock = new object();

        /// <summary>
        /// 插件加载完成。
        /// </summary>
        public override Task OnLoadAsync(PluginContext context)
        {
            Console.WriteLine($"[DataSyncPlugin] OnLoadAsync — 配置校验通过");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 插件已注册，初始化后台任务控制器。
        /// </summary>
        public override Task OnRegisterAsync(PluginContext context)
        {
            _cts = new CancellationTokenSource();
            Console.WriteLine($"[DataSyncPlugin] OnRegisterAsync — 后台任务控制器已初始化");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 插件启动：开启后台同步循环。
        /// </summary>
        public override Task OnStartAsync(PluginContext context)
        {
            lock (_lock)
            {
                if (_backgroundTask != null) return Task.CompletedTask;

                _backgroundTask = Task.Run(async () =>
                {
                    Console.WriteLine($"[DataSyncPlugin] ⏺ 数据同步后台任务已启动");
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            // 模拟同步逻辑（每 60 秒执行一次）
                            await Task.Delay(TimeSpan.FromSeconds(60), _cts.Token);
                            if (!_cts.Token.IsCancellationRequested)
                            {
                                // TODO: 在此执行实际的数据同步逻辑
                                // await SyncDataAsync(context);
                                Console.WriteLine($"[DataSyncPlugin] 同步心跳: {DateTime.Now:HH:mm:ss}");
                            }
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"[DataSyncPlugin] 同步异常: {ex.Message}");
                            // 等待后重试，避免死循环
                            try { await Task.Delay(5000, _cts.Token); } catch { break; }
                        }
                    }
                    Console.WriteLine($"[DataSyncPlugin] ⏹ 数据同步后台任务已停止");
                }, _cts.Token);
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 插件停止：取消后台任务。
        /// </summary>
        public override async Task OnStopAsync(PluginContext context)
        {
            CancellationTokenSource cts;
            lock (_lock)
            {
                cts = _cts;
                _cts = null;
            }

            if (cts != null)
            {
                cts.Cancel();
                try
                {
                    if (_backgroundTask != null)
                        await _backgroundTask;
                }
                catch (OperationCanceledException) { }
                finally
                {
                    _backgroundTask = null;
                    cts.Dispose();
                }
            }

            Console.WriteLine($"[DataSyncPlugin] OnStopAsync — 后台任务已安全停止");
        }

        /// <summary>
        /// 插件卸载。
        /// </summary>
        public override Task OnUnloadAsync(PluginContext context)
        {
            Console.WriteLine($"[DataSyncPlugin] OnUnloadAsync — 资源已释放");
            return Task.CompletedTask;
        }
    }
}
