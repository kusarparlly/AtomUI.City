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
| Interaction 只表达请求，展示和 handler 注册由 Presentation 承担。 | Interaction 测试必须断言无 handler 返回失败。 |
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
| AUC-MVVM-007 | Unit/Concurrency | ActivationScopeTests | 断言任务准入、唯一 key、活动统计、关闭、取消、drain、释放顺序和回收。 | 重复 key、关闭竞态、同步/异步异常、不合作任务超时。 | Completed |
| AUC-MVVM-008 | Contract | CommandTests; Public API tests | 断言泛型参数、业务 CanExecute、通知和 OperationExecutionResult。 | null/非法参数、predicate 异常、API rename 漂移。 | Completed |
| AUC-MVVM-009 | Unit/Concurrency | CommandTests; ViewModelBaseTests | 断言 Reject/Allow/RejectByKey、活动计数、原子准入、Scope A/B 及 activation 导致的 CanExecuteChanged。 | 同 key race、inactive、deactivating、Scope dispose race。 | Completed |
| AUC-MVVM-010 | Contract/Failure injection | CommandTests | 断言 await 原异常、ICommand handler 恰好一次、取消排除和无磁盘副作用。 | handler 异常、重复观察、fire-and-forget UI path。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
