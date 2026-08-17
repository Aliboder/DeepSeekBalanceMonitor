using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 自绘滑杆：细轨道 + 蓝色填充 + 圆形滑块。替代 WinForms TrackBar。
    /// </summary>
    public class ModernSlider : Control
    {
        public event EventHandler<int> ValueChanged;
        public event EventHandler DragEnded;

        private readonly bool _dark;
        private int _min, _max, _value;
        private bool _dragging;

        // 配色
        private static readonly Color AccentBlue = Color.FromArgb(0x1F, 0x6F, 0xEB);
        private static readonly Color DarkTrack = Color.FromArgb(0x3A, 0x3A, 0x40);
        private static readonly Color LightTrack = Color.FromArgb(0xD5, 0xD5, 0xD9);
        private static readonly Color DarkKnob = Color.FromArgb(0xE8, 0xE8, 0xE8);
        private static readonly Color LightKnob = Color.White;

        public int Minimum { get => _min; set { _min = value; Invalidate(); } }
        public int Maximum { get => _max; set { _max = value; Invalidate(); } }

        public int Value
        {
            get => _value;
            set
            {
                int v = Math.Max(_min, Math.Min(_max, value));
                if (v == _value) return;
                _value = v;
                Invalidate();
                ValueChanged?.Invoke(this, v);
            }
        }

        public ModernSlider(bool dark, int min, int max, int value)
        {
            _dark = dark;
            _min = min;
            _max = max;
            _value = value;
            Height = 28;
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.Selectable,
                true);
        }

        private float KnobX()
        {
            if (_max <= _min) return 12;
            float ratio = (float)(_value - _min) / (_max - _min);
            return 12 + ratio * (Width - 24);
        }

        private int ValueFromX(float x)
        {
            if (x <= 12) return _min;
            if (x >= Width - 12) return _max;
            float ratio = (x - 12) / (Width - 24);
            return _min + (int)Math.Round(ratio * (_max - _min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            _dragging = true;
            Value = ValueFromX(e.X);
            Capture = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_dragging) Value = ValueFromX(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_dragging)
            {
                _dragging = false;
                Capture = false;
                DragEnded?.Invoke(this, EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int cy = Height / 2;

            // 轨道
            var trackRect = new Rectangle(12, cy - 3, Width - 24, 6);
            using (var trackBrush = new SolidBrush(_dark ? DarkTrack : LightTrack))
            using (var path = RoundedRect(trackRect, 3))
            {
                g.FillPath(trackBrush, path);
            }

            // 已填充部分
            float knobX = KnobX();
            if (knobX > 14)
            {
                var fillRect = new Rectangle(12, cy - 3, (int)(knobX - 12), 6);
                using (var fillBrush = new SolidBrush(AccentBlue))
                using (var path = RoundedRect(fillRect, 3))
                {
                    g.FillPath(fillBrush, path);
                }
            }

            // 滑块
            using (var knobBrush = new SolidBrush(_dark ? DarkKnob : LightKnob))
            {
                g.FillEllipse(knobBrush, knobX - 7, cy - 7, 14, 14);
            }
            using (var pen = new Pen(AccentBlue, 1.5f))
            {
                g.DrawEllipse(pen, knobX - 7, cy - 7, 14, 14);
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