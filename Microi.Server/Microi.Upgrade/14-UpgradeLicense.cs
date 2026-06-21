using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microi.net
{
    /// <summary>
    /// 必要升级：创建 diy_license 授权管理表 --2026-06-21
    /// </summary>
    public class UpgradeLicense
    {
        public static string Version = "5.7.7.0";

        public static string Sql = @"
CREATE TABLE IF NOT EXISTS `diy_license` (
  `Id`                   VARCHAR(32)   NOT NULL              COMMENT '主键',
  `HID`                  VARCHAR(128)  NOT NULL              COMMENT '硬件指纹ID（唯一）',
  `Company`              VARCHAR(200)  DEFAULT ''            COMMENT '授权公司名称',
  `Name`                 VARCHAR(100)  DEFAULT ''            COMMENT '联系人姓名',
  `Phone`                VARCHAR(50)   DEFAULT ''            COMMENT '联系电话',
  `IP`                   VARCHAR(100)  DEFAULT ''            COMMENT '服务器IP',
  `ProductType`          VARCHAR(50)   DEFAULT 'Personal'   COMMENT '产品类型：Personal/Enterprise',
  `Status`               VARCHAR(20)   DEFAULT 'Pending'    COMMENT '状态：Pending/Issued/Revoked/Rejected',
  `LicenseContent`       LONGTEXT      DEFAULT NULL         COMMENT '完整License JSON内容（含RSA签名）',
  `IssuedAt`             DATETIME      DEFAULT NULL         COMMENT '签发时间（UTC）',
  `ExpirationDate`       DATETIME      DEFAULT NULL         COMMENT 'License到期时间',
  `UpdateExpirationDate` DATETIME      DEFAULT NULL         COMMENT '更新服务到期时间',
  `RejectReason`         VARCHAR(500)  DEFAULT NULL         COMMENT '驳回原因',
  `Remark`               VARCHAR(1000) DEFAULT ''           COMMENT '备注',
  `CreateTime`           DATETIME      NOT NULL             COMMENT '申请时间',
  `UpdateTime`           DATETIME      DEFAULT NULL         COMMENT '最后更新时间',
  PRIMARY KEY (`Id`),
  UNIQUE KEY `idx_diy_license_hid` (`HID`),
  KEY `idx_diy_license_status` (`Status`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='License授权管理表';
";

        public async Task<List<string>> Run(string osClient)
        {
            var msgs = new List<string>();
            return await Task.FromResult(msgs);
        }
    }
}
