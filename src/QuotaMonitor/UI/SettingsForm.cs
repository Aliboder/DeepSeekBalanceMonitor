using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using QuotaMonitor.Core;

namespace QuotaMonitor.UI
{
    /// <summary>
    /// 设置窗口（现代导航布局）：左侧导航 + 内容区，全部自绘控件。
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly Timer _saveTimer;

        private TextBox _keyBox;
        private Label _lblKeyStatus;
        private TextBox _goKeyBox;
        private Label _lblGoKeyStatus;

        private readonly bool _dark;

        private static readonly Color DarkBg = Color.FromArgb(0x1A, 0x1A, 0x1E);
        private static readonly Color LightBg = Color.FromArgb(0xF2, 0xF2, 0xF5);
        private static readonly Color DarkFg = Color.FromArgb(0xDD, 0xDD, 0xDD);
        private static readonly Color LightFg = Color.FromArgb(0x33, 0x33, 0x33);
        private static readonly Color DarkInputBg = Color.FromArgb(0x2D, 0x2D, 0x30);
        private static readonly Color DarkInputFg = Color.FromArgb(0xDD, 0xDD, 0xDD);
        private static readonly Color DarkInputBorder = Color.FromArgb(0x50, 0x50, 0x55);
        private static readonly Color LightInputBorder = Color.FromArgb(0xC0, 0xC0, 0xC4);
        private static readonly Color AccentBlue = Color.FromArgb(0x1F, 0x6F, 0xEB);

        // 内容区面板（4 个）
        private readonly Panel[] _pages = new Panel[4];

        // 布局常量
        private const int SidebarW = 112;
        private const int ContentLeft = SidebarW + 20;
        private const int ContentTop = 16;
        private const int ContentW = 420;

