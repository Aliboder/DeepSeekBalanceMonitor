using System;
using System.Threading;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor
{
    /// <summary>程序入口：单实例锁 + 全局应用生命周期。</summary>
    internal static class Program
    {
        /// <summary>全局互斥锁名称，防止软件重复启动。</summary>
        private static readonly string MutexName = "DeepSeekBalanceMonitor_SingleInstance";

        [STAThread]
        private static void Main(string[] args)
        {
            // 全局异常捕获：崩溃不静默，记录日志并可向用户提示
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += (s, e) => HandleUnhandledException(e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                HandleUnhandledException(e.ExceptionObject as Exception, fatal: true);

            bool createdNew;
            using (var mutex = new Mutex(true, MutexName, out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("DeepSeek 余额监控已经在运行了。\n请到屏幕右下角托盘区域查看。",
                        "DeepSeek 余额监控", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                using (var ctx = new AppContext())
                {
                    // 开发调试：--show-settings / --show-stats 启动后自动打开对应窗口
                    foreach (var a in args)
                    {
                        if (a == "--show-settings") ctx.RequestShowSettings();
                        if (a == "--show-stats") ctx.RequestShowStats();
                    }
                    Application.Run(ctx);
                }
            }
        }

        /// <summary>未处理异常：写入日志，非致命异常提示后继续运行。</summary>
        private static void HandleUnhandledException(Exception ex, bool fatal = false)
        {
            try
            {
                Core.Logger.Current?.Error("未处理异常（" + (fatal ? "致命" : "可恢复") + "）: ", ex);
            }
            catch { }

            try
            {
                if (fatal)
                {
                    MessageBox.Show(
                        "程序遇到未预期的错误，即将退出。\n错误详情已记录到日志：\n"
                        + System.IO.Path.Combine(AppContext.DataRoot, "日志"),
                        "DeepSeek 余额监控 - 发生错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    // 非致命（如 UI 线程异常）：提示但保持运行
                    MessageBox.Show(
                        "发生了一个可恢复的错误（已记录日志）：\n" + ex?.Message,
                        "DeepSeek 余额监控",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch { }
        }
    }
}
