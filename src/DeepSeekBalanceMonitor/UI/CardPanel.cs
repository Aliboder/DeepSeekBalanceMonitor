using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 圆角卡片面板：深色/浅色主题自适应，支持标题 + 内容区。
    /// 替代 WinForms 默认 GroupBox，视觉更干净。
    /// </summary>
    public class CardPanel : Panel
    {
        private string _title = "";
        private int _cornerRadius = 10;

        // 颜色方案
        private static readonly Color DarkCardBg = Color.FromArgb(0x25, 0x25, 0x28);
        private static readonly Color DarkCardBorder = Color.FromArgb(0x3A, 0x3A, 0x3F);
        private static readonly Color DarkTitleFg = Color.FromArgb(0xAA, 0xAA, 0xAA);
        private static readonly Color LightCardBg = Color.FromArgb(0xF7, 0xF7, 0xF7);
        private static readonly Color LightCardBorder = Color.FromArgb(0xE0, 0xE0, 0xE0);
        private static readonly Color LightTitleFg = Color.FromArgb(0x88, 0x88, 0x88);

        private readonly bool _dark;

        public string Title
        {
            get => _title;
            set { _title = value; Invalidate(); }
        }

        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        public CardPanel(bool dark)
        {
            _dark = dark;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            BackColor = Color.Transparent;
            Padding = new Padding(16, 36, 16, 12);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);

            // 绘制圆角背景
            using (var path = RoundedRect(rect, _cornerRadius))
            {
                using (var brush = new SolidBrush(_dark ? DarkCardBg : LightCardBg))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(_dark ? DarkCardBorder : LightCardBorder, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 绘制标题
            if (!string.IsNullOrEmpty(_title))
            {
                var titleColor = _dark ? DarkTitleFg : LightTitleFg;
                using (var titleFont = new Font(Font.FontFamily, 8.5f, FontStyle.Bold))
                {
                    var titleRect = new Rectangle(16, 10, Width - 32, 20);
                    TextRenderer.DrawText(g, _title, titleFont, titleRect, titleColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }

            // 不调用 base.OnPaint，完全自绘
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // 透明背景，由父控件绘制
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
    }

    /// <summary>
    /// 卡片式按钮：圆角、hover 高亮、支持深色主题。
    /// </summary>
    public class CardButton : Button
    {
        private readonly bool _dark;
        private bool _hovered;
        private bool _pressed;

        // 颜色
        private static readonly Color DarkBtnBg = Color.FromArgb(0x33, 0x33, 0x38);
        private static readonly Color DarkBtnHover = Color.FromArgb(0x40, 0x40, 0x48);
        private static readonly Color DarkBtnPressed = Color.FromArgb(0x28, 0x28, 0x2C);
        private static readonly Color DarkBtnFg = Color.FromArgb(0xDD, 0xDD, 0xDD);
        private static readonly Color DarkBtnBorder = Color.FromArgb(0x50, 0x50, 0x55);

        private static readonly Color LightBtnBg = Color.FromArgb(0xE8, 0xE8, 0xE8);
        private static readonly Color LightBtnHover = Color.FromArgb(0xDB, 0xDB, 0xDB);
        private static readonly Color LightBtnPressed = Color.FromArgb(0xD0, 0xD0, 0xD0);
        private static readonly Color LightBtnFg = Color.FromArgb(0x33, 0x33, 0x33);
        private static readonly Color LightBtnBorder = Color.FromArgb(0xC0, 0xC0, 0xC0);

        // 强调色按钮（如"应用"）
        private static readonly Color AccentBg = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color AccentHover = Color.FromArgb(0x2B, 0x7D, 0xF5);
        private static readonly Color AccentPressed = Color.FromArgb(0x18, 0x5C, 0xC7);

        public bool IsAccent { get; set; }

        public CardButton(bool dark, string text, int width = 75, int height = 30)
        {
            _dark = dark;
            Text = text;
            Width = width;
            Height = height;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Font = new Font("Microsoft YaHei UI", 9f);
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hovered = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hovered = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _pressed = true;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _pressed = false;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            Color bgColor, fgColor, borderColor;
            if (IsAccent)
            {
                bgColor = _pressed ? AccentPressed : (_hovered ? AccentHover : AccentBg);
                fgColor = Color.White;
                borderColor = AccentPressed;
            }
            else if (_dark)
            {
                bgColor = _pressed ? DarkBtnPressed : (_hovered ? DarkBtnHover : DarkBtnBg);
                fgColor = DarkBtnFg;
                borderColor = DarkBtnBorder;
            }
            else
            {
                bgColor = _pressed ? LightBtnPressed : (_hovered ? LightBtnHover : LightBtnBg);
                fgColor = LightBtnFg;
                borderColor = LightBtnBorder;
            }

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = 6;

            using (var path = RoundedRect(rect, radius))
            {
                using (var brush = new SolidBrush(bgColor))
                {
                    g.FillPath(brush, path);
                }
                using (var pen = new Pen(borderColor, 1f))
                {
                    g.DrawPath(pen, path);
                }
            }

            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, fgColor, flags);
        }

        protected override void OnPaintBackground(PaintEventArgs e) { }

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
    }
}
