using System;
using System.Threading.Tasks;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 单据编号生成服务默认实现。
    /// 基于 Redis Hash 自增保证当日流水唯一（按租户隔离），失败时回退到时间戳，保证高可用。
    /// </summary>
    public class BillNoService : IBillNoService
    {
        /// <inheritdoc/>
        public virtual Task<string> GenerateAsync(string prefix, string osClient, int seqLength = 6)
        {
            var day = DateTime.Now.ToString("yyyyMMdd");
            long seq;
            try
            {
                // Key 命名规范：Microi:{OsClient}:{分类}:{Key}
                var cacheKey = $"Microi:{osClient}:BillNo:{prefix}:{day}";
                var cache = MicroiEngine.CacheTenant.Cache(osClient);
                seq = (long)cache.HashIncrement(cacheKey, "seq", 1);
            }
            catch
            {
                // 缓存不可用时的兜底：用当天毫秒数，避免阻断业务
                seq = (long)(DateTime.Now - DateTime.Today).TotalMilliseconds;
            }

            var billNo = $"{prefix}{day}{seq.ToString().PadLeft(seqLength, '0')}";
            return Task.FromResult(billNo);
        }
    }
}
