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

## 补充 Public API 类型卡

本节补齐已经进入 `PublicAPI.Shipped.txt`、但此前只在类型族或概念章节中出现的公开类型。下列类型统一属于 `Preview-frozen`：签名已经受门禁保护，但尚未提升为对外发布后的 `Stable`。除表格特别说明外，必填引用为 null 时抛 `ArgumentNullException`，空白 identity 抛 `ArgumentException`，未知 enum 在实际接纳边界拒绝。

### 注册与默认实现入口

这些扩展类型只配置调用方传入的 `IServiceCollection`，不构建临时 provider；使用 `TryAdd` 的入口必须保留应用已显式注册的替代实现。

| Type / Feature | Purpose / Owner | Lifetime / Disposal | Threading / Failure | Tests |
| --- | --- | --- | --- | --- |
| `AvaloniaUiDispatcherServiceCollectionExtensions` / 001 | 注册 Avalonia `IUiDispatcher` bridge；由应用 composition root 调用。 | 注册 singleton；生命周期由 Root DI 持有。 | Build 前同步配置；移除 Core 的 `UnavailableUiDispatcher` 后以 `TryAdd` 注册，null collection 失败。 | `AvaloniaUiDispatcherTests` |
| `PresentationInteractionServiceCollectionExtensions` / 006、011 | 注册 `InteractionHandlerRegistry` 及其接口映射；由 Presentation composition root 调用。 | concrete/interface 指向同一 singleton；DI 负责生命周期。 | Build 前同步配置；缺少 `IUiDispatcher` 在 provider 解析时失败，不创建临时 provider。 | `PresentationInteractionHandlerTests` |
| `PresentationRuntimeServiceCollectionExtensions` / 009、010、014 | 注册 Runtime、ViewModel factory 与默认 failure presenter。 | concrete/interface Runtime 是同一 singleton；Runtime Stop/DI disposal 负责终止。 | Build 前同步配置；自定义接口实现通过 `TryAdd` 保留，Runtime 状态错误遵守 Runtime 合同。 | `PresentationRuntimeTests`, `AvaloniaUiDispatcherTests` |
| `ViewRegistryServiceCollectionExtensions` / 002、012 | 注册同一个 `ViewRegistry` 作为 registry 与 locator。 | singleton；owner override/revoke 由 registry 管理。 | Build 前同步配置；null collection 失败，重复调用不替换现有服务。 | `ViewLocatorTests` |
| `ActivePluginViewRegistryServiceCollectionExtensions` / 008 | 注册 active plugin view registry 的 concrete/interface 映射。 | 同一 singleton；跟踪项由 lease 或 unload cleanup 撤销。 | Build 前同步配置；重复调用幂等，不代表 PluginSystem 已注册。 | `ActivePluginViewRegistryTests` |
| `PresentationResourceRegistryServiceCollectionExtensions` / 007 | 注册 owner-bound Presentation resource registry。 | 同一 singleton；资源 lease/revoke 决定贡献生命周期。 | Build 前同步配置；重复调用幂等，不扫描插件程序集。 | `PresentationResourceRegistryTests` |
| `PresentationPluginUnloadCoordinatorServiceCollectionExtensions` / 008 | 注册 Presentation 自身的 plugin UI cleanup coordinator。 | 同一 singleton；不拥有 PluginSystem load context。 | Build 前同步配置；依赖缺失在 provider 解析时失败；只建立 Presentation 侧能力。 | `PresentationPluginUnloadCoordinatorTests` |

### View、Outlet、Command 与视觉状态模型

