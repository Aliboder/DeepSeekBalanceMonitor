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
    }

    /// <summary>
    /// 余额历史存储：追加去重、自动截断。
    /// 文件：我的文档\DeepSeek余额监控\余额记录.json
    /// </summary>
    public class HistoryStore
    {
        private const int MaxRecords = 5000;
        private readonly string _path;
        private readonly object _lock = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        private List<BalanceRecord> _records;

        public HistoryStore(string path)
        {
            _path = path;
            _records = LoadFromDisk();
        }

        /// <summary>全部记录（按时间正序）。</summary>
        public IReadOnlyList<BalanceRecord> Records
        {
            get { lock (_lock) { return _records.ToList(); } }
        }

        /// <summary>
        /// 追加一条记录。相同余额自动去重：只刷新最后一条的时间戳，不产生重复记录
        /// （需求：相同余额自动去重；同时保证"最近更新"时间保持新鲜）。
        /// </summary>
        public void Append(decimal balance, DateTime time)
        {
            lock (_lock)
            {
                var last = _records.Count > 0 ? _records[_records.Count - 1] : null;
                if (last != null && last.Balance == balance)
                {
                    last.Time = time; // 余额未变：仅更新时间
                    SaveToDisk();
                    return;
                }

                _records.Add(new BalanceRecord { Time = time, Balance = balance });

                // 超过上限时丢弃最旧的一半，控制文件体积
                if (_records.Count > MaxRecords)
                    _records = _records.Skip(_records.Count - MaxRecords / 2).ToList();

                SaveToDisk();
            }
        }

        /// <summary>累计消费（所有余额下降之和，元）。</summary>
        public decimal TotalSpent()
        {
            lock (_lock)
            {
                return SpentSince(DateTime.MinValue);
            }
        }

        /// <summary>从指定时刻起的消费总额（余额下降之和，元）。</summary>
        public decimal SpentSince(DateTime from)
        {
            lock (_lock)
            {
                decimal spent = 0;
                for (int i = 1; i < _records.Count; i++)
                {
                    if (_records[i].Time < from) continue;
                    var d = _records[i - 1].Balance - _records[i].Balance;
                    if (d > 0) spent += d;
                }
                return spent;
            }
        }

        /// <summary>今日（本地零点起）消费，元。</summary>
        public decimal TodaySpent()
        {
            return SpentSince(DateTime.Today);
        }

        /// <summary>
        /// 近 days 天日均消费（不含今天），元。
        /// 按实际有记录的日期数平均，历史不足 days 天时按实际天数算。
        /// </summary>
        public decimal AverageDailySpent(int days)
        {
            lock (_lock)
            {
                var today = DateTime.Today;
                var start = today.AddDays(-days);
                decimal spent = 0;
                int daysWithData = 0;

                for (int i = 1; i < _records.Count; i++)
                {
                    var t = _records[i].Time;
                    if (t < start || t >= today) continue; // 只看近 N 天且不含今天

                    var d = _records[i - 1].Balance - _records[i].Balance;
                    if (d > 0) spent += d;
                }

                // 统计参与天数（记录出现的不同日期数）
                for (int i = 0; i < _records.Count; i++)
                {
                    var t = _records[i].Time;
                    if (t < start || t >= today) continue;
                    if (i == 0 || _records[i - 1].Time.Date != t.Date) daysWithData++;
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
                            .Select(r => new BalanceRecord { Time = r.time, Balance = r.balance })
                            .ToList();
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
                        balance = r.Balance
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
        }
    }
}
