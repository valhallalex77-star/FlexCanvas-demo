# S1 实现与验收检查点

日期：2026-10-16。最新用户确认：“s1全部测试验证通过，退出状态开始s2”。

最新退出结论：S1 全部验收按用户确认接受通过，T0103/T0401/T1201、A0103/A0401/A1201 与 S1 退出同步通过。未附逐场景日志，不将助手未亲测项目编造为亲测；下方未验收/禁止 S2 表述保留为 S1 初次交付历史，不再作为当前阻塞。S2 已获授权，仍不扩展至画布/工作区。

## 目标与状态

单一目标：正式 App 的只读窗口发现 -> 精确选择 -> 风险报告 -> 本地诊断链。任务 T0103/T0301/T0302/T0401/T1201，依据 [ADR 0002](ADR/0002-s1-discovery.md)。

S0 退出按自动化 + 用户人工确认接受通过，未附逐场景配置/日志，不篡改原有助手未亲测记录。S1 代码已实现，T0301/T0302 和 A0301/A0302 通过；T0103/T0401/T1201 待完整验收。**S1 退出尚未通过，不进入 S2。**

禁止事项：不生产嵌入、不移动/关闭普通外部应用、不做画布/工作区、不提权或修改系统设置、不改初稿、不初始化 Git/提交。

## 实际修改

| 文件 | 目的 |
|---|---|
| Directory.Packages.props；src/FlexCanvas.App/FlexCanvas.App.csproj；src/FlexCanvas.Application/FlexCanvas.Application.csproj | DI 8.0.1、Toolkit 8.4.0 中央版本与必要引用 |
| src/FlexCanvas.Domain/WindowDiscoveryModels.cs | 运行期身份、窗口信息、OperationResult<T>、风险报告、固定诊断字段 |
| src/FlexCanvas.Application/WindowDiscoveryServices.cs | 发现/日志端口、重新读取目标的兼容服务与保守初筛策略 |
| src/FlexCanvas.Application/WindowManagerViewModel.cs | 异步刷新、搜索、准确选择、迟到结果隔离、错误提示、取消关闭 |
| src/FlexCanvas.Infrastructure/Win32/NativeMethods.cs | 标题/类/DPI/DWM 读取声明；集中声明测试所需的自建窗口标题/显示 API |
| src/FlexCanvas.Infrastructure/Win32/Win32WindowDiscoveryService.cs | 通用只读顶层枚举、过滤、权限 unknown、原生错误立即捕获、前后 HWND/PID 复核 |
| src/FlexCanvas.Infrastructure/RollingDiagnosticSink.cs | 有界后台 JSONL、UTC 日期/大小滚动、7 天/8 文件/1 MiB、失败健康状态 |
| src/FlexCanvas.App/App.xaml、App.xaml.cs、MainWindow.xaml、MainWindow.xaml.cs | 单一 DI 启动、列表/风险 UI、选中目标定时检查、全局异常入口与异步关闭 |
| tests/FlexCanvas.Application.Tests/WindowDiscoveryTests.cs | 15 项服务/策略/ViewModel 用例，涵盖 unknown/权限/DPI/重复标题/过期结果/异常脱敏 |
| tests/FlexCanvas.Infrastructure.Tests/RollingDiagnosticSinkTests.cs | 3 项日志滚动/字段/失败/关闭后写入测试 |
| tests/FlexCanvas.Infrastructure.Tests/WindowDiscoveryIntegrationTests.cs | 1 项真实双 Probe 的通用发现/过滤/矩形/身份/取消/退出验证 |
| tests/Invoke-S1UiSmoke.ps1 | 可重复 WPF UIAutomation 交互与正常关闭 Smoke |
| docs/ADR/0002-s1-discovery.md、docs/COMPATIBILITY_MATRIX.md、本报告 | 决策、支持边界、验证与交接 |
| PROJECT_CONTEXT.md、TASK_PLAN.md、TASKS.md、ACCEPTANCE.md、CHANGELOG_DEV.md、README.md、docs/S0_POC_REPORT.md | 同步 S0 用户确认、S1 实现/验收证据与未完成项 |

未重写 S0 引擎/Probe/UI；保留原有失败 TRX、用户 Bluepoint 目录、初稿、项目规则和 ENVIRONMENT_REPORT.md。bin/obj 为构建生成，artifacts/TestResults 新增真实 TRX；运行日志在本地日志目录，不上传。

## 实际验证

环境：Windows 11 10.0.26200 x64、SDK 8.0.426、.NET 8.0.32，保持现有官方 NuGet 源。