| Type / Feature | Purpose / Owner | Lifetime / Disposal | Threading / Failure | Tests |
| --- | --- | --- | --- | --- |
| `ViewFactoryContext` / 003 | 向已登记的 View factory 暴露当前创建操作使用的 `IServiceProvider`；由 `ViewFactory` 创建。 | 单次 View 创建上下文；不拥有或释放 provider，构造函数不向应用公开。 | factory 在 UI dispatcher 中接收该对象；服务缺失由 factory/DI 的既有异常传播。 | `ViewBindingTests` |
| `ViewModelAcquisitionRequest` / 010 | 描述 ViewModel 类型、解析 provider、可选显式 factory 与可复用实例。 | 单次 acquisition 的不可变请求；不拥有输入对象，所有权由返回的 `ViewModelLease` 决定。 | 可从调用线程构造；type/provider 必须非空；取消传递给显式 factory。 | `ViewModelFactoryTests`, desktop dogfood |
| `RouteOutletCommitResult` / 004、014、015 | 结构化报告 Outlet commit 的 success/error/message/operationId。 | 不可变结果，无释放责任。 | 可跨线程读取；`Failed` 要求非空白 message；取消仍按 `CommitAsync` 合同传播，不伪装为普通成功。 | `RouteOutletTests`, `PresentationIndustrialContractTests` |
| `RouteOutletProperties` / 004、009 | Avalonia attached property bridge，把命名 `ContentControl` 自动注册到所属 WindowSession。 | attach/name change 建立 registration；detach、清空名称或 Window cleanup 时释放 registration。 | 由 Avalonia UI/visual tree 线程使用；无 Window、Session、名称或非 `ContentControl` 时不注册，不执行全局查找。 | Headless/Desktop fixtures |
| `ICommandBindingHandle` / 006 | 表示 command 与 UI source 的可撤销绑定，并允许显式刷新 command visual state。 | 调用方或 `IActivationScope` 所有；`Dispose` 幂等并解绑事件。 | `RefreshAsync` 通过 dispatcher 应用状态；取消/target 失败传播并写诊断；释放后 refresh 为 no-op。 | `CommandBindingTests` |
| `UiCommandState` / 006 | 向 `IUiCommandSource` 传递 `CanExecute` 与异步执行状态快照。 | 不可变 record，无释放责任。 | 可跨线程创建；只能在 dispatcher 内应用到 UI source。 | `CommandBindingTests` |
| `ValidationVisualStateSnapshot` / 006 | 将 `ValidationScope` 当前状态、错误、消息和异常复制为只读视觉快照。 | 独立不可变快照，不持有可变字典；无释放责任。 | 可从任意线程创建；应用到 UI target 必须由 binding 经过 dispatcher；null scope 失败。 | `ValidationVisualStateBindingTests` |
| `UiStateFeedbackKind` / 005 | 区分允许回送 ViewModel 的语义事件与只属于 UI 的高频视觉事件。 | enum 值是兼容面。 | 未知值不得在 policy/adapter 接纳边界静默映射。 | `VisualFeedbackTests` |
| `UiStateFeedbackPolicy` / 005 | 对 `UiStateFeedbackKind` 作纯函数 allowlist 裁决。 | 静态无状态，无释放责任。 | 并发安全；只允许 ValueChanged、CommandInvoked、InteractionCompleted、ValidationRequested、SelectionChanged 回送，hover/pointer/layout/scroll/animation 不回送。 | `VisualFeedbackTests` |
| `NullPresentationFailurePresenter` / 014 | 默认无 UI 的 failure presenter，允许应用不提供 Dialog/Toast 实现。 | singleton-safe、无状态、无释放责任。 | 校验 failure 与预取消 token；否则同步完成，不吞并调用前已经产生的业务异常。 | `PresentationRuntimeTests`, `RouteOutletTests` |

### Visual lifecycle 模型

| Type / Feature | Purpose / Owner | Lifetime / Disposal | Threading / Failure | Tests |
| --- | --- | --- | --- | --- |
| `VisualLifecycleEvent` / 005 | 携带 `VisualIdentity` 与真实 visual event kind；便捷构造可从 View 创建 identity。 | 不可变事件快照，不拥有 View；订阅者不得长期保留以延长 UI 生命周期。 | Hub 通知路径同步读取；null identity/view 失败，未知 kind 由 Hub 接纳边界拒绝。 | `VisualFeedbackTests`, `ViewBindingTests` |
| `VisualLifecycleEventKind` / 005 | 稳定区分 Attached、Detached、Loaded、Unloaded、Focused、Unfocused、Visible、Hidden。 | enum 值属于兼容面。 | 未知值在 `VisualLifecycleHub.Notify` 抛 `ArgumentOutOfRangeException`。 | `VisualFeedbackTests` |
| `VisualLifecycleSubscriptionOptions` / 005 | 为 WindowId、OutletName、OperationId、EntryId 提供 exact-match 过滤，并可绑定 ActivationScope。 | options 由 subscription 在存活期读取；subscription Dispose 或 ActivationScope 结束时撤销。 | null filter 表示 wildcard；字符串使用 ordinal exact match；handler failure 被 Hub 隔离并诊断。 | `VisualFeedbackTests`, Headless/Desktop fixtures |

### Presentation 拥有的 Plugin-facing 合同

以下类型只定义 Presentation 资源、View、Interaction 和字典的 owner/revoke 边界。它们可以被未来 PluginSystem 调用，但其存在与 `Release Verified` 状态不代表 PluginSystem 的加载、隔离或卸载流程已经交付。

