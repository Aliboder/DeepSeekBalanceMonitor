using System.Windows.Forms;

namespace QuotaMonitor.Core
{
    /// <summary>
    /// 系统通知：通过托盘图标弹出通知（Windows 10/11 上显示为系统通知中心消息）。
    /// 用于：余额不足提醒、消费突增提醒、定时隐藏提示。
    /// </summary>
    public class NotificationService
    {
        private readonly NotifyIcon _trayIcon;

        public NotificationService(NotifyIcon trayIcon)
        {
            _trayIcon = trayIcon;
        }

        /// <summary>弹出系统通知。</summary>
        /// <param name="title">标题（加 emoji 前缀由调用方决定）。</param>
        /// <param name="message">正文。</param>
        /// <param name="kind">图标样式：Info（蓝色 i）/ Warning（黄色！）/ Error（红色 x）。</param>
        public void Show(string title, string message, ToolTipIcon kind = ToolTipIcon.Info)
        {
            if (_trayIcon == null) return;
            // 超时参数在 Windows 10+ 由系统决定，传 5000 仅为兼容
            _trayIcon.ShowBalloonTip(5000, title, message, kind);
        }
    }
}
