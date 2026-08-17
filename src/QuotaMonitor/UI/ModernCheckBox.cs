using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 自绘复选框：圆角方框 + 蓝色填充勾选。替代 WinForms CheckBox。
    /// </summary>
    public class ModernCheckBox : Control
    {
        public event EventHandler<bool> CheckedChanged;

        private readonly bool _dark;
        private bool _checked;
        private bool _hovered;

        private static readonly Color AccentBlue = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color DarkBorder = Color.FromArgb(0x5A, 0x5A, 0x60);
        private static readonly Color LightBorder = Color.FromArgb(0xAA, 0xAA, 0xAE);
        private static readonly Color DarkFg = Color.FromArgb(0xBB, 0xBB, 0xBB);
        private static readonly Color LightFg = Color.FromArgb(0x55, 0x55, 0x55);

        public bool Checked
        {
            get => _checked;
            set
            {
                if (_checked == value) return;
                _checked = value;
                Invalidate();
                CheckedChanged?.Invoke(this, value);
            }
        }

        public ModernCheckBox(bool dark, string text)
        {
            _dark = dark;
            Text = text;
            AutoSize = true;
            Height = 24;
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left) Checked = !Checked;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var boxRect = new Rectangle(0, (Height - 18) / 2, 18, 18);

            // 方框
            using (var path = RoundedRect(boxRect, 4))
            {
                if (_checked)
                {
                    using (var brush = new SolidBrush(AccentBlue))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(AccentBlue, 1f))
                    {
                        g.DrawPath(pen, path);
                    }

                    // 勾选标记
                    using (var pen = new Pen(Color.White, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                    {
                        g.DrawLines(pen, new PointF[]
                        {
                            new PointF(boxRect.X + 4, boxRect.Y + 9),
                            new PointF(boxRect.X + 8, boxRect.Y + 13),
                            new PointF(boxRect.X + 14, boxRect.Y + 5)
                        });
                    }
                }
                else
                {
                    var borderColor = _hovered ? AccentBlue : (_dark ? DarkBorder : LightBorder);
                    using (var brush = new SolidBrush(_dark ? Color.FromArgb(0x2D, 0x2D, 0x30) : Color.White))
                    {
                        g.FillPath(brush, path);
                    }
                    using (var pen = new Pen(borderColor, 1.2f))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            // 文字
            var fg = _dark ? DarkFg : LightFg;
            TextRenderer.DrawText(g, Text, Font,
                new Rectangle(26, 0, Width - 26, Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            var size = TextRenderer.MeasureText(Text, Font);
            return new Size(size.Width + 32, Math.Max(size.Height + 6, 24));
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
}