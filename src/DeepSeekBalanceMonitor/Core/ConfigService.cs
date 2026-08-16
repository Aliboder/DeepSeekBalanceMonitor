using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>
    /// 配置读写：JSON 文件存储，每个账户的 API 密钥使用 Windows DPAPI 加密。
    /// 文件结构：{ ..., "Accounts": [{ "ApiKeyEncrypted": "base64..." }] }
    /// </summary>
    public class ConfigService
    {
        private readonly string _path;
        private readonly object _lock = new object();
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

        public ConfigService(string path)
        {
            _path = path;
        }

        /// <summary>加载配置；文件不存在或损坏时返回默认配置。旧格式（单密钥）自动迁移为新格式。</summary>
        public Config Load()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_path))
                    {
                        var raw = File.ReadAllText(_path);
                        var doc = Json.Deserialize<ConfigDocument>(raw);
                        if (doc != null)
                        {
                            var cfg = doc.ToConfig();

                            // 旧版本迁移：有旧密钥且无账户 → 生成默认 DeepSeek 账户
                            if ((doc.Accounts == null || doc.Accounts.Count == 0)
                                && !string.IsNullOrEmpty(doc.ApiKeyEncrypted))
                            {
                                var acc = new AccountConfig
                                {
                                    Name = "默认账户",
                                    ProviderId = "deepseek",
                                    ApiKey = DecryptKey(doc.ApiKeyEncrypted),
                                    WarnThreshold = doc.WarnThreshold < 0 ? 10m : doc.WarnThreshold
                                };
                                cfg.Accounts.Add(acc);
                                cfg.ActiveAccountId = acc.Id;
                                Save(cfg); // 立即落盘为新格式
                                return cfg;
                            }

                            return cfg;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Current?.Warn("读取配置失败，使用默认配置: " + ex.Message);
                }
                return new Config();
            }
        }

        /// <summary>保存配置（各账户密钥自动加密）。</summary>
        public void Save(Config cfg)
        {
            lock (_lock)
            {
                try
                {
                    var doc = ConfigDocument.FromConfig(cfg);
                    File.WriteAllText(_path, Json.Serialize(doc));
                }
                catch (Exception ex)
                {
                    Logger.Current?.Error("保存配置失败: ", ex);
                }
            }
        }

        /// <summary>加密明文密钥 → base64。</summary>
        private static string EncryptKey(string plain)
        {
            if (string.IsNullOrEmpty(plain)) return "";
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>解密 base64 → 明文密钥。</summary>
        private static string DecryptKey(string encrypted)
        {
            if (string.IsNullOrEmpty(encrypted)) return "";
            try
            {
                var bytes = ProtectedData.Unprotect(Convert.FromBase64String(encrypted), null, DataProtectionScope.CurrentUser);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                // 密钥无法解密（如换用户/系统），视为无效
                return "";
            }
        }

        /// <summary>可序列化的配置文档（密钥只存密文，永不存明文）。</summary>
        private class ConfigDocument
        {
            public int FontSize { get; set; }
            public int Opacity { get; set; }
            public int IdleOpacity { get; set; }
            public decimal WarnThreshold { get; set; }
            public bool NotifyLowBalance { get; set; }
            public bool NotifySurge { get; set; }
            public string ApiKeyEncrypted { get; set; } = "";
            public int RefreshIntervalSeconds { get; set; }
            public bool AutoStart { get; set; }
            public bool LockMode { get; set; }
            public bool TopMost { get; set; }
            public int? FloatX { get; set; }
            public int? FloatY { get; set; }
            public int? StatsW { get; set; }
            public int? StatsH { get; set; }
            // 保留旧字段 ApiKeyEncrypted / WarnThreshold 用于迁移判断
            public List<AccountItem> Accounts { get; set; } = new List<AccountItem>();
            // 当前显示的账户 Id
            public string ActiveAccountId { get; set; } = "";

            public class AccountItem
            {
                public string Id { get; set; }
                public string Name { get; set; }
                public string ProviderId { get; set; }
                public string ApiKeyEncrypted { get; set; } = "";
                public decimal WarnThreshold { get; set; }
            }

            public static ConfigDocument FromConfig(Config c)
            {
                var doc = new ConfigDocument
                {
                    FontSize = c.FontSize,
                    Opacity = c.Opacity,
                    IdleOpacity = c.IdleOpacity,
                    WarnThreshold = 0m,
                    NotifyLowBalance = c.NotifyLowBalance,
                    NotifySurge = c.NotifySurge,
                    ApiKeyEncrypted = "",
                    RefreshIntervalSeconds = c.RefreshIntervalSeconds,
                    AutoStart = c.AutoStart,
                    LockMode = c.LockMode,
                    TopMost = c.TopMost,
                    FloatX = c.FloatPosition?.X,
                    FloatY = c.FloatPosition?.Y,
                    StatsW = c.StatsSize?.Width,
                    StatsH = c.StatsSize?.Height
                };
                doc.Accounts = (c.Accounts ?? new List<AccountConfig>()).Select(a => new AccountItem
                {
                    Id = a.Id,
                    Name = a.Name,
                    ProviderId = a.ProviderId,
                    ApiKeyEncrypted = EncryptKey(a.ApiKey),
                    WarnThreshold = a.WarnThreshold
                }).ToList();
                doc.ActiveAccountId = c.ActiveAccountId;
                return doc;
            }

            public Config ToConfig()
            {
                var cfg = new Config
                {
                    FontSize = Normalize(FontSize, 12, 48, 28),
                    Opacity = Normalize(Opacity, 30, 100, 90),
                    IdleOpacity = Normalize(IdleOpacity, 10, 100, 45),
                    NotifyLowBalance = NotifyLowBalance,
                    NotifySurge = NotifySurge,
                    RefreshIntervalSeconds = Normalize(RefreshIntervalSeconds, 5, 120, 30),
                    AutoStart = AutoStart,
                    LockMode = LockMode,
                    TopMost = TopMost,
                    FloatPosition = (FloatX.HasValue && FloatY.HasValue)
                        ? new System.Drawing.Point(FloatX.Value, FloatY.Value)
                        : (System.Drawing.Point?)null,
                    StatsSize = (StatsW.HasValue && StatsH.HasValue)
                        ? new System.Drawing.Size(StatsW.Value, StatsH.Value)
                        : (System.Drawing.Size?)null
                };
                cfg.Accounts = (Accounts ?? new List<AccountItem>())
                    .Where(a => !string.IsNullOrEmpty(a.Id))
                    .Select(a => new AccountConfig
                    {
                        Id = a.Id,
                        Name = string.IsNullOrEmpty(a.Name) ? a.ProviderId : a.Name,
                        ProviderId = string.IsNullOrEmpty(a.ProviderId) ? "deepseek" : a.ProviderId,
                        ApiKey = DecryptKey(a.ApiKeyEncrypted),
                        WarnThreshold = a.WarnThreshold < 0 ? 10m : a.WarnThreshold
                    }).ToList();
                cfg.ActiveAccountId = ActiveAccountId;
                if (string.IsNullOrEmpty(cfg.ActiveAccountId) && cfg.Accounts.Count > 0)
                    cfg.ActiveAccountId = cfg.Accounts[0].Id;
                return cfg;
            }

            private static int Normalize(int v, int min, int max, int dflt)
            {
                return v >= min && v <= max ? v : dflt;
            }
        }
    }
}
