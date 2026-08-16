using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// OpenRouter 余额适配器。
    /// GET https://openrouter.ai/api/v1/credits
    /// Authorization: Bearer &lt;Management Key&gt;
    /// </summary>
    public class OpenRouterProvider : IBalanceProvider
    {
        public string Id => "openrouter";
        public string DisplayName => "OpenRouter";
        public string BaseUrl => "https://openrouter.ai/api";

        private const string Endpoint = "https://openrouter.ai/api/v1/credits";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public async Task<AccountBalance> GetBalanceAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new BalanceQueryException(QueryErrorKind.AuthFailed, "尚未设置 API 密钥，请先到「设置」中填写");

            using (var req = new HttpRequestMessage(HttpMethod.Get, Endpoint))
            {
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey.Trim());
                req.Headers.TryAddWithoutValidation("Accept", "application/json");

                HttpResponseMessage resp;
                try { resp = await Http.SendAsync(req).ConfigureAwait(false); }
                catch (Exception ex)
                {
                    throw new BalanceQueryException(QueryErrorKind.Network, "网络请求失败: " + ex.Message);
                }

                using (resp)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
                        throw new BalanceQueryException(QueryErrorKind.AuthFailed,
                            "API 密钥无效或已失效（HTTP " + (int)resp.StatusCode + "）");
                    if (!resp.IsSuccessStatusCode)
                        throw new BalanceQueryException(QueryErrorKind.Network,
                            "接口返回异常状态码 HTTP " + (int)resp.StatusCode);
                    try { return ParseCredits(body); }
                    catch (Exception ex)
                    {
                        throw new BalanceQueryException(QueryErrorKind.ParseError,
                            "无法解析余额数据（接口可能变更）: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>解析 credits 响应：剩余 = total_credits - total_usage。</summary>
        public static AccountBalance ParseCredits(string body)
        {
            var doc = Json.Deserialize<CreditsDoc>(body);
            if (doc?.data == null)
                throw new FormatException("响应无 data 字段");
            var total = doc.data.total_credits;
            var used = doc.data.total_usage;
            if (total == null && used == null)
                throw new FormatException("响应无 credits 数据");
            return new AccountBalance
            {
                IsAvailable = total != null && used != null ? total - used > 0 : total > 0,
                Remaining = total != null && used != null ? (decimal?)(total - used) : (decimal?)total,
                Used = (decimal?)used,
                Total = (decimal?)total,
                Currency = "USD"
            };
        }

        private class CreditsDoc { public CreditsData data { get; set; } }
        private class CreditsData { public double? total_credits { get; set; } public double? total_usage { get; set; } }
    }
}
