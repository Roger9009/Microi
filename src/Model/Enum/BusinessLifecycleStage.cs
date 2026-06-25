namespace Microi.net.Business
{
    /// <summary>
    /// 模块插件生命周期阶段。
    /// 与 IBusinessModule 的各生命周期钩子一一对应，便于日志、监控、诊断。
    /// </summary>
    public enum BusinessLifecycleStage
    {
        /// <summary>未加载</summary>
        None = 0,

        /// <summary>已发现（程序集扫描到模块类型）</summary>
        Discovered = 1,

        /// <summary>服务注册阶段（ConfigureServices 执行中）</summary>
        ConfiguringServices = 2,

        /// <summary>注册完成（OnRegisterAsync 执行后）</summary>
        Registered = 3,

        /// <summary>启动中（OnStartingAsync 执行中）</summary>
        Starting = 4,

        /// <summary>已启动（OnStartedAsync 执行后，模块正式可用）</summary>
        Started = 5,

        /// <summary>停止中（OnStoppingAsync 执行中）</summary>
        Stopping = 6,

        /// <summary>已停止</summary>
        Stopped = 7,

        /// <summary>启动失败</summary>
        Faulted = 99
    }
}
