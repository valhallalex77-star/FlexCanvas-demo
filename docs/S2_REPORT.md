# S2 正式窗口承载检查点与验收交接

初次交付日期：2026-10-16。更新：2026-10-19。当前状态：用户已确认 S2 验证完毕并退出，授权 S3，见§9。§1-8 保留初次交付/交接历史，不覆盖最新确认。

## 1. 目标、授权与边界

最新用户要求：“s1全部测试验证通过，退出状态开始s2”。S1 退出按用户确认接受；未附逐场景日志，不编造助手亲测记录。

本次只实现 T0501/T0502/T0503/T0601/T0602：真实承载、尺寸/焦点、解除/回滚、事件与安全退出。未开发 S3 画布/容器布局/磁吸、S4 工作区；不修改原稿、不升级技术栈、不提权、不注入、不强杀外部进程。

## 2. 实际实现与文件

| 实际新增/修改文件 | 目的 |
|---|---|
| src/FlexCanvas.Domain/EmbeddingModels.cs | 正式状态机、会话/placement/恢复快照、操作与恢复结果、事件模型 |
| src/FlexCanvas.Domain/WindowDiscoveryModels.cs | 正式承载状态码及诊断事件/API 词汇 |
| src/FlexCanvas.Application/WindowEmbeddingService.cs | 正式接口与多会话引擎；幂等、串行约定、失败恢复责任、身份复核 |
| src/FlexCanvas.Application/WindowDiscoveryServices.cs、WindowManagerViewModel.cs | 保留只读初筛，修正旧 S1 文案，提示嵌入再次确认 |
| src/FlexCanvas.Infrastructure/Win32/NativeMethods.cs | 集中新增 placement、security、creation time、event hook、焦点声明 |
| src/FlexCanvas.Infrastructure/Win32/Win32WindowEmbeddingApi.cs | 严格 capture、原生样式/父关系/客户区/placement/恢复/显式聚焦 |
| src/FlexCanvas.Infrastructure/Win32/WindowEmbeddingService.cs | 专用 STA facade、有限事件队列、成对 hook/进程监听、关闭恢复 |
| src/FlexCanvas.Infrastructure/Win32/NativeWindowHost.cs | 正式 HwndHost；未解除子窗口时禁止销毁宿主 |
| src/FlexCanvas.App/App.xaml.cs | 注册生产承载服务，先恢复后释放 DI |
| src/FlexCanvas.App/MainWindow.xaml、MainWindow.xaml.cs | 再次初筛与确认、嵌入入口、主窗逐宿主解除；失败阻止退出 |
| src/FlexCanvas.App/NativeHostingWindow.cs | 独立承载窗口、解除/聚焦/尺寸/状态，失败保留并允许重试 |
| tests/FlexCanvas.Application.Tests/WindowEmbeddingEngineTests.cs | 11 项状态/故障/身份/幂等用例 |
| tests/FlexCanvas.Infrastructure.Tests/ProductionEmbeddingTests.cs | 5 项生产原生用例，包括三类 Probe、多个目标、真实事件 |
| tests/Invoke-S2UiSmoke.ps1 | 本次自建目标的正式 UI 承载/文本消息读写/解除/关闭验证 |
| docs/ADR/0003-s2-embedding.md、COMPATIBILITY_MATRIX.md、本报告 | 决策、测试支持边界与可执行交接 |
| PROJECT_CONTEXT.md、TASK_PLAN.md、TASKS.md、ACCEPTANCE.md、CHANGELOG_DEV.md、README.md | 同步真实状态、命令、未测范围和运行步骤 |
| docs/S1_REPORT.md | 追加 S1 用户验收确认，不扩大旧自动化证据 |

POC 代码、工具、旧失败 TRX 保留；正式承载不依赖 POC 白名单。没有新依赖安装、系统设置修改或用户数据删除。

关键设计：

