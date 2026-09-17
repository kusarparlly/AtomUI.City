# 应用模型 API

本页汇总 Routing、State、MVVM 与 Presentation。四者共同构成桌面应用的页面、状态、ViewModel 和窗口运行模型，但仍保持独立包边界。

## Routing

完整合同：[Routing API Contracts](../modules/routing/api-contracts.md)

| API family | 主要类型 | 用途 |
| --- | --- | --- |
| 定义 | `RouteTemplate`、route attributes、`RouteReference<T>` | 声明、解析和绑定路由 |
| 图 | `RouteDescriptor`、`RouteGraphSnapshot`、`IRouteRegistry` | 验证候选图并原子发布快照 |
| 导航 | `IRouter`、`NavigationScope`、`NavigationOptions`、`NavigationResult` | 执行可取消的导航事务 |
| 管线 | guard、resolver、match policy、middleware contracts | 在提交前运行应用逻辑 |
| 动态贡献 | `RouteContribution`、`RouteContributionLease` | 注册并撤销带 owner 的路由贡献 |

接入 Host 时选择 `RoutingModule`，或者在独立 DI 场景调用 `AddRouting`。导航成功才会原子提交 snapshot 和 journal；NotFound、Rejected、Cancelled、Failed 都不得留下半提交状态。

关键顺序：leave guard 从当前叶到根，enter guard 与 resolver 从目标根到叶，middleware 以嵌套顺序执行。不要保存并延迟调用 middleware 的 `next`。

## State

完整合同：[State API Contracts](../modules/state/api-contracts.md)

| API family | 主要类型 | 用途 |
| --- | --- | --- |
| 注册 | `IStateRegistry`、`StateDefinition<T>` | 以稳定 key 定义状态 |
| 创建 | `IStateFactory` | 创建 writable、computed 和 collection state |
| 读取/写入 | `IReadOnlyState<T>`、`IWritableState<T>` | 读取快照和受控写入 |
| 计算 | `IComputedState<T>` | 根据依赖派生状态 |
| 集合 | `IStateCollection<TKey,TItem>` | 有身份的集合状态 |
| Scope | `IStateScope`、`IStateScopeAccessor` | 将状态和订阅绑定到生命周期 |
| 权限 | `StateWriteAuthority`、`IApplicationStateWriter` | 限制跨模块写入 |

使用 `services.AddState()` 注册基础设施。订阅和 reaction 必须释放；scope 释放后不得继续通知。不要绕过 `StateWriteAuthority` 直接给予无关模块写权限。

## MVVM

完整合同：[MVVM API Contracts](../modules/mvvm/api-contracts.md)

| API family | 主要类型 | 用途 |
| --- | --- | --- |
| ViewModel | `ViewModelBase` | observable validation、激活与释放 |
| 激活 | `IActivatable`、activation scope contracts | 将订阅和异步工作绑定到可见生命周期 |
| 命令 | `CommandGroup`、`CommandExecutionState` | 聚合命令并观察执行状态 |
| 验证 | validation contracts 和事件模型 | 公开稳定、不可伪造的验证状态 |

ViewModel 的构造函数只建立对象不变量；订阅、加载和 UI 相关工作放入激活事务。Deactivate/Dispose 必须撤销订阅并停止属于该 ViewModel 的工作。

## Presentation

完整合同：[Presentation API Contracts](../modules/presentation/api-contracts.md)

| API family | 主要类型 | 用途 |
| --- | --- | --- |
| Runtime | `PresentationModule`、`IPresentationRuntime` | 将桌面呈现生命周期接入 Host |
| View mapping | `[ViewFor]`、mapping descriptors | 将 ViewModel 映射到 View |
| Window | window session、registry 和 options | 创建、跟踪和关闭窗口 |
| Outlet | outlet session、navigation presentation contracts | 在区域中呈现目标 |
| Interaction | interaction request/handler contracts | ViewModel 发起 UI 交互 |
| Resources | resource contribution/registry contracts | owner 化注册主题和资源 |

推荐通过 `PresentationModule` 接入 City Host；独立 DI 场景可使用 `AddPresentation`。View 创建由 Presentation runtime 管理，应用业务代码不应直接调用内部 view factory 执行入口。

窗口、outlet、interaction 和资源贡献都必须绑定 owner；owner 终止后不得保留 View、ViewModel、delegate 或插件类型引用。
