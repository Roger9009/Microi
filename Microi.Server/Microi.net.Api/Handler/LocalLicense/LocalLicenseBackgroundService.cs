

using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microi.net.Api.LocalLicense
{
    /// <summary>
    /// License 后台心跳服务
    ///
    /// 功能�?    ///   1. 启动后延�?30 秒首次心跳（避免影响启动速度�?    ///   2. �?HeartbeatIntervalHours 小时向官方服务器验证一�?    ///   3. 检测服务器端吊销（Revoke），并在日志中告�?    ///   4. 离线超过 OfflineGraceDays 天时记录告警（不直接中断服务�?    ///
    /// 注册方式（Program.cs）：
    ///   builder.Services.AddHostedService&lt;LocalLicenseBackgroundService&gt;();
    /// </summary>
    public class LocalLicenseBackgroundService : BackgroundService
    {
        private static readonly TimeSpan InitialDelay    = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan CheckInterval   = TimeSpan.FromHours(LocalLicenseServiceFacade.HeartbeatIntervalHours);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try { await Task.Delay(InitialDelay, stoppingToken); }
            catch (TaskCanceledException) { return; }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var hbResult = await LocalLicenseServiceFacade.HeartbeatAsync();
                    Console.WriteLine($"Microi：【LocalLicense心跳】【{DateTime.Now:yyyy-MM-dd HH:mm:ss}】{hbResult}");

                    if (LocalLicenseServiceFacade.IsRevokedByServer)
                        Console.WriteLine($"Microi：【⚠️LocalLicense心跳】服务器已吊销本地附加授权，下次重启将拒绝运行！");

                    var (overLimit, offlineDays) = LocalLicenseServiceFacade.CheckOfflineDays();
                    if (overLimit)
                        Console.WriteLine($"Microi：【⚠️LocalLicense心跳】已离线 {offlineDays} 天（限制 {LocalLicenseServiceFacade.OfflineGraceDays} 天），请检查本地授权中心连接。");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Microi：【LocalLicense心跳异常】{ex.Message}");
                }

                try { await Task.Delay(CheckInterval, stoppingToken); }
                catch (TaskCanceledException) { return; }
            }
        }
    }
}
