using System;

namespace Microi.LocalLicense
{
    /// <summary>
    /// License 文件载荷（存储在 local-license.json 中）
    /// </summary>
    public class LocalLicensePayload
    {
        /// <summary>硬件指纹ID</summary>
        public string HID { get; set; }
        /// <summary>授权公司名称</summary>
        public string Company { get; set; }
        /// <summary>联系人姓名</summary>
        public string Name { get; set; }
        /// <summary>联系电话</summary>
        public string Phone { get; set; }
        /// <summary>服务器IP（签发时记录）</summary>
        public string IP { get; set; }
        /// <summary>产品类型：Personal / Enterprise</summary>
        public string ProductType { get; set; }
        /// <summary>签发时间（UTC）</summary>
        public DateTime IssuedAt { get; set; }
        /// <summary>License到期时间（UTC）</summary>
        public DateTime ExpirationDate { get; set; }
        /// <summary>更新服务到期时间（UTC）</summary>
        public DateTime UpdateExpirationDate { get; set; }
        /// <summary>RSA-SHA256 签名（Base64），验签时排除此字段</summary>
        public string Signature { get; set; }
    }

    /// <summary>
    /// License 本地验证结果
    /// </summary>
    public class LocalLicenseVerifyResult
    {
        /// <summary>License是否有效</summary>
        public bool Valid { get; set; }
        /// <summary>前端兼容别名：IsLicensed = Valid</summary>
        public bool IsLicensed => Valid;
        /// <summary>产品类型</summary>
        public string ProductType { get; set; }
        /// <summary>授权公司</summary>
        public string Company { get; set; }
        /// <summary>硬件指纹（已验证）</summary>
        public string HID { get; set; }
        /// <summary>License到期时间</summary>
        public DateTime? ExpirationDate { get; set; }
        /// <summary>更新服务到期时间</summary>
        public DateTime? UpdateExpirationDate { get; set; }
        /// <summary>前端兼容：payload.IssuedAt 的格式化字符串</summary>
        public string IssuedDate { get; set; }
        /// <summary>剩余有效天数（负数表示已过期）</summary>
        public int? DaysRemaining { get; set; }
        /// <summary>验证结果描述信息</summary>
        public string Message { get; set; }
        /// <summary>是否处于宽限期（License文件缺失或无效，降级运行）</summary>
        public bool IsGracePeriod { get; set; }
    }

    /// <summary>
    /// diy_local_license 数据库记录实体
    /// </summary>
    public class DiyLocalLicenseRecord
    {
        /// <summary>主键</summary>
        public string Id { get; set; }
        /// <summary>硬件指纹ID（唯一）</summary>
        public string HID { get; set; }
        /// <summary>授权公司</summary>
        public string Company { get; set; }
        /// <summary>联系人</summary>
        public string Name { get; set; }
        /// <summary>联系电话</summary>
        public string Phone { get; set; }
        /// <summary>服务器IP</summary>
        public string IP { get; set; }
        /// <summary>产品类型：Personal / Enterprise</summary>
        public string ProductType { get; set; }
        /// <summary>状态：Pending / Issued / Revoked / Rejected</summary>
        public string Status { get; set; }
        /// <summary>完整License JSON内容（含签名，仅Issued状态有值）</summary>
        public string LicenseContent { get; set; }
        /// <summary>签发时间</summary>
        public DateTime? IssuedAt { get; set; }
        /// <summary>License到期时间</summary>
        public DateTime? ExpirationDate { get; set; }
        /// <summary>更新服务到期时间</summary>
        public DateTime? UpdateExpirationDate { get; set; }
        /// <summary>驳回原因</summary>
        public string RejectReason { get; set; }
        /// <summary>备注</summary>
        public string Remark { get; set; }
        /// <summary>创建时间</summary>
        public DateTime CreateTime { get; set; }
        /// <summary>最后更新时间</summary>
        public DateTime? UpdateTime { get; set; }
    }

    /// <summary>
    /// License 状态常量
    /// </summary>
    public static class LocalLicenseStatus
    {
        public const string Pending = "Pending";
        public const string Issued = "Issued";
        public const string Revoked = "Revoked";
        public const string Rejected = "Rejected";
    }

    /// <summary>
    /// 产品类型常量
    /// </summary>
    public static class LocalLicenseProductType
    {
        public const string Personal = "Personal";
        public const string Enterprise = "Enterprise";
    }
}
