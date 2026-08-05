using System;
using System.Text;
using System.Threading.Tasks;
using DeepSeekBalanceMonitor.Core;

namespace SmokeTest
{
    /// <summary>
    /// 冒烟测试：验证余额查询模块的各类路径。
    /// 用法：
    ///   SmokeTest.exe               —— 假密钥（预期：认证失败分类）
    ///   SmokeTest.exe write-config <密钥> —— 把密钥写入正式配置（开发验证用）
    ///   设置环境变量 DS_API_KEY 后运行 —— 真实密钥（预期：返回余额，密钥不进入命令行）
    /// </summary>
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            if (args.Length >= 2 && args[0] == "write-config")
                return WriteConfig(args[1]);

            string key = Environment.GetEnvironmentVariable("DS_API_KEY");
            if (string.IsNullOrEmpty(key))
                key = args.Length > 0 ? args[0] : "sk-invalid-smoke-test-key";
            Console.WriteLine("测试密钥: " + key.Substring(0, Math.Min(8, key.Length)) + "..." + (key.Length > 8 ? key.Substring(key.Length - 4) : ""));
            Console.WriteLine("目标接口: https://api.deepseek.com/user/balance");

            var api = new DeepSeekApiClient();
            try
            {
                var result = Task.Run(async () => await api.GetBalanceAsync(key)).GetAwaiter().GetResult();
                Console.WriteLine("[成功] 余额 = ¥" + result.TotalBalance.ToString("F2")
                    + "，账户可用 = " + result.IsAvailable + "，查询时间 = " + result.Time.ToString("yyyy-MM-dd HH:mm:ss"));
                return 0;
            }
            catch (BalanceQueryException ex)
            {
                Console.WriteLine("[失败] 分类=" + ex.Kind + "，原因=" + ex.Message);
                return ex.Kind == QueryErrorKind.AuthFailed ? 2 : 3;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[未知错误] " + ex);
                return 1;
            }
        }

        /// <summary>把密钥写入正式配置（开发验证用，模拟设置界面保存）。</summary>
        private static int WriteConfig(string key)
        {
            try
            {
                var dataRoot = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "DeepSeek余额监控");
                System.IO.Directory.CreateDirectory(dataRoot);

                var svc = new ConfigService(System.IO.Path.Combine(dataRoot, "设置.json"));
                var cfg = svc.Load();
                cfg.ApiKey = key;
                svc.Save(cfg);
                Console.WriteLine("[配置] 密钥已写入 " + dataRoot + "\\设置.json");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("[配置] 写入失败: " + ex.Message);
                return 1;
            }
        }
    }
}
