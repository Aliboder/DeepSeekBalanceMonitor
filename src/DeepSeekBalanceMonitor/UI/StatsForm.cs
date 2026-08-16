using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 统计面板：统计摘要 + 余额走势图 + 历史记录表 + CSV 导出。
    /// </summary>
    public class StatsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly DataGridView _grid;
        private readonly Label _lblRecordCount;
        private readonly Dictionary<string, Label> _stats = new Dictionary<string, Label>();
        private Font _boldFont; // 复用（避免每次刷新新建 GDI 字体句柄）

        // 主题状态（跟随系统）
        private readonly bool _dark;
        private Color _bgColor, _fgColor, _panelColor, _rowColor, _rowAltColor,
            _headerColor, _gridLineColor, _mutedColor, _downColor, _upColor;

        /// <summary>表格最多渲染的记录数（最近 N 条），控制大数据量下的刷新开销。</summary>
        private const int GridMaxRows = 200;

        /// <summary>数据指纹：余额/最后记录/阈值均未变化时跳过界面重建（防抖）。</summary>
        private string _dataFingerprint = "";

        /// <summary>按系统主题设置配色（深色/浅色两套）。</summary>
        private void ApplyTheme()
        {
            if (_dark)
            {
                _bgColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
                _fgColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
                _panelColor = Color.FromArgb(0x25, 0x25, 0x26);
                _rowColor = Color.FromArgb(0x25, 0x25, 0x26);
                _rowAltColor = Color.FromArgb(0x2A, 0x2A, 0x2B);
                _headerColor = Color.FromArgb(0x33, 0x33, 0x37);
                _gridLineColor = Color.FromArgb(0x3F, 0x3F, 0x46);
                _mutedColor = Color.FromArgb(0x9C, 0x9C, 0x9C);
                _downColor = Color.FromArgb(0xF4, 0x87, 0x71);
                _upColor = Color.FromArgb(0x89, 0xD1, 0x85);
            }
            else
            {
                _bgColor = Color.White;
                _fgColor = Color.FromArgb(0x33, 0x33, 0x33);
                _panelColor = Color.FromArgb(0xF5, 0xF5, 0xF5);
                _rowColor = Color.White;
                _rowAltColor = Color.FromArgb(0xF7, 0xF7, 0xF7);
                _headerColor = Color.FromArgb(0xE8, 0xE8, 0xE8);
                _gridLineColor = Color.FromArgb(0xD8, 0xD8, 0xD8);
                _mutedColor = Color.Gray;
                _downColor = Color.FromArgb(0xC0, 0x39, 0x2B);
                _upColor = Color.FromArgb(0x1E, 0x84, 0x41);
            }
            BackColor = _bgColor;
            ForeColor = _fgColor;
        }

        public StatsForm(AppContext ctx)
        {
            _ctx = ctx;

            Text = "统计 - DeepSeek 余额监控";
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(640, 400);
            Size = new Size(700, 440);
            Font = new Font("Microsoft YaHei UI", 9);

            // 尺寸记忆：恢复上次手动调整过的窗口大小（限制在最小尺寸与屏幕范围内）
            var saved = _ctx.Config.StatsSize;
            if (saved.HasValue)
            {
                int w = Math.Max(saved.Value.Width, MinimumSize.Width);
                int h = Math.Max(saved.Value.Height, MinimumSize.Height);
                var wa = Screen.PrimaryScreen.WorkingArea;
                Size = new Size(Math.Min(w, wa.Width), Math.Min(h, wa.Height));
            }

            // 主题：跟随系统深浅色
            _dark = SystemTheme.IsDark();
            ApplyTheme();

            // —— 顶部：统计摘要（单行 3 格：当前余额 / 今日消费 / 日均消费） ——
            var statsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 68,
                ColumnCount = 3,
                RowCount = 1,
                Padding = new Padding(8, 6, 8, 2),
                BackColor = _panelColor
            };
            for (int c = 0; c < 3; c++) statsPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            statsPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            foreach (var key in new[] { "balance", "today", "dailyAvg" })
            {
                var lbl = new Label
                {
                    AutoSize = false,
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("Microsoft YaHei UI", 10f, FontStyle.Bold),
                    ForeColor = _fgColor,
                    // 高 DPI 下标题过长会溢出格子被裁：超出时省略号显示，避免文字被剪断
                    AutoEllipsis = true
                };
                _stats[key] = lbl;
                statsPanel.Controls.Add(lbl);
            }
            Controls.Add(statsPanel);

            // —— 底部：记录数提示 ——
            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 42, Padding = new Padding(10, 4, 10, 6), BackColor = _bgColor };
            _lblRecordCount = new Label
            {
                AutoSize = true,
                Location = new Point(10, 13),
                ForeColor = _mutedColor,
                BackColor = Color.Transparent
            };
            bottom.Controls.Add(_lblRecordCount);
            Controls.Add(bottom);

            // —— 历史记录表（深色主题） ——
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = _bgColor,
                BorderStyle = BorderStyle.None,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                EnableHeadersVisualStyles = false,
                // 表头高度自动适应：高 DPI 缩放下文字不会被上下裁剪（此前 DisableResizing 导致截断）
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                GridColor = _gridLineColor
            };
            _grid.ColumnHeadersDefaultCellStyle.BackColor = _headerColor;
            _grid.ColumnHeadersDefaultCellStyle.ForeColor = _fgColor;
            _grid.ColumnHeadersDefaultCellStyle.Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold);
            _grid.DefaultCellStyle.BackColor = _rowColor;
            _grid.DefaultCellStyle.ForeColor = _fgColor;
            _grid.DefaultCellStyle.SelectionBackColor = _dark ? Color.FromArgb(0x0E, 0x63, 0x9C) : Color.FromArgb(0xCC, 0xE4, 0xF7);
            _grid.DefaultCellStyle.SelectionForeColor = _dark ? Color.White : Color.Black;
            _grid.AlternatingRowsDefaultCellStyle.BackColor = _rowAltColor;

            _grid.Columns.Add("colTime", "时间");
            _grid.Columns.Add("colBalance", "余额");
            _grid.Columns.Add("colChange", "变动");
            // 固定列宽保证完整显示（高 DPI 下时间戳较长，Fill 均分会挤压余额/变动列导致截断）
            _grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _grid.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _grid.Columns[2].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            _grid.Columns[1].Width = 110;
            _grid.Columns[2].Width = 100;
            Controls.Add(_grid);

            // 刷新次序：表格填满剩余空间，需最后添加
            _grid.BringToFront();

            RefreshData();
            Shown += (s, e) => RefreshData();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle); // 标题栏跟随系统主题
        }

        /// <summary>用户手动调整窗口大小结束后，记住尺寸供下次启动恢复。</summary>
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (WindowState == FormWindowState.Normal)
            {
                _ctx.Config.StatsSize = Size;
                _ctx.SaveConfig();
            }
        }

        /// <summary>重新计算并刷新全部统计内容（打开面板/余额更新时调用）。</summary>
        public void RefreshData()
        {
            var history = _ctx.History;
            var records = history.Records;
            var monitor = _ctx.Monitor;
            var cfg = _ctx.Config;

            // 防抖：数据未变化（余额、最后记录、阈值均相同）时不重建界面，
            // 避免每 30 秒轮询时表格反复全量重建（闪烁/开销）
            string fp = (records.Count > 0 ? records[records.Count - 1].Time.Ticks + "|" + records[records.Count - 1].Balance : "e")
                + "|" + monitor.Balance + "|" + (cfg.ActiveAccount?.WarnThreshold ?? 10m);
            if (fp == _dataFingerprint) return;
            _dataFingerprint = fp;

            // —— 统计摘要（当前余额 / 今日消费 / 日均消费） ——
            decimal? balance = monitor.Balance;
            decimal today = history.TodaySpent();
            decimal total = history.TotalSpent();

            // 整体日均：从首条记录到今天的天数
            decimal avgAll = 0;
            if (records.Count > 0)
            {
                int daysAll = Math.Max(1, (DateTime.Today - records[0].Time.Date).Days + 1);
                avgAll = total / daysAll;
            }

            SetStat("balance", "当前余额", balance.HasValue ? "¥ " + balance.Value.ToString("F2") : "--");
            SetStat("today", "今日消费", "¥ " + today.ToString("F2"));
            SetStat("dailyAvg", "日均消费", "¥ " + avgAll.ToString("F2"));

            // —— 历史记录表（倒序，变动红绿标注；仅渲染最近 GridMaxRows 条） ——
            _lblRecordCount.Text = records.Count > GridMaxRows
                ? "共 " + records.Count + " 条记录（表格仅显示最近 " + GridMaxRows + " 条）"
                : "共 " + records.Count + " 条记录";

            _grid.Rows.Clear();
            int shown = Math.Min(records.Count, GridMaxRows);
            for (int i = records.Count - 1; i >= records.Count - shown; i--)
            {
                var r = records[i];
                decimal change = i == 0 ? 0m : records[i].Balance - records[i - 1].Balance;
                int row = _grid.Rows.Add(
                    r.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                    "¥ " + r.Balance.ToString("F2"),
                    change == 0 ? "--" : (change > 0 ? "▲ +" + change.ToString("F2") : "▼ " + change.ToString("F2")));

                var changeCell = _grid.Rows[row].Cells[2];
                changeCell.Style.ForeColor = change < 0 ? _downColor
                    : change > 0 ? _upColor
                    : _mutedColor;
                if (i == records.Count - 1)
                {
                    if (_boldFont == null) _boldFont = new Font(Font, FontStyle.Bold);
                    _grid.Rows[row].Cells[1].Style.Font = _boldFont;
                }
            }
        }

        private void SetStat(string key, string caption, string value)
        {
            if (_stats.TryGetValue(key, out var lbl))
            {
                lbl.Text = caption + Environment.NewLine + value;
            }
        }
    }
}
