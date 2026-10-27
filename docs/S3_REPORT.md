# S3 / T0701 空画布子任务检查点

## 新电脑检查点8：S3一次性收尾与阶段退出（2026-10-27）

用户授权"把S3的任务一次性全部完成，只需要记录，不需要人工验收"。本轮实现剩余全部范围：①T0802b外部尺寸读回与防循环——EmbeddingSessionStatus加ExternalBounds、IWindowEmbeddingApi加GetTargetBounds（GetWindowRect+ScreenToClient），引擎区分显式放置的画布会话（LocationChanged读回校验，±2px内容差忽略防循环，显著差异记录ExternalBounds供布局同步）与未放置的S2会话（保持原填满语义，S2用例证明不破坏）；②T0801——PlaceVisibleHosts后按布局ZIndex升序将宿主SetWindowPos置顶（MoveSurfaceToTop，NOACTIVATE非全屏、不重建会话），邻窗未被覆盖边缘自然可点击，切换为纯Z序变化；③总览卡片右下尺寸手柄（经共享布局显式同步原生尺寸）；④T0902吸附补齐4项用例（双轴/恰好12不吸附/边界与固定参照/1000次往返零漂移）；⑤T0903共用记录（总览编辑与原生呈现共享布局与吸附，松手内存提交，不写工作区）。

验证（本机实测）：Release构建0警告/0错误；真实失败保留（CS1061可空结构解包3处、双轴用例距离恰为12触发严格阈值，失败TRX s3-final保留）；复测非原生121项（52/57/12）通过；**全量含原生131项（57/52/22）通过0失败/跳过**——新电脑首次全量原生回归通过；TRX前缀s3-final2/s3-full-native。

S3阶段退出：按用户2026-10-27授权，以"实现+自动化验证+记录"完成并退出；用户多轮人工确认覆盖总览名称/尺寸、嵌入/裁剪/边框交互/解除恢复等核心行为，剩余人工场景（真实键盘/IME、权限/DPI差异、真实崩溃、边带真人操作、外部读回真实第三方实测）由用户显式豁免并保持"未测试"记录；原生证据仅覆盖Windows 11 Home 26200/150%DPI/同权限/PerMonitorV2受控Probe，不外推第三方兼容。S4工作区未开发。

## 新电脑检查点7：移除原生模式外框（2026-10-26最终形态）

用户两轮指令：先建议移除外框移动逻辑，后明确"移除外框逻辑"。最终形态：边框**整体移除**——原生1:1模式为纯真实窗口1:1呈现+可见交集原生裁剪+视口联动（防抖合并）+真实窗口交互；名称/位置/尺寸等布局编辑统一在总览模式（卡片拖动+吸附线，已验证）。实施过程中先移除拖动/调整逻辑（保留视觉边框），再按用户指令移除边框视觉本身：删除_nativeFrames/UpdateFrame/HideFrame/RemoveFrame/Frame常量，宿主尺寸=完整可见交集、目标偏移=纯NativeGeometry投影值。问题①根因未确证（两次机理修复未命中，如实记录）；移除拖动后触发路径已消除，若非拖动场景仍闪现需继续定位。最终验证：Release构建0警告/0错误、非原生115项通过；TRX前缀s3-no-frame。A0702/A0802整组仍未测试；T0801边带规划时再评估原生内编辑的恢复方式。

## 新电脑检查点6：T0802a反馈修复（2026-10-26）

用户验证T0802a反馈三问题（附截图）：首次移动1:1窗口时底部闪现原生尺寸窗口、拖拽边框/内容闪烁、窗口贴画布边界时边框完整悬垂。修复（仅CanvasView.xaml.cs）：绑定会话不再发送"全尺寸(0,0)"立即放置，改为按当前视口投影发送（消除瞬态）；拖动/调整中只更新WPF宿主与边框、松手提交最终原生位置/尺寸（松手确定语义）；宿主/边框尺寸扣除FrameGrip消除贴边悬垂与不对称。状态栏WINDOW_NOT_FOUND为发现页选中目标失效提示（嵌入后目标非顶层，S1既有行为），非新缺陷。

验证：Release构建0警告/0错误、非原生115项通过；TRX前缀s3-t0802a-fix-round2。真实效果待用户复测；若底部闪现仍复现需补充录屏定位。

