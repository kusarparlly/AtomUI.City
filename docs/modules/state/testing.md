# AtomUI.City.State Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 状态写入先完成原子提交，再通知订阅者。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 默认不隐式切 UI 线程。 | 至少一个明确测试断言，不能只断言流程成功。 |
| StateSnapshot 创建后不可变。 | 至少一个明确测试断言，不能只断言流程成功。 |
| ComputedState 不能形成循环依赖。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 插件 state definition、subscription 和 snapshot provider 必须绑定插件 owner。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-STATE-001 | Unit | WritableStateTests | 断言原子更新、version、提交后通知、相等值不通知、订阅 dispose、disposed mutation rejection、updater 异常诊断和写拒绝/access policy。 | updater 拒绝或失败、handler 失败、disposed state、ReadOnly 写拒绝。 | Completed |
| AUC-STATE-002 | Unit | ApplicationStateTests; StateDefinitionTests; StateFactoryTests | 断言注册、读取、DI、factory/scope accessor、五种 access policy writer、not registered、StateDefinition enum/schema/authorization metadata 边界。 | 未注册、重复注册、身份或 capability 写入拒绝、Update null updater。 | Completed |
| AUC-STATE-003 | Unit | ComputedStateTests | 断言 lazy invalidation、依赖失效、缓存、首次异常重抛、循环依赖拒绝、锁外计算、异常诊断、null dependency 拒绝。 | compute 异常、循环依赖、并发 dispose、null dependency、依赖订阅释放。 | Completed |
| AUC-STATE-004 | Unit | StateScopeTests; StateThreadingTests | 断言 dispose 后不通知、Background 不阻塞状态提交、Background handler 失败诊断。 | 重复释放、owner dispose、callback 失败、Dispatcher pending callback 释放后到达。 | Completed |
| AUC-STATE-005 | Unit | StateSnapshotTests | 断言不可变、过滤、restore diagnostics、scope kind、entry version/schema 边界、entries 不含 null。 | 版本/scope 不兼容、policy 拒绝、restore 失败、entry 边界非法。 | Completed |
| AUC-STATE-006 | Unit | StateCollectionTests | 断言 change kind、item version、collection version、重复 key 合并、仅快照版本差异不回退、快照不可变和 dispose 合同。 | 重复 key、missing key、clear、disposed collection、null key、未知 change kind、负 version。 | Completed |
| AUC-STATE-007 | Unit | StateDiagnosticsTests; StateThreadingTests | 断言 AUCSTA001-011、唯一性、格式、severity 和定位 context。 | handler、queue overflow、update、restore、dispose、access failure、diagnostics collector 缺失。 | Completed |
| AUC-STATE-008 | Unit | StateThreadingTests | 断言不隐式 UI、并发写、锁外通知、dispatcher unavailable diagnostics、延迟策略有界串行 FIFO。 | 并发写、调度器不可用、队列溢出、未完成 dispatcher operation、延迟回调释放。 | Completed |
| AUC-STATE-009 | Unit/Contract/RuntimeLifecycle | WritableStateTests; ComputedStateTests; ApplicationStateTests; StateCollectionTests; StateThreadingTests | 断言初始快照恰好一次、注册边界不漏更新、版本顺序、锁外回调、集合快照、Dispose 竞态和模块 Writer 工厂。 | 并发注册/写入、pending initial dispose、容量 1 队列、OwnerWrite authority。 | Completed（State 154/154；Public API 4/4） |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
