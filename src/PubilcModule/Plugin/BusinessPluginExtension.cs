using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务插件 DI 装配与启动扩展。
    ///
    /// 用法（在 Program.cs 中）：
    ///   services.AddMicroiPlugin();             // builder.Services 阶段
    ///   ...
    ///   app.UseMicroiPlugin();                  // app = builder.Build() 之后
    ///
    /// 性能：插件程序集扫描仅在 AddMicroiPlugin() 时执行一次，
    /// 扫描结果缓存于 BusinessPluginManager，后续无反射开销。
    /// </summary>
    public static class BusinessPluginExtension
    {
        /// <summary>
        /// 注册业务插件：自动扫描已加载程序集中的 IBusinessPlugin，
        /// 调用其 ConfigureServices，并注册插件管理器单例。
        /// </summary>
        public static IServiceCollection AddMicroiPlugin(
            this IServiceCollection services,
            Action<BusinessPluginOptions> configure = null)
        {
            try
            {
                var options = new BusinessPluginOptions();
                configure?.Invoke(options);

                var pluginTypes = DiscoverPluginTypes(options);
                var plugins = new List<IBusinessPlugin>();

                foreach (var type in pluginTypes)
                {
                    if (Activator.CreateInstance(type) is IBusinessPlugin plugin)
                    {
                        plugins.Add(plugin);
                    }
                }

                // 让各插件向容器注册自己的服务
                foreach (var plugin in plugins.Where(p => p.Enabled))
                {
                    plugin.ConfigureServices(services);
                }

                // 注册插件管理器（既是编排器也是注册表，单例）
                var manager = new BusinessPluginManager(plugins);
                services.AddSingleton(manager);
                services.AddSingleton<IBusinessPluginRegistry>(manager);

                System.Console.WriteLine(
                    $"Microi.Plugin：【✅成功】插件系统装配完成，发现插件 {plugins.Count} 个："
                    + string.Join("、", plugins.Select(p => p.Key)));

                return services;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Microi.Plugin：【Error异常】插件系统装配失败：" + ex.Message);
                return services;
            }
        }

        /// <summary>
        /// 启动业务插件：驱动所有插件的生命周期，并注册关闭时的停止钩子。
        /// </summary>
        public static IApplicationBuilder UseMicroiPlugin(this IApplicationBuilder app)
        {
            try
            {
                var manager = app.ApplicationServices.GetService<BusinessPluginManager>();
                if (manager == null)
                {
                    System.Console.WriteLine(
                        "Microi.Plugin：【⚠️警告】未找到插件管理器，请确认已调用 services.AddMicroiPlugin()。");
                    return app;
                }

                var registry = app.ApplicationServices.GetService<IBusinessPluginRegistry>();
                var context = new PluginContext(app.ApplicationServices, registry);
                manager.StartAsync(context).GetAwaiter().GetResult();

                // 应用关闭时驱动停止生命周期
                var lifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();
                if (lifetime != null)
                {
                    lifetime.ApplicationStopping.Register(() =>
                    {
                        try { manager.StopAsync(context).GetAwaiter().GetResult(); }
                        catch (Exception ex)
                        {
                            System.Console.WriteLine(
                                "Microi.Plugin：【Error异常】插件停止失败：" + ex.Message);
                        }
                    });
                }

                System.Console.WriteLine("Microi.Plugin：【✅成功】插件系统启动完成！");
                return app;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine("Microi.Plugin：【Error异常】插件系统启动失败：" + ex.Message);
                return app;
            }
        }

        // ── 内部 ──

        private static List<Type> DiscoverPluginTypes(BusinessPluginOptions options)
        {
            var result = new HashSet<Type>(options.PluginTypes.Where(IsConcretePlugin));

            if (options.AutoScan)
            {
                var assemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
                foreach (var asm in options.AdditionalAssemblies)
                    assemblies.Add(asm);

                foreach (var asm in assemblies)
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

                    foreach (var t in types.Where(IsConcretePlugin))
                    {
                        result.Add(t);
                    }
                }
            }

            return result.ToList();
        }

        private static bool IsConcretePlugin(Type t)
        {
            return t != null
                && typeof(IBusinessPlugin).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsInterface
                && t.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
