using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// OpenCode Go 套餐余量查询客户端。
    /// GET https://opencode.ai/zen/go/v1/usage
    /// Authorization: Bearer &lt;API Key&gt;
    /// 返回三个窗口（5小时滚动、每周、每月）的使用百分比。
    /// </summary>
    public class OpenCodeGoClient
    {
        private const string Endpoint = "https://opencode.ai/zen/go/v1/usage";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        /// <summary>查询 Go 套餐余量。失败时抛出 <see cref="BalanceQueryException"/>。</summary>
        public async Task<SubscriptionResult> GetUsageAsync(string apiKey)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new BalanceQueryException(QueryErrorKind.AuthFailed, "尚未设置 OpenCode Go API 密钥");

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

                    if (resp.StatusCode == HttpStatusCode.Unauthorized || resp.StatusCode == HttpStatusCode.Forbidden)
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
                        return ParseUsage(body);
                    }
                    catch (Exception ex)
                    {
                        throw new BalanceQueryException(QueryErrorKind.ParseError,
                            "无法解析套餐数据（接口可能变更）: " + ex.Message);
                    }
                }
            }
        }

        /// <summary>解析 Go usage 响应。公开供测试。</summary>
        public static SubscriptionResult ParseUsage(string body)
        {
            var doc = Json.Deserialize<UsageDoc>(body);
            if (doc == null || doc.usage == null)
                throw new FormatException("响应中无 usage 信息");

            var windows = new List<SubscriptionWindow>();

            var session = ParseWindow(doc.usage.rolling, "session");
            if (session != null) windows.Add(session);

            var weekly = ParseWindow(doc.usage.weekly, "weekly");
            if (weekly != null) windows.Add(weekly);

            var monthly = ParseWindow(doc.usage.monthly, "monthly");
            if (monthly != null) windows.Add(monthly);

            if (windows.Count == 0)
                throw new FormatException("响应中无有效窗口数据");

            return new SubscriptionResult { IsOk = true, Windows = windows };
        }

        private static SubscriptionWindow ParseWindow(WindowInfo info, string kind)
        {
            if (info == null) return null;

            int percent = 0;
            if (info.percent.HasValue)
                percent = Math.Max(0, Math.Min(100, (int)info.percent.Value));
            else
                return null;

            DateTime? resetsAt = null;
            if (!string.IsNullOrEmpty(info.resetsAt))
            {
                DateTime dt;
                if (DateTime.TryParse(info.resetsAt, System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out dt))
                {
                    resetsAt = dt.ToLocalTime();
                }
            }

            return new SubscriptionWindow
            {
                Kind = kind,
                UsedPercent = percent,
                RemainingPercent = 100 - percent,
                ResetsAt = resetsAt
            };
        }

        // —— 响应结构 ——

        private class UsageDoc
        {
            public UsageInfo usage { get; set; }
        }

        private class UsageInfo
        {
            public WindowInfo rolling { get; set; }
            public WindowInfo weekly { get; set; }
            public WindowInfo monthly { get; set; }
        }

        private class WindowInfo
        {
            public string status { get; set; }
            public double? percent { get; set; }
            public string resetsAt { get; set; }
        }
    }
}
