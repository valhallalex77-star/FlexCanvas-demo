# FlexCanvas 开发变更日志

初始化阶段只建立空白模板，未补造历史。下方从实际环境检查开始记录，环境阶段无业务代码；当前已追加真实 S0 骨架/POC代码与验证记录。管理初始化证据见 PROJECT_CONTEXT.md、TASK_PLAN.md。

后续每个重要开发阶段完成后，复制下方模板并填写真实证据，保留已有记录。未执行的测试明确写“未执行”并说明原因，不使用计划命令冒充执行结果，不记录密钥或用户敏感数据。

## 2026-10-16：E1 开发环境差距检查

- 目标：基于已明确的 C# / .NET 8 / WPF 基线，核查必要工具并保存差距报告，尚不安装或开发业务。
- 状态：检查与报告完成；SDK 安装和安装后验收未完成。
- 实际修改：新增 ENVIRONMENT_REPORT.md（版本、差距、网络证据、方案影响）；更新 PROJECT_CONTEXT.md（环境状态及风险）、TASK_PLAN.md（E1-E4 任务与授权门槛）、本文件（真实记录）。
- 实际命令：`Get-Content -Raw -Encoding UTF8` 重读状态/规则/初稿；`git rev-parse --show-toplevel`、`git status --short` 返回非仓库；`Get-CimInstance Win32_OperatingSystem`、`dotnet --info/--list-sdks/--list-runtimes`、`git --version`、`winget --version`、`vswhere -all -products '*'`、工作负载查询、`MSBuild.exe -version -nologo`、路径/权限查询和 `Get-FileHash`。
- 结果：Windows 11 x64；未安装 .NET SDK；已有 .NET 8.0.11 Desktop Runtime、Git 2.53.0、WinGet 1.29.380、Trae 3.5.91、VS Build Tools 17.14.39、Windows SDK 目录；非管理员。
- 网络验证：`Invoke-WebRequest -UseBasicParsing -TimeoutSec 30` 访问 NuGet V3 索引、MVVM/xUnit 版本索引、样本 MVVM 8.4.0 包和官方 .NET 8 发布元数据，均 HTTP 200；包仅下载至内存，不是项目依赖安装。
- 未执行：安装、SDK NuGet 源列表、真实还原/WPF 构建、业务测试。原因：缺 SDK，需先确认安装范围；项目尚无工程。
- 风险：官方 .NET 8 元数据报告 EOL 2026-11-10；技术路线保持不变。系统安装可能更新共享 .NET 8 运行时；不自动提权、重启或修改全局配置。
- Git/恢复点：项目非仓库，未初始化或提交；初稿/全局规则 SHA256 与初始化基线一致。
- 下一步/交接：安装方案及验收要求见 ENVIRONMENT_REPORT.md §4-6；先获得安装范围与必要授权，再安装并复查。

## 2026-10-16：E2-E4 官方 SDK 安装与 CLI 验收

- 目标：补齐必要 .NET 8 SDK 并验证实际还原、WPF 编译与基础运行时；不开发业务。
- 状态：安装与 CLI 验收完成；IDE 语言服务/断点调试、正式项目和 HWND POC 未完成。
- 授权：用户明确选择“官方系统级安装”，已说明 UAC、共享运行时影响；未授权自动重启、卸载其他版本或修改 VS 工作负载，未执行这些操作。
- 正式项目修改：ENVIRONMENT_REPORT.md（安装、网络、命令及结果），PROJECT_CONTEXT.md（当前环境与交接），TASK_PLAN.md（E2-E4 结果与后续事项），本文件（阶段日志）。初稿和项目规则未改。
- 外部实际变更：官方 SDK 8.0.426 x64 安装至 `C:\Program Files\dotnet`，含 NETCore/WindowsDesktop/AspNetCore 8.0.32 与引用组件；旧运行时仍存在。SDK 首次运行生成此前不存在的 `%APPDATA%\NuGet\NuGet.Config`，默认官方 HTTPS 源，未覆盖既有文件。
- 下载与校验：官方 builds.dotnet.microsoft.com 安装器 223,749,224 字节；`Get-FileHash -Algorithm SHA512` 匹配官方元数据，`Get-AuthenticodeSignature` 为 Valid / Microsoft Corporation。
- 安装实际命令：`Start-Process <安装器> -Verb RunAs -ArgumentList '/install','/quiet','/norestart','/log',<临时日志> -Wait -PassThru`。退出码 0；日志 `Exit code: 0x0, restarting: No`。
- 版本实际命令：`dotnet --info`、`--list-sdks`、`--list-runtimes`、`--version`。结果 SDK 8.0.426 / win-x64 / MSBuild 17.11.69，Desktop 与 NETCore 8.0.32，默认命令可用。
- 源实际命令：`dotnet nuget list source`（默认和临时 `--configfile`），均官方源启用；再次 `Invoke-WebRequest` 索引和 MVVM 包均 HTTP 200。
- 编译实际命令：临时 `dotnet new wpf --framework net8.0 --no-restore`，`dotnet add ... package CommunityToolkit.Mvvm --version 8.4.0 --no-restore`，随后 `dotnet restore ... --configfile ... --no-cache` 和 `dotnet build ... -c Release --no-restore`。真实还原成功，WPF/XAML 编译 0 警告、0 错误。8.4.0 仅为验证样本，不锁定正式项目版本。
- 运行实际命令：临时 RuntimeEnvironmentSmoke 的 restore、Release build、`dotnet run --project ... -c Release --no-build --no-restore`。输出 `PASS: .NET 8.0.32 x64, System.Text.Json, Win32 P/Invoke, WPF STA initialization.`，检查本进程 API 与未显示的本地 WPF 对象，不接触外部窗口。
- 临时修改范围：`%TEMP%\FlexCanvas-environment-20261016` 下安装器/日志、专用 NuGet.Config、verify-environment.ps1、WPF 与 Runtime 样例、packages/cli-home。Program.cs 的运行时断言与 Runtime.csproj 的 UseWPF/net8.0-windows 均只在临时目录。
- 未执行：正式项目 build/test/publish、GUI/断点调试、HWND 兼容性测试；正式项目无工程，不伪报业务验证。
- 未解决：Trae 内置 C# 仅语法扩展，语言服务/调试仍待官方支持方案选型；.NET 8 EOL 2026-11-10，未擅自升级技术栈。
- 安全/恢复点：无网络或权限失败，未切换镜像、未禁用证书/TLS/签名、未删除配置。初稿/全局规则 SHA256 与基线一致；项目非 Git 仓库，不初始化、不提交。安装日志保留，不自动回滚 SDK。
- 下一步与交接：完整证据见 ENVIRONMENT_REPORT.md §5；下次先复核 SDK/源与真实文件，确认 IDE 调试和技术设计，另行授权后再建业务工程与 POC。

## 2026-10-16：S0 最小分层骨架与受控 HWND POC

- 本阶段目标/授权：用户选择完整 S0 POC；建立基础项目、独立受控 Host/Probe 与实际测试，不进入 S1/画布/工作区。
- 状态：T0101/T0102已完成；T0201待人工交互验收，T0202完整矩阵未开始；S0退出未通过。
- 实际新增：FlexCanvas.sln、global.json、Directory.Build.props/targets、Directory.Packages.props、.gitignore、两个Manifest、README；四层src、三个tests、WindowHostProbe与三类Probe（共11工程）；docs/ADR/0001-s0-prototype.md、docs/S0_POC_REPORT.md。逐文件清单及目的见S0报告§4。
- 实际更新：PROJECT_CONTEXT.md、TASK_PLAN.md、TASKS.md、ACCEPTANCE.md和本文件；记录工程证据、真实进度、测试与未完成验收。原初稿、项目/全局规则、ENVIRONMENT_REPORT.md、既有NuGet.Config及Bluepoint保留。
- 决策：C#/.NET8/WPF/x64不变；仅允许仓库tools下同权限/相同DPI awareness的自制Probe，专用STA消息泵逐操作切换PerMonitorV2；POC端口/结果与生产接口分开，不冻结完整产品API。
- 依赖：SDK 8.0.426固定，xUnit 2.9.3、runner 2.8.2、Test.Sdk 17.11.1通过现有官方NuGet源真实还原；未安装MVVM/日志/Mock框架或系统软件，无网络失败和镜像修改。

