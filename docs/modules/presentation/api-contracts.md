# AtomUI.City.Presentation API Contracts

## Stability 基线

`PublicAPI.Shipped.txt` 中现有签名当前全部为 `Preview-frozen`：门禁阻止未经 review 的漂移，但不等价于已经对外发布的 `Stable` API。每个类型升为稳定状态时必须在对应类型族中明确记录，并同步 compatibility 和测试证据。

本文记录 1.0 public API 的用途、前置条件、所有权、失败、取消、并发和释放语义。源码新增 public 类型时必须同步更新本文。

## ViewModel Composition API

`AUC-PRESENTATION-017` 提供显式 composition service 和异步 ownership lease：

- 调用方按 Runtime/Window owner identity attach 一个逻辑 Root，并显式在 parent lease 下 attach Child。
- Root/Child 是角色而非基类；MVVM public API 不增加 Presentation 角色。
- 每次 attach 返回唯一 `IAsyncDisposable` lease；child 拥有独立、链接父取消的 ActivationScope。
- 重复 owned instance、重复 identity、跨 owner parent、终态 owner 和 lease dispose 后 mutation 稳定拒绝。
- RouteOutlet `PresentationEntry` 不自动加入 composition tree。
- Window close/Runtime stop 接管遗留 Root lease，按 leaf-to-root 完整清理并聚合失败。

公开类型已冻结为 `ViewModelCompositionOwner` 与 `ViewModelCompositionLease`；行为不能弱化为同步 Dispose、反射发现或全局 Root 单例。完整合同见 [viewmodel-composition.md](viewmodel-composition.md)。

## 注册与 Runtime

| API | 合同 |
| --- | --- |
| `PresentationServiceCollectionExtensions.AddPresentation` | 注册完整 Presentation 服务面；可重复调用；options 容量必须为正；不 Build provider。 |
| `PresentationModule` | `UseModule<PresentationModule>()` 的 City Host 入口；服务注册必须等价于 `AddPresentation`；Host shutdown 调用 Runtime Stop。 |
| `IPresentationRuntime.Attach` | Host 已启动、Avalonia 已初始化、UI lifetime 非空；创建 PresentationScope；同一 lifetime 重复调用幂等，换 lifetime 失败。 |
| `IPresentationRuntime.RegisterWindow` | Runtime Ready、UI 线程、Window 尚未 Show 且未重复注册；返回唯一 WindowSession。 |
| `IPresentationRuntime.StartAsync/CreateWindowScope` | headless/test/compatibility 低级入口，不附加 Avalonia lifetime，不替代桌面主路径。 |
| `IPresentationRuntime.StopAsync` | 并发调用共享同一事务；`waitCancellationToken` 只取消等待；尝试清理全部 Window 和 PresentationScope；聚合失败后 Runtime Faulted。 |

## View 和 ViewModel

| API | 合同 |
| --- | --- |
| `IViewModelFactory.AcquireAsync` | 优先当前 Entry 精确复用，其次创建 DI scope 解析，再使用显式/generated factory；不得反射猜测或写回 provider；返回 ownership lease。 |
| `ViewModelLease` | 只实现 `IAsyncDisposable`；`EntryOwned` 释放实例，`ServiceScopeOwned` 释放 scope，`Borrowed` 不释放；重复释放幂等。 |
| `IViewRegistry/IViewLocator` | `ViewModel Type + ViewKey` 精确键；无 assignable/name/assembly scan fallback；默认重复失败，显式 owner override 可撤销恢复。 |
| `ViewFactory.CreateAsync` | 在 UI dispatcher 创建 View；factory 结果必须匹配 descriptor ViewType；取消前不执行 factory。 |
| `ViewBinder.Bind` | Avalonia View 必须在 UI 线程绑定；设置 DataContext 并返回唯一 BoundViewHandle；失败清理部分绑定。Bind/Unbind 不发布 visual lifecycle。 |
| `BoundViewHandle.Dispose` | 幂等；由 owning Entry 在 UI dispatcher 释放。`FromExisting` 的自定义 dispose callback 也在该线程执行。 |
| `ViewModelCompositionOwner.AttachRootAsync` | owner 内 composition id 唯一；同一实例全局同时只允许一个 composition activation owner；先注册 ownership 再 activation，失败完整回滚。 |
| `ViewModelCompositionLease.AttachChildAsync` | parent 必须 active 且属于同一 owner；child 使用独立且链接取消的 ActivationScope；返回唯一 async lease。 |
| `ViewModelCompositionLease/Owner.DisposeAsync` | 并发调用共享清理；拒绝新 attach、drain structured tasks、leaf-to-root 停用并按 ownership 释放；单项失败继续且最终聚合。 |

## Outlet 和 Entry

