using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 系统托盘：图标颜色随余额状态变化（绿/红/橙/靛蓝），悬停显示余额，
    /// 左键打开统计，右键完整菜单。
    /// </summary>
    public class TrayIcon : IDisposable
    {
        private readonly AppContext _ctx;
        private readonly NotifyIcon _icon;
        private readonly ContextMenuStrip _menu;

        // 菜单项
        private readonly ToolStripMenuItem _miShow;
        private readonly ToolStripMenuItem _miBalance;
        private readonly ToolStripMenuItem _miError;
        private readonly ToolStripMenuItem _miLock;
        private readonly ToolStripMenuItem _miTopmost;
        private readonly ToolStripMenuItem _miAccounts;

        // 动态生成的状态图标（4 色）
        private Icon _iconNormal, _iconLow, _iconError, _iconDefault;
        private Icon _currentIcon;

        /// <summary>左键单击：打开统计面板。</summary>
        public event EventHandler ShowStatsRequested;

        /// <summary>菜单「设置...」：打开设置窗口。</summary>
        public event EventHandler OpenSettingsRequested;

        /// <summary>底层托盘图标（供通知服务弹出气泡）。</summary>
        internal NotifyIcon NativeIcon => _icon;

        public TrayIcon(AppContext ctx)
        {
            _ctx = ctx;

            // 预生成 4 色图标
            _iconDefault = CreateTrayIcon(Color.FromArgb(0x3F, 0x51, 0xB5)); // 靛蓝：默认/无数据
            _iconNormal = CreateTrayIcon(Color.FromArgb(0x2E, 0x9E, 0x4F));  // 绿：充足
            _iconLow = CreateTrayIcon(Color.FromArgb(0xD6, 0x45, 0x45));     // 红：不足
            _iconError = CreateTrayIcon(Color.FromArgb(0xE8, 0x93, 0x0C));   // 橙：异常

            _icon = new NotifyIcon
            {
                Icon = _iconDefault,
                Visible = true,
                Text = "DeepSeek 余额监控"
            };
            _currentIcon = _iconDefault;

            _menu = new ContextMenuStrip();

            _miShow = new ToolStripMenuItem("显示");
            _miShow.Visible = false; // 仅悬浮窗隐藏时出现
            _miShow.Click += (s, e) => _ctx.HideService.Restore();

            _miBalance = new ToolStripMenuItem("当前余额：--");
            _miBalance.Enabled = false;

            _miError = new ToolStripMenuItem("错误信息");
            _miError.Visible = false;
            _miError.Enabled = false;

            var miSettings = new ToolStripMenuItem("设置...");
            miSettings.Click += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

            var miReset = new ToolStripMenuItem("复位（悬浮窗回中央）");
            miReset.Click += (s, e) => _ctx.FloatWindow.ResetPosition();

            _miLock = new ToolStripMenuItem("锁定（点击穿透）");
            _miLock.Click += (s, e) => ToggleLock();

            _miTopmost = new ToolStripMenuItem("置顶");
            _miTopmost.Click += (s, e) => ToggleTopMost();

            _miAccounts = new ToolStripMenuItem("账户");

            var miStats = new ToolStripMenuItem("统计");
            miStats.Click += (s, e) => ShowStatsRequested?.Invoke(this, EventArgs.Empty);

            var miExit = new ToolStripMenuItem("退出");
            miExit.Click += (s, e) => _ctx.ExitApp();

            _menu.Items.Add(_miShow);
            _menu.Items.Add(_miBalance);
            _menu.Items.Add(_miError);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(miSettings);
            _menu.Items.Add(miReset);
            _menu.Items.Add(_miLock);
            _menu.Items.Add(_miTopmost);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_miAccounts);
            _menu.Items.Add(miStats);
            _menu.Items.Add(miExit);

            // 每次打开菜单前刷新各菜单项状态
            _menu.Opening += (s, e) => RefreshMenu();

            _icon.ContextMenuStrip = _menu;
            _icon.MouseClick += OnMouseClick;

            MenuTheme.Apply(_menu); // 右键菜单跟随系统主题
        }

        private void OnMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                // 左键单击：若悬浮窗隐藏则先恢复，再打开统计
                if (_ctx.HideService.IsHidden) _ctx.HideService.Restore();
                ShowStatsRequested?.Invoke(this, EventArgs.Empty);
            }
        }

        // ============ 状态更新 ============

        /// <summary>余额监控状态变化后调用：更新图标颜色、悬停气泡、菜单文字。</summary>
        public void UpdateFromMonitor()
        {
            var m = _ctx.Coordinator?.Current;
            if (m == null)
            {
                // 未配置账户：默认图标 + 暂无数据，不崩
                SetIcon(_iconDefault);
                _icon.Text = "DeepSeek 余额监控\n未配置账户";
                _miBalance.Text = "当前余额：暂无数据";
                _miError.Text = "";
                return;
            }

            switch (m.Status)
            {
                case BalanceStatus.Normal:
                    SetIcon(_iconNormal);
                    _icon.Text = "DeepSeek 余额监控\n余额：¥" + m.Balance.Value.ToString("F2") + "（充足）";
                    break;

                case BalanceStatus.Low:
                    SetIcon(_iconLow);
                    _icon.Text = "DeepSeek 余额监控\n余额：¥" + m.Balance.Value.ToString("F2") + "（不足！）";
                    break;

                case BalanceStatus.Error:
                    SetIcon(_iconError);
                    string err = (m.ErrorMessage ?? "查询出错").Replace("\n", " ");
                    _icon.Text = Shorten("DeepSeek 余额监控\n查询失败：" + err
                        + "（连续 " + m.ConsecutiveFailures + " 次）");
                    break;
            }

            _miBalance.Text = m.Balance.HasValue
                ? "当前余额：¥" + m.Balance.Value.ToString("F2")
                : "当前余额：暂无数据";

            _miError.Text = (m.ErrorMessage ?? "查询出错")
                + "（连续失败 " + m.ConsecutiveFailures + " 次）";
        }

        private void SetIcon(Icon icon)
        {
            if (_currentIcon == icon) return;
            _currentIcon = icon;
            _icon.Icon = icon;
        }

        /// <summary>NotifyIcon.Text 有 63 字符上限，超长截断并加省略号。</summary>
        private static string Shorten(string text, int max = 63)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        // ============ 菜单操作 ============

        private void ToggleLock()
        {
            var cfg = _ctx.Config;
            cfg.LockMode = !cfg.LockMode;
            _ctx.SaveConfig();
            _ctx.FloatWindow.ApplyLockMode();
        }

        private void ToggleTopMost()
        {
            var cfg = _ctx.Config;
            cfg.TopMost = !cfg.TopMost;
            _ctx.SaveConfig();
            _ctx.FloatWindow.TopMost = cfg.TopMost;
        }

        internal void RefreshMenu()
        {
            var cfg = _ctx.Config;
            var m = _ctx.Coordinator?.Current;

            _miBalance.Text = m != null && m.Balance.HasValue
                ? "当前余额：¥" + m.Balance.Value.ToString("F2")
                : "当前余额：暂无数据";

            // 「显示」项：仅悬浮窗隐藏时出现
            _miShow.Visible = _ctx.HideService.IsHidden;

            // 错误信息项：有错误时显示，无错误时隐藏
            _miError.Visible = m != null && m.Status == BalanceStatus.Error && !string.IsNullOrEmpty(m.ErrorMessage);
            if (_miError.Visible)
            {
                _miError.Text = (m.ErrorMessage ?? "查询出错")
                    + "（连续失败 " + m.ConsecutiveFailures + " 次）";
            }

            // 锁定 / 置顶：勾选状态 + 文字
            _miLock.Text = cfg.LockMode ? "解锁（取消点击穿透）" : "锁定（点击穿透）";
            _miLock.Checked = cfg.LockMode;
            _miTopmost.Checked = cfg.TopMost;

            // 「账户」子菜单：按配置账户重建，勾选当前显示账户；无/仅 1 个账户时隐藏
            _miAccounts.DropDownItems.Clear();
            foreach (var acc in _ctx.Config.Accounts)
            {
                var name = acc.Name;
                if (string.IsNullOrEmpty(name))
                    name = _ctx.Coordinator.Get(acc.Id)?.ProviderDisplayName ?? acc.ProviderId;
                var item = new ToolStripMenuItem(name) { Checked = acc.Id == _ctx.Config.ActiveAccountId };
                string aid = acc.Id;
                item.Click += (s, e) => _ctx.SwitchAccount(aid);
                _miAccounts.DropDownItems.Add(item);
            }
            _miAccounts.Visible = _ctx.Config.Accounts.Count > 1;
        }

        // ============ 图标绘制 ============

        /// <summary>
        /// 绘制托盘图标：深色渐变圆 + 状态色 ¥ 符号（48x48 高清，托盘缩放不模糊）。
        /// ¥ 寓意"余额"，状态色（绿/红/橙/靛蓝）表达当前状态。
        /// </summary>
        private static Icon CreateTrayIcon(Color statusColor)
        {
            const int size = 48;
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // 深色渐变圆底（中心亮、边缘暗，有立体感）
                    var rect = new Rectangle(1, 1, size - 2, size - 2);
                    using (var path = new GraphicsPath())
                    {
                        path.AddEllipse(rect);
                        using (var brush = new PathGradientBrush(path)
                        {
                            CenterColor = Color.FromArgb(0x4A, 0x4A, 0x4A),
                            SurroundColors = new[] { Color.FromArgb(0x18, 0x18, 0x18) }
                        })
                        {
                            g.FillPath(brush, path);
                        }
                        // 细描边增强轮廓
                        using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x55), 1f))
                        {
                            g.DrawPath(pen, path);
                        }
                    }

                    // 状态色 ¥ 符号
                    using (var f = new Font("Segoe UI", 24f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    {
                        g.DrawString("¥", f, new SolidBrush(statusColor), new RectangleF(0, -1, size, size), sf);
                    }
                }
                IntPtr hIcon = bmp.GetHicon();
                try
                {
                    return (Icon)Icon.FromHandle(hIcon).Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public void Dispose()
        {
            _icon.Visible = false;
            _icon.Dispose();
            _menu.Dispose();
            _iconNormal?.Dispose();
            _iconLow?.Dispose();
            _iconError?.Dispose();
            _iconDefault?.Dispose();
        }
    }
}
