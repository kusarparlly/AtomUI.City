# Desktop Dogfood 总体设计

## 1. 产品定义

应用名暂定为 **City Operations Workbench**，是一个多租户商业运营桌面工作台。用户可以同时处理商品、库存、订单、支付、履约、物流、退货、客户支持、报表和系统诊断；应用内置一个仅监听 loopback 的真实 HTTP/gRPC/SignalR 业务服务器，用可重复 seed 产生业务数据和实时变化。

选择这一业务域的原因不是界面丰富，而是它天然具备以下压力：

- 一个操作跨多个领域，适合验证 Module、DI、EventBus、State 和 Data 的交叉调用。
- 列表、详情、工作区、弹窗、多窗口和实时看板并存，适合验证 Router、MVVM 与 Presentation。
- 权限、租户、账号、离线、重连和敏感操作并存，适合验证 Security 与 Data。
- 大量菜单、字段、错误和动态消息需要中英文即时切换，适合验证 Localization。
- 导航、网络推送、后台命令和用户操作会竞争同一状态，适合验证并发、取消和释放。

Dogfood 必须能被人实际使用至少数小时，也必须能用固定 seed 自动重放。业务数据可以是模拟数据，但框架路径、网络协议、Avalonia Window/VisualTree 和文件 IO 必须是真实实现。

## 2. 非目标

- 不追求生产级商业规则完整性，不接公网或真实支付系统。
- 不用 Dogfood 代替模块单元测试、NativeAOT、package consumer 或基准测试。
- 不在应用内重新实现 City 缺失能力来制造“已覆盖”假象。
- 不把 Presentation 变成业务 Dialog、文案、路由解释或 Validation 规则提供者。
- 不使用 AtomUI 控件依赖；先以 Avalonia 原生控件验证 Presentation 主包的最小依赖边界。
- 不把 `fixtures/AtomUI.City.Presentation.DesktopApp` 删除或扩成产品；该 fixture 继续承担短生命周期发布 smoke。

## 3. 工程形态

建议最终目录结构：

```text
fixtures/AtomUI.City.Fixtures.DesktopDogfood/
  Program.cs                         # City Host 与 Avalonia 双生命周期入口
  App.axaml / App.axaml.cs           # Attach runtime、注册 Window
  Composition/                       # root module、DI、生成清单接入
  Modules/                           # 48 个应用 Module
  Contracts/                         # Event/Data/route/shared immutable DTO
  Services/                          # 110 个服务实现
  State/                             # definitions、computed、collections、scopes
  Routing/                           # RouteMap、adapter、guard/resolver/middleware
  ViewModels/                        # 64 个 ViewModel
  Views/                             # Window、View、interaction、validation target
  Localization/                     # generated/assembly/file/in-memory locpack
  Security/                          # account session、file provider、policy、permission
  Data/                              # native clients、pipeline、local server
  Automation/                        # deterministic driver、fault gates、ledger
  Diagnostics/                       # UI explorer、snapshots、failure presenter
  Reports/                           # machine-readable run report
```

第一阶段可以是一个 executable project，但源码必须按上述边界拆分；若单项目编译时间或 owner generator 边界不能真实验证，再把 Contracts、Foundation 和 LocalServer 拆为独立项目。不得一开始创建 48 个空 csproj 伪造模块复杂度。

## 4. 启动与停止

桌面主路径固定为：

```text
ProcessEntryPoint
-> ApplicationHost.CreateBuilder(args)
-> ConfigureHost / ConfigureServices / generated module roots
-> Build
-> await cityHost.StartAsync()
-> Avalonia StartWithClassicDesktopLifetime
-> App.OnFrameworkInitializationCompleted
-> presentationRuntime.Attach(lifetime, cityHost.HostScope)
-> RegisterWindow(mainWindow, "main") before Show
-> interactive or automation session
-> Window/Application exit
-> await cityHost.StopAsync()
-> await cityHost.DisposeAsync()
```

Avalonia 和 City Host 是两个协作生命周期，不互相伪装。组合根只允许一个只读 bootstrap handoff 把已构建 Host 交给 `Application`；业务 View/ViewModel 不得持有 builder。Host 必须只有一个 Root Provider。

关闭必须覆盖：用户点关闭、应用命令关闭、操作系统关闭、Host stop、并发关闭、关闭确认拒绝和强制不可拒绝清理。所有 accepted Data/Event/Outlet/Interaction 工作先收束，连接逆序关闭，Window/Outlet/Activation scope 释放，最后停止 Host。

## 5. 运行时分层

```text
Avalonia Views
    | binding / controls / real visual events
Presentation runtime + application Routing-Presentation adapter
    | ViewModel lease / Outlet commit / Interaction / validation target
MVVM ViewModels
    | commands / activation / interaction request
Application services
    | business transaction orchestration
Routing + Security guards + Localization lookup
    | target / policy / text
State projections <-> EventBus workflows <-> Data pipeline
    |                         |
Security principal/token      HTTP + gRPC + SignalR local server
    \_________________________/
             City Host / Module / Lifecycle / Diagnostics
```

