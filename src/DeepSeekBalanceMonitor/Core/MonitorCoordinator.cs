using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 多账户监控协调器：每个账户一个 <see cref="BalanceMonitor"/> 实例，
    /// 统一管理生命周期与事件，按 ActiveAccountId 提供当前账户。
    /// </summary>
    public class MonitorCoordinator : IDisposable
    {
        private readonly List<BalanceMonitor> _monitors = new List<BalanceMonitor>();
        private readonly object _lock = new object();
        private string _activeAccountId = "";

        /// <summary>任一账户状态变化时触发（含当前账户切换）。</summary>
        public event EventHandler StateChanged;

        /// <summary>全部账户监控实例。</summary>
        public IReadOnlyList<BalanceMonitor> Monitors
        {
            get { lock (_lock) { return _monitors.ToList(); } }
        }

        /// <summary>当前显示的账户 Id。</summary>
        public string ActiveAccountId
        {
            get { return _activeAccountId; }
            set
            {
                _activeAccountId = value;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>当前账户监控实例（找不到返回 null）。</summary>
        public BalanceMonitor Current => Get(_activeAccountId) ?? Monitors.FirstOrDefault();

        /// <summary>按账户 Id 查找监控实例。</summary>
        public BalanceMonitor Get(string accountId)
        {
            lock (_lock) { return _monitors.FirstOrDefault(m => m.AccountId == accountId); }
        }

        /// <summary>
        /// 用账户列表重建监控实例（设置窗口增删账户后调用）。
        /// providerResolver: 供应商 Id → 适配器实例。
        /// </summary>
        public void SetAccounts(IEnumerable<AccountConfig> accounts, HistoryStore history,
            Func<string, IBalanceProvider> providerResolver)
        {
            lock (_lock)
            {
                foreach (var m in _monitors) { m.StateChanged -= OnMonitorChanged; m.Dispose(); }
                _monitors.Clear();

                foreach (var acc in accounts)
                {
                    var provider = providerResolver(acc.ProviderId);
                    if (provider == null) continue;
                    var mon = new BalanceMonitor(provider, history, acc);
                    mon.StateChanged += OnMonitorChanged;
                    _monitors.Add(mon);
                }
                if (string.IsNullOrEmpty(_activeAccountId) || Get(_activeAccountId) == null)
                    _activeAccountId = _monitors.FirstOrDefault()?.AccountId ?? "";
            }
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        private void OnMonitorChanged(object sender, EventArgs e)
        {
            StateChanged?.Invoke(this, e);
        }

        /// <summary>启动全部账户轮询。</summary>
        public void Start()
        {
            foreach (var m in Monitors) m.Start(30);
        }

        /// <summary>停止全部账户轮询。</summary>
        public void Stop()
        {
            foreach (var m in Monitors) m.Stop();
        }

        /// <summary>广播刷新间隔（秒）。</summary>
        public void SetInterval(int intervalSeconds)
        {
            foreach (var m in Monitors) m.SetInterval(intervalSeconds);
        }

        /// <summary>修改某账户预警阈值。</summary>
        public void SetWarnThreshold(string accountId, decimal threshold)
        {
            Get(accountId)?.SetWarnThreshold(threshold);
        }

        /// <summary>修改某账户 API 密钥并立即查询。</summary>
        public void SetApiKey(string accountId, string apiKey)
        {
            Get(accountId)?.SetApiKey(apiKey);
        }

        /// <summary>立即刷新全部账户（异步，不阻塞）。</summary>
        public void RefreshNow()
        {
            foreach (var m in Monitors) m.RefreshNow();
        }

        public void Dispose()
        {
            foreach (var m in Monitors) m.Dispose();
        }
    }
}
