

using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microi.net.Api
{
    /// <summary>
    /// License 后台心跳服务
    ///
    /// 功能�?    ///   1. 启动后延�?30 秒首次心跳（避免影响启动速度�?    ///   2. �?HeartbeatIntervalHours 小时向官方服务器验证一�?    ///   3. 检测服务器端吊销（Revoke），并在日志中告�?    ///   4. 离线超过 OfflineGraceDays 天时记录告警（不直接中断服务�?    ///
    /// 注册方式（Program.cs）：
    ///   builder.Services.AddHostedService&lt;LicenseBackgroundService&gt;();
    /// </summary>
    public class LicenseBackgroundService : BackgroundService
    {
        private static readonly TimeSpan InitialDelay    = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CheckInterval   = TimeSpan.FromHours(LicenseService.HeartbeatIntervalHours);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(InitialDelay, stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var hbResult = await LicenseService.HeartbeatAsync();
                    Console.WriteLine($"Microi：【License心跳】【{DateTime.Now:yyyy-MM-dd HH:mm:ss}】{hbResult}");

                    if (LicenseService.IsRevokedByServer)
                        Console.WriteLine($"Microi：【⚠️License心跳】服务器已吊销授权，下次重启将拒绝运行！");

                    var (overLimit, offlineDays) = LicenseService.CheckOfflineDays();
                    if (overLimit)
                        Console.WriteLine($"Microi：【⚠️License心跳】已离线 {offlineDays} 天（限制 {LicenseService.OfflineGraceDays} 天），请确保网络可达 api.itdos.com");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Microi：【License心跳异常】{ex.Message}");
                }

                try { await Task.Delay(CheckInterval, stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }
    }
}