规则：

1. View 只处理 Avalonia visual 和样式；不直接访问 Data transport。
2. ViewModel 调用 application service，不直接构造 transport、provider、scope 或 View。
3. 业务服务在成功提交后写 State、发布 Event；取消后不得写 State/UI/cache。
4. Event handler 不假定 UI 线程；需要呈现时写 State 或使用明确 dispatcher。
5. Router 只提交导航 snapshot；应用 adapter 在 snapshot 成功后取得 ViewModel lease 并提交 Outlet。
6. Localization 文本由业务/ViewModel 调用 `ILocalizationService` 获得，Presentation 不提供文案。
7. Security 只作身份、权限、policy、route/command/data 前置判断，不实现登录 UI。

## 6. Window 与 Outlet

应用至少维护四类真实 Window：

| Window | 用途 | Outlet |
| --- | --- | --- |
| MainWindow | 主工作台 | `primary`、`navigation`、`inspector`、`activity` |
| OrderWorkbenchWindow | 可弹出的订单并行工作区 | `primary`、`details`、`timeline` |
| SupportWindow | 客服会话与客户资料 | `primary`、`customer`、`conversation` |
| DiagnosticsWindow | Host、事件、State、路由、队列和连接观测 | `primary`、`probe` |

主窗口采用安静、工具型布局：顶部账号/租户/语言/连接状态，左侧树状导航，中间主 Outlet，右侧 inspector，底部 activity/diagnostics。页面不使用营销 hero、装饰性渐变或嵌套卡片。

每个 Window 必须在 `Show` 前注册；Attached Property 与显式 `RegisterOutlet` 两条入口都要使用。不同 Window/Outlet 并行提交，同一 Outlet 保持 FIFO。自动化必须制造 pending 满载、caller cancellation、lifecycle cancellation、临时 attach 后 activation 失败、rollback、旧 Entry 清理失败和 Faulted 拒绝。

## 7. Routing-Presentation adapter

Dogfood 自己实现明确的 adapter，不把该职责塞回 Routing 或 Presentation：

```text
NavigateAsync(route)
-> Router 提交 NavigationSnapshot
-> adapter 读取最终 route/outlet/parameters/resolved data
-> IViewModelFactory.AcquireAsync
-> IViewLocator.Locate
-> ViewFactory.CreateAsync
-> ViewBinder.Bind
-> ActivationScope + lifecycle token
-> RouteOutletCommitPlan.Replace
-> IRouteOutlet.CommitAsync
-> 记录 Router snapshot 与 UI Entry 是否收敛
```

adapter 在交出 plan 前拥有 candidate；`CommitAsync` admission 后不再释放 candidate。Routing 成功但 UI commit 失败时保留 Router 真相，显示结构化 failure，并允许按同一 snapshot 显式 reconcile；不得回滚 Router journal 冒充原子跨模块事务。

## 8. 关键跨模块业务链

### 8.1 创建并履约订单

```text
OrderEditorView command
-> MVVM OperationScope + ValidationScope
-> Security command policy
-> Data HTTP create order (token + retry + idempotency)
-> OrderCreated event
-> Inventory reservation + Fraud evaluation (parallel handlers)
-> State collections/projections
-> Payment gRPC unary
-> Fulfillment workflow
-> SignalR shipment progress
-> Notifications/Audit/Analytics handlers
-> computed dashboard state
-> EventBus/State subscription marshalled to Avalonia UI
-> Router opens order details in inspector outlet
```

链路必须支持在 HTTP 返回、Event handler、payment、Outlet commit 和 Window close 任一阶段取消或失败，并验证不产生半提交 UI、过期 cache 或跨账号数据。

### 8.2 切换租户与账号上下文

使用 `IAccountSessionStore`、`ICredentialStore` 和 `IAccountSessionManager` 管理真实文件会话：先完整读取目标账号资料、权限和凭据，验证 online/offline 条件，再发布同 revision 的 authentication/session；Data 后续请求从当前账号按 client resource 获取 token，应用 bridge 据 revision 失效 cache、重建受保护命令状态并重新评估当前 route。

自动化保存 3 个长期账号和 1 个临时账号，覆盖成功、权限过期拒绝、离线受限、重复幂等、删除、资源隔离和 process-like 重启恢复。任一步失败都必须保留原活动账号，诊断与报告只记录账号 hash，不记录 token、refresh token 或完整 claims。

### 8.3 中英文切换

