using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务插件基类，提供所有生命周期钩子的默认空实现。
    /// 插件通常只需继承此类并重写 Key/Name 以及关心的生命周期钩子。
    ///
    /// 性能：空实现为 Task.CompletedTask，零开销。
    /// </summary>
    public abstract class BusinessPluginBase : IBusinessPlugin
    {
        /// <inheritdoc/>
        public abstract string Key { get; }

        /// <inheritdoc/>
        public abstract string Name { get; }

        /// <inheritdoc/>
        public virtual string Version => "1.0.0";

        /// <inheritdoc/>
        public virtual int Order => 100;

        /// <inheritdoc/>
        public virtual bool Enabled => true;

        /// <inheritdoc/>
        public virtual string[] DependsOn => Array.Empty<string>();

        /// <inheritdoc/>
        public virtual void ConfigureServices(IServiceCollection services) { }

        /// <inheritdoc/>
        public virtual Task OnLoadAsync(PluginContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnRegisterAsync(PluginContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnStartAsync(PluginContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnStopAsync(PluginContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnUnloadAsync(PluginContext context) => Task.CompletedTask;
    }
}
