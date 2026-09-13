# AtomUI.City Desktop Dogfood

本目录包含一个真实、可人工操作且可自动驾驶的 Avalonia 桌面应用。它不是展示型 sample，也不是把单元测试包进 Window；它以一个多租户商业运营工作台为业务载体，持续压测 City 的运行期基础模块。

## 当前实现基线

Dogfood 已从“目录复杂度”推进到第三阶段传输韧性验证，并具备可重复的 Quick、Standard、Headless 和 Windows GUI 门禁：

- City Host 与 Avalonia 使用独立、协作的启动和停止流程；进程完成后必须退出码为 0。
- 48 个应用 Module、110 个业务 Service、72 个 Event contract、128 个 State、96 条静态 Route、64 个 ViewModel 和 96 个 Command 具有运行时数量断言。
- 主窗口和 3 个辅助窗口均为真实 Avalonia `Window`，合计 4 个 `WindowSession`、12 个命名 Outlet，全部在 `Show` 前注册并在退出前关闭。
- Localization 装载 12 个中英文 package、480 个 key，执行 100 次初始切换和 20 次交叉负载切换。
- Security 覆盖 64 个 permission、24 个 policy、3 个持久化账号、在线/离线受限切换、token 单飞续期、权限快照续期与显式 session 刷新、删除和 process-like 重启恢复。
- Data 通过 loopback 端口执行真实 HTTP、gRPC unary/server-stream/duplex 和 SignalR 调用；13 项传输韧性门禁覆盖 503 重试、timeout、半响应、乱序、幂等 mutation、gRPC deadline/流中断、SignalR 断连/重连，以及独立服务进程崩溃后在相同端口恢复。
- 110 个 Service 被划分为 8 条跨模块工作流，形成并行分支、摘要传递、事件发布，以及 12 步失败后逆序补偿链；不再只执行统一的浅层 `Execute()`。
- 自动驾驶会遍历全部 96 条 Route 定义，实际导航并提交 76 条可导航 Route；64 个 ViewModel 全部进入真实 VisualTree，96 个 Command 全部执行。
- 自动驾驶每轮交叉执行 Router、MVVM、State、EventBus、Data 和 Localization；Quick 不少于 500 个动作，Standard 不少于 10,000 个动作，Headless 不少于 20,000 个动作。
- Headless 控件门禁在真实 `Window`/`VisualTree` 上定位 30 个 `AutomationId`，使用 Avalonia.Headless 原始鼠标、键盘、文本和滚轮事件完成 6 次导航、3 次搜索（含执行中取消）、2 次语言切换、3 次账号切换（Online -> OfflineRestricted -> Online）、单场景执行、矩阵取消恢复和完整 12 场景矩阵，以及 CheckBox、Slider、ListBox、ScrollViewer 和 3 个缩放等级的尺寸/像素内容渲染验证；进程测试使用 3 个固定 seed 重放完整负载。
- Windows GUI 门禁使用 UI Automation 从 4 个真实顶层窗口定位 30 个对象，再用系统 `SendInput` 驱动鼠标、键盘和滚轮；覆盖导航、搜索/取消、语言、账号、单场景与两轮完整矩阵、State 控件、列表、窗口最小化/恢复/缩放、多显示器条件分支、实际屏幕截图和 Alt+F4 关闭。应用报告采用 schema 2，并至少保留 25 条逐场景证据。
- 故障与并发探针覆盖 EventBus 有界队列超时、三并发上限、State 有界通知丢弃、Service 预取消、MVVM 失败/拒绝/取消、业务补偿和多调用方 Window 关闭。
- 每次自动运行生成包含 seed、动作分类、覆盖点、预期故障、首个主故障和资源释放结果的脱敏 `run-report.json`。

跨模块动态 contribution、资源趋势采样、两小时 soak 控制面和 exported public API inventory 门禁已经落地。`network` 使用本地 TLS、逻辑 DNS 与 TCP fault proxy；`contribution` 执行七阶段事务和回滚；`api` 对九个运行时程序集执行公开面哈希与证据检查；`burn-in`/`soak` 使用 Avalonia.Headless，不占用系统鼠标。两小时 soak 只有在真实完整时长报告通过后才能记为发布证据。

## 文档索引

- [DESIGN.md](DESIGN.md)：产品边界、总体架构、进程模型、UI 和跨模块事务。
- [MODULE-CATALOG.md](MODULE-CATALOG.md)：48 个应用模块、依赖图和 110 个业务服务。
- [WORKLOAD-CATALOG.md](WORKLOAD-CATALOG.md)：72 个事件、128 个 State、96 条静态路由和 Data 工作负载。
- [SCENARIO-ORCHESTRATION.md](SCENARIO-ORCHESTRATION.md)：12 个复合业务场景、确定性故障计划、跨模块不变量和 GUI/Headless 门禁。
- [DATA-TRANSPORT-RESILIENCE.md](DATA-TRANSPORT-RESILIENCE.md)：HTTP/gRPC/SignalR 协议故障、独立服务进程崩溃和同端口恢复门禁。
- [API-COVERAGE.md](API-COVERAGE.md)：逐项覆盖 City 开发者公开 API 的机械门禁。
- [TEST-PLAN.md](TEST-PLAN.md)：自动驾驶、真实桌面、故障注入、并发、soak 和验收不变量。
- [GUI-CHECKLIST.md](GUI-CHECKLIST.md)：Windows 系统输入门禁与 30 分钟人工视觉复核清单。

## 覆盖边界

运行期必须覆盖：

