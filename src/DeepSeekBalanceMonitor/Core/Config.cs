using System;
using System.Collections.Generic;
using System.Drawing;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>一个被监控的账户（供应商 + 密钥 + 独立预警阈值）。</summary>
    public class AccountConfig
    {
        /// <summary>稳定主键（Guid）。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>自定义名称（如「工作号」）。</summary>
        public string Name { get; set; } = "";

        /// <summary>供应商 Id（对应 ProviderRegistry）。</summary>
        public string ProviderId { get; set; } = "deepseek";

        /// <summary>API 密钥（内存明文，落盘前由 ConfigService 加密）。</summary>
        public string ApiKey { get; set; } = "";

        /// <summary>该账户余额预警阈值。</summary>
        public decimal WarnThreshold { get; set; } = 10m;
    }

    /// <summary>
    /// 软件全部设置项。保存为「我的文档\DeepSeek余额监控\设置.json」。
    /// 所有字段均有默认值，改动即时生效。
    /// </summary>
    public class Config
    {
        /// <summary>刷新间隔档位（秒）。需求：5 秒 ~ 2 分钟，六档。</summary>
        public static readonly int[] RefreshIntervals = { 5, 15, 30, 60, 90, 120 };

        /// <summary>悬浮窗文字大小（12~48）。</summary>
        public int FontSize { get; set; } = 28;

        /// <summary>悬浮窗整体透明度（30~100，百分比）。</summary>
        public int Opacity { get; set; } = 90;

        /// <summary>鼠标离开时悬浮窗变暗到的透明度（10~100，百分比）。</summary>
        public int IdleOpacity { get; set; } = 45;

        /// <summary>余额不足时是否弹出通知。</summary>
        public bool NotifyLowBalance { get; set; } = true;

        /// <summary>消费突增时是否弹出通知。</summary>
        public bool NotifySurge { get; set; } = true;

        /// <summary>全部被监控账户。</summary>
        public List<AccountConfig> Accounts { get; set; } = new List<AccountConfig>();

        /// <summary>悬浮窗当前显示的账户 Id。</summary>
        public string ActiveAccountId { get; set; } = "";

        /// <summary>当前账户（找不到时返回 null）。</summary>
        [System.Web.Script.Serialization.ScriptIgnore]
        public AccountConfig ActiveAccount
        {
            get { return Accounts.Find(a => a.Id == ActiveAccountId) ?? (Accounts.Count > 0 ? Accounts[0] : null); }
        }

        /// <summary>刷新间隔（秒），取 RefreshIntervals 中的值。</summary>
        public int RefreshIntervalSeconds { get; set; } = 30;

        /// <summary>登录 Windows 后自动启动。</summary>
        public bool AutoStart { get; set; } = false;

        /// <summary>悬浮窗点击穿透（锁定模式）。</summary>
        public bool LockMode { get; set; } = false;

        /// <summary>悬浮窗始终置顶。</summary>
        public bool TopMost { get; set; } = true;

        /// <summary>悬浮窗位置（屏幕坐标），null 表示默认位置。</summary>
        public Point? FloatPosition { get; set; } = null;

        /// <summary>统计窗口尺寸，null 表示默认尺寸。</summary>
        public Size? StatsSize { get; set; } = null;
    }
}
