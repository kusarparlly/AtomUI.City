# AtomUI.City.Presentation Industrial Design

本文是 Presentation 1.0 的规范性总合同。专题文档只能细化本文，不能改变本文的边界、所有权、线程、事务、失败和验收规则。

## 1. 定位

Presentation 是 City 业务运行时与 Avalonia 之间的 UI 事务协调层。Routing 决定已经提交的导航目标，应用的 Routing-Presentation adapter 取得 ViewModel lease、定位并创建 View、完成绑定，再把单次使用的 `RouteOutletCommitPlan` 提交给 Outlet。Presentation 不解释 route，也不修改 Router snapshot 或 journal。

Presentation 主包只绑定 Avalonia，不绑定 AtomUI。AtomUI 或其他控件库必须通过应用代码或独立可选适配包接入。

## 2. 明确边界

Presentation 负责：

- Avalonia dispatcher、WindowSession、具名 Outlet 和 VisualTree 物理提交。
- View 精确注册、定位、创建和 DataContext 绑定。
- ViewModel 获取协议和 ownership lease；实例必须来自 DI、generated factory、应用 factory 或当前 Entry 复用。
- Activation、deactivation、Interaction 定位、模态排队、可选 Validation visual bridge。
- 白名单 Avalonia visual 回执、插件 UI contribution 撤销和分层故障诊断。

Presentation 不负责：

- 不创建业务状态，不解释路由，不实现业务 Dialog、Toast、表单规则或视觉风格。
- 不提供文案查找、语言包、fallback、culture revision、`GetText` 或 `Localized*` binding。应用 ViewModel/业务代码直接使用 Localization；Avalonia Binding 和资源字典由应用决定如何承接。
- 不建立第二套 Avalonia Binding、事件或 priority 模型，不运行阻塞 IO，不做程序集扫描式 View 发现。
- 不把 factory 创建的对象写回已经构建的 `IServiceProvider`。

## 3. 启动和注册

支持两种等价注册入口：

- City Host 使用 `builder.UseModule<PresentationModule>()`。
- 独立 DI 或测试使用 `services.AddPresentation()`。

二者必须注册同一服务集合且可重复调用。Host Build/Start 后、Avalonia framework initialization 完成时调用 `IPresentationRuntime.Attach`；Attach 只建立 runtime bridge 和 `PresentationScope`，不修改 DI。Window 必须在 `Show` 前于 UI 线程调用 `RegisterWindow`，一个 Window 只对应一个 WindowSession 和 WindowScope。

`StartAsync/CreateWindowScope` 是 headless 和兼容性低级入口，不得代替桌面主路径 `Attach/RegisterWindow`。

## 4. 所有权

ViewModel 不由 Presentation 直接反射构造。`IViewModelFactory` 返回 `ViewModelLease`，ownership 只能是：

- `EntryOwned`：Entry 异步释放实例。
- `ServiceScopeOwned`：Entry 异步释放创建该实例的 DI scope。
- `Borrowed`：Entry 不释放实例。

`ViewModelLease` 只实现 `IAsyncDisposable`，不得提供同步等待异步释放的入口。

commit plan 是单次使用的候选资源所有权凭证，内部状态必须按以下路径 CAS 转移：

```text
AdapterOwned -> OutletTransactionOwned -> EntryOwned -> Released
                                  \--------------------> Released
```

`CommitAsync` 一旦接收 plan，Outlet 就拥有候选。成功最终提交后所有权转给 `PresentationEntry`；拒绝、取消或最终提交前失败由 Outlet 释放。重复提交同一个 plan 是不变量破坏，必须抛 `CandidateOwnershipViolation` 并记录 `AUCPRS042`。

Entry 清理顺序为 deactivate、ActivationScope、visual subscription 与 BoundViewHandle、ViewModelLease。Avalonia 相关解绑必须在 UI dispatcher；单项失败不阻断后续项，最终聚合失败。

## 5. 五套状态机

1. Runtime：`NotReady -> Ready -> Stopping -> Stopped`，不可恢复失败进入 `Faulted`。
2. WindowSession：`Registered -> Ready -> Closing -> Closed`；可拒绝关闭被拒绝时 `Closing -> Ready`；清理失败进入 `Faulted`。
3. RouteOutlet：`Empty/Committed/OutOfSync -> Preparing -> TemporaryAttached -> Committed/Empty`；最终提交前普通失败进入 `OutOfSync`；rollback、failure presenter 或所有权不变量失败进入 `Faulted`；停止经过 `Stopping -> Stopped`。
4. Candidate ownership：`AdapterOwned -> OutletTransactionOwned -> EntryOwned -> Released`，拒绝路径允许 `OutletTransactionOwned -> Released`。
5. Bounded serial lane：`Idle -> Running -> Idle`；运行时最多一个 in-flight，pending 达上限时状态不变并拒绝最新请求。

`OutOfSync` 表示外部真相仍有效、当前 UI 可以显式重试；`Faulted` 表示局部不变量或恢复能力已被破坏，不再接收新工作。状态名称和终态行为属于兼容合同。

## 6. Outlet 事务和线程