- 用户选定并确认后仍重新 capture；拒绝自身/系统 PID、owner/子/工具/危险样式、隐藏/最小化、未知或不匹配权限、UIAccess、非 PerMonitorV2。
- 保存 Guid、HWND/PID、进程创建时间、parent/style/exstyle、物理矩形、WINDOWPLACEMENT、DPI；运行期快照不是持久化身份。
- 修改前置 NeedsRestore；失败回滚，恢复失败保留会话/宿主供重试；操作错误与恢复错误分别返回。
- 专用 STA 串行操作；尺寸 NOACTIVATE，不无故抢焦点；用户聚焦才有限 AttachThreadInput，并在 finally 解除。
- 每活动会话三个 out-of-context WinEventHook，回调只过滤/排队；容量 256，消费者串行复核 session 与身份。进程退出另有通知，1 秒活动宿主查询兜底，不扫描全桌面。
- 主窗正常关闭先逐个解除，服务再释放；失败保持窗口。默认不结束外部应用。

## 3. 实际验证命令与结果

环境：Windows 11 10.0.26200 x64；SDK 8.0.426、Runtime 8.0.32，Release/x64；三类本仓库 Probe，同普通权限、125% 缩放、PerMonitorV2。没有测试 Windows 10 或其他显示器配置。

最终实际命令，工作目录为项目根：

```powershell
dotnet build FlexCanvas.sln -c Release --no-restore
dotnet test FlexCanvas.sln -c Release --no-build --no-restore --logger "trx;LogFilePrefix=s2-final" --results-directory "$env:TEMP\FlexCanvas-s2-final-TestResults"
powershell -NoProfile -File tests/Invoke-S1UiSmoke.ps1
powershell -NoProfile -File tests/Invoke-S2UiSmoke.ps1
```

最终顺序执行且均成功：

| 验证 | 真实结果 |
|---|---|
| Release 构建 | 0 警告、0 错误 |
| Domain | 9 通过、0 失败/跳过 |
| Application | 31 通过、0 失败/跳过 |
| Infrastructure | 22 通过、0 失败/跳过 |
| 合计 | 62 通过；不是累加多次运行 |
| S1 UI Smoke | 启动、搜索、精确选择、风险、空结果、退出清除、正常关闭 PASS |
| S2 UI Smoke | 取消、两个真实 UI 宿主、原生 Edit 文本消息往返、移动/缩放、显式解除、主窗恢复、目标存活 PASS |

最终 TRX 在 `C:\Users\ARCTU\AppData\Local\Temp\FlexCanvas-s2-final-TestResults`：

- `s2-final_net8.0_20261016171556.trx`：Domain 9。
- `s2-final_net8.0_20261016171557.trx`：Application 31。
- `s2-final_net8.0_20261016171604.trx`：Infrastructure 22。

原生测试直接核对 parent、style/exstyle、客户区屏幕原点/尺寸、解除原始矩形；三类各 3 次。hook 每会话 3 个、双会话 6 个，解除后 0。目标正常 WM_CLOSE 后，先等待真实回调产生 TargetExited 日志，不先调用 GetSessions 以轮询冒充事件成功。

模型/fake 覆盖回滚失败/重试、适配器异常、不可读身份保留恢复责任、重复/旧 session/错 PID 事件；这些不是原生异常或第三方兼容性证据。UI 文本验证是 WM_SETTEXT/WM_GETTEXT，不是真键盘、IME 或剪贴板验证。

## 4. 失败、诊断与复测

