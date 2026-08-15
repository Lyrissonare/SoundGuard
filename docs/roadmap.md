# SoundGuard 路线图

## 里程碑 1 — MVP（本仓库已实现）

- [x] WASAPI Loopback 捕获（共享模式，float32，无重采样/转位深/混音）
- [x] 实时 dBFS 峰值检测（样本峰值 + 8x 真实峰值）
- [x] 系统静音（Core Audio）+ 一键恢复 + 10 秒自动恢复
- [x] 基础 WPF UI + 托盘 + 开机自启

## 里程碑 2 — 双阶段引擎（本仓库已实现）

- [x] BS.1770-4 LUFS（Momentary + Short-term）
- [x] 阶段 A 软拐点前视限幅器（8x 超采样）
- [x] 阶段 B 响度限幅器（attack 150ms / release 2s）
- [x] 分级响应（轻微/严重/极端）与主音量衰减代理
- [x] 直通模式、配置持久化、事件日志、单元测试

### 待完善（里程碑 2 收尾）

- [ ] 峰值限幅器改为**逐样本滑动真实峰值包络**（当前为块级目标 + 逐样本平滑），进一步把有效
      attack 压到 &lt;1ms。
- [ ] 白名单进程实际生效（当前 `AppConfig.Whitelist` 已建模，引擎端按前台进程跳过保护即可接线）。
- [ ] 限幅器输出写入监听副本，供“处理后波形”预览（当前只暴露 GR）。

## 里程碑 3 — 独占模式 / 虚拟设备 / 游戏模式

- [ ] 独占模式可靠检测：会话活动 + Loopback 静音 → 判定 `ExclusiveLikely`
      （`ExclusiveModeDetector` 已提供会话级半成品）。
- [ ] 独占时自动降级：控制系统主音量（已具备 `MasterVolumeController`）。
- [ ] 虚拟设备模式：提示并引导安装 VB-Cable / 自研虚拟声卡，捕获虚拟设备输入 → 处理 → 输出。
- [ ] 游戏模式自动切换（`FullscreenDetector` 已实现全屏检测，待接入阈值收紧策略的调优）。

## 里程碑 4 — APO 驱动 / 内核级处理（长期）

- [ ] 研究 Windows Audio Processing Object（APO）或虚拟音频驱动，把 `sample *= gain` 放入系统
      音频渲染路径，实现 **&lt;5ms 延迟、真·无损** 处理。
- [ ] 用 C++ 重写 DSP 热路径（当前 C# 已足够 MVP；APO 必须原生）。
- [ ] 保持 `ICaptureSource` / `ISystemAudioController` 抽象，使内核后端可无痛替换。

## 工程改进

- [ ] CI（GitHub Actions）在 .NET 8 SDK 上跑 `dotnet test`。
- [ ] 响度计与 libebur128 交叉验证（对拍标准测试信号）。
- [ ] 多采样率（44.1/48/96/192kHz）与多通道（2.0/5.1/7.1）测试矩阵——代码按格式自适应，需补测试。
