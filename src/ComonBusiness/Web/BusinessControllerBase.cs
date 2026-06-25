using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务模块控制器基类。
    /// 统一从平台 Token 中解析当前用户与租户，填充到 BusinessParam，
    /// 子类控制器无需重复编写 DefaultParam 逻辑。
    ///
    /// 说明：业务模块以独立程序集编译并被主站点 AddApplicationPart 加载，
    /// 无法直接引用主站点的 DiyFilter，因此这里使用标准 [Authorize] 鉴权 +
    /// DiyToken 解析上下文，保持与平台一致且解耦。
    /// </summary>
    [Authorize]
    [EnableCors("any")]
    [Route("api/[controller]/[action]")]
    public abstract class BusinessControllerBase : Controller
    {
        /// <summary>
        /// 用当前 Token 的用户与租户信息填充业务参数。
        /// 在每个 Action 入口调用：await FillContext(param);
        /// </summary>
        protected async Task FillContext(BusinessParam param)
        {
            if (param == null) return;
            var current = await DiyToken.GetCurrentToken();
            param._CurrentUser = current.CurrentUser;
            param.OsClient = current.OsClient;
        }
    }
}
