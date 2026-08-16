using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>一条余额历史记录。</summary>
    public class BalanceRecord
    {
        public DateTime Time { get; set; }
        public decimal Balance { get; set; }
        public string AccountId { get; set; } = "";
    }

    /// <summary>
    /// 余额历史存储：追加去重、自动截断，按账户隔离。
    /// 文件：我的文档\DeepSeek余额监控\余额记录.json
    /// </summary>
    public class HistoryStore
    {
        private const int MaxRecords = 5000;
        private readonly string _path;
        private readonly string _legacyAccountId;
        private readonly object _lock = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private List<BalanceRecord> _records;

        public HistoryStore(string path, string legacyAccountId = null)
        {
            _path = path;
            _legacyAccountId = legacyAccountId ?? "";
            _records = LoadFromDisk();
        }

        /// <summary>全部记录（按时间正序）。</summary>
        public IReadOnlyList<BalanceRecord> Records
        {
            get { lock (_lock) { return _records.ToList(); } }
        }

        /// <summary>按账户过滤记录。</summary>
        public IReadOnlyList<BalanceRecord> GetRecords(string accountId)
        {
            lock (_lock) { return _records.Where(r => r.AccountId == accountId).ToList(); }
        }

        /// <summary>
        /// 追加一条记录。同账户相同余额自动去重：只刷新最后一条的时间戳，不产生重复记录
        /// （需求：相同余额自动去重；同时保证"最近更新"时间保持新鲜）。
        /// </summary>
        public void Append(string accountId, decimal balance, DateTime time)
        {
            lock (_lock)
            {
                var last = _records.Count > 0 ? _records.LastOrDefault(r => r.AccountId == accountId) : null;
                if (last != null && last.Balance == balance) { last.Time = time; SaveToDisk(); return; }
                _records.Add(new BalanceRecord { Time = time, Balance = balance, AccountId = accountId });
                if (_records.Count > MaxRecords)
                    _records = _records.Skip(_records.Count - MaxRecords / 2).ToList();
                SaveToDisk();
            }
        }

        /// <summary>指定账户累计消费（所有余额下降之和，元）。</summary>
        public decimal TotalSpent(string accountId) => SpentSince(accountId, DateTime.MinValue);

        /// <summary>指定账户从指定时刻起的消费总额（余额下降之和，元）。</summary>
        public decimal SpentSince(string accountId, DateTime from)
        {
            lock (_lock)
            {
                var rs = _records.Where(r => r.AccountId == accountId).ToList();
                decimal spent = 0;
                for (int i = 1; i < rs.Count; i++)
                {
                    if (rs[i].Time < from) continue;
                    var d = rs[i - 1].Balance - rs[i].Balance;
                    if (d > 0) spent += d;
                }
                return spent;
            }
        }

        /// <summary>指定账户今日（本地零点起）消费，元。</summary>
        public decimal TodaySpent(string accountId) => SpentSince(accountId, DateTime.Today);

        /// <summary>
        /// 指定账户近 days 天日均消费（不含今天），元。
        /// 按实际有记录的日期数平均，历史不足 days 天时按实际天数算。
        /// </summary>
        public decimal AverageDailySpent(string accountId, int days)
        {
            lock (_lock)
            {
                var rs = _records.Where(r => r.AccountId == accountId).ToList();
                var today = DateTime.Today;
                var start = today.AddDays(-days);
                decimal spent = 0; int daysWithData = 0;
                for (int i = 1; i < rs.Count; i++)
                {
                    var t = rs[i].Time;
                    if (t < start || t >= today) continue;
                    var d = rs[i - 1].Balance - rs[i].Balance;
                    if (d > 0) spent += d;
                }
                for (int i = 0; i < rs.Count; i++)
                {
                    var t = rs[i].Time;
                    if (t < start || t >= today) continue;
                    if (i == 0 || rs[i - 1].Time.Date != t.Date) daysWithData++;
                }
                return daysWithData == 0 ? 0m : spent / daysWithData;
            }
        }

        private List<BalanceRecord> LoadFromDisk()
        {
            try
            {
                if (File.Exists(_path))
                {
                    var raw = File.ReadAllText(_path);
                    var doc = Json.Deserialize<HistoryDoc>(raw);
                    if (doc?.records != null)
                    {
                        return doc.records
                            .OrderBy(r => r.time)
                            .Select(r => new BalanceRecord
                            {
                                Time = r.time,
                                Balance = r.balance,
                                AccountId = string.IsNullOrEmpty(r.accountId) ? _legacyAccountId : r.accountId
                            }).ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Current?.Warn("读取余额历史失败: " + ex.Message);
            }
            return new List<BalanceRecord>();
        }

        private void SaveToDisk()
        {
            try
            {
                var doc = new HistoryDoc
                {
                    records = _records.Select(r => new RecordItem
                    {
                        time = r.Time,
                        balance = r.Balance,
                        accountId = r.AccountId
                    }).ToList()
                };
                File.WriteAllText(_path, Json.Serialize(doc));
            }
            catch (Exception ex)
            {
                Logger.Current?.Error("保存余额历史失败: ", ex);
            }
        }

        private class HistoryDoc
        {
            public List<RecordItem> records { get; set; } = new List<RecordItem>();
        }

        private class RecordItem
        {
            public DateTime time { get; set; }
            public decimal balance { get; set; }
            public string accountId { get; set; }
        }
    }
}
