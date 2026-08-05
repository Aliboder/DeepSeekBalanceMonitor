using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 余额走势图（自绘）：折线 + 网格 + 预警阈值虚线（红色标注）+ 起点/终点标注。
    /// </summary>
    public class BalanceChart : Control
    {
        private IReadOnlyList<BalanceRecord> _records = new List<BalanceRecord>();
        private decimal? _warnThreshold;

        // 绘图区边距：左（Y 轴刻度）、底（日期）、右上
        private const int PadLeft = 66;
        private const int PadBottom = 24;
        private const int PadTop = 24;
        private const int PadRight = 12;

        public BalanceChart()
        {
            // 深色主题（与 Windows 深色系统风格一致）
            BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
            SetStyle(ControlStyles.ResizeRedraw | ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Font = new Font("Microsoft YaHei UI", 9);
        }

        // 深色主题配色
        private static readonly Color LineColor = Color.FromArgb(0x4F, 0xA3, 0xFF);      // 亮蓝折线
        private static readonly Color GridColor = Color.FromArgb(0x36, 0x36, 0x36);       // 网格
        private static readonly Color TextColor = Color.FromArgb(0xA8, 0xA8, 0xA8);       // 刻度文字
        private static readonly Color WarnColor = Color.FromArgb(0xE0, 0x6C, 0x75);       // 预警红
        private static readonly Color LabelBgColor = Color.FromArgb(0x2D, 0x2D, 0x30);    // 标注底
        private static readonly Color EmptyColor = Color.FromArgb(0x80, 0x80, 0x80);      // 空数据

        /// <summary>更新数据并重绘。</summary>
        public void SetData(IReadOnlyList<BalanceRecord> records, decimal? warnThreshold)
        {
            _records = records ?? new List<BalanceRecord>();
            _warnThreshold = warnThreshold;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            if (_records.Count == 0)
            {
                TextRenderer.DrawText(g, "暂无数据，等待首次余额记录...", Font,
                    ClientRectangle, EmptyColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            var plot = new Rectangle(PadLeft, PadTop, Width - PadLeft - PadRight, Height - PadTop - PadBottom);
            if (plot.Width < 50 || plot.Height < 30) return;

            // —— 数据范围 ——
            decimal min = _records[0].Balance, max = _records[0].Balance;
            foreach (var r in _records)
            {
                if (r.Balance < min) min = r.Balance;
                if (r.Balance > max) max = r.Balance;
            }
            if (_warnThreshold.HasValue)
            {
                if (_warnThreshold.Value < min) min = _warnThreshold.Value;
                if (_warnThreshold.Value > max) max = _warnThreshold.Value;
            }
            decimal span = max - min;
            if (span < 1m) { min -= 0.5m; max += 0.5m; span = 1m; }
            min -= span * 0.08m; max += span * 0.08m;
            span = max - min;

            float X(int i) => plot.Left + (float)(_records.Count == 1 ? 0 : (double)i / (_records.Count - 1) * plot.Width);
            float Y(decimal v) => plot.Bottom - (float)((v - min) / span) * plot.Height;

            // —— 网格 + Y 轴刻度 ——
            using (var gridPen = new Pen(GridColor))
            using (var textBrush = new SolidBrush(TextColor))
            {
                const int gridLines = 4;
                for (int i = 0; i <= gridLines; i++)
                {
                    float gy = plot.Top + plot.Height * i / gridLines;
                    g.DrawLine(gridPen, plot.Left, gy, plot.Right, gy);
                    decimal v = max - span * i / gridLines;
                    // 刻度带 ¥ 单位，避免误读（如把 47.51 当成余额）
                    TextRenderer.DrawText(g, "¥" + v.ToString("F2"), Font,
                        new Rectangle(0, (int)gy - 8, PadLeft - 8, 16), textBrush.Color,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }
                // 底部日期（起点/终点）
                TextRenderer.DrawText(g, _records[0].Time.ToString("MM-dd HH:mm"), Font,
                    new Rectangle(plot.Left, plot.Bottom + 4, plot.Width / 2, 16), textBrush.Color,
                    TextFormatFlags.Left);
                TextRenderer.DrawText(g, _records[_records.Count - 1].Time.ToString("MM-dd HH:mm"), Font,
                    new Rectangle(plot.Left + plot.Width / 2, plot.Bottom + 4, plot.Width / 2, 16), textBrush.Color,
                    TextFormatFlags.Right);
            }

            // —— 预警阈值虚线（红色 + 标注） ——
            if (_warnThreshold.HasValue)
            {
                float wy = Y(_warnThreshold.Value);
                using (var warnPen = new Pen(WarnColor, 1.2f))
                {
                    warnPen.DashStyle = DashStyle.Dash;
                    g.DrawLine(warnPen, plot.Left, wy, plot.Right, wy);
                }
                using (var bg = new SolidBrush(WarnColor))
                {
                    var label = "预警 ¥" + _warnThreshold.Value.ToString("F2");
                    var size = TextRenderer.MeasureText(label, Font);
                    var rect = new Rectangle(plot.Right - size.Width - 8, (int)wy - size.Height - 6, size.Width + 6, size.Height + 2);
                    if (rect.Top < plot.Top) rect.Y = plot.Top;
                    g.FillRectangle(bg, rect);
                    TextRenderer.DrawText(g, label, Font, rect, Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
            }

            // —— 余额折线 ——
            using (var linePen = new Pen(LineColor, 1.8f))
            {
                if (_records.Count == 1)
                {
                    g.DrawEllipse(linePen, X(0) - 2, Y(_records[0].Balance) - 2, 5, 5);
                }
                else
                {
                    var pts = new PointF[_records.Count];
                    for (int i = 0; i < _records.Count; i++)
                        pts[i] = new PointF(X(i), Y(_records[i].Balance));
                    g.DrawLines(linePen, pts);
                }
            }

            // —— 起点 / 终点标注（余额数值） ——
            var first = _records[0];
            var last = _records[_records.Count - 1];
            DrawPointLabel(g, first.Balance, X(0), Y(first.Balance), plot, alignLeft: true);
            DrawPointLabel(g, last.Balance, X(_records.Count - 1), Y(last.Balance), plot, alignLeft: false);
        }

        /// <summary>绘制数据点圆形 + 数值标签。</summary>
        private void DrawPointLabel(Graphics g, decimal value, float x, float y, Rectangle plot, bool alignLeft)
        {
            using (var dotBrush = new SolidBrush(LineColor))
            {
                g.FillEllipse(dotBrush, x - 3, y - 3, 6, 6);
            }

            var label = "¥" + value.ToString("F2");
            var size = TextRenderer.MeasureText(label, Font);
            int labelW = size.Width + 8, labelH = size.Height + 2;

            float lx = alignLeft ? x + 6 : x - 6 - labelW;
            if (lx < plot.Left) lx = plot.Left;
            if (lx + labelW > plot.Right) lx = plot.Right - labelW;
            float ly = y - labelH - 4;
            if (ly < plot.Top) ly = y + 6;

            using (var bg = new SolidBrush(LabelBgColor))
            using (var border = new Pen(LineColor))
            {
                g.FillRectangle(bg, lx, ly, labelW, labelH);
                g.DrawRectangle(border, lx, ly, labelW, labelH);
                TextRenderer.DrawText(g, label, Font,
                    new Rectangle((int)lx, (int)ly, labelW, labelH),
                    LineColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }
    }
}
