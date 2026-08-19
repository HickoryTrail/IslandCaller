<div align="center">

# <image src="https://raw.githubusercontent.com/HickoryTrail/IslandCaller/master/Icon.png" height="28" width="28"/> IslandCaller

一个为课堂场景设计的轻量级点名插件。  
目标是：**上手快、抽取公平、课堂操作顺手**。

[![正式版 Release](https://img.shields.io/github/v/release/HickoryTrail/IslandCaller?style=flat-square&color=%233fb950&label=正式版)](https://github.com/HickoryTrail/IslandCaller/releases/latest)
[![下载量](https://img.shields.io/github/downloads/HickoryTrail/IslandCaller/total?style=social&label=下载量&logo=github)](https://github.com/HickoryTrail/IslandCaller/releases/latest)
[![GitHub Repo Languages](https://img.shields.io/github/languages/top/HickoryTrail/IslandCaller?style=flat-square)](https://github.com/HickoryTrail/IslandCaller/search?l=c%23)

</div>

---

## 简介

IslandCaller 是基于 ClassIsland 2.0 插件 SDK开发的开源课堂点名插件，使用 .NET 10 与 Avalonia UI 构建，与 ClassIsland 的课程表、科目和提醒系统深度联动。

点名不是简单的随机数：插件综合**手动权重、本节课防重复记录、长期点名历史**动态计算每个学生的抽取概率，让课堂点名既公平又灵活。

---

> [!NOTE]
> > **关于 IslandCaller 开发计划调整的公告**
>
> 致 IslandCaller 的所有用户与开发者：
>
>大家好。
>
>首先要感谢每一位用户和开发者对 IslandCaller 的长期支持与信任，正是你们的反馈、Issue 和 Pull Request，让这个项目一步步走到今天。
>
>由于我即将进入高三，无暇继续开发 IslandCaller。因此，在此向大家说明接下来的开发安排：
>
> - **未来一年内，IslandCaller 将暂停发布较大的功能更新与常规 Bug 修复。**
> - **项目日常维护将全部交由 Codex 负责。** 对于严重影响日常使用的 Bug，Codex 会及时进行修复。
> - 为了帮助 Codex 更准确地定位和解决问题，**希望大家在提交 Issue 时尽量把问题描述得详细、清晰**，例如：复现步骤、运行环境、版本号、相关日志等，这将大大提升修复效率。
>
> **同时，也欢迎有时间的开发者通过 Pull Request 参与到项目中来。** 无论是 Bug 修复、代码优化还是新功能，只要经过 review 确认，我都会尽快合并。你们的每一份贡献，都会让 IslandCaller 变得更好。
>
> 关于新功能建议：欢迎大家继续通过 Issue 留言，我会在看到后尽快回复，并将有价值的建议记录下来，留待后续开发时参考。
>
> 请相信，这只是一个短暂的暂停，而不是告别。
>
> **明年暑假，高考结束之后，我会带着新的状态回到 IslandCaller，继续推进项目的开发。** 届时，还请大家多多支持。
> 
> 再次感谢各位一路以来的理解与支持。我们明年见！
>
> —— HickoryTrail
> 2026 年 8 月 19 日

---

## 功能特性

### 🎯 随机点名

- **一键点名**：单击即抽取 1 人；

- **自定义抽取**：悬浮窗/URI 打开“自定义抽取”窗口，用滑块指定 1~5 人后点名；

- **公平性算法**：算法智能结合手动权重和历史记录，确保长期次数均衡，短期不重复点名；

- **下课禁用**：课间（非上课时间）自动禁用所有点名功能，避免误触发；

- **提醒时长可调**：提醒展示时长可按需配置；

### 📢 点名结果展示与播报

- **双通道展示**：可同时开启“ClassIsland 提醒”（遮罩式通知）与“IslandCaller 独立结果窗口”（可选用 Fluent 或 LiquidGlass 主题）；
- **自适应对比度**：独立结果窗口会自动分析屏幕背景亮度，在深色/浅色背景下自动切换黑白色文字，保证清晰可读；
- **TTS 语音播报**：支持 `ClassIsland` 语音与 `OmniTTS` 两个提供方（需ClassIsland安装OmniTTS插件，并配置供应商），可自定义播报前/后文本。

### 🪟 悬浮窗

- **三种布局**：完整、紧凑、Mini；
- **两种主题**：Fluent（亚克力模糊）与实验性 LiquidGlass（GPU 液态玻璃材质）；
- **自由调整**：大小缩放、位置记忆、自动限制在屏幕范围内、超级置顶；
- **触控友好**：鼠标与触控/手写笔均可拖动，点击即点名、拖动不误触。

### 📋 名单档案

- **多档案管理**：可创建多份独立名单，设置默认名单，一键切换；
- **可视化编辑**：新增档案编辑器直接增删学生
- **按科目自动切换**：将科目与名单绑定后，上课时根据 ClassIsland 当前科目自动切换对应名单，换课自动重置本节课记录；
- **多格式导入**：支持文本名单（`.txt`）、SecRandom 名单（`.json`）、CSV 名单（`.csv`），CSV/SecRandom 可配置姓名列、性别列与男/女映射文本；

### 🔗 自动化与联动

- **ClassIsland 行动**：在自动化中可直接使用“IslandCaller 行动”菜单——**随机点名**、**启用悬浮窗**、**禁用悬浮窗**、**切换档案**（可指定目标档案）；
- **URI 调用**：支持通过 `classisland://` 协议从快捷方式、其他插件或自动化触发点名（见下文）。

### 💾 数据迁移

- **`.iscdoc` 数据包**：一键导出全部设置 + 名单 + 历史记录为数据包，换机/重装时导入即可完整恢复。

---

## 环境要求

| 项目   | 要求                                       |
| ---- | ---------------------------------------- |
| 操作系统 | Windows 10 2004（10.0.19041）及以上，x64       |
| 主程序  | ClassIsland 2.1.1 及以上（插件 API 版本 2.1.1.0） |

## 安装

1. 打开 ClassIsland，进入 **插件市场**；
2. 搜索 **IslandCaller** 并安装；
3. 安装后进入 `应用设置 -> 插件 -> IslandCaller 设置` 完成配置。

> 也可以从 [Releases](https://github.com/HickoryTrail/IslandCaller/releases/latest) 下载 `IslandCaller.Plugin2.cipx` 手动安装（下载后请核对文件 MD5）。

首次启动会自动创建一份示例名单并弹出欢迎提示，替换为自己的名单即可开始使用。

## 快速上手

1. **准备名单**：在“档案设置”中新建名单，或在档案编辑器里点击“导入名单”从 `.txt` / SecRandom `.json` / `.csv` 导入；
2. **开始点名**：点击悬浮窗主按钮一键抽取 1 人；点击副按钮（或通过 URI）打开自定义抽取窗口抽取多人；
3. **按需调整**：在设置页开启/关闭“下课禁用”“打断点名”，选择点名结果展示渠道与 TTS 播报提供方。

## URI 调用方式（可用于快捷方式/联动）

- 简单抽取（1 人）：

```text
classisland://plugins/IslandCaller/Simple/1
```

- 高级抽取（弹出窗口，GUI 指定人数，1~5 人）：

```text
classisland://plugins/IslandCaller/Advanced/GUI
```

## 导入文件示例

### 文本名单（`.txt`）

> [!tip]
> 名单只包含姓名，相邻姓名使用空格、逗号或换行分隔；性别、权重自动为默认值。

```text
张三 李四
王五,赵六
钱七
```

### CSV 名单（`.csv`）

> [!important]
> 文件不能包含标题行；姓名列与性别列（可选）在导入对话框中指定，男/女映射文本需与源文件一致，无法识别的性别行会被跳过。

```csv
1,张三,男
2,李四,女
3,王五,男
```

### SecRandom 名单（`.json`）

> [!tip]
> 每个键为学生姓名，值为包含 `gender` 字段的对象；可在导入对话框中按需读取性别并配置映射。

```json
{
  "张三": { "gender": "男" },
  "李四": { "gender": "女" }
}
```

## 常见问题

- **点名按钮无反应？**
  
  - 请确认当前不在课间（“下课禁用”开启时课间会禁用点名），并检查插件是否已完成初始化（可在 ClassIsland 日志中搜索 `IslandCaller`）。

- **OmniTTS 播报不生效？**
  
  - 未检测到 OmniTTS 服务时，TTS 提供方会自动回退为“无”，请确认已安装并启用 OmniTTS 插件。

- **导入后性别不正确？**
  
  - 请检查导入对话框中的男/女映射文本是否与源文件中的写法完全一致。

- **LiquidGlass 主题导致异常？**
  
  - LiquidGlass 为实验性主题，仅供开发测试，存在内存泄漏与崩溃风险，教学/生产环境请使用 Fluent 主题。

- **我的数据保存在哪里？**
  
  - 名单与历史记录存放在 `%AppData%\IslandCaller`，设置保存在注册表 `HKCU\Software\IslandCaller`；换机迁移建议使用 `.iscdoc` 数据包导出/导入。

## 开发与构建

技术栈：.NET 10 / Avalonia UI / ClassIsland.PluginSdk / MorerialsAvalonia / OmniTTS.Shared

```powershell
# 还原依赖（非 Windows 环境需启用 Windows 目标）
dotnet restore IslandCaller.slnx -p:EnableWindowsTargeting=true

# 构建（Release）
dotnet build IslandCaller.slnx --configuration Release --no-restore --nologo -p:EnableWindowsTargeting=true

# 本地打包发布包（生成 .cipx、MD5 与发布说明）
.\scripts\Package-Release.ps1 -Version x.x.x.x
```

发布流程由 GitHub Actions 自动完成：推送 `x.x.x.x` 格式的四段版本号 tag 后，CI 会构建插件、生成 `IslandCaller.Plugin2.cipx` 及其 MD5，并以 [docs/CHANGELOG](docs/CHANGELOG) 中对应版本的更新日志作为发布说明创建 GitHub Release。

## 反馈与贡献

- 项目地址：<https://github.com/HickoryTrail/IslandCaller>
- 问题与建议：<https://github.com/HickoryTrail/IslandCaller/issues>
- 欢迎提交 PR 参与 Bug 修复与功能开发；提交 Issue 时请尽量详细描述复现步骤、环境与日志，便于快速定位。

## 致谢

本项目直接引用了以下开源库：

- [ClassIsland.PluginSdk](https://github.com/HelloWRC/ClassIsland)
- [MorerialsAvalonia](https://github.com/Morerials/MorerialsAvalonia)
- [OmniTTS.Shared](https://github.com/ClassIsland/OmniTTS)

## 许可

本项目使用 [GPL-3.0](LICENSE) 许可证开源。