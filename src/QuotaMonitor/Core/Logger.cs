using System;
using System.IO;

namespace QuotaMonitor.Core
{
    /// <summary>
    /// 滚动日志：按天生成文件，保留最近若干份，避免无限膨胀。
    /// </summary>
    public class Logger : IDisposable
    {
        /// <summary>当前全局日志实例（AppContext 启动时注入），供各组件使用。</summary>
        public static Logger Current { get; set; }

        private readonly object _lock = new object();
        private readonly string _logDir;
        private readonly int _keepDays;

        /// <summary>日志目录（如 Documents\QuotaMonitor\日志）。</summary>
        public string LogDirectory => _logDir;

        public Logger(string logDir, int keepDays = 7)
        {
            _logDir = logDir;
            _keepDays = keepDays;
            try { Directory.CreateDirectory(logDir); } catch { /* 目录创建失败不致命 */ }
        }

        public void Info(string message) => Write("INFO", message);

        public void Warn(string message) => Write("WARN", message);

        public void Error(string message, Exception ex = null)
        {
            Write("ERROR", ex == null ? message : message + Environment.NewLine + ex);
        }

        private void Write(string level, string message)
        {
            lock (_lock)
            {
                try
                {
                    var file = Path.Combine(_logDir, "log-" + DateTime.Now.ToString("yyyyMMdd") + ".txt");
                    File.AppendAllText(file,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " [" + level + "] " + message + Environment.NewLine);
                    CleanupOldLogs();
                }
                catch { /* 日志失败不影响主程序 */ }
            }
        }

        /// <summary>删除超出保留天数的旧日志文件。</summary>
        private void CleanupOldLogs()
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-_keepDays);
                foreach (var f in Directory.GetFiles(_logDir, "log-*.txt"))
                {
                    if (File.GetLastWriteTime(f) < cutoff)
                    {
                        try { File.Delete(f); } catch { }
                    }
                }
            }
            catch { }
        }

        public void Dispose() { }
    }
}
