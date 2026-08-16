using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using DeepSeekBalanceMonitor.Core;

namespace DeepSeekBalanceMonitor.UI
{
    /// <summary>
    /// 设置窗口：全部设置改动即时生效。含多账户管理（账户列表 + 编辑区）与开机自启。
    /// 布局：所有控件使用组内相对坐标，组高度按内容自动计算，保证全部设置项完整显示。
    /// </summary>
    public class SettingsForm : Form
    {
        private readonly AppContext _ctx;
        private readonly Timer _saveTimer; // 防抖保存：连续改动 800ms 后落盘

        // 跨方法使用的控件（其余控件均为局部变量）
        private ListBox _accountList;
        private TextBox _nameBox, _keyBox;
        private ComboBox _providerBox;
        private NumericUpDown _thresholdBox;
        private Button _btnTest, _btnApply, _btnDelete, _btnAdd;
        private Label _lblKeyStatus;
        private string _editingId; // 当前编辑账户 Id

        // 布局：窗体级 Y 游标
        private int _y = 12;

        // 深色主题（构造时确定）
        private readonly bool _dark;

        // 输入控件深色配色（WinForms 输入框默认白底，需显式设置）
        private static readonly Color DarkInputBg = Color.FromArgb(0x2D, 0x2D, 0x30);
        private static readonly Color DarkInputFg = Color.FromArgb(0xDD, 0xDD, 0xDD);

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

            // 主题跟随系统：深色系统下设置页也使用深色背景（标题栏已由 DarkTitleBar 处理）
            _dark = SystemTheme.IsDark();
            if (_dark)
            {
                BackColor = Color.FromArgb(0x1E, 0x1E, 0x1E);
                ForeColor = Color.FromArgb(0xDD, 0xDD, 0xDD);
            }
            else
            {
                BackColor = Color.White;
                ForeColor = Color.FromArgb(0x33, 0x33, 0x33);
            }

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
            BuildAccountsGroup();
            BuildOtherGroup();

            // 底部：打开数据文件夹
            var btnOpenData = new Button { Text = "打开数据文件夹", Width = 130, Height = 30, Location = new Point(14, _y + 6) };

            // 版本信息
            var lblVersion = new Label
            {
                AutoSize = true,
                Text = "版本 " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version,
                Location = new Point(310, _y + 12),
                ForeColor = SystemTheme.IsDark() ? Color.FromArgb(0x8A, 0x8A, 0x8A) : Color.Gray
            };
            Controls.Add(lblVersion);
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

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar.Apply(Handle); // 标题栏跟随系统主题
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

            AddCheck(g, "余额低于阈值时弹出通知", _ctx.Config.NotifyLowBalance, ref y,
                v => { _ctx.Config.NotifyLowBalance = v; MarkDirty(); });
            AddCheck(g, "消费突增时弹出通知", _ctx.Config.NotifySurge, ref y,
                v => { _ctx.Config.NotifySurge = v; MarkDirty(); });

            FinishGroup(g, y);
        }

