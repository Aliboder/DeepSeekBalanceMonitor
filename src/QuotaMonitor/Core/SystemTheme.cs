using Microsoft.Win32;

namespace QuotaMonitor.Core
{
    /// <summary>系统深浅色主题检测（Windows 10/11 设置 → 个性化 → 颜色）。</summary>
    public static class SystemTheme
    {
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
