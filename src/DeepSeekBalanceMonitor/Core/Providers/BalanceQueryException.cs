using System;

namespace DeepSeekBalanceMonitor.Core
{
    /// <summary>余额查询失败的原因分类（用于悬浮窗/托盘提示）。</summary>
    public enum QueryErrorKind
    {
        /// <summary>API 密钥无效或已失效（401 / 40002 / 40003 等认证错误）。</summary>
        AuthFailed,
        /// <summary>网络不通、超时、限流等。</summary>
        Network,
        /// <summary>接口返回了无法解析的内容（可能接口变更）。</summary>
        ParseError,
        /// <summary>账户不可用（欠费停用等）。</summary>
        AccountUnavailable
    }

    /// <summary>带错误分类的余额查询异常。</summary>
    public class BalanceQueryException : Exception
    {
        public QueryErrorKind Kind { get; }

        public BalanceQueryException(QueryErrorKind kind, string message) : base(message)
        {
            Kind = kind;
        }
    }
}
