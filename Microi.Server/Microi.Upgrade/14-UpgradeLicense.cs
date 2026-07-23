using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microi.net
{
    /// <summary>
    /// 历史授权升级占位。
    /// 自定义授权数据已迁移到 LocalLicenseDbConn 独立库，此升级不得再改动框架主库。
    /// </summary>
    public class UpgradeLicense
    {
        public static string Version = "5.7.7.0";

        /// <summary>保留空串以兼容旧调用；实际升级走 <see cref="Run"/>。</summary>
        public static string Sql = "";

        public async Task<List<string>> Run(string osClient)
        {
            return await Task.FromResult(new List<string>());
        }
    }
}