        public SettingsForm(AppContext ctx)
        {
            _ctx = ctx;

            Text = "设置";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Microsoft YaHei UI", 9f);
            AutoScaleMode = AutoScaleMode.None;

            _dark = SystemTheme.IsDark();
            BackColor = _dark ? DarkBg : LightBg;
            ForeColor = _dark ? DarkFg : LightFg;

            _saveTimer = new Timer { Interval = 800 };
            _saveTimer.Tick += (s, e) => { _saveTimer.Stop(); _ctx.SaveConfig(); };

            ClientSize = new Size(ContentLeft + ContentW + 20, 520);

            // 左侧导航
            var nav = new NavSidebar(_dark)
            {
                Location = new Point(0, 0),
                Height = ClientSize.Height - 46
            };
            nav.ItemSelected += (s, idx) => ShowPage(idx);
            Controls.Add(nav);

            // 内容区
            for (int i = 0; i < 4; i++)
            {
                _pages[i] = new Panel
                {
                    Location = new Point(ContentLeft, ContentTop),
                    Size = new Size(ContentW, ClientSize.Height - ContentTop - 60),
                    BackColor = _dark ? Color.FromArgb(0x1E, 0x1E, 0x22) : Color.FromArgb(0xF7, 0xF7, 0xF9)
                };
                Controls.Add(_pages[i]);
                _pages[i].Visible = (i == 0);
            }

            BuildDisplayPage();
            BuildAlertPage();
            BuildKeyPage();
            BuildOtherPage();

            // 底部栏
            var footerY = ClientSize.Height - 42;
            var btnOpenData = new CardButton(_dark, "打开数据文件夹", 130, 28)
            {
                Location = new Point(ContentLeft, footerY)
            };
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

            var lblVersion = new Label
            {
                AutoSize = true,
                Text = "v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3),
                Location = new Point(ContentLeft + ContentW - 55, footerY + 6),
                ForeColor = _dark ? Color.FromArgb(0x66, 0x66, 0x66) : Color.FromArgb(0xAA, 0xAA, 0xAA)
            };
            Controls.Add(lblVersion);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle);
        }

        private void ShowPage(int index)
        {
            for (int i = 0; i < 4; i++)
                _pages[i].Visible = (i == index);
        }

        // ============ 内容构建辅助 ============

        private Label AddCaption(Panel page, string text, int y)
        {
            var lbl = new Label
            {
                Text = text,
                AutoSize = true,
                Location = new Point(4, y),
                ForeColor = _dark ? Color.FromArgb(0xBB, 0xBB, 0xBB) : Color.FromArgb(0x55, 0x55, 0x55)
            };
            page.Controls.Add(lbl);
            return lbl;
        }

        private void AddSliderRow(Panel page, string caption, int min, int max, int value,
            ref int y, Func<int, string> fmt)
        {
            AddCaption(page, caption, y + 2);
            var valueLabel = new Label
            {
                AutoSize = true,
                Location = new Point(360, y + 2),
                ForeColor = AccentBlue,
                Font = new Font(Font.FontFamily, 9f, FontStyle.Bold)
            };
            page.Controls.Add(valueLabel);

            var slider = new ModernSlider(_dark, min, max, value)
            {
                Location = new Point(110, y),
                Width = 230
            };
            void RefreshLabel() => valueLabel.Text = fmt(slider.Value);
            RefreshLabel();
            slider.ValueChanged += (s, v) => { RefreshLabel(); OnSliderChanged(caption, v); };
            slider.DragEnded += (s, e) => MarkDirty();
            page.Controls.Add(slider);

            y += 36;
        }

        private void AddCheckRow(Panel page, string text, bool checkedValue, ref int y, Action<bool> onChanged)
        {
            var chk = new ModernCheckBox(_dark, text)
            {
                Location = new Point(2, y - 2)
            };
            chk.CheckedChanged += (s, v) => onChanged(v);
            chk.Checked = checkedValue;
            page.Controls.Add(chk);
            y += 30;
        }

        private TextBox AddKeyRow(Panel page, string value, ref int y)
        {
            var box = new TextBox
            {
                Location = new Point(2, y),
                Width = 320,
                UseSystemPasswordChar = true,
                Text = value,
                BorderStyle = BorderStyle.FixedSingle
            };
            if (_dark)
            {
                box.BackColor = DarkInputBg;
                box.ForeColor = DarkInputFg;
            }
            page.Controls.Add(box);

            var chk = new ModernCheckBox(_dark, "显示")
            {
                Location = new Point(330, y + 2)
            };
            chk.CheckedChanged += (s, v) => box.UseSystemPasswordChar = !v;
            page.Controls.Add(chk);
            y += 34;
            return box;
        }

        private void AddButtonRow(Panel page, string[] labels, Action[] handlers, ref int y)
        {
            int x = 2;
            for (int i = 0; i < labels.Length; i++)
            {
                var btn = new CardButton(_dark, labels[i], 72, 28)
                {
                    Location = new Point(x, y),
                    IsAccent = (labels[i] == "应用")
                };
                int idx = i;
                btn.Click += (s, e) => handlers[idx]();
                page.Controls.Add(btn);
                x += 80;
            }
            y += 36;
        }

        private Label AddStatusLine(Panel page, ref int y)
        {
            var lbl = new Label
            {
                AutoSize = false,
                Location = new Point(4, y),
                Width = ContentW - 12,
                Height = 22,
                ForeColor = _dark ? Color.FromArgb(0x88, 0x88, 0x88) : Color.Gray
            };
            page.Controls.Add(lbl);
            y += 28;
            return lbl;
        }

        // ============ 四个页面 ============

        private void BuildDisplayPage()
        {
            var p = _pages[0];
            int y = 20;
            AddSliderRow(p, "字体大小", 12, 48, _ctx.Config.FontSize, ref y, v => v + " 号");
            AddSliderRow(p, "整体透明度", 30, 100, _ctx.Config.Opacity, ref y, v => v + " %");
            AddSliderRow(p, "鼠标离开暗度", 10, 100, _ctx.Config.IdleOpacity, ref y, v => v + " %");
            AddSliderRow(p, "悬浮窗圆角", 0, 30, _ctx.Config.CornerRadius, ref y, v => v + " px");
            AddCheckRow(p, "套餐窗口显示剩余额度（关闭则显示已用额度）", _ctx.Config.ShowRemaining, ref y,
                v => { _ctx.Config.ShowRemaining = v; _ctx.FloatWindow.UpdateDisplay(); MarkDirty(); });
        }

        private void BuildAlertPage()
        {
            var p = _pages[1];
            int y = 20;

            AddCaption(p, "预警阈值", y + 4);
            var numThreshold = new NumericUpDown
            {
                Location = new Point(90, y),
                Width = 100,
                Minimum = 0,
                Maximum = 99999.99m,
                DecimalPlaces = 2,
                Value = Math.Min(_ctx.Config.WarnThreshold, 99999.99m)
            };
            if (_dark)
            {
                numThreshold.BackColor = DarkInputBg;
                numThreshold.ForeColor = DarkInputFg;
            }
            p.Controls.Add(numThreshold);
            p.Controls.Add(new Label
            {
                Text = "元",
                AutoSize = true,
                Location = new Point(198, y + 4),
                ForeColor = _dark ? Color.FromArgb(0x88, 0x88, 0x88) : Color.Gray
            });
            numThreshold.ValueChanged += (s, e) =>
            {
                _ctx.Config.WarnThreshold = numThreshold.Value;
                _ctx.Monitor.SetWarnThreshold(numThreshold.Value);
                MarkDirty();
            };
            y += 38;

            AddCheckRow(p, "余额低于阈值时弹出通知", _ctx.Config.NotifyLowBalance, ref y,
                v => { _ctx.Config.NotifyLowBalance = v; MarkDirty(); });
            AddCheckRow(p, "消费突增时弹出通知", _ctx.Config.NotifySurge, ref y,
                v => { _ctx.Config.NotifySurge = v; MarkDirty(); });
        }

        private void BuildKeyPage()
        {
            var p = _pages[2];
            int y = 20;

            // DeepSeek 区块
            AddCaption(p, "DeepSeek API", y);
            y += 24;
            _keyBox = AddKeyRow(p, _ctx.Config.ApiKey, ref y);
            AddButtonRow(p,
                new[] { "测试", "应用", "清空" },
                new Action[] { () => OnTestKey(), () => OnApplyKey(), () => OnClearKey() },
                ref y);
            _lblKeyStatus = AddStatusLine(p, ref y);
            y += 8;

            // 分隔线
            var sep = new Panel
            {
                Location = new Point(4, y),
                Size = new Size(ContentW - 8, 1),
                BackColor = _dark ? Color.FromArgb(0x35, 0x35, 0x3A) : Color.FromArgb(0xDD, 0xDD, 0xE0)
            };
            p.Controls.Add(sep);
            y += 16;

            // Go 区块
            AddCaption(p, "OpenCode Go API", y);
            y += 24;
            _goKeyBox = AddKeyRow(p, _ctx.Config.OpenCodeGoApiKey, ref y);
            AddButtonRow(p,
                new[] { "测试", "应用", "清空" },
                new Action[] { () => OnTestGoKey(), () => OnApplyGoKey(), () => OnClearGoKey() },
                ref y);
            _lblGoKeyStatus = AddStatusLine(p, ref y);
        }

        private void BuildOtherPage()
        {
            var p = _pages[3];
            int y = 20;

            AddCaption(p, "刷新间隔", y + 4);
            string[] labels = { "5 秒", "15 秒", "30 秒", "1 分钟", "1 分 30 秒", "2 分钟" };
            int sel = Array.IndexOf(Config.RefreshIntervals, _ctx.Config.RefreshIntervalSeconds);
            var cmb = new ModernComboBox(_dark, labels, sel >= 0 ? sel : 2)
            {
                Location = new Point(90, y),
                Width = 130
            };
            cmb.SelectedIndexChanged += (s, idx) =>
            {
                _ctx.Config.RefreshIntervalSeconds = Config.RefreshIntervals[idx];
                _ctx.Monitor.SetInterval(_ctx.Config.RefreshIntervalSeconds);
                _ctx.SubMonitor.SetInterval(_ctx.Config.RefreshIntervalSeconds);
                MarkDirty();
            };
            p.Controls.Add(cmb);
            y += 38;

            AddCheckRow(p, "开机自动启动", _ctx.Config.AutoStart, ref y, v =>
            {
                _ctx.Config.AutoStart = v;
                AutoStartService.SetEnabled(v);
                MarkDirty();
            });
        }

        // ============ 滑杆 & 保存 ============

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
            else if (caption == "悬浮窗圆角")
            {
                cfg.CornerRadius = value;
                _ctx.FloatWindow.ApplyConfig();
            }
            MarkDirty();
        }

        private void MarkDirty()
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }

        // ============ DeepSeek 密钥 ============

        private void OnTestKey()
        {
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) { SetKeyStatus("请先输入密钥", true); return; }
            SetKeyStatus("正在测试...", false);
            _ = TestKeyAsync(key);
        }

        private async System.Threading.Tasks.Task TestKeyAsync(string key)
        {
            try
            {
                var result = await _ctx.Api.GetBalanceAsync(key);
                SetKeyStatus("密钥有效，余额 ¥" + result.TotalBalance.ToString("F2"), false);
            }
            catch (BalanceQueryException ex)
            {
                SetKeyStatus(ex.Message, true);
            }
        }

        private void OnApplyKey()
        {
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) { OnClearKey(); return; }
            string tail = key.Length >= 4 ? key.Substring(key.Length - 4) : key;
            if (MessageBox.Show(this, "确认应用此密钥（末 4 位：" + tail + "）？",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.ApiKey = key;
            _ctx.SaveConfig();
            _ctx.Monitor.SetApiKey(key);
            SetKeyStatus("已应用", false);
        }

        private void OnClearKey()
        {
            if (MessageBox.Show(this, "确定清空密钥吗？", "确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.ApiKey = "";
            _ctx.SaveConfig();
            _ctx.Monitor.SetApiKey("");
            _keyBox.Text = "";
            SetKeyStatus("已清空", false);
        }

        private void SetKeyStatus(string text, bool error)
        {
            _lblKeyStatus.Text = text;
            _lblKeyStatus.ForeColor = error
                ? Color.FromArgb(0xE0, 0x4B, 0x4B)
                : Color.FromArgb(0x4A, 0xDE, 0x80);
        }

        // ============ Go 密钥 ============

        private void OnTestGoKey()
        {
            var key = _goKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) { SetGoKeyStatus("请先输入密钥", true); return; }
            SetGoKeyStatus("正在测试...", false);
            _ = TestGoKeyAsync(key);
        }

        private async System.Threading.Tasks.Task TestGoKeyAsync(string key)
        {
            try
            {
                var result = await _ctx.GoClient.GetUsageAsync(key);
                if (result.Windows.Count > 0)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    foreach (var w in result.Windows)
                        parts.Add(w.DisplayName + " " + w.RemainingPercent + "%");
                    SetGoKeyStatus("密钥有效，" + string.Join(" / ", parts), false);
                }
                else
                    SetGoKeyStatus("密钥有效，但无窗口数据", false);
            }
            catch (BalanceQueryException ex)
            {
                SetGoKeyStatus(ex.Message, true);
            }
        }

        private void OnApplyGoKey()
        {
            var key = _goKeyBox.Text.Trim();
            if (string.IsNullOrEmpty(key)) { OnClearGoKey(); return; }
            string tail = key.Length >= 4 ? key.Substring(key.Length - 4) : key;
            if (MessageBox.Show(this, "确认应用此 Go 密钥（末 4 位：" + tail + "）？",
                "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.OpenCodeGoApiKey = key;
            _ctx.SaveConfig();
            _ctx.SubMonitor.SetApiKey(key);
            SetGoKeyStatus("已应用", false);
        }

        private void OnClearGoKey()
        {
            if (MessageBox.Show(this, "确定清空 Go 密钥吗？", "确认",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            _ctx.Config.OpenCodeGoApiKey = "";
            _ctx.SaveConfig();
            _ctx.SubMonitor.SetApiKey("");
            _goKeyBox.Text = "";
            SetGoKeyStatus("已清空", false);
        }

        private void SetGoKeyStatus(string text, bool error)
        {
            _lblGoKeyStatus.Text = text;
            _lblGoKeyStatus.ForeColor = error
                ? Color.FromArgb(0xE0, 0x4B, 0x4B)
                : Color.FromArgb(0x4A, 0xDE, 0x80);
        }
    }
}