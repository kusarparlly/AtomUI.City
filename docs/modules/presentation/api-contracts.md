# AtomUI.City.Presentation API Contracts

本文记录 1.0 public API 的用途、前置条件、所有权、失败、取消、并发和释放语义。源码新增 public 类型时必须同步更新本文。

## 注册与 Runtime

| API | 合同 |
| --- | --- |
| `PresentationServiceCollectionExtensions.AddPresentation` | 注册完整 Presentation 服务面；可重复调用；options 容量必须为正；不 Build provider。 |
| `PresentationModule` | `UseModule<PresentationModule>()` 的 City Host 入口；服务注册必须等价于 `AddPresentation`；Host shutdown 调用 Runtime Stop。 |
| `IPresentationRuntime.Attach` | Host 已启动、Avalonia 已初始化、UI lifetime 非空；创建 PresentationScope；同一 lifetime 重复调用幂等，换 lifetime 失败。 |
| `IPresentationRuntime.RegisterWindow` | Runtime Ready、UI 线程、Window 尚未 Show 且未重复注册；返回唯一 WindowSession。 |
| `IPresentationRuntime.StartAsync/CreateWindowScope` | headless/test/compatibility 低级入口，不附加 Avalonia lifetime，不替代桌面主路径。 |
| `IPresentationRuntime.StopAsync` | 并发调用共享同一事务；caller token 只取消等待；尝试清理全部 Window 和 PresentationScope；聚合失败后 Runtime Faulted。 |

## View 和 ViewModel

| API | 合同 |
| --- | --- |
| `IViewModelFactory.AcquireAsync` | 优先当前 Entry 精确复用，其次创建 DI scope 解析，再使用显式/generated factory；不得反射猜测或写回 provider；返回 ownership lease。 |
| `ViewModelLease` | 只实现 `IAsyncDisposable`；`EntryOwned` 释放实例，`ServiceScopeOwned` 释放 scope，`Borrowed` 不释放；重复释放幂等。 |
| `IViewRegistry/IViewLocator` | `ViewModel Type + ViewKey` 精确键；无 assignable/name/assembly scan fallback；默认重复失败，显式 owner override 可撤销恢复。 |
| `ViewFactory.CreateAsync` | 在 UI dispatcher 创建 View；factory 结果必须匹配 descriptor ViewType；取消前不执行 factory。 |
| `ViewBinder.Bind` | Avalonia View 必须在 UI 线程绑定；设置 DataContext 并返回唯一 BoundViewHandle；失败清理部分绑定。Bind/Unbind 不发布 visual lifecycle。 |
| `BoundViewHandle.Dispose` | 幂等；由 owning Entry 在 UI dispatcher 释放。`FromExisting` 的自定义 dispose callback 也在该线程执行。 |

## Outlet 和 Entry

| API | 合同 |
| --- | --- |
| `RouteOutletCommitPlan.Replace/Clear` | plan 单次使用；Replace 必须带 handle；lifecycle token 控制最终提交前事务。 |
| `IRouteOutlet.CommitAsync` | 可从任意线程调用；admission 立即转移候选 ownership；同 Outlet FIFO；caller token 入队后只取消等待。 |
| `IRouteOutlet.QueueSnapshot` | 返回 capacity、pending、in-flight、peak pending、rejected count 的一致快照。 |
| `PresentationEntry.DisposeAsync` | 幂等；deactivate、ActivationScope、UI 解绑、ViewModelLease 均尝试执行；聚合失败；最后释放 plan ownership。 |
| `IRouteOutletTarget` | 物理 content target；Avalonia 实现的读写必须在 UI dispatcher。 |

Outlet 失败语义：

- 名称错误、普通 guard/activation/temporary attach 失败返回 Failed，候选释放，Outlet `OutOfSync`。
- rollback、candidate ownership 或 failure presenter 不变量失败使 Outlet `Faulted`。
- final commit 后旧 Entry 清理失败只诊断，结果仍成功，新 Entry 保持 current。
- pending 满载返回 `OutletQueueFull`，拒绝最新 plan 并释放候选。

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

内部 `CandidateOwnershipState` 和 bounded lane 状态不是 public enum，但转换规则属于运行时兼容合同。

## Public 类型族

- Runtime/DI：`PresentationModule`、`PresentationServiceCollectionExtensions`、`IPresentationRuntime`、`PresentationRuntime`、`WindowSession`、`PresentationQueueOptions/Snapshot`。
- View：`ViewForAttribute`、`ViewDescriptor`、`ViewLookupRequest`、`IViewRegistry/IViewLocator`、`ViewFactory/Context`、`ViewBinder`、`BoundViewHandle`、`IViewModelFactory`、`ViewModelLease`。
- Outlet/feedback：`IRouteOutlet`、`RouteOutlet`、`RouteOutletCommitPlan/Result`、`IRouteOutletTarget`、`AvaloniaRouteOutletTarget`、`PresentationEntry`、`VisualIdentity`、`VisualLifecycle*`。
- Interaction/visual state：`IInteractionHandlerRegistry`、`InteractionDispatchContext`、`InteractionHandlerRegistrationOptions`、`CommandBinding`、`ValidationVisualStateBinding` 及其 target/handle/snapshot contracts。
- Plugin UI：active view、resource registry/dictionary revoker、plugin unload coordinator 相关 public contracts。
- Failure/diagnostics：`PresentationException`、`PresentationFailure`、`IPresentationFailurePresenter`、`PresentationDiagnosticIds`。

## 通用规则

必填引用使用 `ArgumentNullException`，空 identity 使用 `ArgumentException`，未知 enum 使用 `ArgumentOutOfRangeException`。终态 mutating API 返回稳定 Result/PresentationException/ObjectDisposedException。取消不得包装成普通失败，除非 API 的 Result 模型明确使用 Canceled 状态。
