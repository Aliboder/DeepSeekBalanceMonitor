using System.Drawing;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 右键菜单主题：WinForms 菜单默认不跟随系统深浅色（始终白底），
    /// 深色系统下手动应用深色配色。
    /// </summary>
    public static class MenuTheme
    {
        /// <summary>按系统主题应用菜单配色。</summary>
        public static void Apply(ContextMenuStrip menu)
        {
            bool dark = Core.SystemTheme.IsDark();
            if (dark)
            {
                menu.Renderer = new DarkMenuRenderer();
                SetItemColors(menu.Items, Color.FromArgb(0x2D, 0x2D, 0x30), Color.FromArgb(0xE6, 0xE6, 0xE6));
            }
            else
            {
                menu.Renderer = new ToolStripProfessionalRenderer();
                SetItemColors(menu.Items, SystemColors.Menu, SystemColors.MenuText);
            }
        }

        private static void SetItemColors(ToolStripItemCollection items, Color bg, Color fg)
        {
            foreach (ToolStripItem item in items)
            {
                item.BackColor = bg;
                item.ForeColor = fg;
                if (item is ToolStripMenuItem mi && mi.HasDropDownItems)
                {
                    SetItemColors(mi.DropDownItems, bg, fg);
                }
            }
        }
    }

    /// <summary>深色菜单配色表。</summary>
    public class DarkMenuColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color MenuItemBorder => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color ToolStripDropDownBackground => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color MenuBorder => Color.FromArgb(0x3F, 0x3F, 0x46);
        public override Color ImageMarginGradientBegin => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color ImageMarginGradientEnd => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color SeparatorDark => Color.FromArgb(0x3F, 0x3F, 0x46);
        public override Color SeparatorLight => Color.FromArgb(0x3F, 0x3F, 0x46);
        public override Color MenuStripGradientBegin => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color MenuStripGradientEnd => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color ToolStripGradientBegin => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color ToolStripGradientMiddle => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color ToolStripGradientEnd => Color.FromArgb(0x2D, 0x2D, 0x30);
        public override Color CheckBackground => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color CheckSelectedBackground => Color.FromArgb(0x3E, 0x3E, 0x42);
        public override Color CheckPressedBackground => Color.FromArgb(0x3E, 0x3E, 0x42);
    }

    public class DarkMenuRenderer : ToolStripProfessionalRenderer
    {
        public DarkMenuRenderer() : base(new DarkMenuColorTable()) { }
    }
}
