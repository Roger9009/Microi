using System;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace Microi.net
{
    /// <summary>
    /// sys_role和sys_user表Level字段升级：999->9999, 998->9998
    /// </summary>
    public class Upgrade11
    {
        /// <summary>
        /// 
        /// </summary>
        public static string Version = "4.5.3.0";
        /// <summary>
        /// 
        /// </summary>
        public async Task<List<string>> Run(string OsClient)
        {
            var msgs = new List<string>();
            try
            {
                // 该升级必须能在仅有物理表、尚未导入完整 diy_field 元数据的空库执行。
                // 固定条件和值不涉及外部输入，直接通过底座 DbSession 更新，避免 FormEngine
                // 因 PwdEncode 字段元数据尚不存在而拒绝生成 Where。
                var db = Microi.net.OsClient.GetClient(OsClient).Db;
                db.FromSql("UPDATE sys_role SET Level = 9999 WHERE Level = 999").ExecuteNonQuery();
                db.FromSql("UPDATE sys_role SET Level = 9998 WHERE Level = 998").ExecuteNonQuery();
                db.FromSql("UPDATE sys_user SET Level = 9999 WHERE Level = 999").ExecuteNonQuery();
                db.FromSql("UPDATE sys_user SET Level = 9998 WHERE Level = 998").ExecuteNonQuery();
                db.FromSql("UPDATE sys_config SET PwdEncode = 'DES' WHERE PwdEncode = 'V8' OR PwdEncode = '' OR PwdEncode IS NULL")
                    .ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                msgs.Add($"升级 Level/PwdEncode 失败: {ex.Message}");
            }

            await Task.CompletedTask;
            return msgs;
        }

        private static bool IsSkipableEmptyDb(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return false;
            // 空库无业务数据 / 受影响行数为 0：属正常跳过
            return msg.IndexOf("NoExistData", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("不存在的数据", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("Line0", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("受影响行数为0", StringComparison.OrdinalIgnoreCase) >= 0
                || msg.IndexOf("列名", StringComparison.OrdinalIgnoreCase) >= 0 && msg.IndexOf("无效", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
