# ADR 0002：S1 只读发现与启动边界

日期：2026-10-16。依据：用户授权 S1、初稿 §6/§7/§13.2、T0103/T0301/T0302/T0401/T1201。

- 使用 Microsoft.Extensions.DependencyInjection 8.0.1 和 CommunityToolkit.Mvvm 8.4.0；Domain 不依赖框架，Application 的 ViewModel 不依赖 WPF。
- 服务返回 OperationResult<T>，落实 §6 统一结果；§7 的直接返回值示例不作为冻结签名。保留 POC 独立接口。
- 正式发现只读取当前桌面，不复用 Probe 白名单，也不调用 SetParent/SetWindowPos。排除自身、隐藏、无标题、工具/子窗口、DWM cloaked 窗口；owner/最小化保留并提示风险。
- 原生读取在专用 STA 上串行执行；显式刷新全列表，不高频扫描桌面。每 2 秒只重读选中目标，关闭/身份变化时清除选择；完整 WinEventHook 在 S2 接入。
- CanAttempt 仅为保守初筛；权限/DPI 未知阻止建议尝试。相同 elevation 不是完整的完整性级别/UIPI 验证，需后续生产检查。默认独立窗口降级，未知应用交互仍需人工验证。
- 本阶段采用小型结构化日志端口及后台滚动 JSONL 实现，暂不引入可选 Serilog。固定事件/代码/API 枚举与数值字段，不接受标题/路径/输入/任意异常消息；异常仅存 HResult。7 天、最多 8 文件、每文件 1 MiB、队列 256 项，按 UTC 日期或大小轮换；队列满或写失败可见健康状态，不冒充写入成功。设置/清理 UI 留给后续任务。
- DI 容器拥有原生 Dispatcher、日志和 ViewModel；关闭先取消/等待命令，再异步释放。StartupUri 移除；Dispatcher/未观察任务异常提示后退出，不吞异常继续工作。CLR 终止异常只尽力提交诊断，无法保证提示或队列落盘，故进程级故障注入仍待验收。设置与工作区索引在 S4 接入，不建立假服务。
- 验收：构建、结果/日志/服务测试、受控原生发现、S0 全量回归、WPF UI Smoke。系统权限差异与混合 DPI 的人工验证单列，不自动提权。
- 回退：关闭 App 停止只读服务，不修改目标窗口；保留 S0 工具。项目非 Git 仓库，不自动初始化/提交。
