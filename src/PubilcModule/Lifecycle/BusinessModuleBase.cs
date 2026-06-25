using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务模块基类，提供默认空实现。
    /// 业务模块通常只需继承此类并重写 Key/Name 以及关心的生命周期钩子。
    /// </summary>
    public abstract class BusinessModuleBase : IBusinessModule
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
        public virtual string[] DependsOn => System.Array.Empty<string>();

        /// <inheritdoc/>
        public virtual bool AutoMigrate => true;

        /// <inheritdoc/>
        public virtual void ConfigureServices(IServiceCollection services) { }

        /// <inheritdoc/>
        public virtual Task OnRegisterAsync(BusinessModuleContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnStartingAsync(BusinessModuleContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnStartedAsync(BusinessModuleContext context) => Task.CompletedTask;

        /// <inheritdoc/>
        public virtual Task OnStoppingAsync(BusinessModuleContext context) => Task.CompletedTask;
    }
}
