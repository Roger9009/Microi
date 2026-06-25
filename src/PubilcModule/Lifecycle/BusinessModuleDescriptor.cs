using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 模块运行期描述符，记录模块实例及其当前生命周期阶段。
    /// </summary>
    public sealed class BusinessModuleDescriptor
    {
        public BusinessModuleDescriptor(IBusinessModule module)
        {
            Module = module ?? throw new ArgumentNullException(nameof(module));
            Stage = BusinessLifecycleStage.Discovered;
        }

        /// <summary>模块实例</summary>
        public IBusinessModule Module { get; }

        /// <summary>模块 Key</summary>
        public string Key => Module.Key;

        /// <summary>当前生命周期阶段</summary>
        public BusinessLifecycleStage Stage { get; set; }

        /// <summary>启动失败时的异常信息</summary>
        public string Error { get; set; }

        /// <summary>最近一次阶段变更时间</summary>
        public DateTime StageChangedTime { get; set; } = DateTime.Now;
    }
}
