using System;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>归一化账户余额（多供应商统一结构）。各适配器把原始响应解析为本结构。</summary>
    public class AccountBalance
    {
        /// <summary>账户是否可调用 API。</summary>
        public bool IsAvailable { get; set; }

        /// <summary>剩余可用余额。</summary>
        public decimal? Remaining { get; set; }

        /// <summary>已用（部分供应商提供）。</summary>
        public decimal? Used { get; set; }

        /// <summary>总额度（部分供应商提供）。</summary>
        public decimal? Total { get; set; }

        /// <summary>赠送额度。</summary>
        public decimal? Granted { get; set; }

        /// <summary>充值额度。</summary>
        public decimal? ToppedUp { get; set; }

        /// <summary>币种（默认 CNY）。</summary>
        public string Currency { get; set; } = "CNY";
    }
}
