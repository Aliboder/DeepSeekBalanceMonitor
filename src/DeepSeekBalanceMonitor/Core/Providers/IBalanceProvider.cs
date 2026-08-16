using System.Threading.Tasks;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>余额供应商适配器：把某家供应商的余额接口统一为一个查询方法。</summary>
    public interface IBalanceProvider
    {
        /// <summary>供应商唯一标识（如 "deepseek"）。</summary>
        string Id { get; }

        /// <summary>显示名（如 "DeepSeek"）。</summary>
        string DisplayName { get; }

        /// <summary>接口基址。</summary>
        string BaseUrl { get; }

        /// <summary>查询余额。失败时抛 <see cref="BalanceQueryException"/>。</summary>
        Task<AccountBalance> GetBalanceAsync(string apiKey);
    }
}