## 新电脑检查点5：T0702b确认收尾与T0802a显式尺寸调整（2026-10-26）

用户确认T0702b第二轮修复人工验证通过（裁剪锚定/闪烁缓解/边框拖动/内部交互/模式切换/解除恢复），T0702b收尾；A0702整组仍因外部尺寸读回/防循环未完成保持未测试。

实施T0802a：WindowOverviewModel新增Resize（LayoutEngine校验，固定/非法/未知拒绝保留原布局）；CanvasView边框新增右/下8px调整外带（Thumb，位于边框外侧空白区不遮挡原生窗口），拖拽实时换算（DragDelta累计/zoom）调整容器逻辑DIP并重放置——按已确认语义，显式调整是唯一改变原生实际尺寸的路径，视图变换仍只裁剪。新增1项模型用例。

验证：Release构建0警告/0错误；非原生115项（48/55/12）通过；TRX前缀s3-t0802a-resize。真实窗口拖拽调整（目标跟随/固定锁定/双模式共享尺寸）待人工验证。总览模式暂无尺寸手柄；外部尺寸读回/防反馈循环留待T0802b；A0802整组未测试。

## 新电脑检查点4：T0702b 第二轮反馈修复（2026-10-26）

用户首轮人工验证：跳转修复生效；解除恢复正常（用户确认）。同时反馈：平移闪烁且原生窗口溢出画布区、缩放后裁剪显示不全、1:1模式不可拖动，并建议细外边框方案（内部交互保留）。

修复（仅CanvasView.xaml/.cs）：①宿主定位改用可见交集起点（max(container, region)），裁剪起点始终锚定视口内，修复溢出与缩放裁剪不全；②QueuePlacement以Background优先级合并放置请求，缓解滚动/缩放闪烁；③按用户建议新增原生边框——顶部20px名称带+4px外环、内部完全穿透，按名称带/外环拖动移动容器逻辑位置（固定容器不可拖），结构供T0801邻窗边带沿用；④NativeOverlay去掉IsHitTestVisible=False改为null背景命中模型。

验证：首次构建2个CS错误（缺using与Thickness构造）修复后，Release构建0警告/0错误、非原生114项（48/54/12）通过；TRX前缀s3-canvas-frame-fix3。编译失败期间的测试结果不作数。待用户复测：平移/缩放裁剪锚定、闪烁缓解、边框拖动、内部交互、模式切换、解除恢复。已知边界：原生模式拖动暂无吸附辅助线（T0903统一）；A0702整组未测试。

## 新电脑检查点3：T0702b 同画布原生1:1模式（2026-10-26）

实现同画布真实HWND 1:1交互首段：主窗新增"嵌入到画布(1:1)"入口（同S2的检查/确认/名称记录），在空间画布内创建承载面并直接嵌入；成功后自动切到空间画布原生模式。CanvasView新增模式切换与NativeOverlay宿主层：宿主按共享布局与NativeGeometry投影放置——宿主HWND为可见裁剪区域尺寸，目标SetBounds为负偏移+完整原始物理尺寸，由宿主窗口边界原生裁剪（WPF无法裁剪HwndHost/airspace）；视图zoom/pan只改宿主位置与裁剪，目标实际尺寸不变。会话失效自动清理宿主；关闭链在S2宿主后增加画布会话逐一解除，失败阻止退出。引擎新增PlaceAsync（显式边界、不填满宿主、失败保留会话），走既有专用STA串行派发。

验证：Release构建0警告/0错误；非原生114项（48/54/12，含引擎Place新增3项）通过0失败/跳过；TRX前缀s3-canvas-native-mode。真实HWND嵌入/放置/裁剪/输入/解除恢复待人工验证；嵌入瞬间先按初始宿主尺寸填充再立即Place全尺寸为已知瞬态；A0702整组未测试。

下一步：人工验证通过后按TASKS顺序进入T0802显式尺寸/外部读回或T0801边带/平滑切换。

## 新电脑检查点2：总览名称人工确认与T0702a原生1:1几何投影（2026-10-26）

用户人工验证通过总览名称/原始尺寸行/拖动吸附/仅视图缩放（未附逐步骤日志，按确认接受；A0701整组仍保持未测试）。