| 实际失败 | 处理与结果 |
|---|---|
| NativeWindowHost.Handle 隐藏基类属性，CS0108 | 改 SurfaceHandle，不关闭警告；构建复测通过 |
| 首次 62 项中 5 个生产原生恢复失败 | 精确诊断显示仍为子样式/宿主矩形；finally 关闭异常曾掩盖原始断言，调整本次目标清理顺序并保留失败 TRX |
| SetParent(NULL) 中间态误判 | WS_CHILD 尚在时 GetParent 可报告桌面；NULL parent 校验移至原样式恢复后，原生返回错误与最终精确恢复断言保留；62 项复测通过 |
| 确认框 Unsupported Pattern | 实际节点：#32770 下 ID 6/7 原生 Button 被 UIA 暴露为无 Pattern 的 Pane。限定本次 PID、确认框与原生 Button，再发送正常 BM_CLICK |
| 确认框选择超时 | Windows PowerShell 对无 BOM UTF-8 中文字面量兼容问题；改 ASCII 字符码生成标题 |
| 找不到 hosted Edit | UIA 树未暴露跨进程控件；按实际宿主 HWND、owned PID、原生 Edit 定位，有限 SendMessageTimeout 文本往返 |
| 初始 refresh 就绪超时 | HWND 可见早于控件渲染，等待重新查到刷新控件后才读状态 |
| 调试输出混入函数返回值 | 改 Write-Host，避免 $hostA 混入字符串；完整 UI Smoke 与最终全量复测通过 |

先前成功/失败原生 TRX 在 artifacts/TestResults（s2-check、s2-recovery-diagnosis、s2-recovery-fixed 前缀）保留。UI 失败来自实际命令，无 TRX，不伪造证据文件。

UI 排错曾留下本次 App PID 24448、24952；已正常取消/关闭，未强杀。最终验证程序均按正常关闭清理。

## 5. 未完成与风险

以下为助手初次交付时未执行范围与风险；S2 验收现已按§9用户确认接受，但不编造助手亲测或补齐代码证据。

- 三类正式宿主的真实鼠标/滚动/文本选择、键盘/Ctrl+C/V、IME、中英文切换、菜单/下拉/弹窗、Alt+Tab 和聚焦按钮未整组验收。
- 实际不同 elevation/integrity/UIAccess、100/150/200%/混合 DPI/多屏、真正目标/Host 崩溃、恢复失败 UI 重试未测；不为验收自动提权、改显示设置或杀用户进程。
- 最大化 WINDOWPLACEMENT 专项、原生错误路径和公共 API XML 注释覆盖尚需补齐；不声称全部 API 已文档化或所有状态恢复均验证。
- HWND 同 PID 复用和 TOCTOU 不能完全消除；会话/进程代次只能降低风险。事件队列满会告警，仍可能丢通知。
- 宿主强制终止或 CLR 终止异常无法保证恢复；不是持久化崩溃恢复方案。
- 第三方/GPU 自绘/UWP/WinUI 不承诺支持；发现或初筛成功不等于可交互承载。
- .NET 8 于 2026-11-10 结束支持；IDE C# 调试未确认，未擅自升级或安装。

因此五项任务均“待验收”，五组整体验收仍“未测试”，ACCEPTANCE.md 单独列已通过部分场景；S2 退出未通过，不进入 S3。

## 6. 下一步与人工操作

1. 先读取状态/任务/验收、本报告、ADR 0003、矩阵及源码，检查 Git 和当前会话；旧对话不是实现证据。
2. 构建后启动正式 App 与一个自制 Probe（README 提供命令）。刷新、按 PID 搜索、检查，再点“嵌入选中窗口”。先选“否”确认目标未变化，再选“是”。
3. 对 Win32/WPF/WinForms 分别输入中英文；检查 IME 候选/组合文本、鼠标/滚动/选择、Ctrl+C/V、菜单/右键/下拉/弹窗与焦点切换。点击“聚焦应用”后再点击内部文本框，确认没有持续抢焦点。
4. 拖动/缩放/最小化恢复独立宿主，持续输入；点“解除并关闭承载”，检查目标父关系、原始边框/位置和继续可用。每类重复 3 次，并记录实际结果。
5. 开两个宿主：正常关闭一个 Probe，检查 TargetExited，宿主可以正常关闭；再次嵌入两个目标，关闭主 App，核对目标仍存活且恢复。
6. 按获准环境补齐权限/DPI与受控失败/崩溃场景；无安全故障注入入口时记录未测，不用强杀或破坏用户应用代替。记录 Windows/Probe版本、权限、DPI、步骤、结果和日志关联 ID。
7. 发现输入/恢复缺陷先暂停并修复复测。全部必需范围通过后由用户确认 S2 退出，再另行授权 S3。

