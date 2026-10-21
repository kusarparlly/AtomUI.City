# AtomUI.City.Routing

文档等级：Level 3
成熟度：Verified
执行边界：Host runtime navigation graph
程序集：`AtomUI.City.Routing`

## 模块定位

Routing 是与 UI 框架无关的桌面导航内核。它负责声明和生成路由、发布不可变 Route Graph、匹配路径、执行 Guard/Resolver/Middleware、提交 `NavigationSnapshot`、维护历史，以及动态添加和撤销模块或插件路由。

Routing 的提交结果是 Route -> ViewModel Target 的不可变描述，不是 ViewModel 实例或 UI 树。

## 1.0 硬约束

- 运行时不扫描程序集；静态路由由 Source Generator 生成 descriptor。
- `RouteGraphSnapshot` 发布后不可变，版本严格单调递增。
- 每次导航捕获一个 graph snapshot；同一事务中不切换 graph。
- Guard、Resolver、Middleware（包括 post-`next`）或取消失败时不改变 current snapshot 和 journal。
- `NavigationScope` 串行提交；`CancelPrevious`、`Queue`、`RejectIfBusy` 行为确定。
- 每次成功提交生成 scope-bound、generation-bound 的 `NavigationCommitReceipt`；条件恢复只在该提交仍为 current 时恢复提交前 snapshot 与完整 journal checkpoint。
- 诊断只负责观测，诊断接收器故障不得改变导航或图发布结果。
- 动态 contribution 必须可撤销；撤销后新导航不能进入已撤销路由。
- Routing 不引用 Presentation/Avalonia，不创建 ViewModel，不创建 Route DI scope，不操作 VisualTree。

## 当前能力

- Route/Layout/Index/Group/Redirect/ExtensionPoint。
- literal、parameter、optional、default、catch-all 和内置 constraint。
- 强类型 `RouteReference<TParameters>`，含 `[Query]`、`[Fragment]` 绑定。
- path、route reference、URI deep link 和 named outlet route selection。
- enter/leave guard、match policy、resolver、route middleware。
- static/dynamic redirect、循环和最大跳数防护。
- Back/Forward、Push/Replace/Reset、容量裁剪、标量 resolved data 恢复。
- commit receipt、单次条件恢复和 superseded/foreign receipt 稳定拒绝。
- `RouteRegistry` 原子 contribution 发布、ExtensionPoint 挂载和专属服务解析边界。
- `RoutingModule` / `AddRouting` Host DI 集成。
- `AUCRT001` 至 `AUCRT011` 运行时诊断。

## 非目标

- ViewModel/View 创建、激活和复用缓存：Presentation/MVVM 所有。
- UI dispatcher、Outlet 控件和 VisualTree commit：Presentation 所有；Routing 只选择 outlet 名称对应的 descriptor。
- 插件加载、drain 和 ALC 卸载编排：PluginSystem 所有；Routing 只提供 route lease 和服务边界。
- 自定义 route constraint、文件系统路由、ASP.NET endpoint/MVC 语义：不属于 1.0。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| [architecture.md](architecture.md) | 所有权、事务模型和依赖边界。 |
| [detailed-design.md](detailed-design.md) | 管线和失败纪律的展开索引。 |
| [features.md](features.md) | Feature ID 与验收状态。 |
| [api-contracts.md](api-contracts.md) | Public API 行为合同。 |
| [route-definition-syntax.md](route-definition-syntax.md) | Attribute、模板、生成器合同。 |
| [route-graph.md](route-graph.md) | Graph、Registry、Contribution。 |
| [navigation.md](navigation.md) | 导航、并发、redirect。 |
| [lifecycle.md](lifecycle.md) | Graph、scope 和 contribution 生命周期。 |
| [threading.md](threading.md) | 并发、取消和死锁防护。 |
| [guards.md](guards.md) | Guard 与 match policy。 |
| [resolvers.md](resolvers.md) | Resolver 数据边界。 |
| [journal-and-reuse.md](journal-and-reuse.md) | Journal 与恢复语义。 |
| [plugins.md](plugins.md) | 插件路由接入和撤销。 |
| [viewmodel-target.md](viewmodel-target.md) | ViewModel target 描述边界。 |
| [integration.md](integration.md) | Core、Presentation、PluginSystem 等集成边界。 |
| [diagnostics.md](diagnostics.md) | 诊断码和字段。 |
| [diagnostics-and-testing.md](diagnostics-and-testing.md) | 诊断与测试唯一合同索引。 |
| [testing.md](testing.md) | 测试矩阵。 |
| [compatibility.md](compatibility.md) | 1.0 兼容面。 |