随后实施T0702a纯几何投影子任务：新增Domain/NativeGeometry.cs（ClipRegion/NativeSurfaceBounds/NativePlacement；DIP↔捕获DPI物理px换算；Project输出目标实际物理尺寸+可见宿主裁剪面+裁剪偏移；**视口zoom/pan仅裁剪、目标尺寸不变**）；CanvasViewModel新增VisibleRegion()；新增Domain10项/Application1项用例。设计依据ADR0004"拆开目标实际尺寸与可见裁剪区域"；未修改旧ResizeNative/LocationChanged（S2路径不变），NativePlacement尚无生产消费者，供原生1:1宿主放置子任务使用。

验证过程保留真实失败：首次sln构建因用户验证遗留的Win32Probe PID7612锁定bin下Infrastructure.dll失败（MSB3026/3027），未强杀进程，先按工程跑Domain48/Application51通过；用户正常关闭Probe后完整sln Release构建0警告/0错误、非原生48/51/12共111通过0失败/跳过。TRX前缀s3-native-geometry。

下一步：T0702下一子任务——同画布原生1:1模式UI入口与宿主放置，消费NativeGeometry/VisibleRegion；A0702整组未测试。

## 新电脑检查点：总览名称与原始尺寸范围明确处理（2026-10-26）

新电脑基线验证通过后（restore成功、Release 0警告/错误、非原生96项，见docs/ENVIRONMENT_NEW_PC.md），实施T0701/T0702首个业务子任务。

设计与范围：不改Domain WindowIdentity；名称用App层运行期映射（嵌入确认时记录发现列表标题/进程名，按Hwnd关联，会话失效即清理，仅内存显示不持久化）。较小/超界原始尺寸会话从单条状态提示改为逐项省略清单（原始物理px、DPI、换算DIP与原因），不伪造几何、不改原生。

实际修改：Application/WindowOverviewModel.cs（OverviewOmission/Names/Omissions投影与解析器参数）、App/CanvasView.xaml及.xaml.cs（卡片名称+原始尺寸行、CanvasOmissionStatus省略区）、App/MainWindow.xaml.cs（_windowNames记录/清理/传递）、WindowOverviewTests.cs（新增4项）。

实际验证：首次构建2个CS1503（Assert.Equal int/uint重载）修正；一次误删断言与一次误替换Pin测试当即恢复。最终Release 0警告/0错误；非原生Domain38/Application50/Infrastructure12共100通过，0失败/跳过；TRX前缀s3-names-omissions，位于%TEMP%\FlexCanvas-new-pc-TestResults。

未完成：同画布原生1:1交互、显式尺寸调整/外部读回、邻窗边带/平滑切换、固定UI、完整吸附事务仍按TASKS.md顺序推进；A0701整组保持未测试，待人工在总览确认名称/原始尺寸行与省略提示显示正确。

## 最新检查点：总览用户验证、宿主返回修复与正式任务修订

用户反馈“验证成功”，接受当前总览部分人工验证，不扩大逐配置尺寸/原生模式证据。用户报告Host覆盖主窗，须最小化才能操作，并要求按完整效果修改S3任务表。

代码核对未发现ShowDialog或嵌入后禁用主窗；MainWindow创建宿主设置Owner导致owned窗口保持owner之上。小范围修复：MainWindow.xaml.cs移除Owner，传返回主窗动作；NativeHostingWindow.cs新增ReturnToMain按钮。原生父子承载及_hosts恢复责任不变。独立宿主只是过渡，不等同最终同画布交互。

用户App/Probe运行中，未关闭/操作它们，使用`dotnet build FlexCanvas.sln -c Release --artifacts-path "$env:TEMP\FlexCanvas-s3-host-return-artifacts"`隔离构建，0警告/错误。扩展Invoke-S2UiSmoke.ps1支持-ArtifactsPath并断言宿主无owner，点击返回后主窗前台、宿主不最小化；受控双宿主/会话/解除/主窗恢复PASS。只操作本次自建App/Probe。

首次隔离非原生测试4项ArchitectureTests找不到源码（原定位仅产物父目录），保留失败TRX；修改ArchitectureTests.cs的FindRoot增加CallerFilePath源码回退，未删除/跳过断言。最终：

