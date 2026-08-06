using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 桌面悬浮窗：始终置顶显示余额，颜色随状态变化，支持拖动/穿透/悬停增亮。
    /// </summary>
    public class FloatingWindow : Form
    {
        private readonly AppContext _ctx;

        // 显示内容（自绘，避免子控件拦截鼠标事件）
        private string _balanceText = "⚠ 查询中";
        private Color _balanceTextColor = Color.FromArgb(0x4A, 0xDE, 0x80);
        private Font _balanceFont;

        // 拖动状态
        private bool _dragging;
        private Point _dragOffset;

        /// <summary>双击请求打开统计面板。</summary>
        public event EventHandler OpenStatsRequested;

        /// <summary>右键菜单请求打开设置。</summary>
        public event EventHandler OpenSettingsRequested;

        public FloatingWindow(AppContext ctx)
        {
            _ctx = ctx;

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;

            // 圆角：用 Region 裁剪实现（不依赖颜色透明，与 Opacity 透明度完全兼容）。
            // 注意：不能用 TransparencyKey 方案——窗口透明度 <100% 时透明键失效，圆角显示异常
            BackColor = Color.Black;

            // 内容全部由 OnPaint 自绘，不添加任何子控件——
            // 子控件会拦截鼠标事件，导致窗体无法拖动/点击

            BuildMenu();
            ApplyConfig();
            UpdateDisplay();
        }

        // ============ 外观与配置 ============

        /// <summary>配置变化后调用：字号、透明度、位置、置顶、穿透全部即时生效。</summary>
        public void ApplyConfig()
        {
            var cfg = _ctx.Config;

            if (_balanceFont != null) _balanceFont.Dispose();
            _balanceFont = new Font("Microsoft YaHei UI", cfg.FontSize, FontStyle.Bold);

            UpdateSize();

            TopMost = cfg.TopMost;

            // 位置：记忆的位置；无记忆时屏幕中央
            if (cfg.FloatPosition.HasValue)
            {
                Location = ClampToScreen(cfg.FloatPosition.Value);
            }
            else
            {
                var area = Screen.PrimaryScreen.WorkingArea;
                Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
            }

            // 鼠标离开时的暗度立即生效
            UpdateOpacity(_mouseInside);

            ApplyLockMode();
            Refresh();
        }

        /// <summary>状态变化后调用：更新余额文字、颜色、详情。</summary>
        public void UpdateDisplay()
        {
            var m = _ctx.Monitor;

            switch (m.Status)
            {
                case BalanceStatus.Normal:
                    _balanceText = "¥ " + m.Balance.Value.ToString("F2");
                    _balanceTextColor = Color.FromArgb(0x4A, 0xDE, 0x80); // 绿：充足
                    break;

                case BalanceStatus.Low:
                    _balanceText = "¥ " + m.Balance.Value.ToString("F2");
                    _balanceTextColor = Color.FromArgb(0xFF, 0x6B, 0x6B); // 红：不足
                    break;

                default: // Error：橙色，保留最后余额，带 ⚠（错误原因见托盘气泡）
                    _balanceText = m.Balance.HasValue ? "⚠ ¥ " + m.Balance.Value.ToString("F2") : "⚠ 查询失败";
                    _balanceTextColor = Color.FromArgb(0xFF, 0xA9, 0x4D); // 橙：异常
                    break;
            }

            UpdateSize();
            Refresh();
        }

        /// <summary>
        /// 极限紧凑：窗口只比文字大一点点（左右各 4px、上下各 3px）。
        /// 按实际文字测量结果计算，字号/DPI 无关，文字永远完整显示不截断。
        /// </summary>
        private void UpdateSize()
        {
            if (_balanceFont == null) return;

            var m = TextRenderer.MeasureText(_balanceText, _balanceFont);
            int w = Math.Max(m.Width + 8, 46);
            int h = m.Height + 6; // 上下各 3px

            if (ClientSize.Width != w || ClientSize.Height != h)
            {
                ClientSize = new Size(w, h);
                // 尺寸变化后确保仍在屏幕内
                Location = ClampToScreen(Location);
            }
        }

        // ============ 圆角绘制 ============

        // 深色背景（固定），状态色体现在文字上
        private Color _backgroundColor = Color.FromArgb(0x16, 0x16, 0x16);
        private Color _backgroundColorTop = Color.FromArgb(0x26, 0x26, 0x26);
        private int _cornerRadius = 16;

        /// <summary>窗口尺寸变化时更新圆角裁剪区域。</summary>
        private void UpdateRegion()
        {
            if (!IsHandleCreated) return;
            using (var path = RoundedRect(new Rectangle(0, 0, Width, Height), _cornerRadius))
            {
                Region = new Region(path);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRegion();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 深色胶囊背景：渐变 + 深灰描边，状态色由文字表达
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, _cornerRadius))
            {
                using (var brush = new LinearGradientBrush(rect, _backgroundColorTop, _backgroundColor, 90f))
                {
                    e.Graphics.FillPath(brush, path);
                }
                // 深灰描边：深色底上轮廓清晰
                using (var pen = new Pen(Color.FromArgb(0x3F, 0x3F, 0x46), 1f))
                {
                    e.Graphics.DrawPath(pen, path);
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            // 余额文字：状态色（深色背景上直接清晰，无需阴影）
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            TextRenderer.DrawText(e.Graphics, _balanceText, _balanceFont, ClientRectangle, _balanceTextColor, flags);
        }

        private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, d, d, 180, 90);
            path.AddArc(bounds.Right - d, bounds.Y, d, d, 270, 90);
            path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
            path.AddArc(bounds.X, bounds.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        // ============ 悬停增亮 ============

        private bool _mouseInside;

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _mouseInside = true;
            UpdateOpacity(true);
            Refresh();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _mouseInside = false;
            UpdateOpacity(false);
            Refresh();
        }

        /// <summary>两档亮度：悬停 = 设置透明度，离开 = 再乘以暗度比例。</summary>
        private void UpdateOpacity(bool hovered)
        {
            var cfg = _ctx.Config;
            Opacity = hovered
                ? cfg.Opacity / 100.0
                : cfg.Opacity / 100.0 * cfg.IdleOpacity / 100.0;
        }

        // ============ 拖动与记住位置 ============

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left)
            {
                if (e.Clicks == 2)
                {
                    // 双击打开统计（不进入拖动），清除第一击可能残留的拖动状态
                    _dragging = false;
                    OpenStatsRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }
                _dragging = true;
                _dragOffset = new Point(MousePosition.X - Location.X, MousePosition.Y - Location.Y);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging)
            {
                Location = ClampToScreen(new Point(
                    MousePosition.X - _dragOffset.X,
                    MousePosition.Y - _dragOffset.Y));
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging && e.Button == MouseButtons.Left)
            {
                _dragging = false;
                _ctx.Config.FloatPosition = Location; // 松手自动记住
                _ctx.SaveConfig();
            }
        }

        /// <summary>恢复显示悬浮窗（定时隐藏到期/托盘手动恢复时调用）。</summary>
        public void ShowWindow()
        {
            if (!Visible) Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        }

        /// <summary>把悬浮窗移回屏幕中央（托盘「复位」功能）。</summary>
        public void ResetPosition()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            Location = new Point(area.X + (area.Width - Width) / 2, area.Y + (area.Height - Height) / 2);
            _ctx.Config.FloatPosition = Location;
            _ctx.SaveConfig();
        }

        /// <summary>把窗口位置限制在屏幕工作区内，防止拖丢。</summary>
        private Point ClampToScreen(Point p)
        {
            var area = Screen.FromPoint(p).WorkingArea;
            int x = Math.Min(Math.Max(p.X, area.X), area.Right - Math.Max(Width, 40));
            int y = Math.Min(Math.Max(p.Y, area.Y), area.Bottom - Math.Max(Height, 40));
            return new Point(x, y);
        }

        // ============ 点击穿透（锁定模式） ============

        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_TRANSPARENT = 0x20;

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        /// <summary>开启/关闭点击穿透（锁定模式）。</summary>
        public void ApplyLockMode()
        {
            if (!IsHandleCreated) return;
            int style = GetWindowLong(Handle, GWL_EXSTYLE);
            if (_ctx.Config.LockMode)
                SetWindowLong(Handle, GWL_EXSTYLE, style | WS_EX_TRANSPARENT);
            else
                SetWindowLong(Handle, GWL_EXSTYLE, style & ~WS_EX_TRANSPARENT);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyLockMode();
            UpdateRegion();
        }

        // ============ 右键菜单 ============

        private ContextMenuStrip _menu;
        private ToolStripMenuItem _menuSettings;
        private ToolStripMenuItem _menuHide;
        private ToolStripMenuItem _menuExit;

        private void BuildMenu()
        {
            _menu = new ContextMenuStrip();

            _menuSettings = new ToolStripMenuItem("设置...", null, (s, e) =>
                OpenSettingsRequested?.Invoke(this, EventArgs.Empty));

            // 「隐藏」子菜单（5/30 分钟、3 小时、自定义），定时隐藏功能在后续模块实现，先接入事件
            var hideItems = new ToolStripMenuItem("隐藏");
            hideItems.DropDownItems.Add(new ToolStripMenuItem("5 分钟", null, (s, e) => HideFor(5)));
            hideItems.DropDownItems.Add(new ToolStripMenuItem("30 分钟", null, (s, e) => HideFor(30)));
            hideItems.DropDownItems.Add(new ToolStripMenuItem("3 小时", null, (s, e) => HideFor(180)));
            hideItems.DropDownItems.Add(new ToolStripMenuItem("自定义时长...", null, (s, e) => HideCustom()));
            _menuHide = hideItems;

            _menuExit = new ToolStripMenuItem("退出", null, (s, e) => _ctx.ExitApp());

            _menu.Items.Add(_menuSettings);
            _menu.Items.Add(_menuHide);
            _menu.Items.Add(new ToolStripSeparator());
            _menu.Items.Add(_menuExit);

            MenuTheme.Apply(_menu); // 右键菜单跟随系统主题

            ContextMenuStrip = _menu;
        }

        /// <summary>定时隐藏（TimerService 在后续模块实现，此事件先占位）。</summary>
        public event EventHandler<int> HideRequested;

        private void HideFor(int minutes)
        {
            HideRequested?.Invoke(this, minutes);
        }

        private void HideCustom()
        {
            // 自定义 1~600 分钟
            var input = new InputBox("定时隐藏", "隐藏时长（分钟，1~600）：", "30");
            if (input.ShowDialog(this) == DialogResult.OK)
            {
                int minutes;
                if (int.TryParse(input.Value, out minutes) && minutes >= 1 && minutes <= 600)
                {
                    HideRequested?.Invoke(this, minutes);
                }
                else
                {
                    MessageBox.Show(this, "请输入 1~600 之间的整数分钟。", "定时隐藏", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }

    /// <summary>简单的单行输入对话框（供自定义隐藏时长使用）。</summary>
    public class InputBox : Form
    {
        public string Value { get; private set; }
        private readonly TextBox _input;

        public InputBox(string title, string prompt, string defaultValue)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(280, 110);

            var dark = Core.SystemTheme.IsDark();
            if (dark)
            {
                BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
                ForeColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
            }
            var lbl = new Label { Text = prompt, Location = new Point(12, 10), AutoSize = true };
            _input = new TextBox { Location = new Point(12, 36), Width = 256, Text = defaultValue };
            if (dark)
            {
                _input.BackColor = Color.FromArgb(0x2D, 0x2D, 0x30);
                _input.ForeColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
            }
            var ok = new Button { Text = "确定", DialogResult = DialogResult.OK, Location = new Point(100, 70), Width = 75 };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(190, 70), Width = 75 };

            ok.Click += (s, e) => Value = _input.Text;
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(lbl);
            Controls.Add(_input);
            Controls.Add(ok);
            Controls.Add(cancel);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle); // 标题栏跟随系统主题
        }
    }
}
