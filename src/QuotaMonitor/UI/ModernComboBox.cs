using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 自绘下拉框：圆角边框 + 下拉箭头 + 弹出列表。替代 WinForms ComboBox。
    /// </summary>
    public class ModernComboBox : Control
    {
        public event EventHandler<int> SelectedIndexChanged;

        private readonly bool _dark;
        private readonly ListBox _list;
        private readonly ToolStripDropDown _dropDown;
        private readonly string[] _items;
        private int _selectedIndex = -1;
        private bool _hovered;

        private static readonly Color AccentBlue = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color DarkBg = Color.FromArgb(0x2D, 0x2D, 0x30);
        private static readonly Color LightBg = Color.White;
        private static readonly Color DarkBorder = Color.FromArgb(0x50, 0x50, 0x55);
        private static readonly Color LightBorder = Color.FromArgb(0xC0, 0xC0, 0xC4);
        private static readonly Color DarkFg = Color.FromArgb(0xDD, 0xDD, 0xDD);
        private static readonly Color LightFg = Color.FromArgb(0x33, 0x33, 0x33);

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex == value || value < 0 || value >= _items.Length) return;
                _selectedIndex = value;
                Invalidate();
                SelectedIndexChanged?.Invoke(this, value);
            }
        }

        public string SelectedText => _selectedIndex >= 0 ? _items[_selectedIndex] : "";

        public ModernComboBox(bool dark, string[] items, int selected)
        {
            _dark = dark;
            _items = items;
            _selectedIndex = selected >= 0 && selected < items.Length ? selected : 0;
            Height = 28;
            Cursor = Cursors.Hand;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.SupportsTransparentBackColor,
                true);
            BackColor = Color.Transparent;

            // 弹出列表
            _list = new ListBox
            {
                BorderStyle = BorderStyle.None,
                IntegralHeight = false,
                Font = new Font("Microsoft YaHei UI", 9f),
                BackColor = _dark ? Color.FromArgb(0x2D, 0x2D, 0x30) : Color.White,
                ForeColor = _dark ? DarkFg : LightFg
            };
            foreach (var item in items) _list.Items.Add(item);
            _list.SelectedIndex = _selectedIndex;
            _list.MouseClick += (s, e) =>
            {
                int idx = _list.IndexFromPoint(e.Location);
                if (idx >= 0) SelectedIndex = idx;
                _dropDown.Close();
            };

            _dropDown = new ToolStripDropDown();
            _dropDown.Padding = Padding.Empty;
            _dropDown.AutoSize = false;
            var host = new ToolStripControlHost(_list)
            {
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                AutoSize = false
            };
            _dropDown.Items.Add(host);
            _dropDown.Size = new Size(Width, Math.Min(28 * _items.Length + 8, 168));
            _list.Size = new Size(Width, _dropDown.Height - 8);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); _hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); _hovered = false; Invalidate(); }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button != MouseButtons.Left) return;
            _list.SelectedIndex = _selectedIndex;
            _dropDown.Show(this, new Point(0, Height + 1));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var path = RoundedRect(rect, 6))
            {
                using (var brush = new SolidBrush(_dark ? DarkBg : LightBg))
                {
                    g.FillPath(brush, path);
                }
                var borderColor = _hovered ? AccentBlue : (_dark ? DarkBorder : LightBorder);
                using (var pen = new Pen(borderColor, 1.2f))
                {
                    g.DrawPath(pen, path);
                }
            }

            // 文字
            var fg = _dark ? DarkFg : LightFg;
            TextRenderer.DrawText(g, SelectedText, Font,
                new Rectangle(10, 0, Width - 34, Height), fg,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);

            // 下拉箭头
            int cx = Width - 16;
            int cy = Height / 2;
            using (var pen = new Pen(_dark ? Color.FromArgb(0x88, 0x88, 0x8C) : Color.FromArgb(0x88, 0x88, 0x8C), 1.6f)
            { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                g.DrawLine(pen, cx - 5, cy - 2, cx, cy + 3);
                g.DrawLine(pen, cx, cy + 3, cx + 5, cy - 2);
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