| 实际命令 / 步骤 | 真实结果 |
|---|---|
| `git rev-parse --show-toplevel`、`git status --short`、目录/状态/源码重读 | 非Git仓库；中断后已核实部分落盘工程再续作，未覆盖用户改动 |
| `dotnet new sln --name FlexCanvas`、`dotnet sln FlexCanvas.sln add <11个真实项目>`、`dotnet restore FlexCanvas.sln` | 成功，实际新增Solution/项目引用；官方源还原成功 |
| 首次 `dotnet build FlexCanvas.sln -c Release --no-restore` | 失败：WFAC010与4个CS0103。修正WinForms权限Manifest与SetHighDpiMode分工，补System.IO；不禁用检查 |
| 修复后Release构建及 `dotnet test ... --filter "Category!=NativeIntegration"` | 构建0警告/0错误，22项测试通过 |
| 初次 `dotnet test ... --filter "Category=NativeIntegration"` | 4项失败：DPI虚拟化/外框与客户区假设错误；原生回滚与自建目标正常关闭执行，失败TRX保留 |
| 修复DPI逐操作上下文及GetClientRect/ClientToScreen基准后全量测试 | 26项通过；没有放宽精确像素/样式/恢复断言 |
| 增加真实空客户区失败回滚、嵌入中正常目标退出及异常后回滚测试后最终build/test | Release构建0警告/0错误；27/27通过、0失败、0跳过；Domain9/Application5/Infrastructure13 |
| `dotnet test FlexCanvas.sln -c Release --no-build --no-restore --logger "trx;LogFilePrefix=s0-final" --results-directory artifacts/TestResults` | 三个最终TRX见S0报告§3；只统计此轮，不重复累计早期测试 |
| PowerShell UIAutomation、本次App/Host可见启动与正常关闭 | 基础App窗口和Host/Embed入口真实呈现，正常关闭；Host退出码0。首次idle后立即读HWND过早已核对其后可见窗口，不假报启动失败/成功 |
| Get-FileHash及用户目录/进程检查 | 初稿/规则/环境报告/全局NuGet保护基线一致；自建窗口正常关闭，未强杀用户应用 |

- 已验证原生范围：Windows11/125%DPI/same elevation，Win32/WPF/WinForms各3次真实嵌入、宿主移动/尺寸、原始父窗口/样式/扩展样式/矩形恢复；每类真实空客户区失败回滚和正常目标退出后TargetExited收敛。
- 未执行：WPF Host中的真实键盘/IME/菜单/弹窗/焦点、100/150/200%与混合显示器、权限差异、真正Host/目标崩溃、长循环资源/性能、发布与生产模块验收。不能以fake/API/可见启动替代这些测试。
- 未解决：airspace/IME/跨屏/权限/崩溃恢复兼容边界；简单HWND/PID复核不能彻底解决同进程句柄复用与TOCTOU；完整WinEventHook/生产生命周期未实现。.NET8支持周期与IDE调试方案仍待确认。
- 恢复点/Git：未初始化、提交、改远程或创建备份快照；.gitignore不是仓库，文件/哈希清单不是备份。是否建立恢复点需用户授权。
- 下一步与交接：先按README运行独立Host补齐A0201交互验收，再开展T0202完整矩阵/S0退出评审。状态文件与TASKS/ACCEPTANCE已同步；未完成门槛前停止在S0，不自动进入S1。

## 2026-10-16：S0 用户验收确认与 S1 只读发现实现

- 最新授权：用户确认“全部验证，s0验收测试完成。开始s1”。T0201/T0202、A0201/A0202 和 S0 退出按用户人工确认接受通过；未附逐场景日志，不编造助手亲测记录。上方 S0 未验收文字保留为初次交付历史。
- 单一目标：T0103/T0301/T0302/T0401/T1201 的正式启动、窗口发现/选择、风险报告与本地诊断链，不开发 S2 承载/画布/工作区。
- 实际修改：Domain 窗口/结果/诊断模型；Application 发现/兼容端口、策略和 ViewModel；Infrastructure 通用只读 Win32 发现、集中声明和后台滚动日志；App DI/主窗口/异常与关闭；中央 DI/MVVM 版本与引用；3 个测试文件和 UI Smoke 脚本；状态/任务/验收/README/S0报告、ADR0002、兼容矩阵/S1报告。逐文件目的见 docs/S1_REPORT.md。
- 真实依赖：现有官方 NuGet 源还原 DI 8.0.1、Toolkit 8.4.0，未安装系统软件、修改源/权限/技术路线。
- 实际验证：`dotnet restore FlexCanvas.sln` 成功；最终 `dotnet build FlexCanvas.sln -c Release --no-restore` 0警告/0错误；最终 `dotnet test ... --logger "trx;LogFilePrefix=s1-final" --results-directory artifacts/TestResults` Domain9/Application20/Infrastructure17，共46通过、0失败、0跳过，含S0回归。
- 真实 UI 验证：`powershell -NoProfile -File tests/Invoke-S1UiSmoke.ps1` 输出 PASS；可见启动、PID搜索、选择、风险报告、空结果、选中目标退出清除和正常关闭通过。正常关闭本次自建程序，不强杀。
- 失败与修复保留：两处xUnit2031改用predicate重载；失败构建后的42项旧/部分产物测试不计完整S1验证；PowerShell单元素集合索引修正；原生读取分类修改后的CS1503由lambda `_`遮蔽discard导致，改具名参数；最终构建/测试/UI全部重跑通过。
- 日志核对：Startup/Discovery/Selection/Shutdown真实事件闭环；无标题/搜索/路径/输入/任意异常消息字段；7天/8文件/1MiB/256队列，UTC日或大小滚动，失败告警。
- 验收状态：T0301/T0302、A0301/A0302完成；T0103/T0401/T1201已实现待完整验收。未执行实际elevated/混合DPI、启动/全局异常进程级故障注入，不将部分通过当S1退出。
- 安全/保护：Git检查仍非仓库；未初始化/提交/创建备份。初稿、规则和ENV报告SHA256与基线一致，Bluepoint/旧TRX保留；验证后无App/Probe残留。
- 下一步与交接：PROJECT_CONTEXT/TASK_PLAN/TASKS/ACCEPTANCE已同步；按docs/S1_REPORT.md的顺序补齐未测项并评审S1退出，未经新授权不进入S2。没有真实版本恢复点，是否建立须由用户决定。

## 2026-10-16：S1 用户退出确认与 S2 正式承载检查点