```text
Language selector
-> Localization.SetCultureAsync
-> lazy load global + active Window/Route packages
-> CultureState revision (City.State)
-> LocalizedText FIFO refresh
-> application Avalonia bridge dispatch
-> 菜单、页面、Validation、DataError 和实时消息同时刷新
```

Presentation 只承接 UI dispatcher；所有 key、fallback、格式化和 missing marker 均由 Localization 处理。切换期间并发导航、SignalR 推送、窗口关闭和 contribution revoke，最终所有可见文本必须收敛到同一 culture revision。

### 8.4 断线、重连与离线只读

Fault console 关闭 SignalR、延迟 HTTP、令 gRPC deadline 超时。Data connection 状态通过 EventBus 投影到 State；Router/Security guard 阻止需要在线 mutation 的页面和命令；只读页面使用 stale cache 并明确展示状态。恢复连接后执行有界同步队列，旧 principal/route revision 的结果不得提交。

### 8.5 动态 contribution 撤销

不加载真实插件，只通过各模块公开 controller/registry 签发一个 `seasonal-operations` 合成 contribution：增加 Event handler/channel 权限、6 条 route、局部语言包、Data client/handler/cache owner 和 Presentation View/resource override。撤销顺序由应用编排并断言所有 lease 释放、route journal stale entry 被跳过、缓存失效、View override 恢复、旧类型无强引用。

## 9. 可观测性

DiagnosticsWindow 至少显示：

- Host/module/lifecycle 状态和最近 diagnostics；
- EventBus channel capacity、pending、drop/reject/coalesce、handler latency；
- State key/version/access policy、computed dirty/failure、collection revision；
- Router graph/current/back/forward、operationId、guard/resolver/middleware timeline；
- Presentation Window/Outlet/Interaction queue、Entry 和 close origin；
- Data request、cache、resilience、connection、stream 和 transfer progress；
- Security auth/permission/policy revision，不显示 token 或完整 claims；
- Localization culture/fallback/loaded package/missing key。

UI explorer 只读取 public snapshot/diagnostic contract。若模块没有公开可观测入口，则通过测试 ledger 记录调用边界，不通过反射读取私有字段。

## 10. 配置与数据

- 配置来源：appsettings、环境变量、命令行和 automation profile；Build 后验证捕获的配置入口冻结。
- 工作数据：每个 run 使用隔离临时目录；manual profile 使用应用 Local 数据目录。
- 服务器：随机 loopback port，禁止监听公网接口。
- seed：默认 `20260910`，所有生成数据、延迟和故障序列可重放。
- 时间：业务服务使用可控 clock/scheduler，Avalonia visual 和真实网络仍用真实时间。
- 凭据：只使用测试 token；输出、日志、State、diagnostics 和报告必须做泄漏扫描。

## 11. 实施切片

复合业务行为的第二阶段按 [SCENARIO-ORCHESTRATION.md](SCENARIO-ORCHESTRATION.md) 实施。该合同定义 12 个固定场景、统一编排器、确定性故障、逐场景报告证据，以及 Headless 与 Windows GUI 共用的真实 UI 入口；本文件继续负责总体架构和模块边界。

Data 传输稳定性的第三阶段按 [DATA-TRANSPORT-RESILIENCE.md](DATA-TRANSPORT-RESILIENCE.md) 实施。该合同在现有 loopback 正常路径上增加进程内协议故障与独立服务子进程两层门禁，并要求 HTTP、gRPC、SignalR 在真实进程崩溃后通过相同 endpoint 恢复。

当前已完成 Bootstrap、静态 Catalog、四 Window/十二 Outlet、Data breadth、第二阶段 Automation 和第三阶段 Data resilience：110 个 Service 已形成 8 条业务图，全部静态 Route/ViewModel/Command 已执行，12 个复合场景与 13 个传输故障已进入 Quick/Standard/Headless/Windows GUI，schema 2 JSON 主报告、独立服务进程 crash/restart 和真实屏幕截图门禁已落地。动态 contribution、资源趋势、soak 和公开 API 机械清单尚未完成。

1. Bootstrap：真实 City Host + Avalonia lifetime + MainWindow + Presentation attach。
2. Catalog：生成 48 Module、110 service、72 event、128 state、96 route 的实际目录和计数门禁。
3. Vertical slice：订单链先贯穿九个运行期模块。
4. UI breadth：四 Window、全部 Outlet、64 ViewModel、Interaction/Validation/Command。
5. Data breadth：HTTP/gRPC/SignalR/stream/large payload/resilience/cache/concurrency。
6. Dynamic and failure：contribution、故障注入、队列满载、取消、关闭竞态。
7. Automation：quick/standard/soak/extreme，子进程 watchdog 和 machine report。
8. Release gate：真实 Windows desktop、Headless、公开 API coverage、包消费和长期资源检查。

每个切片都必须产生可运行应用；不得等所有页面写完后才第一次启动 Avalonia。
