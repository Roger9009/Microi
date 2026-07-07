using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 【示例插件】审计日志插件。
    /// 展示 IBusinessPlugin 的完整生命周期用法。
    ///
    /// 开发者可复制此类作为新插件开发的模板。
    /// </summary>
    public class AuditLogPlugin : BusinessPluginBase
    {
        public override string Key => "audit-log";
        public override string Name => "审计日志插件";
        public override string Version => "1.0.0";
        public override int Order => 50;

        /// <summary>
        /// 生命周期：ConfigureServices。
        /// 向 DI 容器注册插件自己的服务。
        /// </summary>
        public override void ConfigureServices(IServiceCollection services)
        {
            // 示例：注册一个后台服务
            // services.AddHostedService<AuditLogBackgroundService>();
            // 示例：注册一个 Scoped Service
            // services.AddScoped<IAuditLogService, AuditLogService>();
        }

        /// <summary>
        /// 生命周期：OnLoadAsync。
        /// 插件程序集已加载，可读取内嵌资源或校验配置。
        /// 不应在此阶段做重 IO。
        /// </summary>
        public override Task OnLoadAsync(PluginContext context)
        {
            Console.WriteLine($"[AuditLogPlugin] OnLoadAsync — 插件已加载，当前时间: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 生命周期：OnRegisterAsync。
        /// 容器已构建，可注册运行时组件、订阅事件。
        /// </summary>
        public override Task OnRegisterAsync(PluginContext context)
        {
            Console.WriteLine($"[AuditLogPlugin] OnRegisterAsync — 插件服务已注册");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 生命周期：OnStartAsync。
        /// 插件正式对外可用。可在此启动后台任务或定时器。
        /// </summary>
        public override Task OnStartAsync(PluginContext context)
        {
            Console.WriteLine($"[AuditLogPlugin] OnStartAsync — ⏺ 插件已启动，审计日志记录功能可用");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 生命周期：OnStopAsync。
        /// 应用关闭或插件被禁用时释放资源。
        /// </summary>
        public override Task OnStopAsync(PluginContext context)
        {
            Console.WriteLine($"[AuditLogPlugin] OnStopAsync — ⏹ 插件已停止");
            return Task.CompletedTask;
        }

        /// <summary>
        /// 生命周期：OnUnloadAsync。
        /// 插件程序集将要被卸载前调用，清理所有资源。
        /// </summary>
        public override Task OnUnloadAsync(PluginContext context)
        {
            Console.WriteLine($"[AuditLogPlugin] OnUnloadAsync — 插件已卸载，资源已释放");
            return Task.CompletedTask;
        }
    }
}
