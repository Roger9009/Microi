using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务插件装配选项。
    /// </summary>
    public sealed class BusinessPluginOptions
    {
        /// <summary>
        /// 是否自动扫描已加载程序集来发现 IBusinessPlugin。默认 true。
        /// </summary>
        public bool AutoScan { get; set; } = true;

        /// <summary>
        /// 额外参与插件扫描的程序集（用于未被主程序直接引用的插件 dll）。
        /// </summary>
        public List<Assembly> AdditionalAssemblies { get; } = new List<Assembly>();

        /// <summary>
        /// 显式注册的插件类型（无需扫描即可加载）。
        /// </summary>
        public List<Type> PluginTypes { get; } = new List<Type>();

        /// <summary>
        /// 注册一个插件类型。
        /// </summary>
        public BusinessPluginOptions AddPlugin<TPlugin>() where TPlugin : IBusinessPlugin
        {
            PluginTypes.Add(typeof(TPlugin));
            return this;
        }

        /// <summary>
        /// 添加一个参与扫描的程序集。
        /// </summary>
        public BusinessPluginOptions AddAssembly(Assembly assembly)
        {
            if (assembly != null) AdditionalAssemblies.Add(assembly);
            return this;
        }
    }
}