        private void BuildAccountsGroup()
        {
            var g = AddGroup("账户");
            int y = 26;

            // 左列：账户列表
            _accountList = new ListBox { Location = new Point(14, y), Size = new Size(130, 190) };
            if (_dark)
            {
                _accountList.BackColor = DarkInputBg;
                _accountList.ForeColor = DarkInputFg;
            }
            _accountList.SelectedIndexChanged += OnAccountSelected;
            g.Controls.Add(_accountList);

            // 右列：编辑区（x=160 起）
            g.Controls.Add(new Label { Text = "名称：", AutoSize = true, Location = new Point(160, y + 4) });
            _nameBox = new TextBox { Location = new Point(230, y), Width = 190 };
            if (_dark)
            {
                _nameBox.BackColor = DarkInputBg;
                _nameBox.ForeColor = DarkInputFg;
            }
            _nameBox.TextChanged += (s, e) => { SaveEditingToAccount(); MarkDirty(); };
            g.Controls.Add(_nameBox);
            y += 36;

            g.Controls.Add(new Label { Text = "供应商：", AutoSize = true, Location = new Point(160, y + 4) });
            _providerBox = new ComboBox { Location = new Point(230, y), Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
            if (_dark)
            {
                _providerBox.BackColor = DarkInputBg;
                _providerBox.ForeColor = DarkInputFg;
            }
            foreach (var p in ProviderRegistry.All) _providerBox.Items.Add(new ProviderItem(p));
            _providerBox.SelectedIndexChanged += (s, e) => { SaveEditingToAccount(); MarkDirty(); };
            g.Controls.Add(_providerBox);
            y += 36;

            g.Controls.Add(new Label { Text = "API 密钥：", AutoSize = true, Location = new Point(160, y + 4) });
            _keyBox = new TextBox { Location = new Point(230, y), Width = 150, UseSystemPasswordChar = true };
            if (_dark)
            {
                _keyBox.BackColor = DarkInputBg;
                _keyBox.ForeColor = DarkInputFg;
            }
            _keyBox.TextChanged += (s, e) => { SaveEditingToAccount(); MarkDirty(); };
            g.Controls.Add(_keyBox);
            var chkShowKey = new CheckBox { Text = "显示", AutoSize = true, Location = new Point(384, y + 3) };
            chkShowKey.CheckedChanged += (s, e) => _keyBox.UseSystemPasswordChar = !chkShowKey.Checked;
            g.Controls.Add(chkShowKey);
            y += 36;

            g.Controls.Add(new Label { Text = "预警阈值：", AutoSize = true, Location = new Point(160, y + 4) });
            _thresholdBox = new NumericUpDown
            {
                Location = new Point(230, y), Width = 110, Minimum = 0, Maximum = 99999.99m, DecimalPlaces = 2
            };
            if (_dark)
            {
                _thresholdBox.BackColor = DarkInputBg;
                _thresholdBox.ForeColor = DarkInputFg;
            }
            // 阈值写入账户对象并即时更新悬浮窗颜色
            _thresholdBox.ValueChanged += (s, e) =>
            {
                var acc = EditingAccount;
                if (acc != null)
                {
                    acc.WarnThreshold = _thresholdBox.Value;
                    _ctx.Coordinator.Get(acc.Id)?.SetWarnThreshold(_thresholdBox.Value);
                }
                MarkDirty();
            };
            g.Controls.Add(_thresholdBox);
            g.Controls.Add(new Label { Text = "元", AutoSize = true, Location = new Point(348, y + 4) });
            y += 36;

            // 按钮行
            _btnAdd = new Button { Text = "新增账户", Location = new Point(160, y), Width = 84, Height = 30 };
            _btnDelete = new Button { Text = "删除", Location = new Point(252, y), Width = 54, Height = 30 };
            _btnTest = new Button { Text = "测试", Location = new Point(314, y), Width = 54, Height = 30 };
            _btnApply = new Button { Text = "应用", Location = new Point(376, y), Width = 54, Height = 30 };
            _btnAdd.Click += OnAddAccount;
            _btnDelete.Click += OnDeleteAccount;
            _btnTest.Click += OnTestKey;
            _btnApply.Click += OnApplyKey;
            g.Controls.Add(_btnAdd);
            g.Controls.Add(_btnDelete);
            g.Controls.Add(_btnTest);
            g.Controls.Add(_btnApply);
            y += 38;

            _lblKeyStatus = new Label
            {
                AutoSize = false, Location = new Point(160, y), Width = 270, Height = 32,
                ForeColor = Color.Gray
            };
            g.Controls.Add(_lblKeyStatus);
            y += 38;

            FinishGroup(g, y);

            RefreshAccountList();
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
            if (_dark)
            {
                cmbInterval.BackColor = DarkInputBg;
                cmbInterval.ForeColor = DarkInputFg;
            }
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

        // ============ 账户管理 ============

        /// <summary>当前编辑区对应的账户（未选择时返回 null）。</summary>
        private AccountConfig EditingAccount => _ctx.Config.Accounts.Find(a => a.Id == _editingId);

        /// <summary>当前列表选中的账户。</summary>
        private AccountConfig SelectedAccount =>
            _accountList.SelectedIndex >= 0 && _accountList.SelectedIndex < _ctx.Config.Accounts.Count
                ? _ctx.Config.Accounts[_accountList.SelectedIndex]
                : null;

        /// <summary>把编辑区四个字段写回当前编辑账户对象（切换/应用/测试前调用）。</summary>
        private void SaveEditingToAccount()
        {
            var acc = EditingAccount;
            if (acc == null) return;
            acc.Name = _nameBox.Text.Trim();
            if (_providerBox.SelectedItem is ProviderItem pi) acc.ProviderId = pi.Id;
            acc.ApiKey = _keyBox.Text.Trim();
            acc.WarnThreshold = _thresholdBox.Value;
        }

        /// <summary>把账户列表按当前 Accounts 重建，并选中 _editingId 对应账户（默认首个）。</summary>
        private void RefreshAccountList()
        {
            _accountList.Items.Clear();
            foreach (var a in _ctx.Config.Accounts) _accountList.Items.Add(a.Name);
            int idx = _ctx.Config.Accounts.FindIndex(a => a.Id == _editingId);
            if (idx < 0 && _ctx.Config.Accounts.Count > 0) idx = 0;
            _accountList.SelectedIndex = idx;
        }

        /// <summary>把选中账户加载到编辑区。</summary>
        private void LoadAccountToEdit()
        {
            var acc = SelectedAccount;
            if (acc == null)
            {
                _editingId = null;
                _nameBox.Text = "";
                _keyBox.Text = "";
                _thresholdBox.Value = 10m;
                _providerBox.SelectedIndex = -1;
                SetKeyStatus("未选择账户", true);
                return;
            }
            _editingId = acc.Id;
            _nameBox.Text = acc.Name;
            _keyBox.Text = acc.ApiKey;
            _thresholdBox.Value = Math.Min(Math.Max(acc.WarnThreshold, _thresholdBox.Minimum), _thresholdBox.Maximum);
            _providerBox.SelectedIndex = FindProviderIndex(acc.ProviderId);
            SetKeyStatus("", false);
        }

        private int FindProviderIndex(string providerId)
        {
            for (int i = 0; i < _providerBox.Items.Count; i++)
            {
                if ((_providerBox.Items[i] as ProviderItem)?.Id == providerId) return i;
            }
            return -1;
        }

        /// <summary>切换列表选中：先保存当前编辑到账户对象，再加载新选中账户。</summary>
        private void OnAccountSelected(object sender, EventArgs e)
        {
            SaveEditingToAccount();
            LoadAccountToEdit();
        }

        private void OnAddAccount(object sender, EventArgs e)
        {
            SaveEditingToAccount();
            var acc = new AccountConfig { Name = "新账户", ProviderId = "deepseek" };
            _ctx.Config.Accounts.Add(acc);
            ApplyAccounts();
            _editingId = acc.Id;
            RefreshAccountList();
            LoadAccountToEdit();
        }

        private void OnDeleteAccount(object sender, EventArgs e)
        {
            var acc = SelectedAccount;
            if (acc == null)
            {
                MessageBox.Show(this, "请先选择要删除的账户", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (MessageBox.Show(this, "确定删除账户「" + acc.Name + "」吗？",
                    "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            SaveEditingToAccount();
            _ctx.Config.Accounts.Remove(acc);
            if (_ctx.Config.ActiveAccountId == acc.Id)
                _ctx.Config.ActiveAccountId = _ctx.Config.Accounts.Count > 0 ? _ctx.Config.Accounts[0].Id : "";
            _editingId = null;
            ApplyAccounts();
            RefreshAccountList();
            LoadAccountToEdit();
        }

        /// <summary>把账户列表推给协调器重建监控，并保存配置。</summary>
        private void ApplyAccounts()
        {
            _ctx.Coordinator.SetAccounts(_ctx.Config.Accounts, _ctx.History,
                pid => ProviderRegistry.Get(pid));
            _ctx.Coordinator.ActiveAccountId = _ctx.Config.ActiveAccountId;
            _ctx.Coordinator.SetInterval(_ctx.Config.RefreshIntervalSeconds);
            _ctx.SaveConfig();
        }

        private async void OnTestKey(object sender, EventArgs e)
        {
            SaveEditingToAccount();
            var acc = SelectedAccount;
            if (acc == null)
            {
                SetKeyStatus("请先选择账户", true);
                return;
            }
            var key = _keyBox.Text.Trim();
            if (string.IsNullOrEmpty(key))
            {
                SetKeyStatus("请先输入密钥", true);
                return;
            }
            var provider = ProviderRegistry.Get(acc.ProviderId);
            if (provider == null)
            {
                SetKeyStatus("未知供应商：" + acc.ProviderId, true);
                return;
            }
            SetKeyStatus("正在测试密钥...", false);
            try
            {
                var result = await provider.GetBalanceAsync(key);
                SetKeyStatus("✅ 密钥有效，当前余额 ¥" + (result.Remaining?.ToString("F2") ?? "-"), false);
            }
            catch (BalanceQueryException ex)
            {
                SetKeyStatus("❌ " + ex.Message, true);
            }
        }

        private void OnApplyKey(object sender, EventArgs e)
        {
            SaveEditingToAccount();
            var acc = SelectedAccount;
            if (acc == null)
            {
                SetKeyStatus("请先选择账户", true);
                return;
            }
            ApplyAccounts();
            _ctx.Coordinator.Get(acc.Id)?.RefreshNow(); // 立即查询，不等下一个刷新周期
            SetKeyStatus("已应用，正在刷新余额...", false);
            RefreshAccountList(); // 账户改名后刷新列表显示
        }

        private void SetKeyStatus(string text, bool error)
        {
            _lblKeyStatus.Text = text;
            _lblKeyStatus.ForeColor = error ? Color.FromArgb(0xC0, 0x39, 0x2B) : Color.FromArgb(0x1E, 0x84, 0x41);
        }

        /// <summary>供应商下拉项：显示 DisplayName，携带 Id。</summary>
        private class ProviderItem
        {
            public string Id { get; }
            public string Name { get; }
            public ProviderItem(IBalanceProvider p) { Id = p.Id; Name = p.DisplayName; }
            public override string ToString() => Name;
        }
    }
}
