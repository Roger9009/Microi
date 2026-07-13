using System;
using Microsoft.Extensions.DependencyInjection;
using Microi.net.Business;

namespace Microi.Plugin.Demo.WorkOrder
{
    /// <summary>
    /// 工单独立插件 DLL。
    /// 一个 DLL = 一个业务领域，可独立部署、版本管理、客户定制替换。
    /// </summary>
    [BusinessPlugin(Key = "demo-workorder", Name = "Demo-工单管理", Version = "1.0.0", Order = 210)]
    public sealed class WorkOrderPlugin : IPluginRegister
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<WorkOrderService>();
            Console.WriteLine("Microi.Demo：【WorkOrder.dll】工单插件已注册。");
        }
    }
}