| 实际命令 / 活动 | 真实结果 |
|---|---|
| `git status --short`；重读状态、说明书/源码/测试 | 非 Git 仓库；不初始化、不提交，不把文件哈希当备份 |
| `dotnet restore FlexCanvas.sln` | 11 工程成功，必要 DI/MVVM 依赖从现有源还原，无系统安装或源配置修改 |
| 首轮 `dotnet build FlexCanvas.sln -c Release --no-restore` | 正式实现构建成功，0 警告/错误 |
| 加入测试后的构建 | xUnit2031 两处 Assert.Single 断言格式错误；修正为带 predicate 的重载，不关闭检查 |
| 该失败构建后曾运行 `--no-build` 测试 | 42 项通过但 Infrastructure 是旧产物，不计入完整 S1 验证；s1-check TRX 保留 |
| 修正后的成功构建 + 全量测试 | 46/46 通过；s1-verified TRX 保留 |
| 首次 `powershell -NoProfile -File tests/Invoke-S1UiSmoke.ps1` | PowerShell 单元素集合展开导致 Item 方法错误；App/Probe 正常关闭，修正数组索引后通过 |
| 收尾原生失败分类修改后的构建 | CS1503：lambda 的 `_` 参数遮蔽 discard；改为具名 parameter，未降低检查；失败时不执行测试 |
| 最终 `dotnet build FlexCanvas.sln -c Release --no-restore` | 成功，0 警告、0 错误 |
| 最终 `dotnet test FlexCanvas.sln -c Release --no-build --no-restore --logger "trx;LogFilePrefix=s1-final" --results-directory artifacts/TestResults` | Domain 9 + Application 20 + Infrastructure 17 = **46 通过，0 失败，0 跳过**，包括全部 S0 回归 |
| 最终 `powershell -NoProfile -File tests/Invoke-S1UiSmoke.ps1` | 可见启动、按本次 Probe PID 搜索、准确选择、Medium 风险报告、无结果提示、选中目标退出清除、App 正常关闭通过，退出码 0 |
| 真实 app-*.jsonl 字段/事件核对 | Startup/Discovery/Selection/Shutdown 闭环；只有固定事件/枚举/关联 ID/API/数值字段，没有标题、路径、任意 Message 或键入文本 |
| 原稿/规则/ENV 报告 SHA256、测试进程检查 | 三者与原基线一致；本次验证后无 App/三类 Probe 残留；无强杀 |
| PowerShell 文档链接/任务行/保护哈希断言 | PASS；38 项任务：6 完成、3 待验收、22 未开始、7 待确认；本地文档链接有效 |

最终证据：

- `artifacts/TestResults/s1-final_net8.0_20261016150618.trx`：Domain 9。
- `artifacts/TestResults/s1-final_net8.0_20261016150619.trx`：Application 20。
- `artifacts/TestResults/s1-final_net8.0_20261016150621.trx`：Infrastructure 17。

构建与测试有成功条件保护，不累计早期重复执行数量。UI Smoke 是真实 UIAutomation，不冒充人工环境/权限测试。

## 未完成与风险

- A0103：初始化服务异常的进程级故障注入尚未执行；正常启动/退出、DI 构建和日志目录失败单元测试已通过。
- A0401：unknown/不同权限/DPI 的策略测试通过；**实际 elevated Probe 与混合 DPI 环境未测**，不自动提权或改显示设置。
- A1201：命令异常与脱敏/滚动测试通过；Dispatcher/AppDomain/未观察任务异常入口已实现但未做进程级注入。CLR 终止异常不保证 UI 提示或后台队列落盘，需验证/后续加固。
- HWND/PID 二次检查无法彻底排除同 PID 的 HWND 复用与 TOCTOU；只有选中目标 2 秒复核，无完整 WinEventHook。未选中的退出项由显式刷新移除，选择后初筛复核，不提供生产承载操作。
- 相同 elevation 不是完整的完整性级别/UIPI 证明；PerMonitorV2 也不代表第三方输入/airspace/GPU 兼容。矩阵见 [COMPATIBILITY_MATRIX.md](COMPATIBILITY_MATRIX.md)。
- 日志失败后健康状态保持告警；队列满会丢记录并告警，正常退出等待已接受队列。无日志导出/清理 UI 或完整设置；性能长测、发布、IDE 调试仍未开展。.NET 8 支持周期风险保持记录。

## 下一步与恢复

1. 重新读取状态文件、ADR 0002、本报告和源码；核对 Git/实际文件，不重复要求已经确认的 S0 人工验收。
2. 运行 README 的正式 App，复核不同应用窗口搜索/同标题选择/退出和风险报告。不得从 CanAttempt 推断承载已验证。
3. 在授权测试环境验证 elevated 自制 Probe 与不同 DPI；只读检查，不嵌入、不绕过安全。记录应用/Windows/权限/DPI/实际提示。
4. 对本次自建 App 做启动、Dispatcher 与进程异常故障注入，确认诊断/提示/退出/资源释放；不对普通用户进程注入，不以吞异常作为通过条件。
5. 补齐 A0103/A0401/A1201 后同步验收并评审 S1 退出；S2 须另外授权。

回退：正常关闭 App，停止只读服务；外部窗口未修改，无需解除嵌入。S0 POC 若另行运行，按原 Detach 流程恢复。无 Git 仓库、提交授权、恢复点或备份；不自动删除代码/日志/用户数据，是否建立版本化恢复点由用户决定。
