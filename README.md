# SoundGuard — 系统级实时响度保护器

[![License: MIT](https://img.shields.io/badge/license-MIT-green)](LICENSE)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Status](https://img.shields.io/badge/status-MVP-orange)](docs/roadmap.md)
[![IIIA-4](https://img.shields.io/badge/IIIA-4-9370DB)](https://github.com/ErSanSan233/IIIA)

实时捕获 Windows 系统音频输出，监测感知响度（LUFS）与峰值电平（dBFS），在音量过大时自动进行
增益衰减或系统静音，保护听力与耳机设备。设计目标：**零音质损失、低延迟、不改动声场**。

> **免责声明**：本项目用于听力保护研究，不构成医疗设备或专业听力防护建议。若你已有听力损伤，
> 请咨询医生或听力学家。本项目由AI主导构建，开发者不对实际使用效果负责。

---

## 功能特性

- ✅ **WASAPI Loopback 捕获**：共享模式捕获默认扬声器的最终混音，float32，不重采样、不转位深、不混音
- ✅ **真实峰值检测**：实时 dBFS 样本峰值 + 8x 超采样真实峰值（True Peak）
- ✅ **ITU-R BS.1770-4 响度计**：Momentary 400ms / Short-term 3s LUFS，含绝对门限与相对门限
- ✅ **双阶段保护引擎**：阶段 A 峰值软拐点前视限幅器 + 阶段 B 响度限幅器
- ✅ **分级响应**：轻微超标渐进衰减 / 严重超标快速压限 / 极端危险静音 + 通知
- ✅ **一键恢复 + 10 秒自动恢复**
- ✅ **系统主音量控制与静音**（Core Audio）
- ✅ **WPF 深色 UI**：四路表头 + 阈值滑块 + 状态灯
- ✅ **托盘图标 + 开机自启**
- ✅ **配置持久化（JSON）与事件日志**
- ✅ **单元测试**：LUFS 标定、限幅器、滤波器、环形缓冲区

## 快速开始

### 环境要求

- Windows 10 / 11（x64）
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)（`dotnet --list-sdks` 应显示 8.x）

### 构建与运行

```powershell
git clone https://github.com/<你的用户名>/SoundGuard.git
cd SoundGuard

dotnet restore
dotnet build -c Release

# 运行 UI
dotnet run --project src\SoundGuard.App -c Release

# 运行单元测试
dotnet test
```

首次 `restore` 会从 NuGet 拉取唯一第三方依赖 **NAudio 2.2.1**（MIT 开源，仅用于 WASAPI 捕获与
Core Audio 音量封装；其余 DSP/引擎代码全部在仓库内，无闭源组件）。

## 使用说明

- 启动后默认显示主窗口（可在配置中改为最小化到托盘）。
- 拖动「响度阈值」「峰值阈值」滑块即时生效。
- 「压限幅度」50% 为默认全额压限；可下调（更轻）或上调（更强）。
- 「直通 (Bypass)」关闭所有保护动作（表头继续测量，用于 A/B 对比）。
- 静音后点击「恢复」立即解除；默认 10 秒后且信号已安全时自动恢复。
- 检测到全屏应用自动进入「游戏模式」，收紧响度阈值 3 LU。

## 工作原理

```
WASAPI 捕获回调（生产者）
    → LockFreeRingBuffer（无锁 SPSC 环形缓冲区）
    → 分析线程（SoundGuard.Analysis，~5ms 块）
         ├─ PeakMeter        dBFS 样本峰值
         ├─ TruePeak         8x 超采样真实峰值
         ├─ LoudnessMeter    BS.1770-4 短期/瞬时 LUFS
         ├─ SoftKneeLimiter  阶段 A：峰值软拐点前视限幅
         └─ LoudnessLimiter  阶段 B：响度限幅
    → ResponsePolicy（纯函数策略，取 max(峰值GR, 响度GR)）
    → 保护动作：
         ├─ 一般超标 → 调低系统主音量（10Hz 节流定时器执行）
         └─ 极端危险 → 系统静音 + 托盘通知
```

**关键设计决策**：

1. **共享模式下的保护方式**：Loopback 捕获的是“已经混好、即将送到扬声器”的音频，无法在捕获点
   改写它。因此 MVP 的“增益衰减”是通过**降低系统主音量**实现的（把计算出的 Gain Reduction dB 从
   基线音量中减去），而“极端危险”则直接静音。真正“零延迟、在信号路径内”的增益处理需要 APO 或
   虚拟声卡，见 `docs/roadmap.md` 里程碑 3/4。

2. **音质**：处理链路只做 `sample *= gain` 这一种线性运算。软拐点限幅器是**纯增益**限幅器，
   不引入波形整形的谐波失真。不做重采样 / 位深转换 / 通道混音 / EQ / 降噪 / 空间处理。

3. **延迟**：检测窗口约 5ms，前视约 1.5ms，加上共享模式引擎周期，总延迟 < 30ms。精确到
   <5ms 需要内核级（APO），见里程碑 4。

## 主要参数

| 参数 | 默认值 | 说明 |
|---|---|---|
| 响度阈值 `LufsThreshold` | -18.0 LUFS | 阶段 B 触发点，UI 可调 -26…-14 |
| 峰值阈值 `PeakThresholdDb` | -1.0 dBFS | 阶段 A 触发点，UI 可调 -6…0 |
| 压限幅度 `LimiterStrength` | 1.0（UI 50%） | 0–2.0；最终衰减 = 计算 GR × 强度 × 2 |
| 最大衰减 `MaxAttenuationDb` | 80 dB | 最终衰减上限 |
| 危险静音阈值 | -6 LUFS 持续 100ms | 直接静音 |
| 自动恢复 | 10 s，可关闭 | 信号安全后自动恢复音量 |
| 游戏模式偏移 | -3 LU | 全屏应用时自动收紧响度阈值 |

> 说明：计算出的 Gain Reduction 会统一乘以 2 倍补偿后再应用，因此 6 dB 的计算值实际衰减 12 dB；
> 这是 SoundGuard 的 dB 基准与系统音量衰减之间的校准系数。

## 项目结构

```
SoundGuard.sln
Directory.Build.props            # 全局编译设置
global.json                      # 固定 .NET 8 SDK
src/
  SoundGuard.Core/               # 平台无关的音频 + DSP + 引擎（无 UI 依赖）
    Audio/                       # 捕获源接口、WASAPI 实现、无锁环形缓冲区
    Dsp/                         # dB、K 加权、峰值/真实峰值、LUFS、双限幅器
    Engine/                      # 保护引擎、响应策略、配置、状态
    System/                      # 主音量/静音、独占检测、全屏检测、前台进程
    Config/                      # 配置加载/保存
    Logging/                     # 事件日志
  SoundGuard.App/                # WPF UI
    Controls/                    # 垂直表头控件
    ViewModels/                  # 主视图模型、命令
    Services/                    # 应用编排、托盘图标、图标生成、开机自启
    Converters/
  SoundGuard.Tests/              # xunit 单元测试
docs/
  architecture.md                # 架构、类图、线程/数据流
  dsp.md                         # 全部 DSP 数学推导
  roadmap.md                     # 里程碑映射与后续计划
  publishing.md                  # 如何发布到 GitHub
.github/workflows/build.yml      # CI：自动构建 + 测试
```

## 已知限制

- **独占模式**（游戏/专业软件独占 WASAPI）下，共享模式 Loopback 无法捕获该独占流。当前实现提供
  会话级启发式检测与“降级为控制系统主音量”的回退，完整处理见里程碑 3。
- **浮点格式假设**：Loopback 假设设备混合格式为 float32；个别设备若上报 16-bit 混合格式会抛出
  明确异常（按设计不转换位深）。

## 文档

- [架构设计](docs/architecture.md)
- [DSP 数学说明](docs/dsp.md)
- [路线图](docs/roadmap.md)
- [发布指南](docs/publishing.md)

## 许可证

本项目采用 [MIT License](LICENSE)。第三方依赖 NAudio 同样采用 MIT 许可证。

## 贡献

欢迎提交 Issue 与 Pull Request。建议先阅读 [架构设计](docs/architecture.md) 与
[路线图](docs/roadmap.md)，了解现有设计决策与后续计划。
