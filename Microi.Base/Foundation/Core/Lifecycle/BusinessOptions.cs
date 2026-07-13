using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务底座装配选项。
    /// </summary>
    public sealed class BusinessOptions
    {
        /// <summary>
        /// 是否自动扫描已加载程序集来发现 IBusinessModule。默认 true。
        /// </summary>
        public bool AutoScan { get; set; } = true;

        /// <summary>
        /// 额外参与模块扫描的程序集（用于未被主程序直接引用的插件 dll）。
        /// </summary>
        public List<Assembly> AdditionalAssemblies { get; } = new List<Assembly>();

        /// <summary>
        /// 显式注册的模块类型（无需扫描即可加载）。
        /// </summary>
        public List<Type> ModuleTypes { get; } = new List<Type>();

        /// <summary>
        /// 是否在启动时执行代码优先自动建表/补列。默认 true。
        /// 设为 false 可全局关闭（各模块的 AutoMigrate 也会被忽略）。
        /// </summary>
        public bool AutoMigrate { get; set; } = true;

        /// <summary>
        /// 需要执行自动建表的租户列表。
        /// 为空时仅对主租户（OsClient.GetConfigOsClient()）执行。
        /// 多租户独立库场景，可在新建租户时另行调用 BusinessSchemaInitializer。
        /// </summary>
        public List<string> MigrateOsClients { get; } = new List<string>();

        /// <summary>
        /// 注册一个模块类型。
        /// </summary>
        public BusinessOptions AddModule<TModule>() where TModule : IBusinessModule
        {
            ModuleTypes.Add(typeof(TModule));
            return this;
        }

        /// <summary>
        /// 添加一个参与扫描的程序集。
        /// </summary>
        public BusinessOptions AddAssembly(Assembly assembly)
        {
            if (assembly != null) AdditionalAssemblies.Add(assembly);
            return this;
        }
    }
}
