using System;
using System.Threading.Tasks;
using Microi.net.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Mes
{
    /// <summary>
    /// MES 业务模块入口。
    /// </summary>
    public class MesModule : BusinessModuleBase
    {
        public override string Key => "mes";
        public override string Name => "MES 生产制造";
        public override int Order => 110;

        public override string[] DependsOn => new[] { "common" };

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddScoped<WorkOrderService>();
        }

        public override Task OnStartedAsync(BusinessModuleContext context)
        {
            Console.WriteLine("Microi.Business：【MES】模块已启动，API 路由 api/WorkOrder/* 可用。");
            return Task.CompletedTask;
        }
    }
}