## 7. 保护与回退

仍非 Git 仓库：实际 git rev-parse/status 返回 fatal；未初始化、提交、改远程或创建备份。是否建立版本化恢复点须用户授权；哈希不是备份。

结束实际复核：原稿 SHA256 `D75E80553378AD199D41EDBF22516CA8DEFC99A9CBEFD9D5083E0EE6D246B5EA`、项目规则 `6BD033C7B544C522292E5DADB5C4F2F1DB3FEE0952BCCBE09ECCA30BDA88F16F`、环境报告 `6DF56971321E1146EE0EE43EB4133AA9CAB91DEF55A4037042A7FFC570523C77` 均与保护基线一致。Get-Process 核查无 App/三类 Probe 残留。PowerShell 文档链接与任务/验收数量断言 PASS：38 对，9 完成/5 待验收/17 未开始/7 待确认。旧源码/POC/测试证据、用户数据与秘密未删除或覆盖。

回退操作：正常 Detach/关闭承载恢复外部窗口；失败保持宿主和服务供重试，检查本地关联日志。禁止直接销毁带外部子窗口的宿主；不默认杀目标、删除工程或回滚用户文件。

## 8. 新对话交接复核

2026-10-16 用户因长对话、多次压缩影响效率，要求暂停当前会话并提供 Trae 新对话交接提示词。本次只核对和记录交接，不继续编码，不推进 S2 退出或 S3。

- 已重读当前状态、计划、日志、本报告和原稿，核对源码入口与现存最终 TRX：9/31/22 通过，0 失败/跳过；本次没有重跑构建、测试或 UI Smoke，不将历史结果写成本次执行。
- Git 实际仍返回非仓库错误；原稿 SHA256 与上述保护基线一致。本次仅更新 PROJECT_CONTEXT.md、TASK_PLAN.md、CHANGELOG_DEV.md 和本报告的交接记录，无业务源码变更。
- 当前发现 FlexCanvas.App PID 25972，主窗标题 FlexCanvas - Window Discovery；不是此前测试残留清理结论。未检查其承载会话、未操作或关闭它；新对话须重新确认 PID/窗口/会话，不复用旧 PID 或强杀。
- 新对话首次只恢复上下文：依据真实文件确认 S2 已实现/待验收及缺口，输出简短状态和下一项验收步骤后等待用户反馈/授权。不要重新生成 S0-S2、重复排错已修问题、自动执行全套 UI 或进入 S3。

## 9. S2 用户验收退出与 S3 启动

2026-10-19 最新用户要求：“s2验证完毕可以退出，现在开始任务s3”。

- 接受 T0501/T0502/T0503/T0601/T0602、A0501/A0502/A0503/A0601/A0602 及 S2 阶段退出通过，来源为用户验收确认。
- 未附逐场景环境、应用版本或日志，不编造助手真实键盘/IME、权限/DPI、崩溃、恢复失败 UI 与最大化专项的亲测结果；历史 62 项与 UI Smoke 范围保持不变。
- 公共 API XML 注释和原生错误路径覆盖不足仍是代码/覆盖债务；验收确认不证明这些代码已补齐。TOCTOU、事件丢失、强制宿主/CLR终止及第三方兼容限制继续保留。
- S3 已获授权；随后用户确认有限画布与布局缩放语义，T0701空画布子任务已实现/自动化验证，容器选择/原生联动未实现，见[S3报告](S3_REPORT.md)。不进入S4、不重建工程、不改变技术栈、不自动提交。
- 本次重读状态/计划/日志/任务/验收/报告/ADR/矩阵/规则/原稿及相关源码测试；Git 仍非仓库。仅同步阶段文档，未重跑构建/测试/UI、未启动或关闭应用。
