using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务模块插件接口（模块级生命周期）。
    /// ERP、MES 等每个业务系统实现为一个模块，由 BusinessModuleManager 统一发现、排序、装配、启停。
    ///
    /// 生命周期顺序：
    ///   发现(Discovered) → ConfigureServices → OnRegisterAsync → OnStartingAsync → OnStartedAsync → ...运行... → OnStoppingAsync
    /// </summary>
    public interface IBusinessModule
    {
        /// <summary>
        /// 模块唯一标识（小写，全局唯一），如 "erp"、"mes"。
        /// </summary>
        string Key { get; }

        /// <summary>
        /// 模块显示名称，如 "ERP 进销存"。
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 模块版本号。
        /// </summary>
        string Version { get; }

        /// <summary>
        /// 加载顺序，数值越小越先加载（公共/基础模块应更小）。默认 100。
        /// </summary>
        int Order { get; }

        /// <summary>
        /// 是否启用。返回 false 时该模块的全部生命周期都会被跳过。
        /// </summary>
        bool Enabled { get; }

        /// <summary>
        /// 依赖的其它模块 Key 列表（用于排序与启动校验）。
        /// </summary>
        string[] DependsOn { get; }

        /// <summary>
        /// 是否启用代码优先自动建表/补列。
        /// 启动时扫描本模块程序集中带 [BusinessTable] 的实体，自动同步到数据库。默认 true。
        /// </summary>
        bool AutoMigrate { get; }

        /// <summary>
        /// 【服务注册阶段】向 DI 容器注册本模块的服务（Logic、State Machine、后台任务等）。
        /// 在应用 Build 之前调用。
        /// </summary>
        void ConfigureServices(IServiceCollection services);

        /// <summary>
        /// 【注册阶段】容器已构建，可做轻量初始化（读配置、注册状态机等），不要在此执行重 IO。
        /// </summary>
        Task OnRegisterAsync(BusinessModuleContext context);

        /// <summary>
        /// 【启动前】执行启动准备（数据库表检查、缓存预热、数据校验等）。
        /// </summary>
        Task OnStartingAsync(BusinessModuleContext context);

        /// <summary>
        /// 【启动后】模块正式对外可用（可启动后台监听、订阅 MQ、注册菜单等）。
        /// </summary>
        Task OnStartedAsync(BusinessModuleContext context);

        /// <summary>
        /// 【停止】应用关闭时释放资源（停止后台任务、刷盘、取消订阅）。
        /// </summary>
        Task OnStoppingAsync(BusinessModuleContext context);
    }
}
