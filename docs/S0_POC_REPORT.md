# S0 最小骨架与 POC 实测、交接

日期：2026-10-16。本报告主体保留 S0 交付时的原始验证证据；当前已获用户人工验收并进入 S1。决策依据：[ADR 0001](ADR/0001-s0-prototype.md)、[任务](../TASKS.md)、[验收](../ACCEPTANCE.md)。

## 最新验收确认

用户明确反馈“全部验证，s0验收测试完成。开始s1”。据此 T0201/T0202、A0201/A0202 与 S0 退出接受通过；来源是用户人工确认，未提供逐场景环境与附加日志，不编造详细矩阵。下方“待人工/未测试/不开始 S1”是先前交付时状态，不再作为当前阻塞；助手 27 项自动化和未亲测范围保持原记录。第三方应用兼容性不作外推。当前交接见 [S1 报告](S1_REPORT.md)。

## 1. S0 初次交付结论（历史）

- T0101/T0102：已完成；A0101/A0102 的范围核对、实际工程引用/基线构建与基础测试通过。
- T0201：代码与受控原生自动化验证已交付，待人工交互验收。A0201 保留“未测试”（部分场景已通过，不代表整组通过）。
- T0202：完整 POC-1至4 人工矩阵活动未开始；已有原生预验证证据可复用。A0202 与 S0 退出仍为“未测试”。
- 已建立 11 项工程：四层 App/Application/Domain/Infrastructure、三个测试项目、独立 Host 和三个 Probe。
- 最终 Release 构建 0 警告、0 错误；实际发现并执行 27 项测试，27 通过、0 失败、0 跳过。它们不是完整产品验收。

## 2. 环境与真实命令

Windows 11 专业版 10.0.26200 x64，SDK 8.0.426，.NET Desktop 8.0.32，VSTest 17.11.1 x64。原生测试均为自制 Probe、同 elevation、PerMonitorV2；本次实测 DPI 120（125%），不是所有 DPI/混合显示器通过。

| 已执行命令 / 操作 | 实际结果 |
|---|---|
| `dotnet new sln --name FlexCanvas`、`dotnet sln FlexCanvas.sln add <11个实际项目路径>` | 成功；`dotnet sln FlexCanvas.sln list` 列出 11 项 |
| `dotnet restore FlexCanvas.sln` | 成功，从既有官方 NuGet 源还原所选测试依赖，无网络安装失败/镜像切换 |
| 初次 `dotnet build FlexCanvas.sln -c Release --no-restore` | 失败：WFAC010（WinForms DPI Manifest 冲突）及 4 个 CS0103（缺少 System.IO）；未绕过检查 |
| 修复后 build + `dotnet test ... --filter "Category!=NativeIntegration"` | 构建成功，22 项测试通过 |
| 首次 `dotnet test tests/FlexCanvas.Infrastructure.Tests/FlexCanvas.Infrastructure.Tests.csproj ... --filter "Category=NativeIntegration"` | 4 失败：DPI 虚拟化与把宿主外框当客户区的测试/实现上下文问题；目标恢复和正常关闭执行，无强杀 |
| 修复后 `dotnet build FlexCanvas.sln -c Release --no-restore` + 全量 `dotnet test ...` | 26 项全部通过；之后补充真实回滚/退出和异常恢复测试 |
| 最终 `dotnet build FlexCanvas.sln -c Release --no-restore` | 成功，0 警告、0 错误；本次耗时 1.57 秒，仅是构建时间，不是产品启动指标 |
| 最终 `dotnet test FlexCanvas.sln -c Release --no-build --no-restore --logger "trx;LogFilePrefix=s0-final" --results-directory artifacts/TestResults` | Domain 9、Application 5、Infrastructure 13；合计 27 全通过 |
| PowerShell UIAutomation + 实际启动基础 App/Host | 基础 App 可见，Host 标题与 Embed 入口呈现；两个本次启动窗口正常关闭，Host 退出码 0；未操作嵌入按钮或真实键盘 |

UI 首次探测在 WaitForInputIdle 后立即读 HWND 得到 0，不是应用崩溃；核查其稍后可见窗口并正常关闭。后续 Host 用有界窗口出现检查验证，不把 idle 当加载完成。

