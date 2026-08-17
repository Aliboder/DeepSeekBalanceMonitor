using System;
using System.Windows.Forms;

namespace QuotaMonitor.Core
{
    /// <summary>
    /// 定时隐藏服务：隐藏悬浮窗一段时间，到点自动恢复。
    /// 重复设置会重新计时；托盘「显示」/左键单击可提前恢复。
    /// </summary>
    public class HideService : IDisposable
    {
        private readonly AppContext _ctx;
        private readonly Timer _timer;

        private DateTime? _resumeAt;

        /// <summary>隐藏/恢复状态变化时触发（托盘菜单「显示」项可见性刷新）。</summary>
        public event EventHandler StateChanged;

        /// <summary>是否处于隐藏中。</summary>
        public bool IsHidden { get; private set; }

        /// <summary>预计恢复时间（隐藏中有效）。</summary>
        public DateTime? ResumeAt => _resumeAt;

        public HideService(AppContext ctx)
        {
            _ctx = ctx;
            _timer = new Timer();
            _timer.Tick += (s, e) => Restore();
        }

        /// <summary>隐藏悬浮窗指定分钟数（1~600）。重复调用重新计时。</summary>
        public void Hide(int minutes)
        {
            minutes = Math.Max(1, Math.Min(600, minutes));

            _ctx.FloatWindow.Hide();
            IsHidden = true;
            _resumeAt = DateTime.Now.AddMinutes(minutes);

            // 重新计时
            _timer.Stop();
            _timer.Interval = minutes * 60 * 1000;
            _timer.Start();

            _ctx.Notify.Show("悬浮窗已隐藏",
                "将于 " + _resumeAt.Value.ToString("HH:mm") + " 自动恢复显示。\n可通过托盘图标随时提前恢复。");

            StateChanged?.Invoke(this, EventArgs.Empty);
            Logger.Current?.Info("悬浮窗已隐藏 " + minutes + " 分钟，预计 " + _resumeAt.Value.ToString("HH:mm") + " 恢复");
        }

        /// <summary>提前恢复显示（托盘「显示」或左键单击）。</summary>
        public void Restore()
        {
            if (!IsHidden) return;

            _timer.Stop();
            IsHidden = false;
            _resumeAt = null;

            _ctx.FloatWindow.ShowWindow();
            StateChanged?.Invoke(this, EventArgs.Empty);
            Logger.Current?.Info("悬浮窗已恢复显示");
        }

        public void Dispose()
        {
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
