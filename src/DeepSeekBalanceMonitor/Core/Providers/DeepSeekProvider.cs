using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// DeepSeek 官方余额适配器。
    /// GET https://api.deepseek.com/user/balance
    /// Authorization: Bearer &lt;API Key&gt;
    /// </summary>
    public class DeepSeekProvider : IBalanceProvider
    {
        public string Id => "deepseek";
        public string DisplayName => "DeepSeek";
        public string BaseUrl => "https://api.deepseek.com";

        private const string Endpoint = "https://api.deepseek.com/user/balance";
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
                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden
                        || body.Contains("40002") || body.Contains("40003"))
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

        /// <summary>解析官方响应，取人民币（CNY）余额。公开供测试。</summary>
        public static AccountBalance ParseBalance(string body)
        {
            var doc = Json.Deserialize<BalanceDoc>(body);
            if (doc == null || doc.balance_infos == null || doc.balance_infos.Length == 0)
                throw new FormatException("响应中无余额信息");

            BalanceInfo cny = null;
            foreach (var info in doc.balance_infos)
            {
                if (string.Equals(info.currency, "CNY", StringComparison.OrdinalIgnoreCase)) { cny = info; break; }
            }
            var picked = cny ?? doc.balance_infos[0];
            if (picked == null || string.IsNullOrEmpty(picked.total_balance))
                throw new FormatException("余额字段为空");

            return new AccountBalance
            {
                IsAvailable = doc.is_available,
                Remaining = ParseNullable(picked.total_balance),
                Granted = ParseNullable(picked.granted_balance),
                ToppedUp = ParseNullable(picked.topped_up_balance),
                Currency = picked.currency ?? "CNY"
            };
        }

        /// <summary>字符串余额 → decimal?；空串返回 null。</summary>
        private static decimal? ParseNullable(string value)
        {
            if (string.IsNullOrEmpty(value)) return null;
            return decimal.Parse(value, CultureInfo.InvariantCulture);
        }

        private class BalanceDoc
        {
            public bool is_available { get; set; }
            public BalanceInfo[] balance_infos { get; set; }
        }

        private class BalanceInfo
        {
            public string currency { get; set; }
            public string total_balance { get; set; }
            public string granted_balance { get; set; }
            public string topped_up_balance { get; set; }
        }
    }
}
