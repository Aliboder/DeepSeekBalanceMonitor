using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// UI 元素定位调试工具（技术栈无关的通用方案）。
    /// 给控件树挂唯一标识 + Tooltip + 悬停高亮，并支持导出控件树快照，
    /// 用于定位元素重叠 / 遮挡 / 错位等问题。
    /// 开关：设置 → 其他 → 「界面调试模式」（Config.DebugMode），开启后重启应用生效。
    /// 参考 docs/UI元素定位调试通用方案.md
    /// </summary>
    public static class DebugProbe
    {

        /// <summary>默认 ToolTip（供无自身 ToolTip 的窗口使用）。</summary>
        private static readonly ToolTip DefaultTip = new ToolTip
        {
            InitialDelay = 0,
            ReshowDelay = 0,
            ShowAlways = true
        };

        /// <summary>递归为控件树挂唯一标识 + Tooltip（类型+序号+坐标+文本）+ 悬停高亮/还原。
        /// tip 参数传入目标窗口已有的 ToolTip 实例，避免同窗双 ToolTip 冲突。</summary>
        public static void Attach(Control root, bool dark, ToolTip tip = null)
        {
            var t = tip ?? DefaultTip;
            int seq = 0;
            void Walk(Control node)
            {
                foreach (Control c in node.Controls)
                {
                    string id = c.GetType().Name + "[" + seq++ + "]";
                    c.Name = id;
                    string hint = (c is Label lb && !string.IsNullOrEmpty(lb.Text))
                        ? " text='" + (lb.Text.Length > 12 ? lb.Text.Substring(0, 12) + ".." : lb.Text) + "'"
                        : "";
                    t.SetToolTip(c, id + " (" + c.Location.X + "," + c.Location.Y + " "
                        + c.Width + "x" + c.Height + ")" + hint);

                    var orig = c.BackColor;
                    c.MouseEnter += (s, e) =>
                    {
                        if (s is Control cc) cc.BackColor = dark ? Color.FromArgb(0x2A, 0x3B, 0x54) : Color.FromArgb(0xDB, 0xE7, 0xF8);
                    };
                    c.MouseLeave += (s, e) =>
                    {
                        if (s is Control cc) cc.BackColor = orig;
                    };
                    Walk(c);
                }
            }
            Walk(root);
        }

        /// <summary>导出控件树快照到临时目录（类型/标识/坐标/内容/可见性）。</summary>
        public static void Dump(Control root, string fileName)
        {
            try
            {
                var dir = Path.Combine(Path.GetTempPath(), "opencode");
                Directory.CreateDirectory(dir);
                var lines = new List<string>();
                void Walk(Control node, string indent)
                {
                    foreach (Control c in node.Controls)
                    {
                        string txt = c is Label lb ? "'" + lb.Text + "'" : c.GetType().Name;
                        lines.Add(indent + c.GetType().Name + " [" + c.Name + "] ("
                            + c.Location.X + "," + c.Location.Y + " " + c.Width + "x" + c.Height
                            + ") " + txt + " Visible=" + c.Visible);
                        Walk(c, indent + "  ");
                    }
                }
                Walk(root, "");
                File.WriteAllLines(Path.Combine(dir, fileName), lines);
            }
            catch { }
        }
    }
}
