<div align="center">

# <image src="https://github.com/HUSX100/IslandCaller/blob/master/Icon.png" height="28" width="28"/> IslandCaller（ClassIsland 2.0 插件）

一个为课堂场景设计的轻量级点名插件。  
目标是：**上手快、抽取公平、课堂操作顺手**。

[![正式版 Release](https://img.shields.io/github/v/release//HickoryTrail/IslandCaller?style=flat-square&color=%233fb950&label=正式版)](https://github.com/HickoryTrail/IslandCaller/releases/latest)
[![下载量](https://img.shields.io/github/downloads//HickoryTrail/IslandCaller/total?style=social&label=下载量&logo=github)](https://github.com//HickoryTrail/IslandCaller/releases/latest)
[![GitHub Repo Languages](https://img.shields.io/github/languages/top//HickoryTrail/IslandCaller?style=flat-square)](https://github.com//HickoryTrail/IslandCaller/search?l=c%23)
</div>

---

## 插件特色

- **一键点名**：支持快速抽取 1 人。
- **自定义抽取人数**：支持图形界面选择人数后再抽取。
- **名单可视化编辑**：在设置页直接增删学生、修改性别、手动权重。
- **多格式导入**：支持 `.txt`、SecRandom `.json`、`.csv`。
- **公平性优化**：综合“手动权重 + 本节课记录 + 长期历史”动态计算抽取概率。
- **悬浮窗快捷操作**：支持启用/禁用、缩放、位置记忆。
- **下课保护**：可设置在非上课时间禁用点名功能。

---

## 使用教程

### 1. 安装插件

1. 打开 ClassIsland 插件市场。
2. 搜索并安装 **IslandCaller**。
3. 安装后进入：`应用设置 -> 插件 -> IslandCaller 设置`。

### 2. 准备名单

你可以使用以下任一方式：

- **手动录入**：在“档案编辑”里直接添加学生；
- **导入名单**：点击“导入名单”，选择支持格式文件。

支持格式：

- 文本名单：`*.txt`（姓名用空格/逗号/换行分隔）
- SecRandom 名单：`*.json`
- CSV 名单：`*.csv`

### 3. 开始点名

- **快速点名（1 人）**：直接调用简单抽取入口；
- **自定义点名（多人）**：打开高级界面设置人数后抽取；
- **悬浮窗点名**：在设置中启用悬浮窗后可快速操作。

### 4. 可选设置建议

- 开启“下课禁用”：避免非上课时误触发；
- 按屏幕调整“悬浮窗缩放系数”；
- 按需设置学生“手动权重”，实现更灵活的抽取策略。

---

## URI 调用方式（可用于快捷方式/联动）

- 简单抽取（1 人）

```text
classisland://plugins/IslandCaller/Simple/1
```

- 高级抽取（GUI 指定人数）

```text
classisland://plugins/IslandCaller/Advanced/GUI
```

---

## 导入文件示例

### 文本名单（`.txt`）

>[!tip]
>相邻姓名之间使用逗号，空格，或换行分隔

```text
张三 李四
王五,赵六
钱七
```

### CSV 名单（`.csv`）

>[!important]
> 要求不带标题行；如有性别列，请在导入时配置男/女映射值。

```csv
1,张三,男
2,李四,女
3,王五,男
```

---

## 常见问题

- **点击快捷方式无反应？**
  - 请确认 ClassIsland 已启用 URI 协议注册。
- **导入后性别不正确？**
  - 请在导入对话框中检查男/女映射文本是否与源文件一致。
- **悬浮窗位置异常？**
  - 调整一次位置后会自动记忆，下次启动会按保存位置显示。

---

## 反馈与支持

- 项目地址：<https://github.com/HUSX100/IslandCaller>
- 问题反馈：<https://github.com/HUSX100/IslandCaller/issues>
---

## 致谢

本项目使用了以下第三方库：

- ClassIsland.PluginSdk

## 许可

本项目使用 GPL3 许可证进行开源，详细信息请查看 LICENSE 文件。
