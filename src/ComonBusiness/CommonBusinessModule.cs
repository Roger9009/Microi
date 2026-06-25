using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 公共业务模块（基础模块）。
    /// Order 较小，先于 ERP/MES 加载，注册共享服务（单据编号等）。
    /// </summary>
    public class CommonBusinessModule : BusinessModuleBase
    {
        public override string Key => "common";
        public override string Name => "公共业务";
        public override int Order => 10;

        public override void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IBillNoService, BillNoService>();
        }

        public override Task OnStartedAsync(BusinessModuleContext context)
        {
            Console.WriteLine("Microi.Business：【公共业务】共享服务已就绪（单据编号 IBillNoService 等）。");
            return Task.CompletedTask;
        }
    }
}
