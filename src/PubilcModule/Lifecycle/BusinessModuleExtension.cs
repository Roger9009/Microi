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
    /// 业务底座 DI 装配与启动扩展。
    /// 用法（在 Program.cs 中）：
    ///   services.AddMicroiBusiness();        // builder.Services 阶段
    ///   ...
    ///   app.UseMicroiBusiness();             // app = builder.Build() 之后
    /// 约定与平台 AddMicroiJob / UseMicroiJob 一致。
    /// </summary>
    public static class BusinessModuleExtension
    {
        /// <summary>
        /// 注册业务底座：发现所有 IBusinessModule，调用其 ConfigureServices，并注册模块管理器。
        /// </summary>
        public static IServiceCollection AddMicroiBusiness(this IServiceCollection services, Action<BusinessOptions> configure = null)
        {
            try
            {
                var options = new BusinessOptions();
                configure?.Invoke(options);

                var moduleTypes = DiscoverModuleTypes(options);
                var modules = new List<IBusinessModule>();
                foreach (var type in moduleTypes)
                {
                    if (Activator.CreateInstance(type) is IBusinessModule module)
                    {
                        modules.Add(module);
                    }
                }

                // 让各模块向容器注册自己的服务
                foreach (var module in modules.Where(m => m.Enabled))
                {
                    module.ConfigureServices(services);
                }

                // 注册模块管理器（既是编排器也是注册表，单例）
                var manager = new BusinessModuleManager(modules);
                manager.AutoMigrate = options.AutoMigrate;
                manager.MigrateOsClients.AddRange(options.MigrateOsClients);
                services.AddSingleton(manager);
                services.AddSingleton<IBusinessModuleRegistry>(manager);

                // 将模块所在程序集中的 Controller 注册为 ApplicationPart，使其 API 可被路由发现
                var mvcBuilder = services.AddControllers();
                foreach (var asm in moduleTypes.Select(t => t.Assembly).Distinct())
                {
                    mvcBuilder.AddApplicationPart(asm);
                }

                Console.WriteLine($"Microi.Business：【✅成功】业务底座装配完成，发现模块 {modules.Count} 个：{string.Join("、", modules.Select(m => m.Key))}");
                return services;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microi.Business：【Error异常】业务底座装配失败：" + ex.Message);
                return services;
            }
        }

        /// <summary>
        /// 启动业务底座：驱动所有模块的启动生命周期，并注册关闭时的停止钩子。
        /// </summary>
        public static IApplicationBuilder UseMicroiBusiness(this IApplicationBuilder app)
        {
            try
            {
                var manager = app.ApplicationServices.GetService<BusinessModuleManager>();
                if (manager == null)
                {
                    Console.WriteLine("Microi.Business：【⚠️警告】未找到业务模块管理器，请确认已调用 services.AddMicroiBusiness()。");
                    return app;
                }

                var context = new BusinessModuleContext(app.ApplicationServices, manager);
                manager.StartAsync(context).GetAwaiter().GetResult();

                // 应用关闭时驱动停止生命周期
                var lifetime = app.ApplicationServices.GetService<IHostApplicationLifetime>();
                lifetime?.ApplicationStopping.Register(() =>
                {
                    try { manager.StopAsync(context).GetAwaiter().GetResult(); }
                    catch (Exception ex) { Console.WriteLine("Microi.Business：【Error异常】业务模块停止失败：" + ex.Message); }
                });

                Console.WriteLine("Microi.Business：【✅成功】业务底座启动完成！");
                return app;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Microi.Business：【Error异常】业务底座启动失败：" + ex.Message);
                return app;
            }
        }

        private static List<Type> DiscoverModuleTypes(BusinessOptions options)
        {
            var result = new HashSet<Type>(options.ModuleTypes.Where(IsConcreteModule));

            if (options.AutoScan)
            {
                var assemblies = new HashSet<Assembly>(AppDomain.CurrentDomain.GetAssemblies());
                foreach (var asm in options.AdditionalAssemblies) assemblies.Add(asm);

                foreach (var asm in assemblies)
                {
                    Type[] types;
                    try { types = asm.GetTypes(); }
                    catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray(); }
                    catch { continue; }

                    foreach (var t in types.Where(IsConcreteModule))
                    {
                        result.Add(t);
                    }
                }
            }

            return result.ToList();
        }

        private static bool IsConcreteModule(Type t)
        {
            return t != null
                && typeof(IBusinessModule).IsAssignableFrom(t)
                && !t.IsAbstract
                && !t.IsInterface
                && t.GetConstructor(Type.EmptyTypes) != null;
        }
    }
}
