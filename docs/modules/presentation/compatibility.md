# AtomUI.City.Presentation Compatibility

## 1.0 稳定面

- `PublicAPI.Shipped.txt` 中的公开类型和成员签名；新签名先进入 Unshipped，删除或改变 shipped 签名必须按 breaking change 处理。
- public API、enum 值、attribute/options 默认值、diagnostic code、generated View registrar 形状。
- 主包只硬依赖 Avalonia；增加 AtomUI 或 Localization 硬依赖是 breaking change。
- `PresentationModule` 与 `AddPresentation` 的等价完整注册。
- `Attach/RegisterWindow` 主路径、Show 前注册、WindowSession/WindowScope 一一对应。
- exact View key、override owner 栈、generated registrar 稳定排序。
- ViewModel ownership、async-only lease、single-use candidate、FIFO/final commit/rollback/cancellation 语义。
- queue 默认容量 32/8、reject-newest、错误分类和 snapshot 字段。
- close origin、唯一 confirmation、并发 Close/Stop 合并和不可拒绝 shutdown。
- Visual identity 过滤、真实事件规则和不自动写 State/EventBus。
- Validation 完全可选且应用拥有规则、文案、回调和样式。
- plugin UI owner/revoke/unload 顺序。
- NuGet 包的 net8.0/net10.0 资产以及 Avalonia/Core/Mvvm/Routing/Security/State 依赖边；AtomUI、Localization、fixture、test 和 benchmark 不得进入主包。
- `IUiCommandFactory` 是 Presentation 的 Preview UI 默认入口；默认实现可保持 internal，但同步/异步/泛型/domain-first 重载、activation/concurrency/exception 参数和 DI dispatcher 注入语义属于兼容面。该接口签名已进入 `PublicAPI.Shipped.txt`，`PublicAPI.Unshipped.txt` 保持无待审签名。
- `IRoutePresentationCoordinator`、plan factory/context/result/status、per-Outlet serialization、receipt-based recovery 与 AUCPRS047-050 是 Preview-frozen 技术协调兼容面。
- `ViewFactoryContext`、`ViewModelAcquisitionRequest`、`RouteOutletCommitResult`、`RouteOutletProperties`、Command/Validation/Visual lifecycle 的公开伴随模型和七个 DI extension container 已进入 Preview-frozen public surface；即使它们无独立 Feature，也不能绕过 Public API review 改名、删除或改变线程/所有权语义。
- active plugin view、Presentation resource lease/contribution/dictionary revocation 以及 unload request/error/result/coordinator 是 Presentation-owned plugin-facing compatibility contract；它们不承诺 PluginSystem 的 load context、依赖解析或完整 unload 已实现。

## 不属于 Presentation 兼容面

- Localization 的 culture、revision、fallback、文案 key/value 和 language package。
- 应用自有 Dialog/Toast/Validation UI、route 到 View/ViewModel/lease 的业务映射、错误等级与用户反馈政策。
- roadmap 候选、测试替身内部结构、diagnostic message 文案。
- Linux/macOS runtime 行为；1.0 仅承诺 build-only experimental。

## Retired before 1.0

原 `PresentationLocalizationBridge`、`Localized*Binding/Target`、culture applier、flow direction applier 以及 AUCPRS018/019 active 语义在 1.0 冻结前移除。类型不构成 1.0 compatibility，但诊断编号保留且不得复用。

## 演进

改变默认值、线程、ownership、取消、提交点、Result/exception、状态终态或关闭可拒绝性均为 breaking change。Deprecated API 必须记录版本、replacement、最早删除版本、迁移方法和 Analyzer diagnostic。

1.0 Preview 取消合同收口移除 `RouteOutletCommitPlan.LifecycleToken` 与 `Replace(..., lifecycleToken)`；`IRouteOutlet.CommitAsync` 成为唯一取消入口。`IPresentationRuntime.StopAsync` 和 `WindowSession.CloseAsync` 的 wait-only 参数改名为 `waitCancellationToken`。

`AUC-PRESENTATION-017` 已落地：Root/Child 只作为显式 lease 角色，不增加 MVVM 基类、全局 Root 单例或反射发现。`ViewModelCompositionOwner`、`ViewModelCompositionLease`、lease identity、owner 边界、异步释放和 leaf-to-root 顺序进入 1.0 Preview 兼容面。

ADR 0014 引入双入口：MVVM `CommandFactory` 继续保持 Headless 兼容；Presentation `IUiCommandFactory` 成为 UI 推荐入口。ADR 0015 的 domain-first 泛型/非泛型重载已同步到该接口。删除 UI 工厂或 domain 重载、恢复逐调用 dispatcher 参数、让工厂绕过 DI dispatcher 或复制 domain 状态机均属于 breaking change。

ADR 0016 把重复且易错的 Router commit -> Outlet commit -> conditional restore 提升为可复用技术协调层，但没有吸收应用业务映射。让协调器无条件 rewind、覆盖更新 navigation、回放用户操作、隐式发现 View 或接管错误展示均违反该兼容边界。