- 最新授权：用户确认“s1全部测试验证通过，退出状态开始s2”。S1 整组验收/退出接受用户确认；上方旧待验收文字为历史，不编造助手逐场景亲测。
- 单一目标：T0501-T0503/T0601-T0602 的正式承载、尺寸/焦点、幂等解除/回滚、生命周期事件和安全关闭；未开发 S3/S4。
- 实际文件：新增正式 Domain 模型、Application 接口/引擎、Infrastructure 原生适配/service/HwndHost、App 独立宿主、两组测试与 S2 UI Smoke；修改集中 P/Invoke/诊断词汇/DI/主窗确认与关闭/初筛文案；新增 ADR0003/S2报告，更新状态/任务/验收/README/矩阵及 S1 用户确认。完整清单与目的见 docs/S2_REPORT.md §2。
- 决策：正式服务与 POC 分离；capture 完整权限元组/进程创建时间/placement；修改前标记恢复责任；失败保留宿主/快照；专用 STA 和 256 事件队列；每会话 3 hook，正常关闭不杀目标。
- 实际构建：`dotnet build FlexCanvas.sln -c Release --no-restore` 最终 0 警告、0 错误。
- 实际测试：`dotnet test FlexCanvas.sln -c Release --no-build --no-restore --logger "trx;LogFilePrefix=s2-final" --results-directory "$env:TEMP\FlexCanvas-s2-final-TestResults"`：Domain9/Application31/Infrastructure22，共62通过、0失败/跳过；最新TRX文件名见S2报告§3。前次成功与失败TRX仍保留在artifacts/TestResults。
- 实际 UI：`powershell -NoProfile -File tests/Invoke-S1UiSmoke.ps1` 和 `Invoke-S2UiSmoke.ps1` 最终均PASS。S2取消、双宿主、原生文本消息读写、移动缩放、显式解除/主窗恢复、目标存活通过；不是键盘/IME验收。
- 实际失败/修复：CS0108属性改SurfaceHandle；5个真实原生恢复失败由SetParent(NULL)/WS_CHILD中间态误判造成，校验延后至原样式恢复，保留最终精确断言；62复测通过。
- UI排错：确认框原生Button被UIA暴露为无Pattern的Pane，限定owned PID/dialog/native Button并正常BM_CLICK；中文编码改ASCII字符码；跨进程Edit按宿主/PID/原生类定位，有限WM_SETTEXT/GETTEXT；等待控件实际渲染；诊断输出不混入函数返回。所有失败真实保留在报告，不伪造UI TRX。
- 清理：曾留下本次App PID24448/24952，已正常取消/关闭；最终自建App/Probe正常清理，无Process.Kill。
- 未执行：真实键盘/IME/鼠标/菜单/焦点、实际权限/DPI差异、真正目标/Host崩溃、原生恢复失败UI/重试及最大化placement专项；公共API文档/原生失败路径覆盖未完整。不能据此声明S2整组退出。
- 当前状态：五任务待验收；五组完整验收及S2退出未完成，已通过部分场景单独记录。S1回归保持通过，不自动进入S3。
- Git/保护：实际检查仍非仓库，未初始化/提交/备份/改远程；原稿/规则/环境报告保护基线保留，无系统安装/权限变更，旧POC/用户数据/失败证据保留。哈希不等于恢复点。
- 结束复核：Get-FileHash确认原稿/项目规则/环境报告SHA256与基线一致；Get-Process无App/三类Probe残留；PowerShell本地文档链接及38对任务/验收断言PASS，统计9完成/5待验收/17未开始/7待确认。
- 下一步/交接：按S2报告§6完成三类Probe真实交互、环境与失败路径验收，记录真实配置/关联ID；缺陷先修复复测，用户确认S2退出后才另行授权S3。PROJECT_CONTEXT/TASK_PLAN/TASKS/ACCEPTANCE与矩阵/ADR已同步。

## 2026-10-16：新对话交接复核

- 用户要求：长对话/多次压缩后切换 Trae 新对话，提供交接提示词；暂停编码，不推进阶段。
- 实际修改：PROJECT_CONTEXT.md、TASK_PLAN.md、本文件、docs/S2_REPORT.md，仅记录最新会话要求与复核，不修改业务代码。
- 实际核查：Get-Content 重读状态/计划/日志/报告/原稿；git rev-parse/status 仍非仓库；rg 核对生产源码入口；Get-FileHash 原稿 SHA256 与基线一致；读取现存最终 TRX 为9/31/22通过、0失败/跳过。
- 当前运行状态：Get-Process 发现 App PID25972，未检查承载会话、未操作或关闭；不得把旧测试后清理结论当当前无进程。无构建/测试/UI重跑、无提交/备份。
- 下一步：新对话按S2报告§8先恢复真实上下文，给出下一项验收步骤并等待用户反馈/授权；S2仍待完整验收，不进入S3。

## 2026-10-19：S2 用户验收退出与 S3 启动设计确认

- 最新要求：“s2验证完毕可以退出，现在开始任务s3”。接受 S2 五任务/五验收组及阶段退出，来源为用户确认；未附逐场景日志，不扩展助手历史 62 项/UI Smoke 的实测范围。
- 当前目标：启动 S3，先确认 T0701 有限画布及真实 HWND 缩放语义；未编码、不进入其他子任务或 S4。
- 实际修改：PROJECT_CONTEXT.md（当前阶段/风险/交接）、TASK_PLAN.md（逐项范围/依赖/验收/回退）、TASKS.md（S2完成/S3设计确认与统计）、ACCEPTANCE.md（用户验收状态和助手历史范围）、docs/S2_REPORT.md（§9最新确认）、docs/ADR/0003-s2-embedding.md（退出记录，恢复策略不变）、docs/COMPATIBILITY_MATRIX.md（用户来源及S3未测）、README.md（当前阶段）与本日志。
- 实际检查：Get-Content 复读状态/计划/日志/任务/验收/S2报告/ADR/矩阵/项目规则/初稿、生产状态模型和分层测试；rg --files 核对现有源码与测试。git status --short / git rev-parse --show-toplevel 均返回 fatal: not a git repository。
- 文件级验证：PowerShell 主表计数、S2五任务/五验收状态及原稿 SHA256 断言 PASS：38对，14完成/1进行中/16未开始/7待确认。首次计数正则包含历史说明行而报数量错误；限定任务行的 Mxx 与验收行的 Txxxx 列后复核通过，未删除数据或放宽任务数量要求。
- 未执行：构建/测试/UI Smoke/运行程序，原因是关键设计仍待确认且本次仅同步阶段文档；没有业务代码变更。旧成功/失败证据保留，不把历史结果写成本次执行。
- 未解决：Q04/Q05 中有限边界/负坐标/重叠固定/原生缩放等需确认；公共 API 注释、原生错误覆盖债务保留；强制宿主终止、TOCTOU、第三方兼容及 .NET 8 支持周期限制不变。
- 保护/回退：未修改原稿、工程、源码、测试、用户数据或环境；未启动/关闭/强杀进程；无 Git 初始化/提交/恢复点。已确认的 S2 独立宿主/恢复路径保留，未来恢复失败必须保留宿主与快照重试。
- 下一步：确认 T0701 关键设计，再仅实施该子任务、执行相关验证、同步真实结果，然后再继续下一个子任务。

## 2026-10-19：T0701 空画布子任务实现与验证

