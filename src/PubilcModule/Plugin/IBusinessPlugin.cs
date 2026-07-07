using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务底座插件接口。
    /// 插件通过实现此接口获得完整的生命周期管理（加载 → 注册 → 启动 → 停止），
    /// 由 BusinessPluginManager 统一编排。
    ///
    /// 生命周期顺序（与 BusinessModuleManager 协调）：
    ///   发现(Discovered) → OnLoadAsync → ConfigureServices → OnRegisterAsync
    ///   → OnStartAsync → ...运行... → OnStopAsync → OnUnloadAsync
    ///
    /// 性能说明：
    /// - 插件实例为单例，只创建一次
    /// - 生命周期钩子为异步 Task，非热点路径
    /// - 插件元数据通过 Assembly 扫描缓存，仅首次加载时执行一次反射
    /// </summary>
    public interface IBusinessPlugin
    {
        /// <summary>插件唯一标识（小写，全局唯一），如 "audit-log"、"data-sync"。</summary>
        string Key { get; }

        /// <summary>插件显示名称，如 "审计日志插件"。</summary>
        string Name { get; }

        /// <summary>插件版本号。</summary>
        string Version { get; }

        /// <summary>加载顺序，数值越小越先加载。默认 100。</summary>
        int Order { get; }

        /// <summary>是否启用。返回 false 时跳过全部生命周期。</summary>
        bool Enabled { get; }

        /// <summary>依赖的其他插件 Key 列表（用于排序与启动校验）。</summary>
        string[] DependsOn { get; }

        /// <summary>
        /// 【服务注册阶段】向 DI 容器注册本插件的服务。
        /// 在应用 Build 之前调用，仅在首次发现时执行一次。
        /// </summary>
        void ConfigureServices(IServiceCollection services);

        /// <summary>
        /// 【加载阶段】插件程序集已加载，可做轻量初始化（读取内嵌资源、校验配置）。
        /// 此阶段不应执行重 IO 操作。
        /// </summary>
        Task OnLoadAsync(PluginContext context);

        /// <summary>
        /// 【注册阶段】容器已构建，可注册运行时组件（后台任务、事件订阅等）。
        /// </summary>
        Task OnRegisterAsync(PluginContext context);

        /// <summary>
        /// 【启动阶段】插件正式对外可用。可启动后台循环、监听、定时任务。
        /// </summary>
        Task OnStartAsync(PluginContext context);

        /// <summary>
        /// 【停止阶段】应用关闭或插件被禁用时释放资源。
        /// </summary>
        Task OnStopAsync(PluginContext context);

        /// <summary>
        /// 【卸载阶段】插件程序集将要被卸载前调用，清理所有托管/非托管资源。
        /// </summary>
        Task OnUnloadAsync(PluginContext context);
    }
}
