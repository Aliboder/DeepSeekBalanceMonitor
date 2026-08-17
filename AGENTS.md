# QuotaMonitor 项目记忆

> 给接手本项目的 AI 的工作约定与踩坑记录。开始任何任务前先读本文件 + 用户全局 `~/.config/opencode/AGENTS.md`。

## 项目概况

- **是什么**：Windows 常驻悬浮窗小工具，实时监控 DeepSeek API 余额 + OpenCode Go 套餐余量（5 小时/周/月），余额不足/消费突增自动通知，含统计面板（近 14 天每日消费柱状图 + 套餐用量进度）。
- **技术栈**：C# / WinForms，目标 .NET Framework 4.8（用 .NET 8 SDK 编译），Inno Setup 打包，数据存 `文档\QuotaMonitor\`（密钥 DPAPI 加密）。
- **仓库**：https://github.com/Aliboder/QuotaMonitor.git，分支 `master`，当前 Release **v1.1.0**。
- **用户**：靠 AI 结对开发。**不会用 git——涉及 git 操作主动代做**；不熟悉的技术要讲解，不要假设他懂。

## 构建 / 测试 / 打包

```powershell
$dotnet = "C:\Users\Aliboder\.dotnet\dotnet.exe"

# 编译
& $dotnet build "src\QuotaMonitor\QuotaMonitor.csproj" -c Release

# 核心测试（26/26，含 OpenCodeGo 解析测试）
& $dotnet build "tools\CoreTests\CoreTests.csproj" -c Release
& "tools\CoreTests\bin\Release\net48\CoreTests.exe"

# 发布（打包前先杀进程，否则 exe 被占用）
Get-Process -Name QuotaMonitor -ErrorAction SilentlyContinue | Stop-Process -Force
& $dotnet publish "src\QuotaMonitor\QuotaMonitor.csproj" -c Release -r win-x64 --self-contained false -o "publish\Release"

# 打安装包 → installer\QuotaMonitor-Setup-v1.1.0.exe
& "C:\Users\Aliboder\AppData\Local\Programs\Inno Setup 6\ISCC.exe" "installer.iss"

# 上传 Release（覆盖）
gh release upload v1.1.0 "installer\QuotaMonitor-Setup-v1.1.0.exe" --repo Aliboder/QuotaMonitor --clobber
```

**启动验证**：`Start-Process "publish\Release\QuotaMonitor.exe" -ArgumentList "--show-stats --show-settings"` 同时打开统计+设置窗口。

## 项目结构

- `src/QuotaMonitor/` — 主程序（net48）
  - `Core/` — Config（设置模型）、ConfigService（JSON+DPAPI）、HistoryStore（余额记录）、DeepSeekApiClient、OpenCodeGoClient、BalanceMonitor（轮询状态机）、SubscriptionMonitor、AlertEngine、SystemTheme（深浅色检测+Changed 事件）、DarkTitleBar、Logger
  - `UI/` — FloatingWindow（悬浮窗）、SettingsForm（设置 4 页）、StatsForm（统计面板）、TrayIcon、NavSidebar/ModernSlider/ModernCheckBox/ModernComboBox/CardPanel/CardButton（自绘控件）、MenuTheme、**DebugProbe**（调试工具）
- `tools/CoreTests/` — 核心逻辑测试
- `docs/` — 技术架构.md / 构建打包.md / 开发计划.md / UI元素定位调试通用方案.md
- `功能说明.md` / `README.md` — 用户向文档

## 关键约定与坑（务必先看）

1. **主题体系（Win11 配色 + 运行时跟随系统）**
   - 所有颜色**硬编码**，不做动态 Theme 类。深色：窗口 `#202020`/表面 `#2D2D2D`/输入 `#323232`/主文字 `#E0E0E0`/次要 `#9D9D9D`/分隔 `#404040`；浅色：`#FFFFFF`/`#F3F3F3`/`#1A1A1A`/`#6E6E6E`/`#EBEBEB`；强调蓝 `#0067C0`。
   - `SystemTheme.Changed` 事件（监听 `SystemEvents.UserPreferenceChanged` Category=Color）→ 各窗口订阅调 `ApplyTheme()`；自绘控件加 `ApplyTheme(bool dark)`（`_dark` 改可变 + Invalidate）。
   - 悬浮窗是深色半透明自绘设计，**不随系统切换**。

2. **统计页自绘（StatsForm）**
   - 卡片圆角背景画在 Panel 的 `Paint` 事件（`PaintCard`）；状态徽章**不是子控件**，直接画进父卡片 Paint（`DrawBadge`），否则出现"Label 基类矩形底色盖不掉"问题。
   - `ColorProgressBar`（嵌套 Control 子类）自绘渐变进度条；柱状图用原生 Panel 当柱子 + `PaintGrid` 画坐标/网格。
   - **无 DataGridView/表格、无 CSV 导出、无"预计可用天数"**——这些已删除，别在文档/代码里复活。

3. **DPI 缩放**：`AutoScaleMode.None` + `OnLoad` 用 `GetDpiForWindow` 取真实 DPI（`DeviceDpi` 恒 96 不可用）等比 `Scale` 控件树。`Scale(1.5)` 会放大字体导致溢出——用 `Tag` 存设计字号，`RestoreFonts` 精确还原。

4. **ToolTip 单例冲突**：同一 Form 只能有一个活跃 ToolTip。`DebugProbe.Attach(root, dark, tip)` 的 `tip` 参数复用窗口自身 `_tip`，不要另建静态 ToolTip 挂到有自己 ToolTip 的窗体（设置页无自建 ToolTip 则用默认）。

5. **调试模式**：开关是**用户设置项** `Config.DebugMode`（设置 → 其他 → 界面调试模式），不是改代码常量。开启后页面构造挂载 DebugProbe；设置页勾选时对已打开页面补调 `AttachDebugProbe()` **即时生效，无需重启**。控件树快照导出到 `%TEMP%\opencode\`。

6. **无数据提示 Label bug**：进过一次"无数据"状态后，`hasData=true` 时必须显式 `Visible=false`，否则永久残留。

7. **git/发布习惯**：提交按逻辑分组；改动涉及安装包时最后重新 `ISCC` 打包并更新 exe（installer/ 下 exe 已入库）；push 前跑 CoreTests。Release 版本号目前 v1.1.0。

## 常用操作

- 改 UI 颜色/布局 → 改完 build + publish + 用 `--show-stats --show-settings` 打开验证，再让用户确认。
- 定位 UI 重叠/错位问题 → 让用户在设置开启"界面调试模式"，悬停看 Tooltip/高亮，或读 `%TEMP%\opencode\*_controls.txt` 控件树。方法论见 `docs/UI元素定位调试通用方案.md`。
- 涉及新功能 → 先更新 `功能说明.md`（用户向）与 `docs/技术架构.md`（开发向），改动大时同步 `README.md`。