1. `AtomUI.City.Core`
2. `AtomUI.City.EventBus`
3. `AtomUI.City.State`
4. `AtomUI.City.Routing`
5. `AtomUI.City.Mvvm`
6. `AtomUI.City.Localization`
7. `AtomUI.City.Security`
8. `AtomUI.City.Data`
9. `AtomUI.City.Presentation`

`Templates`、`CLI`、`Build` 和 `Generators` 通过创建、编译、生成清单和发布门禁覆盖，不作为应用运行期服务；`AtomUI.City.Testing` 的开发者公开 test double 只在 automation/fault profile 使用。`PluginSystem` 按当前版本决策排除；EventBus、Routing、Localization、Data 和 Presentation 已公开的 contribution/lease 合同仍使用宿主签发的合成 contribution 验证，但不得据此宣称真实插件加载、ALC 卸载或签名验证已经完成。

Security 的 `AUC-SECURITY-008/009` 使用框架公开合同和默认文件 Provider：Dogfood 在每次运行隔离的目录中保存 3 个账号，验证在线切换、过期缓存的离线受限切换、失败保持旧账号、重复切换幂等、凭据资源隔离、账号删除，以及重建 store/manager 后恢复最后活动账号。测试目录在应用退出后删除，不污染真实用户数据目录。

正常 Data 请求使用应用组合层的 `DogfoodRefreshingAccessTokenProvider`：默认 Security Provider 仍只读取并报告过期凭据，自定义 Provider 在有效期不足 2 分钟时用 refresh token 执行单飞续期、原子回写账号凭据，再让 Data 重取 token。启动阶段用短有效期资源并发发起 16 次请求，必须合并为一次主动刷新；运行不少于 7 分钟的 profile 还必须观察到至少两次主凭据轮换。报告只记录 resource 名和次数，不记录 token、refresh token 或账号标识。

## 规模硬指标

| 资产 | 数量 | 防空壳规则 |
| --- | ---: | --- |
| 应用业务模块 | 48 | 每个模块至少拥有服务、事件或 UI contribution，并参与启动/停止断言 |
| City 基础模块入口 | 4 | EventBus、Routing、Data、Presentation 进入同一 Host 模块图 |
| 业务服务 | 110 | 已由 8 条跨模块工作流全部解析并调用，失败链执行 12 步逆序补偿 |
| Event contract | 72 | 每个 contract 至少有一次成功投递，关键 contract 还要覆盖失败或背压 |
| Event handler | 不少于 144 | 每个事件至少两个 owner 不同的 handler |
| Application State | 72 | 五种访问策略、五种 State lifetime、Transient/Persisted snapshot policy 均有实例 |
| Computed State | 20 | 包含链、菱形、失败恢复和循环防护 |
| Collection State | 16 | 包含批量、快照、恢复、重复 key 和并发更新 |
| Route/Window scoped State | 20 | 随 Route/Window scope 释放并验证不再通知 |
| 静态路由 | 96 | 由 Generator 生成；96 条定义全检查，76 条可导航定义实际匹配并提交 |
| 动态路由 contribution | 6 | attach/revoke 后图版本、journal 和导航结果可验证 |
| ViewModel | 64 | 全部经 `IViewModelFactory` 获取、绑定、提交并验证真实 VisualTree attach |
| View descriptor | 不少于 82 | generated、显式、keyed override/revoke 全覆盖 |
| Command | 96 | 目录命令全部执行，并额外验证 City Command 的失败、取消和重入拒绝状态 |
| Localization key | 不少于 480 | `zh-CN`/`en-US` 主包及 parent fallback、作用域包和格式化 |

上表同时保留当前门禁与最终目标。已完成项必须由运行时代码和测试读取实际注册表后断言，不接受注释计数；动态 contribution、82 个 View descriptor ownership 分支等未完成项仍按本节前述边界作为后续目标。

## 运行命令

```powershell
# 人工使用，窗口保持打开
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile manual

# 真实 Windows 桌面自动化
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile quick --seed 20260911
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile standard --seed 20260911

# Avalonia.Headless，默认不少于 20,000 个动作，并执行原始控件输入门禁
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile headless --seed 20260911

# 新增专项门禁
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile network
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile contribution --contribution-cycles 100
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile api
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile burn-in
dotnet run --project fixtures/AtomUI.City.Fixtures.DesktopDogfood -- --profile soak

# Windows 系统级 GUI 自动化；执行期间会占用鼠标键盘
$env:ATOMUI_CITY_RUN_INTERACTIVE_GUI='1'
$env:ATOMUI_CITY_GUI_ARTIFACT_ROOT="$PWD/.artifacts/desktop-dogfood-gui"
dotnet test tests/AtomUI.City.Presentation.Tests -c Release --filter "Category=InteractiveDesktop"
```

`--smoke` 是 `--profile quick` 的兼容别名。自动 profile 可使用 `--artifact-root <dir>`、`--report <file>`、`--minimum-duration <TimeSpan>`、`--sample-interval <TimeSpan>`、`--contribution-cycles <n>` 和 `--keep-open-on-failure`；时长和轮数覆盖参数用于快速验证测试基础设施，不能替代正式 15 分钟/2 小时/100 轮门禁。报告默认写入隔离的临时目录。GUI artifact 目录必须不存在，以避免把旧证据误判为本轮结果。系统输入前有 5 秒倒计时，`Ctrl+Shift+F12` 可紧急停止，结束后恢复原鼠标位置。Headless 原始输入只注入内存中的 Avalonia Headless Window，不移动或占用操作系统鼠标；IME 和主观视觉质量仍按人工清单复核。
