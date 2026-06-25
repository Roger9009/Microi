using System.Threading.Tasks;

namespace Microi.net.Business.Common
{
    /// <summary>
    /// 单据编号生成服务（ERP/MES 共用）。
    /// </summary>
    public interface IBillNoService
    {
        /// <summary>
        /// 生成单据编号，格式：前缀 + yyyyMMdd + 当日流水（按租户隔离）。
        /// 例如：SO20260624000123
        /// </summary>
        /// <param name="prefix">单据前缀，如 SO / WO</param>
        /// <param name="osClient">租户标识</param>
        /// <param name="seqLength">流水号位数，默认 6 位</param>
        Task<string> GenerateAsync(string prefix, string osClient, int seqLength = 6);
    }
}