修复说明：

- WinForms 使用独立 asInvoker Manifest，不含 DPI 声明；程序通过 SetHighDpiMode 配置 PerMonitorV2，未禁用 WFAC010。
- 专用 Dispatcher 在每个原生操作中明确切换 PerMonitorV2，并在结束时恢复前一个线程 DPI 上下文；不修改系统设置。
- 原生测试以 GetClientRect + ClientToScreen 为客户区基准，严格比对目标矩形；非法尺寸测试严格检查调用前后矩形不变，没有降低误差断言。

## 3. 已验证与未测试矩阵

| 场景 | 实际范围 | 状态 / 结果 |
|---|---|---|
| 状态机、结果与架构 | 合法/非法/失败重试/终态；四层引用和 Domain/Application 无 WPF 引用 | 测试通过 |
| fake 原生端口 | 失败回滚、回滚失败重试、异常后回滚、失效身份、幂等解除、尺寸同步 | 测试通过；不是应用兼容证明 |
| 串行线程 | 同一 STA 上 16 个并发请求串行处理、开始前取消不执行 | 测试通过 |
| 三类真实 Probe | Win32/WPF/WinForms 各 3 次嵌入/宿主移动/尺寸/原始父窗口/样式/扩展样式/矩形恢复 | 测试通过；精确像素比对、解除后进程仍存活 |
| 三类真实失败回滚 | 空客户区导致嵌入后 INVALID_ARGUMENT，原始窗口完整恢复 | 测试通过；不是 fake |
| 嵌入中正常目标退出 | 每类 Probe 发送正常关闭消息，状态收敛 TargetExited、重复解除成功 | 测试通过；不是目标崩溃测试 |
| 无效与自身 HWND、非法尺寸 | 无效/同进程窗口被拒绝，非法尺寸不改变原始矩形 | 测试通过 |
| WPF UI 启动/关闭 | 实际 App 空壳和 Host/HwndHost 入口呈现；无目标正常退出 | 测试通过；不代表承载状态关闭故障通过 |
| 键盘/IME/焦点/菜单/弹窗/Alt+Tab | POC-2；WPF Host 中真实输入完整性 | 未测试，需独立人工验收 |
| 100/150/200%、跨屏混合 DPI、分辨率/睡眠变化 | POC-3；125% 以外与混合显示器 | 未测试 |
| 权限差异、目标/Host 真正崩溃、解除失败 UI 重试 | POC-4；不自动提权或强杀，须受控方案 | 未测试 |
| 记事本/资源管理器/Chromium/Electron/Qt/UWP/WinUI等 | 初稿§11.3 扩展应用 | 未测试；POC 当前仅允许自制 Probe |
| 性能/长循环资源、保存发布与 IDE 调试 | 对应后续阶段 | 未测试；不从 3 次循环推导资源稳定或 60 FPS |

最终 TRX：

- `artifacts/TestResults/s0-final_net8.0_20261016141120.trx`（Application 5）
- `artifacts/TestResults/s0-final_net8.0_20261016141121.trx`（Domain 9）
- `artifacts/TestResults/s0-final_net8.0_20261016141122.trx`（Infrastructure 13）

早期失败 TRX 和所有本次生成的结果保留在同目录；不要累计不同轮次重复结果当测试数量。具体时间戳与 PID 只是本次证据，不写入稳定匹配逻辑。

## 4. 实际修改文件与用途

全部代码/工程为此次新增；既有状态文档按当前阶段更新，不覆盖原初稿、ENVIRONMENT_REPORT.md、项目/全局规则或 Bluepoint 用户目录。