| API | 合同 |
| --- | --- |
| `RouteOutletCommitPlan.Replace/Clear` | plan 单次使用；Replace 必须带 handle；plan 只描述候选资源与路由 metadata，不保存 token。 |
| `IRouteOutlet.CommitAsync` | 可从任意线程调用；admission 立即转移候选 ownership；同 Outlet FIFO；调用方 token 与可选 Window Scope token 在边界合成为唯一有效令牌。取消会在 final commit 前停止事务，并在回滚、候选释放完成后报告。 |
| `IRouteOutlet.QueueSnapshot` | 返回 capacity、pending、in-flight、peak pending、rejected count 的一致快照。 |
| `PresentationEntry.DisposeAsync` | 幂等；deactivate、ActivationScope、UI 解绑、ViewModelLease 均尝试执行；聚合失败；最后释放 plan ownership。 |
| `IRouteOutletTarget` | 物理 content target；Avalonia 实现的读写必须在 UI dispatcher。 |

Outlet 失败语义：

- 名称错误、普通 guard/activation/temporary attach 失败返回 Failed，候选释放，Outlet `OutOfSync`。
- rollback、candidate ownership 或 failure presenter 不变量失败使 Outlet `Faulted`。
- final commit 后旧 Entry 清理失败只诊断，结果仍成功，新 Entry 保持 current。
- pending 满载返回 `OutletQueueFull`，拒绝最新 plan 并释放候选。

## Routing-Presentation coordination

| API | 合同 |
| --- | --- |
| `IRoutePresentationCoordinator.NavigateAsync` | 按 Outlet 实例串行；调用 navigation delegate 后只接受已提交且带 receipt 的结果；Router 未提交时不调用 plan factory；Presentation 成功时不恢复 Router。 |
| `IRoutePresentationPlanFactory.CreateAsync` | 应用拥有 route -> ViewModel/View/lease 映射；返回前拥有并清理部分候选，成功返回单次 `RouteOutletCommitPlan` 后由协调流程提交所有权。 |
| `RoutePresentationPlanContext` | 只读提供 committed `NavigationResult`、对应 `NavigationCommitReceipt` 与实际 outlet name。 |
| `RoutePresentationResult` | 结构化报告 Routing、Presentation、restore 三阶段；`Succeeded` 仅在 Router 与 Outlet 都成功提交时为 true。 |

pre-admission cancellation 不调用 Router/plan。Router commit 后的 plan/Outlet 失败必须用 receipt 请求条件恢复，且恢复不再受 caller token 中断；更新 generation 已获胜时返回 `RestoreRejectedByNewerNavigation`。协调器不回放用户操作、不构造业务 View、不选择错误提示或重试政策。

## Window

| API | 合同 |
| --- | --- |
| `WindowSession.RegisterOutlet/GetOutlet` | Window 未关闭；名称非空且唯一；registration dispose 撤销并异步停止 Outlet。 |
| `WindowSession.CloseAsync()` | 等价于 `CloseAsync(Application)`；普通 Application close 可被 guard 拒绝。 |
| `WindowSession.CloseAsync(WindowCloseOrigin)` | `User/Application/OperatingSystem` 必须为已定义值；OS 不可拒绝；并发调用共享事务。 |
| `WindowSession.DisposeAsync` | 不可拒绝关闭并等待全部清理。 |
| `WindowSession.Outlets` | 返回按名称稳定排序的快照，不暴露内部可变集合。 |

可拒绝关闭先执行全部 `ICanDeactivate`，再执行最多一个 `IConfirmDeactivate`。多个 confirmation owner 返回 false、保持 Ready、记录 `AUCPRS043`。

## Interaction、Validation 和 Command

| API | 合同 |
| --- | --- |
| `IInteractionHandlerRegistry.Register` | registration 按 Activation/Route、Window、Presentation 分层；同层最后有效者生效；dispose/revoke 取消在途调用。 |
| `HandleAsync` | handler 在 UI dispatcher；无 handler 为 NotHandled；取消为 Canceled；异常为 Failed。 |
| `GetModalQueueSnapshot` | 每 Window 模态 lane 快照；默认 1 in-flight + 8 pending；满载拒绝最新并返回 `InteractionQueueFull`。 |
| `ValidationVisualStateBinding.ApplyAsync` | 仅显式调用时把 snapshot 应用到 target；不发现规则、不决定文案/样式；UI target 失败传播。 |
| `CommandBinding.BindAsync` | 可选自定义 command source bridge；刷新 visual state 在 dispatcher；handle dispose 解除事件。 |
| `IUiCommandFactory.Create/CreateAsync` | Avalonia ViewModel 的默认命令创建入口；镜像 MVVM 的同步、泛型、activation、单 Command 并发、共享 `CommandExecutionDomain` 和异常处理能力；dispatcher 与 diagnostics 由 DI 固定提供，调用方不得逐命令遗漏或替换。异步通知遵循 ADR 0013，命令业务仍不被搬入 UI 线程；domain-first 重载只转发 ADR 0015 语义。 |

