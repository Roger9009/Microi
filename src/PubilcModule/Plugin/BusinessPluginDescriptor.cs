using System;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件运行期描述符，记录插件实例及其当前生命周期阶段。
    /// 轻量 POCO，无业务逻辑。
    /// </summary>
    public sealed class BusinessPluginDescriptor
    {
        public BusinessPluginDescriptor(IBusinessPlugin plugin)
        {
            Plugin = plugin ?? throw new ArgumentNullException(nameof(plugin));
            Stage = PluginLifecycleStage.Discovered;
        }

        /// <summary>插件实例（单例）</summary>
        public IBusinessPlugin Plugin { get; }

        /// <summary>插件 Key</summary>
        public string Key => Plugin.Key;

        /// <summary>当前生命周期阶段</summary>
        public PluginLifecycleStage Stage { get; set; }

        /// <summary>启动失败时的异常信息</summary>
        public string Error { get; set; }

        /// <summary>最近一次阶段变更时间</summary>
        public DateTime StageChangedTime { get; set; } = DateTime.Now;
    }
}
