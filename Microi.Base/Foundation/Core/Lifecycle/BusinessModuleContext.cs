using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 模块生命周期上下文，贯穿模块的注册与启停过程。
    /// </summary>
    public sealed class BusinessModuleContext
    {
        public BusinessModuleContext(IServiceProvider services, IBusinessModuleRegistry registry)
        {
            Services = services;
            Registry = registry;
        }

        /// <summary>
        /// 应用根级服务提供者（已构建），可解析任意注册服务。
        /// </summary>
        public IServiceProvider Services { get; }

        /// <summary>
        /// 模块注册表，可查询其它已加载模块及其状态。
        /// </summary>
        public IBusinessModuleRegistry Registry { get; }

        /// <summary>
        /// 从 DI 容器解析服务（快捷方法）。
        /// </summary>
        public T Resolve<T>() where T : class
        {
            return Services.GetService(typeof(T)) as T;
        }
    }
}
