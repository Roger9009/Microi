using System;
using Microsoft.Extensions.DependencyInjection;
using Microi.net.Business;

namespace Microi.Plugin.Demo.SalesOrder
{
    /// <summary>
    /// 销售订单独立插件 DLL。
    /// 一个 DLL = 一个业务领域，可独立部署、版本管理、客户定制替换。
    /// </summary>
    [BusinessPlugin(Key = "demo-salesorder", Name = "Demo-销售订单", Version = "1.0.0", Order = 200)]
    public sealed class SalesOrderPlugin : IPluginRegister
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<SalesOrderService>();
            Console.WriteLine("Microi.Demo：【SalesOrder.dll】销售订单插件已注册。");
        }
    }
}