| Type / Feature | Purpose / Owner | Lifetime / Disposal | Threading / Failure | Tests |
| --- | --- | --- | --- | --- |
| `ActivePluginView` / 008 | 关联 plugin/contribution、Outlet 与已绑定 View handle；由注册调用方创建。 | descriptor 自身不释放资源；对应 lease/registry cleanup 控制跟踪，Outlet/Entry 仍拥有实际 View。 | 构造要求 pluginId、outlet、handle 有效；contribution 空白规范化为 null。 | `ActivePluginViewRegistryTests` |
| `ActivePluginViewRegistry` / 008 | 维护 active plugin view 快照，并按 plugin 或 contribution 请求关闭。 | singleton registry；`IActivePluginViewLease.Dispose` 或成功关闭时解除强引用。 | 快照和登记受同步锁保护；逐项关闭观察取消；普通 Outlet 失败诊断后返回未关闭，不终止后续项。 | `ActivePluginViewRegistryTests` |
| `IActivePluginViewLease` / 008 | 表示单个 active view 跟踪所有权。 | 调用方所有；同步 `Dispose` 幂等，只解除 registry 跟踪，不直接释放 Outlet/View handle。 | 可与 registry cleanup 竞争，最终只撤销一次。 | `ActivePluginViewRegistryTests`, `PresentationPluginUnloadCoordinatorTests` |
| `PresentationResourceContribution` / 007 | 描述 kind/resource 及可选 plugin/contribution owner。 | contribution 不主动释放；注册后的 lease/registry 拥有撤销责任。 | 构造要求 kind 非空白、resource 非空；owner 空白规范化为 null。 | `PresentationResourceRegistryTests` |
| `PresentationResourceRegistry` / 007 | 保存 owner-bound contribution 快照并按 owner 撤销。 | singleton registry；lease Dispose 或 revoke 移除条目；若 resource 实现 `IDisposable`，撤销时调用一次。 | 注册/快照/revoke 通过 registry gate 协调；resource Dispose 异常写诊断并隔离。 | `PresentationResourceRegistryTests` |
| `IPresentationResourceLease` / 007 | 表示单个 resource contribution 的撤销所有权。 | 调用方所有；`Dispose` 幂等并请求 registry 撤销。 | Dispose failure 按 registry 合同诊断隔离，不把 plugin/resource 对象保留在快照中。 | `PresentationResourceRegistryTests`, `PresentationPluginUnloadCoordinatorTests` |
| `PresentationResourceDictionaryRevocation` / 007、008 | 指定需要从 UI resource dictionaries 撤销的 plugin 与可选 contribution identity。 | 单次异步撤销的不可变请求，无释放责任。 | pluginId 必须非空白；contribution 空白规范化为 null；实际 UI 操作由 revoker dispatcher 化。 | `PresentationResourceDictionaryRevokerTests` |
| `PresentationPluginUnloadRequest` / 008 | 指定 Presentation cleanup 的 plugin-wide 或 contribution-specific 范围。 | 单次 cleanup 的不可变请求，无释放责任。 | pluginId 必须非空白；null contribution 表示整个 plugin。 | `PresentationPluginUnloadCoordinatorTests` |
| `PresentationPluginUnloadCoordinator` / 008 | 依次关闭 active views，再撤销 interaction、view descriptors、resource dictionaries 与 contributions。 | singleton coordinator，不拥有 Plugin load context；只编排 Presentation registries。 | 观察 cancellation；active view 残留立即阻断后续撤销；其余局部异常转成结构化 error 并继续可执行阶段。 | `PresentationPluginUnloadCoordinatorTests` |
| `PresentationPluginUnloadErrorKind` / 008 | 稳定分类 active-view 阻断及四类 revoke failure。 | enum 值属于兼容面。 | 未知值不得由 coordinator 产生；新增值需要兼容性 review。 | `PresentationPluginUnloadCoordinatorTests` |
| `PresentationPluginUnloadError` / 008 | 保存 error kind、message 与可选原始异常。 | 不可变结果项，无释放责任。 | coordinator 按发生阶段稳定追加；不替代 cancellation 的异常传播。 | `PresentationPluginUnloadCoordinatorTests` |
| `PresentationPluginUnloadResult` / 008 | 汇总关闭/撤销计数、字典状态及只读 errors；`Succeeded` 仅在 errors 为空时为 true。 | 不可变结果，防御复制 errors，无释放责任。 | 可跨线程读取；构造要求 pluginId 与 errors 有效；局部失败不伪装成功。 | `PresentationPluginUnloadCoordinatorTests` |

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
