using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business
{
    /// <summary>
    /// 业务底座上下文自动填充过滤器。
    /// 自动从 Token 解析当前用户与租户，填充到所有继承 BusinessParam 的 Action 参数中，
    /// 消除每个 Action 手动调用 await FillContext(param) 的重复代码。
    ///
    /// 用法：在 AddMicroiBusiness() 内部自动注册为全局过滤器，无需业务模块额外配置。
    /// </summary>
    public class BusinessContextFilter : IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 填充所有继承 BusinessParam 的参数
            foreach (var arg in context.ActionArguments.Values)
            {
                if (arg is BusinessParam bp)
                {
                    try
                    {
                        var current = await DiyToken.GetCurrentToken();
                        bp._CurrentUser ??= current.CurrentUser;
                        bp.OsClient ??= current.OsClient;
                    }
                    catch
                    {
                        // Token 不可用时（如 AllowAnonymous 接口）不做填充
                    }
                }
            }

            await next();
        }
    }
}