## Visual lifecycle

`VisualLifecycleHub.Subscribe` 注册带可选 Window/Outlet/Operation/Entry identity 过滤的 handler。真实 Avalonia attached/detached adapter 发布事件；单 handler 失败记录诊断并继续后续 handler。`ViewBinder` 不合成事件，事件不自动写 State/EventBus。

## Resource 和 Plugin UI

| API | 合同 |
| --- | --- |
| `IPresentationResourceRegistry` | owner-bound contribution 注册、按 plugin/contribution 撤销；lease/revoke 幂等；失败隔离。 |
| `IPresentationResourceDictionaryRevoker.RevokeAsync` | 在 dispatcher 按序调用全部 target，聚合 Exception；无 culture/Localization 语义。 |
| `IActivePluginViewRegistry` | 跟踪 active plugin view；close/revoke 后不能保留强引用。 |
| `IPresentationPluginUnloadCoordinator.CleanupAsync` | active view 优先；仍有 active view 时阻断；其余类别按序撤销并聚合结果。 |

## Public Enum

| Enum | 值与语义 |
| --- | --- |
| `PresentationRuntimeState` | NotReady, Ready, Stopping, Stopped, Faulted |
| `WindowSessionState` | Registered, Ready, Closing, Closed, Faulted |
| `WindowCloseOrigin` | User, Application, OperatingSystem |
| `RouteOutletState` | Empty, Preparing, TemporaryAttached, Committed, OutOfSync, Stopping, Stopped, Faulted |
| `RouteOutletOperation` | Replace, Clear |
| `ViewModelOwnership` | EntryOwned, ServiceScopeOwned, Borrowed |
| `InteractionHandlerScope` | Presentation, Window, Route, Activation |
| `PresentationFailureLevel` | Operation, Outlet, Window, Runtime |
| `PresentationError` | 稳定错误分类；queue、ownership、close confirmation 均有独立值。 |
| `RoutePresentationResultStatus` | Succeeded, RoutingNotCommitted, PresentationFailedRouteRestored, PresentationFailedRouteCommitted, RestoreRejectedByNewerNavigation, StructuralFailure, Cancelled |

内部 `CandidateOwnershipState` 和 bounded lane 状态不是 public enum，但转换规则属于运行时兼容合同。

## Public 类型族

- Runtime/DI：`PresentationModule`、`PresentationServiceCollectionExtensions`、`IPresentationRuntime`、`PresentationRuntime`、`WindowSession`、`PresentationQueueOptions/Snapshot`。
- View：`ViewForAttribute`、`ViewDescriptor`、`ViewLookupRequest`、`IViewRegistry/IViewLocator`、`ViewFactory/Context`、`ViewBinder`、`BoundViewHandle`、`IViewModelFactory`、`ViewModelLease`。
- Outlet/feedback：`IRouteOutlet`、`RouteOutlet`、`RouteOutletCommitPlan/Result`、`IRouteOutletTarget`、`AvaloniaRouteOutletTarget`、`PresentationEntry`、`VisualIdentity`、`VisualLifecycle*`。
- Routing coordination：`IRoutePresentationCoordinator`、`RoutePresentationCoordinator`、`RoutePresentationNavigation`、`IRoutePresentationPlanFactory`、`RoutePresentationPlanContext`、`RoutePresentationResult/Status`。
- Composition：`ViewModelCompositionOwner`、`ViewModelCompositionLease`。
- Interaction/visual state：`IInteractionHandlerRegistry`、`InteractionDispatchContext`、`InteractionHandlerRegistrationOptions`、`IUiCommandFactory`、`CommandBinding`、`ValidationVisualStateBinding` 及其 target/handle/snapshot contracts。
- Plugin UI：active view、resource registry/dictionary revoker、plugin unload coordinator 相关 public contracts。
- Failure/diagnostics：`PresentationException`、`PresentationFailure`、`IPresentationFailurePresenter`、`PresentationDiagnosticIds`。

## 通用规则

必填引用使用 `ArgumentNullException`，空 identity 使用 `ArgumentException`，未知 enum 使用 `ArgumentOutOfRangeException`。终态 mutating API 返回稳定 Result/PresentationException/ObjectDisposedException。取消不得包装成普通失败，除非 API 的 Result 模型明确使用 Canceled 状态。

Composition 与 Routing coordination 已加入 Public 类型族、PublicAPI baseline、诊断和 compatibility；后续改变 ownership、generation、取消、恢复或结构化结果语义必须按 breaking change review。
