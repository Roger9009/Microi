using System.Collections.Generic;

namespace Microi.net.Business
{
    /// <summary>
    /// 模块注册表（单例），保存全部已发现模块及其生命周期状态。
    /// 运行期可通过它查询模块是否就绪、做诊断与监控。
    /// </summary>
    public interface IBusinessModuleRegistry
    {
        /// <summary>
        /// 全部模块描述符（已按 Order + 依赖排序）。
        /// </summary>
        IReadOnlyList<BusinessModuleDescriptor> Modules { get; }

        /// <summary>
        /// 按 Key 获取模块描述符，不存在返回 null。
        /// </summary>
        BusinessModuleDescriptor Get(string key);

        /// <summary>
        /// 判断指定模块是否已启动完成。
        /// </summary>
        bool IsStarted(string key);
    }
}
