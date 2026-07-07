extern alias MicroiLicense;
using Dos.Common;
using LicSvc = MicroiLicense::Microi.License.LicenseService;
using LicenseVerifyResult = MicroiLicense::Microi.License.LicenseVerifyResult;
using LicensePayload = MicroiLicense::Microi.License.LicensePayload;
using System;
using System.Threading.Tasks;

namespace Microi.net.Api
{
    /// <summary>
    /// License 服务桥接类 - 委托给 Microi.License.LicenseService
    /// </summary>
    public static class LicenseService
    {
        public static string GetHardwareId() => LicSvc.GetHardwareId();
        public static LicenseVerifyResult Verify() => LicSvc.Verify();
        public static object GetDiagnostics() => LicSvc.GetDiagnostics();
        public static DosResult WriteLicenseFile(string c) => LicSvc.WriteLicenseFile(c);
        public static void SetGracePeriodMode(bool e) => LicSvc.SetGracePeriodMode(e);
        public static bool IsGracePeriod => LicSvc.IsGracePeriod;
        public static bool IsOpenSourceMode() => LicSvc.IsOpenSourceMode();
        public static (bool, int) CheckGracePeriod() => LicSvc.CheckGracePeriod();
        public static void PersistGracePeriodToDb() => LicSvc.PersistGracePeriodToDb();
        public static void WriteValidProof(DateTime d) => LicSvc.WriteValidProof(d);

        public static class Features
        {
            public const string AiPlugin = LicSvc.Features.AiPlugin;
            public const string MultiTenant = LicSvc.Features.MultiTenant;
            public const string AdvancedReport = LicSvc.Features.AdvancedReport;
            public const string CustomDomain = LicSvc.Features.CustomDomain;
            public const string LicenseAdmin = LicSvc.Features.LicenseAdmin;
        }
        public static bool IsFeatureAllowed(string f) => LicSvc.IsFeatureAllowed(f);

        public static bool IsRevokedByServer => LicSvc.IsRevokedByServer;
        public static int HeartbeatIntervalHours => LicSvc.HeartbeatIntervalHours;
        public static int OfflineGraceDays => LicSvc.OfflineGraceDays;
        public static string ContactEmail => LicSvc.ContactEmail;
        public static void LoadHeartbeatStatus() => LicSvc.LoadHeartbeatStatus();
        public static Task<string> HeartbeatAsync() => LicSvc.HeartbeatAsync();
        public static (bool, int) CheckOfflineDays() => LicSvc.CheckOfflineDays();

        public static object GetHeartbeatDiagnostics()
        {
            try { return LicSvc.GetHeartbeatDiagnostics(); }
            catch { return new { Error = "Heartbeat diagnostics unavailable" }; }
        }

        public static bool GetIsRevokedByServer() => LicSvc.GetIsRevokedByServer();
        public static bool GetIsGracePeriod() => LicSvc.GetIsGracePeriod();
        public static int GetOfflineGraceDays() => LicSvc.GetOfflineGraceDays();
        public static int GetHeartbeatIntervalHours() => LicSvc.GetHeartbeatIntervalHours();

        public static DosResult GenerateRegistrationPackage(string a, string b, string c, string d, string e, string f) => LicSvc.GenerateRegistrationPackage(a, b, c, d, e, f);
        public static Task<DosResult> ImportRegistrationFile(string fc) => LicSvc.ImportRegistrationFile(fc);
        public static Task<DosResult> ImportRegistrationFile(string hid, string ec) => LicSvc.ImportRegistrationFile(ec);

        public static Task<DosResult> ApplyAsync(string hid, string company, string name, string phone,
            string ip, string pt, DateTime? exp, DateTime? uexp, string remark, string account, string pwd,
            string opName = "", string opIP = "")
            => LicSvc.ApplyAsync(hid, company, name, phone, ip, pt, exp, uexp, remark, account, pwd, opName, opIP);

        public static Task<DosResult> IssueAsync(string hid, string company, string name, string phone,
            string ip, string pt, DateTime? exp, DateTime? uexp, string opName = "", string opIP = "")
            => LicSvc.IssueAsync(hid, company, name, phone, ip, pt, exp, uexp, opName, opIP);

        public static Task<DosResult> CheckAsync(string hid) => LicSvc.CheckAsync(hid);
        public static Task<DosResult> QueryApplicationAsync(string hid) => LicSvc.QueryApplicationAsync(hid);

        public static async Task<DosResult> ApproveAsync(string hid, string opName = "", string opIP = "")
            => await LicSvc.ApproveAsync(hid, opName, opIP);

        public static Task<DosResult> RejectAsync(string hid, string reason, string opName = "", string opIP = "")
            => LicSvc.RejectAsync(hid, reason, opName, opIP);

        public static Task<DosResult> RevokeAsync(string hid, bool revoke, string opName = "", string opIP = "")
            => LicSvc.RevokeAsync(hid, revoke, opName, opIP);

        public static DosResult GetLicenseList(string status = "", int page = 1, int pageSize = 20)
            => LicSvc.GetLicenseList(status, page, pageSize);
        public static DosResult GetLicenseLogs(string hid = "", int page = 1, int pageSize = 50)
            => LicSvc.GetLicenseLogs(hid, page, pageSize);

        public static class LogAction
        {
            public const string Apply = LicSvc.LogAction.Apply;
            public const string Issue = LicSvc.LogAction.Issue;
            public const string Approve = LicSvc.LogAction.Approve;
            public const string Reject = LicSvc.LogAction.Reject;
            public const string Revoke = LicSvc.LogAction.Revoke;
            public const string Restore = LicSvc.LogAction.Restore;
            public const string Deploy = LicSvc.LogAction.Deploy;
            public const string Import = LicSvc.LogAction.Import;
        }
        public static void WriteLicenseLog(string hid, string action, string opName = "", string opIP = "", string detail = "")
            => LicSvc.WriteLicenseLog(hid, action, opName, opIP, detail);

        public static (string, string, string, string) GenerateKeyPair() => LicSvc.GenerateKeyPair();
    }
}
