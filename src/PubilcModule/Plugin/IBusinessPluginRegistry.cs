using System.Collections.Generic;

namespace Microi.net.Business
{
    /// <summary>
    /// 插件注册表（单例），保存全部已发现插件及其生命周期状态。
    /// 运行期可通过它查询插件是否就绪、做诊断与监控。
    /// </summary>
    public interface IBusinessPluginRegistry
    {
        /// <summary>
        /// 全部插件描述符（已按 Order + 依赖排序）。
        /// </summary>
        IReadOnlyList<BusinessPluginDescriptor> Plugins { get; }

        /// <summary>
        /// 按 Key 获取插件描述符，不存在返回 null。
        /// </summary>
        BusinessPluginDescriptor Get(string key);

        /// <summary>
        /// 判断指定插件是否已启动完成。
        /// </summary>
        bool IsStarted(string key);
    }
}
