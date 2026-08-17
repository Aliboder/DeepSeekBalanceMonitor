using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using QuotaMonitor.Core;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 统计面板：DeepSeek 余额卡 + OpenCode Go 套餐卡 + 近 14 天每日消费柱状图。
    /// 全部使用原生控件（Label / ProgressBar / Panel）布局——文字与布局交给 WinForms 管理，
    /// 任何 DPI 缩放下都自洽，不自绘文字、不用手动坐标。
    /// </summary>
    public class StatsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly ToolTip _tip;
        private readonly TableLayoutPanel _layout;

        // —— 余额卡控件 ——
        private readonly Panel _balanceCard;
        private readonly Label _balanceMark, _balanceName, _balanceSub, _balanceBadge, _balanceAmount, _balanceDetail;

        // —— 套餐卡控件 ——
        private readonly Panel _subCard;
        private readonly Label _subMark, _subName, _subSub, _subBadge, _subEmpty;
        private readonly Label[] _winName = new Label[3];
        private readonly Label[] _winReset = new Label[3];
        private readonly Label[] _winPct = new Label[3];
        private readonly ColorProgressBar[] _winBar = new ColorProgressBar[3];

        // —— 柱状图控件 ——
        private readonly Panel _chartPanel;
        private readonly Panel[] _bars = new Panel[14];
        private readonly Label[] _barValues = new Label[14];
        private readonly Label[] _barDates = new Label[14];

        private decimal _todaySpent, _avgDaily;
        private decimal[] _dailyValues = new decimal[14];

        /// <summary>DPI 缩放因子（OnLoad 由 GetDpiForWindow 计算，LayoutBars 固定值按此缩放）。</summary>
        private float _scale = 1f;

        // 主题状态（跟随系统，Win11 配色）
        private bool _dark;
        private Color _bgColor, _fgColor, _panelColor, _gridLineColor, _mutedColor, _downColor, _upColor;
        private static readonly Color AccentBlue = Color.FromArgb(0x00, 0x67, 0xC0);

        /// <summary>数据指纹：余额/最后记录/阈值/套餐状态均未变化时跳过界面重建（防抖）。</summary>
        private string _dataFingerprint = "";

        /// <summary>按系统主题设置配色（Win11 风格）；主题变化时可重复调用。</summary>
        private void ApplyTheme()
        {
            if (_dark)
            {
                _bgColor = Color.FromArgb(0x20, 0x20, 0x20);
                _fgColor = Color.FromArgb(0xE0, 0xE0, 0xE0);
                _panelColor = Color.FromArgb(0x2D, 0x2D, 0x2D);
                _gridLineColor = Color.FromArgb(0x40, 0x40, 0x40);
                _mutedColor = Color.FromArgb(0x9D, 0x9D, 0x9D);
                _downColor = Color.FromArgb(0xF4, 0x87, 0x71);
                _upColor = Color.FromArgb(0x89, 0xD1, 0x85);
            }
            else
            {
                _bgColor = Color.White;
                _fgColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
                _panelColor = Color.FromArgb(0xF3, 0xF3, 0xF3);
                _gridLineColor = Color.FromArgb(0xE5, 0xE5, 0xE5);
                _mutedColor = Color.FromArgb(0x6E, 0x6E, 0x6E);
                _downColor = Color.FromArgb(0xC0, 0x39, 0x2B);
                _upColor = Color.FromArgb(0x1E, 0x84, 0x41);
            }
            BackColor = _bgColor;
            ForeColor = _fgColor;
            if (_layout != null) _layout.BackColor = _bgColor;
            if (_balanceCard != null) _balanceCard.BackColor = _bgColor;
            if (_subCard != null) _subCard.BackColor = _bgColor;
            if (_chartPanel != null) _chartPanel.BackColor = _bgColor;
            if (_winBar != null)
                foreach (var p in _winBar) if (p != null) p.ApplyTheme(_dark);
            ApplyLabelColors();
            Invalidate();
        }

        /// <summary>主题切换时按角色重设各 Label 颜色（badge/金额由 RefreshData 动态重设）。</summary>
        private void ApplyLabelColors()
        {
            if (_balanceName != null) _balanceName.ForeColor = _fgColor;
            if (_balanceSub != null) _balanceSub.ForeColor = _mutedColor;
            if (_balanceDetail != null) _balanceDetail.ForeColor = _mutedColor;
            if (_subName != null) _subName.ForeColor = _fgColor;
            if (_subSub != null) _subSub.ForeColor = _mutedColor;
            if (_subEmpty != null) _subEmpty.ForeColor = _mutedColor;
            for (int i = 0; i < 3; i++)
            {
                if (_winName[i] != null) _winName[i].ForeColor = _mutedColor;
                if (_winReset[i] != null) _winReset[i].ForeColor = _mutedColor;
                if (_winPct[i] != null) _winPct[i].ForeColor = _fgColor;
            }
            for (int i = 0; i < 14; i++)
            {
                if (_barValues[i] != null) _barValues[i].ForeColor = _mutedColor;
                if (_barDates[i] != null) _barDates[i].ForeColor = i == 13 ? _fgColor : _mutedColor;
            }
        }

        public StatsForm(AppContext ctx)
        {
            _ctx = ctx;

            Text = "统计 - QuotaMonitor";
            // 固定尺寸（最小大小），不允许调整
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            // 手动 DPI 缩放（AutoScaleMode.None）：SystemAware 下控件坐标是 96 逻辑基准，
            // OnLoad 里用 GetDpiForWindow（真实 DPI，DeviceDpi 恒 96 不可用）等比 Scale 整个控件树，
            // 控件尺寸与字体一起缩放，比例恒定不错位。
            AutoScaleMode = AutoScaleMode.None;
            Font = new Font("Microsoft YaHei UI", 9);
            _tip = new ToolTip { InitialDelay = 0, ReshowDelay = 0, ShowAlways = true };

            // 主题：跟随系统深浅色
            _dark = SystemTheme.IsDark();
            ApplyTheme();

            // 三板块布局：TableLayoutPanel 填满窗口（余额 / 柱状图 / 套餐，按内容比例自适应）
            _layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 3,
                BackColor = _bgColor,
                Padding = new Padding(0)
            };
            _layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 26f));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 33f));
            _layout.RowStyles.Add(new RowStyle(SizeType.Percent, 41f));
            Controls.Add(_layout);

            // ============ 余额卡（全原生控件 + 自绘圆角背景） ============
            _balanceCard = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                BackColor = _bgColor,
                Padding = new Padding(8, 6, 8, 6)
            };
            _balanceCard.Paint += (s, e) => PaintCard(e.Graphics, _balanceCard.ClientRectangle, _gridLineColor);
            _balanceMark = MakeMarkLabel("DS", Color.FromArgb(0x1F, 0x6F, 0xEB));
            _balanceCard.Controls.Add(_balanceMark);
            _balanceName = MakeLabel("DeepSeek", 10f, FontStyle.Bold, _fgColor, new Point(44, 4), new Size(240, 20));
            _balanceCard.Controls.Add(_balanceName);
            _balanceSub = MakeLabel("API 余额", 8.5f, FontStyle.Regular, _mutedColor, new Point(44, 24), new Size(240, 16));
            _balanceCard.Controls.Add(_balanceSub);
            _balanceBadge = MakeBadgeLabel(new Point(0, 10), new Size(70, 22));
            _balanceCard.Controls.Add(_balanceBadge);
            _balanceAmount = MakeLabel("--", 19f, FontStyle.Bold, _fgColor, new Point(10, 44), new Size(0, 32));
            _balanceAmount.Width = _balanceCard.ClientSize.Width - 20;
            _balanceAmount.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _balanceCard.Controls.Add(_balanceAmount);
            _balanceDetail = MakeLabel("", 8.5f, FontStyle.Regular, _mutedColor, new Point(10, 84), new Size(0, 18));
            _balanceDetail.Width = _balanceCard.ClientSize.Width - 20;
            _balanceDetail.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
            _balanceCard.Controls.Add(_balanceDetail);
            _layout.Controls.Add(_balanceCard, 0, 0);

            // ============ 套餐卡（全原生控件 + 自绘圆角背景） ============
            _subCard = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                BackColor = _bgColor,
                Padding = new Padding(8, 6, 8, 6)
            };
            _subCard.Paint += (s, e) => PaintCard(e.Graphics, _subCard.ClientRectangle, _gridLineColor);
            _subMark = MakeMarkLabel("GO", Color.FromArgb(0x00, 0xA6, 0x7D));
            _subCard.Controls.Add(_subMark);
            _subName = MakeLabel("Go 套餐", 10f, FontStyle.Bold, _fgColor, new Point(44, 4), new Size(240, 20));
            _subCard.Controls.Add(_subName);
            _subSub = MakeLabel("OpenCode Go", 8.5f, FontStyle.Regular, _mutedColor, new Point(44, 24), new Size(240, 16));
            _subCard.Controls.Add(_subSub);
            _subBadge = MakeBadgeLabel(new Point(0, 10), new Size(70, 22));
            _subCard.Controls.Add(_subBadge);
            int cardW = _subCard.ClientSize.Width; // 卡片客户区宽度（96 设计基准，Scale 后物理一致）
            // 无数据提示（仅套餐无数据时显示，此时三行窗口隐藏，互不冲突）；居中于卡片中部
            _subEmpty = MakeLabel("", 8.5f, FontStyle.Regular, _mutedColor, new Point(10, 92), new Size(cardW - 20, 20));
            _subEmpty.TextAlign = ContentAlignment.MiddleCenter;
            _subEmpty.Visible = false;
            _subCard.Controls.Add(_subEmpty);
            int[] winYs = { 42, 80, 118 };
            for (int i = 0; i < 3; i++)
            {
                // 第一行：名称 + 重置时间/倒计时
                _winName[i] = MakeLabel("", 8.5f, FontStyle.Regular, _mutedColor, new Point(10, winYs[i]), new Size(76, 18));
                _subCard.Controls.Add(_winName[i]);
                _winReset[i] = MakeLabel("", 7.5f, FontStyle.Regular, _mutedColor, new Point(90, winYs[i]), new Size(cardW - 100, 18));
                _winReset[i].Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                _subCard.Controls.Add(_winReset[i]);
                // 第二行：进度条（全宽，右侧留百分比位）+ 百分比紧随其后
                _winBar[i] = new ColorProgressBar(_dark, _panelColor)
                {
                    Location = new Point(10, winYs[i] + 24),
                    Height = 6,
                    Width = cardW - 20 - 66
                };
                _winBar[i].Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                _subCard.Controls.Add(_winBar[i]);
                _winPct[i] = MakeLabel("", 8.5f, FontStyle.Bold, _fgColor, new Point(cardW - 66, winYs[i] + 17), new Size(56, 18));
                _winPct[i].TextAlign = ContentAlignment.MiddleRight;
                _winPct[i].Anchor = AnchorStyles.Top | AnchorStyles.Right;
                _subCard.Controls.Add(_winPct[i]);
            }
            _layout.Controls.Add(_subCard, 0, 2);

            // ============ 近 14 天每日消费柱状图 ============
            _chartPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 3, 0, 3),
                BackColor = _bgColor,
                Padding = new Padding(8, 2, 8, 2)
            };
            _chartPanel.Paint += (s, e) => PaintCard(e.Graphics, _chartPanel.ClientRectangle, _gridLineColor);
            _chartPanel.Paint += PaintGrid;
            var chartTitle = MakeLabel("近 14 天每日消费（元）", 8.5f, FontStyle.Regular, _mutedColor, new Point(8, 0), new Size(240, 20));
            _chartPanel.Controls.Add(chartTitle);
            for (int i = 0; i < 14; i++)
            {
                _bars[i] = new Panel { BackColor = _bgColor };
                _chartPanel.Controls.Add(_bars[i]);
                _barValues[i] = MakeLabel("", 7.5f, FontStyle.Regular, _mutedColor, Point.Empty, new Size(0, 14));
                _barValues[i].TextAlign = ContentAlignment.MiddleCenter;
                _chartPanel.Controls.Add(_barValues[i]);
                _barDates[i] = MakeLabel("", 7.5f, FontStyle.Regular, _mutedColor, Point.Empty, new Size(0, 15));
                _barDates[i].TextAlign = ContentAlignment.MiddleCenter;
                _chartPanel.Controls.Add(_barDates[i]);
            }
            _chartPanel.Resize += (s, e) => LayoutBars();
            _layout.Controls.Add(_chartPanel, 0, 1);

            // 徽章位置（右对齐，随卡片宽度变化）
            _balanceBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _balanceBadge.Left = _balanceCard.ClientSize.Width - _balanceBadge.Width - 12;
            _subBadge.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _subBadge.Left = _subCard.ClientSize.Width - _subBadge.Width - 12;
            _balanceCard.Resize += (s, e) => _balanceBadge.Left = _balanceCard.ClientSize.Width - _balanceBadge.Width - 12;
            _subCard.Resize += (s, e) => _subBadge.Left = _subCard.ClientSize.Width - _subBadge.Width - 12;

            RefreshData();
            Shown += (s, e) => RefreshData();

            // 跟随系统深浅色切换（运行时自动同步）
            SystemTheme.Changed += (s, e) => ApplyTheme();

            // 调试模式（设置 → 其他 → 界面调试模式 开启，重启生效）：控件定位用
            if (_ctx.Config.DebugMode)
            {
                DebugProbe.Attach(_layout, _dark);
                DebugProbe.Dump(_layout, "stats_controls.txt");
            }
        }

        // ============ 控件工厂 ============

        /// <summary>卡片圆角背景（自绘纯图形，无文字，DPI 缩放安全）。</summary>
        private void PaintCard(Graphics g, Rectangle rect, Color borderColor)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var card = new Rectangle(1, 1, rect.Width - 2, rect.Height - 2);
            using (var path = RoundedRect(card, 10))
            {
                using (var b = new SolidBrush(_panelColor)) g.FillPath(b, path);
                using (var p = new Pen(borderColor, 1f)) g.DrawPath(p, path);
            }
        }

        /// <summary>柱状图 Y 轴刻度 + 水平网格线（刻度按最大消费金额自动取整，画在柱子后方）。</summary>
        private void PaintGrid(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            float sc = _scale;
            var rect = _chartPanel.ClientRectangle;

            int axisW = (int)(36 * sc);                 // 左侧刻度区宽度
            int top = (int)(40 * sc);
            int bottom = rect.Height - (int)(24 * sc);
            int plotH = Math.Max((int)(10 * sc), bottom - top);
            int left = axisW + (int)(6 * sc);

            decimal max = _dailyValues.Max();
            if (max <= 0) return;

            decimal step = NiceStep(max);
            if (step <= 0) return;
            decimal ymax = step * 3;

            using (var gridPen = new Pen(_dark ? Color.FromArgb(0x3A, 0x3A, 0x40) : Color.FromArgb(0xD8, 0xD8, 0xD8), 1f))
            using (var axisPen = new Pen(_mutedColor, 1f))
            {
                // Y 轴竖线
                g.DrawLine(axisPen, left, top, left, bottom);
                for (int i = 0; i <= 3; i++)
                {
                    decimal val = step * i;
                    float y = bottom - (float)(val / ymax) * plotH;
                    // 水平网格线：从 Y 轴贯穿到板块右边缘
                    g.DrawLine(gridPen, left, y, rect.Width - 4, y);
                    // 刻度标注（Y 轴左侧，右对齐）
                    string label = (val == 0 ? "0" : "¥" + FormatAmount(val));
                    TextRenderer.DrawText(g, label, _barValues[0].Font,
                        new Rectangle((int)(2 * sc), (int)y - (int)(8 * sc), axisW - (int)(4 * sc), (int)(16 * sc)),
                        _mutedColor,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
                }
            }
        }

        /// <summary>刻度步长：max/3 向上取整到 1/2/5×10ⁿ（保证 3×step ≥ max）。</summary>
        private static decimal NiceStep(decimal max)
        {
            if (max <= 0) return 0;
            double raw = (double)(max / 3m);
            double exp = Math.Floor(Math.Log10(raw));
            double f = raw / Math.Pow(10, exp);
            double nf;
            if (f <= 1) nf = 1;
            else if (f <= 2) nf = 2;
            else if (f <= 5) nf = 5;
            else nf = 10;
            return (decimal)(nf * Math.Pow(10, exp));
        }

        /// <summary>刻度金额显示格式：≥10 整数，其余一位小数。</summary>
        private static string FormatAmount(decimal v)
        {
            if (v >= 10) return v.ToString("F0");
            return v.ToString("F1");
        }

        /// <summary>重置倒计时文案："还剩 x 小时 x 分钟重置" / "还剩 x 分钟重置" / "已重置"。</summary>
        private static string FormatCountdown(DateTime resetsAt)
        {
            var remain = resetsAt - DateTime.Now;
            if (remain <= TimeSpan.Zero) return "已重置";
            if (remain.TotalHours >= 1)
                return "还剩 " + (int)remain.TotalHours + " 小时 " + remain.Minutes + " 分钟重置";
            if (remain.TotalMinutes >= 1)
                return "还剩 " + Math.Max(1, (int)remain.TotalMinutes) + " 分钟重置";
            return "即将重置";
        }

        private Label MakeLabel(string text, float fontSize, FontStyle style, Color color, Point location, Size size)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Microsoft YaHei UI", fontSize, style),
                ForeColor = color,
                Location = location,
                Size = size,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleLeft,
                BackColor = Color.Transparent
            };
            lbl.Tag = fontSize; // 设计字号（RestoreFonts 据此精确还原，不受 Scale 字体行为影响）
            // 记录颜色角色（主题切换时按角色重设；muted=次要文字，其余按主文字）
            lbl.AccessibleDescription = (color == _mutedColor) ? "muted" : "fg";
            return lbl;
        }

        private static Label MakeMarkLabel(string text, Color back)
        {
            var lbl = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = back,
                Location = new Point(10, 8),
                Size = new Size(26, 26),
                TextAlign = ContentAlignment.MiddleCenter
            };
            lbl.Tag = 8.5f;
            return lbl;
        }

        private static Label MakeBadgeLabel(Point location, Size size)
        {
            var lbl = new Label
            {
                Location = location,
                Size = size,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Microsoft YaHei UI", 7.5f),
                BackColor = Color.Transparent
            };
            lbl.Tag = 7.5f;
            // 圆角胶囊背景（自绘：背景 + 文字，物理基准一致）
            lbl.Paint += (s, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var rect = lbl.ClientRectangle;
                using (var path = RoundedRect(new Rectangle(0, 0, rect.Width - 1, rect.Height - 1), rect.Height / 2))
                using (var b = new SolidBrush(lbl.BackColor))
                {
                    g.FillPath(b, path);
                }
                TextRenderer.DrawText(g, lbl.Text, lbl.Font, rect, lbl.ForeColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            };
            return lbl;
        }

        /// <summary>直边进度条（原生外观；填充色随使用率从品牌绿平滑渐变到红）。</summary>
        private class ColorProgressBar : Control
        {
            private bool _dark;
            private int _value;

            public int Value
            {
                get => _value;
                set
                {
                    int v = Math.Max(0, Math.Min(100, value));
                    if (v == _value) return;
                    _value = v;
                    FillColor = ProgressColor(v / 100f);
                    Invalidate();
                }
            }

            public Color FillColor { get; private set; } = Color.FromArgb(0x00, 0xA6, 0x7D);

            public ColorProgressBar(bool dark, Color backColor)
            {
                _dark = dark;
                BackColor = backColor; // 与卡片背景一致，避免默认系统色形成黑条
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            }

            /// <summary>切换深浅色主题并重绘（系统主题变化时窗口调用）。</summary>
            public void ApplyTheme(bool dark)
            {
                if (_dark == dark) return;
                _dark = dark;
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var track = new Rectangle(0, 1, Width - 1, Height - 2);
                // 轨道（对齐设置页 ModernSlider）：圆角 + 轨道色可见
                using (var path = RoundedRect(track, 3))
                using (var b = new SolidBrush(_dark ? Color.FromArgb(0x3D, 0x3D, 0x3D) : Color.FromArgb(0xD9, 0xD9, 0xD9)))
                {
                    g.FillPath(b, path);
                }
                if (Value > 0)
                {
                    var fill = new Rectangle(0, 1, Math.Max(3, (int)(track.Width * Value / 100f)), track.Height);
                    using (var path = RoundedRect(fill, 3))
                    using (var b = new SolidBrush(FillColor))
                    {
                        g.FillPath(b, path);
                    }
                }
            }

            /// <summary>使用率 → 颜色：HSV 色相 165°（Go 品牌绿）→ 0°（红），饱和度/明度同步插值。</summary>
            private static Color ProgressColor(float t)
            {
                float hue = 165f * (1 - t);
                float sat = 1f - 0.32f * t;
                float val = 0.65f + 0.19f * t;
                return HsvToRgb(hue, sat, val);
            }

            private static Color HsvToRgb(float h, float s, float v)
            {
                float c = v * s;
                float x = c * (1 - Math.Abs((h / 60f) % 2 - 1));
                float m = v - c;
                float r = 0, g = 0, b = 0;
                if (h < 60) { r = c; g = x; }
                else if (h < 120) { r = x; g = c; }
                else if (h < 180) { g = c; b = x; }
                else if (h < 240) { g = x; b = c; }
                else if (h < 300) { r = x; b = c; }
                else { r = c; b = x; }
                return Color.FromArgb(
                    (int)((r + m) * 255), (int)((g + m) * 255), (int)((b + m) * 255));
            }
        }

        /// <summary>圆角路径。</summary>
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

        /// <summary>可拖动分隔条（已弃用：板块高度改为自适应，保留类定义以兼容引用）。</summary>
        private class VSplitter : Control
        {
            public VSplitter(Control target, bool dark) : base() { }
        }

        // ============ 柱状图布局（Resize 时重算，全部原生 Panel/Label） ============

        private void LayoutBars()
        {
            var rect = _chartPanel.ClientRectangle;
            float sc = _scale; // 固定值按 DPI 缩放（布局在 Scale 后的坐标体系中执行）

            // 布局：标题 0..20s / 数字缓冲 20s..40s / 绘图区（左侧刻度区 36s + 柱区）/ 日期区
            int axisW = (int)(36 * sc);                 // 左侧刻度区宽度（与 PaintGrid 一致）
            int left = axisW + (int)(6 * sc);           // 柱区起点
            int top = (int)(40 * sc);                   // 绘图区上沿
            int bottom = rect.Height - (int)(24 * sc);  // 日期区上沿
            int plotH = Math.Max((int)(10 * sc), bottom - top);
            int gap = (int)(4 * sc);
            float barW = (rect.Width - left - (int)(20 * sc) - gap * 13) / 14f;
            if (barW < 3 * sc) barW = 3 * sc;
            decimal max = _dailyValues.Max();
            // 柱高基准 = 刻度上限（nice 取整，与网格线一致），避免数据最大柱顶到绘图区顶部
            decimal ymax = 0;
            if (max > 0)
            {
                decimal step = NiceStep(max);
                ymax = step * 3;
            }

            for (int i = 0; i < 14; i++)
            {
                var v = _dailyValues[i];
                int h = v > 0 && ymax > 0 ? Math.Max((int)(2 * sc), (int)(v / ymax * plotH)) : 0;
                int x = left + i * (int)(barW + gap);
                var barColor = v > 0 ? Color.FromArgb(0x00, 0xA6, 0x7D)
                    : (_dark ? Color.FromArgb(0x2C, 0x2C, 0x2E) : Color.FromArgb(0xE4, 0xE4, 0xE8));
                _bars[i].Location = new Point(x, bottom - h);
                _bars[i].Size = new Size((int)barW, h);
                _bars[i].BackColor = barColor;

                // 金额数字：紧贴柱顶上方居中（AutoSize 透明背景，无背景块遮挡）
                if (v > 0)
                {
                    _barValues[i].Visible = true;
                    _barValues[i].Text = v.ToString("0.##");
                    _barValues[i].AutoSize = true;
                    _barValues[i].BackColor = Color.Transparent;
                    _barValues[i].ForeColor = _mutedColor;
                    _barValues[i].Location = new Point(
                        x + (int)(barW / 2) - _barValues[i].Width / 2,
                        bottom - h - (int)(20 * sc));
                }
                else
                {
                    _barValues[i].Visible = false;
                }

                // 悬停提示：日期 + 消费金额
                var day = DateTime.Today.AddDays(-(13 - i));
                _tip.SetToolTip(_bars[i], day.ToString("M月d日") + " · 消费 ¥" + v.ToString("F2"));

                // 日期标签
                _barDates[i].Text = day.Month + "/" + day.Day;
                _barDates[i].ForeColor = i == 13 ? _fgColor : _mutedColor;
                _barDates[i].Location = new Point(x - (int)(4 * sc), bottom + (int)(4 * sc));
                _barDates[i].Size = new Size((int)barW + (int)(8 * sc), (int)(15 * sc));
            }
        }

        // ============ 数据刷新 ============

        /// <summary>重新计算并刷新全部统计内容（打开面板/余额更新时调用）。</summary>
        public void RefreshData()
        {
            var history = _ctx.History;
            var records = history.Records;
            var monitor = _ctx.Monitor;
            var cfg = _ctx.Config;
            var sub = _ctx.SubMonitor;

            // 防抖：余额/最后记录/阈值/套餐状态均未变化时不重建界面
            string subFp = sub.ErrorMessage + "|" + (sub.Result == null ? "null"
                : string.Join(",", sub.Result.Windows.Select(w => w.Kind + ":" + w.UsedPercent + "@" + w.ResetsAt)));
            string fp = (records.Count > 0 ? records[records.Count - 1].Time.Ticks + "|" + records[records.Count - 1].Balance : "e")
                + "|" + monitor.Balance + "|" + cfg.WarnThreshold + "|" + monitor.Status + "|" + subFp + "|" + DateTime.Today.Ticks;
            if (fp == _dataFingerprint) return;
            _dataFingerprint = fp;

            // —— 余额卡 ——
            _balanceAmount.Text = monitor.Balance.HasValue
                ? (monitor.Status == BalanceStatus.Error ? "⚠ " : "") + "¥ " + monitor.Balance.Value.ToString("F2")
                : "--";
            _balanceAmount.ForeColor = monitor.Status == BalanceStatus.Normal ? _upColor
                : monitor.Status == BalanceStatus.Low ? _downColor
                : Color.FromArgb(0xFF, 0xA9, 0x4D);
            _todaySpent = history.TodaySpent();
            decimal total = history.TotalSpent();
            decimal avgAll = 0;
            if (records.Count > 0)
            {
                int daysAll = Math.Max(1, (DateTime.Today - records[0].Time.Date).Days + 1);
                avgAll = total / daysAll;
            }
            _avgDaily = avgAll;
            _balanceDetail.Text = "今日消费 ¥" + _todaySpent.ToString("F2") + "    日均消费 ¥" + _avgDaily.ToString("F2");
            ApplyBalanceBadge(monitor.Status, monitor.ErrorMessage);

            // —— 套餐卡 ——
            _subEmpty.Text = "";
            var windows = sub.Result == null ? null : sub.Result.Windows;
            bool hasData = windows != null && windows.Count > 0;
            for (int i = 0; i < 3; i++)
            {
                bool show = hasData && i < windows.Count;
                _winName[i].Visible = show;
                _winReset[i].Visible = show;
                _winPct[i].Visible = show;
                _winBar[i].Visible = show;
                if (!show) continue;
                var w = windows[i];
                _winName[i].Text = w.DisplayName;
                _winReset[i].Text = w.ResetsAt.HasValue
                    ? w.ResetsAt.Value.ToString("M-d HH:mm") + " 重置 · " + FormatCountdown(w.ResetsAt.Value)
                    : "";
                _winPct[i].Text = w.UsedPercent + "%";
                _winBar[i].Value = Math.Max(0, Math.Min(100, w.UsedPercent));
            }
            if (!hasData)
            {
                _subEmpty.Visible = true;
                _subEmpty.Text = !sub.IsConfigured ? "未配置 OpenCode Go API 密钥（设置 → 密钥）"
                    : string.IsNullOrEmpty(sub.ErrorMessage) ? "暂无套餐数据" : sub.ErrorMessage;
            }
            else
            {
                _subEmpty.Visible = false; // 有数据时隐藏提示，避免盖住三行窗口
            }
            ApplySubBadge(sub.IsConfigured, sub.Result, sub.ErrorMessage);

            // —— 柱状图 ——
            _dailyValues = history.DailySpent(14);
            LayoutBars();
        }

        private void ApplyBalanceBadge(BalanceStatus status, string error)
        {
            string text;
            Color color;
            if (status == BalanceStatus.Normal) { text = "正常"; color = _upColor; }
            else if (status == BalanceStatus.Low) { text = "不足"; color = _downColor; }
            else if ((error ?? "").Contains("尚未设置")) { text = "未配置"; color = _mutedColor; }
            else if ((error ?? "").Contains("限流")) { text = "限流"; color = Color.FromArgb(0xE8, 0x93, 0x0C); }
            else if ((error ?? "").Contains("密钥")) { text = "密钥无效"; color = _mutedColor; }
            else { text = "出错"; color = Color.FromArgb(0xFF, 0xA9, 0x4D); }
            SetBadge(_balanceBadge, text, color);
        }

        private void ApplySubBadge(bool configured, SubscriptionResult result, string error)
        {
            string text;
            Color color;
            if (!configured) { text = "未配置"; color = _mutedColor; }
            else if (result != null && string.IsNullOrEmpty(error)) { text = "正常"; color = _upColor; }
            else if ((error ?? "").Contains("限流")) { text = "限流"; color = Color.FromArgb(0xE8, 0x93, 0x0C); }
            else if ((error ?? "").Contains("密钥")) { text = "密钥无效"; color = _mutedColor; }
            else { text = "出错"; color = Color.FromArgb(0xFF, 0xA9, 0x4D); }
            SetBadge(_subBadge, text, color);
        }

        private void SetBadge(Label badge, string text, Color color)
        {
            badge.Text = text;
            badge.ForeColor = color;
            badge.BackColor = Color.FromArgb(_dark ? 45 : 25, color);
            badge.Invalidate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle);
        }

        /// <summary>窗口尺寸恢复与 DPI 等比缩放（GetDpiForWindow 为真实 DPI，DeviceDpi 恒 96 不可用）。</summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // 真实 DPI（SystemAware 下 GetDpiForWindow 返回系统 DPI，如 144）
            float s = GetDpiForWindow(Handle) / 96f;
            _scale = s;

            // 等比缩放整个控件树（控件尺寸 + 字体一起缩放，比例恒定）
            if (s > 1.01f) Scale(new SizeF(s, s));
            // 还原字体：SystemAware 下 GDI 已按物理 DPI 渲染文字（pt 自动放大），
            // Scale 再放大字体会导致文字比控件大 1.5 倍被截断，需还原
            RestoreFonts(this, s);

            // 窗体自身尺寸（Scale 不缩放 Form.Size，需手动）：固定为最小尺寸
            var wa = Screen.PrimaryScreen.WorkingArea;
            int minW = (int)(480 * s);
            int minH = (int)(480 * s);
            MinimumSize = new Size(minW, minH);
            Size = new Size(Math.Min(minW, wa.Width), Math.Min(minH, wa.Height));

            // 柱状图重排（ClientRectangle 已缩放）
            LayoutBars();
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd);

        /// <summary>递归将 Label 字体还原为设计字号（Tag 中保存；Scale 的字体缩放行为不可靠，统一按 Tag 重设）。</summary>
        private static void RestoreFonts(Control root, float s)
        {
            foreach (Control c in root.Controls)
            {
                if (c is Label lbl && lbl.Tag is float pt && lbl.Font != null)
                {
                    lbl.Font = new Font(lbl.Font.FontFamily, pt, lbl.Font.Style);
                }
                RestoreFonts(c, s);
            }
        }
    }
}
