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
    }
}
