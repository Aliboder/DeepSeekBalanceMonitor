using System;
using System.Collections.Generic;
using System.Linq;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>余额供应商注册表：集中登记所有适配器，按 Id 查找。</summary>
    public static class ProviderRegistry
    {
        private static readonly Dictionary<string, IBalanceProvider> _providers =
            new Dictionary<string, IBalanceProvider>(StringComparer.OrdinalIgnoreCase);

        static ProviderRegistry()
        {
            Register(new DeepSeekProvider());
        }

        /// <summary>登记一个适配器（重复 Id 覆盖）。</summary>
        public static void Register(IBalanceProvider provider)
        {
            if (provider == null || string.IsNullOrEmpty(provider.Id)) return;
            _providers[provider.Id] = provider;
        }

        /// <summary>全部已登记的适配器。</summary>
        public static IReadOnlyList<IBalanceProvider> All => _providers.Values.ToList();

        /// <summary>按 Id 查找；不存在返回 null。</summary>
        public static IBalanceProvider Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            return _providers.TryGetValue(id, out var p) ? p : null;
        }
    }
}
