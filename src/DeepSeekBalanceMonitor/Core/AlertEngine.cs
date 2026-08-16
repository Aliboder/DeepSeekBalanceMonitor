using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 智能告警引擎（按账户）：
    /// 1. 余额不足 —— 余额从充足跌到预警线以下时提醒一次，恢复后自动重置。
    /// 2. 消费突增 —— 今日消费超过近 7 天日均消费 3 倍时提醒，每天最多一次。
    /// 两类提醒均受设置中的开关控制；通知通过注入的委托发出，便于脱离 UI 测试。
    /// </summary>
    public class AlertEngine
    {
        private readonly Config _config;
        private readonly HistoryStore _history;
        private readonly Action<string, string, ToolTipIcon> _notify;

        private readonly Dictionary<string, bool> _wasLow = new Dictionary<string, bool>();
        private readonly Dictionary<string, DateTime> _lastSurgeDay = new Dictionary<string, DateTime>();
        // 某账户是否已被观察过（首次观察不提醒，避免启动轰炸）
        private readonly Dictionary<string, bool> _initialized = new Dictionary<string, bool>();

        public AlertEngine(Config config, HistoryStore history, Action<string, string, ToolTipIcon> notify)
        {
            _config = config;
            _history = history;
            _notify = notify;
        }

        /// <summary>某账户监控状态变化时调用（账户切换时传当前 monitor）。</summary>
        public void OnBalanceChanged(BalanceMonitor monitor)
        {
            if (monitor == null) return;
            var aid = monitor.AccountId;
            var threshold = monitor.WarnThreshold;

            bool initialized = _initialized.TryGetValue(aid, out var init) && init;
            _initialized[aid] = true; // mark seen

            if (monitor.Status == BalanceStatus.Low)
            {
                bool wasLow = _wasLow.TryGetValue(aid, out var w) && w;
                if (_config.NotifyLowBalance && initialized && !wasLow && monitor.Balance.HasValue)
                {
                    _notify("⚠ 余额不足",
                        monitor.AccountName + " 余额仅剩 ¥" + monitor.Balance.Value.ToString("F2")
                        + "（预警阈值: ¥" + threshold.ToString("F2") + "）",
                        ToolTipIcon.Warning);
                }
                _wasLow[aid] = true;
            }
            else if (monitor.Status == BalanceStatus.Normal)
            {
                _wasLow[aid] = false;
            }

            if (monitor.Balance.HasValue) CheckSurge(monitor, aid);
        }

        private void CheckSurge(BalanceMonitor monitor, string accountId)
        {
            if (!_config.NotifySurge) return;
            if (_lastSurgeDay.TryGetValue(accountId, out var day) && day == DateTime.Today) return;

            var today = _history.TodaySpent(accountId);
            var avg = _history.AverageDailySpent(accountId, 7);
            if (avg <= 0m || today <= 0m) return;

            if (today > avg * 3)
            {
                _notify("🔥 消费突增",
                    monitor.AccountName + " 今日消费 ¥" + today.ToString("F2")
                    + "，超过近 7 天日均消费（¥" + avg.ToString("F2") + "）的 3 倍，请注意排查异常消耗！",
                    ToolTipIcon.Warning);
                _lastSurgeDay[accountId] = DateTime.Today;
            }
        }
    }
}
