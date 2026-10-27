# FlexCanvas 项目上下文

## 最新检查点：S3已完成并退出（2026-10-27）

按用户授权"把S3的任务一次性全部完成，只需要记录，不需要人工验收"，2026-10-27完成S3收尾：T0802b外部尺寸读回+防循环（引擎按会话是否显式放置区分画布/S2语义，±2px回声容差；S2填满语义经用例证明未破坏）、T0801非全屏Z序突出（按布局ZIndex宿主置顶，不缩放/不挪窗/不重建会话，邻窗未覆盖边缘可点击）、总览尺寸手柄（共享布局显式同步原生）、T0902吸附完整用例、T0903双模式共用记录。**全量含原生131项（57/52/22）通过0失败/跳过——新电脑首次全量原生回归通过**；构建0警告/0错误；失败证据保留（CS1061三处、双轴阈值用例首轮失败，TRX s3-final）。S3按用户授权以"实现+自动化验证+记录"退出；用户多轮人工确认覆盖核心行为，剩余人工场景（真实键盘/IME、权限/DPI差异、真实崩溃、边带真人操作、第三方外部读回实测）由用户显式豁免并保持"未测试"记录，不伪称通过。当前阶段：**S4工作区未开发，下一阶段为S4**（需另行授权）。Git仍非仓库，未初始化/提交。详见CHANGELOG_DEV.md 2026-10-27条目、S3报告检查点8、ADR0004收尾决策。

## 新电脑接手检查点（2026-10-26）

项目已按跨电脑ZIP交接在新电脑完成首次接管。S0/S1/S2已退出；S3进行中未退出。新电脑环境：Windows 11 家庭中文版 10.0.26200 x64；SDK 8.0.426/运行时8.0.32已预装；Git 2.39.1可用但项目仍非Git仓库（未初始化）；NuGet官方源。基线验证实测通过（restore成功、Release 0警告/0错误、非原生96项）；环境详情见[docs/ENVIRONMENT_NEW_PC.md](docs/ENVIRONMENT_NEW_PC.md)。

本日S3推进：①总览名称/省略清单（用户确认）；②T0702a原生几何投影纯模型；③T0702b同画布原生1:1模式（两轮反馈修复——跳转/裁剪锚定/防抖/边框拖动，用户确认验证通过）；④T0802a原生模式显式尺寸调整（右/下调整外带+模型Resize，非原生115项通过，待人工验证）。A0701/A0702/A0802整组均未测试；外部尺寸读回/防循环、吸附补齐、边带/平滑切换待做。下一步：人工验证T0802a拖拽调整；通过后按TASKS顺序选择T0802b/T0902-T0903/T0801。记录见CHANGELOG_DEV.md 2026-10-26条目与S3报告检查点4/5。

## 最新目标修订

最新用户要求为跨电脑ZIP交接，本次停止业务开发，仅生成[新电脑配置与交接](HANDOFF_NEW_PC.md)及[首次接管提示词](FIRST_TAKEOVER_PROMPT.md)。S3仍部分完成，不因用户“任务差不多完成”推断阶段退出。当前只读查询发现App PID39012，未操作/关闭，承载状态未知；打包前须用户正常解除/退出，不沿用此PID。未创建ZIP、安装工具、重跑构建测试或提交；旧TEMP证据不自动进入源码包，迁移边界与复制清单见交接说明。

最新用户反馈：总览“验证成功”，并报告独立Host挡住主窗、要求正式改写S3任务清单。已按TASKS/ACCEPTANCE的原ID重写S3范围/依赖/整组验收，不将总览当同画布原生交互完成。修复MainWindow创建Host的Owner层级约束，NativeHostingWindow新增返回主窗按钮，安全关闭仍由_hosts逐一解除；受控UI验证无需最小化即可返回主窗且目标安全恢复。隔离Release0警告/错误、96项非原生通过；首次隔离架构查找4失败修正后复测，失败证据保留。当前用户App PID27240与两Probe为查询时状态，未关闭或修改会话；修复仅临时隔离产物，运行中的旧版本不会自动更新。

