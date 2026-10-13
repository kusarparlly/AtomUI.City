# AtomUI.City.State Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 状态写入先完成原子提交，再通知订阅者。
- 默认不隐式切 UI 线程。
- StateSnapshot 创建后不可变。
- ComputedState 不能形成循环依赖。
- 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `WritableState<T>` 的 optional `stateName`、`access` 构造参数、ReadOnly 写拒绝、`AUCSTA004` 诊断、原子提交后通知、相等值不通知和 Dispose 后 mutation 拒绝进入 1.0 兼容承诺。
- `ApplicationStateRegistry` 的显式注册、未注册诊断、重复注册诊断、读写入口分离、ReadOnly 写拒绝、Update null updater 优先拒绝和 `StateDefinition<T>` enum/schema 边界进入 1.0 兼容承诺。
- `ComputedState<T>` 的 lazy invalidation、缓存、依赖变更通知、compute 失败诊断、Dispose 后读取拒绝、null dependency 先校验后订阅和依赖订阅释放进入 1.0 兼容承诺。
- `ComputedState<T>` 首次计算失败重新抛出原异常、循环依赖拒绝、锁外计算和失效世代提交校验进入 1.0 兼容承诺。
- `StateSubscriptionOptions` 的延迟队列容量默认值为 1024；溢出丢弃最旧通知并记录 `AUCSTA011`。
- `StateSnapshotEntry.ScopeKind` 及 restore scope kind 校验进入 1.0 快照合同。
- `StateSubscriptionOptions` 的 Immediate、Dispatcher、Background、Queued 调度语义、subscription Dispose 幂等、延迟回调释放抑制、handler 失败诊断和 StateScope 反向释放进入 1.0 兼容承诺。
- `StateDispatchPolicy` 的 `Immediate=0`、`Queued=1`、`Dispatcher=2`、`Background=3` 枚举值，以及不可用 dispatcher 失败被诊断隔离且不回滚已提交状态进入 1.0 兼容承诺。
- `StateSnapshot` 和 `StateSnapshotEntry` 的不可变 entries、version/schema 边界、Persisted 过滤、restore 诊断、owner/plugin/type/schema 校验和 Transient restore 拒绝进入 1.0 兼容承诺。
- `StateCollection<TKey,TItem>` 的 collection version、item version、快照不可变、无变化不通知、Dispose 幂等、Dispose 后读 API 可用、mutation/restore/subscription API 拒绝，以及 `StateCollectionChange`、`StateCollectionSnapshot` 和 `StateCollectionChangedEventArgs` 的 null、空列表、未知 change kind 与负 version 边界进入 1.0 兼容承诺。
- `StateDiagnosticIds` 的 `AUCSTA001` 到 `AUCSTA011` code、severity 语义和稳定定位 context key 进入 1.0 兼容承诺。
- `Changed` 是同步 CLR event，先于可调度的 `OnChange` subscriptions；`OnChange` 返回 `IStateSubscription` 并承担调度与 Scope 生命周期语义。
- `Dispatcher` subscription 使用非阻塞投递，不允许同步等待 UI callback。
- `StateWriteAuthority`、`StateWriteAuthorityKind` 和五种 `StateAccessPolicy` 的身份/capability 判定进入 1.0 兼容承诺。
- Host authority 铸造限于框架内部（internal）；Module/Plugin authority 为进程内自声明凭据——访问策略是纪律级约束而非安全边界（与插件系统"AssemblyLoadContext 不是安全边界"的立场一致），跨信任边界隔离必须依赖进程隔离。
- `IStateScopeAccessor`、`StateScopeAccessor`、`IStateFactory`、`StateFactory` 和 `AddState()` DI 入口进入 1.0 兼容承诺。
- Snapshot 恢复失败保留恢复前的当前值和 version，并写入 `AUCSTA007`。

## AUC-STATE-009 兼容性说明

- `OnChange(...)`、`Changed` event 及既有构造函数保持原语义，不自动回放当前值。
- `Observe(...)` 是纯新增 API；只有显式调用者会收到 `IsInitial=true` 的初始通知。
- `StateChangedEventArgs.IsInitial` 对既有变更通知恒为 `false`。
- `IApplicationStateWriterFactory` 是新增 DI 服务；现有 `IApplicationStateWriter` Host writer 注册暂时保留，避免 Preview 使用者无迁移窗口。
- 并发情况下通知顺序收紧为按提交版本 FIFO；单线程 Immediate 行为保持同步投递。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