同一 Outlet 严格 FIFO，不同 Window/Outlet 可以并行。顺序固定为：leave guard；UI 线程临时安装候选并订阅真实 visual 事件；UI 线程外 activation；原子发布 Entry；释放旧 Entry。

最终提交前失败必须在 UI dispatcher 恢复旧 physical content，并释放候选；恢复失败使 Outlet `Faulted`。最终提交后旧 Entry 清理失败只记录 `AUCPRS037`，不得回滚新 Entry。failure presenter 自身失败记录 `AUCPRS038` 并使 Outlet `Faulted`。

`CommitAsync` 可从任意线程调用。只有 Avalonia 对象创建、读取、写入、DataContext、事件订阅和 VisualTree 修改进入 UI dispatcher；DI scope、ViewModel factory、guard、activation/deactivation 和诊断组装不得在 UI 线程或框架锁内执行。使用 Avalonia 原生 priority，不公开 City priority abstraction。

caller token 在入队前可拒绝；入队后只取消调用者等待，不删除已接受事务。plan 的 lifecycle token 可在最终提交前取消事务。Runtime stopping 仍允许已接受事务和清理使用 dispatcher，但拒绝新 Window/Outlet 工作。

## 7. 有界队列

- 每个 Outlet：1 个 in-flight，默认最多 32 个 pending。
- 每个 Window 的模态 Interaction：1 个 in-flight，默认最多 8 个 pending。
- 非模态 Interaction 不进入模态队列；不同 Window 的模态队列彼此独立。
- 容量通过 `PresentationQueueOptions` 显式配置，必须为正数。
- 满载时拒绝最新请求，不丢弃旧请求、不自动重试。Outlet 返回 `OutletQueueFull` 并释放候选；Interaction 返回 Failed(`InteractionQueueFull`)。
- `PresentationQueueSnapshot` 提供 capacity、pending、in-flight、peak pending 和 rejected count；拒绝分别记录 `AUCPRS040/041`。

## 8. Window 关闭

关闭来源稳定区分为 `User`、`Application`、`OperatingSystem`。用户关闭和普通应用关闭可以被 guard 拒绝；Host shutdown、Dispose 和 OS shutdown 不可拒绝。并发 Close/Dispose/Stop 合并到同一个事务，caller token 只取消各自等待。

可拒绝关闭先运行所有当前 Entry 的 `ICanDeactivate`，然后最多运行一个 `IConfirmDeactivate`。同一 Window 同时存在多个 confirmation owner 时，不弹出任何确认 UI，拒绝关闭并记录 `AUCPRS043`；Presentation 不替开发者编排多层确认框。

非 OS 原生 Closing 先取消，异步事务允许后标记 committed 并重新调用 `Window.Close`。OS shutdown 不阻止原生关闭，只做 best-effort 清理。硬杀进程、断电等没有 managed callback 的场景不在合同内。

## 9. Visual、Interaction 和 Validation

Bind/Unbind 不是 Attached/Detached。只有 Avalonia 真实 `AttachedToVisualTree/DetachedFromVisualTree` 等白名单事件才能产生物理回执；每次回执携带并匹配 Window、Outlet、Operation、Entry 和 View identity。物理回执不改变 activation 真相，不自动写 State 或发布 EventBus。

Interaction 的可见 UI 由应用 Handler 实现。Presentation 只负责 Activation/Route、Window、Presentation 三层从近到远解析、UI dispatch、生命周期和队列。Validation bridge 默认不接管任何字段；规则、回调、是否使用以及成功/失败样式均由应用决定。

## 10. View、插件和生成器

View lookup 使用 `ViewModel Type + ViewKey` 精确键。默认重复注册失败；`ReplaceExisting` 形成 owner 覆盖栈，撤销顶层时恢复上一层。1.0 source generator 只生成 View registrar；Interaction、资源和插件 descriptor 不属于 Presentation generator 1.0。

插件 View、resource 和 handler 必须携带 plugin/contribution owner 并具有 revoke 路径。资源字典撤销是通用 UI 资源生命周期合同，不承载 culture 或文案语义。

## 11. 平台和验收

Presentation 1.0 正式支持 Windows。Linux/macOS 是 build-only experimental，不得宣称运行时支持。

日常 CI 必须通过 unit、generator、Avalonia.Headless 和 10,000 操作压力测试；单压力场景 watchdog 为 30 秒，结束时 pending/in-flight 必须为 0，ownership 资源恰好释放一次，弱引用在最多三轮强制 GC 后可回收，压力段 retained memory 不超过 8 MiB。发布候选还必须运行真实 Windows desktop smoke。基准相对已批准 baseline 的时间回退不得超过 15%，分配回退不得超过 10%。

批准 baseline 只在固定 Windows 开发机产生，包含操作系统、CPU、架构、SDK、Runtime 和候选内容指纹；环境不匹配不得比较或自动更新。共享 Windows CI 只运行 benchmark 完整性验证。发布候选还必须冻结 Public API、通过 strict package validation 和 SourceLink，并运行完全脱离 ProjectReference 的本地 NuGet package consumer。

Feature、API card、诊断码、测试证据和兼容性文档必须在同一变更中同步；未通过发布门禁只能标记 Implemented，不能标记 Release Verified。
