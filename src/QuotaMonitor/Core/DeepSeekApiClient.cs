using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QuotaMonitor.Core
{
    /// <summary>余额查询结果。</summary>
    public class BalanceResult
    {
        /// <summary>账户是否还可调用 API。</summary>
        public bool IsAvailable { get; set; }

        /// <summary>余额（元）。</summary>
        public decimal TotalBalance { get; set; }

        /// <summary>查询时间。</summary>
        public DateTime Time { get; set; } = DateTime.Now;
    }

    /// <summary>余额查询失败的原因分类（用于悬浮窗/托盘提示）。</summary>
    public enum QueryErrorKind
    {
        /// <summary>API 密钥无效或已失效（401 / 40002 / 40003 等认证错误）。</summary>
        AuthFailed,
        /// <summary>网络不通、超时等。</summary>
        Network,
        /// <summary>接口返回了无法解析的内容（可能接口变更）。</summary>
        ParseError,
        /// <summary>账户不可用（欠费停用等）。</summary>
        AccountUnavailable
    }

    /// <summary>
    /// DeepSeek 官方余额接口客户端。
    /// GET https://api.deepseek.com/user/balance
    /// Authorization: Bearer &lt;API Key&gt;
    /// </summary>
    public class DeepSeekApiClient
    {
        private const string Endpoint = "https://api.deepseek.com/user/balance";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        /// <summary>查询当前余额。失败时抛出 <see cref="BalanceQueryException"/>。</summary>
        public async Task<BalanceResult> GetBalanceAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new BalanceQueryException(QueryErrorKind.AuthFailed, "尚未设置 API 密钥，请先到「设置」中填写");

            using (var req = new HttpRequestMessage(HttpMethod.Get, Endpoint))
            {
                req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey.Trim());
                req.Headers.TryAddWithoutValidation("Accept", "application/json");

                HttpResponseMessage resp;
                try
                {
                    resp = await Http.SendAsync(req).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    throw new BalanceQueryException(QueryErrorKind.Network, "网络请求失败: " + ex.Message);
                }

                using (resp)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // 认证类错误：HTTP 401/403，或平台返回的业务错误码 40002/40003
                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden
                        || body.Contains("40002") || body.Contains("40003"))
                    {
                        throw new BalanceQueryException(QueryErrorKind.AuthFailed,
                            "API 密钥无效或已失效（HTTP " + (int)resp.StatusCode + "）");
                    }

                    if (!resp.IsSuccessStatusCode)
                    {
                        throw new BalanceQueryException(QueryErrorKind.Network,
                            "接口返回异常状态码 HTTP " + (int)resp.StatusCode);
                    }

                    try
                    {
                        return ParseBalance(body);
                    }
                    catch (Exception ex)
                    {
                        throw new BalanceQueryException(QueryErrorKind.ParseError,
                            "无法解析余额数据（接口可能变更）: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>解析官方响应，取人民币（CNY）余额。公开供测试。</summary>
        public static BalanceResult ParseBalance(string body)
        {
            var doc = Json.Deserialize<BalanceDoc>(body);
            if (doc == null || doc.balance_infos == null || doc.balance_infos.Length == 0)
                throw new FormatException("响应中无余额信息");

            BalanceInfo cny = null;
            foreach (var info in doc.balance_infos)
            {
                if (string.Equals(info.currency, "CNY", StringComparison.OrdinalIgnoreCase))
                {
                    cny = info;
                    break;
                }
            }
            var picked = cny ?? doc.balance_infos[0];
            if (picked == null || string.IsNullOrEmpty(picked.total_balance))
                throw new FormatException("余额字段为空");

            return new BalanceResult
            {
                IsAvailable = doc.is_available,
                TotalBalance = decimal.Parse(picked.total_balance, CultureInfo.InvariantCulture)
            };
        }

        // —— 官方响应结构（按需字段） ——
        private class BalanceDoc
        {
            public bool is_available { get; set; }
            public BalanceInfo[] balance_infos { get; set; }
        }

        private class BalanceInfo
        {
            public string currency { get; set; }
            public string total_balance { get; set; }
        }
    }

    /// <summary>带错误分类的余额查询异常。</summary>
    public class BalanceQueryException : Exception
    {
        public QueryErrorKind Kind { get; }

        public BalanceQueryException(QueryErrorKind kind, string message) : base(message)
        {
            Kind = kind;
        }
    }
}
