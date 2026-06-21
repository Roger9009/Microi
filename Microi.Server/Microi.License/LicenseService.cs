using Dos.Common;
using Dos.ORM;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Microi.License
{
    /// <summary>
    /// License 授权核心服务（静态类）
    ///
    /// 密钥配置说明：
    ///   私钥（仅License服务器需要）：
    ///     环境变量 MICROI_LICENSE_PRIVATE_KEY = PKCS#8 DER 的 Base64 编码
    ///     或将 PEM 文件放置于 AppBaseDir/license-private.pem
    ///   公钥（所有服务器需要）：
    ///     环境变量 MICROI_LICENSE_PUBLIC_KEY = SubjectPublicKeyInfo DER 的 Base64 编码
    ///     或将 PEM 文件放置于 AppBaseDir/license-public.pem
    ///     或替换 LicenseService.cs 中的 DefaultPublicKeyBase64 常量
    ///
    /// 初始化密钥对：
    ///   调用 LicenseService.GenerateKeyPair() 获取新的 RSA 2048 密钥对
    ///   将输出的 PublicKeyBase64 替换到 DefaultPublicKeyBase64 常量中
    ///   将 PrivateKeyBase64 设置为环境变量 MICROI_LICENSE_PRIVATE_KEY（仅 License 服务器）
    /// </summary>
    public static class LicenseService
    {
        private const string DefaultPublicKeyBase64 = "REPLACE_WITH_YOUR_RSA2048_PUBLIC_KEY_BASE64";
        private const string LicenseFileName = "license.json";
        private const string PrivateKeyFileName = "license-private.pem";
        private const string PublicKeyFileName = "license-public.pem";

        private static volatile bool _isGracePeriod = false;
        private static LicenseVerifyResult _cachedVerify = null;
        private static readonly object _verifyLock = new object();

        private const int GracePeriodDays = 7;
        private const string GraceFileName = ".lic_grace";
        private const string GraceDbKey = "__LICENSE_GRACE_START__";
        private const string ValidProofDbKey = "__LICENSE_VALID_PROOF__";

        public const int HeartbeatIntervalHours = 12;
        public const int OfflineGraceDays = 30;
        private const string HeartbeatFileName = ".lic_hb";
        private const string DefaultHeartbeatUrl = "https://api.itdos.com/api/License/Heartbeat";
        private const string DefaultContactEmail = "license@microi.net";
        public const string EncPrefix = "MILIC_ENC:";
        private const string LicenseLogTable = "diy_license_log";

        // ── License 操作日志动作常量 ──
        public static class LogAction
        {
            public const string Apply   = "Apply";
            public const string Issue   = "Issue";
            public const string Approve = "Approve";
            public const string Reject  = "Reject";
            public const string Revoke  = "Revoke";
            public const string Restore = "Restore";
            public const string Deploy  = "Deploy";
            public const string Import  = "ImportReg";
        }

        private static volatile bool _revokedByServer = false;
        private static readonly System.Net.Http.HttpClient _heartbeatHttp = new System.Net.Http.HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

        private static string HeartbeatServerUrl =>
            ConfigHelper.GetAppSettings("LicenseHeartbeatUrl").DosIsNullOrWhiteSpace()
                ? DefaultHeartbeatUrl : ConfigHelper.GetAppSettings("LicenseHeartbeatUrl");

        public static string ContactEmail =>
            ConfigHelper.GetAppSettings("LicenseContactEmail").DosIsNullOrWhiteSpace()
                ? DefaultContactEmail : ConfigHelper.GetAppSettings("LicenseContactEmail");

        public static bool IsRevokedByServer => _revokedByServer;
        public static bool IsGracePeriod => _isGracePeriod;

        // ======================== 公共 API ========================

        public static string GetHardwareId() => HardwareHelper.GetHardwareId();

        public static bool IsOpenSourceMode()
        {
            var pk = GetPublicKey();
            return pk == DefaultPublicKeyBase64 || pk == "REPLACE_WITH_YOUR_RSA2048_PUBLIC_KEY_BASE64";
        }

        public static LicenseVerifyResult Verify()
        {
            lock (_verifyLock) { _cachedVerify = VerifyInternal(); return _cachedVerify; }
        }

        public static void SetGracePeriodMode(bool enabled) => _isGracePeriod = enabled;

        // ======================== 宽限期 ========================

        public static (bool Allowed, int DaysLeft) CheckGracePeriod()
        {
            var now = DateTime.UtcNow;
            var gracePath = Path.Combine(AppContext.BaseDirectory, GraceFileName);
            var db = GetDb();

            if (db != null)
            {
                var proofExpiry = ReadValidProof();
                if (proofExpiry == null)
                {
                    if (!File.Exists(gracePath))
                    {
                        try { File.WriteAllText(gracePath, now.ToString("O") + "|bootstrap"); } catch { }
                        Console.WriteLine($"Microi：【🆕License引导】首次部署，自动授予 {GracePeriodDays} 天初始宽限期。请尽快生成密钥对并自签发 License！");
                        return (true, GracePeriodDays);
                    }
                    return (false, 0);
                }
                if ((now - proofExpiry.Value).TotalDays > GracePeriodDays) return (false, 0);
            }
            else
            {
                if (!File.Exists(gracePath))
                {
                    try { File.WriteAllText(gracePath, now.ToString("O") + "|init"); } catch { }
                    return (true, GracePeriodDays);
                }
            }

            DateTime? fileDate = null, dbDate = null;
            try
            {
                if (File.Exists(gracePath))
                {
                    var parts = File.ReadAllText(gracePath).Trim().Split('|');
                    if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var fd)) fileDate = fd.ToUniversalTime();
                }
            }
            catch { }
            try
            {
                if (db != null)
                {
                    var row = db.FromSql("SELECT LicenseContent FROM diy_license WHERE HID=@p0 LIMIT 1")
                        .AddInParameter("@p0", GraceDbKey).First<dynamic>();
                    if (row != null)
                    {
                        var rawVal = ((JObject)JObject.FromObject(row))["LicenseContent"]?.ToString();
                        var val = AesDecrypt(rawVal, DeriveDbKey());
                        if (DateTime.TryParse(val, out var dd)) dbDate = dd.ToUniversalTime();
                    }
                }
            }
            catch { }

            DateTime firstSeen;
            if (fileDate == null && dbDate == null)
            {
                firstSeen = now;
                try { File.WriteAllText(gracePath, now.ToString("O") + "|init"); } catch { }
            }
            else
            {
                if (fileDate.HasValue && dbDate.HasValue)
                    firstSeen = fileDate.Value < dbDate.Value ? fileDate.Value : dbDate.Value;
                else firstSeen = fileDate ?? dbDate ?? now;
                if (!File.Exists(gracePath))
                    try { File.WriteAllText(gracePath, firstSeen.ToString("O") + "|restored"); } catch { }
            }

            var daysLeft = GracePeriodDays - (int)(now - firstSeen).TotalDays;
            return (daysLeft > 0, Math.Max(0, daysLeft));
        }

        public static void PersistGracePeriodToDb()
        {
            try
            {
                var gracePath = Path.Combine(AppContext.BaseDirectory, GraceFileName);
                if (!File.Exists(gracePath)) return;
                var db = GetDb(); if (db == null) return;
                var existing = db.FromSql("SELECT Id FROM diy_license WHERE HID=@p0")
                    .AddInParameter("@p0", GraceDbKey).First<dynamic>();
                if (existing != null) return;

                var graceStart = DateTime.UtcNow.ToString("O");
                if (File.Exists(gracePath))
                {
                    var parts = File.ReadAllText(gracePath).Trim().Split('|');
                    if (parts.Length >= 1) graceStart = parts[0];
                }
                var val = AesEncrypt(graceStart, DeriveDbKey());
                db.FromSql(@"INSERT INTO diy_license (Id, HID, Status, LicenseContent, CreateTime)
                    VALUES (@p0, @p1, @p2, @p3, @p4)")
                    .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant())
                    .AddInParameter("@p1", GraceDbKey).AddInParameter("@p2", "Grace")
                    .AddInParameter("@p3", val).AddInParameter("@p4", DateTime.Now).ExecuteNonQuery();
                Console.WriteLine($"Microi：【License】宽限期起始时间已同步至数据库（{graceStart}）");
            }
            catch (Exception ex) { Console.WriteLine($"Microi：【License】宽限期DB同步失败（不影响运行）：{ex.Message}"); }
        }

        // ======================== 历史有效证明 ========================

        public static void WriteValidProof(DateTime expirationDate)
        {
            try
            {
                var db = GetDb(); if (db == null) return;
                var hid = GetHardwareId();
                var expStr = expirationDate.ToUniversalTime().ToString("O");
                var hmac = ComputeProofHmac(hid, expStr);
                var proofValue = AesEncrypt($"{expStr}|{hmac}", DeriveDbKey());
                var existing = db.FromSql("SELECT Id FROM diy_license WHERE HID=@p0")
                    .AddInParameter("@p0", ValidProofDbKey).First<dynamic>();
                if (existing != null)
                    db.FromSql("UPDATE diy_license SET LicenseContent=@p1, UpdateTime=@p2 WHERE HID=@p0")
                        .AddInParameter("@p0", ValidProofDbKey).AddInParameter("@p1", proofValue)
                        .AddInParameter("@p2", DateTime.Now).ExecuteNonQuery();
                else
                    db.FromSql(@"INSERT INTO diy_license (Id, HID, Status, LicenseContent, CreateTime)
                        VALUES (@p0, @p1, @p2, @p3, @p4)")
                        .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant())
                        .AddInParameter("@p1", ValidProofDbKey).AddInParameter("@p2", "ValidProof")
                        .AddInParameter("@p3", proofValue).AddInParameter("@p4", DateTime.Now).ExecuteNonQuery();
            }
            catch (Exception ex) { Console.WriteLine($"Microi：【License】有效证明写入失败（不影响运行）：{ex.Message}"); }
        }

        // ======================== 诊断 & 写入 ========================

        public static object GetDiagnostics()
        {
            var vr = Verify(); var hw = HardwareHelper.GetDiagnosticInfo();
            var hasPrv = !string.IsNullOrWhiteSpace(GetPrivateKey());
            var pk = GetPublicKey(); var isDefault = pk == DefaultPublicKeyBase64;
            return new
            {
                Hardware = hw, License = vr,
                Server = new { HasPrivateKey = hasPrv, IsLicenseServer = hasPrv,
                    PublicKeyConfigured = !isDefault, PublicKeySource = GetPublicKeySource(),
                    LicenseFilePath = GetLicensePath(), LicenseFileExists = File.Exists(GetLicensePath()) }
            };
        }

        public static DosResult WriteLicenseFile(string licenseContent)
        {
            if (string.IsNullOrWhiteSpace(licenseContent)) return new DosResult(0, null, "License内容不能为空");
            try
            {
                var hid = GetHardwareId();
                var plainJson = IsEncrypted(licenseContent) ? AesDecrypt(licenseContent, DeriveFileKey(hid)) : licenseContent;
                if (plainJson == null) return new DosResult(0, null, "License解密失败");

                var payload = JsonConvert.DeserializeObject<LicensePayload>(plainJson);
                if (payload == null) return new DosResult(0, null, "License格式无效");
                if (!VerifySignature(payload)) return new DosResult(0, null, "License签名验证失败");
                if (!string.Equals(payload.HID, hid, StringComparison.OrdinalIgnoreCase))
                    return new DosResult(0, null, $"License与当前服务器硬件不匹配（当前HID：{hid}）");
                if (payload.ExpirationDate < DateTime.UtcNow)
                    return new DosResult(0, null, $"License已于 {payload.ExpirationDate:yyyy-MM-dd} 到期");

                var path = GetLicensePath();
                var toWrite = IsEncrypted(licenseContent) ? licenseContent : AesEncrypt(plainJson, DeriveFileKey(hid));
                File.WriteAllText(path, toWrite, Encoding.UTF8);
                lock (_verifyLock) { _cachedVerify = null; }
                _revokedByServer = false;
                try { var hbPath = Path.Combine(AppContext.BaseDirectory, HeartbeatFileName);
                    if (File.Exists(hbPath)) File.WriteAllText(hbPath, $"{DateTime.UtcNow:O}|Issued"); } catch { }
                return new DosResult(1, new { Path = path }, "License文件写入成功（AES加密存储）");
            }
            catch (Exception ex) { return new DosResult(0, null, "写入失败：" + ex.Message); }
        }

        // ======================== 功能门控 ========================

        public static class Features
        {
            public const string AiPlugin = "AiPlugin";
            public const string MultiTenant = "MultiTenant";
            public const string AdvancedReport = "AdvancedReport";
            public const string CustomDomain = "CustomDomain";
            public const string LicenseAdmin = "LicenseAdmin";
        }

        public static bool IsFeatureAllowed(string feature)
        {
            var r = _cachedVerify ?? Verify();
            if (!r.Valid) return false;
            return feature switch
            {
                Features.AiPlugin => r.ProductType == LicenseProductType.Enterprise,
                Features.MultiTenant => r.ProductType == LicenseProductType.Enterprise,
                Features.AdvancedReport => r.Valid,
                Features.CustomDomain => r.Valid,
                Features.LicenseAdmin => r.Valid,
                _ => false
            };
        }

        // ======================== 心跳 ========================

        public static void LoadHeartbeatStatus()
        {
            var hp = Path.Combine(AppContext.BaseDirectory, HeartbeatFileName);
            if (!File.Exists(hp)) return;
            try
            {
                var parts = File.ReadAllText(hp).Trim().Split('|');
                if (parts.Length >= 2 && parts[1].Trim() == LicenseStatus.Revoked)
                {
                    _revokedByServer = true;
                    Console.WriteLine("Microi：【⚠️License】检测到上次心跳记录服务端已吊销该License");
                }
            }
            catch { }
        }

        public static async Task<string> HeartbeatAsync()
        {
            try
            {
                var hid = GetHardwareId();
                var r = _cachedVerify ?? Verify();
                if (!r.Valid) return "skip:unlicensed";

                var hp = Path.Combine(AppContext.BaseDirectory, HeartbeatFileName);
                var json = JsonConvert.SerializeObject(new
                {
                    HID = hid, LicenseHash = HardwareHelper.Sha256Hex(r.ExpirationDate?.ToString("O") + hid),
                    LocalTime = DateTime.UtcNow.ToString("O"), ProductType = r.ProductType
                });
                var resp = await _heartbeatHttp.PostAsync(HeartbeatServerUrl,
                    new System.Net.Http.StringContent(json, Encoding.UTF8, "application/json"));
                if (resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    var obj = JsonConvert.DeserializeObject<dynamic>(body);
                    var status = obj?.Status?.ToString();
                    File.WriteAllText(hp, $"{DateTime.UtcNow:O}|{status}");
                    if (status == "Revoked") { _revokedByServer = true; return "revoked"; }
                    _revokedByServer = false; return "ok:" + status;
                }
                return "server_error:" + resp.StatusCode;
            }
            catch (Exception ex) { return "offline:" + ex.Message[..Math.Min(50, ex.Message.Length)]; }
        }

        public static (bool OverOfflineLimit, int OfflineDays) CheckOfflineDays()
        {
            var hp = Path.Combine(AppContext.BaseDirectory, HeartbeatFileName);
            if (!File.Exists(hp)) return (false, 0);
            try
            {
                var line = File.ReadAllText(hp).Split('|')[0];
                if (!DateTime.TryParse(line, out var lastHb)) return (false, 0);
                var days = (int)(DateTime.UtcNow - lastHb.ToUniversalTime()).TotalDays;
                return (days > OfflineGraceDays, days);
            }
            catch { return (false, 0); }
        }

        // ======================== 离线申请 ========================

        public static DosResult GenerateRegistrationPackage(string company, string name, string phone,
            string ip, string productType, string remark)
        {
            try
            {
                var hid = GetHardwareId(); var rt = DateTime.UtcNow.ToString("O");
                var pt = string.IsNullOrWhiteSpace(productType) ? LicenseProductType.Personal : productType;
                var canonical = $"{hid}|{company}|{name}|{phone}|{pt}|{rt}";
                var rh = HardwareHelper.Sha256Hex(canonical);
                var pkg = JsonConvert.SerializeObject(new { Version = "1.0", HID = hid,
                    Company = company ?? "", Name = name ?? "", Phone = phone ?? "", IP = ip ?? "",
                    ProductType = pt, Remark = remark ?? "", RequestTime = rt, RequestHash = rh }, Formatting.Indented);
                return new DosResult(1, new { HID = hid, EncryptedContent = AesEncrypt(pkg, DeriveFileKey(hid)),
                    FileName = "microi-registration.milic", ContactEmail = ContactEmail }, "注册文件生成成功");
            }
            catch (Exception ex) { return new DosResult(0, null, "生成注册文件失败：" + ex.Message); }
        }

        public static Task<DosResult> ImportRegistrationFile(string fileContent)
        {
            if (string.IsNullOrWhiteSpace(fileContent)) return Task.FromResult(new DosResult(0, null, "注册文件内容不能为空"));
            try
            {
                JObject pkg; var hid = GetHardwareId();
                try { pkg = JObject.Parse(fileContent); }
                catch { return Task.FromResult(new DosResult(0, null, "注册文件格式无效")); }
                if (pkg["Version"]?.ToString() != "1.0")
                {
                    var dec = AesDecrypt(fileContent, DeriveFileKey(hid));
                    if (dec != null) { try { pkg = JObject.Parse(dec); } catch { return Task.FromResult(new DosResult(0, null, "注册文件解密后格式无效")); } }
                }
                var company = pkg["Company"]?.ToString() ?? "";
                var name = pkg["Name"]?.ToString() ?? "";
                var phone = pkg["Phone"]?.ToString() ?? "";
                var ip = pkg["IP"]?.ToString() ?? "";
                var pt = pkg["ProductType"]?.ToString() ?? LicenseProductType.Personal;
                var remark = pkg["Remark"]?.ToString() ?? "";
                var rh = pkg["RequestHash"]?.ToString() ?? "";
                var rt = pkg["RequestTime"]?.ToString() ?? "";
                var canonical = $"{hid}|{company}|{name}|{phone}|{pt}|{rt}";
                if (!string.Equals(rh, HardwareHelper.Sha256Hex(canonical), StringComparison.OrdinalIgnoreCase))
                    return Task.FromResult(new DosResult(0, null, "注册文件完整性验证失败"));

                var db = GetDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var existing = db.FromSql("SELECT * FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                if (existing != null)
                {
                    var obj = JObject.FromObject(existing); var st = obj["Status"]?.ToString();
                    if (st == LicenseStatus.Issued) return Task.FromResult(new DosResult(0, null, "该HID已签发License"));
                    if (st == LicenseStatus.Pending) return Task.FromResult(new DosResult(2, new { Status = LicenseStatus.Pending }, "已提交申请"));
                    db.FromSql(@"UPDATE diy_license SET Company=@p1, Name=@p2, Phone=@p3, IP=@p4,
                        ProductType=@p5, Status=@p6, RejectReason=NULL, Remark=@p7, UpdateTime=@p8 WHERE HID=@p0")
                        .AddInParameter("@p0", hid).AddInParameter("@p1", company).AddInParameter("@p2", name)
                        .AddInParameter("@p3", phone).AddInParameter("@p4", ip).AddInParameter("@p5", pt)
                        .AddInParameter("@p6", LicenseStatus.Pending).AddInParameter("@p7", remark)
                        .AddInParameter("@p8", DateTime.Now).ExecuteNonQuery();
                    return Task.FromResult(new DosResult(1, new { HID = hid, Status = LicenseStatus.Pending }, "注册文件已导入（更新）"));
                }
                db.FromSql(@"INSERT INTO diy_license (Id,HID,Company,Name,Phone,IP,ProductType,Status,Remark,CreateTime)
                    VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9)")
                    .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant()).AddInParameter("@p1", hid)
                    .AddInParameter("@p2", company).AddInParameter("@p3", name).AddInParameter("@p4", phone)
                    .AddInParameter("@p5", ip).AddInParameter("@p6", pt).AddInParameter("@p7", LicenseStatus.Pending)
                    .AddInParameter("@p8", remark).AddInParameter("@p9", DateTime.Now).ExecuteNonQuery();
                return Task.FromResult(new DosResult(1, new { HID = hid, Status = LicenseStatus.Pending }, "注册文件已导入（新增）"));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "导入失败：" + ex.Message)); }
        }

        public static (string PublicKeyBase64, string PrivateKeyBase64, string PublicKeyPem, string PrivateKeyPem) GenerateKeyPair()
        {
            using var rsa = RSA.Create(2048);
            var pb = Convert.ToBase64String(rsa.ExportSubjectPublicKeyInfo());
            var pv = Convert.ToBase64String(rsa.ExportPkcs8PrivateKey());
            return (pb, pv, $"-----BEGIN PUBLIC KEY-----\n{WrapBase64(pb)}\n-----END PUBLIC KEY-----",
                $"-----BEGIN PRIVATE KEY-----\n{WrapBase64(pv)}\n-----END PRIVATE KEY-----");
        }

        // ======================== License 服务器 API ========================

        public static Task<DosResult> ApplyAsync(string hid, string company, string name, string phone,
            string ip, string productType, DateTime? expirationDate, DateTime? updateExpirationDate,
            string remark, string account, string password, string operatorName = "", string operatorIP = "")
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var db = GetLicenseDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var existing = db.FromSql("SELECT * FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                if (existing != null)
                {
                    var obj = JObject.FromObject(existing); var st = obj["Status"]?.ToString();
                    if (st == LicenseStatus.Issued) return Task.FromResult(new DosResult(0, null, "该HID已签发License"));
                    if (st == LicenseStatus.Pending) return Task.FromResult(new DosResult(2, new { Status = LicenseStatus.Pending }, "已提交申请"));
                    db.FromSql(@"UPDATE diy_license SET Company=@p1, Name=@p2, Phone=@p3, IP=@p4,
                        ProductType=@p5, Status=@p6, RejectReason=NULL, Remark=@p7, UpdateTime=@p8 WHERE HID=@p0")
                        .AddInParameter("@p0", hid).AddInParameter("@p1", company ?? "").AddInParameter("@p2", name ?? "")
                        .AddInParameter("@p3", phone ?? "").AddInParameter("@p4", ip ?? "")
                        .AddInParameter("@p5", productType ?? LicenseProductType.Personal)
                        .AddInParameter("@p6", LicenseStatus.Pending).AddInParameter("@p7", remark ?? "")
                        .AddInParameter("@p8", DateTime.Now).ExecuteNonQuery();
                    WriteLicenseLog(hid, LogAction.Apply, operatorName, operatorIP, $"重新申请 | {company}");
                    return Task.FromResult(new DosResult(1, new { Status = LicenseStatus.Pending }, "申请已重新提交"));
                }
                var count = db.FromSql(@"INSERT INTO diy_license (Id, HID, Company, Name, Phone, IP, ProductType, Status, Remark, CreateTime)
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8, @p9)")
                    .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant()).AddInParameter("@p1", hid)
                    .AddInParameter("@p2", company ?? "").AddInParameter("@p3", name ?? "").AddInParameter("@p4", phone ?? "")
                    .AddInParameter("@p5", ip ?? "").AddInParameter("@p6", productType ?? LicenseProductType.Personal)
                    .AddInParameter("@p7", LicenseStatus.Pending).AddInParameter("@p8", remark ?? "")
                    .AddInParameter("@p9", DateTime.Now).ExecuteNonQuery();
                if (count <= 0) return Task.FromResult(new DosResult(0, null, "申请记录保存失败"));
                WriteLicenseLog(hid, LogAction.Apply, operatorName, operatorIP, $"新申请 | {company}");
                Console.WriteLine($"Microi.License：【申请】HID={hid}");
                return Task.FromResult(new DosResult(1, new { Status = LicenseStatus.Pending }, "申请已提交"));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "申请失败：" + ex.Message)); }
        }

        public static Task<DosResult> IssueAsync(string hid, string company, string name, string phone,
            string ip, string productType, DateTime? expirationDate, DateTime? updateExpirationDate,
            string operatorName = "", string operatorIP = "")
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            var pk = GetPrivateKey(); if (string.IsNullOrWhiteSpace(pk)) return Task.FromResult(new DosResult(0, null, "未配置私钥"));
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var expiry = expirationDate ?? DateTime.UtcNow.AddYears(1);
                var upExpiry = updateExpirationDate ?? expiry;
                var payload = new LicensePayload { HID = hid, Company = company ?? "", Name = name ?? "",
                    Phone = phone ?? "", IP = ip ?? "", ProductType = productType ?? LicenseProductType.Personal,
                    IssuedAt = DateTime.UtcNow, ExpirationDate = expiry, UpdateExpirationDate = upExpiry };
                payload.Signature = SignPayload(payload, pk);
                if (string.IsNullOrWhiteSpace(payload.Signature)) return Task.FromResult(new DosResult(0, null, "签名失败"));
                var lc = AesEncrypt(JsonConvert.SerializeObject(payload, Formatting.Indented), DeriveFileKey(hid));
                var db = GetLicenseDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var existing = db.FromSql("SELECT Id FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                if (existing != null)
                    db.FromSql(@"UPDATE diy_license SET Company=@p1, Name=@p2, Phone=@p3, IP=@p4, ProductType=@p5,
                        Status=@p6, LicenseContent=@p7, IssuedAt=@p8, ExpirationDate=@p9, UpdateExpirationDate=@p10, UpdateTime=@p11
                        WHERE HID=@p0").AddInParameter("@p0", hid).AddInParameter("@p1", company ?? "")
                        .AddInParameter("@p2", name ?? "").AddInParameter("@p3", phone ?? "").AddInParameter("@p4", ip ?? "")
                        .AddInParameter("@p5", productType ?? LicenseProductType.Personal).AddInParameter("@p6", LicenseStatus.Issued)
                        .AddInParameter("@p7", lc).AddInParameter("@p8", payload.IssuedAt).AddInParameter("@p9", expiry)
                        .AddInParameter("@p10", upExpiry).AddInParameter("@p11", DateTime.Now).ExecuteNonQuery();
                else
                    db.FromSql(@"INSERT INTO diy_license (Id, HID, Company, Name, Phone, IP, ProductType, Status,
                        LicenseContent, IssuedAt, ExpirationDate, UpdateExpirationDate, CreateTime)
                        VALUES (@p0,@p1,@p2,@p3,@p4,@p5,@p6,@p7,@p8,@p9,@p10,@p11,@p12)")
                        .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant()).AddInParameter("@p1", hid)
                        .AddInParameter("@p2", company ?? "").AddInParameter("@p3", name ?? "").AddInParameter("@p4", phone ?? "")
                        .AddInParameter("@p5", ip ?? "").AddInParameter("@p6", productType ?? LicenseProductType.Personal)
                        .AddInParameter("@p7", LicenseStatus.Issued).AddInParameter("@p8", lc)
                        .AddInParameter("@p9", payload.IssuedAt).AddInParameter("@p10", expiry)
                        .AddInParameter("@p11", upExpiry).AddInParameter("@p12", DateTime.Now).ExecuteNonQuery();
                WriteLicenseLog(hid, LogAction.Issue, operatorName, operatorIP, $"签发 | {company} | {productType} | 到期:{expiry:yyyy-MM-dd}");
                Console.WriteLine($"Microi.License：【签发】HID={hid} Expiry={expiry:yyyy-MM-dd}");
                return Task.FromResult(new DosResult(1, new { LicenseContent = lc }, "License签发成功（AES加密）"));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "签发失败：" + ex.Message)); }
        }

        public static Task<DosResult> CheckAsync(string hid)
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var db = GetDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var row = db.FromSql("SELECT * FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                if (row == null) return Task.FromResult(new DosResult(2, null, "未找到该HID的申请记录"));
                var obj = JObject.FromObject(row);
                string lc = null;
                if (obj["Status"]?.ToString() == LicenseStatus.Issued) lc = obj["LicenseContent"]?.ToString();
                return Task.FromResult(new DosResult(1, new { HID = hid, Status = obj["Status"]?.ToString(),
                    Company = obj["Company"]?.ToString(), ProductType = obj["ProductType"]?.ToString(),
                    IssuedAt = obj["IssuedAt"]?.ToString(), ExpirationDate = obj["ExpirationDate"]?.ToString(),
                    UpdateExpirationDate = obj["UpdateExpirationDate"]?.ToString(),
                    RejectReason = obj["RejectReason"]?.ToString(), LicenseContent = lc }));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "查询失败：" + ex.Message)); }
        }

        public static Task<DosResult> QueryApplicationAsync(string hid)
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var db = GetDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var row = db.FromSql(@"SELECT Id, HID, Company, Name, Phone, IP, ProductType,
                    Status, IssuedAt, ExpirationDate, UpdateExpirationDate, RejectReason, CreateTime, UpdateTime
                    FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                return row == null ? Task.FromResult(new DosResult(2, null, "未找到申请记录")) : Task.FromResult(new DosResult(1, row));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "查询失败：" + ex.Message)); }
        }

        public static async Task<DosResult> ApproveAsync(string hid, string operatorName = "", string operatorIP = "")
        {
            if (string.IsNullOrWhiteSpace(hid)) return new DosResult(0, null, "HID不能为空");
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var db = GetLicenseDb(); if (db == null) return new DosResult(0, null, "数据库未就绪");
                var row = db.FromSql("SELECT * FROM diy_license WHERE HID=@p0").AddInParameter("@p0", hid).First<dynamic>();
                if (row == null) return new DosResult(0, null, "未找到该HID的申请记录");
                var obj = JObject.FromObject(row);
                if (obj["Status"]?.ToString() == LicenseStatus.Issued) return new DosResult(0, null, "已签发");
                var result = await IssueAsync(hid, obj["Company"]?.ToString(), obj["Name"]?.ToString(), obj["Phone"]?.ToString(),
                    obj["IP"]?.ToString(), obj["ProductType"]?.ToString(), obj["ExpirationDate"]?.Value<DateTime?>(),
                    obj["UpdateExpirationDate"]?.Value<DateTime?>(), operatorName, operatorIP);
                if (result.Code == 1)
                    WriteLicenseLog(hid, LogAction.Approve, operatorName, operatorIP, $"审核通过并签发 | {obj["Company"]}");
                return result;
            }
            catch (Exception ex) { return new DosResult(0, null, "审核失败：" + ex.Message); }
        }

        public static Task<DosResult> RejectAsync(string hid, string rejectReason, string operatorName = "", string operatorIP = "")
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            hid = hid.Trim().ToUpperInvariant();
            try
            {
                var db = GetLicenseDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var count = db.FromSql(@"UPDATE diy_license SET Status=@p1, RejectReason=@p2, UpdateTime=@p3
                    WHERE HID=@p0 AND Status=@p4").AddInParameter("@p0", hid)
                    .AddInParameter("@p1", LicenseStatus.Rejected).AddInParameter("@p2", rejectReason ?? "")
                    .AddInParameter("@p3", DateTime.Now).AddInParameter("@p4", LicenseStatus.Pending).ExecuteNonQuery();
                if (count <= 0) return Task.FromResult(new DosResult(0, null, "驳回失败"));
                WriteLicenseLog(hid, LogAction.Reject, operatorName, operatorIP, $"驳回 | 原因:{rejectReason}");
                Console.WriteLine($"Microi.License：【驳回】HID={hid}");
                return Task.FromResult(new DosResult(1, null, "已驳回"));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, "驳回失败：" + ex.Message)); }
        }

        public static Task<DosResult> RevokeAsync(string hid, bool revoke, string operatorName = "", string operatorIP = "")
        {
            if (string.IsNullOrWhiteSpace(hid)) return Task.FromResult(new DosResult(0, null, "HID不能为空"));
            hid = hid.Trim().ToUpperInvariant();
            var ns = revoke ? LicenseStatus.Revoked : LicenseStatus.Issued;
            var act = revoke ? "作废" : "恢复";
            var logAct = revoke ? LogAction.Revoke : LogAction.Restore;
            try
            {
                var db = GetLicenseDb(); if (db == null) return Task.FromResult(new DosResult(0, null, "数据库未就绪"));
                var count = db.FromSql("UPDATE diy_license SET Status=@p1, UpdateTime=@p2 WHERE HID=@p0")
                    .AddInParameter("@p0", hid).AddInParameter("@p1", ns).AddInParameter("@p2", DateTime.Now).ExecuteNonQuery();
                if (count <= 0) return Task.FromResult(new DosResult(0, null, $"{act}失败"));
                WriteLicenseLog(hid, logAct, operatorName, operatorIP, act);
                Console.WriteLine($"Microi.License：【{act}】HID={hid}");
                return Task.FromResult(new DosResult(1, null, $"License已{act}"));
            }
            catch (Exception ex) { return Task.FromResult(new DosResult(0, null, $"{act}失败：" + ex.Message)); }
        }

        // ======================== License 独立数据库 + 日志 ========================

        /// <summary>
        /// 获取 License 专用数据库会话。
        /// 优先级：环境变量 MICROI_LICENSE_DB_CONN → appsettings.LicenseDbConn → 主库 OsClient
        /// </summary>
        private static DbSession GetLicenseDb()
        {
            try
            {
                var connStr = Environment.GetEnvironmentVariable("MICROI_LICENSE_DB_CONN");
                if (string.IsNullOrWhiteSpace(connStr))
                    connStr = ConfigHelper.GetAppSettings("LicenseDbConn");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    // 从主库推断数据库类型（与主库一致）
                    var mainDb = GetDb();
                    if (mainDb != null)
                        return new DbSession(mainDb.Db.DbProvider.DatabaseType, connStr);
                }
                // 回退到主库
                return GetDb();
            }
            catch { return GetDb(); }
        }

        /// <summary>确保 diy_license_log 表存在（自动建表）</summary>
        private static void EnsureLogTable(DbSession db)
        {
            try
            {
                db.FromSql(@"CREATE TABLE IF NOT EXISTS diy_license_log (
                    Id          VARCHAR(32)  NOT NULL PRIMARY KEY,
                    HID         VARCHAR(64)  NOT NULL,
                    Action      VARCHAR(20)  NOT NULL,
                    Operator    VARCHAR(100) DEFAULT '',
                    OperatorIP  VARCHAR(50)  DEFAULT '',
                    Detail      VARCHAR(1000) DEFAULT '',
                    CreateTime  DATETIME     NOT NULL,
                    INDEX idx_log_hid (HID),
                    INDEX idx_log_time (CreateTime)
                )").ExecuteNonQuery();
            }
            catch { /* 表已存在或其他DDL错误，忽略 */ }
        }

        /// <summary>写入操作日志</summary>
        public static void WriteLicenseLog(string hid, string action, string operatorName = "", string operatorIP = "", string detail = "")
        {
            try
            {
                var db = GetLicenseDb(); if (db == null) return;
                EnsureLogTable(db);
                db.FromSql(@"INSERT INTO diy_license_log (Id, HID, Action, Operator, OperatorIP, Detail, CreateTime)
                    VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)")
                    .AddInParameter("@p0", Guid.NewGuid().ToString("N").ToUpperInvariant())
                    .AddInParameter("@p1", (hid ?? "").Trim().ToUpperInvariant())
                    .AddInParameter("@p2", action ?? "")
                    .AddInParameter("@p3", operatorName ?? "")
                    .AddInParameter("@p4", operatorIP ?? "")
                    .AddInParameter("@p5", detail != null && detail.Length > 1000 ? detail.Substring(0, 1000) : (detail ?? ""))
                    .AddInParameter("@p6", DateTime.Now)
                    .ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Microi：【License】日志写入失败（不影响业务）：{ex.Message}");
            }
        }

        /// <summary>管理员查询 License 列表</summary>
        public static DosResult GetLicenseList(string status = "", int page = 1, int pageSize = 20)
        {
            try
            {
                var db = GetLicenseDb(); if (db == null) return new DosResult(0, null, "数据库未就绪");
                var where = string.IsNullOrWhiteSpace(status) ? "1=1" : "Status=@status";
                var sql = $"SELECT * FROM diy_license WHERE {where} AND HID NOT LIKE '@__LICENSE_%' ORDER BY UpdateTime DESC, CreateTime DESC";
                var countSql = $"SELECT COUNT(*) FROM diy_license WHERE {where} AND HID NOT LIKE '@__LICENSE_%'";
                var cmd = db.FromSql(sql);
                var countCmd = db.FromSql(countSql);
                if (!string.IsNullOrWhiteSpace(status))
                {
                    cmd = cmd.AddInParameter("@status", status);
                    countCmd = countCmd.AddInParameter("@status", status);
                }
                var list = cmd.ToArray();
                var total = countCmd.ToScalar<int>();
                return new DosResult(1, new { List = list, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex) { return new DosResult(0, null, "查询失败：" + ex.Message); }
        }

        /// <summary>管理员查询操作日志</summary>
        public static DosResult GetLicenseLogs(string hid = "", int page = 1, int pageSize = 50)
        {
            try
            {
                var db = GetLicenseDb(); if (db == null) return new DosResult(0, null, "数据库未就绪");
                EnsureLogTable(db);
                var where = string.IsNullOrWhiteSpace(hid) ? "1=1" : "HID=@hid";
                var sql = $"SELECT * FROM diy_license_log WHERE {where} ORDER BY CreateTime DESC";
                var countSql = $"SELECT COUNT(*) FROM diy_license_log WHERE {where}";
                var cmd = db.FromSql(sql);
                var countCmd = db.FromSql(countSql);
                if (!string.IsNullOrWhiteSpace(hid))
                {
                    cmd = cmd.AddInParameter("@hid", hid.Trim().ToUpperInvariant());
                    countCmd = countCmd.AddInParameter("@hid", hid.Trim().ToUpperInvariant());
                }
                var list = cmd.ToArray();
                var total = countCmd.ToScalar<int>();
                return new DosResult(1, new { List = list, Total = total, Page = page, PageSize = pageSize });
            }
            catch (Exception ex) { return new DosResult(0, null, "查询失败：" + ex.Message); }
        }

        // ======================== 内部实现 ========================

        private static LicenseVerifyResult VerifyInternal()
        {
            var path = GetLicensePath();
            if (!File.Exists(path)) return new LicenseVerifyResult { Valid = false, IsGracePeriod = _isGracePeriod, Message = "License文件不存在" };
            try
            {
                var raw = File.ReadAllText(path, Encoding.UTF8);
                var content = IsEncrypted(raw) ? AesDecrypt(raw, DeriveFileKey(GetHardwareId())) : raw;
                if (content == null) return Fail("License文件解密失败");
                var payload = JsonConvert.DeserializeObject<LicensePayload>(content);
                if (payload == null) return Fail("License文件格式无效");
                var curHid = GetHardwareId();
                if (!string.Equals(payload.HID, curHid, StringComparison.OrdinalIgnoreCase))
                    return Fail($"License与当前服务器不匹配（当前HID={curHid}）");
                if (!VerifySignature(payload)) return Fail("License签名验证失败");
                var daysLeft = (int)(payload.ExpirationDate.ToUniversalTime() - DateTime.UtcNow).TotalDays;
                if (daysLeft < 0) return new LicenseVerifyResult { Valid = false, HID = payload.HID, Company = payload.Company,
                    ProductType = payload.ProductType, ExpirationDate = payload.ExpirationDate,
                    UpdateExpirationDate = payload.UpdateExpirationDate, DaysRemaining = daysLeft,
                    IsGracePeriod = _isGracePeriod, Message = $"License已于 {payload.ExpirationDate:yyyy-MM-dd} 到期" };
                return new LicenseVerifyResult { Valid = true, HID = payload.HID, Company = payload.Company,
                    ProductType = payload.ProductType, ExpirationDate = payload.ExpirationDate,
                    UpdateExpirationDate = payload.UpdateExpirationDate,
                    IssuedDate = payload.IssuedAt.ToString("yyyy-MM-dd HH:mm:ss"),
                    DaysRemaining = daysLeft, IsGracePeriod = false, Message = $"License有效，剩余 {daysLeft} 天" };
            }
            catch (Exception ex) { return Fail("License验证异常：" + ex.Message); }
        }

        private static LicenseVerifyResult Fail(string msg) =>
            new LicenseVerifyResult { Valid = false, IsGracePeriod = _isGracePeriod, Message = msg };

        private static string SignPayload(LicensePayload payload, string privateKeyBase64)
        {
            try
            {
                var data = Encoding.UTF8.GetBytes(GetCanonicalPayload(payload));
                using var rsa = RSA.Create();
                rsa.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKeyBase64.Trim()), out _);
                return Convert.ToBase64String(rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
            }
            catch (Exception ex) { Console.WriteLine($"Microi.License：【签名失败】{ex.Message}"); return null; }
        }

        private static bool VerifySignature(LicensePayload payload)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(payload?.Signature)) return false;
                var pk = GetPublicKey();
                if (string.IsNullOrWhiteSpace(pk) || pk == DefaultPublicKeyBase64) return false;
                var data = Encoding.UTF8.GetBytes(GetCanonicalPayload(payload));
                using var rsa = RSA.Create();
                rsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(pk.Trim()), out _);
                return rsa.VerifyData(data, Convert.FromBase64String(payload.Signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch { return false; }
        }

        private static string GetCanonicalPayload(LicensePayload payload) =>
            JsonConvert.SerializeObject(new { payload.Company, payload.ExpirationDate, payload.HID, payload.IP,
                payload.IssuedAt, payload.Name, payload.Phone, payload.ProductType, payload.UpdateExpirationDate },
                new JsonSerializerSettings { DateFormatString = "yyyy-MM-ddTHH:mm:ssZ" });

        public static bool IsEncrypted(string content) => content?.StartsWith(EncPrefix, StringComparison.Ordinal) == true;

        private static byte[] DeriveFileKey(string hid)
        {
            using var derive = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(hid + "microi-lic-enc-2026"),
                Encoding.UTF8.GetBytes("MicroiLicSalt!!_"), 100_000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }

        private static byte[] DeriveDbKey()
        {
            var ek = Environment.GetEnvironmentVariable("MICROI_LICENSE_ENCRYPT_KEY") ?? "";
            using var derive = new Rfc2898DeriveBytes(
                Encoding.UTF8.GetBytes(string.IsNullOrWhiteSpace(ek) ? "microi-db-default-enc-2026" : ek),
                Encoding.UTF8.GetBytes("MicroiDbSalt!!__"), 100_000, HashAlgorithmName.SHA256);
            return derive.GetBytes(32);
        }

        private static string AesEncrypt(string plainText, byte[] key)
        {
            using var aes = Aes.Create(); aes.Key = key; aes.GenerateIV();
            using var enc = aes.CreateEncryptor();
            var data = Encoding.UTF8.GetBytes(plainText);
            var cipher = enc.TransformFinalBlock(data, 0, data.Length);
            var result = new byte[aes.IV.Length + cipher.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(cipher, 0, result, aes.IV.Length, cipher.Length);
            return EncPrefix + Convert.ToBase64String(result);
        }

        private static string AesDecrypt(string cipherWithPrefix, byte[] key)
        {
            if (string.IsNullOrWhiteSpace(cipherWithPrefix)) return cipherWithPrefix;
            if (!cipherWithPrefix.StartsWith(EncPrefix, StringComparison.Ordinal)) return cipherWithPrefix;
            try
            {
                var combined = Convert.FromBase64String(cipherWithPrefix[EncPrefix.Length..]);
                using var aes = Aes.Create(); aes.Key = key;
                var iv = new byte[aes.BlockSize / 8];
                var cipher = new byte[combined.Length - iv.Length];
                Buffer.BlockCopy(combined, 0, iv, 0, iv.Length);
                Buffer.BlockCopy(combined, iv.Length, cipher, 0, cipher.Length);
                aes.IV = iv;
                using var dec = aes.CreateDecryptor();
                return Encoding.UTF8.GetString(dec.TransformFinalBlock(cipher, 0, cipher.Length));
            }
            catch { return null; }
        }

        private static string ComputeProofHmac(string hid, string expStr)
        {
            var key = HardwareHelper.Sha256Hex(hid + "microi-valid-proof-2026");
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            return BitConverter.ToString(hmac.ComputeHash(Encoding.UTF8.GetBytes(expStr + hid))).Replace("-", "").ToLowerInvariant();
        }

        private static DateTime? ReadValidProof()
        {
            try
            {
                var db = GetDb(); if (db == null) return null;
                var row = db.FromSql("SELECT LicenseContent FROM diy_license WHERE HID=@p0")
                    .AddInParameter("@p0", ValidProofDbKey).First<dynamic>();
                if (row == null) return null;
                var rc = ((JObject)JObject.FromObject(row))["LicenseContent"]?.ToString();
                if (string.IsNullOrWhiteSpace(rc)) return null;
                var content = AesDecrypt(rc, DeriveDbKey());
                if (content == null) return null;
                var parts = content.Split('|');
                if (parts.Length < 2) return null;
                if (!string.Equals(parts[1], ComputeProofHmac(GetHardwareId(), parts[0]), StringComparison.OrdinalIgnoreCase))
                    return null;
                return DateTime.TryParse(parts[0], out var exp) ? exp.ToUniversalTime() : (DateTime?)null;
            }
            catch { return null; }
        }

        private static string GetLicensePath() => Path.Combine(AppContext.BaseDirectory, LicenseFileName);

        private static string GetPrivateKey()
        {
            var ek = Environment.GetEnvironmentVariable("MICROI_LICENSE_PRIVATE_KEY");
            if (!string.IsNullOrWhiteSpace(ek)) return ek.Trim();
            var fp = Path.Combine(AppContext.BaseDirectory, PrivateKeyFileName);
            return File.Exists(fp) ? ExtractBase64FromPem(File.ReadAllText(fp).Trim()) : null;
        }

        private static string GetPublicKey()
        {
            var ek = Environment.GetEnvironmentVariable("MICROI_LICENSE_PUBLIC_KEY");
            if (!string.IsNullOrWhiteSpace(ek)) return ek.Trim();
            var fp = Path.Combine(AppContext.BaseDirectory, PublicKeyFileName);
            return File.Exists(fp) ? ExtractBase64FromPem(File.ReadAllText(fp).Trim()) : DefaultPublicKeyBase64;
        }

        private static string GetPublicKeySource()
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("MICROI_LICENSE_PUBLIC_KEY")))
                return "环境变量 MICROI_LICENSE_PUBLIC_KEY";
            if (File.Exists(Path.Combine(AppContext.BaseDirectory, PublicKeyFileName))) return $"文件 {PublicKeyFileName}";
            return "内嵌常量 DefaultPublicKeyBase64（未替换=无法验证）";
        }

        private static string ExtractBase64FromPem(string pem) =>
            string.IsNullOrWhiteSpace(pem) ? null : pem
                .Replace("-----BEGIN PUBLIC KEY-----", "").Replace("-----END PUBLIC KEY-----", "")
                .Replace("-----BEGIN PRIVATE KEY-----", "").Replace("-----END PRIVATE KEY-----", "")
                .Replace("-----BEGIN RSA PRIVATE KEY-----", "").Replace("-----END RSA PRIVATE KEY-----", "")
                .Replace("\r", "").Replace("\n", "").Replace(" ", "").Trim();

        private static string WrapBase64(string b64, int ll = 64)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < b64.Length; i += ll) sb.AppendLine(b64.Substring(i, Math.Min(ll, b64.Length - i)));
            return sb.ToString().TrimEnd();
        }

        private static DbSession GetDb()
        {
            try { return Microi.net.OsClient.GetClient(Microi.net.OsClientDefault.OsClient)?.Db; }
            catch { return null; }
        }
    }
}