用户明确要求S3完整空间窗口交互，而非空画布/独立宿主列表；画布缩放只改视图，不改原生尺寸，覆盖历史布局缩放重新排版。已确认同一画布双模式：真实几何矩形总览（无截图/假内容）与未来真实HWND 1:1交互/邻窗边带。当前只完成第一段WindowOverviewModel/SnapEngine/CanvasView会话尺寸总览与移动吸附基础，刷新会话导入snapshot原始尺寸/DPI，位置内存保留；不写JSON。

最新Release0警告/错误、96项非原生（38/46/12）与空画布/受控双宿主回归通过；首次CS0136局部变量重名修正后复测。总览人工验证现按用户成功反馈接受，不编造逐项测量；未实现名称、窗口尺寸调整、同画布原生模式、双向同步、邻窗边带和平滑切换。任务未完成/S3未退出。旧89项和独立宿主不代表当前完整需求通过；现有ResizeNative/LocationChanged强制填充宿主需在原生段拆分，HwndHost不能仿射缩放。详见ADR0004及S3报告最新部分检查点。

> 更新日期：2026-10-19。S0/S1/S2 已按自动化与用户验收确认退出；S3 已交付 T0701 空画布子任务，整组未验收。

最新检查点（覆盖下方历史记录）：用户授权“处理固定策略，再接入容器选择”，并确认固定锁位置和尺寸、允许层级/选择/应用交互/解除。T0901布局模型固定策略已实现/验证，A0901通过；新增ContainerSelectionModel与主窗“承载会话”页，按真实SessionId选择/刷新、复核后定位既有S2宿主。此为真实会话选择入口，不是画布内卡片；逻辑IsPinned尚未控制S2独立宿主拖动/尺寸。最终Release0警告/错误、非原生89项（33/44/12）通过，受控双宿主选择UI Smoke复测通过。T0701画布卡片选择/原生联动仍未完成，S3未退出，下一步人工验证会话选择后再推进T0702/T0801。详细失败/复测/文件见S3报告最新检查点。
>
> 最新用户确认“s2验证完毕可以退出，现在开始任务s3”。接受 S2 五组验收及阶段退出，来源为用户确认，未附逐场景配置或日志；不扩大助手既有 62 项测试/UI Smoke 的亲测范围。当前授权 S3：T0701/T0702/T0801/T0802/T0901/T0902/T0903；每次一个子任务，Q04/Q05 关键设计先确认，不开发 S4。
> 产品与架构初稿：[FlexCanvas_Project_Development_Specification.md](FlexCanvas_Project_Development_Specification.md)（1.0.0）。
> 阶段计划：[TASK_PLAN.md](TASK_PLAN.md)；开发日志：[CHANGELOG_DEV.md](CHANGELOG_DEV.md)。
> 模块任务：[TASKS.md](TASKS.md)；验收：[ACCEPTANCE.md](ACCEPTANCE.md)；当前检查点：[S2 报告](docs/S2_REPORT.md)。

最新检查点：[S3 报告](docs/S3_REPORT.md)、[ADR 0004](docs/ADR/0004-s3-canvas.md)。用户已确认 4096×4096 DIP 正坐标有限画布、允许重叠、布局缩放并重新排版。CanvasViewModel/CanvasView 与主窗标签接入已实现；历史Release0警告/错误、非原生62项（新增10项）与空画布UI Smoke通过，随后用户确认空画布验证完成。没有容器选择/原生画布联动，T0701整组与S3未退出。

最新反馈“验证完成开始下一个子任务”：接受 T0701 空画布人工验证通过，不等于尚未实现的容器选择通过。本次仅完成 T0901 非固定容器纯布局子任务：Domain/CanvasLayout.cs 的不可变快照/校验/移动/尺寸/前后层级及 LayoutEngineTests.cs；Release0警告/错误，非原生84项（Domain31/Application41/Infrastructure12，新增22项）通过。未接UI或原生，不启动/关闭用户进程。固定容器语义待确认，T0901整组仍进行中；下一步另行确认容器选择接入或固定策略的单一子任务，不自动继续磁吸/S4。

## 1. 项目目标与需求基线

FlexCanvas 规划为 Windows 空间化窗口容器与工作区管理系统：在统一主窗口内承载符合兼容性条件的外部应用真实窗口，支持交互、容器移动与缩放、基础磁吸、本地工作区保存与部分恢复。

