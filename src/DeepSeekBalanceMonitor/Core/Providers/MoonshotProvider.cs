using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// Moonshot（月之暗面）余额适配器。
    /// GET https://api.moonshot.cn/v1/users/me/balance
    /// Authorization: Bearer &lt;API Key&gt;
    /// </summary>
    public class MoonshotProvider : IBalanceProvider
    {
        public string Id => "moonshot";
        public string DisplayName => "Moonshot";
        public string BaseUrl => "https://api.moonshot.cn";

        private const string Endpoint = "https://api.moonshot.cn/v1/users/me/balance";
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
                    try { return ParseBalance(body); }
                    catch (Exception ex)
                    {
                        throw new BalanceQueryException(QueryErrorKind.ParseError,
                            "无法解析余额数据（接口可能变更）: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>解析余额响应：available / voucher / cash 分解。</summary>
        public static AccountBalance ParseBalance(string body)
        {
            var doc = Json.Deserialize<BalanceDoc>(body);
            var d = doc?.data;
            if (d == null) throw new FormatException("响应无 data 字段");
            return new AccountBalance
            {
                IsAvailable = d.available_balance > 0,
                Remaining = (decimal?)d.available_balance,
                Granted = (decimal?)d.voucher_balance,
                ToppedUp = (decimal?)d.cash_balance,
                Currency = string.IsNullOrEmpty(d.currency) ? "CNY" : d.currency
            };
        }

        private class BalanceDoc { public BalanceData data { get; set; } }
        private class BalanceData { public double available_balance { get; set; } public double voucher_balance { get; set; } public double cash_balance { get; set; } public string currency { get; set; } }
    }
}