| 实际文件 | 用途 |
|---|---|
| `FlexCanvas.sln`、`global.json`、`Directory.Build.props`、`Directory.Build.targets`、`Directory.Packages.props`、`.gitignore`、`app.manifest`、`app-winforms.manifest` | 标准 Solution、SDK/nullable/x64/警告与包版本、忽略目录、权限/DPI 配置 |
| `src/FlexCanvas.App/{FlexCanvas.App.csproj,App.xaml,App.xaml.cs,MainWindow.xaml,MainWindow.xaml.cs}` | 不伪装业务完成的基础 WPF 空壳 |
| `src/FlexCanvas.Domain/{FlexCanvas.Domain.csproj,Poc/ProbeModels.cs}` | POC 结果/快照/状态机，纯 .NET 8 |
| `src/FlexCanvas.Application/{FlexCanvas.Application.csproj,Poc/IProbeWindowApi.cs}` | 原生端口与层级边界，无 WPF/实现依赖 |
| `src/FlexCanvas.Infrastructure/FlexCanvas.Infrastructure.csproj` 及 `Win32/{NativeMethods,Win32ProbeWindowApi,ProbeEmbeddingEngine,WindowHostDispatcher,ProbeHostController,NativeProbeSurface,NativeProbeRunner}.cs` | 原生封装、受控探针筛查、快照/嵌入/回滚、串行线程与互操作控件 |
| `tools/WindowHostProbe/{WindowHostProbe.csproj,App.xaml,App.xaml.cs,ProbeWindow.cs}` | 独立 Host 操作界面、尺寸/移动、日志与关闭重试 |
| `tools/Probes/WpfProbe/{WpfProbe.csproj,App.xaml,App.xaml.cs}` | WPF 输入/菜单/下拉/弹窗测试目标 |
| `tools/Probes/WinFormsProbe/{WinFormsProbe.csproj,Program.cs}`、`tools/Probes/Win32Probe/{Win32Probe.csproj,Program.cs}` | WinForms 与真正原生 HWND 的独立目标 |
| 三个 `tests/*/*.csproj`、`EmbeddingStateMachineTests.cs`、`ArchitectureTests.cs`、`ProbeEmbeddingEngineTests.cs`、`WindowHostDispatcherTests.cs`、`NativeProbeTests.cs` | 实际基础、异常与原生验证；不是空模板测试 |
| `README.md`、`docs/ADR/0001-s0-prototype.md`、本报告 | 运行入口、S0 决策与交接证据 |
| `PROJECT_CONTEXT.md`、`TASK_PLAN.md`、`CHANGELOG_DEV.md`、`TASKS.md`、`ACCEPTANCE.md` | 真实状态、任务进度、命令/失败与验收限制 |

bin/obj 与 artifacts 是实际构建/测试生成文件，`.gitignore` 忽略，不假称不存在。未初始化 Git、未提交；文件清单/哈希不是备份，不自动回滚。

最终文件级断言通过：11 个项目均登记在 Solution，38 对任务/验收 ID 唯一且对应，仅 T0101/T0102 标为完成；最终 TRX 实际计数为 27 全通过。Markdown 本地链接有效，原初稿哈希/大小/时间/只读属性、项目/全局规则、环境报告与全局 NuGet.Config 未变，Bluepoint 原空目录保留；检查时本次 App/Host/Probe 均已正常关闭。

## 5. 风险与下一步交接

1. 原生 SetParent/样式修改只验证了三个受控目标；仍有 HWND airspace、焦点/IME、DPI awareness重置、弹窗与异常恢复风险。UI 原生操作异常保留明确结果；失败恢复不是任意程序可恢复承诺。
2. 同 PID 内的 HWND 复用与窗口操作间 TOCTOU 不能由简单 IsWindow/PID 复核彻底解决。POC 不含完整 WinEventHook；留待真实场景证据与 T0601 设计，不把基础轮询当完整事件服务。
3. `.NET 8` EOL 2026-11-10 与 IDE C# 调试未配置仍存在；不擅自升级技术栈或安装工具。
4. 下一任务先核实本报告、TASKS/ACCEPTANCE、原生实现与实际 Git，按 README 运行 Host，逐项填写 POC-2/3/4。必要时先确认多屏/权限测试环境，不自动执行提权/强杀。
5. A0201 完整人工交互通过后再完成 T0201；复用自动化证据开展 T0202，并评审 S0 退出。未通过前不启动 T0103、完整画布或工作区。
6. 如需恢复点先由用户授权 Git 初始化/提交或其他备份；目前没有恢复快照。原稿/规则/全局 NuGet 配置的保护基线已核对，日志不含秘密。
