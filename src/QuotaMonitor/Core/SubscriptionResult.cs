using System;
using System.Collections.Generic;

namespace QuotaMonitor.Core
{
    /// <summary>单个配额窗口（5小时滚动/周/月）。</summary>
    public class SubscriptionWindow
    {
        /// <summary>窗口类型：session（5小时滚动）、weekly、monthly。</summary>
        public string Kind { get; set; }

        /// <summary>已使用百分比（0~100）。</summary>
        public int UsedPercent { get; set; }

        /// <summary>剩余百分比（0~100）。</summary>
        public int RemainingPercent { get; set; }

        /// <summary>窗口重置时间。</summary>
        public DateTime? ResetsAt { get; set; }

        /// <summary>窗口显示名称。</summary>
        public string DisplayName
        {
            get
            {
                switch (Kind)
                {
                    case "session": return "滚动用量";
                    case "weekly": return "每周用量";
                    case "monthly": return "每月用量";
                    default: return Kind;
                }
            }
        }
    }

    /// <summary>套餐查询结果。</summary>
    public class SubscriptionResult
    {
        /// <summary>是否查询成功。</summary>
        public bool IsOk { get; set; }

        /// <summary>查询时间。</summary>
        public DateTime Time { get; set; } = DateTime.Now;

        /// <summary>配额窗口列表。</summary>
        public List<SubscriptionWindow> Windows { get; set; } = new List<SubscriptionWindow>();
    }
}
