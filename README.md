# QuotaMonitor（AI 额度监控）

一个小巧的 Windows 桌面工具：实时监控 **DeepSeek API 账户余额**与 **OpenCode Go 套餐余量**，额度不足或消费异常时自动提醒，并记录消费历史供统计分析。

## 功能一览

- **桌面悬浮窗**：始终置顶显示当前额度，颜色随状态变化（绿=充足 / 红=不足 / 橙=查询异常），支持拖动、点击穿透、悬停增亮
- **双模式监控**：余额模式显示 DeepSeek 余额；套餐模式显示 OpenCode Go 套餐余量（5 小时 / 周 / 月 三个窗口，单击悬浮窗循环切换），可切换显示剩余或已用额度
- **系统托盘**：常驻任务栏通知区，图标颜色反映余额状态，悬停即可查看余额
- **智能告警**：
  - 余额低于预警阈值时通知（仅在临界点提醒一次）
  - 今日消费超过近 7 天日均 3 倍时提醒「消费突增」（每天最多一次）
- **统计面板**：余额走势图、日均/月均消费、预计可用天数、历史记录表，支持导出 CSV
- **定时隐藏**：可让悬浮窗暂时消失（5 分钟 / 30 分钟 / 3 小时 / 自定义），到期自动恢复
- **完整设置**：侧边导航式设置窗口（显示 / 提醒 / 密钥 / 其他），全部设置即时生效

## 快速开始

> 以下面向使用软件的人（非开发者）。

1. 从 [GitHub Releases](https://github.com/Aliboder/QuotaMonitor/releases) 下载安装包（`QuotaMonitor-Setup-v*.exe`），按提示完成安装。
2. 打开软件后，右键悬浮窗（或托盘图标）→ **设置** → **密钥**。
3. 粘贴你的 API Key，点击**测试**验证，再点**应用**：
   - DeepSeek API Key：在 [platform.deepseek.com](https://platform.deepseek.com) → API Keys 页面创建
   - OpenCode Go API Key：在 [opencode.ai](https://opencode.ai) 的订阅/API 页面获取（未使用可留空）
4. 回到桌面即可看到实时额度，悬浮窗可拖动到任意位置（位置会被记住）。

额度数据来源于官方接口（DeepSeek `GET https://api.deepseek.com/user/balance` 与 OpenCode Go 官方接口），仅使用你自己的 API Key 查询，不上传任何数据到第三方。

## 数据保存在哪里

所有数据存放在 Windows 用户目录下 `文档\QuotaMonitor\`（旧版本数据目录 `文档\DeepSeek余额监控\` 会在升级后自动迁移，无需手动备份）：

| 文件 | 内容 |
|------|------|
| `设置.json` | 全部设置（API 密钥使用 Windows 系统加密存储） |
| `余额记录.json` | 余额历史记录 |
| `日志\` | 运行日志（自动保留最近若干份） |

## 开发者信息

- 技术栈：C# / WinForms，目标框架 .NET Framework 4.8（Windows 10/11 自带，无需安装任何运行时）
- 详见 [docs/技术架构.md](docs/技术架构.md) 与 [docs/开发计划.md](docs/开发计划.md)
- 构建与打包方式见 [docs/构建打包.md](docs/构建打包.md)
