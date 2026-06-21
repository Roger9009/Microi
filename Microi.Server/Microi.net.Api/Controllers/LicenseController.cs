using Dos.Common;
using Lazy.Captcha.Core;

using Microi.net;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System;

namespace Microi.net.Api
{
    /// <summary>
    /// License授权管理
    /// 
    /// 同一套代码部署在两种服务器上�?
    /// - License服务器（有私钥）：Apply/Issue/Check/Revoke 等数据库操作可用
    /// - 客户服务器（无私钥）：仅 GetHardwareId/Verify/WriteLicenseFile/Diagnostics 可用
    /// </summary>
    [EnableCors("any")]
    [ServiceFilter(typeof(DiyFilter<dynamic>))]
    [Route("api/[controller]/[action]")]
    public class LicenseController : Controller
    {
        private readonly ICaptcha _captcha;

        public LicenseController(ICaptcha captcha)
        {
            _captcha = captcha;
        }

        /// <summary>
        /// 获取License申请验证码（匹名可访问）
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetCaptcha()
        {
            try
            {
                var captchaId = "license:Captcha:" + Guid.NewGuid().ToString("N");
                var info = _captcha.Generate(captchaId);
                if (info == null)
                    return Json(new DosResult(0, null, "获取验证码失败"));
                return Json(new DosResult(1, new { CaptchaId = info.Id, Image = Convert.ToBase64String(info.Bytes) }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取验证码失�? " + ex.Message));
            }
        }

        /// <summary>
        /// 客户申请License（提交HID和公司信息，写入diy_license表）
        /// 仅在License服务器（有私钥）上可�?
        /// 服务器IP自动从请求中获取，无需客户手动填写
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> Apply([FromBody] LicenseApplyRequest request)
        {
            try
            {
                // 验证码校验"
                if (string.IsNullOrWhiteSpace(request?.CaptchaId))
                    return Json(new DosResult(0, null, "请先获取验证码"));
                if (string.IsNullOrWhiteSpace(request?.CaptchaValue))
                    return Json(new DosResult(0, null, "请输入验证码"));
                if (!_captcha.Validate(request.CaptchaId, request.CaptchaValue, true, true))
                    return Json(new DosResult(0, null, "验证码错误，请重新输入"));

                // 自动获取客户端IP（优先X-Forwarded-For，适配反向代理/Docker环境）"
                var clientIP = request?.IP;
                if (string.IsNullOrWhiteSpace(clientIP))
                {
                    clientIP = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
                        ?? HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
                        ?? HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString()
                        ?? "";
                }

                var result = await LicenseService.ApplyAsync(
                    request?.HID, request?.Company, request?.Name, request?.Phone,
                    clientIP, request?.ProductType, request?.ExpirationDate,
                    request?.UpdateExpirationDate, request?.Remark,
                    request?.Account, request?.Password);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "License申请失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 在线签发License（需要私�?+ 管理员权限）
        /// 仅在License服务器（有私钥）上可�?
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Issue([FromBody] LicenseIssueRequest request)
        {
            // 验证管理员权限（权限检查保留在Controller层）
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null)
                return Json(new DosResult(0, null, "请先登录"));

            var level = currentUser["Level"].Val<int>();
            if (level < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可签发License"));

            try
            {
                var result = await LicenseService.IssueAsync(
                    request?.HID, request?.Company, request?.Name, request?.Phone,
                    request?.IP, request?.ProductType, request?.ExpirationDate,
                    request?.UpdateExpirationDate);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "License签发失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 获取 License 模块的可配置项（匿名可访问）
        /// 前端通过此接口读�?ContactEmail 等，避免硬编�?
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public JsonResult GetConfig()
        {
            return Json(new DosResult(1, new
            {
                ContactEmail = LicenseService.ContactEmail
            }));
        }

        /// <summary>
        /// 获取当前服务器的硬件指纹ID（匿名可访问，本地操作）
        /// </summary>
        [HttpGet, HttpPost]
        [AllowAnonymous]
        public JsonResult GetHardwareId()
        {
            try
            {
                var hid = LicenseService.GetHardwareId();
                return Json(new DosResult(1, new { HID = hid }));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取HID失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 验证当前服务器的License状态（匿名可访问，本地操作�?
        /// </summary>
        [HttpGet, HttpPost]
        [AllowAnonymous]
        public JsonResult Verify()
        {
            try
            {
                var data = LicenseService.Verify();
                return Json(new DosResult(1, data));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "License验证失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 获取硬件指纹诊断信息（需要登录）
        /// </summary>
        [HttpGet, HttpPost]
        public JsonResult Diagnostics()
        {
            try
            {
                var diagnostics = LicenseService.GetDiagnostics();
                return Json(new DosResult(1, diagnostics));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "获取诊断信息失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 查询License状态（根据HID查询是否已签发、是否被作废�?
        /// 仅查询数据库，不需要私�?
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> Check([FromBody] LicenseCheckRequest request)
        {
            try
            {
                var result = await LicenseService.CheckAsync(request?.HID);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "查询License状态失�? " + ex.Message));
            }
        }

        /// <summary>
        /// 查询License申请状态（根据HID查询是否已提交申请及当前状态）
        /// 不返回LicenseContent，仅返回申请元数据，匿名可访�?
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> QueryApplication([FromBody] LicenseCheckRequest request)
        {
            try
            {
                var result = await LicenseService.QueryApplicationAsync(request?.HID);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "查询申请状态失�? " + ex.Message));
            }
        }

        /// <summary>
        /// 将License内容写入当前服务器磁盘（客户前端"自动部署"时调用本地服务器�?
        /// 写入前会验证License内容的合法性（JSON格式 + RSA签名验签�?
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public JsonResult WriteLicenseFile([FromBody] WriteLicenseFileRequest request)
        {
            try
            {
                var result = LicenseService.WriteLicenseFile(request?.LicenseContent);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "写入License文件失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 作废或恢复License（仅超级管理员可操作�?
        /// 仅在License服务器（有私钥）上可�?
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Revoke([FromBody] LicenseRevokeRequest request)
        {
            // 验证管理员权限（权限检查保留在Controller层）
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null)
                return Json(new DosResult(0, null, "请先登录"));

            var level = currentUser["Level"].Val<int>();
            if (level < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可作废License"));

            try
            {
                var result = await LicenseService.RevokeAsync(request?.HID, request?.Revoke ?? true);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "操作失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 审核通过License申请（对Pending状态的申请执行签发�?
        /// 仅超级管理员可操作，仅在License服务器（有私钥）上可�?
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Approve([FromBody] LicenseCheckRequest request)
        {
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null)
                return Json(new DosResult(0, null, "请先登录"));

            var level = currentUser["Level"].Val<int>();
            if (level < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可审核License"));

            try
            {
                var result = await LicenseService.ApproveAsync(request?.HID);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "审核失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 驳回License申请（附驳回原因�?
        /// 仅超级管理员可操作，仅在License服务器（有私钥）上可�?
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> Reject([FromBody] LicenseRejectRequest request)
        {
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null)
                return Json(new DosResult(0, null, "请先登录"));

            var level = currentUser["Level"].Val<int>();
            if (level < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可驳回License"));

            try
            {
                var result = await LicenseService.RejectAsync(request?.HID, request?.RejectReason);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "驳回失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 生成离线注册申请文件（纯内网/无法访问 api.itdos.com 时使用）
        /// 将返回的 JSON 保存为文件发给官方，收到 license.json 后通过「手动导入」写�?
        /// 匿名可访问（因为内网机器可能没有登录态）
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public IActionResult GenerateRegistrationFile([FromBody] LicenseRegistrationRequest request)
        {
            try
            {
                var result = LicenseService.GenerateRegistrationPackage(
                    request?.Company, request?.Name, request?.Phone,
                    request?.IP, request?.ProductType, request?.Remark);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "生成失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 将客户端生成的离线注册文件（.milic）直接提交到 License 服务器，替代邮件发送流程�?
        /// 解密注册包、验证完整性哈希后写入 Pending 申请记录，等待管理员审核�?
        /// 匿名可访问（内网上传场景），�?IP+HID 双维�?60 秒限流防滥用�?
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        public async Task<JsonResult> ImportRegistrationFile([FromBody] ImportRegistrationRequest request)
        {
            // IP + HID 双维度限流：60 秒内�?IP 最�?1 �?
            var clientIp = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim()
                ?? HttpContext.Request.Headers["X-Real-IP"].FirstOrDefault()
                ?? HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var throttleKey = $"{clientIp}|{request?.HID?.Trim().ToUpperInvariant()}";
            if (ImportThrottle.TryGetValue(throttleKey, out var lastTime)
                && (DateTime.UtcNow - lastTime).TotalSeconds < 60)
                return Json(new DosResult(0, null, "提交过于频繁，请 60 秒后重试"));
            ImportThrottle[throttleKey] = DateTime.UtcNow;

            try
            {
                var result = await LicenseService.ImportRegistrationFile(
                    request?.HID, request?.EncryptedContent);
                return Json(result);
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "导入失败: " + ex.Message));
            }
        }

        // 简单内存限流表（进程级别，重启后清零，足够防止突发滥用�?
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime>
            ImportThrottle = new();

        /// <summary>
        /// 生成 RSA 2048 密钥对（License服务器初始化使用，仅超级管理员可调用�?
        /// 输出：PublicKeyBase64 填入 LicenseService.DefaultPublicKeyBase64
        ///       PrivateKeyBase64 设为环境变量 MICROI_LICENSE_PRIVATE_KEY
        /// ⚠️ 密钥只生成一次，生成后务必妥善保管私钥并更新公钥常量
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> GenerateKeyPair()
        {
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null)
                return Json(new DosResult(0, null, "请先登录"));

            var level = currentUser["Level"].Val<int>();
            if (level < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可生成密钥对"));

            try
            {
                var (pubBase64, prvBase64, pubPem, prvPem) = LicenseService.GenerateKeyPair();
                return Json(new DosResult(1, new
                {
                    PublicKeyBase64 = pubBase64,
                    PrivateKeyBase64 = prvBase64,
                    PublicKeyPem = pubPem,
                    PrivateKeyPem = prvPem,
                    Instructions = new[]
                    {
                        "1. 将 PublicKeyBase64 替换 LicenseService.cs 中的 DefaultPublicKeyBase64 常量，然后重新编译部署",
                        "2. 在 License 服务器设置环境变量：MICROI_LICENSE_PRIVATE_KEY = PrivateKeyBase64",
                        "3. 私钥只需在 License 服务器上配置，切勿提交到代码仓库",
                        "4. 密钥对生成一次即可，更换公钥会导致历史 License 文件无法验证"
                    }
                }, "密钥对生成成功，请按 Instructions 步骤操作"));
            }
            catch (Exception ex)
            {
                return Json(new DosResult(0, null, "生成密钥对失败: " + ex.Message));
            }
        }

        /// <summary>
        /// 管理员：获取 License 列表（超级管理员）
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> List([FromQuery] string status = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null || currentUser["Level"].Val<int>() < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可查看License列表"));
            var result = LicenseService.GetLicenseList(status, page, pageSize);
            return Json(result);
        }

        /// <summary>
        /// 管理员：获取 License 操作日志（超级管理员）
        /// </summary>
        [HttpGet]
        public async Task<JsonResult> Logs([FromQuery] string hid = "", [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var currentUser = await DiyToken.GetCurrentUser();
            if (currentUser == null || currentUser["Level"].Val<int>() < DiyCommon.MaxRoleLevel)
                return Json(new DosResult(0, null, "仅超级管理员可查看操作日志"));
            var result = LicenseService.GetLicenseLogs(hid, page, pageSize);
            return Json(result);
        }
    }

    /// <summary>
    /// 导入离线注册文件请求参数
    /// </summary>
    public class ImportRegistrationRequest
    {
        /// <summary>硬件指纹ID（注册包中明文展示，用于导出密钥�?/summary>
        public string HID { get; set; }
        /// <summary>加密的注册包内容�?milic 文件内容�?/summary>
        public string EncryptedContent { get; set; }
    }

    /// <summary>
    /// 离线注册申请请求参数
    /// </summary>
    public class LicenseRegistrationRequest
    {
        /// <summary>授权公司名称</summary>
        public string Company { get; set; }
        /// <summary>联系人姓�?/summary>
        public string Name { get; set; }
        /// <summary>联系电话</summary>
        public string Phone { get; set; }
        /// <summary>服务器IP（选填，便于官方核实）</summary>
        public string IP { get; set; }
        /// <summary>产品类型：Personal / Enterprise</summary>
        public string ProductType { get; set; }
        /// <summary>备注</summary>
        public string Remark { get; set; }
    }

    /// <summary>
    /// License申请请求参数（客户提交）
    /// </summary>
    public class LicenseApplyRequest
    {
        /// <summary>联系电话</summary>
        public string Phone { get; set; }
        /// <summary>服务器IP</summary>
        public string IP { get; set; }
        /// <summary>硬件指纹ID</summary>
        public string HID { get; set; }
        /// <summary>授权公司名称</summary>
        public string Company { get; set; }
        /// <summary>联系人姓�?/summary>
        public string Name { get; set; }
        /// <summary>产品类型：Personal / Enterprise（可选，优先使用用户的LicenseType�?/summary>
        public string ProductType { get; set; }
        /// <summary>授权到期时间</summary>
        public DateTime? ExpirationDate { get; set; }
        /// <summary>更新服务到期时间</summary>
        public DateTime? UpdateExpirationDate { get; set; }
        /// <summary>备注</summary>
        public string Remark { get; set; }
        /// <summary>License服务器的 sys_user 账号</summary>
        public string Account { get; set; }
        /// <summary>License服务器的 sys_user 密码</summary>
        public string Password { get; set; }
        /// <summary>验证码ID</summary>
        public string CaptchaId { get; set; }
        /// <summary>验证码�?/summary>
        public string CaptchaValue { get; set; }
    }

    /// <summary>
    /// License签发请求参数（管理员操作�?
    /// </summary>
    public class LicenseIssueRequest
    {
        /// <summary>联系电话</summary>
        public string Phone { get; set; }
        /// <summary>服务器IP</summary>
        public string IP { get; set; }
        /// <summary>客户的硬件指纹ID</summary>
        public string HID { get; set; }
        /// <summary>授权公司名称</summary>
        public string Company { get; set; }
        /// <summary>授权人姓名（可选，默认同Company�?/summary>
        public string Name { get; set; }
        /// <summary>产品类型：Personal / Enterprise</summary>
        public string ProductType { get; set; }
        /// <summary>授权到期时间（默认一年后�?/summary>
        public DateTime? ExpirationDate { get; set; }
        /// <summary>更新服务到期时间（默认同ExpirationDate�?/summary>
        public DateTime? UpdateExpirationDate { get; set; }
    }

    /// <summary>
    /// License查询请求参数
    /// </summary>
    public class LicenseCheckRequest
    {
        /// <summary>硬件指纹ID</summary>
        public string HID { get; set; }
    }

    /// <summary>
    /// 写入License文件请求参数
    /// </summary>
    public class WriteLicenseFileRequest
    {
        /// <summary>License文件内容（JSON字符串）</summary>
        public string LicenseContent { get; set; }
    }

    /// <summary>
    /// License作废/恢复请求参数
    /// </summary>
    public class LicenseRevokeRequest
    {
        /// <summary>硬件指纹ID</summary>
        public string HID { get; set; }
        /// <summary>true=作废, false=恢复</summary>
        public bool Revoke { get; set; } = true;
    }

    /// <summary>
    /// License驳回请求参数
    /// </summary>
    public class LicenseRejectRequest
    {
        /// <summary>硬件指纹ID</summary>
        public string HID { get; set; }
        /// <summary>驳回原因</summary>
        public string RejectReason { get; set; }
    }
}
