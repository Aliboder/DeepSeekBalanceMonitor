using System;
using System.IO;
using System.Text;
using DeepSeekBalanceMonitor.Core;

namespace CoreTests
{
    /// <summary>
    /// 核心逻辑自动化测试（零依赖断言，失败返回非零退出码）。
    /// 覆盖：余额解析、历史去重、消费统计。
    /// </summary>
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static int Main()
        {
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }

            Console.WriteLine("=== 核心逻辑测试开始 ===");

            TestBalanceParse();
            TestHistoryStore();
            TestDeepSeekProvider();
            TestOtherProviders();

            Console.WriteLine();
            Console.WriteLine($"=== 结果：通过 {_passed}，失败 {_failed} ===");
            return _failed == 0 ? 0 : 1;
        }

        // ============ 余额解析 ============

        private static void TestBalanceParse()
        {
            Console.WriteLine("-- 余额解析 --");

            // 1. 标准响应（CNY）
            var r1 = DeepSeekApiClient.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"}]}");
            Assert(r1.TotalBalance == 110m && r1.IsAvailable, "标准 CNY 响应解析");

            // 2. 多币种时取 CNY
            var r2 = DeepSeekApiClient.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"15.50\"},{\"currency\":\"CNY\",\"total_balance\":\"88.88\"}]}");
            Assert(r2.TotalBalance == 88.88m, "多币种取 CNY");

            // 3. 无 CNY 时取首个币种
            var r3 = DeepSeekApiClient.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"15.50\"}]}");
            Assert(r3.TotalBalance == 15.50m, "无 CNY 取首个币种");

            // 4. 空余额 → 抛异常
            AssertThrows<FormatException>(() => DeepSeekApiClient.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[]}"), "空余额列表抛异常");

            // 5. 非法 JSON → 抛异常（JavaScriptSerializer 抛 ArgumentException，上层统一包装）
            AssertThrowsAny(() => DeepSeekApiClient.ParseBalance("not json"), "非法 JSON 抛异常");
        }

        // ============ DeepSeek 适配器 ============

        private static void TestDeepSeekProvider()
        {
            Console.WriteLine("-- DeepSeek 适配器 --");

            var r1 = DeepSeekProvider.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"CNY\",\"total_balance\":\"110.00\",\"granted_balance\":\"10.00\",\"topped_up_balance\":\"100.00\"}]}");
            Assert(r1.Remaining == 110m && r1.IsAvailable, "标准 CNY 响应解析");
            Assert(r1.Granted == 10m && r1.ToppedUp == 100m && r1.Currency == "CNY", "赠送/充值/币种解析");

            var r2 = DeepSeekProvider.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"15.50\"},{\"currency\":\"CNY\",\"total_balance\":\"88.88\"}]}");
            Assert(r2.Remaining == 88.88m, "多币种取 CNY");

            var r3 = DeepSeekProvider.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[{\"currency\":\"USD\",\"total_balance\":\"15.50\"}]}");
            Assert(r3.Remaining == 15.50m && r3.Currency == "USD", "无 CNY 取首个币种");

            AssertThrows<FormatException>(() => DeepSeekProvider.ParseBalance(
                "{\"is_available\":true,\"balance_infos\":[]}"), "空余额列表抛异常");
            AssertThrowsAny(() => DeepSeekProvider.ParseBalance("not json"), "非法 JSON 抛异常");

            Assert(ProviderRegistry.Get("deepseek") != null, "注册表包含 deepseek");
            Assert(ProviderRegistry.All.Count >= 1, "注册表 All 非空");
        }

        // ============ OpenRouter / Moonshot / Z.ai 适配器 ============

        private static void TestOtherProviders()
        {
            Console.WriteLine("-- OpenRouter / Moonshot / Z.ai 适配器 --");

            // OpenRouter：credits 接口（Management Key）
            var o = OpenRouterProvider.ParseCredits(
                "{\"data\":{\"total_credits\":100.0,\"total_usage\":23.5}}");
            Assert(o.Remaining == 76.5m && o.Used == 23.5m && o.Total == 100.0m && o.Currency == "USD", "OpenRouter 剩余=总额-已用");

            // Moonshot：available / voucher / cash
            var m = MoonshotProvider.ParseBalance(
                "{\"data\":{\"available_balance\":42.5,\"voucher_balance\":10.0,\"cash_balance\":32.5,\"currency\":\"CNY\"}}");
            Assert(m.Remaining == 42.5m && m.Granted == 10.0m && m.ToppedUp == 32.5m, "Moonshot 余额分解");

            // Z.ai：total / available
            var z = ZaiProvider.ParseBalance(
                "{\"data\":{\"total_balance\":200.0,\"available_balance\":188.5,\"currency\":\"CNY\"}}");
            Assert(z.Remaining == 188.5m && z.Total == 200.0m, "Z.ai 可用余额解析");

            // 注册表
            Assert(ProviderRegistry.Get("openrouter") != null, "注册表包含 openrouter");
            Assert(ProviderRegistry.Get("moonshot") != null, "注册表包含 moonshot");
            Assert(ProviderRegistry.Get("zai") != null, "注册表包含 zai");
        }

        // ============ 历史存储与消费统计 ============

        private static void TestHistoryStore()
        {
            Console.WriteLine("-- 历史存储与消费统计 --");
            var path = Path.Combine(Path.GetTempPath(), "CoreTests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var store = new HistoryStore(path);
                var day0 = DateTime.Today.AddDays(-6).AddHours(10);

                // 1. 相同余额去重：只刷新时间戳不新增
                store.Append(100m, day0);
                store.Append(100m, day0.AddHours(1));
                Assert(store.Records.Count == 1, "相同余额去重");
                Assert(store.Records[0].Time == day0.AddHours(1), "去重时刷新时间戳");

                // 2. 余额下降记录追加
                store.Append(95m, day0.AddDays(1));
                store.Append(90m, day0.AddDays(2));
                Assert(store.Records.Count == 3, "余额变化追加记录");

                // 3. 今日消费：昨天 90 → 今天 85 = 5
                var todayRec = DateTime.Today.AddHours(9);
                store.Append(85m, todayRec);
                Assert(store.TodaySpent() == 5m, "今日消费计算");

                // 4. 近 7 天日均（不含今天）：100→95→90 = 10 消费 / 3 个有记录的天 = 3.33
                Assert(store.AverageDailySpent(7) == 10m / 3m, "近7天日均（不含今天）");

                // 5. 总消费：10 + 5 = 15
                Assert(store.TotalSpent() == 15m, "总消费计算");

                // 6. 持久化：新实例重新加载
                var store2 = new HistoryStore(path);
                Assert(store2.Records.Count == 4, "历史持久化重载");
                Assert(store2.TotalSpent() == 15m, "重载后统计一致");

                // 7. 文件损坏时安全回退
                File.WriteAllText(path, "{broken json");
                var store3 = new HistoryStore(path);
                Assert(store3.Records.Count == 0, "损坏文件安全回退");
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        // ============ 微型断言 ============

        private static void Assert(bool condition, string name)
        {
            if (condition) { _passed++; Console.WriteLine("  [PASS] " + name); }
            else { _failed++; Console.WriteLine("  [FAIL] " + name); }
        }

        private static void AssertThrows<T>(Action action, string name) where T : Exception
        {
            try
            {
                action();
                _failed++;
                Console.WriteLine("  [FAIL] " + name + "（未抛异常）");
            }
            catch (T)
            {
                _passed++;
                Console.WriteLine("  [PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("  [FAIL] " + name + "（异常类型错误: " + ex.GetType().Name + "）");
            }
        }

        private static void AssertThrowsAny(Action action, string name)
        {
            try
            {
                action();
                _failed++;
                Console.WriteLine("  [FAIL] " + name + "（未抛异常）");
            }
            catch
            {
                _passed++;
                Console.WriteLine("  [PASS] " + name);
            }
        }
    }
}
