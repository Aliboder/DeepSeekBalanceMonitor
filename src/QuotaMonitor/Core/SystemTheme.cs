using System;
using Microsoft.Win32;

namespace QuotaMonitor.Core
{
    /// <summary>系统深浅色主题检测与切换监听（Windows 10/11 设置 → 个性化 → 颜色）。</summary>
    public static class SystemTheme
    {
        /// <summary>系统深浅色主题变化时触发（窗口订阅后刷新配色，实现运行时自动切换）。</summary>
        public static event EventHandler Changed;

        static SystemTheme()
        {
            try
            {
                // 监听系统颜色/主题变化（切换深浅色时触发）
                SystemEvents.UserPreferenceChanged += (s, e) =>
                {
                    if (e.Category == UserPreferenceCategory.Color)
                        Changed?.Invoke(null, EventArgs.Empty);
                };
            }
            catch { }
        }

        /// <summary>当前系统是否为深色主题。</summary>
        public static bool IsDark()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    var v = key?.GetValue("AppsUseLightTheme");
                    return v is int i && i == 0;
                }
            }
            catch
            {
                return false; // 无法读取时按浅色处理
            }
        }
    }
}