`dotnet test FlexCanvas.sln -c Release --no-build --no-restore --artifacts-path "$env:TEMP\FlexCanvas-s3-host-return-artifacts" --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-host-return-fixed" --results-directory "$env:TEMP\FlexCanvas-s3-host-return-TestResults"`：38/46/12共96通过，0失败/跳过。最终构建也0警告/错误。

TASKS.md正式S3七个ID和ACCEPTANCE对应组已修订为用户完整范围，保留38对任务/验收、不扩展S4：原始尺寸名称比例、仅视图缩放、同画布1:1真实交互、自由移动/显式尺寸与外部读回、非全屏主对象/邻窗边带/平滑切换、完整吸附指南与松手提交。T0902/T0903由未开始更正进行中；原生交互任务仍未开始。

下一步：用户先正常解除/退出旧App再构建启动修复版，验证返回主窗/直接切换无需最小化，关闭仍恢复目标。之后按TASK_PLAN顺序接真实原生模式；本次没有越过范围开发它。原稿和用户数据保留，无Git初始化/提交。

## 最新部分检查点：双模式总览首段

最新用户要求完整画布空间交互，强调缩放不改原生尺寸；已确认同画布几何总览+原生1:1交互双模式。旧“缩放容器重新排版”失效，历史下文保留。本轮未完成完整需求。

实际新增Domain/SnapEngine.cs（边缘、邻接、等间距、严格12DIP阈值和指南），Application/WindowOverviewModel.cs（真实会话原始bounds/DPI映射，内存布局与选择/锁定）；新增SnapEngineTests.cs（5项）/WindowOverviewTests.cs（2项）；修改CanvasView.xaml/.cs（真实会话几何矩形与拖动/指南，复用卡片），MainWindow.xaml/.cs（命名SpatialCanvas并在刷新时导入会话）。不展示截图/窗口内容，不改变原生源码或持久化。

实际验证：首次build失败2个CS0136，候选变量重命名后`dotnet build FlexCanvas.sln -c Release --no-restore`0警告/错误；`dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-overview" --results-directory "$env:TEMP\FlexCanvas-s3-overview-TestResults"`38/46/12共96通过，0失败/跳过；TRX100452/100453/100454。`Invoke-S3CanvasUiSmoke.ps1`与`Invoke-S2UiSmoke.ps1 -VerifySessionSelection`均PASS，正常关闭自建App/Probe，无强杀；不是实际总览拖动/指南测试。

未完成：名称映射、任意原始较小/超界尺寸、调整大小、同画布原生模式、外部尺寸双向同步、非全屏主要窗口/可点击邻窗边带与平滑过渡。现有原生ResizeNative与LocationChanged把目标填宿主，必须先分离裁剪面和实际目标尺寸；HwndHost不受WPF仿射缩放且有airspace限制，不可用截图或直接缩小宿主规避。

下一步先验证总览的原始比例、移动吸附/指南、缩放前后原生尺寸不被总览代码修改；再实施原生段并真实验证。位置只内存保存，不实现S4；Git仍非仓库，无自动提交或恢复点。完整安全解除/恢复责任保留。

日期：2026-10-19。S0/S1/S2 已按用户确认退出；S3 已启动但未完成。当前只交付 T0701 的空画布与视口状态，T0701 整组未验收，不推进其他子任务或 S4。

## 目标与授权

最新要求：“s2验证完毕可以退出，现在开始任务s3”。用户选择正坐标 4096×4096 DIP 有限画布、允许重叠，布局缩放并由目标重新排版；不使用截图、镜像或原生内容仿射缩放。设计见 [ADR 0004](ADR/0004-s3-canvas.md)。

## 实际文件

| 文件 | 目的 |
|---|---|
| src/FlexCanvas.Application/CanvasViewModel.cs | 非 WPF 视口状态、缩放/偏移验证、边界夹紧、命令与通知 |
| src/FlexCanvas.App/CanvasView.xaml、CanvasView.xaml.cs | 空画布、滚动/中键平移、缩放工具栏、回原点与事件清理 |
| src/FlexCanvas.App/MainWindow.xaml | 保留默认发现页、增加画布标签；S2独立承载入口不变 |
| tests/FlexCanvas.Application.Tests/CanvasViewModelTests.cs | 10项初始化/非法缩放/边界/命令/通知测试 |
| tests/Invoke-S3CanvasUiSmoke.ps1 | 仅本次App的真实空画布UI验证及正常关闭 |
| docs/ADR/0004-s3-canvas.md、本报告 | 用户选择、实现边界、命令及交接 |

