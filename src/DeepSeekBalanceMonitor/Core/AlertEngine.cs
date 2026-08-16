using System;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 智能告警引擎：
    /// 1. 余额不足 —— 余额从充足跌到预警线以下时提醒一次，恢复后自动重置。
    /// 2. 消费突增 —— 今日消费超过近 7 天日均消费 3 倍时提醒，每天最多一次。
    /// 两类提醒均受设置中的开关控制。
    /// </summary>
    public class AlertEngine
    {
        private readonly AppContext _ctx;
        private bool _wasLow;            // 上一次余额状态是否不足（用于临界点检测）
        private bool _initialized;       // 是否已完成首次状态记录（首次不提醒，避免启动轰炸）
        private DateTime? _lastSurgeDay; // 最近一次消费突增提醒的日期

        public AlertEngine(AppContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>余额监控每次状态变化时调用（成功查询 / 失败 / 阈值修改）。</summary>
        public void OnBalanceChanged()
        {
            var m = _ctx.Monitor;

            // —— 余额不足临界点提醒 ——
            if (m.Status == BalanceStatus.Low)
            {
                if (_initialized && !_wasLow && _ctx.Config.NotifyLowBalance && m.Balance.HasValue)
                {
                    var threshold = _ctx.Monitor != null && _ctx.Monitor.Balance.HasValue
                        ? _ctx.Config.ActiveAccount?.WarnThreshold ?? 10m : 10m;
                    _ctx.Notify.Show("⚠ 余额不足",
                        "DeepSeek 余额仅剩 ¥" + m.Balance.Value.ToString("F2")
                        + "（预警阈值: ¥" + threshold.ToString("F2") + "）",
                        ToolTipIcon.Warning);
                    Logger.Current?.Warn("告警：余额不足 ¥" + m.Balance.Value.ToString("F2")
                        + "，阈值 ¥" + threshold.ToString("F2"));
                }
                _wasLow = true;
            }
            else if (m.Status == BalanceStatus.Normal)
            {
                _wasLow = false; // 余额恢复，重置临界点
            }

            // —— 消费突增检查（成功查询到余额时才评估） ——
            if (m.Balance.HasValue)
            {
                CheckSurge();
            }

            _initialized = true;
        }

        /// <summary>今日消费超过近 7 天日均 3 倍时提醒（每天最多一次）。</summary>
        private void CheckSurge()
        {
            if (!_ctx.Config.NotifySurge) return;
            if (_lastSurgeDay == DateTime.Today) return;

            var today = _ctx.History.TodaySpent();
            var avg = _ctx.History.AverageDailySpent(7);

            // 无历史基线（如刚安装）或今天无消费时不判断
            if (avg <= 0m || today <= 0m) return;

            if (today > avg * 3)
            {
                _ctx.Notify.Show("🔥 消费突增",
                    "今日消费 ¥" + today.ToString("F2") + "，超过近 7 天日均消费（¥"
                    + avg.ToString("F2") + "）的 3 倍，请注意排查异常消耗！",
                    ToolTipIcon.Warning);
                _lastSurgeDay = DateTime.Today;
                Logger.Current?.Warn("告警：消费突增，今日 ¥" + today.ToString("F2")
                    + "，近 7 天日均 ¥" + avg.ToString("F2"));
            }
        }
    }
}
