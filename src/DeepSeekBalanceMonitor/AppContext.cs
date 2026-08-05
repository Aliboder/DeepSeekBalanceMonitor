using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;
using DeepSeekBalanceMonitor.UI;

namespace DeepSeekBalanceMonitor
{
    /// <summary>
    /// 全局应用上下文：负责初始化数据目录、装配各组件、管理程序生命周期。
    /// </summary>
    public class AppContext : ApplicationContext
    {
        /// <summary>用户数据根目录（Documents\DeepSeek余额监控）。</summary>
        public static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "DeepSeek余额监控");

        public Logger Log { get; }
        public ConfigService ConfigService { get; }
        public Config Config { get; private set; }
        public HistoryStore History { get; }
        public DeepSeekApiClient Api { get; }
        public BalanceMonitor Monitor { get; private set; }
        public FloatingWindow FloatWindow { get; private set; }
        public TrayIcon Tray { get; private set; }
        public HideService HideService { get; private set; }
        public NotificationService Notify { get; private set; }
        public AlertEngine Alerts { get; private set; }
        public StatsForm Stats { get; private set; }
        public SettingsForm Settings { get; private set; }
        public string ConfigPath => Path.Combine(DataRoot, "设置.json");
        public string HistoryPath => Path.Combine(DataRoot, "余额记录.json");

        public AppContext()
        {
            try { Directory.CreateDirectory(DataRoot); } catch { }
            Log = new Logger(Path.Combine(DataRoot, "日志"));
            Logger.Current = Log;

            ConfigService = new ConfigService(ConfigPath);
            Config = ConfigService.Load();
            History = new HistoryStore(HistoryPath);
            Api = new DeepSeekApiClient();

            // —— 轮询调度 + 悬浮窗 + 托盘 ——
            Monitor = new BalanceMonitor(Api, History, Config.ApiKey, Config.WarnThreshold);
            FloatWindow = new FloatingWindow(this);
            Monitor.StateChanged += (s, e) =>
            {
                FloatWindow.UpdateDisplay();
                Tray.UpdateFromMonitor();
            };
            FloatWindow.Show();

            Tray = new TrayIcon(this);
            Tray.UpdateFromMonitor(); // 初始化托盘显示（无数据时靛蓝）

            // 通知服务与定时隐藏（悬浮窗右键菜单「隐藏」接入）
            Notify = new NotificationService(Tray.NativeIcon);
            HideService = new HideService(this);
            HideService.StateChanged += (s, e) => Tray.RefreshMenu();
            FloatWindow.HideRequested += (s, minutes) => HideService.Hide(minutes);

            // 智能告警（余额不足 / 消费突增）
            Alerts = new AlertEngine(this);
            Monitor.StateChanged += (s, e) => Alerts.OnBalanceChanged();

            // 统计面板：悬浮窗双击 / 托盘左键 / 托盘菜单「统计」统一入口
            Stats = new StatsForm(this);
            Monitor.StateChanged += (s, e) => { if (Stats != null && Stats.Visible) Stats.RefreshData(); };
            FloatWindow.OpenStatsRequested += (s, e) => ShowStats();
            Tray.ShowStatsRequested += (s, e) => ShowStats();

            // 设置窗口：悬浮窗右键 / 托盘菜单「设置...」统一入口
            Settings = new SettingsForm(this);
            FloatWindow.OpenSettingsRequested += (s, e) => ShowSettings();
            Tray.OpenSettingsRequested += (s, e) => ShowSettings();

            // 开机自启状态同步（防止安装位置变化后自启失效）
            AutoStartService.Sync(Config);

            Monitor.Start(Config.RefreshIntervalSeconds);

            Log.Info("程序启动");
        }

        /// <summary>打开统计面板（单例，重复打开只前置显示并刷新数据）。</summary>
        public void ShowStats()
        {
            if (Stats == null || Stats.IsDisposed) Stats = new StatsForm(this);
            Stats.RefreshData();
            if (Stats.Visible) { Stats.Activate(); }
            else { Stats.Show(); }
        }

        /// <summary>打开设置窗口（单例）。</summary>
        public void ShowSettings()
        {
            if (Settings == null || Settings.IsDisposed) Settings = new SettingsForm(this);
            if (Settings.Visible) { Settings.Activate(); }
            else { Settings.Show(); }
        }

        // 开发调试用：等消息循环启动后再显示窗口（字段持有 Timer 防 GC）
        private System.Windows.Forms.Timer _showSettingsTimer;
        private System.Windows.Forms.Timer _showStatsTimer;

        public void RequestShowSettings()
        {
            _showSettingsTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _showSettingsTimer.Tick += (s, e) => { _showSettingsTimer.Stop(); ShowSettings(); };
            _showSettingsTimer.Start();
        }

        public void RequestShowStats()
        {
            _showStatsTimer = new System.Windows.Forms.Timer { Interval = 400 };
            _showStatsTimer.Tick += (s, e) => { _showStatsTimer.Stop(); ShowStats(); };
            _showStatsTimer.Start();
        }

        /// <summary>保存当前配置到磁盘（各组件修改 Config 后调用）。</summary>
        public void SaveConfig()
        {
            ConfigService.Save(Config);
        }

        /// <summary>退出程序（托盘右键菜单的唯一退出入口）。</summary>
        public void ExitApp()
        {
            Log.Info("程序退出");
            Monitor?.Stop();
            HideService?.Dispose();
            Tray?.Dispose();
            ExitThread();
        }
    }
}
