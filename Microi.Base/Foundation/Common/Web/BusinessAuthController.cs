using System;
using System.Security.Cryptography;
using System.Text;
using Dos.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 业务底座独立登录鉴权控制器。
    /// 提供一个与主站账号体系隔离的超级管理员入口，专用于 business-schema / business-document 管理页面。
    ///
    /// 账号体系：
    ///   - 用户名固定为 bizadmin；
    ///   - 密码哈希存储在 Redis Hash：Microi:{osClient}:BizAdmin → PwdHash = SHA256(password)；
    ///   - 首次未设置密码时，默认密码为 Admin@123（登录后请立即修改）；
    ///   - 登录成功后颁发 Session Token（UUID），以 token 为 Hash Field 存入
    ///     Microi:{osClient}:BizAdmin → {token} = {Unix 秒级过期时间戳}（24h TTL）；
    ///   - 前端存入 localStorage.biz_admin_token。
    /// </summary>
    [AllowAnonymous]
    [EnableCors("any")]
    [Route("api/BusinessAuth/[action]")]
    public class BusinessAuthController : Controller
    {
        private const string DefaultPassword = "Admin@123";
        private const string AdminUsername = "bizadmin";
        private static readonly int TokenTtlSeconds = 86400; // 24h
        private const int MaxLoginAttempts = 5;
        private const int LockoutMinutes = 15;

        // ── Redis 键 ──────────────────────────────────────────────────────────

        private static string AdminHashKey(string osClient)
            => $"Microi:{osClient}:BizAdmin";

        private const string PwdHashField = "PwdHash";
        private const string LoginFailField = "LoginFailCount";
        private const string LockUntilField = "LockUntil";

        // ── 工具 ──────────────────────────────────────────────────────────────

        private static string Sha256(string text)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(text ?? ""));
            return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static dynamic GetCache(string osClient)
        {
            try { return MicroiEngine.CacheTenant?.Cache(osClient); } catch { return null; }
        }

        private static string CacheHashGet(dynamic cache, string hashKey, string field)
        {
            try { return cache?.HashGet(hashKey, field) as string; } catch { return null; }
        }

        private static void CacheHashSet(dynamic cache, string hashKey, string field, string value)
        {
            try { cache?.HashSet(hashKey, field, value); } catch { /* 缓存不可用时静默降级 */ }
        }

        private static void CacheHashDelete(dynamic cache, string hashKey, string field)
        {
            try { cache?.HashDelete(hashKey, field); } catch { }
        }

        private static long NowUnix() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        private static void RecordLoginFailure(dynamic cache, string hashKey)
        {
            try
            {
                var failStr = CacheHashGet(cache, hashKey, LoginFailField);
                var count = 0;
                int.TryParse(failStr, out count);
                count++;
                CacheHashSet(cache, hashKey, LoginFailField, count.ToString());
                if (count >= MaxLoginAttempts)
                {
                    var lockUntil = NowUnix() + LockoutMinutes * 60;
                    CacheHashSet(cache, hashKey, LockUntilField, lockUntil.ToString());
                }
            }
            catch { /* 缓存不可用时静默降级 */ }
        }

        private string OsClientFromHeader()
        {
            Request.Headers.TryGetValue("OsClient", out var v);
            return v.ToString();
        }

        // ── 对外接口 ──────────────────────────────────────────────────────────

        /// <summary>
        /// 登录。
        /// POST api/BusinessAuth/Login
        /// Body: { OsClient, Username, Password }
        /// 返回: { Code:1, Data:{ Token, OsClient } }
        /// </summary>
        [HttpPost]
        public IActionResult Login([FromBody] BizLoginParam param)
        {
            var osClient = param?.OsClient ?? OsClientFromHeader();
            if (string.IsNullOrWhiteSpace(osClient)) osClient = "";

            var cache = GetCache(osClient);
            var hashKey = AdminHashKey(osClient);

            // 检查是否被锁定
            var lockUntilStr = CacheHashGet(cache, hashKey, LockUntilField);
            if (!string.IsNullOrWhiteSpace(lockUntilStr))
            {
                if (long.TryParse(lockUntilStr, out long lockUntil) && NowUnix() < lockUntil)
                {
                    var remainMin = (lockUntil - NowUnix()) / 60 + 1;
                    return Json(new DosResult(0, null, $"账户已被锁定，请 {remainMin} 分钟后再试。"));
                }
                // 锁定已过期，清除锁定标记
                CacheHashDelete(cache, hashKey, LockUntilField);
                CacheHashDelete(cache, hashKey, LoginFailField);
            }

            if (!string.Equals(param?.Username, AdminUsername, StringComparison.OrdinalIgnoreCase))
            {
                RecordLoginFailure(cache, hashKey);
                return Json(new DosResult(0, null, "用户名或密码错误。"));
            }

            var storedHash = CacheHashGet(cache, hashKey, PwdHashField)
                             ?? Sha256(DefaultPassword);

            if (!string.Equals(storedHash, Sha256(param.Password), StringComparison.Ordinal))
            {
                RecordLoginFailure(cache, hashKey);
                return Json(new DosResult(0, null, "用户名或密码错误。"));
            }

            // 登录成功，清除失败计数
            CacheHashDelete(cache, hashKey, LoginFailField);
            CacheHashDelete(cache, hashKey, LockUntilField);

            var token = Guid.NewGuid().ToString("N");
            var expiry = (NowUnix() + TokenTtlSeconds).ToString();
            CacheHashSet(cache, hashKey, token, expiry);

            return Json(new DosResult(1, new { Token = token, OsClient = osClient }, "登录成功。"));
        }

        /// <summary>
        /// 验证 Token 是否有效。
        /// POST api/BusinessAuth/Verify
        /// Body: { OsClient, Token }
        /// </summary>
        [HttpPost]
        public IActionResult Verify([FromBody] BizTokenParam param)
        {
            var osClient = param?.OsClient ?? OsClientFromHeader() ?? "";
            if (string.IsNullOrWhiteSpace(param?.Token))
                return Json(new DosResult(0, null, "无效。"));

            var cache = GetCache(osClient);
            var expStr = CacheHashGet(cache, AdminHashKey(osClient), param.Token);
            if (expStr == null) return Json(new DosResult(0, null, "Token 已过期，请重新登录。"));

            long exp;
            if (long.TryParse(expStr, out exp) && NowUnix() > exp)
            {
                CacheHashDelete(cache, AdminHashKey(osClient), param.Token);
                return Json(new DosResult(0, null, "Token 已过期，请重新登录。"));
            }

            return Json(new DosResult(1, null, "有效。"));
        }

        /// <summary>
        /// 修改管理员密码。
        /// POST api/BusinessAuth/SetPassword
        /// Body: { OsClient, Token, OldPassword, NewPassword }
        /// </summary>
        [HttpPost]
        public IActionResult SetPassword([FromBody] BizSetPasswordParam param)
        {
            var osClient = param?.OsClient ?? OsClientFromHeader() ?? "";
            var cache = GetCache(osClient);

            var expStr = CacheHashGet(cache, AdminHashKey(osClient), param?.Token ?? "");
            long exp;
            if (expStr == null || (long.TryParse(expStr, out exp) && NowUnix() > exp))
                return Json(new DosResult(0, null, "请先登录。"));

            var storedHash = CacheHashGet(cache, AdminHashKey(osClient), PwdHashField)
                             ?? Sha256(DefaultPassword);
            if (!string.Equals(storedHash, Sha256(param.OldPassword), StringComparison.Ordinal))
                return Json(new DosResult(0, null, "旧密码错误。"));

            if (string.IsNullOrWhiteSpace(param.NewPassword) || param.NewPassword.Length < 6)
                return Json(new DosResult(0, null, "新密码不能少于 6 位。"));

            CacheHashSet(cache, AdminHashKey(osClient), PwdHashField, Sha256(param.NewPassword));
            return Json(new DosResult(1, null, "密码修改成功。"));
        }

        /// <summary>
        /// 退出登录（使 Token 立即失效）。
        /// POST api/BusinessAuth/Logout
        /// Body: { OsClient, Token }
        /// </summary>
        [HttpPost]
        public IActionResult Logout([FromBody] BizTokenParam param)
        {
            var osClient = param?.OsClient ?? OsClientFromHeader() ?? "";
            if (!string.IsNullOrWhiteSpace(param?.Token))
            {
                var cache = GetCache(osClient);
                CacheHashDelete(cache, AdminHashKey(osClient), param.Token);
            }
            return Json(new DosResult(1, null, "已退出。"));
        }
    }

    // ── 参数类 ────────────────────────────────────────────────────────────────

    public class BizLoginParam
    {
        public string OsClient { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
    }

    public class BizTokenParam
    {
        public string OsClient { get; set; }
        public string Token { get; set; }
    }

    public class BizSetPasswordParam
    {
        public string OsClient { get; set; }
        public string Token { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
    }
}
