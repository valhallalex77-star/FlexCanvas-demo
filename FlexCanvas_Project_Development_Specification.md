# FlexCanvas 项目开发说明书

> **文档版本：** 1.0.0  
> **项目代号：** FlexCanvas  
> **目标平台：** Windows 10 / Windows 11 x64  
> **文档用途：** 作为 Cursor 等 AI 编码工具的项目级开发依据  
> **核心技术路线：** C# / .NET 8 / WPF + Windows 原生窗口管理 API  
> **重要架构约束：** 外部应用窗口嵌入属于跨进程 HWND 承载方案，必须先通过技术验证（POC）确定兼容边界。不得将 DWM Thumbnail、截图或视频流伪装为真实窗口嵌入。

---

## 1. 项目概述

### 1.1 项目名称

FlexCanvas——Windows 空间化窗口容器与工作区管理系统。

### 1.2 项目背景

在科研、论文阅读、软件开发、数据分析等场景中，用户通常需要同时使用 IDE、浏览器、PDF 阅读器、终端、文件管理器等多个独立 Windows 应用。传统 Windows 分屏方式主要依赖固定区域或窗口拖放，窗口数量增加后，容易出现空间不足、窗口尺寸不合理、布局难以维护以及频繁切换的问题。

FlexCanvas 旨在提供一个类似平板虚拟屏空间交互的桌面工作环境：用户在统一主窗口中组织符合兼容性要求的外部应用窗口，并通过自由布局、磁吸拼接、组合管理和工作区恢复，构建适合当前任务的多窗口空间。

### 1.3 产品目标

1. 将符合兼容性要求的外部 Windows 应用窗口承载到 FlexCanvas 主窗口内。
2. 保留真实窗口交互能力，包括鼠标、键盘、滚动、文本输入和常见控件操作。
3. 提供可视化画布，支持窗口卡片的移动、缩放、排列与吸附。
4. 支持将多个窗口组成逻辑组合，并对组合进行整体移动和布局管理。
5. 支持保存、切换和恢复工作区布局。
6. 将窗口承载、布局计算、应用生命周期与界面交互分层，便于验证和迭代。

### 1.4 业务价值

- 降低多应用协作时的窗口切换成本。
- 让用户根据任务而非操作系统固定分屏规则组织空间。
- 通过工作区快照降低重复布置窗口的时间。
- 为后续扩展多显示器、快捷操作和自动布局建立可维护的技术基础。

### 1.5 适用场景

- 软件开发：IDE、终端、浏览器、API 调试工具。
- 科研与论文阅读：PDF 阅读器、文献管理软件、浏览器、笔记工具。
- 数据分析：表格软件、终端、数据库客户端、图表工具。
- 学习与教学：课程资料、代码编辑器、浏览器和练习环境并排查看。

### 1.6 本次开发范围

本项目采用分阶段交付方式。

**MVP 范围：**

- WPF 主窗口与画布基础界面。
- 外部窗口枚举、识别、选择与基础信息展示。
- 原生 HWND 嵌入技术验证及承载模块。
- 嵌入窗口尺寸同步、焦点处理和基础输入验证。
- 画布内窗口容器的移动、缩放与边界管理。
- 基础磁吸布局。
- 本地工作区保存、读取、重命名、删除与恢复。
- 日志、异常处理、兼容性提示和诊断信息。
- 自动化单元测试及人工兼容性测试清单。

**后续版本范围：**

- 组合容器。
- 多显示器适配。
- 快捷键系统。
- 更完善的窗口关闭、重启与恢复流程。
- 更多应用兼容性适配。
- 工作区导入导出。

### 1.7 明确不做的功能边界

MVP 不包含以下功能：

- 不实现远程桌面、云桌面或应用流式传输。
- 不通过截图、DWM Thumbnail 或视频流替代真实窗口承载。
- 不注入外部进程，不读取外部应用私有内存，不修改目标应用业务逻辑。
- 不承诺兼容所有 Windows 应用。
- 不承诺 UWP、管理员权限应用、受保护窗口、反作弊应用、独占全屏应用及特殊安全桌面应用均可嵌入。
- 不开发应用市场、账号体系、云同步、团队协作或在线服务。
- 不将 FlexCanvas 定义为通用虚拟桌面或操作系统 Shell。
- 不强制结束用户的外部应用进程；关闭 FlexCanvas 默认不得静默杀死外部进程。
- 不将工作区恢复承诺为所有应用的无损状态恢复。MVP 优先恢复布局与可识别窗口；应用内部文档、标签页、登录状态等由各应用自行管理。

### 1.8 核心技术前提与风险声明

Windows 的窗口嵌入通常依赖 HWND 父子关系调整，例如 `SetParent` 以及相应窗口样式管理。该方式与“外部应用始终保持独立顶层窗口、只将其内容原生映射到主窗口”不是同一方案。

本项目选择**跨进程 HWND 嵌入作为首要验证路线**，但必须在编码完整画布前完成 POC。不得宣称它对所有应用均可靠。若特定应用无法满足交互与稳定性要求，应将其标记为不兼容，并保留“以独立窗口方式打开”的降级策略。图形合成 API 本身不等于任意外部应用的可交互托管 API。

---

## 2. 整体功能需求

### 2.1 核心功能

#### 2.1.1 主窗口与画布

- 提供主窗口、工具栏、画布区域、窗口列表和状态栏。
- 画布支持平移；MVP 可使用有限画布边界，后续扩展无限画布。
- 支持画布缩放，建议初始范围 50%–200%，缩放步进 10%。
- 画布坐标与屏幕坐标必须分离，布局数据统一使用 DIP（设备无关像素）。
- 画布背景不拦截已承载应用窗口的正常输入。
- 主窗口关闭前显示未完成操作或外部窗口处理提示（如有必要）。

#### 2.1.2 外部窗口发现

- 枚举当前桌面可见的顶层窗口。
- 排除自身进程、不可见窗口、工具窗口、无标题且不适合用户选择的窗口。
- 对每个候选窗口采集 HWND、标题、进程 ID、进程名、窗口矩形、可见状态和权限级别（可检测时）。
- 允许用户刷新列表并选择窗口。
- 不因窗口标题重复而误判同一窗口；HWND 仅作为运行期标识，不作为持久化身份。
- 窗口销毁后及时清理候选项与关联状态。

#### 2.1.3 外部窗口承载

- 通过独立的 Native Window Host 模块执行嵌入。
- 嵌入前保存目标窗口原始父窗口、窗口样式、扩展样式、窗口矩形、DPI 状态（可获取时）及所属进程信息。
- 嵌入操作应有明确的状态机：Discovered → Validating → Embedding → Embedded → Detaching / Failed。
- 根据目标应用要求调整窗口样式；不能假定 `SetParent` 会自动修正 `WS_CHILD` / `WS_POPUP`。
- 嵌入后将目标窗口的大小同步到容器客户区。
- 支持显式解除嵌入，尽可能恢复目标窗口原始状态。
- 对失败操作记录 Win32 错误码，并向用户提供可理解的失败原因。
- 不通过轮询高频枚举所有窗口来实现实时同步；优先使用事件通知，必要时进行低频校准。

#### 2.1.4 窗口容器操作

- 每个已承载窗口对应一个 FlexCanvas 容器。
- 容器具有标题栏、最小化/解除嵌入/关闭承载等操作区。
- 容器移动时，更新容器位置并同步嵌入窗口位置。
- 容器缩放时，更新容器尺寸并同步嵌入窗口客户区。
- 为目标应用设置合理的最小尺寸；若应用有自身最小跟踪尺寸，应尽可能尊重。
- 拖动容器标题栏时，不应将鼠标输入错误地发送到内部应用。
- 内部应用处于交互状态时，容器不得无故抢夺键盘焦点。
- 区分“关闭承载”与“关闭应用”：默认按钮应为解除嵌入/移出画布，不结束外部进程。

#### 2.1.5 磁吸与布局

