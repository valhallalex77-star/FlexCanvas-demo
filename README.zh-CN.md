\# FlexCanvas



\[English](README.md) | \*\*简体中文\*\*



\*\*FlexCanvas\*\* 是一款面向 Windows 的空间窗口容器与工作区管理系统。它将外部应用程序的真实窗口嵌入统一画布，保留原生交互能力，并允许用户自由排列、吸附和切换窗口。整个过程不会使用截图、DWM 缩略图或视频流伪造窗口内容。



> \*\*项目状态：\*\* S0–S3 阶段已实现并通过验证，涵盖可行性 POC、窗口发现、生产级嵌入与恢复、空间画布。工作区持久化（S4）和发布加固（S5）尚未实现。



\## 核心特性



\- \*\*真实 HWND 嵌入\*\* — 通过 Win32 `SetParent` 将跨进程窗口重新设置到 FlexCanvas 宿主表面中，并完整保存原始父窗口、窗口样式、位置状态、DPI 和进程身份，以便准确恢复。

\- \*\*安全恢复机制\*\* — 每次嵌入都会保存回滚快照；分离操作具备幂等性；发生失败时保留宿主以供重试；关闭 FlexCanvas 不会终止被托管的应用程序。

\- \*\*双模式空间画布\*\*（4096 × 4096 DIP，允许窗口重叠）：

&#x20; - \*\*几何概览模式\*\* — 以类似 Windows“显示设置”的矩形展示每个会话的实际名称、逻辑尺寸、原始物理尺寸和 DPI；支持拖动及吸附辅助线。

&#x20; - \*\*原生 1:1 模式\*\* — 在可视视口中以原始尺寸显示真实窗口，并通过原生方式裁剪。视图缩放和平移只改变观察效果，不会调整被托管窗口的实际尺寸。

\- \*\*吸附引擎\*\* — 支持边缘对齐、无缝相邻和等间距吸附；阈值严格为 12 DIP，采用确定性的冲突决策规则，多次重复操作不会产生漂移。

\- \*\*明确的缩放语义\*\* — 只有用户显式操作才会改变窗口的真实尺寸；外部尺寸变化会同步回共享布局，并通过循环防护避免反馈循环。

\- \*\*非全屏聚焦\*\* — 选中容器时，只会提升其宿主窗口的 Z 顺序，不会最大化窗口、移动其他窗口或重建会话。



\## 兼容性限制（重要）



FlexCanvas 只嵌入满足严格条件的窗口：



\- 目标必须是顶层窗口，并使用与宿主匹配的 \*\*Per-Monitor V2 DPI 感知模式\*\*。

\- 目标应用必须与 FlexCanvas 运行在\*\*相同的权限/完整性级别\*\*。



不满足条件的窗口（例如多数旧式 Win32 应用、DPI 感知能力不足的应用或以提升权限运行的窗口）会被拒绝，并返回 `DPI\_MISMATCH` 或 `ACCESS\_DENIED` 等可解释的错误代码，而不是被错误嵌入。FlexCanvas 不会注入代码、绕过 UIPI、提升自身权限或终止外部进程。



详见 \[兼容性矩阵](docs/COMPATIBILITY\_MATRIX.md)。



\## 架构



```text

FlexCanvas.App              WPF 入口、视图、双模式画布、会话 UI

FlexCanvas.Application      视图模型、嵌入引擎编排、端口接口

FlexCanvas.Domain           纯布局、吸附、几何投影、状态机

FlexCanvas.Infrastructure   Win32 P/Invoke、嵌入 API、窗口发现、诊断

tools/Probes                独立的 Win32 / WPF / WinForms 测试目标

tools/WindowHostProbe       独立的 POC 宿主程序

```



\- 依赖方向：`App → Application → Domain`；`Infrastructure → Application + Domain`。Domain 层不依赖 WPF/Win32。

\- 所有原生窗口变更都通过专用 STA 调度器串行执行；WinEvent Hook 按成对注册和释放。



\## 环境要求



\- Windows 10/11 x64

\- \[.NET SDK 8.0.426](https://dotnet.microsoft.com/download/dotnet/8.0)（由 `global.json` 固定版本）

\- 交互式桌面会话（测试会启动真实窗口）



\## 构建与测试



```powershell

dotnet restore FlexCanvas.sln

dotnet build FlexCanvas.sln -c Release

dotnet test FlexCanvas.sln -c Release --filter "Category!=NativeIntegration"

```



完整测试套件（编写本文档时共 131 项测试）还包含原生集成测试：启动随项目提供的 Probe 可执行程序，嵌入、调整大小、恢复真实窗口，并确保相关进程能够正常关闭。



```powershell

dotnet test FlexCanvas.sln -c Release

```



\## 运行



```powershell

dotnet run --project src\\FlexCanvas.App\\FlexCanvas.App.csproj -c Release --no-build

```



1\. 在 \*\*Window Discovery\*\*（窗口发现）选项卡中刷新并选择目标窗口。建议先使用项目自带的 Probe 程序作为已知兼容目标。

2\. 点击 \*\*Check Selected Window\*\*，查看只读兼容性检查报告。

3\. 选择 \*\*Embed Selected Window\*\*，将窗口嵌入独立宿主窗口；或选择 \*\*Embed to Canvas (1:1)\*\*，以 1:1 尺寸嵌入画布。

4\. 在 \*\*Spatial Canvas\*\*（空间画布）选项卡中切换几何概览模式和原生 1:1 模式：在概览模式中排列窗口，在原生模式中进行交互。

5\. 正常关闭程序，被托管的窗口会恢复到原始状态。



可重复执行的 UI 冒烟测试脚本位于 `tests/Invoke-S1UiSmoke.ps1`、`tests/Invoke-S2UiSmoke.ps1` 和 `tests/Invoke-S3CanvasUiSmoke.ps1`。



\## 项目文档



\- \[开发说明书](FlexCanvas\_Project\_Development\_Specification.md) — 完整的产品与工程说明

\- \[任务清单](TASKS.md) / \[验收标准](ACCEPTANCE.md)

\- \[开发变更日志](CHANGELOG\_DEV.md) / \[项目上下文](PROJECT\_CONTEXT.md) / \[阶段计划](TASK\_PLAN.md)

\- 阶段报告：\[S0](docs/S0\_POC\_REPORT.md) · \[S1](docs/S1\_REPORT.md) · \[S2](docs/S2\_REPORT.md) · \[S3](docs/S3\_REPORT.md)

\- 架构决策记录：\[ADR 0001](docs/ADR/0001-s0-prototype.md) · \[0002](docs/ADR/0002-s1-discovery.md) · \[0003](docs/ADR/0003-s2-embedding.md) · \[0004](docs/ADR/0004-s3-canvas.md)

\- \[兼容性矩阵](docs/COMPATIBILITY\_MATRIX.md)



\## 开发路线图



\- \*\*S4 — 工作区\*\*：版本化 JSON 持久化、原子写入、部分恢复。

\- \*\*S5 — 稳定性加固\*\*：扩展应用/DPI/权限兼容性矩阵、诊断信息导出、发布构建。



\## 许可证



本项目基于 \[MIT License](LICENSE) 发布。