- 用户确认：4096×4096 DIP正坐标有限画布、允许重叠/容器边界内；布局缩放50%–200%并由目标重新排版，不做像素镜像缩放。
- 单一目标/范围：仅T0701空画布与视口状态，保留S2独立宿主；容器选择和T0702/T0801/T0802/T0901-T0903未实现，不进入S4。
- 实际代码：新增Application/CanvasViewModel.cs（验证/边界/命令/通知）、App/CanvasView.xaml及.xaml.cs（空画布/滚动/中键捕获/清理）；修改MainWindow.xaml（默认发现页保留、画布标签接入），未修改原生源码/工程引用/旧测试。
- 实际测试：新增CanvasViewModelTests.cs（10项）、Invoke-S3CanvasUiSmoke.ps1（仅本次自建App正常关闭）；新增ADR0004与S3_REPORT.md，更新当前状态/计划/任务/验收/README/矩阵与日志。
- 实际命令：dotnet build FlexCanvas.sln -c Release --no-restore，0警告/0错误；dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-t0701" --results-directory "$env:TEMP\FlexCanvas-s3-t0701-TestResults"，Domain9/Application41/Infrastructure12共62通过，0失败/跳过；powershell -NoProfile -File tests/Invoke-S3CanvasUiSmoke.ps1，PASS。
- 证据区别：本次62项非原生含10新增，排除10原生；不是历史S2全量62项。未重跑S1/S2 UI Smoke及原生测试，当前原生链未改；不声称真实中键/IME/卡片选择/原生画布/性能通过。
- 阶段状态：空画布子任务自动化检查点完成，T0701/A0701整组未完成（容器选择未实现、真实手势待反馈）；其他S3子任务未开始。
- 保护/回退：当前进程检查未发现FlexCanvas/Probe；UI Smoke仅启动并正常关闭自建App，不强杀。原稿/用户数据/旧失败证据保留；Git仍非仓库，未初始化/提交/创建恢复点。现有S2解除/恢复链不变。
- 下一步：人工验证当前画布平移/缩放/回原点；反馈问题仅处理当前子任务，后续先确认单一范围及剩余固定/吸附/最小化规则。完整交接见docs/S3_REPORT.md。

## 2026-10-19：T0701 用户验证与 T0901 非固定纯布局子任务

- 最新反馈：“验证完成开始下一个子任务”，接受空画布人工验证通过；容器选择尚未实现，不标记T0701整组通过。
- 单一目标：T0901非固定容器纯布局基础；新增Domain/CanvasLayout.cs（不可变快照/校验/移动/尺寸/层级）与Domain.Tests/LayoutEngineTests.cs（22项）；不接UI/原生/JSON，不决定固定容器策略。
- 范围：4096×4096正坐标、240×160最小DIP、允许重叠；非法值/越界/重复空ID或层级拒绝并保留原布局；层级紧凑重排保持其他项相对顺序，重复操作几何无累计变化。
- 实际验证：dotnet build FlexCanvas.sln -c Release --no-restore，0警告/错误；dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-t0901" --results-directory "$env:TEMP\FlexCanvas-s3-t0901-TestResults"，31/41/12共84通过，0失败/跳过，新增22项。TRX保留。
- 未执行UI/原生验证：代码未接入这些路径；未启动/关闭/强杀进程。Git检查仍非仓库，不初始化/提交；原稿/用户文件/旧失败证据未覆盖。
- 同步：PROJECT_CONTEXT/TASK_PLAN/TASKS/ACCEPTANCE、S3报告/ADR0004/兼容矩阵/README与本日志。T0901进行中，固定规则未完成，A0901整组保持未测试；S3不退出。
- 下一步：等待下一单一子任务授权，先确认固定策略或容器选择接入范围；不自动推进磁吸/原生联动/S4。交接细节见S3报告后续检查点。

## 2026-10-19：固定策略与真实会话选择入口

- 最新授权：固定策略后接入容器选择；用户确认锁位置/尺寸，允许层级/选择/应用交互/解除。
- 实际源码/测试：CanvasLayout.cs与LayoutEngineTests.cs增加IsPinned/SetPinned及2用例；新增ContainerSelectionModel.cs与ContainerSelectionTests.cs（3用例）；MainWindow.xaml/.cs增加真实会话页/刷新/精确选择/复核定位/关闭等待；NativeHostingWindow.cs仅公开HostSurface。修改Invoke-S2UiSmoke.ps1添加可选VerifySessionSelection，不移除旧验证。
- 分阶段验证：固定后Release0警告/错误及Domain33通过；选择后Release0警告/错误，非原生89项（33/44/12）通过。TRX位于TEMP/FlexCanvas-s3-pin-TestResults及FlexCanvas-s3-selection-TestResults，旧证据保留。
- UI实际命令：powershell -NoProfile -File tests/Invoke-S2UiSmoke.ps1 -VerifySessionSelection。首轮定位选择断言超时，脚本过早读取渲染状态；增加有效32位SessionId呈现等待后复测PASS，业务源码未为此调整。受控双宿主选择/定位/解除刷新和原有安全恢复通过，正常关闭自建目标，无强杀。
- 实现限制：固定只约束Domain布局，尚未锁S2独立宿主；选择是真实会话入口，不是画布卡片，T0701/T0702/T0801及S3整组未完成。真实键盘/IME/恢复失败选择UI未测试，不扩大fake证据。
- 状态同步：T0901完成/A0901通过；更新状态/计划/任务/验收/S3报告/ADR0004/矩阵/README与本日志。Git仍非仓库，未初始化/提交，原稿哈希未变，用户数据/旧失败证据保留。
- 下一步：人工验证真实会话页，反馈异常仅修当前入口；画布卡片、固定UI/原生联动后续授权再实施，不进入磁吸/S4。

## 2026-10-19：S3 需求纠偏与双模式总览首段（未完成完整任务）

- 用户明确完整画布交互/原始尺寸比例/自由布局磁吸/非全屏聚焦/邻窗/真实尺寸联动；画布缩放不得改原生尺寸，覆盖旧缩放重新排版。用户选择同画布几何总览+原生1:1交互双模式，不准用截图/假内容。
- 核查：HwndHost/原生适配/会话引擎/独立宿主与画布源码；当前ResizeNative及LocationChanged强制填宿主，外部尺寸会覆盖，不能直接使用作新双向同步。Microsoft Technology Regions Overview验证airspace限制。
- 实际代码：新增SnapEngine.cs/WindowOverviewModel.cs、2组测试（5+2）；修改CanvasView.xaml/.cs与MainWindow.xaml/.cs，总览真实会话原始bounds*96/DPI尺寸矩形、移动吸附指南、刷新保留位置。没有名称/调整大小/原生交互/双向同步/邻窗动画，不将此报告为完整实现。
- 实际失败/复测：首次Release构建CS0136两个错误，局部候选x/y变量重命名，复测0警告/错误；非原生96项（38/46/12）通过，TRX TEMP/FlexCanvas-s3-overview-TestResults；空画布UI及S2双宿主选择UI回归PASS，正常关闭自建程序，无强杀。实际总览拖动指南未验收。
- 同步状态/计划/任务/验收/报告/ADR/矩阵/README，保留历史。Git非仓库，无初始化提交；原稿/原生源码/数据与失败证据保留。
- 下一步总览人工验收后继续原生段；本轮完整任务仍未完成，不能进入S4或把矩形总览当真实交互。

## 2026-10-19：总览用户通过、宿主返回修复与S3正式任务修订

