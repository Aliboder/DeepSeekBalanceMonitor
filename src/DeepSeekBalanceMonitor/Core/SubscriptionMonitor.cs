using System;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// Go 套餐轮询调度器：与 BalanceMonitor 并行运行，独立 timer。
    /// 查询结果通过事件通知 UI。
    /// </summary>
    public class SubscriptionMonitor : IDisposable
    {
        private readonly OpenCodeGoClient _client;
        private readonly Timer _timer;
        private readonly object _lock = new object();

        private string _apiKey;
        private bool _busy;
        private bool _disposed;
        private int _configuredInterval = 30;

        /// <summary>状态变化时触发。</summary>
        public event EventHandler StateChanged;

        /// <summary>最后一次成功获取的套餐数据（查询失败时保留旧值）。</summary>
        public SubscriptionResult Result { get; private set; }

        /// <summary>最近一次错误说明（成功时清空）。</summary>
        public string ErrorMessage { get; private set; }

        /// <summary>是否有有效 API Key。</summary>
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

        public SubscriptionMonitor(OpenCodeGoClient client, string apiKey)
        {
            _client = client;
            _apiKey = apiKey ?? "";

            _timer = new Timer();
            _timer.Tick += OnTick;
            _timer.Interval = 30000;
        }

        /// <summary>启动轮询并立即查询一次。</summary>
        public void Start(int intervalSeconds)
        {
            lock (_lock)
            {
                _configuredInterval = intervalSeconds;
                _timer.Interval = intervalSeconds * 1000;
                _timer.Start();
            }
            if (IsConfigured) _ = RefreshAsync();
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
                _timer.Interval = intervalSeconds * 1000;
            }
        }

        /// <summary>更新 API 密钥，并立即查询一次。</summary>
        public void SetApiKey(string apiKey)
        {
            _apiKey = apiKey ?? "";
            _ = RefreshAsync();
        }

        /// <summary>立即查询一次。</summary>
        public void RefreshNow()
        {
            _ = RefreshAsync();
        }

        private async Task RefreshAsync()
        {
            if (_disposed || !IsConfigured) return;
            lock (_lock)
            {
                if (_busy) return;
                _busy = true;
            }

            try
            {
                var result = await _client.GetUsageAsync(_apiKey).ConfigureAwait(true);

                Result = result;
                ErrorMessage = null;
                OnStateChanged();

                Logger.Current?.Info("Go 套餐查询成功");
            }
            catch (BalanceQueryException ex)
            {
                ErrorMessage = ex.Message;
                Logger.Current?.Warn("Go 套餐查询失败(" + ex.Kind + "): " + ex.Message);
                OnStateChanged();
            }
            finally
            {
                lock (_lock) { _busy = false; }
            }
        }

        private void OnStateChanged()
        {
            StateChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            _disposed = true;
            _timer.Stop();
            _timer.Dispose();
        }
    }
}
