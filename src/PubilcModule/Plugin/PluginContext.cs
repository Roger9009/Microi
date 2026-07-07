using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件生命周期上下文，贯穿插件的加载、注册与启停过程。
    /// 轻量对象，通过构造函数注入 IServiceProvider，避免热路径分配。
    /// </summary>
    public sealed class PluginContext
    {
        /// <summary>
        /// 应用根级服务提供者（已构建），可解析任意注册服务。
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// 插件注册表，可查询其它已加载插件及其状态。
        /// </summary>
        public IBusinessPluginRegistry Registry { get; }

        public PluginContext(IServiceProvider services, IBusinessPluginRegistry registry)
        {
            Services = services ?? throw new ArgumentNullException(nameof(services));
            Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        /// <summary>
        /// 从 DI 容器解析服务（快捷方法，非泛型版本减少 JIT 开销）。
        /// </summary>
        public object Resolve(Type serviceType)
        {
            return Services.GetService(serviceType);
        }

        /// <summary>
        /// 从 DI 容器解析服务（泛型快捷方法）。
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }
    }
}