同步 PROJECT_CONTEXT.md、TASK_PLAN.md、TASKS.md、ACCEPTANCE.md、CHANGELOG_DEV.md、README.md、兼容矩阵及 S2 报告/ADR 的验收退出记录。未修改原稿、原生承载源码、现有测试或工程引用；未初始化/提交 Git。

## 实际验证

| 命令 | 结果 |
|---|---|
| git status --short / git rev-parse --show-toplevel | 非 Git 仓库，不初始化；没有可用版本恢复点 |
| dotnet build FlexCanvas.sln -c Release --no-restore | 0警告/0错误，11工程通过 |
| dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-t0701" --results-directory "$env:TEMP\FlexCanvas-s3-t0701-TestResults" | Domain9/Application41/Infrastructure12，共62通过，0失败/跳过；新增画布10项 |
| powershell -NoProfile -File tests/Invoke-S3CanvasUiSmoke.ps1 | PASS：画布标签、缩放/重置、实际滚动/状态、上下限禁用、正常退出 |

本次62项排除了10项原生集成，不是历史S2的9/31/22全量62项。TRX位于 `%TEMP%\FlexCanvas-s3-t0701-TestResults`，前缀`s3-t0701`，时间093046/093047/093048。未重跑S1/S2 UI Smoke或原生测试，因为本子任务不修改原生逻辑；它们仍为历史证据。

文件级检查初次将历史说明表计入任务总数失败，限定主表后通过；保留记录，不删除行。原稿SHA256仍为`D75E80553378AD199D41EDBF22516CA8DEFC99A9CBEFD9D5083E0EE6D246B5EA`。测试仅正常关闭本次自建App，没有强杀。

## 未完成与风险

- T0701容器选择部分尚无容器模型，不虚构卡片；A0701整组仍未测试。
- 真实中键拖动/释放/窗口切换需人工验证，UI Smoke只验证UIA按钮和滚动，不冒充真实输入。
- T0702原生位置/客户区/DPI/裁剪、T0801/T0802容器、T0901-T0903布局/磁吸尚未实现。
- 缩放当前只作用于空画布WPF元素，后续必须实际调整HWND并由目标重新排版；当前不宣称原生画布承载或性能60FPS通过。
- 固定容器与最小化、吸附等值/多候选规则需相应子任务前确认。S2代码注释/原生错误覆盖债务、TOCTOU、强制宿主终止限制和.NET8支持周期仍保留。

## 可执行交接

当前已完成空画布子任务代码与自动化检查点；待用户验证中键平移/缩放/回原点，反馈异常先仅修复此子任务。下一候选为T0901纯布局基础或T0701容器选择接入，先确认具体范围与剩余规则，不自动同时实施。

启动现有Release：`dotnet run --project src/FlexCanvas.App/FlexCanvas.App.csproj -c Release --no-build`。进入“空间画布”，使用滚动条/中键平移、+/-缩放和“100% / 原点”；嵌入选中窗口仍打开S2独立宿主。承载期间正常解除/退出，失败保留宿主与快照，不用Ctrl+C终止。

## 后续检查点：T0901 非固定纯布局

以上为T0701初次交付历史。用户最新反馈“验证完成开始下一个子任务”，接受空画布人工验证完成，未附逐场景日志；T0701容器选择仍未实现，整组不退出。

本次单一目标是T0901非固定纯布局基础。新增：

- `src/FlexCanvas.Domain/CanvasLayout.cs`：不可变几何/集合快照、校验、移动/尺寸/前后层级；逻辑最小240×160 DIP，4096边界内，允许重叠；非法操作失败并返回原布局。非空唯一ID/唯一非负层级，前后层级紧凑重排、其他项相对顺序不变。
- `tests/FlexCanvas.Domain.Tests/LayoutEngineTests.cs`：22用例，包括边界与非法参数、集合防御复制、缺失ID、层级溢出/幂等/往返及1000次重复几何不漂移、Domain引用检查。

