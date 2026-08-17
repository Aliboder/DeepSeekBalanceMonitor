using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 图标工厂：深色渐变圆底 + 圆弧仪表 + 白色 Q。
    /// 同一逻辑生成托盘 4 色状态图标与 app.ico（tools/make-icon.ps1 保持同构图）。
    /// 圆弧用状态色渐变（托盘动态语义），Q 恒为白色（品牌稳定）。
    /// </summary>
    public static class IconFactory
    {
        public static Icon Create(int size, Color arcColor)
        {
            using (var bmp = new Bitmap(size, size))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

                    // 深色渐变圆底（中心亮、边缘暗）
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
                        using (var pen = new Pen(Color.FromArgb(0x55, 0x55, 0x55), 1f))
                        {
                            g.DrawPath(pen, path);
                        }
                    }

                    // 圆弧仪表：右上 315° 起顺时针扫 270°（左上开口），状态色→亮色渐变
                    float r = size * 0.34f;
                    float w = Math.Max(2f, size * 0.09f);
                    var arcRect = new RectangleF(size / 2f - r, size / 2f - r, r * 2, r * 2);
                    using (var gb = new LinearGradientBrush(arcRect, Lighten(arcColor, 0.35f), arcColor, 90f))
                    using (var pen = new Pen(gb, w))
                    {
                        g.DrawArc(pen, arcRect, -45f, 270f);
                    }

                    // 白色 Q
                    using (var f = new Font("Segoe UI", size * 0.52f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    using (var b = new SolidBrush(Color.White))
                    {
                        g.DrawString("Q", f, b, new RectangleF(0, -1, size, size), sf);
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

        private static Color Lighten(Color c, float t)
        {
            return Color.FromArgb(c.A,
                (int)(c.R + (255 - c.R) * t),
                (int)(c.G + (255 - c.G) * t),
                (int)(c.B + (255 - c.B) * t));
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);
    }
}