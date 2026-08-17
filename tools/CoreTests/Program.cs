using System;
using System.IO;
using System.Text;
using QuotaMonitor.Core;

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
            TestOpenCodeGoParse();

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

        // ============ OpenCode Go 套餐解析 ============

        private static void TestOpenCodeGoParse()
        {
            Console.WriteLine("-- OpenCode Go 套餐解析 --");

            // 1. 标准响应：percent 为 0~100 语义，直接使用
            var r1 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"percent\":42.0,\"resetsAt\":\"2026-08-17T10:00:00Z\"}," +
                "\"weekly\":{\"percent\":60.5},\"monthly\":{\"percent\":30}}}");
            Assert(r1.Windows.Count == 3, "标准响应解析 3 窗口");
            var s1 = r1.Windows.Find(w => w.Kind == "session");
            Assert(s1.UsedPercent == 42 && s1.RemainingPercent == 58, "percent 直接使用（0~100）");
            Assert(s1.ResetsAt.HasValue, "resetsAt ISO 字符串解析");
            Assert(Math.Abs(s1.ResetsAt.Value.ToUniversalTime().Subtract(new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc)).TotalMinutes) < 1, "resetsAt 值正确");

            // 2. 0~1 比例归一化：dashboard 风格 usagePercent 字段
            var r2 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"usagePercent\":0.42},\"weekly\":{\"usagePercent\":0.6},\"monthly\":{\"usagePercent\":0.3}}}");
            var s2 = r2.Windows.Find(w => w.Kind == "session");
            Assert(s2.UsedPercent == 42, "0~1 比例自动放大为百分数（usagePercent）");

            // 3. 其它别名字段（usedPercent / percentUsed / percentage）
            var r3 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"usedPercent\":0.25},\"weekly\":{\"percentUsed\":0.5},\"monthly\":{\"percentage\":0.75}}}");
            Assert(r3.Windows.Find(w => w.Kind == "session").UsedPercent == 25, "usedPercent 别名 + 比例归一化");
            Assert(r3.Windows.Find(w => w.Kind == "weekly").UsedPercent == 50, "percentUsed 别名 + 比例归一化");
            Assert(r3.Windows.Find(w => w.Kind == "monthly").UsedPercent == 75, "percentage 别名 + 比例归一化");

            // 4. 重置时间多种格式：resetInSec（秒）
            var r4 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"percent\":10,\"resetInSec\":3600},\"weekly\":{\"percent\":20},\"monthly\":{\"percent\":30}}}");
            var s4 = r4.Windows.Find(w => w.Kind == "session");
            Assert(s4.ResetsAt.HasValue && Math.Abs(s4.ResetsAt.Value.Subtract(DateTime.Now.AddSeconds(3600)).TotalSeconds) < 60, "resetInSec 秒数解析");

            // 5. 重置时间：resetAt 秒级时间戳
            var ts = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var r5 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"percent\":10,\"resetAt\":" + ts + "},\"weekly\":{\"percent\":20},\"monthly\":{\"percent\":30}}}");
            var s5 = r5.Windows.Find(w => w.Kind == "session");
            Assert(s5.ResetsAt.HasValue && s5.ResetsAt.Value.ToUniversalTime().Date == new DateTime(2026, 8, 20), "resetAt 秒级时间戳解析");

            // 6. 无任何百分比字段 → 该窗口被跳过
            var r6 = OpenCodeGoClient.ParseUsage(
                "{\"usage\":{\"rolling\":{\"status\":\"ok\"},\"weekly\":{\"percent\":20},\"monthly\":{\"percent\":30}}}");
            Assert(r6.Windows.Count == 2 && r6.Windows.Find(w => w.Kind == "session") == null, "缺失百分比字段的窗口跳过");

            // 7. 非法 JSON → 抛异常
            AssertThrowsAny(() => OpenCodeGoClient.ParseUsage("not json"), "Go 非法 JSON 抛异常");
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
