# AtomUI.City.Mvvm Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。 | Assembly 和 API 测试必须断言无 Presentation/Avalonia visual 依赖。 |
| Interaction 只表达请求，展示和 handler 注册由 Presentation 承担。 | Interaction 测试必须断言无 handler 返回 `NotHandled`，handler 异常返回 `Failed`。 |
| Command、Activation、Interaction、Validation 都必须有取消和失败结果语义。 | 必须覆盖取消、异常、重复调用和 Dispose 后行为。 |
| ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。 | activation/deactivation 测试必须覆盖 CanDeactivate 拒绝。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-MVVM-001 | Contract | ViewModelBaseTests; MvvmAssemblyTests | 断言 PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。 | Dispose 后 mutating API、重复通知、空 property name。 | Completed |
| AUC-MVVM-002 | Contract | ActivationScopeTests; DeactivationTests; ViewModelBaseTests | 断言状态机、拒绝停用、取消、异常映射和资源释放。 | 激活失败、CanDeactivate 拒绝、取消、重复 Dispose。 | Completed |
| AUC-MVVM-003 | Contract | CommandTests | 断言成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。 | execute 异常、并发执行、取消、CanExecute 变更。 | Completed |
| AUC-MVVM-004 | Contract | InteractionTests | 断言有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。 | 无 handler、handler 异常、取消后结果。 | Completed |
| AUC-MVVM-005 | Contract | ValidationScopeTests | 断言消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。 | 未知 field、重复 message、Dispose 后更新。 | Completed |
| AUC-MVVM-006 | Contract | CommandTests | 断言状态转换、取消顺序、重复终态、耗时字段和资源释放。 | 重复 Complete/Fail/Cancel、外部取消、Dispose 后 mutation。 | Completed |
| AUC-MVVM-007 | Unit/Concurrency | ActivationScopeTests | 断言任务准入、唯一 key、活动统计、关闭、取消、drain、释放顺序、回收及诊断故障隔离。 | 重复 key、关闭竞态、同步/异步异常、不合作任务超时、throwing diagnostics。 | Implemented / Release Validation Pending |
| AUC-MVVM-008 | Contract | CommandTests; Public API tests | 断言泛型参数、业务 CanExecute、通知、ExecutionTask 发布顺序和 OperationExecutionResult。 | null/非法参数、predicate 异常、API rename 漂移、旧 Task 可见性。 | Implemented / Release Validation Pending |
| AUC-MVVM-009 | Unit/Concurrency | CommandTests; ViewModelBaseTests | 断言 Reject/Allow/RejectByKey、活动计数、原子准入、Scope A/B、activation 通知隔离及同步释放恢复。 | 同 key race、inactive、deactivating、Scope dispose race、订阅者异常、异步资源同步释放误用。 | Implemented / Release Validation Pending |
| AUC-MVVM-010 | Contract/Failure injection | CommandTests | 断言 await 原异常、ICommand handler 恰好一次、取消排除、诊断故障隔离和无磁盘副作用。 | handler 异常、throwing diagnostics、重复观察、fire-and-forget UI path。 | Implemented / Release Validation Pending |
| AUC-MVVM-011 | Contract/Platform integration | CommandTests; UiCommandFactoryTests | 断言四个状态属性在开始/完成阶段经 dispatcher 交付、Headless 同步语义、faulted ValueTask 被观察、真实 Avalonia UI 线程交付。 | inline/queued 订阅者异常、Runtime Stopped/Faulted、完成/失败/取消结果隔离。 | Implemented / Release Validation Pending |
| AUC-MVVM-012 | Unit/Concurrency/Contract | CommandTests; UiCommandFactoryTests; Public API tests; package consumer | 断言跨 Command latest-wins、generation/currentness、立即切换、Operation Canceled、Activation 监督、DI dispatcher 转交和独立 domain 隔离。 | 不合作旧执行、关闭/Dispose 竞态、取消回调失败、drain 超时、throwing diagnostics、domain-first API 漂移。 | Release Verified |

表中 `Status` 是 Feature 的发布成熟度，不只是现有测试是否通过。`AUC-MVVM-007` 至 `011` 仍保持各自既有的 `Release Validation Pending`；不得因同一轮全局回归自动推断它们的专项发布矩阵已闭环。`AUC-MVVM-012` 的发布证据为 MVVM `113/113`、Presentation `171/171`、16 个非平台测试项目 `2224/2224`、10 轮重复竞态测试、Presentation Public API build/pack、API governance `16/16` 及隔离 `win-x64` self-contained package consumer。

## 当前审计缺口

以下历史缺口已由 2026-10-17 发布门禁收口：

- throwing diagnostics 不覆盖原异常、状态提交或 Task completion；
- `ActivationStateChanged` 订阅者失败不阻断生命周期与 Scope 清理；
- 同步 Dispose 误用于异步 Scope 时保留可恢复引用；
- ValidationScope 先于父 Scope 释放时，父 Scope 关闭保持幂等；
- `ExecutionTask` 在属性通知前完成引用发布。

专项新增 `6` 个测试，分布于 `ActivationScopeTests`、`CommandTests`、`ValidationScopeTests` 和 `ViewModelBaseTests`。

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
