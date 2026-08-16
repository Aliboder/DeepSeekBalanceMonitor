using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>余额状态：充足（绿）/ 不足（红）/ 查询异常（橙）。</summary>
    public enum BalanceStatus
    {
        Normal,
        Low,
        Error
    }

    /// <summary>
    /// 轮询调度器 + 状态机：按配置间隔查询余额，维护当前状态，通过事件通知 UI。
    /// 查询串行执行（上一请求未完成则跳过本轮），连续失败自动退避，成功即恢复。
    /// </summary>
    public class BalanceMonitor : IDisposable
    {
        private readonly DeepSeekApiClient _api;
        private readonly HistoryStore _history;
        private readonly Timer _timer;
        private readonly object _lock = new object();

        private string _apiKey;
        private string _accountId = "";
        private decimal _warnThreshold;
        private bool _busy;
        private bool _disposed;
        private int _configuredInterval = 30;

        /// <summary>状态变化时触发（余额更新 / 状态切换 / 错误信息变化）。</summary>
        public event EventHandler StateChanged;

        /// <summary>当前状态。</summary>
        public BalanceStatus Status { get; private set; } = BalanceStatus.Error;

        /// <summary>最后一次成功获取的余额（查询失败时保留旧值）。</summary>
        public decimal? Balance { get; private set; }

        /// <summary>最后一次成功查询时间。</summary>
        public DateTime? LastSuccessTime { get; private set; }

        /// <summary>最近一次错误说明（成功时清空）。</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>连续失败次数。</summary>
        public int ConsecutiveFailures { get; private set; }

        public BalanceMonitor(DeepSeekApiClient api, HistoryStore history, string apiKey, decimal warnThreshold)
        {
            _api = api;
            _history = history;
            _apiKey = apiKey ?? "";
            _warnThreshold = warnThreshold;

            _timer = new Timer();
            _timer.Tick += OnTick;
            _timer.Interval = GetEffectiveIntervalMs(30);
        }

        /// <summary>启动轮询并立即查询一次。</summary>
        public void Start(int intervalSeconds)
        {
            lock (_lock)
            {
                _configuredInterval = intervalSeconds;
                _timer.Interval = GetEffectiveIntervalMs(intervalSeconds);
                _timer.Start();
            }
            _ = RefreshAsync(); // 启动即查一次，不阻塞界面
        }

        private async void OnTick(object sender, EventArgs e)
        {
            await RefreshAsync();
        }

        public void Stop()
        {
            _timer.Stop();
        }

        /// <summary>修改轮询间隔（秒），即时生效。</summary>
        public void SetInterval(int intervalSeconds)
        {
            lock (_lock)
            {
                _configuredInterval = intervalSeconds;
                _timer.Interval = GetEffectiveIntervalMs(intervalSeconds);
            }
        }

        /// <summary>修改预警阈值，即时生效。</summary>
        public void SetWarnThreshold(decimal threshold)
        {
            _warnThreshold = threshold;
            ReevaluateStatus();
        }

        /// <summary>更新 API 密钥，并立即查询一次。</summary>
        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey ?? "";
            _ = RefreshAsync();
        }

        /// <summary>立即查询一次（不等下一个轮询周期）。异步执行，不阻塞界面。</summary>
        public void RefreshNow()
        {
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_disposed) return;
            lock (_lock)
            {
                if (_busy) return; // 上一请求未完成，跳过本轮
                _busy = true;
            }

            try
            {
                var result = await _api.GetBalanceAsync(_apiKey).ConfigureAwait(true);

                Balance = result.TotalBalance;
                LastSuccessTime = result.Time;
                ErrorMessage = null;
                ConsecutiveFailures = 0;
                _history.Append(_accountId, result.TotalBalance, result.Time);
                ReevaluateStatus();

                Logger.Current?.Info("余额查询成功: ¥" + result.TotalBalance.ToString("F2"));

                // 成功后恢复为配置的轮询间隔
                _timer.Interval = GetEffectiveIntervalMs(_configuredInterval);
            }
            catch (BalanceQueryException ex)
            {
                ConsecutiveFailures++;
                ErrorMessage = ex.Message;
                Status = BalanceStatus.Error;
                Logger.Current?.Warn("余额查询失败(" + ex.Kind + "): " + ex.Message);

                // 连续失败时拉长间隔，避免高频请求
                if (ConsecutiveFailures >= 3)
                {
                    _timer.Interval = Math.Max(_timer.Interval, 30000);
                }
                OnStateChanged();
            }
            finally
            {
                lock (_lock) { _busy = false; }
            }
        }

        /// <summary>根据当前余额与阈值重新判定状态（阈值修改后调用）。</summary>
        private void ReevaluateStatus()
        {
            if (Balance.HasValue)
            {
                Status = Balance.Value < _warnThreshold ? BalanceStatus.Low : BalanceStatus.Normal;
            }
            else
            {
                Status = BalanceStatus.Error;
            }
            OnStateChanged();
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>计算实际轮询间隔（毫秒）。Timer.Interval 单位是毫秒，配置以秒为单位。</summary>
        private int GetEffectiveIntervalMs(int configuredSeconds)
        {
            // 连续失败退避：至少 30 秒
            if (ConsecutiveFailures >= 3 && configuredSeconds < 30) return 30000;
            return configuredSeconds * 1000;
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
