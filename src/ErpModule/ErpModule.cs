using System;
using System.Threading.Tasks;
using Microi.net.Business;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Erp
{
    /// <summary>
    /// ERP 业务模块入口。
    /// 由 BusinessModuleManager 自动发现并按生命周期装配。
    /// </summary>
    public class ErpModule : BusinessModuleBase
    {
        public override string Key => "erp";
        public override string Name => "ERP 进销存";
        public override int Order => 100;

        /// <summary>依赖公共业务模块（单据编号等）。</summary>
        public override string[] DependsOn => new[] { "common" };

        public override void ConfigureServices(IServiceCollection services)
        {
            // 业务服务为无状态轻量对象，按需 new 即可；
            // 如需注入依赖，可在此注册为 Scoped/Singleton。
            services.AddScoped<SalesOrderService>();
        }

        public override Task OnStartingAsync(BusinessModuleContext context)
        {
            // 启动前可在此校验/初始化 ERP 相关数据表（erp_sales_order 等）。
            Console.WriteLine("Microi.Business：【ERP】启动前检查通过。");
            return Task.CompletedTask;
        }

        public override Task OnStartedAsync(BusinessModuleContext context)
        {
            Console.WriteLine("Microi.Business：【ERP】模块已启动，API 路由 api/SalesOrder/* 可用。");
            return Task.CompletedTask;
        }
    }
}
