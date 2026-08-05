using System;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 开机自启：通过当前用户注册表 Run 键实现（无需管理员权限）。
    /// </summary>
    public static class AutoStartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "DeepSeekBalanceMonitor";

        /// <summary>设置/取消开机自启。</summary>
        public static void SetEnabled(bool enabled)
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) return;
                    if (enabled)
                    {
                        key.SetValue(AppName, "\"" + Application.ExecutablePath + "\"");
                    }
                    else
                    {
                        key.DeleteValue(AppName, false);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Current?.Error("设置开机自启失败: ", ex);
            }
        }

        /// <summary>当前是否已注册开机自启。</summary>
        public static bool IsEnabled()
        {
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKeyPath))
                {
                    return key?.GetValue(AppName) != null;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>启动时同步注册表状态（防止安装位置变化后自启失效）。</summary>
        public static void Sync(Config cfg)
        {
            bool registered = IsEnabled();
            if (cfg.AutoStart && !registered) SetEnabled(true);
            if (!cfg.AutoStart && registered) SetEnabled(false);
        }
    }
}
