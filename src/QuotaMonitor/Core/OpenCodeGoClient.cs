using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace QuotaMonitor.Core
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
            // 显式密钥为空时，回退读取本机 opencode 已登录凭据（auth.json），
            // 复用 opencode CLI 的登录会话，无需在设置中重复填写。
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = LoadLocalApiKey();
            }
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new BalanceQueryException(QueryErrorKind.AuthFailed,
                    "尚未设置 OpenCode Go API 密钥（也未找到本机 opencode 登录凭据）");

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

                    if ((int)resp.StatusCode == 429)
                    {
                        throw new BalanceQueryException(QueryErrorKind.RateLimited,
                            "请求过于频繁（限流），请稍后重试");
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

        /// <summary>
        /// 尝试读取本机 opencode 已登录的 Go 凭据（~/.local/share/opencode/auth.json）。
        /// 复用 CLI 登录会话，返回 API Key；未找到或结构不符时返回空字符串。
        /// </summary>
        public static string LoadLocalApiKey()
        {
            try
            {
                var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                var path = Path.Combine(home, ".local", "share", "opencode", "auth.json");
                if (!File.Exists(path)) return "";

                var doc = Json.Deserialize<Dictionary<string, AuthEntry>>(File.ReadAllText(path));
                AuthEntry entry = null;
                if (doc != null)
                {
                    doc.TryGetValue("opencode-go", out entry);
                    if (entry == null) doc.TryGetValue("opencode", out entry);
                }
                if (entry != null && entry.type == "api" && !string.IsNullOrWhiteSpace(entry.key))
                    return entry.key.Trim();
                return "";
            }
            catch
            {
                return ""; // 读取失败不致命，走原有"未设置密钥"路径
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

            // 多字段名兼容 + 0~1 比例归一化：
            // 上游 percent 字段语义为 0~100；其余 dashboard 风格字段（usagePercent 等）为 0~1 比例，
            // 只在后者场景下自动放大为百分数，避免显示成 0.42%（几乎满额）的误导值。
            double? raw = info.percent;
            bool fromPercentField = info.percent.HasValue;
            if (!fromPercentField) raw = info.usagePercent;
            if (!fromPercentField && raw == null) raw = info.usedPercent;
            if (!fromPercentField && raw == null) raw = info.percentUsed;
            if (!fromPercentField && raw == null) raw = info.percentage;
            if (raw == null) return null;

            double p = raw.Value;
            if (!fromPercentField && p >= 0 && p <= 1) p *= 100;
            int percent = Math.Max(0, Math.Min(100, (int)Math.Round(p)));

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
            else if (info.resetAt.HasValue)
            {
                resetsAt = FromTimestamp(info.resetAt.Value);
            }
            else if (info.resetInSec.HasValue)
            {
                try { resetsAt = DateTime.Now.AddSeconds(Math.Max(0, info.resetInSec.Value)); }
                catch { }
            }

            return new SubscriptionWindow
            {
                Kind = kind,
                UsedPercent = percent,
                RemainingPercent = 100 - percent,
                ResetsAt = resetsAt
            };
        }

        /// <summary>时间戳转本地时间（兼容秒与毫秒两种单位）。</summary>
        private static DateTime? FromTimestamp(double value)
        {
            try
            {
                long ms = value < 20000000000 ? (long)(value * 1000) : (long)value;
                return DateTimeOffset.FromUnixTimeMilliseconds(ms).LocalDateTime;
            }
            catch { return null; }
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
            public double? usagePercent { get; set; }
            public double? usedPercent { get; set; }
            public double? percentUsed { get; set; }
            public double? percentage { get; set; }
            public string resetsAt { get; set; }
            public double? resetAt { get; set; }
            public double? resetInSec { get; set; }
        }

        private class AuthEntry
        {
            public string type { get; set; }
            public string key { get; set; }
        }
    }
}