- MVP 规划：WPF 主界面、窗口发现、HWND 承载与解除嵌入、输入/焦点/尺寸同步、画布布局、JSON 工作区、日志与测试。
- 后续规划：组合容器、多显示器增强、快捷键配置、工作区导入导出、经授权的启动辅助等。
- 这些内容均为需求，不代表已经开发或通过验收。性能指标（如启动 3 秒、拖动目标 60 FPS）也尚未测量。

## 2. 已确认与待确认的技术栈

“初稿明确”表示已有文档依据，不表示工程配置、依赖或运行验证已经完成。

| 状态 | 技术或约束 | 依据与现状 |
|---|---|---|
| 已配置：初稿技术基线 | C# / .NET 8 / WPF，Windows 10/11 x64，Win32 P/Invoke | 实际 FlexCanvas.sln、11 个 .csproj、global.json；SDK 8.0.426、nullable、x64 与分层构建已验证 |
| 已确认：初稿存储基线 | 版本化 JSON、System.Text.Json、本地 `%LocalAppData%\FlexCanvas\`，MVP 不用关系型数据库 | §3.2、§5；尚无模型与存储实现 |
| 已配置：阶段依赖 | xUnit 2.9.3、runner 2.8.2、Microsoft.NET.Test.Sdk 17.11.1、DI 8.0.1、CommunityToolkit.Mvvm 8.4.0 | Directory.Packages.props；正式项目还原/构建通过，日志采用有界后台 JSONL 端口实现，见 ADR 0002 |
| 待确认：可选项 | Serilog、NSubstitute 或 Moq、FlaUI、GitHub Actions、发布方式细节 | 初稿含可选/后续项，不在本次选定或安装 |
| 已安装并验证 | .NET 8 SDK 8.0.426 x64，Desktop / NETCore / AspNetCore Runtime 8.0.32 | 官方系统安装、哈希/签名校验、版本复核、临时 NuGet 还原及 WPF 编译/运行时验证通过；详见 ENVIRONMENT_REPORT.md |
| 已核查：工具基础 | Windows 11 x64、Git 2.53.0、Trae 3.5.91、VS 2022 Build Tools、Windows SDK 目录 | CLI 编译链可用；Trae 仅发现内置 C# 语法扩展，语言服务/断点调试仍待选型验证 |

## 3. 架构与技术决策

当前已配置四层工程及独立 POC/测试；完整产品的模块/接口仍属规划。以下为实际骨架与后续设计边界：

| 规划模块 | 职责与依赖基线 |
|---|---|
| App / Presentation | WPF 入口、Views、ViewModels、UI Dispatcher；不散布 Win32 细节 |
| Application | 窗口承载、兼容性、工作区用例编排；依赖 Domain |
| Domain | 布局、吸附、模型、验证与状态；不依赖 WPF、Win32、文件系统 |
| Infrastructure | Win32、JSON、日志；依赖 Domain 与 Application，不直接更新 UI |
| 独立 POC / tests | tools/WindowHostProbe 与三个 Probe 已存在；S0 人工验收由用户确认通过，S1 自动化/UI Smoke 已执行 |

已有文档决策及原因：

1. 跨进程 HWND 嵌入为首要验证路线，而非已证实方案；先 POC，避免在兼容性未知时投入完整画布开发（§1.8、§11.2）。
2. 真实窗口交互不可由截图、DWM Thumbnail 或视频流冒充；不兼容时降级为独立窗口（附录 C）。
3. UI、布局、互操作、持久化分层，以支持独立验证和错误隔离（§3.3、§4）。
4. HWND 仅是运行期标识；持久化身份使用应用识别信息，避免重启与句柄复用造成误匹配（§5、§9）。
5. MVP 本地 JSON 存储并采用原子写入，避免不必要数据库与损坏风险（§3.2、§5）。
6. 对同一 HWND 的操作串行化、可诊断、可回滚；线程边界和具体实现仍需设计及 POC（§4.4）。

已新增 [ADR 0001](docs/ADR/0001-s0-prototype.md)：用户选择完整 S0 POC 后，建立最小骨架；原生操作在专用 STA/每次 PerMonitorV2 上下文串行执行，仅操作仓库内受控 Probe。POC 结果/端口与生产接口区分，未冻结完整产品设计。其他技术决策仍需真实证据与相应授权。

[ADR 0002](docs/ADR/0002-s1-discovery.md) 记录正式只读发现与 POC 白名单隔离、OperationResult<T>、DI 所有权、选中目标 2 秒复核、日志脱敏与容量限制；不把权限/elevation 或 DPI 初筛当嵌入保证。

[ADR 0003](docs/ADR/0003-s2-embedding.md) 记录正式多会话、完整权限/进程代次/placement 快照、专用 STA、失败恢复责任、成对事件监听与独立承载窗口。正式承载不是画布；恢复失败必须保留宿主重试。

## 4. 实际文件与实现证据

初始化前，`Get-ChildItem -Force` 与 `rg --files --hidden` 仅发现原始初稿。

- 初始化前没有 README、LICENSE、src、tests、docs、tools 或技术栈配置；现在已有 S0 README/配置/源码/测试/工具/ADR，LICENSE 尚未创建。
- 没有 Git 仓库；`git rev-parse --show-toplevel` 和 `git status --short` 均返回 `fatal: not a git repository`。
- 没有原有项目级规则。已读取用户全局长任务规则，未修改。
- 初始化新增 `PROJECT_CONTEXT.md`、`TASK_PLAN.md`、`CHANGELOG_DEV.md`、`.trae/rules/long-task-development.md`；环境阶段新增 `ENVIRONMENT_REPORT.md` 并更新三个状态文档，开发日志现含真实环境记录。
- 已实现 S0：四层项目、受控 Probe 的真实嵌入/移动/尺寸/解除、快照回滚、串行调度与诊断入口。代码证据为 src/FlexCanvas.Infrastructure/Win32、src/FlexCanvas.Domain/Poc、tools/WindowHostProbe、tools/Probes 与分层 tests；详细文件清单见 S0 报告§4。
- 已实现 S1：App.xaml.cs DI/异常入口、MainWindow/WindowManagerViewModel、WindowDiscoveryModels/Services、Win32WindowDiscoveryService、RollingDiagnosticSink；正式窗口枚举/搜索/身份选择/退出清除/风险报告与有界日志。完整列表仅显式刷新，定时只复核选中目标。
- 实际验证：Release 构建 0 警告/错误；最终 Domain 9、Application 5、Infrastructure 13 共 27 项测试全通过；三类 Probe 在 125% DPI 各 3 次循环、真实失败回滚、嵌入中正常退出通过。WPF App/Host 可见启动与无目标正常关闭已验证。
- S1 实际验证：Release 构建 0 警告/错误，Domain 9/Application 20/Infrastructure 17 共 46 项测试通过；真实 WPF UI Smoke 通过。S0 人工验证按用户确认接受通过；原助手未亲测项目不伪称亲测。
- 已实现 S2：EmbeddingModels/WindowEmbeddingEngine、Win32WindowEmbeddingApi/WindowEmbeddingService/NativeWindowHost、正式 App 的确认入口与 NativeHostingWindow；多个目标串行承载、客户区同步、显式聚焦、幂等解除、回滚重试、真实 hook/进程退出与关闭恢复。
- S2 最终实际验证：Release 0 警告/错误；Domain 9/Application 31/Infrastructure 22 共 62 项通过、0 失败/跳过；S1/S2 UI Smoke 通过。三类 Probe 各 3 次精确恢复及真实退出事件、两个 UI 宿主与原生文本消息读写通过。不是键盘/IME 验收，完整证据见 S2 报告。
- 已实现 S3 的 T0701 空画布子任务：CanvasViewModel/CanvasView、MainWindow 标签接入、10项状态用例和空画布 UI Smoke；不依赖/修改原生承载。尚未实现容器选择/布局/磁吸/原生画布联动、JSON 工作区、完整设置/日志清理导出、发布和 CI。S2 完整验收已于 2026-10-19 获用户确认；助手未亲测的场景仍不声称亲测。公共 API XML 注释及原生错误路径覆盖不足仍是代码/覆盖债务，用户验收不等于这些代码已补齐。

## 5. 重要约束

- 以最新用户要求、真实文件和验证证据核实状态；与初稿冲突时先说明，不静默更改设计。
- 不修改原始初稿，不覆盖全局规则，不未经授权重写项目或改变技术基线、数据格式、公共接口。
- 不伪造测试，不把推荐技术、示例代码、计划功能描述为已配置、已实现或已通过。
- 不注入目标进程、不绕过权限边界、不默认强杀外部应用、不自动执行不可信工作区命令。
- 不引入 HTTP API、账号、云服务或关系型数据库，除非后续明确授权。
- 不写入密钥、密码、令牌或无关用户敏感信息；保护现有修改和数据。
- 最新授权为 S3，允许画布/容器/布局与磁吸及相关验证。系统级修改/提权仍须另行授权；不初始化 Git、不提交、不修改远程，不开发 S4 工作区。

## 6. 当前阶段、风险与待确认事项

当前阶段：S0/S1/S2已退出。T0901纯布局及固定规则已实现/验证；T0701空画布用户通过，并新增真实承载会话选择入口，画布内卡片选择未实现。固定规则仅布局层生效，S2独立宿主未锁定。S3整组未验收，初始化/环境/模块规划任务均已完成。

待确认与风险：

1. 助手 S0/S2 原生路径实测 Windows 11/125% DPI/same elevation/PerMonitorV2；S0/S1/S2 完整验收按用户确认，不附加编造环境。S2 权限差异/混合 DPI 等缺少助手亲测及逐场景证据；POC 不代表第三方应用兼容，初筛也不是保证。
2. 最小验证骨架边界按用户完整 S0 选择落地，见 ADR 0001；S1 启动编排与发现 UI 已实现，完整设置/索引/画布仍在后续，不提前开发。
3. §1.6 将导入导出、快捷键系统和多显示器放在后续，§2.2/§7.8 又列相关能力，MVP 还要求多 DPI/多显示器环境测试。需区分测试覆盖与产品功能，确认首版范围。
4. POC 使用独立 ProbeResult/IProbeWindowApi；S2 正式接口见 ADR 0003。已实现 WinEventHook 与进程创建时间复核，但仍不能完全解决同进程 HWND 复用与操作间 TOCTOU；队列满可能丢通知，1 秒活动宿主查询作为兜底。
5. SDK/测试/DI/MVVM 已固定并实测，S1 日志参数见 ADR 0002；schema/完整设置/发布形态和扩展应用环境仍待确认。.NET 8 EOL 为 2026-11-10；不擅自升级技术路线。
6. Git 尚未初始化，没有版本化恢复点；是否使用 Git 及初始化时机由用户决定。
7. 项目规则路径和元数据已按官方文档核对；本机 IDE 加载、生效及上下文压缩能力尚未通过界面确认。规则不是自动会话管理程序。
8. Trae 内置 C# 扩展仅提供语法配置，未发现语言服务/调试扩展；需后续确认官方支持的 IDE 调试方案。本次不安装其他 IDE 或改变现有 VS 工作负载。

## 7. 初始化核查与验证记录（历史）

执行前已完整读取初稿至“文档结束”，读取现有全局规则；本地记忆路径不存在，未据此推断任何实现。

| 实际执行的核查 | 真实结果 |
|---|---|
| `Get-ChildItem -Force`；`rg --files --hidden -g '!.git/**' -g '!node_modules/**'` | 初始化前仅有初稿，没有项目规则或业务工程 |
| `Get-Content -Raw -Encoding UTF8`（初稿、全局规则） | 已完整读取 |
| `git rev-parse --show-toplevel`；`git status --short` | 均失败：非 Git 仓库；未初始化、提交或修改远程 |
| `Get-FileHash ... -Algorithm SHA256` | 已记录原始初稿和全局规则基线，供结束复核 |
| Trae 官方规则文档核查 | `.trae/rules/*.md`，Markdown 与 `alwaysApply: true`；本机 Trae 可执行文件 ProductVersion 为 3.5.91，FileVersion 为 2.3.73738 |
| `dotnet restore/build/test/publish` | 初始化阶段未执行：当时无工程、无业务改动，且禁止安装依赖；环境阶段临时验证见环境报告 |

原始初稿保护基线：58,809 字节；`LastWriteTimeUtc` 为 `2026-10-16 04:32:55`；只读、存档属性。

- 初稿 SHA256：`D75E80553378AD199D41EDBF22516CA8DEFC99A9CBEFD9D5083E0EE6D246B5EA`
- 全局规则 `rule-1792126762643.md` SHA256：`E62A46F399AF75591B40888145A56A9A3064B78C34B8C972492EEB14D65DC8F1`
- 新增文件一致性与保护复核：已通过。PowerShell 使用 `Compare-Object`、`Test-Path`、`Get-Item`、`Get-FileHash` 和正则断言核查文件清单、非空内容、本地链接、规则元数据与交接字段；输出 `PASS`。初稿哈希/大小/时间/属性及全局规则哈希均与基线一致。
- 已复读四个新增文件：无重复项目规则，无状态矛盾；没有业务代码或伪造历史。详细验收状态见 TASK_PLAN.md §4。IDE 实际加载仍待界面确认，不包含在文件级验证结论内。

规则格式依据：https://docs.trae.ai/ide/rules?_lang=zh （2026-10-16 核查）。

## 8. 下一步与交接摘要

当前目标为 S3 空间画布；后续先读取本文件、TASK_PLAN.md、CHANGELOG_DEV.md、TASKS.md、ACCEPTANCE.md、S2 报告/ADR 0003、项目规则及初稿，重新检查文件和 Git 状态。

2026-10-19 最新交接：本次已完成固定规则与真实会话选择入口；文件/命令/首轮UI断言失败及复测见S3报告最新检查点。Git仍非仓库，无初始化/提交授权；原生恢复策略/持久化/原稿未改，用户数据和失败证据保留。下一步人工验证会话列表选择/定位/解除后刷新；画布内卡片及固定UI接入仍依赖T0702/T0801，不自动推进磁吸或S4。

历史会话管理要求（2026-10-16）：用户因长对话/多次压缩要求切换 Trae 新对话并暂停编码。交接只读复核发现 App PID 25972 正在运行，未操作/关闭，承载会话未知；不是当前进程结论。首次只读接手随后完成；当前以 2026-10-19 的 S3 授权为准，见 S2 报告§9。

初始化交接：四个管理文件已创建并通过文件级验收，无业务代码；当时日志仅为空白模板，未造历史。项目规则已出现在后续对话携带的规则上下文中，IDE 设置界面仍未亲自核验。

环境交接：新增 [ENVIRONMENT_REPORT.md](ENVIRONMENT_REPORT.md)，更新三个状态文档；用户授权后安装官方 x64 SDK 8.0.426，安装器 SHA512 / Microsoft 签名核验通过，退出码 0，无重启。实际版本、NuGet 索引/包下载/真实还原、临时 WPF/XAML 编译（0 警告/0 错误）与 .NET 8.0.32 x64、JSON、Win32 P/Invoke、WPF STA 运行时断言均通过。旧运行时仍在，未覆盖初稿、规则或既有配置；SDK 首次生成默认用户 NuGet.Config（原先不存在）。完整证据见环境报告§5与开发日志。

S0 交接：已创建 11 个工程、README、S0 ADR/报告，更新状态/任务/验收与日志；正式测试已执行 27 项全通过，早期 WFAC010/CS0103 和原生坐标失败/修复记录保留。初稿、原规则/ENVIRONMENT_REPORT.md、全局 NuGet 配置与 Bluepoint 未修改；自建测试程序已正常关闭，不强杀用户应用。无 Git 仓库/提交/备份快照。

S1 交接：代码、46 项测试和真实 UI Smoke 已交付；随后用户确认全部验收、退出并授权 S2。原助手未亲测项仍不伪称亲测，详细历史见 S1 报告。

S2 历史交接：正式承载/事件/恢复已交付，最终 62 项与 S1/S2 UI Smoke 通过；原生 SetParent(NULL) 中间态误判、确认框无 UIA Pattern、脚本编码/渲染就绪/输出污染问题均记录在 S2 报告。当时尚待人工验收；现已于 2026-10-19 按用户确认退出并启动 S3。旧测试程序正常关闭为当时结论，不代替当前进程检查。无 Git 提交或备份；原稿、规则、环境报告保持不变。IDE 调试与 .NET 支持周期仍单列未完成。
