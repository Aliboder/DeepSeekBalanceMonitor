using System;
using System.Runtime.InteropServices;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 窗口标题栏跟随系统深浅主题（DWM 沉浸式深色模式）。
    /// Windows 10 1809+ / Windows 11 支持；旧系统静默忽略。
    /// </summary>
    public static class DarkTitleBar
    {
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_20H2 = 20; // Win11 / Win10 20H2+
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_1809 = 19; // Win10 1809 ~ 2004

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        /// <summary>按系统当前主题设置窗口标题栏颜色。</summary>
        public static void Apply(IntPtr hwnd)
        {
            Apply(hwnd, SystemTheme.IsDark());
        }

        /// <summary>设置窗口标题栏深浅色（dark=true 深色）。</summary>
        public static void Apply(IntPtr hwnd, bool dark)
        {
            if (hwnd == IntPtr.Zero) return;
            int value = dark ? 1 : 0;
            try
            {
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_20H2, ref value, sizeof(int));
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_1809, ref value, sizeof(int));
            }
            catch { /* 旧系统不支持时忽略 */ }
        }
    }
}
