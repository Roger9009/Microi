using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务模块控制器基类。
    /// 统一从平台 Token 中解析当前用户与租户，填充到 BusinessParam。
    ///
    /// 自 v2: 上下文填充由全局 BusinessContextFilter 自动处理（AddMicroiBusiness 注册），
    /// 子类控制器不再需要手动调用 await FillContext(param)，
    /// 除非需要额外操作（如扩展 param 中非 BusinessParam 继承的字段）。
    ///
    /// 说明：业务模块以独立程序集编译并被主站点 AddApplicationPart 加载，
    /// 使用标准 [Authorize] 鉴权 + DiyToken 解析上下文，保持与平台一致且解耦。
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/[controller]/[action]")]
    public abstract class BusinessControllerBase : Controller
    {
        /// <summary>
        /// 用当前 Token 的用户与租户信息填充业务参数。
        /// 兼容模式：如果 BusinessContextFilter 已自动填充，此方法不做重复填充。
        /// 子类在需要额外参数处理时可继续使用。
        /// </summary>
        protected async Task FillContext(BusinessParam param)
        {
            if (param == null) return;
            var current = await GetCurrentContext();
            param._CurrentUser ??= current.CurrentUser;
            param.OsClient ??= current.OsClient;
        }

        /// <summary>
        /// 获取当前 Token 中的租户与用户信息。
        /// 当 Action 入参不是 BusinessParam（如直接使用 JObject）时，可单独调用此方法来获取上下文。
        /// </summary>
        protected async Task<(string OsClient, JObject CurrentUser)> GetCurrentContext()
        {
            var current = await DiyToken.GetCurrentToken();
            return (current.OsClient, current.CurrentUser);
        }
    }
}