- 用户确认总览验证成功，并报告独立Host覆盖主窗需最小化；要求修改S3任务md以交付完整效果，不推进其他业务。
- 实际修复：MainWindow.xaml.cs移除Owner=this、传返回主窗动作；NativeHostingWindow.cs新增ReturnToMain按钮。保留_hosts安全解除/失败阻止关闭，原生服务未改。Owner导致owned宿主保持主窗上方，不存在ShowDialog或嵌入后主窗禁用代码。
- 保护：查询时用户App27240/Probe29328/36820运行，未操作/关闭；构建隔离TEMP/FlexCanvas-s3-host-return-artifacts，不覆盖旧运行产物。Git非仓库，无初始化/提交。
- 验证：隔离Release构建0警告/错误；Invoke-S2UiSmoke.ps1新增ArtifactsPath和owner/前台/不最小化断言，受控双宿主返回/选择/解除/主窗恢复PASS。只正常关闭本次程序，无强杀。
- 实际测试失败：隔离非原生4项ArchitectureTests源码目录查找失败，92通过/4失败，TRX保留。FindRoot增加CallerFilePath源码回退，未跳过测试/删断言；最终构建0警告/错误，s3-host-return-fixed TRX38/46/12共96通过，0失败/跳过，位于TEMP/FlexCanvas-s3-host-return-TestResults。
- 文档：TASKS的S3主表/执行顺序/状态统计和ACCEPTANCE的对应七组/退出门槛正式改写，覆盖名称原始尺寸/视图缩放/同画布原生/显式尺寸外部读回/非全屏邻窗平滑切换/完整吸附指南松手提交；T0902/T0903进行中，38对任务验收保持。同步PROJECT_CONTEXT/TASK_PLAN/S3报告/ADR/README/矩阵与日志。
- 未完成：同画布原生交互、名称/全尺寸范围、显式尺寸双向联动、邻窗边带/动画、完整吸附事务/覆盖；总览用户成功不等于S3退出。本次未重跑原生集成（服务未改），UI安全恢复实际通过。
- 下一步：用户正常退出旧版后构建启动，验证返回主窗；再按正式任务表继续原生段，不自动扩大到S4。失败快照/用户数据/原稿保留。

## 2026-10-19：跨电脑ZIP交接材料

- 用户目标：将项目ZIP交给下一台电脑的Trae，提供开发环境检查/安装步骤及首次接管提示词；当前停止业务开发。
- 实际新增：HANDOFF_NEW_PC.md（当前状态/源码证据、ZIP携带与排除边界、SDK8.0.426官方安装SHA512/签名/UAC、版本及工程验证步骤、下一步），FIRST_TAKEOVER_PROMPT.md（首次只读核对、最新S3目标/限制、安全约束与授权门槛）。
- 实际更新：PROJECT_CONTEXT/TASK_PLAN/本日志，记录交接位置和业务暂停。没有修改源码/工程/原稿或旧环境报告，没有新安装/构建/测试/ZIP生成/Git初始化提交。
- 实际核查：读取SDK/中央依赖/README/ENV报告及当前状态计划日志/任务表，Git仍非仓库；当前App39012运行，未操作/关闭，承载状态未知，不复用旧PID。不能据此宣称可以安全强杀或内存布局随ZIP持久化。
- 证据限制：96项非原生/隔离构建/UI恢复为历史结果，新电脑命令全部是待执行步骤，不宣称新机验证通过。TEMP里的最终/失败TRX须单独复制保留，源码ZIP不自动含SDK/缓存/本机配置。
- 交接：S0-S2已退出，S3部分完成；双模式/只视图缩放语义和未完成原生交互明确保留。接收方先打开实际解压根目录核对，不另起骨架或越过S3到S4。

## 2026-10-26：新电脑只读接手核对与非原生基线验证

- 目标：按HANDOFF_NEW_PC.md/FIRST_TAKEOVER_PROMPT完成跨电脑ZIP接手；先只读核对真实状态与环境，获授权后执行新电脑基线验证（restore/build/非原生test），不推进业务编码。
- 只读核对：完整读取交接/状态/任务/验收/报告/ADR/矩阵/规则/初稿/工程配置；核对11工程、分层引用、关键源码与测试（含ArchitectureTests的CallerFilePath回退）；确认S0/S1/S2已退出、S3进行中未退出，旧ResizeNative填满宿主、总览无名称等未完成项与文档一致。Git仍非仓库（fatal），未初始化/提交；查询时无FlexCanvas/Probe进程，未操作任何外部窗口。
- 环境实测：Windows 11 家庭中文版 10.0.26200 x64；SDK 8.0.426 x64与运行时8.0.32已预装（无需安装）；Git 2.39.1；NuGet官方源；PowerShell 5.1（执行策略Restricted，见风险）。新机环境记录另存docs/ENVIRONMENT_NEW_PC.md，不覆盖旧机ENVIRONMENT_REPORT.md。
- 授权后基线验证（项目根顺序执行，均成功）：
  - `dotnet restore FlexCanvas.sln`：11工程还原成功。
  - `dotnet build FlexCanvas.sln -c Release --no-restore`：0警告/0错误。
  - `dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=new-pc-baseline" --results-directory "$env:TEMP\FlexCanvas-new-pc-TestResults"`：Domain38/Application46/Infrastructure12共96通过，0失败/0跳过；TRX在上述TEMP目录。
- 证据边界：96项为新机实测非原生回归基线，与旧机历史数量一致但来源独立；ZIP旧bin/obj不作证据；UI Smoke与全量原生集成测试未在新机执行，需另行授权。
- 未解决：PowerShell执行策略可能阻后续UI脚本（届时用-Bypass单次范围或另行授权）；.NET 8 EOL 2026-11-10临近；家庭中文版与旧机专业版的原生兼容需新机实测；Trae调试方案未确认。
- 保护/回退：未修改源码/工程/原稿/用户数据；无业务代码变更；Git无初始化/提交/恢复点。
- 下一步/交接：按TASKS.md S3顺序实施首个业务子任务（T0701/T0702名称+完整原始尺寸范围），先读相关代码、规划目标/范围/验收/回退再编码；详见docs/ENVIRONMENT_NEW_PC.md §5。

## 2026-10-26：T0701/T0702 子任务——总览名称与原始尺寸范围明确处理

- 单一目标：总览卡片显示窗口名称；较小/超界原始尺寸会话由单条状态提示改为逐项明确列出（原始物理尺寸/DPI/原因），不伪造几何、不改原生尺寸、不进入原生交互。
- 设计决策：不改Domain WindowIdentity（避免波及全部调用点与公共接口）；名称采用App层运行期映射——嵌入确认时从发现列表记录标题（空则进程名），刷新会话时按Hwnd关联并在会话消失后清理，仅内存显示、不持久化、不写日志。
- 实际修改：
  | 文件 | 内容与目的 |
  |---|---|
  | src/FlexCanvas.Application/WindowOverviewModel.cs | 新增OverviewOmission记录、Names/Omissions投影与通知；Update接受可选名称解析器；小于最小/超出画布分别记录原因，保留原布局不伪造 |
  | src/FlexCanvas.App/CanvasView.xaml.cs | UpdateSessions接受Hwnd→名称映射；卡片显示名称+逻辑DIP+原始物理px/DPI；画布底部新增省略会话状态区 |
  | src/FlexCanvas.App/CanvasView.xaml | 新增OverviewStatus动态TextBlock（CanvasOmissionStatus，默认折叠） |
  | src/FlexCanvas.App/MainWindow.xaml.cs | 嵌入确认后记录_windowNames；RefreshSessionsAsync清理失效映射并传给总览 |
  | tests/FlexCanvas.Application.Tests/WindowOverviewTests.cs | 新增4项：名称解析不改几何、小于最小明确省略、超界省略及会话消失后清除、混合场景 |
- 实际验证：首次构建2个CS1503（Assert.Equal int/uint重载不匹配），改120u复测；期间一次编辑误删exceeds断言当即恢复，一次误替换Pin测试当即恢复，未跳过任何断言。最终`dotnet build FlexCanvas.sln -c Release --no-restore` 0警告/0错误；`dotnet test ... --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-names-omissions" --results-directory "$env:TEMP\FlexCanvas-new-pc-TestResults"` Domain38/Application50/Infrastructure12共100通过，0失败/跳过。
- 证据边界：名称映射依赖嵌入时的发现列表选择，嵌入前已不在发现列表或标题为空的窗口回退进程名/会话ID短码；本子任务未触及原生路径与S2宿主。A0701整组仍未测试，需人工总览验证（嵌入自制Probe后查看卡片名称/原始尺寸行与省略提示）。
- 保护/回退：未修改Domain/Infrastructure/工程配置/原稿；无原生调用与持久化变更，回退仅需还原上述4个源码文件；Git仍非仓库，未初始化/提交。
- 下一步：人工验证总览名称与省略显示；通过后按TASKS.md顺序进入T0702（逻辑DIP/原生像素/可见裁剪分离，原生1:1模式）前的必要设计确认。

