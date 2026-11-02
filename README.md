# MCHOSE G3 任务栏电量

在 Windows 任务栏托盘实时显示 MCHOSE G3 无线鼠标电量的轻量工具。原生 Win32/C# 实现，不依赖厂商驱动、不驻留后台服务、无需管理员权限。

> A lightweight Windows tray utility that displays the battery level of the MCHOSE G3 wireless mouse by reading its HID reports directly.

## 功能特性

- **实时电量**：解析鼠标上报的 HID 报文，电量变化即时反映到托盘图标
- **两种图标样式**：数字百分比，或仿电池图形（可在右键菜单切换）
- **颜色分级**：≥50% 绿色，21%–49% 黄色，≤20% 红色，未连接为灰色
- **深浅色自适应**：读取系统主题，自动调整电池图形边框颜色以适配浅色/深色任务栏
- **详情面板**：左键单击图标，显示设备名称、VID/PID、UsagePage/Usage、设备路径与最近一帧的十六进制内容
- **断连自愈**：设备掉线后自动重连，退避间隔为 2s / 5s / 10s / 30s
- **缓存兜底**：记住最近一次电量，鼠标休眠或断开时仍显示上次数值并标注「缓存」
- **陈旧检测**：超过阈值未刷新即标记数据过期，避免把旧值当作实时值
- **开机自启**：写入 `HKCU\...\Run`，仅影响当前用户
- **单实例运行**：互斥量保护，避免重复启动
- **异常留痕**：未捕获异常写入本地日志，不弹窗打扰

## 系统要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / 11（x64 或 x86） |
| 运行时 | .NET Framework 4.x（系统自带，无需另行安装） |
| 权限 | 普通用户即可，无需管理员 |

## 构建

仓库不包含编译产物，需要本地构建：

```bat
build.cmd
```

构建脚本调用 .NET Framework 自带的编译器（`%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`），因此**无需安装 Visual Studio 或 .NET SDK**。输出到 `bin\` 目录：

| 文件 | 说明 |
|---|---|
| `G3Power.exe` | 托盘主程序（约 24 KB） |
| `MouseBattery.Core.dll` | HID 通信与协议解析核心库 |
| `probe.exe` | 命令行诊断工具 |

## 使用

1. 双击 `bin\G3Power.exe`，托盘出现电量图标
2. 左键单击图标 → 打开详情面板（点击窗口外或按 `Esc` 关闭）
3. 右键单击图标 → 菜单：

```
电量：85%
设备：MCHOSE G3（258A:0138）
状态：已连接
──────────────────
查看详情
立即重连
开机自动启动        ✓
托盘图标样式     ▸  数字 / 电池图形
──────────────────
退出
```

## 配置

配置文件位于 `%LOCALAPPDATA%\G3Power\settings.ini`，修改后重启程序生效。

| 键 | 默认值 | 说明 |
|---|---|---|
| `IconStyle` | `0` | `0` 数字样式，`1` 电池图形 |
| `StaleSeconds` | `180` | 超过该秒数未刷新即视为过期数据，最小值 `30` |
| `VendorId` | `0x258A` | 目标设备 VID，十六进制 |
| `ProductId` | `0x0138` | 目标设备 PID，十六进制 |
| `BcdDecode` | `0` | `1` 时按 BCD 编码解析电量字节 |

同目录下还有两个运行时文件：

- `state.txt`：上次电量的持久化缓存（`percent` / `utc` / `name`）
- `error.log`：未捕获异常堆栈，仅在出错时产生

## 诊断工具

`probe.exe` 用于排查设备识别与协议解析问题：

```bat
probe.exe --list        :: 列出全部 HID 集合（VID/PID、UsagePage、报文长度、设备路径）
probe.exe --feature     :: 读取一次 520 字节的特征报告，逐字节打印非零项
probe.exe --seconds 30  :: 监听 30 秒，实时打印收到的每一帧及其解析结果
```

输出示例（报文长度与版本号随固件不同，以实际输出为准）：

```
目标设备 258A:0138  UsagePage 0xFF02 / Usage 0x0002
  [0] \\?\hid#vid_258a&pid_0138&mi_02&col01#...
      IN=9 OUT=0 FEAT=520 版本=0x0100

14:32:07.118  [09 0A 05 55 00 00 00 00]  电量 85%
```

## HID 协议说明

设备匹配条件：`VID 0x258A` + `PID 0x0138` + `UsagePage 0xFF02` + `Usage 0x0002`，且输入报文长度不小于 8 字节。

输入报文布局（8 字节）：

| 偏移 | 值 | 含义 |
|---|---|---|
| 0 | `0x09` | Report ID |
| 1 | `0x0A` | 事件帧标记 |
| 2 | 事件类型 | 见下表 |
| 3 | 值 | 事件携带的数值 |
| 4–7 | — | 保留 |

事件类型：

| 值 | 类型 | 值域含义 |
|---|---|---|
| `0x02` | 设备状态 | `1` 接入，`0` 断开 |
| `0x03` | DPI 变更 | DPI 档位编号 |
| `0x04` | 保留 | — |
| `0x05` | 电量 | `0`–`100` |
| `0x06` | 命令回执 | 非零为成功 |

此外设备提供长度 520 字节的特征报告，可用 `probe.exe --feature` 导出观察。

> 上述格式来自对设备实际报文的观察，**非厂商公开文档**，不同固件版本可能存在差异。

## 目录结构

```
.
├── build.cmd                     # 一键构建脚本
├── src
│   ├── MouseBattery.Core         # 核心库：HID 枚举 / 传输 / 协议 / 缓存 / 配置
│   │   ├── HidDeviceEnumerator.cs
│   │   ├── HidTransport.cs
│   │   ├── MchoseG3Protocol.cs
│   │   ├── BatteryMonitor.cs
│   │   ├── BatteryStateStore.cs
│   │   ├── AppSettings.cs
│   │   ├── HidDeviceInfo.cs
│   │   └── NativeMethods.cs
│   └── MouseBattery.Tray         # 托盘程序：菜单 / 图标渲染 / 详情窗 / 自启
│       ├── Program.cs
│       ├── TrayContext.cs
│       ├── BatteryIconRenderer.cs
│       ├── DetailsForm.cs
│       ├── AutoStart.cs
│       └── app.manifest
└── tools
    └── Probe                     # 命令行诊断工具
        └── Probe.cs
```

## 已知限制

- 仅在 `258A:0138` 型号上验证；同系列其他型号可通过 `settings.ini` 修改 VID/PID 尝试适配，但报文格式未必一致
- 电量由设备**主动上报**，程序不轮询设备，因此鼠标休眠期间不会有新数据
- 仅读取设备，不发送任何写入命令，不修改 DPI、灯光等配置
- 未做代码签名，首次运行可能触发 SmartScreen 提示

## 免责声明

本项目为**互操作性目的**实现的第三方工具，通过系统公开的 HID 接口读取设备自身广播的报文，与 MCHOSE（迈从）官方无任何关联，未获其授权、认可或支持。MCHOSE 及相关产品型号为其各自权利人的商标，此处仅用于说明兼容对象。

使用本软件产生的任何后果由使用者自行承担。

## 许可证

[MIT](LICENSE) © 2026 WeiJianBin