- 拖动容器靠近其他容器边缘时，提供吸附提示线。
- 当边缘距离小于配置阈值时执行吸附。
- 默认吸附阈值为 12 DIP，可配置。
- 支持水平边缘对齐、垂直边缘对齐和间距对齐。
- 布局计算必须与 WPF 视觉树和 Win32 调用解耦。
- 吸附算法应有单元测试，避免反复拖动产生累计漂移。
- MVP 不要求自动求解复杂的全局排版问题。

#### 2.1.6 工作区管理

- 保存工作区名称、创建时间、更新时间、画布设置、容器位置和尺寸、应用识别信息及恢复策略。
- 支持新建、保存、另存为、重命名、删除、加载工作区。
- 加载工作区时，优先匹配已存在的窗口；未找到的窗口显示为“未恢复”状态，不自动下载或安装应用。
- 对可启动且经用户授权配置的应用，后续版本可提供启动辅助；MVP 不自动执行任意命令行。
- 恢复布局不得把持久化 HWND 当作可靠标识。
- 保存操作使用原子写入，避免进程异常退出导致 JSON 文件损坏。

### 2.2 辅助功能

- 深色/浅色主题。
- 基础快捷键：保存工作区、切换工作区、退出画布操作模式。
- 窗口列表搜索。
- 兼容性状态标识：可嵌入、嵌入中、已嵌入、失败、已退出。
- 本地诊断日志导出。
- 首次启动的权限和兼容性说明。
- 用户可设置默认吸附阈值、画布缩放和关闭行为。

### 2.3 非功能性需求

#### 性能

- 空画布启动目标：普通开发机上 3 秒内完成主界面初始化（不含首次加载系统组件）。
- 不执行全盘扫描。
- 窗口枚举采用按需刷新与事件驱动更新。
- UI 线程不得执行阻塞式进程等待、文件 I/O 或长时间 Win32 操作。
- 布局拖动应保持流畅；目标为 60 FPS，低性能环境允许降级。
- 工作区文件读写采用异步方式，且文件体积应与容器数量近似线性增长。
- 日志采用滚动策略，避免无限增长。

#### 安全

- 默认所有数据存储在本地用户数据目录。
- 不上传窗口标题、进程信息、工作区内容或日志。
- 不注入外部进程，不读取外部进程内存。
- 需要管理员权限的窗口应提示权限不匹配，不得尝试绕过 Windows 安全边界。
- 对从工作区文件读取的路径和命令行进行校验；MVP 不自动执行导入文件中的命令。
- 日志中避免记录敏感文档路径、查询内容等非必要信息；提供日志清理入口。

#### 兼容性

- 支持 Windows 10 22H2 与 Windows 11 的受支持版本，x64 为首要目标。
- .NET 8 Desktop Runtime；发布版本优先采用 self-contained 部署。
- 优先验证 Win32、WinForms、WPF、常见 Win32 桌面程序。
- 浏览器、Electron、Qt、UWP/WinUI、管理员权限应用等按应用逐一验证。
- 多 DPI、多显示器、混合缩放环境必须纳入测试。
- 不兼容应用必须能安全解除嵌入或回退到独立窗口模式。

#### 稳定性

- 对每次 Win32 调用检查返回值，并在失败时立即读取错误码。
- 不在 WPF UI 线程上等待目标应用退出。
- 目标应用崩溃、窗口销毁或 FlexCanvas 异常退出时，尽可能避免破坏外部应用窗口状态。
- 嵌入与解除嵌入必须支持幂等调用。
- 窗口生命周期事件处理必须防止重复注册、重复解除和竞态。
- 核心 Native Window Host 应可独立运行测试，不依赖完整画布界面。

---

## 3. 技术栈选型

### 3.1 推荐技术栈

| 层级 | 技术 | 用途 | 选择理由 |
|---|---|---|---|
| 桌面 UI | C# / .NET 8 / WPF | 主窗口、工具栏、画布、容器视图 | Windows 桌面生态成熟，便于与 Win32 HWND 集成 |
| UI 模式 | MVVM | View 与业务状态解耦 | 可测试、适合 AI 分层生成 |
| MVVM 工具 | CommunityToolkit.Mvvm | ObservableObject、RelayCommand | 轻量、常见、维护成本低 |
| 原生窗口 API | Win32 P/Invoke | EnumWindows、SetParent、SetWindowLongPtr、SetWindowPos 等 | 直接调用 Windows 窗口管理能力 |
| 数据模型 | C# records / classes | 工作区、布局、窗口元数据 | 类型安全、便于版本演进 |
| 本地持久化 | JSON 文件 + System.Text.Json | 工作区和用户设置 | MVP 无需数据库服务，文件可迁移和诊断 |
| 日志 | Microsoft.Extensions.Logging + Serilog（可选） | 结构化日志与滚动文件 | 便于故障定位 |
| 单元测试 | xUnit | 业务逻辑测试 | 生态成熟 |
| Mock | NSubstitute 或 Moq（二选一） | 接口依赖替身 | 测试隔离 |
| UI 自动化 | FlaUI（后续） | WPF UI 自动化 | Windows UI 自动化生态 |
| 打包 | `dotnet publish` | 发布构建 | 可生成 self-contained 文件夹或单文件 |
| 版本管理 | Git | 源码管理 | 标准工程实践 |
| CI | GitHub Actions（可选） | 构建、测试、发布 | 自动化验证 |

### 3.2 数据库选择

MVP **不使用关系型数据库**。工作区、设置和兼容性记录使用版本化 JSON 文件存储在 `%LocalAppData%\FlexCanvas\` 下。

理由：

- 当前数据量小，主要是布局配置。
- 无需复杂事务、查询和多用户并发。
- JSON 便于诊断、备份和迁移。
- 避免在 MVP 中引入数据库升级、迁移和打包负担。

若未来需要大量窗口历史、复杂查询或多工作区关系，可评估 SQLite。不要为了“工业级”而预先引入不必要的数据库。

### 3.3 关键架构决策

- WPF 负责 UI，不直接承载全部 Win32 逻辑。
- 所有 P/Invoke 声明集中在 `Infrastructure/Win32`。
- 窗口承载策略通过接口抽象，避免 `SetParent` 逻辑散落在 ViewModel。
- 布局计算是纯业务逻辑，不依赖 HWND。
- 持久化模型不保存 HWND 作为长期身份。
- 所有外部窗口操作必须可记录、可诊断、可回退。
- 不使用截图、DWM Thumbnail 或渲染镜像代替真实交互。

### 3.4 发布方式

开发阶段：

```bash
dotnet restore
dotnet build
dotnet test
dotnet run --project src/FlexCanvas.App
```

发布阶段：

```bash
dotnet publish src/FlexCanvas.App/FlexCanvas.App.csproj \
  -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true
```

如果 WPF 单文件发布或原生依赖出现兼容问题，允许使用 self-contained 文件夹发布。发布流程应以实际验证通过的配置为准，不可仅凭命令成功就认定运行正常。

---

## 4. 系统整体架构与模块拆分

### 4.1 分层架构

```text
┌────────────────────────────────────────────────────────────┐
│ Presentation Layer — WPF Views                              │
│ MainWindow / CanvasView / WindowPicker / WorkspacePanel     │
└──────────────────────────────┬─────────────────────────────┘
                               │ Binding / Commands
┌──────────────────────────────▼─────────────────────────────┐
│ Application Layer — ViewModels / Use Cases                  │
│ CanvasViewModel / WindowManagerViewModel / WorkspaceService │
└──────────────────────────────┬─────────────────────────────┘
                               │ Interfaces
┌──────────────────────────────▼─────────────────────────────┐
│ Domain Layer                                                 │
│ Layout Engine / Snap Engine / Workspace Models / State      │
└──────────────────────────────┬─────────────────────────────┘
                               │ Ports