## 2026-10-26：总览名称人工确认与T0702a原生1:1几何投影模型

- 用户确认：总览名称/原始尺寸行/拖动吸附/仅视图缩放人工验证通过（用户未附逐步骤日志，按用户确认接受；较小/超界省略显示与多窗口名称未单独反馈，不夸大范围）。A0701整组仍因容器选择/原生联动未完成保持未测试。
- 子任务目标：落实ADR0004"拆开目标实际尺寸与可见裁剪区域"——建立原生1:1纯几何投影模型；不修改旧ResizeNative/LocationChanged（S2已验收路径保持不变）。
- 实际新增/修改：
  | 文件 | 内容与目的 |
  |---|---|
  | src/FlexCanvas.Domain/NativeGeometry.cs（新增） | ClipRegion/NativeSurfaceBounds/NativePlacement记录与纯投影：DIP↔目标捕获DPI物理px往返换算；Project输出目标实际物理尺寸+可见宿主裁剪面+目标偏移；视口zoom/pan仅裁剪，目标尺寸不变；非法输入抛ArgumentOutOfRange |
  | src/FlexCanvas.Application/CanvasViewModel.cs | 新增VisibleRegion()：由offset/zoom/viewport计算可见逻辑DIP区域，仅视图语义 |
  | tests/FlexCanvas.Domain.Tests/NativeGeometryTests.cs（新增） | 10项：96/120/192 DPI往返、完整可见零偏移、左/上部分裁剪保持目标尺寸、不相交隐藏、**视口变换前后Target相等**、零DPI/NaN拒绝、62.5px AwayFromZero取整 |
  | tests/FlexCanvas.Application.Tests/CanvasViewModelTests.cs | 新增1项：缩放/平移下VisibleRegion逻辑原点保持、宽度=viewport/zoom |
- 实际验证（含失败记录）：首次sln构建失败——用户验证遗留的Win32Probe PID7612运行中锁定其bin下Infrastructure.dll（MSB3026/3027），未强杀进程；先绕行按工程跑Domain48/Application51通过；用户正常关闭Probe后完整sln Release构建0警告/0错误，非原生Domain48/Application51/Infrastructure12共111通过，0失败/跳过。TRX前缀s3-native-geometry。
- 测试编写期间两处自纠：DPI边界用例62.5取整断言先写62后按AwayFromZero更正为63；VisibleRegion缩放断言原点误算为100，复核"偏移随缩放翻倍保持逻辑原点"后更正为200。修正过程保留在本条目。
- 范围边界：本子任务纯模型，未接画布UI/原生宿主/嵌入路径；NativePlacement尚无生产消费者，供T0702后续子任务（原生1:1宿主放置）使用；不宣称A0702通过。
- 保护/回退：无既有行为变更（CanvasViewModel仅新增方法）；Git仍非仓库，未初始化/提交。
- 下一步：T0702下一子任务——同画布原生1:1模式UI入口与宿主放置（消费NativeGeometry/VisibleRegion），先读CanvasView/NativeHostingWindow/embedding相关代码并规划单一范围。

## 2026-10-26：T0702b 同画布原生1:1模式（嵌入入口、宿主放置与安全解除）

- 单一目标：把真实HWND接入同画布1:1交互——新嵌入入口"嵌入到画布(1:1)"、CanvasView原生模式（NativeOverlay宿主层、按共享布局放置、目标保持原始物理尺寸、视口裁剪）、引擎显式PlaceAsync、关闭链安全解除。S2独立宿主路径未动；不实现自由拖动/显式尺寸调整/边带（后续T0801/T0802）。
- 关键设计（依据ADR0004）：HwndHost不能被WPF裁剪（airspace），因此裁剪是原生的——宿主HWND=可见裁剪区域物理尺寸，目标窗口SetBounds为负偏移+完整原始尺寸，超出宿主边界的部分由宿主窗口客户区裁剪；目标实际尺寸仅显式尺寸调整可变，视图zoom/pan只改变宿主位置与裁剪。嵌入成功即发送全尺寸Place，消除初始400×300瞬态。
- 实际修改：
  | 文件 | 内容与目的 |
  |---|---|
  | src/FlexCanvas.Application/WindowEmbeddingService.cs | IWindowEmbeddingService新增PlaceAsync；引擎Place(id,bounds)显式SetBounds不填满宿主，失败保留会话可恢复 |
  | src/FlexCanvas.Infrastructure/Win32/WindowEmbeddingService.cs | PlaceAsync走同一专用STA串行派发 |
  | src/FlexCanvas.App/CanvasView.xaml | 工具栏模式切换按钮（CanvasModeToggle）；ScrollViewer外包Grid并新增NativeOverlay覆盖层（IsHitTestVisible=False不拦截WPF输入） |
  | src/FlexCanvas.App/CanvasView.xaml.cs | 原生模式状态与宿主管理：PrepareNativeHost创建承载面、按Host句柄绑定会话、会话失效清理宿主；PlaceVisibleHosts用NativeGeometry投影放置（目标1:1物理尺寸+裁剪偏移经PlaceAsync）；绑定即发送全尺寸；模式切换/视口变换联动；DetachAllAsync关闭前逐会话解除、失败保留 |
  | src/FlexCanvas.App/MainWindow.xaml | 新按钮"嵌入到画布(1:1)"（EmbedToCanvas）；TabControl命名MainTabs |
  | src/FlexCanvas.App/MainWindow.xaml.cs | EmbedToCanvasAsync：同一检查/确认/名称记录→画布承载面→EmbedAsync→成功后切换到空间画布并进入原生模式；关闭链在S2宿主之后增加SpatialCanvas.DetachAllAsync，失败阻止退出 |
  | tests/FlexCanvas.Application.Tests/WindowEmbeddingEngineTests.cs | 新增3项：Place应用显式边界不填宿主；缺失/已退出会话拒绝且不产生原生操作；Place失败保留会话可恢复并可解除 |
- 实际验证：`dotnet build FlexCanvas.sln -c Release --no-restore` 0警告/0错误（一次通过）；`dotnet test ... --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-canvas-native-mode" ...` Domain48/Application54/Infrastructure12共114通过，0失败/跳过；TRX在%TEMP%\FlexCanvas-new-pc-TestResults。
- 证据边界：真实HWND嵌入/放置/裁剪/输入/解除恢复**未经自动化与人工验证**；嵌入瞬态（先按初始宿主尺寸填充再立即Place全尺寸）为已知行为；同显示器同DPI（capture本就要求DPI一致）；NativeHostingWindow（S2）路径未触及。A0702整组仍未测试。
- 保护/回退：关闭失败保留画布宿主与会话可重试；Git仍非仓库，未初始化/提交。
- 下一步：人工验证（嵌入到画布→原生模式真实窗口1:1显示→平移缩放裁剪→目标尺寸不变→解除恢复→关闭App恢复）；通过后进入T0802显式尺寸调整/外部读回或按TASKS顺序的边带/切换。
- 人工验证失败与修复（同日）：用户报告从"窗口发现"页嵌入后未自动跳到空间画布。原因：WPF TabControl只连接选中页，CanvasView未在视觉树时HwndHost不构建HWND，PrepareNativeHost返回0，流程在切页前失败返回。修复：确认后先`MainTabs.SelectedItem=空间画布`并`UpdateLayout()`再创建承载面。复测Release构建0警告/0错误、非原生114项通过（TRX前缀s3-canvas-fix-tab）。旧失败证据保留于本条目，不删除。

