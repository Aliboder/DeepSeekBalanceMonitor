using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 左侧导航栏：4 个导航项（显示/提醒/密钥/其他），选中项蓝色高亮。
    /// </summary>
    public class NavSidebar : Panel
    {
        public event EventHandler<int> ItemSelected;

        private readonly bool _dark;
        private readonly string[] _items = { "显示", "提醒", "密钥", "其他" };
        private int _selectedIndex;
        private int _hoverIndex = -1;

        // 配色
        private static readonly Color DarkSideBg = Color.FromArgb(0x20, 0x20, 0x24);
        private static readonly Color LightSideBg = Color.FromArgb(0xE9, 0xE9, 0xEC);
        private static readonly Color AccentBlue = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color AccentLight = Color.FromArgb(0xE3, 0xEE, 0xFC);

        private static readonly Color DarkItemFg = Color.FromArgb(0xBB, 0xBB, 0xBB);
        private static readonly Color DarkItemFgSel = Color.White;
        private static readonly Color DarkHoverBg = Color.FromArgb(0x2E, 0x2E, 0x33);
        private static readonly Color LightItemFg = Color.FromArgb(0x55, 0x55, 0x55);
        private static readonly Color LightItemFgSel = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color LightHoverBg = Color.FromArgb(0xDD, 0xDD, 0xE0);

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value) return;
                _selectedIndex = value;
                Invalidate();
                ItemSelected?.Invoke(this, value);
            }
        }

        public NavSidebar(bool dark)
        {
            _dark = dark;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw |
                ControlStyles.Selectable,
                true);
            BackColor = _dark ? DarkSideBg : LightSideBg;
            Width = 110;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int idx = HitTest(e.Y);
            if (idx != _hoverIndex)
            {
                _hoverIndex = idx;
                Invalidate();
            }
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hoverIndex = -1;
            Invalidate();
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = HitTest(e.Y);
            if (idx >= 0) SelectedIndex = idx;
        }

        private int HitTest(int y)
        {
            if (y < 0) return -1;
            int idx = y / ItemHeight;
            return idx < _items.Length ? idx : -1;
        }

        private const int ItemHeight = 56;
        private const int ItemMargin = 10;

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            for (int i = 0; i < _items.Length; i++)
            {
                int y = i * ItemHeight;
                var itemRect = new Rectangle(ItemMargin / 2, y + 6, Width - ItemMargin, ItemHeight - 12);

                // hover 背景
                if (i == _hoverIndex && i != _selectedIndex)
                {
                    using (var brush = new SolidBrush(_dark ? DarkHoverBg : LightHoverBg))
                    using (var path = RoundedRect(itemRect, 8))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // 选中背景
                if (i == _selectedIndex)
                {
                    using (var brush = new SolidBrush(_dark ? AccentBlue : AccentLight))
                    using (var path = RoundedRect(itemRect, 8))
                    {
                        g.FillPath(brush, path);
                    }
                }

                // 图标（简单线条图标）
                var iconColor = i == _selectedIndex
                    ? (_dark ? Color.White : AccentBlue)
                    : (_dark ? DarkItemFg : LightItemFg);
                DrawIcon(g, i, new Rectangle(18, y + 16, 24, 24), iconColor);

                // 文字
                var fg = i == _selectedIndex
                    ? (_dark ? DarkItemFgSel : LightItemFgSel)
                    : (_dark ? DarkItemFg : LightItemFg);
                using (var font = new Font(Font.FontFamily, 9.5f, FontStyle.Bold))
                {
                    TextRenderer.DrawText(g, _items[i], font,
                        new Rectangle(46, y, Width - 48, ItemHeight), fg,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                }
            }

            // 右侧分隔线
            using (var pen = new Pen(_dark ? Color.FromArgb(0x30, 0x30, 0x34) : Color.FromArgb(0xD0, 0xD0, 0xD4)))
            {
                g.DrawLine(pen, Width - 1, 0, Width - 1, Height);
            }
        }

        private void DrawIcon(Graphics g, int index, Rectangle r, Color color)
        {
            using (var pen = new Pen(color, 2f) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                switch (index)
                {
                    case 0: // 显示：眼睛
                        g.DrawEllipse(pen, r.X + 2, r.Y + 6, r.Width - 4, r.Height - 12);
                        g.DrawEllipse(pen, r.X + 7, r.Y + 9, r.Width - 14, r.Height - 18);
                        break;
                    case 1: // 提醒：铃铛
                        g.DrawArc(pen, r.X + 5, r.Y + 4, r.Width - 10, r.Height - 10, 200, 140);
                        g.DrawLine(pen, r.X + 4, r.Y + 14, r.X + r.Width - 4, r.Y + 14);
                        g.DrawEllipse(pen, r.X + 9, r.Y + 15, 6, 4);
                        break;
                    case 2: // 密钥：钥匙
                        g.DrawEllipse(pen, r.X + 3, r.Y + 5, 9, 9);
                        g.DrawLine(pen, r.X + 11, r.Y + 13, r.X + r.Width - 3, r.Y + r.Height - 3);
                        g.DrawLine(pen, r.X + r.Width - 8, r.Y + r.Height - 8, r.X + r.Width - 2, r.Y + r.Height - 4);
                        break;
                    case 3: // 其他：齿轮（简化为六边形）
                        var pts = new PointF[]
                        {
                            new PointF(r.X + r.Width/2f, r.Y + 1),
                            new PointF(r.X + r.Width - 2, r.Y + r.Height/2f - 3),
                            new PointF(r.X + r.Width/2f + 2, r.Y + r.Height - 1),
                            new PointF(r.X + 2, r.Y + r.Height/2f + 2)
                        };
                        g.DrawPolygon(pen, pts);
                        break;
                }
            }
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