┌──────────────────────────────▼─────────────────────────────┐
│ Infrastructure Layer                                         │
│ Win32 Window Host / Window Discovery / JSON Storage / Logs  │
└────────────────────────────────────────────────────────────┘
```

### 4.2 模块职责

#### 4.2.1 FlexCanvas.App

- WPF 应用入口。
- 初始化依赖注入、配置、日志和异常处理。
- 管理主窗口启动与关闭流程。
- 不包含复杂布局算法或 P/Invoke 细节。

#### 4.2.2 Presentation

- `MainWindow`：主窗口框架。
- `CanvasView`：画布视觉区域。
- `WindowPickerView`：外部窗口选择器。
- `WorkspacePanelView`：工作区列表与操作。
- `WindowContainerView`：每个外部窗口对应的 WPF 容器外观。
- View 仅负责呈现与输入绑定，不直接执行 Win32 API。

#### 4.2.3 Application

- `CanvasViewModel`：画布状态、缩放、平移和选中容器。
- `WindowManagerViewModel`：外部窗口发现、嵌入和解除嵌入命令。
- `WorkspaceViewModel`：工作区增删改查。
- `WindowEmbeddingService`：协调验证、嵌入、尺寸同步、解除嵌入。
- `WorkspaceService`：保存、加载、恢复布局。
- `CompatibilityService`：运行兼容性检查与结果汇总。
- 负责业务流程编排，不直接定义 Win32 结构体。

#### 4.2.4 Domain

- `CanvasLayout`：画布尺寸、缩放与平移状态。
- `WindowContainerModel`：容器位置、尺寸、状态和关联窗口。
- `Workspace`：工作区根实体。
- `WindowIdentity`：可持久化的应用识别信息。
- `SnapEngine`：吸附候选与吸附结果计算。
- `LayoutValidator`：布局合法性检查。
- `EmbeddingState`：窗口承载状态枚举。
- 不依赖 WPF、Win32 或文件系统。

#### 4.2.5 Infrastructure.Win32

- `Win32WindowEnumerator`：枚举顶层窗口。
- `Win32WindowInfoReader`：读取标题、进程、窗口矩形、样式等。
- `Win32WindowEmbedder`：执行嵌入与解除嵌入。
- `Win32WindowPositioner`：同步位置与尺寸。
- `Win32WindowEventMonitor`：监控窗口创建、销毁、前台切换等事件。
- `Win32DpiHelper`：DPI 感知与坐标换算辅助。
- `NativeMethods`：集中定义 P/Invoke、结构体、常量。
- 此层必须封装错误码、返回值校验和资源释放。

#### 4.2.6 Infrastructure.Persistence

- `JsonWorkspaceRepository`：工作区 JSON 读写。
- `JsonSettingsRepository`：用户设置读写。
- `AtomicFileWriter`：临时文件 + 原子替换。
- 处理 schema 版本、迁移和损坏文件恢复。

#### 4.2.7 Infrastructure.Logging

- 结构化日志。
- 按日期或文件大小滚动。
- 记录操作 ID、目标窗口标识、Win32 API、返回值和错误码。
- 不默认记录用户文档内容。

### 4.3 核心接口

```csharp
public interface IWindowDiscoveryService
{
    Task<IReadOnlyList<ExternalWindowInfo>> EnumerateAsync(
        CancellationToken cancellationToken = default);
}

public interface IWindowEmbeddingService
{
    Task<EmbeddingResult> EmbedAsync(
        nint hwnd,
        nint hostHwnd,
        CancellationToken cancellationToken = default);

    Task<EmbeddingResult> DetachAsync(
        nint hwnd,
        CancellationToken cancellationToken = default);

    Task<bool> ResizeAsync(
        nint hwnd,
        int width,
        int height,
        CancellationToken cancellationToken = default);
}