## 2026-10-26：T0702b 第二轮人工反馈修复（裁剪溢出、防抖与原生边框拖动）

- 用户反馈：①平移时闪烁且原生窗口溢出画布区；②+/-缩放后裁剪显示不全；③1:1模式不可拖动窗口位置，建议加细外边框（内部仍可交互、后续沿用）；④解除恢复验证通过（用户确认）。
- 诊断与修复（全部CanvasView.xaml/.cs，域/引擎未动）：
  | 问题 | 根因与修复 |
  |---|---|
  | 溢出画布区/裁剪不全 | 宿主定位误用容器起点显示坐标；容器部分移出视口左/上侧时宿主画到画布区外。修复：改用可见交集起点 `max(container, region)` 的显示坐标，缩放/平移下裁剪起点始终锚定在视口内 |
  | 平移闪烁 | 每滚动帧同步发送目标SetWindowPos与宿主WPF布局交错。修复：QueuePlacement以DispatcherPriority.Background合并多帧放置请求，每帧仅更新WPF宿主/边框（同步布局），原生SetWindowPos合并为一批 |
  | 1:1不可拖动 | 原生窗口独占鼠标输入，WPF无法在窗口上方命中。修复（按用户建议）：每会话新增原生边框——顶部20px名称带+4px外环，内部Background=null完全穿透给真实窗口；按住名称带/外环拖动即移动容器逻辑位置（换算显示delta/zoom），固定容器按已确认策略不可拖；该结构供T0801邻窗边带沿用 |
- 实际验证：首次构建2个CS错误（缺System.Windows.Threading using；Thickness无1/2参构造new(FrameSidePad)/new(4,0)），改为显式4参后复测；第一次编译失败期间的测试结果不作为本修复验证。最终Release构建0警告/0错误，非原生Domain48/Application54/Infrastructure12共114通过0失败/跳过；TRX前缀s3-canvas-frame-fix3。
- 已知边界：原生模式下拖动暂不带吸附辅助线（自由移动，snap:false；T0903后续统一）；边框名称带文本为WPF元素，位于原生窗口上方带内不受airspace遮挡（边框环内区域无原生内容）；A0702整组仍未测试，待用户按新步骤复测（平移/缩放裁剪锚定、闪烁缓解、边框拖动、内部交互、模式切换、解除恢复）。
- 保护/回退：仅App层显示/交互逻辑修改；Git仍非仓库，未初始化/提交。

## 2026-10-26：T0702b用户确认与T0802a显式尺寸调整

- 用户确认：T0702b第二轮修复人工验证通过（裁剪锚定/闪烁缓解/边框拖动/内部交互/模式切换/解除恢复），未附逐步骤日志，按确认接受。T0702b收尾；A0702整组仍因外部尺寸读回/防循环未完成保持未测试。
- T0802a单一目标：原生模式显式尺寸调整——按已确认语义，显式调整是唯一允许改变原生实际尺寸的路径；视图变换仍不得改变。
- 实际修改：
  | 文件 | 内容与目的 |
  |---|---|
  | src/FlexCanvas.Application/WindowOverviewModel.cs | 新增Resize(id,w,h)：经LayoutEngine.ResizeContainer校验，固定/非法/未知会话拒绝并保留原布局 |
  | src/FlexCanvas.App/CanvasView.xaml.cs | 边框新增右/下8px调整外带（FrameGrip，Thumb，光标SizeWE/SizeNS，位于边框外侧空白区不遮挡原生窗口）；OnGripStarted/Delta/Completed：DragDelta累计换算显示px→逻辑DIP（delta/zoom），实时Resize并重放置；固定容器不启动调整 |
  | tests/FlexCanvas.Application.Tests/WindowOverviewTests.cs | 新增1项：Resize更新逻辑尺寸；低于最小/未知会话/固定均拒绝且保留原布局 |
- 机制说明：容器逻辑DIP变化→NativeGeometry投影Target随之变化→QueuePlacement发送新SetBounds（宽度同步防抖）；松手即内存布局已提交；最小240×160由LayoutValidator强制。外部尺寸读回/防反馈循环（目标自改尺寸、最小跟踪尺寸）不在本子任务，留待T0802b。
- 实际验证：Release构建0警告/0错误（一次通过）；非原生Domain48/Application55/Infrastructure12共115通过0失败/跳过；TRX前缀s3-t0802a-resize。
- 证据边界：真实窗口拖拽调整尺寸行为（目标跟随、固定锁定、总览/原生模式共享尺寸）待人工验证；总览模式暂无尺寸手柄（后续需要再单独子任务）；A0802整组未测试。
- 保护/回退：仅App显示层+模型方法新增；Git仍非仓库，未初始化/提交。
- 下一步：人工验证后按TASKS顺序：T0902/T0903吸附补齐或T0802b外部尺寸读回/防循环，或T0801边带/平滑切换（由用户确认优先级）。

## 2026-10-26：T0802a 反馈修复（首次移动闪现、拖拽闪烁与边框悬垂）

- 用户反馈三问题（附截图）：①首次移动1:1窗口时画布底部闪现一次原生尺寸窗口，点击后消失；②拖拽仍有边框与内容闪烁；③窗口移到画布边界时内容被裁但蓝色边框完整悬垂（显示效果差）。截图另见状态栏"目标已退出或身份已变化[WINDOW_NOT_FOUND]"为发现页选中目标失效提示（嵌入后目标不再是顶层窗口，S1既有行为），非新缺陷。
- 修复（仅CanvasView.xaml.cs）：
  | 问题 | 处理 |
  |---|---|
  | 首次移动闪现 | 移除绑定会话时的"全尺寸(0,0)"立即放置消息（与宿主尺寸/位置更新时序交错的瞬态来源）；绑定后直接按当前视口投影计算并经QueuePlacement发送正确裁剪bounds；总览模式下绑定的会话也发送投影bounds（宿主仍隐藏） |
  | 拖拽闪烁 | 拖动/尺寸调整中不再每帧发送原生SetWindowPos——仅更新WPF宿主与边框（同步布局），松手时（OnFrameUp/OnGripCompleted）提交一次最终原生位置/尺寸，落实"松手确定内存布局"语义 |
  | 边框悬垂 | 宿主/边框尺寸从可见交集中扣除FrameGrip（此前Grip加在可见区外导致容器贴边时边框+12px悬垂出画布且左右不对称）；frame与内容严格同裁剪 |
- 实际验证：Release构建0警告/0错误（一次通过）；非原生115项（48/55/12）通过0失败/跳过；TRX前缀s3-t0802a-fix-round2。
- 证据边界：三问题均为用户实测现象，修复基于机理分析+自动化回归；真实效果需用户复测确认，若"底部闪现"仍复现需用户补充录屏/更具体触发条件再定位。
- 保护/回退：仅App层CanvasView修改；Git仍非仓库，未初始化/提交。

## 2026-10-26：采纳用户建议移除原生模式外框拖动/调整（职责分离决策）

