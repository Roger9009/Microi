using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件生命周期阶段枚举。
    /// 与 IBusinessPlugin 的各生命周期钩子一一对应，便于日志与监控。
    /// </summary>
    public enum PluginLifecycleStage
    {
        /// <summary>未加载</summary>
        None = 0,

        /// <summary>已发现（程序集扫描到插件类型）</summary>
        Discovered = 1,

        /// <summary>已加载（OnLoadAsync 执行后）</summary>
        Loaded = 2,

        /// <summary>服务已注册（ConfigureServices 执行后）</summary>
        ServicesRegistered = 3,

        /// <summary>已注册（OnRegisterAsync 执行后）</summary>
        Registered = 4,

        /// <summary>已启动（OnStartAsync 执行后，插件正式可用）</summary>
        Started = 5,

        /// <summary>已停止（OnStopAsync 执行后）</summary>
        Stopped = 6,

        /// <summary>已卸载（OnUnloadAsync 执行后）</summary>
        Unloaded = 7,

        /// <summary>启动失败</summary>
        Faulted = 99
    }
}
