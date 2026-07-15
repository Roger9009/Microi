using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microi.net
{
    /// <summary>
    /// 必要升级：创建 diy_license 授权管理表 --2026-06-21
    /// 建表/补列走底座 IMicroiORM，不再执行方言 SQL。
    /// </summary>
    public class UpgradeLicense
    {
        public static string Version = "5.7.7.0";

        /// <summary>保留空串以兼容旧调用；实际升级走 <see cref="Run"/>。</summary>
        public static string Sql = "";

        public async Task<List<string>> Run(string osClient)
        {
            var msgs = new List<string>();
            try
            {
                var client = OsClientExtend.GetClient(osClient);
                if (client?.Db == null)
                {
                    msgs.Add($"租户[{osClient}] DbSession 不可用");
                    return await Task.FromResult(msgs);
                }

                UpgradeDdlHelper.EnsureTableWithColumns(client, "diy_license", new[]
                {
                    new UpgradeDdlHelper.ColumnSpec { Name = "HID", Type = "varchar(128)", Label = "硬件指纹ID", NotNull = true },
                    new UpgradeDdlHelper.ColumnSpec { Name = "Company", Type = "varchar(200)", Label = "授权公司名称" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "Name", Type = "varchar(100)", Label = "联系人姓名" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "Phone", Type = "varchar(50)", Label = "联系电话" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "IP", Type = "varchar(100)", Label = "服务器IP" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "ProductType", Type = "varchar(50)", Label = "产品类型" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "Status", Type = "varchar(20)", Label = "状态" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "LicenseContent", Type = "mediumtext", Label = "License内容" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "IssuedAt", Type = "datetime", Label = "签发时间" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "ExpirationDate", Type = "datetime", Label = "到期时间" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "UpdateExpirationDate", Type = "datetime", Label = "更新服务到期" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "RejectReason", Type = "varchar(500)", Label = "驳回原因" },
                    new UpgradeDdlHelper.ColumnSpec { Name = "Remark", Type = "varchar(1000)", Label = "备注" },
                });

                UpgradeDdlHelper.EnsureIndex(client, "diy_license", "idx_diy_license_hid", "HID", unique: true);
                UpgradeDdlHelper.EnsureIndex(client, "diy_license", "idx_diy_license_status", "Status");
            }
            catch (Exception ex)
            {
                msgs.Add(ex.Message);
            }
            return await Task.FromResult(msgs);
        }
    }
}
