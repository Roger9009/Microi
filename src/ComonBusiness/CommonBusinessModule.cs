using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 公共业务模块（基础模块）。
    /// Order 较小，先于 ERP/MES 加载，注册共享服务（单据编号、Schema 服务等）。
    /// </summary>
    public class CommonBusinessModule : BusinessModuleBase
    {
        public override string Key => "common";
        public override string Name => "公共业务";
        public override int Order => 10;

        public override void ConfigureServices(IServiceCollection services)
        {
            // ── 共享业务服务 ──
            services.AddSingleton<IBillNoService, BillNoService>();

            // ── Schema 服务（注册为 Scoped 以支持后续注入到 Controller/Service） ──
            services.AddScoped<BusinessSchemaService>();
            services.AddScoped<BusinessFieldConfigService>();
            services.AddScoped<BusinessDocRelationService>();

            // ── 文档读写器（无状态，注册为 Singleton） ──
            // BusinessDocumentReader / BusinessDocumentWriter 为静态类，无需注册
            // BusinessSchemaInitializer 由模块管理器内部使用，无需注册

            Console.WriteLine("Microi.Business：【公共业务】共享服务已注册（IBillNoService、BusinessSchemaService 等）。");
        }

        public override Task OnStartedAsync(BusinessModuleContext context)
        {
            Console.WriteLine("Microi.Business：【公共业务】共享服务已就绪（单据编号 IBillNoService 等）。");
            return Task.CompletedTask;
        }
    }
}