实际命令：

| 命令 | 结果 |
|---|---|
| git status --short / git rev-parse --show-toplevel | 非仓库，未初始化/提交 |
| dotnet build FlexCanvas.sln -c Release --no-restore | 0警告/0错误 |
| dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-t0901" --results-directory "$env:TEMP\FlexCanvas-s3-t0901-TestResults" | Domain31/Application41/Infrastructure12，共84通过，0失败/跳过；新增22 |

TRX前缀`s3-t0901`，时间093851/093852/093853，位于上述临时目录。未执行UI/原生测试：本次代码未接入UI且不修改原生路径；未启动/关闭/强杀用户进程，旧证据与源码保留。只同步状态/任务/验收/日志/ADR/矩阵/README，不修改原稿或工程配置。

固定容器语义未确认，模型未引入IsPinned；因此T0901仍进行中、A0901整组未完成，不夸大为真实窗口布局可用。下一步需另行授权单一子任务，可先确认固定策略或接入T0701容器选择；不自动开始磁吸/S4。当前无Git恢复点，纯模型未接入任何窗口，回退无需解除外部会话；既有S2安全恢复约束不变。

## 最新检查点：固定策略及真实会话选择入口

用户授权“处理固定策略，再接入容器选择”，并选择“锁位置和尺寸”。先实现固定规则并完成Release构建/Domain33项验证，再接入选择：

| 修改文件 | 目的 |
|---|---|
| Domain/CanvasLayout.cs、Domain.Tests/LayoutEngineTests.cs | IsPinned/SetPinned；锁移动/尺寸、允许层级、幂等与解锁；新增2项 |
| Application/ContainerSelectionModel.cs、Application.Tests/ContainerSelectionTests.cs | 真实活动/待恢复会话投影，SessionId选择、失效清除、恢复失败保留；新增3项 |
| App/MainWindow.xaml、MainWindow.xaml.cs | “承载会话”页、显式刷新/选择、定位前再查询、关闭等待查询；不虚构画布卡片 |
| App/NativeHostingWindow.cs | 只公开HostSurface用于精确定位已有宿主，不改变恢复策略 |
| tests/Invoke-S2UiSmoke.ps1 | 可选-VerifySessionSelection；受控双宿主选择/定位/解除后刷新 |

最终实际验证：

- `dotnet build FlexCanvas.sln -c Release --no-restore`：0警告/错误。
- `dotnet test FlexCanvas.sln -c Release --no-build --no-restore --filter "Category!=NativeIntegration" --logger "trx;LogFilePrefix=s3-selection" --results-directory "$env:TEMP\FlexCanvas-s3-selection-TestResults"`：33/44/12共89通过，0失败/跳过。TRX前缀s3-selection、094913/094914/094915；固定阶段TRX在FlexCanvas-s3-pin-TestResults。
- `powershell -NoProfile -File tests/Invoke-S2UiSmoke.ps1 -VerifySessionSelection`：首轮“selection retained during host lookup”超时；未改变业务代码，脚本增加选中32位SessionId实际呈现等待，复测PASS。双宿主选择、定位复核、解除刷新、文本消息/移动尺寸/主窗安全恢复、目标存活均通过；不代表键盘/IME。
- 原稿SHA256仍与保护基线一致，结束查询无FlexCanvas/Probe命名进程；脚本正常清理自建App/Probe，无强杀，Git仍非仓库。

T0901/A0901纯布局整组已完成/通过。选择入口仅真实会话列表及定位S2独立宿主，不是画布内容器，T0701卡片选择与T0702/T0801未完成。IsPinned只约束逻辑布局，尚未控制S2独立宿主系统拖动/尺寸；固定UI接入须后续实现。失败恢复会话仍可选且不被自动清理。

下一步人工验收：正常嵌入两个自制Probe，在“承载会话”页刷新、分别选择并定位；解除一个后刷新确认只剩另一会话；关闭主窗确认目标恢复且仍存活。无会话时列表应为空，选择不应误切换。未通过先只修本入口，暂不推进原生卡片/磁吸/S4。
