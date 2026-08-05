using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 设置窗口：全部设置改动即时生效。含 API 密钥管理（测试/应用/清空）与开机自启。
    /// 布局：所有控件使用组内相对坐标，组高度按内容自动计算，保证全部设置项完整显示。
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly Timer _saveTimer; // 防抖保存：连续改动 800ms 后落盘

        // 跨方法使用的控件（其余控件均为局部变量）
        private TextBox _keyBox;
        private Label _lblKeyStatus;

        // 布局：窗体级 Y 游标
        private int _y = 12;

        public SettingsForm(AppContext ctx)
        {
            _ctx = ctx;

            Text = "设置 - DeepSeek 余额监控";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(460, 400);
            Font = new Font("Microsoft YaHei UI", 9);
            BackColor = Color.White;

            // 关键：关闭自动缩放（与悬浮窗一致）——高分屏下由系统统一缩放，
            // 避免 AutoScale 叠加导致控件错位、文字被按钮截断
            AutoScaleMode = AutoScaleMode.None;

            // 高分屏（125%/150% 缩放）下窗口可能超出屏幕高度：
            // 开启自动滚动，内容超高时出现滚动条，保证所有设置项可见
            AutoScroll = true;
            AutoScrollMargin = new Size(0, 8);

            _saveTimer = new Timer { Interval = 800 };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); _ctx.SaveConfig(); };

            BuildDisplayGroup();
            BuildAlertGroup();
            BuildKeyGroup();
            BuildOtherGroup();

            // 底部：打开数据文件夹
            var btnOpenData = new Button { Text = "打开数据文件夹", Width = 130, Height = 30, Location = new Point(14, _y + 6) };
            btnOpenData.Click += (s, e) =>
            {
                try
                {
                    Directory.CreateDirectory(AppContext.DataRoot);
                    Process.Start("explorer.exe", "\"" + AppContext.DataRoot + "\"");
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "打开失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            };
            Controls.Add(btnOpenData);

            // 窗口高度上限：按屏幕工作区物理高度换算为逻辑高度，高分屏下不超出屏幕；
            // 超出部分由滚动条查看
            int maxClientH = 640;
            try
            {
                var wa = Screen.PrimaryScreen.WorkingArea;
                // 用桌面 HDC 取系统真实 DPI（窗口句柄未创建时 CreateGraphics 会误报 96）
                using (var g = Graphics.FromHwnd(IntPtr.Zero))
                {
                    double scale = Math.Max(1.0, g.DpiY / 96.0);
                    maxClientH = Math.Max(360, (int)((wa.Height - 100) / scale));
                }
            }
            catch { }

            ClientSize = new Size(460, Math.Min(_y + 48, maxClientH));
            // 滚动范围 = 全部内容高度（含底部按钮），屏幕矮时通过滚动条查看
            AutoScrollMinSize = new Size(0, _y + 40);
        }

        // ============ 布局构建 ============

        /// <summary>添加一个分组框，返回组内控件起始 Y（标题下方）。</summary>
        private GroupBox AddGroup(string title)
        {
            var g = new GroupBox { Text = title, Location = new Point(12, _y), Size = new Size(436, 40) };
            Controls.Add(g);
            return g;
        }

        /// <summary>按内容实际高度收尾分组框，并把窗体游标推进到组底（供下一组/按钮定位）。</summary>
        private void FinishGroup(GroupBox g, int innerY)
        {
            g.Height = innerY + 8;
            _y = g.Bottom + 10;
        }

        private CheckBox AddCheck(GroupBox g, string text, bool checkedValue, ref int y, Action<bool> onChanged)
        {
            var chk = new CheckBox { Text = text, AutoSize = true, Location = new Point(14, y), Checked = checkedValue };
            chk.CheckedChanged += (s, e) => onChanged(chk.Checked);
            g.Controls.Add(chk);
            y += 28;
            return chk;
        }

        private (TrackBar tb, Label lbl) AddSlider(GroupBox g, string caption, int min, int max, int value,
            ref int y, Func<int, string> fmt)
        {
            g.Controls.Add(new Label { Text = caption + "：", AutoSize = true, Location = new Point(14, y + 6) });
            var valueLabel = new Label { AutoSize = true, Location = new Point(300, y + 6), ForeColor = Color.FromArgb(0x1F, 0x6F, 0xEB), Font = new Font(Font, FontStyle.Bold) };
            // 关键：TrackBar 默认 AutoSize=true，高 DPI 下高度自动放大到 69px，
            // 与相邻滑杆重叠（"滑杆消失/界面乱"的根因）。
            // 必须 AutoSize=false + 固定 30px，三根滑杆才互不重叠。
            // 顺序关键：必须先 AutoSize=false 再设 Height，否则 Height 被 AutoSize 覆盖
            var tb = new TrackBar { AutoSize = false, Height = 30 };
            tb.Minimum = min; tb.Maximum = max; tb.Value = value;
            tb.Location = new Point(100, y); tb.Width = 180;
            tb.TickStyle = TickStyle.None;
            g.Controls.Add(tb);
            g.Controls.Add(valueLabel);

            void RefreshLabel() => valueLabel.Text = fmt(tb.Value);
            RefreshLabel();
            tb.ValueChanged += (s, e) => { RefreshLabel(); OnSliderChanged(caption, tb.Value); };
            tb.MouseUp += (s, e) => MarkDirty();

            y += 38;
            return (tb, valueLabel);
        }

        private void BuildDisplayGroup()
        {
            var g = AddGroup("显示");
            int y = 24;
            AddSlider(g, "字体大小", 12, 48, _ctx.Config.FontSize, ref y, v => v + " 号");
            AddSlider(g, "整体透明度", 30, 100, _ctx.Config.Opacity, ref y, v => v + " %");
            AddSlider(g, "鼠标离开暗度", 10, 100, _ctx.Config.IdleOpacity, ref y, v => v + " %");
            FinishGroup(g, y);
        }

        private void BuildAlertGroup()
        {
            var g = AddGroup("余额提醒");
            int y = 26;

            g.Controls.Add(new Label { Text = "预警阈值：", AutoSize = true, Location = new Point(14, y + 4) });
            var numThreshold = new NumericUpDown
            {
                Location = new Point(110, y), Width = 110, Minimum = 0, Maximum = 99999.99m,
                DecimalPlaces = 2, Value = Math.Min(_ctx.Config.WarnThreshold, 99999.99m)
            };
            g.Controls.Add(numThreshold);
            g.Controls.Add(new Label { Text = "元", AutoSize = true, Location = new Point(228, y + 4) });
            numThreshold.ValueChanged += (s, e) =>
            {
                _ctx.Config.WarnThreshold = numThreshold.Value;
                _ctx.Monitor.SetWarnThreshold(numThreshold.Value); // 悬浮窗颜色立即更新
                MarkDirty();
            };
            y += 36;

            AddCheck(g, "余额低于阈值时弹出通知", _ctx.Config.NotifyLowBalance, ref y,
                v => { _ctx.Config.NotifyLowBalance = v; MarkDirty(); });
            AddCheck(g, "消费突增时弹出通知", _ctx.Config.NotifySurge, ref y,
                v => { _ctx.Config.NotifySurge = v; MarkDirty(); });

            FinishGroup(g, y);
        }

        private void BuildKeyGroup()
        {
            var g = AddGroup("API 密钥");
            int y = 26;

            _keyBox = new TextBox
            {
                Location = new Point(14, y), Width = 320,
                UseSystemPasswordChar = true,
                Text = _ctx.Config.ApiKey
            };
            g.Controls.Add(_keyBox);
            var chkShowKey = new CheckBox { Text = "显示密钥", AutoSize = true, Location = new Point(340, y + 3) };
            chkShowKey.CheckedChanged += (s, e) => _keyBox.UseSystemPasswordChar = !chkShowKey.Checked;
            g.Controls.Add(chkShowKey);
            y += 36;

            var btnTest = new Button { Text = "测试", Location = new Point(14, y), Width = 75, Height = 30 };
            var btnApply = new Button { Text = "应用", Location = new Point(98, y), Width = 75, Height = 30 };
            var btnClear = new Button { Text = "清空", Location = new Point(182, y), Width = 75, Height = 30 };
            btnTest.Click += OnTestKey;
            btnApply.Click += OnApplyKey;
            btnClear.Click += OnClearKey;
            g.Controls.Add(btnTest);
            g.Controls.Add(btnApply);
            g.Controls.Add(btnClear);
            y += 38;

            _lblKeyStatus = new Label
            {
                AutoSize = false, Location = new Point(14, y), Width = 420, Height = 32,
                ForeColor = Color.Gray
            };
            g.Controls.Add(_lblKeyStatus);
            y += 38;

            FinishGroup(g, y);
        }

        private void BuildOtherGroup()
        {
            var g = AddGroup("其他");
            int y = 26;

            g.Controls.Add(new Label { Text = "刷新间隔：", AutoSize = true, Location = new Point(14, y + 4) });
            var cmbInterval = new ComboBox
            {
                Location = new Point(110, y), Width = 130, DropDownStyle = ComboBoxStyle.DropDownList
            };
            string[] labels = { "5 秒", "15 秒", "30 秒", "1 分钟", "1 分 30 秒", "2 分钟" };
            for (int i = 0; i < Config.RefreshIntervals.Length; i++)
                cmbInterval.Items.Add(labels[i]);
            int sel = Array.IndexOf(Config.RefreshIntervals, _ctx.Config.RefreshIntervalSeconds);
            cmbInterval.SelectedIndex = sel >= 0 ? sel : 2;
            cmbInterval.SelectedIndexChanged += (s, e) =>
            {
                _ctx.Config.RefreshIntervalSeconds = Config.RefreshIntervals[cmbInterval.SelectedIndex];
                _ctx.Monitor.SetInterval(_ctx.Config.RefreshIntervalSeconds);
                MarkDirty();
            };
            g.Controls.Add(cmbInterval);
            y += 36;

            AddCheck(g, "开机自动启动", _ctx.Config.AutoStart, ref y, v =>
            {
                _ctx.Config.AutoStart = v;
                AutoStartService.SetEnabled(v);
                MarkDirty();
            });

            FinishGroup(g, y);
        }

        /// <summary>滑杆拖动中即时预览（不落盘），松手时统一保存。</summary>
        private void OnSliderChanged(string caption, int value)
        {
            var cfg = _ctx.Config;
            if (caption == "字体大小")
            {
                cfg.FontSize = value;
                _ctx.FloatWindow.ApplyConfig();
            }
            else if (caption == "整体透明度")
            {
                cfg.Opacity = value;
                _ctx.FloatWindow.ApplyConfig();
            }
            else if (caption == "鼠标离开暗度")
            {
                cfg.IdleOpacity = value;
                _ctx.FloatWindow.ApplyConfig();
            }
            MarkDirty();
        }

        private void MarkDirty()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        // ============ 密钥管理 ============

        private async void OnTestKey(object sender, EventArgs e)
        {
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetKeyStatus("请先输入密钥", true);
                return;
            }
            SetKeyStatus("正在测试密钥...", false);
            try
            {
                var result = await _ctx.Api.GetBalanceAsync(key);
                SetKeyStatus("✅ 密钥有效，当前余额 ¥" + result.TotalBalance.ToString("F2"), false);
            }
            catch (BalanceQueryException ex)
            {
                SetKeyStatus("❌ " + ex.Message, true);
            }
        }

        private void OnApplyKey(object sender, EventArgs e)
        {
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                OnClearKey(sender, e);
                return;
            }
            string tail = key.Length >= 4 ? key.Substring(key.Length - 4) : key;
            if (MessageBox.Show(this,
                    "确认应用此 API 密钥（末 4 位：" + tail + "）？\n应用后将立即刷新余额。",
                    "确认密钥", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.ApiKey = key;
            _ctx.SaveConfig();
            _ctx.Monitor.SetApiKey(key); // 立即用新密钥查询，不等下一个刷新周期
            SetKeyStatus("已应用，正在刷新余额...", false);
        }

        private void OnClearKey(object sender, EventArgs e)
        {
            if (MessageBox.Show(this,
                    "确定清空 API 密钥吗？\n清空后将停止查询余额。",
                    "确认清空", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.ApiKey = "";
            _ctx.SaveConfig();
            _ctx.Monitor.SetApiKey("");
            _keyBox.Text = "";
            SetKeyStatus("密钥已清空，余额查询已停止", false);
        }

        private void SetKeyStatus(string text, bool error)
        {
            _lblKeyStatus.Text = text;
            _lblKeyStatus.ForeColor = error ? Color.FromArgb(0xC0, 0x39, 0x2B) : Color.FromArgb(0x1E, 0x84, 0x41);
        }
    }
}