- 用户复测：三问题仍存在，问题①在"嵌入后第一次拖动"复现且上轮修复无变化，用户建议移除外框移动逻辑。上轮修复机理判断未命中问题①根因，如实记录；采纳用户建议。
- 决策：原生1:1模式为**只读呈现+真实窗口交互**——布局编辑（拖动/吸附/尺寸）一律在总览模式完成（该路径稳定且已用户验证），原生模式移除全部拖动/调整入口。消除拖动期WPF与原生SetWindowPos交错，问题①②触发路径不复存在；边框悬垂修复保留并去除FrameGrip（无手柄不再需要预留，边框恢复4px对称）。
- 实际修改（仅CanvasView.xaml.cs）：移除frame鼠标事件与右/下调整Thumb、OnFrameDown/Move/Up与OnGripStarted/Delta/Completed方法、_frameDrag*/_frameResize*状态、FrameGrip常量；frame整体IsHitTestVisible=false（名称带/环仅视觉）；宿主/边框尺寸=可见交集扣除2×Side/Top+Side。
- 保留：嵌入到画布、原生1:1显示与裁剪、视口联动（防抖合并仍在）、真实窗口交互、模式切换、名称带显示、WindowOverviewModel.Resize（总览模式后续可用，用例保留）。
- 实际验证：Release构建0警告/0错误；非原生115项（48/55/12）通过0失败/跳过；TRX前缀s3-readonly-native。
- 证据边界：问题①根因未确证（两次机理修复未命中），若用户复测在**非拖动**场景仍出现底部闪现需继续定位；A0802的"自由拖动"验收改为以总览模式编辑+原生呈现的组合满足，原生模式拖动不再是本子任务交付物，T0801边带规划时再评估是否恢复原生内编辑。
- 保护/回退：仅App层修改；Git仍非仓库，未初始化/提交。

- 后续同日用户明确指令"移除外框逻辑"：边框**整体移除**（含名称带/细环视觉显示，非仅拖动），删除_nativeFrames/UpdateFrame/HideFrame/RemoveFrame/FrameTopPad/FrameSidePad及Primitives引用；宿主尺寸=完整可见交集、目标偏移=纯投影值（与总览几何完全一致，无pad偏移）；NativeOverlay仅承载宿主。复测Release构建0警告/0错误、非原生115项通过，TRX前缀s3-no-frame。原生模式最终形态：纯真实窗口1:1呈现+裁剪+视口联动+真实交互；名称/位置/尺寸编辑在总览模式。

## 2026-10-27：S3一次性收尾（用户授权以记录代替人工验收）

- 用户授权：把S3剩余任务一次性全部完成，自动化验证+记录即可，不需要人工验收。按此执行，同时如实区分已验证项与用户豁免的人工场景。
- 本轮实现（T0802b/T0801/T0902/T0903收尾）：
  | 交付 | 内容 |
  |---|---|
  | T0802b 外部尺寸读回+防循环 | Domain EmbeddingSessionStatus加可选ExternalBounds；IWindowEmbeddingApi新增GetTargetBounds（Win32 GetWindowRect+ScreenToClient读回宿主相对bounds）；引擎区分会话类型——显式Place过的画布会话在LocationChanged时读回校验（±2px容差内=我们自己的回声→忽略防循环；显著不同=外部尺寸改变→记录ExternalBounds，后续Place覆盖）；未Place的S2会话保持原"填满宿主"语义（S2已验收不破坏）；CanvasView在会话刷新时消费ExternalBounds经NativeGeometry.ToDip同步容器逻辑尺寸 |
  | T0801 非全屏突出/边带/平滑切换 | PlaceVisibleHosts后按布局ZIndex升序将各宿主SetWindowPos置顶（NativeWindowHost.MoveSurfaceToTop，SWP_NOSIZE|NOMOVE|NOACTIVATE）——选中容器所在宿主Z序在上（非全屏、不改变任何窗口尺寸/位置、不重建会话）；重叠时邻窗未被覆盖的边缘自然可点击（原生窗口本身接收输入）；切换=纯Z序变化即平滑 |
  | T0702/T0802 总览尺寸调整入口 | 总览卡片右下角12px Thumb（Cursors.SizeNWSE）：DragDelta累计/zoom→overview.Resize（固定/非法拒绝），共享布局→原生尺寸同步；弥补原生模式手柄移除后的显式尺寸入口 |
  | T0902 吸附补齐 | SnapEngineTests新增4项：双轴同时吸附、恰好12 DIP不吸附（严格阈值）、边界内吸附且固定/其他目标不被移动、1000次重复往返零漂移 |
  | T0903 共用记录 | 总览编辑（拖动/吸附辅助线/尺寸手柄）与原生呈现共享同一WindowOverviewModel布局，松手即内存提交；仅内存保存不写工作区 |
- 实际修改文件：src/FlexCanvas.Domain/EmbeddingModels.cs；src/FlexCanvas.Application/WindowEmbeddingService.cs（接口+引擎Session/Place/HandleEvent/VerifyExternalSize）；src/FlexCanvas.Infrastructure/Win32/NativeMethods.cs（ScreenToClient）；Win32WindowEmbeddingApi.cs（GetTargetBounds）；NativeWindowHost.cs（MoveSurfaceToTop）；src/FlexCanvas.App/CanvasView.xaml.cs（ExternalBounds同步/Z序/总览Thumb）；tests：WindowEmbeddingEngineTests（FakeApi.GetTargetBounds+读回/S2语义/Place覆盖3项）、SnapEngineTests（+4项）。
- 实际验证（本机实测）：构建0警告/0错误；测试编写期间真实失败保留——CS1061三处（WindowBounds?可空结构解包，改is模式）；BothAxesSnapSimultaneously首轮失败（构造距离恰为12触发严格阈值，改251）；失败TRX（s3-final）保留。复测非原生121项（52/57/12）通过0失败/跳过；**全量含原生131项（57/52/22）通过0失败/跳过**——新电脑首次全量原生回归（三类受控Probe真实嵌入/客户区同步/精确恢复/真实退出事件/双会话hook）通过；TRX前缀s3-final2与s3-full-native（%TEMP%\FlexCanvas-new-pc-TestResults及FlexCanvas-new-pc-full-TestResults）。
- S3状态：按用户2026-10-27授权，S3以"实现+自动化验证+记录"完成并退出；人工场景（真实键盘/IME、实际权限/DPI差异、真实崩溃、T0801边带/切换的真人操作确认、T0702b外部读回的真实第三方窗口实测）由用户显式豁免，保持"未测试"记录，不伪称通过。原生路径仍仅覆盖Windows 11 Home 26200/150% DPI/同权限/PerMonitorV2受控Probe，不外推。
- 保护/回退：S2语义未破坏（引擎用例证明）；无用户进程操作（全量测试前后进程检查干净，测试Probe正常关闭）；Git仍非仓库，未初始化/提交。

## 空白阶段记录模板

### 日期与阶段

- 日期：
- 阶段 / 任务 ID：
- 本阶段单一目标：
- 当前状态（完成 / 部分完成 / 阻塞）：

### 实际修改

| 文件路径 | 修改内容与目的 |
|---|---|
| 待填写 | 待填写 |

- 已完成工作与实现证据：
- 未完成工作：
- 决策、依据与确认情况：

### 实际验证

| 实际执行的命令 / 人工步骤 | 环境与配置 | 实际结果 / 错误 / 证据 |
|---|---|---|
| 待填写 | 待填写 | 待填写 |

- 未执行的测试或构建及原因：
- 验收结论与限制：

### 未解决问题

- 问题与影响：
- 原因分析及待确认事项：

### 下一步与交接

- 下一阶段目标与具体操作：
- 依赖与阻塞：
- PROJECT_CONTEXT.md / TASK_PLAN.md 更新情况：
- Git 状态检查结果：
- 是否需要恢复点 / 提交及用户授权情况（不自动初始化或提交）：
- 交接报告位置或摘要：
