using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
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
            TestConfigMigration();
            TestBalanceMonitor();

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
            Console.WriteLine("-- 历史存储（按账户） --");
            var path = Path.Combine(Path.GetTempPath(), "CoreTests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                // 旧记录归属测试：先写无账户标记的文件，构造时归入 "acc1"
                var legacyPath = Path.Combine(Path.GetTempPath(), "CoreTests-" + Guid.NewGuid().ToString("N") + ".json");
                File.WriteAllText(legacyPath, "{\"records\":[{\"time\":\"2026-08-01 10:00:00\",\"balance\":100}]}");
                var storeL = new HistoryStore(legacyPath, "acc1");
                Assert(storeL.Records.Count == 1 && storeL.Records[0].AccountId == "acc1", "旧记录归属迁移账户");
                try { File.Delete(legacyPath); } catch { }

                var store = new HistoryStore(path);
                var day0 = DateTime.Today.AddDays(-6).AddHours(10);

                // 账户 A：100 → 95 → 90（三天），今天 85
                store.Append("accA", 100m, day0);
                store.Append("accA", 95m, day0.AddDays(1));
                store.Append("accA", 90m, day0.AddDays(2));
                store.Append("accA", 85m, DateTime.Today.AddHours(9));
                // 账户 B：50 → 49（今天）
                store.Append("accB", 50m, day0);
                store.Append("accB", 49m, DateTime.Today.AddHours(8));

                Assert(store.Records.Count == 6, "两个账户共 6 条");
                Assert(store.GetRecords("accA").Count == 4 && store.GetRecords("accB").Count == 2, "按账户过滤");

                // 同账户相同余额去重：只刷新时间戳
                store.Append("accA", 85m, DateTime.Today.AddHours(10));
                Assert(store.GetRecords("accA").Count == 4, "同账户相同余额去重");

                // 消费统计按账户
                Assert(store.TodaySpent("accA") == 5m, "账户A 今日消费 5");
                Assert(store.TodaySpent("accB") == 1m, "账户B 今日消费 1");
                Assert(store.TotalSpent("accA") == 15m, "账户A 总消费 15");
                Assert(store.TotalSpent("accB") == 1m, "账户B 总消费 1");
                Assert(store.AverageDailySpent("accA", 7) == 10m / 3m, "账户A 近7天日均（不含今天）");

                // 持久化重载
                var store2 = new HistoryStore(path);
                Assert(store2.GetRecords("accA").Count == 4 && store2.GetRecords("accB").Count == 2, "重载后按账户一致");
                Assert(store2.TodaySpent("accA") == 5m, "重载后账户A 今日消费一致");

                // 损坏回退
                File.WriteAllText(path, "{broken json");
                var store3 = new HistoryStore(path);
                Assert(store3.Records.Count == 0, "损坏文件安全回退");
            }
            finally { try { File.Delete(path); } catch { } }
        }

        // ============ 配置迁移 ============

        private static void TestConfigMigration()
        {
            Console.WriteLine("-- 配置迁移 --");
            var path = Path.Combine(Path.GetTempPath(), "CoreTests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                // 先存一份新格式（含一个账户），把账户密文提出来
                var svc = new ConfigService(path);
                var cfg = new Config();
                cfg.Accounts.Add(new AccountConfig
                {
                    Id = "acc1", Name = "默认账户", ProviderId = "deepseek",
                    ApiKey = "sk-test-migrate", WarnThreshold = 20m
                });
                svc.Save(cfg);

                // 用反射调私有 EncryptKey 构造旧格式密文
                var enc = typeof(ConfigService).GetMethod("EncryptKey",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
                    .Invoke(null, new object[] { "sk-test-migrate" });

                // 写旧格式：只有 ApiKeyEncrypted + WarnThreshold，无 Accounts
                var legacy = "{\"WarnThreshold\":20,\"ApiKeyEncrypted\":\"" + enc + "\",\"FontSize\":20}";
                File.WriteAllText(path, legacy);

                var loaded = new ConfigService(path).Load();
                Assert(loaded.Accounts.Count == 1, "旧格式迁移出 1 个账户");
                Assert(loaded.Accounts[0].ProviderId == "deepseek", "迁移账户为 deepseek");
                Assert(loaded.Accounts[0].ApiKey == "sk-test-migrate", "迁移保留密钥明文");
                Assert(loaded.Accounts[0].WarnThreshold == 20m, "迁移保留阈值");
                Assert(loaded.ActiveAccountId == loaded.Accounts[0].Id, "ActiveAccountId 指向迁移账户");

                // 新格式往返
                var svc2 = new ConfigService(path);
                var cfg2 = new Config();
                cfg2.Accounts.Add(new AccountConfig { Id = "a", Name = "A", ProviderId = "openrouter", ApiKey = "k1" });
                cfg2.Accounts.Add(new AccountConfig { Id = "b", Name = "B", ProviderId = "zai", ApiKey = "k2" });
                cfg2.ActiveAccountId = "b";
                svc2.Save(cfg2);
                var loaded2 = new ConfigService(path).Load();
                Assert(loaded2.Accounts.Count == 2 && loaded2.Accounts[0].ApiKey == "k1" && loaded2.Accounts[1].ApiKey == "k2",
                    "多账户往返");
                Assert(loaded2.ActiveAccountId == "b", "ActiveAccountId 往返");
                Assert(!File.ReadAllText(path).Contains("k1") && !File.ReadAllText(path).Contains("k2"),
                    "明文密钥不落盘");
            }
            finally { try { File.Delete(path); } catch { } }
        }

        // ============ BalanceMonitor（多实例） ============

        /// <summary>假 provider：固定余额，记录最近查询的 key。</summary>
        private class FakeProvider : IBalanceProvider
        {
            public string Id => "fake";
            public string DisplayName => "Fake";
            public string BaseUrl => "http://fake";
            public decimal Value = 50m;
            public string LastKey;

            public Task<AccountBalance> GetBalanceAsync(string apiKey)
            {
                LastKey = apiKey;
                return Task.FromResult(new AccountBalance { IsAvailable = true, Remaining = Value, Currency = "CNY" });
            }
        }

        private class ThrowingProvider : IBalanceProvider
        {
            public string Id => "bad"; public string DisplayName => "Bad"; public string BaseUrl => "http://bad";
            public Task<AccountBalance> GetBalanceAsync(string apiKey)
                => throw new BalanceQueryException(QueryErrorKind.Network, "超时");
        }

        private static void TestBalanceMonitor()
        {
            Console.WriteLine("-- BalanceMonitor（多实例） --");
            var path = Path.Combine(Path.GetTempPath(), "CoreTests-" + Guid.NewGuid().ToString("N") + ".json");
            try
            {
                var fp = new FakeProvider { Value = 100m };
                var acc = new AccountConfig { Id = "acc1", Name = "一号", ProviderId = "fake", ApiKey = "k1", WarnThreshold = 30m };
                var mon = new BalanceMonitor(fp, new HistoryStore(path), acc);
                mon.RefreshNowAsync().Wait();

                Assert(mon.Balance == 100m && mon.Status == BalanceStatus.Normal, "查询成功且余额充足为 Normal");
                Assert(mon.AccountId == "acc1" && mon.AccountName == "一号", "账户信息透出");
                Assert(fp.LastKey == "k1", "使用账户的 API Key");

                // 阈值：余额低于阈值 → Low
                fp.Value = 10m;
                mon.SetWarnThreshold(30m);
                mon.RefreshNowAsync().Wait();
                Assert(mon.Status == BalanceStatus.Low, "余额低于阈值为 Low");

                // 错误：provider 抛异常 → Error，保留最后余额
                var bad = new ThrowingProvider();
                var mon2 = new BalanceMonitor(bad, new HistoryStore(path), new AccountConfig { Id = "acc2", ApiKey = "k2" });
                mon2.RefreshNowAsync().Wait();
                Assert(mon2.Status == BalanceStatus.Error && mon2.ConsecutiveFailures == 1, "查询失败为 Error");
            }
            finally { try { File.Delete(path); } catch { } }
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