public interface IWorkspaceRepository
{
    Task<IReadOnlyList<WorkspaceSummary>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Workspace?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task SaveAsync(
        Workspace workspace,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
```

> 上述接口是建议基线。实际实现应统一异步约定，并对纯同步 Win32 操作采用明确的线程策略；不得为了形式而把所有 API 强行包装成无意义的异步方法。

### 4.4 线程模型

- WPF UI 仅在 Dispatcher 线程更新绑定属性和视觉树。
- Win32 窗口操作默认由专用 `WindowHostDispatcher` 串行执行，避免同一 HWND 的并发状态变更。
- 事件回调中只采集最小信息并投递消息，不在系统回调中执行复杂 UI 操作。
- 文件 I/O 使用异步 API。
- 不允许多个线程同时对同一 HWND 执行嵌入、解除嵌入或尺寸调整。

---

## 5. 完整数据模型与持久化设计

### 5.1 数据存储方案

MVP 使用 JSON 文件，不建立数据库表。为满足可维护性，本节定义逻辑实体、字段类型、约束和索引策略。若未来迁移 SQLite，可据此转换为表结构。

默认目录：

```text
%LocalAppData%\FlexCanvas\
├── settings.json
├── workspaces\
│   ├── {workspace-id}.json
│   └── ...
├── logs\
│   └── flexcanvas-.log
└── backups\
```

### 5.2 实体一：Workspace

| 字段 | 类型 | 必填 | 约束 / 说明 |
|---|---|---:|---|
| schemaVersion | int | 是 | JSON 模型版本 |
| id | Guid | 是 | 工作区唯一 ID |
| name | string | 是 | 1–80 字符，同名允许但 UI 应提示 |
| description | string? | 否 | 最长 500 字符 |
| createdAtUtc | DateTimeOffset | 是 | UTC 创建时间 |
| updatedAtUtc | DateTimeOffset | 是 | UTC 更新时间 |
| canvas | CanvasSettings | 是 | 画布设置 |
| containers | List<WindowContainerSnapshot> | 是 | 窗口容器列表 |
| tags | List<string> | 否 | 可选标签 |
| lastOpenedAtUtc | DateTimeOffset? | 否 | 最近打开时间 |

### 5.3 实体二：CanvasSettings

| 字段 | 类型 | 必填 | 约束 / 说明 |
|---|---|---:|---|
| zoom | double | 是 | 0.5–2.0 |
| panX | double | 是 | 画布平移 X，DIP |
| panY | double | 是 | 画布平移 Y，DIP |
| backgroundTheme | string | 是 | `Light` / `Dark` / `System` |
| snapEnabled | bool | 是 | 是否启用磁吸 |
| snapThresholdDip | double | 是 | 默认 12，建议范围 4–40 |
| showGrid | bool | 是 | 是否显示辅助网格 |

### 5.4 实体三：WindowContainerSnapshot

| 字段 | 类型 | 必填 | 约束 / 说明 |
|---|---|---:|---|
| id | Guid | 是 | 容器持久化 ID |
| displayName | string | 是 | UI 显示名称 |
| identity | WindowIdentity | 是 | 用于重新识别应用，不含持久化 HWND |
| x | double | 是 | 画布坐标 X，DIP |
| y | double | 是 | 画布坐标 Y，DIP |
| width | double | 是 | 容器宽度，最小 240 DIP |
| height | double | 是 | 容器高度，最小 160 DIP |
| zIndex | int | 是 | 容器层级 |
| isPinned | bool | 是 | 是否固定容器位置 |
| groupId | Guid? | 否 | 预留组合功能 |
| lastKnownTitle | string? | 否 | 最近一次窗口标题；注意隐私 |
| restorePolicy | string | 是 | `MatchExistingOnly` / `AskBeforeLaunch` |
| preferredMonitor | string? | 否 | 预留显示器识别信息 |

### 5.5 实体四：WindowIdentity

| 字段 | 类型 | 必填 | 约束 / 说明 |
|---|---|---:|---|
| processName | string | 是 | 进程名，不带路径或带路径需规范化 |
| executablePath | string? | 否 | 可选路径；保存前应告知用户 |
| appUserModelId | string? | 否 | 可获取时保存 |
| windowClassName | string? | 否 | Win32 窗口类名 |
| titlePattern | string? | 否 | 可选标题匹配模式，默认不作为唯一键 |
| publisher | string? | 否 | 可选发布者信息 |
| matchingStrategy | string | 是 | `ProcessAndClass` / `ExecutablePath` / `Manual` |

### 5.6 实体五：UserSettings

| 字段 | 类型 | 必填 | 说明 |
|---|---|---:|---|
| schemaVersion | int | 是 | 设置模型版本 |
| theme | string | 是 | `Light` / `Dark` / `System` |
| defaultSnapEnabled | bool | 是 | 默认磁吸状态 |
| defaultSnapThresholdDip | double | 是 | 默认吸附距离 |
| defaultZoom | double | 是 | 默认缩放 |
| closeBehavior | string | 是 | `DetachAll` / `Prompt` |
| startWithWindows | bool | 是 | MVP 固定 false，后续再实现 |
| logLevel | string | 是 | `Debug` / `Information` / `Warning` / `Error` |
| telemetryEnabled | bool | 是 | MVP 固定 false，不发送遥测 |

### 5.7 运行期实体：ExternalWindowInfo

该实体仅用于运行期，不作为持久化数据直接保存。

| 字段 | 类型 | 说明 |
|---|---|---|
| Hwnd | nint | 运行期窗口句柄 |
| ProcessId | uint | 进程 ID |
| ProcessName | string | 进程名 |
| Title | string | 当前标题 |
| ClassName | string? | 窗口类名 |
| Bounds | RectInt | 屏幕物理像素矩形 |
| IsVisible | bool | 是否可见 |
| IsElevated | bool? | 是否检测到更高权限 |
| IsOwnedWindow | bool | 是否为拥有者窗口 |
| CanAttemptEmbedding | bool | 初步筛选结果，不代表保证成功 |

### 5.8 运行期实体：EmbeddingSession

| 字段 | 类型 | 说明 |
|---|---|---|
| SessionId | Guid | 本次承载会话 ID |
| Hwnd | nint | 目标窗口句柄 |
| HostHwnd | nint | 承载宿主 HWND |
| State | EmbeddingState | 当前状态 |
| OriginalParent | nint | 原始父窗口 |
| OriginalStyle | nint | 原始窗口样式 |
| OriginalExtendedStyle | nint | 原始扩展样式 |
| OriginalBounds | RectInt | 原始窗口矩形 |
| ProcessId | uint | 目标进程 ID |
| StartedAtUtc | DateTimeOffset | 开始时间 |
| LastErrorCode | int? | 最近一次 Win32 错误码 |

### 5.9 索引与约束策略

JSON 文件没有数据库索引，因此：

- 启动时加载工作区索引摘要到内存。
- `Workspace.id` 唯一。
- 按 `updatedAtUtc` 排序工作区列表。
- 容器 `id` 在单个工作区内唯一。
- `groupId` 当前可为空，后续实现组合实体时校验引用完整性。
- 保存前校验字段范围和 schemaVersion。
- 每个工作区单独一个文件，避免单文件过大。
- 写入使用临时文件并原子替换；旧文件可保留一份有限数量备份。

---

## 6. 全局统一规范

### 6.1 请求前缀与返回格式

本项目是本地 WPF 桌面应用，**MVP 不提供 HTTP REST API**，因此不设置 `/api/v1` 请求前缀，也不定义 HTTP 统一返回包装。业务层通过 C# 接口和命令调用。

所有跨模块业务操作应使用统一结果类型，避免到处抛出未处理异常：

```csharp
public sealed record OperationResult(
    bool Success,
    string Code,
    string Message,
    string? CorrelationId = null);

public sealed record OperationResult<T>(
    bool Success,
    string Code,
    string Message,
    T? Data = default,
    string? CorrelationId = null);
```

约定：

- `Success` 表示业务操作是否完成。
- `Code` 使用稳定、可检索的业务码。
- `Message` 用于日志与 UI 展示；用户界面应提供更友好的本地化文本。
- `CorrelationId` 用于关联一次操作的日志。
- 不向 UI 返回原始异常堆栈。
- 需要详细排错时，在日志中记录异常和 Win32 错误码。

### 6.2 业务状态码

| Code | 含义 |
|---|---|
| `OK` | 操作成功 |
| `INVALID_ARGUMENT` | 参数不合法 |
| `WINDOW_NOT_FOUND` | 目标窗口不存在或已销毁 |
| `WINDOW_NOT_VISIBLE` | 目标窗口当前不可见 |
| `WINDOW_NOT_SUPPORTED` | 目标窗口不满足当前兼容性条件 |
| `EMBEDDING_FAILED` | 窗口嵌入失败 |
| `DETACH_FAILED` | 解除嵌入失败 |
| `ACCESS_DENIED` | 权限不足或访问被拒绝 |
| `DPI_MISMATCH` | 检测到 DPI 兼容性风险 |
| `WORKSPACE_NOT_FOUND` | 工作区不存在 |
| `WORKSPACE_INVALID` | 工作区文件格式或内容无效 |
| `PERSISTENCE_FAILED` | 本地文件读写失败 |
| `OPERATION_CANCELLED` | 操作被取消 |
| `INTERNAL_ERROR` | 未分类内部错误 |

### 6.3 异常处理

- UI 命令入口必须捕获可预期异常并转换为 `OperationResult`。
- P/Invoke 层应立即保存 Win32 错误码，避免后续调用覆盖错误状态。
- 不允许使用空 `catch`。
- 不允许仅记录异常而让 UI 保持错误状态。
- 进程级未处理异常必须记录日志并显示错误提示；是否继续运行需根据异常类型决定。
- 解除嵌入失败时，保留会话状态和诊断信息，提供重试或安全退出路径。
- 文件 JSON 解析失败时，保留原文件并提示用户，尝试从备份恢复。

### 6.4 日志规范

日志级别：

- `Trace`：高频诊断，仅开发版使用。
- `Debug`：窗口发现、状态转换、布局计算摘要。
- `Information`：启动、工作区保存/加载、嵌入/解除嵌入成功。
- `Warning`：兼容性风险、重试、可恢复错误。
- `Error`：操作失败、文件损坏、Win32 API 失败。
- `Critical`：进程即将无法继续运行的严重错误。

日志字段建议：

- `TimestampUtc`
- `Level`
- `EventName`
- `CorrelationId`
- `ProcessId`
- `Hwnd`（仅运行期诊断）
- `Win32Api`
- `Win32ErrorCode`
- `EmbeddingState`
- `Message`

禁止记录：

- 密码、认证令牌。
- 不必要的文档全文。
- 用户键入内容。
- 与排错无关的完整文件路径。

日志目录：`%LocalAppData%\FlexCanvas\logs\`。默认保留 7–14 天，可配置。

---

## 7. 全部业务接口设计

> 这里的“接口”是本地应用内部的服务接口与业务用例，不是 HTTP API。接口名称和职责需稳定，具体实现可按目录结构拆分。

### 7.1 窗口发现接口

#### `IWindowDiscoveryService.EnumerateAsync`

**用途：** 获取当前可供用户选择的外部窗口。

**输入：**
- `CancellationToken cancellationToken`

**输出：**
- `IReadOnlyList<ExternalWindowInfo>`

**业务逻辑：**
1. 枚举桌面顶层窗口。
2. 排除 FlexCanvas 自身窗口。
3. 检查可见性、标题、进程状态和窗口类型。
4. 读取窗口类名、进程名、矩形和权限线索。
5. 返回候选列表。
6. 不将“可尝试嵌入”误认为“保证兼容”。

**错误：**
- 枚举失败：记录日志并返回可解释错误。
- 目标进程已退出：忽略该候选项并记录 Debug 日志。

### 7.2 窗口兼容性检查接口

#### `IWindowCompatibilityService.CheckAsync`

**输入：**
- `ExternalWindowInfo window`
- `CancellationToken cancellationToken`

**输出：**
- `CompatibilityReport`

**报告字段：**
- `CanAttempt`
- `RiskLevel`：Low / Medium / High / Unknown
- `Reasons`
- `Warnings`
- `RecommendedFallback`

**业务逻辑：**
1. 检查窗口是否仍然存在。
2. 检查目标进程是否可访问。
3. 检查是否可能存在权限等级不匹配。
4. 检查目标窗口样式与类型。
5. 生成风险提示。
6. 不做“所有应用都能嵌入”的保证。

### 7.3 窗口嵌入接口

#### `IWindowEmbeddingService.EmbedAsync`

**输入：**
- `nint hwnd`
- `nint hostHwnd`
- `CancellationToken cancellationToken`

**输出：**
- `EmbeddingResult`

**业务逻辑：**
1. 验证两个 HWND 仍然有效。
2. 确认目标不是 FlexCanvas 自身窗口。
3. 获取目标窗口原始状态并创建会话快照。
4. 校验权限、DPI 与窗口样式风险。
5. 执行嵌入所需的样式与父子关系变更。
6. 设置目标窗口的位置与尺寸。
7. 验证目标窗口可见且能响应基础输入。
8. 成功则更新会话状态为 `Embedded`。
9. 失败则尝试回滚原始状态；回滚失败必须单独记录。

**输出字段：**
- `Success`
- `Code`
- `Message`
- `SessionId`
- `Warnings`
- `Win32ErrorCode`

### 7.4 解除嵌入接口

#### `IWindowEmbeddingService.DetachAsync`

**输入：**
- `nint hwnd`
- `CancellationToken cancellationToken`

**业务逻辑：**
1. 查找对应 `EmbeddingSession`。
2. 验证目标窗口是否仍存在。
3. 将窗口恢复到可用的顶层窗口形态。
4. 恢复原始样式与合理的窗口矩形。
5. 重新显示窗口并验证状态。
6. 结束会话并释放监控资源。
7. 若目标窗口已退出，清理会话即可，不再执行位置恢复。

**幂等要求：**
- 已解除嵌入的窗口再次调用，应返回成功或 `WINDOW_NOT_FOUND`，不得破坏其他窗口。

### 7.5 窗口尺寸同步接口

#### `IWindowEmbeddingService.ResizeAsync`

**输入：**
- `nint hwnd`
- `int width`
- `int height`

**业务逻辑：**
- 检查尺寸大于零并符合应用最小尺寸策略。
- 将尺寸转换为目标 HWND 所需的物理像素。
- 使用 Win32 窗口定位 API 更新目标窗口。
- 记录失败错误码。
- 不在拖动过程高频写日志；成功操作仅记录 Debug 摘要。

### 7.6 窗口事件接口

#### `IWindowEventMonitor`

事件：

- `WindowDestroyed`
- `ForegroundWindowChanged`
- `WindowLocationChanged`
- `WindowTitleChanged`（可选）
- `WindowProcessExited`

要求：

- 注册和解除监听必须成对出现。
- 事件处理不能直接操作 WPF 视觉树。
- 将事件投递到应用层后，由 UI Dispatcher 更新状态。
- 事件可能重复或延迟，消费者必须校验 HWND 是否仍然有效。

### 7.7 画布布局接口

#### `ILayoutEngine`

方法：

- `MoveContainer(containerId, x, y)`
- `ResizeContainer(containerId, width, height)`
- `BringToFront(containerId)`
- `SendToBack(containerId)`
- `ValidateLayout(layout)`
- `ApplySnap(layout, activeContainerId, thresholdDip)`

规则：

- 所有坐标均为画布 DIP 坐标。
- 不直接调用 Win32。
- 不依赖 WPF `FrameworkElement`。
- 对负坐标、极端尺寸和 NaN/Infinity 进行校验。
- 返回新的布局状态或明确的校验错误。

### 7.8 工作区接口

#### `IWorkspaceService`

| 方法 | 输入 | 输出 | 业务逻辑 |
|---|---|---|---|
| `GetAllAsync` | 无 | 工作区摘要列表 | 读取索引或扫描工作区文件 |
| `CreateAsync` | 名称、描述 | Workspace | 生成 Guid 与时间戳 |
| `SaveAsync` | Workspace | OperationResult | 校验、更新版本和时间、原子写入 |
| `LoadAsync` | Workspace ID | Workspace | 读取并校验 schemaVersion |
| `RenameAsync` | ID、名称 | OperationResult | 校验名称并保存 |
| `DeleteAsync` | ID | OperationResult | 删除对应文件并更新索引 |
| `RestoreLayoutAsync` | ID | RestoreReport | 恢复画布布局并匹配当前窗口 |
| `ExportAsync` | ID、目标路径 | OperationResult | 导出 JSON，校验目标路径 |
| `ImportAsync` | 文件路径 | OperationResult | 校验文件，生成新 ID 或确认覆盖 |

### 7.9 设置接口

#### `ISettingsService`

- `GetAsync()`
- `UpdateAsync(UserSettings settings)`
- `ResetAsync()`

必须校验主题、缩放、吸附阈值和关闭行为等字段，忽略未知字段时应保持 schema 兼容策略一致。

### 7.10 主要业务错误处理矩阵

| 操作 | 失败情形 | 处理 |
|---|---|---|
| 枚举窗口 | 目标进程退出 | 丢弃候选项 |
| 嵌入窗口 | HWND 无效 | 返回 `WINDOW_NOT_FOUND` |
| 嵌入窗口 | 权限不匹配 | 返回 `ACCESS_DENIED` 并建议独立窗口模式 |
| 嵌入窗口 | 样式变更失败 | 回滚并返回 `EMBEDDING_FAILED` |
| 解除嵌入 | 目标已退出 | 清理会话并返回可恢复结果 |
| 调整尺寸 | 尺寸非法 | 返回 `INVALID_ARGUMENT` |
| 保存工作区 | 路径不可写 | 返回 `PERSISTENCE_FAILED` |
| 加载工作区 | JSON 损坏 | 返回 `WORKSPACE_INVALID` 并保留原文件 |
| 恢复工作区 | 部分应用窗口未找到 | 部分恢复并生成报告 |

---

## 8. 项目完整目录结构

```text
FlexCanvas/
├── FlexCanvas.sln
├── Directory.Build.props
├── Directory.Packages.props
├── global.json
├── README.md
├── LICENSE
├── docs/
│   ├── PROJECT_SPEC.md
│   ├── ARCHITECTURE.md
│   ├── POC_CHECKLIST.md
│   ├── COMPATIBILITY_MATRIX.md
│   └── ADR/
│       ├── ADR-001-technology-stack.md
│       ├── ADR-002-window-embedding.md
│       └── ADR-003-workspace-storage.md
├── src/
│   ├── FlexCanvas.App/
│   │   ├── App.xaml
│   │   ├── App.xaml.cs
│   │   ├── appsettings.json
│   │   ├── FlexCanvas.App.csproj
│   │   ├── Bootstrap/
│   │   │   ├── DependencyInjection.cs
│   │   │   └── ApplicationStartup.cs
│   │   ├── Views/
│   │   │   ├── MainWindow.xaml
│   │   │   ├── CanvasView.xaml
│   │   │   ├── WindowPickerView.xaml
│   │   │   ├── WorkspacePanelView.xaml
│   │   │   └── WindowContainerView.xaml
│   │   ├── ViewModels/
│   │   │   ├── MainWindowViewModel.cs
│   │   │   ├── CanvasViewModel.cs
│   │   │   ├── WindowManagerViewModel.cs
│   │   │   ├── WindowPickerViewModel.cs
│   │   │   └── WorkspacePanelViewModel.cs
│   │   ├── Converters/
│   │   ├── Behaviors/
│   │   ├── Resources/
│   │   │   ├── Themes/
│   │   │   ├── Styles/
│   │   │   └── Icons/
│   │   └── Services/
│   │       └── UiDispatcher.cs
│   ├── FlexCanvas.Domain/
│   │   ├── FlexCanvas.Domain.csproj
│   │   ├── Models/
│   │   │   ├── Workspace.cs
│   │   │   ├── CanvasSettings.cs
│   │   │   ├── WindowContainerSnapshot.cs
│   │   │   ├── WindowIdentity.cs
│   │   │   ├── WindowContainerModel.cs
│   │   │   └── LayoutSnapshot.cs
│   │   ├── Enums/
│   │   │   ├── EmbeddingState.cs
│   │   │   ├── CompatibilityRiskLevel.cs
│   │   │   └── RestorePolicy.cs
│   │   ├── Results/
│   │   │   ├── OperationResult.cs
│   │   │   └── EmbeddingResult.cs
│   │   ├── Layout/
│   │   │   ├── ILayoutEngine.cs
│   │   │   ├── LayoutEngine.cs
│   │   │   ├── SnapEngine.cs
│   │   │   └── LayoutValidator.cs
│   │   └── Validation/
│   │       └── WorkspaceValidator.cs
│   ├── FlexCanvas.Application/
│   │   ├── FlexCanvas.Application.csproj
│   │   ├── Interfaces/
│   │   │   ├── IWindowDiscoveryService.cs
│   │   │   ├── IWindowEmbeddingService.cs
│   │   │   ├── IWindowCompatibilityService.cs
│   │   │   ├── IWindowEventMonitor.cs
│   │   │   ├── IWorkspaceRepository.cs
│   │   │   ├── IWorkspaceService.cs
│   │   │   └── ISettingsService.cs
│   │   ├── Services/
│   │   │   ├── WindowEmbeddingService.cs
│   │   │   ├── WindowCompatibilityService.cs
│   │   │   ├── WorkspaceService.cs
│   │   │   └── SettingsService.cs
│   │   └── DTOs/
│   │       ├── ExternalWindowInfo.cs
│   │       ├── CompatibilityReport.cs
│   │       └── RestoreReport.cs
│   └── FlexCanvas.Infrastructure/
│       ├── FlexCanvas.Infrastructure.csproj
│       ├── Win32/
│       │   ├── NativeMethods.cs
│       │   ├── NativeStructs.cs
│       │   ├── NativeConstants.cs
│       │   ├── Win32WindowEnumerator.cs
│       │   ├── Win32WindowInfoReader.cs
│       │   ├── Win32WindowEmbedder.cs
│       │   ├── Win32WindowPositioner.cs
│       │   ├── Win32WindowEventMonitor.cs
│       │   ├── Win32DpiHelper.cs
│       │   └── WindowHostDispatcher.cs
│       ├── Persistence/
│       │   ├── JsonWorkspaceRepository.cs
│       │   ├── JsonSettingsRepository.cs
│       │   ├── AtomicFileWriter.cs
│       │   └── SchemaMigrator.cs
│       └── Logging/
│           └── LoggingConfiguration.cs
├── tests/
│   ├── FlexCanvas.Domain.Tests/
│   │   ├── LayoutEngineTests.cs
│   │   ├── SnapEngineTests.cs
│   │   └── WorkspaceValidatorTests.cs
│   ├── FlexCanvas.Application.Tests/
│   │   ├── WorkspaceServiceTests.cs
│   │   └── WindowEmbeddingServiceTests.cs
│   ├── FlexCanvas.Infrastructure.Tests/
│   │   ├── JsonWorkspaceRepositoryTests.cs
│   │   └── Win32InteropSmokeTests.cs
│   └── FlexCanvas.UiTests/
│       └── README.md
└── tools/
    └── WindowHostProbe/
        ├── WindowHostProbe.csproj
        └── MainWindow.xaml
```

### 8.1 项目引用方向

- `FlexCanvas.Domain` 不引用其他项目。
- `FlexCanvas.Application` 引用 `FlexCanvas.Domain`。
- `FlexCanvas.Infrastructure` 引用 `FlexCanvas.Domain` 与 `FlexCanvas.Application`。
- `FlexCanvas.App` 引用其他三个项目。
- 测试项目只引用被测项目。
- 不允许 Domain 引用 WPF、Win32 或 Infrastructure。

---

## 9. 业务流程与核心逻辑

### 9.1 启动流程

1. 初始化日志和全局异常处理。
2. 读取 `settings.json`；文件缺失则生成默认设置。
3. 检查工作区目录并加载工作区摘要。
4. 创建主窗口和画布 ViewModel。
5. 初始化窗口事件监控。
6. 展示主界面。
7. 不在启动时自动嵌入任意外部窗口。

### 9.2 添加外部窗口流程

1. 用户点击“添加窗口”。
2. 调用窗口发现服务枚举候选窗口。
3. 用户选择目标窗口。
4. 调用兼容性检查服务。
5. 显示风险等级和警告。
6. 用户确认后创建容器模型。
7. Native Window Host 保存目标窗口原始状态。
8. 执行嵌入操作。
9. 设置容器客户区大小并显示目标窗口。
10. 执行基础交互验证或提示用户验证。
11. 成功后将容器状态设置为 `Embedded`。
12. 失败时尝试回滚，并提供独立窗口模式选项。

### 9.3 拖动窗口容器流程

1. 用户按下容器标题栏。
2. 记录鼠标起始位置与容器起始坐标。
3. 捕获拖动输入，进入拖动状态。
4. 按画布坐标系计算新位置。
5. 调用布局引擎执行移动和吸附计算。
6. 更新容器模型。
7. 将新位置同步到宿主/嵌入窗口。
8. 鼠标释放后结束拖动并保存内存中的布局状态。
9. 用户显式保存工作区时才写入磁盘；可选自动保存作为后续功能。

### 9.4 调整容器大小流程

1. 用户拖动容器边缘或角点。
2. 根据鼠标位移计算新宽高。
3. 应用最小尺寸与合法性校验。
4. 更新 WPF 容器客户区。
5. 将尺寸同步到目标 HWND。
6. 如目标应用拒绝或自行调整尺寸，重新读取实际窗口矩形并协调容器状态。
7. 拖动结束后更新布局快照。

### 9.5 保存工作区流程

1. 用户点击保存。
2. 汇总画布设置和所有容器的布局。
3. 生成工作区模型。
4. 校验 ID、坐标、尺寸、枚举值和 schemaVersion。
5. 序列化为 JSON。
6. 写入临时文件并刷新。
7. 原子替换正式文件。
8. 更新内存工作区索引。
9. 显示保存成功或错误提示。

### 9.6 恢复工作区流程

1. 用户选择工作区。
2. 读取并校验文件。
3. 恢复画布设置与容器布局。
4. 枚举当前外部窗口。
5. 按匹配策略尝试匹配已有窗口。
6. 对每个匹配窗口逐一提示或按用户设置执行嵌入。
7. 对未匹配窗口生成恢复报告。
8. 不自动执行未受信任的命令行。
9. 展示成功、跳过、失败和未找到数量。

### 9.7 窗口异常退出流程

1. 监控器收到窗口销毁或进程退出事件。
2. 通过 HWND 和 PID 二次校验，避免句柄复用误判。
3. 将关联容器标记为 `TargetExited`。
4. 解除相关事件监听并清理运行期会话。
5. 保留工作区中的布局快照。
6. 显示“应用已关闭，可重新添加窗口”的状态。
7. 不自动重启应用，除非后续版本提供明确授权的恢复策略。

### 9.8 解除嵌入与退出流程

- 单个窗口解除嵌入：恢复为可用顶层窗口，清理会话。
- 关闭 FlexCanvas：按设置选择逐个解除所有嵌入窗口或弹出确认。
- 任何退出路径都必须执行最佳努力恢复。
- 不应直接调用 `Process.Kill()` 作为默认清理手段。
- 若恢复失败，日志记录原始状态、操作阶段和 Win32 错误码，并提示用户检查外部应用窗口。

### 9.9 核心状态机

```text
Discovered
    │
    ▼
Validating ──失败──> Failed
    │
    ▼
Embedding ───失败──> RollingBack ──> Failed
    │
    ▼
Embedded
    │
    ├──目标退出──> TargetExited
    │
    ▼
Detaching ───失败──> DetachFailed
    │
    ▼
Detached
```

状态转换应集中管理，不允许 ViewModel 随意赋值绕过状态机校验。

---

## 10. 开发规范、命名规范与代码约束

### 10.1 C# 规范

- 使用 nullable reference types。
- 使用 `async` / `await` 处理文件 I/O 与真正异步的业务流程。
- 公共 API 必须有 XML 文档注释。
- 不使用 `.Result` 或 `.Wait()` 阻塞 UI 线程。
- 避免无意义的静态全局状态。
- 使用依赖注入，不在 ViewModel 中直接 `new` 复杂 Infrastructure 服务。
- 枚举值和配置项必须显式定义。
- 对外部输入执行验证。
- 对 P/Invoke 返回值和 HWND 有效性进行检查。
- 禁止空 `catch` 和忽略错误码。

### 10.2 命名规范

| 类型 | 规则 | 示例 |
|---|---|---|
| 类、接口 | PascalCase | `WindowEmbeddingService` |
| 接口 | `I` + PascalCase | `IWorkspaceService` |
| 方法 | PascalCase | `DetachAsync` |
| 参数、局部变量 | camelCase | `workspaceId` |
| 私有字段 | `_camelCase` | `_windowHost` |
| 常量 | PascalCase | `DefaultSnapThresholdDip` |
| 枚举 | PascalCase | `EmbeddingState.Embedded` |
| XAML 控件 | PascalCase | `CanvasRoot` |
| 文件名 | 与主类型一致 | `WorkspaceService.cs` |

### 10.3 分层约束

- View 不直接调用 Win32。
- ViewModel 不直接声明 P/Invoke。
- Domain 不引用 WPF、Win32、文件系统或日志框架。
- Infrastructure 不直接更新 UI。
- 工作区持久化 DTO 不得直接依赖运行期 HWND。
- 布局算法不得依赖视觉树测量结果。
- 任何新模块必须明确所属层级与依赖方向。

### 10.4 AI 编码约束

将本说明书交给 Cursor 后，应要求其遵守：

1. 先阅读整个说明书与现有代码，再修改。
2. 每次只实现一个可验证的垂直切片。
3. 先建立解决方案、项目引用和编译通过的空壳。
4. 再实现 POC，不得先生成完整画布后才验证窗口嵌入。
5. 不得虚构 Windows API，不得把示例代码当作已验证实现。
6. P/Invoke 声明需核对参数宽度、调用约定、结构体布局和 32/64 位行为。
7. 每个功能必须包含测试或明确的人工验证步骤。
8. 修改完成后运行 `dotnet build` 和 `dotnet test`，并报告实际结果。
9. 不得擅自引入 Web API、云服务、账号系统或数据库。
10. 不得用截图、DWM Thumbnail、模拟窗口替代真实 HWND 嵌入。
11. 若发现技术前提不成立，先输出证据、影响范围和替代方案，不得用假实现掩盖问题。
12. 每个阶段提交前更新 `docs/COMPATIBILITY_MATRIX.md` 与相关 ADR。

### 10.5 Git 提交建议

- `feat:` 新功能
- `fix:` 缺陷修复
- `refactor:` 重构
- `test:` 测试
- `docs:` 文档
- `build:` 构建与依赖

示例：`feat(window-host): add initial HWND embedding POC`

---

## 11. 测试方案

### 11.1 测试策略

采用分层测试：

1. Domain 单元测试。
2. Application 服务测试。
3. Infrastructure 持久化测试。
4. Win32 集成测试。
5. WPF UI 手工与自动化测试。
6. 目标应用兼容性矩阵测试。
7. 异常恢复与稳定性测试。

### 11.2 POC 必须先于完整功能

**POC-1：基础 HWND 嵌入**

- 创建独立的 `WindowHostProbe` 测试程序。
- 验证目标窗口嵌入、移动、缩放和解除嵌入。
- 验证鼠标点击、键盘输入、滚动、文本选择。
- 验证目标应用关闭时的清理。
- 验证嵌入失败后是否能恢复窗口。

**POC-2：交互完整性**

至少验证：

- 普通按钮和菜单。
- 右键菜单。
- ComboBox 下拉菜单。
- 拖动滑块和滚动条。
- Ctrl+C / Ctrl+V 等常见快捷键。
- 输入法（IME）与中英文切换。
- 文件拖放（若列为 MVP 支持项）。
- 弹窗、对话框和子窗口。
- 焦点切换与 Alt+Tab 行为。

**POC-3：DPI 与多显示器**

- 100%、125%、150%、200% 缩放。
- 不同 DPI 的双显示器。
- 主窗口跨显示器移动。
- 分辨率变化。
- 睡眠/唤醒后恢复。
- 远程桌面连接/断开（可作为后续兼容性测试）。

**POC-4：崩溃与权限**

- 目标应用在嵌入时崩溃。
- FlexCanvas 在嵌入时关闭。
- 目标应用以管理员权限运行而 FlexCanvas 未提权。
- FlexCanvas 以管理员权限运行而目标应用未提权。
- HWND 销毁并被系统复用。
- 多次嵌入/解除嵌入循环。

### 11.3 目标应用测试矩阵

| 应用类别 | 首批测试目标 | 关注点 |
|---|---|---|
| Win32 基础窗口 | WindowHostProbe | 基线嵌入与回滚 |
| Windows 自带文本编辑器 | 记事本 | 文本输入、菜单、弹窗 |
| Win32 文件工具 | 文件资源管理器（按风险测试） | 多窗口、导航、对话框 |
| WPF 应用 | 自制 WPF Probe | WPF 子窗口、DPI、焦点 |
| WinForms 应用 | 自制 WinForms Probe | 控件交互 |
| Chromium/Electron | 浏览器、VS Code 等 | 多进程、弹窗、GPU 合成、菜单 |
| Qt 应用 | 典型 Qt 桌面应用 | 菜单、输入法、DPI |
| UWP/WinUI | 单独评估 | 窗口模型与承载限制 |
| 管理员应用 | 单独评估 | UIPI 与权限边界 |

兼容性表中每个应用应记录 Windows 版本、应用版本、权限级别、DPI 配置、成功/失败项、已知问题与复现步骤。

### 11.4 单元测试清单

- 吸附距离阈值计算。
- 水平/垂直边缘对齐。
- 容器最小宽高约束。
- 负坐标与极端坐标校验。
- 非法 NaN / Infinity 拒绝。
- 工作区 JSON 序列化与反序列化。
- schemaVersion 不兼容处理。
- 损坏 JSON 的恢复路径。
- 工作区 ID 唯一性。
- 部分恢复报告生成。
- 嵌入状态机非法转换拒绝。

### 11.5 验收标准

MVP 至少满足：

- 解决方案可在干净 Windows 环境构建。
- 所有自动化测试通过。
- POC 明确记录支持的窗口类别与限制。
- 对已列入支持清单的目标应用，基础鼠标和键盘交互正常。
- 嵌入失败后能回滚，或给出明确的不可恢复提示。
- 拖动与缩放不会持续引发 UI 卡死。
- 工作区可以保存、重新打开和部分恢复。
- 目标进程退出后 UI 状态能够收敛，不留下永久僵尸容器。
- 关闭 FlexCanvas 不会默认强制结束外部应用。
- 日志可用于复现并定位常见嵌入失败。

---

## 12. 部署与运行说明

### 12.1 开发环境

- Windows 10 22H2 或 Windows 11 x64。
- Visual Studio 2022 或支持 .NET 8 的 IDE。
- .NET 8 SDK。
- Git。
- 可选：Windows SDK、PowerShell 7。
- 目标应用测试环境：记事本、一个自制 Win32/WPF/WinForms Probe 程序。

### 12.2 初始化

```powershell
git clone <repository-url>
cd FlexCanvas
dotnet restore
dotnet build
dotnet test
dotnet run --project .\src\FlexCanvas.App\FlexCanvas.App.csproj
```

将 `<repository-url>` 替换为实际仓库地址。

### 12.3 发布

```powershell
dotnet publish .\src\FlexCanvas.App\FlexCanvas.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true
```

发布后检查 `bin\Release\net8.0-windows\win-x64\publish\` 下产物。实际路径以项目配置为准。

### 12.4 用户数据

默认目录：

- 设置：`%LocalAppData%\FlexCanvas\settings.json`
- 工作区：`%LocalAppData%\FlexCanvas\workspaces\`
- 日志：`%LocalAppData%\FlexCanvas\logs\`
- 备份：`%LocalAppData%\FlexCanvas\backups\`

卸载应用时，默认保留用户数据；应在卸载说明中告知用户如何手动删除。后续可提供“卸载时同时删除本地数据”的显式选项。

### 12.5 权限原则

- 默认以普通用户权限运行。
- 不建议为方便嵌入而默认以管理员身份运行。
- 当目标应用权限高于 FlexCanvas 时，提示用户可能无法控制或嵌入该窗口。
- 不通过自动提权或绕过安全机制处理权限问题。

### 12.6 故障排查

| 现象 | 排查步骤 |
|---|---|
| 窗口列表为空 | 检查目标窗口是否可见、是否被过滤、进程是否存在 |
| 嵌入失败 | 查看日志中的 Win32 API、错误码、窗口样式与权限信息 |
| 窗口显示但无法输入 | 检查焦点、窗口样式、子窗口/弹窗行为，记录兼容性缺陷 |
| DPI 下尺寸异常 | 检查进程 DPI awareness、DIP/物理像素换算和显示器缩放 |
| 弹窗出现在主窗口外 | 记录为应用兼容性问题；不要假设所有 owned window 会自动跟随 |
| 解除嵌入后窗口位置异常 | 检查原始窗口矩形保存、坐标转换与恢复顺序 |
| 工作区损坏 | 保留原文件，尝试备份恢复，导出日志后提交缺陷 |
| UI 卡死 | 检查 UI 线程是否执行阻塞调用、是否存在 Dispatcher 同步等待 |

---

## 13. 迭代版本规划

### 13.1 Phase 0：技术可行性验证（最高优先级）

**目标：** 判断跨进程 HWND 嵌入是否足以支撑产品核心体验。

交付物：

- 最小 WPF Host。
- 独立 Win32/WPF/WinForms Probe。
- 基础嵌入与解除嵌入。
- 鼠标、键盘、菜单、弹窗、输入法、DPI 测试记录。
- `COMPATIBILITY_MATRIX.md`。
- 架构决策记录 ADR-002。

退出条件：

- 至少对基线测试应用完成可重复嵌入和恢复。
- 已知限制被明确记录。
- 如果关键交互无法稳定实现，暂停完整产品开发并重新评估方案。

### 13.2 Phase 1：项目骨架与窗口发现

交付物：

- Solution 与分层项目。
- DI、日志和异常处理。
- WPF 主窗口基础 UI。
- 窗口枚举与候选列表。
- 兼容性报告初版。
- 基础单元测试与 CI 构建（可选）。

### 13.3 Phase 2：真实窗口承载 MVP

交付物：

- Native Window Host。
- 嵌入、尺寸同步、解除嵌入和回滚。
- 窗口状态机。
- 目标进程退出处理。
- 基础诊断日志。
- 已验证应用兼容性列表。

### 13.4 Phase 3：空间画布

交付物：

- 容器视图。
- 自由移动与缩放。
- 画布平移与缩放。
- 层级控制。
- 磁吸提示线与吸附算法。
- 布局单元测试。

### 13.5 Phase 4：工作区与持久化

交付物：

- JSON Repository。
- 工作区新建、保存、加载、重命名和删除。
- schemaVersion 与迁移机制。
- 布局恢复和部分恢复报告。
- 原子写入与备份恢复。

### 13.6 Phase 5：兼容性与稳定性强化

交付物：

- 多 DPI 与多显示器测试。
- 浏览器/Electron/Qt 等应用兼容性报告。
- 异常退出、权限差异和重复嵌入测试。
- 资源清理和性能优化。
- 用户可导出的诊断包。

### 13.7 Phase 6：扩展能力

候选功能：

- 容器组合与整体移动。
- 多工作区快捷切换。
- 布局模板。
- 快捷键配置。
- 自动启动辅助（必须用户显式授权）。
- 多显示器工作区。
- SQLite（仅当 JSON 方案确实不足时评估）。
- 安装程序与自动更新。

### 13.8 版本优先级

| 优先级 | 功能 | 原因 |
|---|---|---|
| P0 | HWND 嵌入 POC、交互验证、解除嵌入恢复 | 决定产品核心路线是否可行 |
| P1 | 分层骨架、窗口发现、错误处理 | 支撑后续开发 |
| P1 | 容器移动/缩放、基础吸附 | 实现核心空间交互 |
| P1 | 工作区保存与恢复 | 形成完整 MVP 闭环 |
| P2 | 兼容性诊断、日志导出 | 降低用户排错成本 |
| P2 | 组合管理、多显示器 | 提升效率 |
| P3 | 自动启动、复杂布局模板、自动更新 | 不应阻塞首版 |

---

## 附录 A：建议的开发顺序

不要让 AI 一次性生成全部项目并直接进入功能开发。按以下顺序实施：

1. 创建 Solution、项目、项目引用与构建配置。
2. 编写并运行 `WindowHostProbe`，验证最小跨进程嵌入。
3. 记录基线应用的输入、弹窗、IME、DPI 和解除嵌入结果。
4. 实现 `NativeMethods` 与 Win32 窗口信息读取。
5. 实现窗口发现列表。
6. 实现嵌入状态机和失败回滚。
7. 实现独立窗口解除嵌入及进程退出清理。
8. 接入 WPF 容器视图。
9. 实现画布布局与吸附算法。
10. 实现工作区 JSON 持久化。
11. 补充兼容性测试、稳定性测试和发布流程。

每一步都应做到“能构建、能运行、能验证”后再进入下一步。

## 附录 B：Cursor 项目级指令

将本说明书保存为仓库根目录的 `PROJECT_SPEC.md` 或 `docs/PROJECT_SPEC.md`，并在 Cursor 项目规则中添加以下指令：

- 本说明书是产品范围和架构决策的首要依据。
- 未经用户确认，不得更换 C# / .NET 8 / WPF 技术路线。
- 不得把 DWM Thumbnail、截图或视频流实现为真实窗口嵌入。
- 先完成 Phase 0 POC，再开发完整画布。
- 任何 Win32 P/Invoke 必须核对官方 API 定义、结构体布局、指针宽度、返回值与错误码。
- 不得声称对任意 Windows 应用完全兼容。
- 所有嵌入操作必须有回滚/解除嵌入路径。
- 业务逻辑、布局计算、Win32 互操作和 WPF UI 必须保持分层。
- 不引入 HTTP API、云端服务、账号系统或关系型数据库，除非后续需求明确变更。
- 每次改动后运行构建和测试，并报告未通过项与尚未验证的行为。
- 若实现与说明书存在冲突，先说明冲突、影响和备选方案，不得静默偏离。

---

## 附录 C：关键架构声明

FlexCanvas 的核心难点不是画布绘制，而是**跨进程窗口承载的兼容性与生命周期管理**。因此：

1. HWND 是运行期句柄，不是持久化应用身份。
2. `SetParent` 不会自动替应用处理所有窗口样式、DPI、焦点、输入法、弹窗和多显示器行为。
3. 图形合成技术不自动提供完整的原生键盘、鼠标、IME、菜单与无障碍交互。
4. 所谓“兼容”必须以明确的 Windows 版本、应用版本、权限和 DPI 配置为条件。
5. 如果某个应用无法稳定嵌入，应降级为独立窗口使用，不得通过假预览掩盖限制。
6. 任何架构调整都应先以小型 POC 验证，再投入完整功能开发。

**文档结束。**
