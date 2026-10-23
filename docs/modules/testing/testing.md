# AtomUI.City.Testing Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 生产项目不得引用 Testing。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 测试不得依赖固定 `Task.Delay`。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 释放、取消、unload、dispatcher 和 generated output 必须有断言。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-TESTING-001 | Unit | TestHostTests | 断言 service、diagnostics、dispose、records。 | 配置非法、Dispose 后使用、未释放资源必须失败。 | Required |
| AUC-TESTING-002 | Unit | FakeUiDispatcherTests | 断言 queue、UI 线程识别、异常、pending count。 | work exception、取消、未 drain 都可断言。 | Required |
| AUC-TESTING-003 | Unit | SharedTestUtilitiesTests | 断言虚拟时间推进、任务顺序、异常记录。 | 负 duration、任务异常、取消任务稳定失败或诊断。 | Required |
| AUC-TESTING-004 | Unit | ModuleTestHostTests | 断言 module graph、lifecycle、diagnostics。 | 循环依赖、初始化失败、shutdown 失败可断言。 | Required |
| AUC-TESTING-005 | Unit | PluginTestHostTests | 断言 load/unload、contribution、owner revoke。 | manifest 错误、unload 后泄漏、依赖冲突可断言。 | Required |
| AUC-TESTING-006 | Unit | RoutingTestHostTests | 断言 route build、match、navigation helper。 | route 冲突、匹配失败、guard deny 可断言。 | Required |
| AUC-TESTING-007 | Unit | SourceGenerationTestKitTests | 断言 generated source snapshot、diagnostics、references。 | 编译失败、输出变化、diagnostic 缺失可断言。 | Required |
| AUC-TESTING-008 | Unit | AotSourcePatternCheckTests | 断言 ordinal substring、自定义/default pattern、重复规则、取消以及明确的启发式边界。 | 注释/字符串假阳性、别名/封装/依赖风险假阴性必须固定为已知边界；任何“AOT compatible”结论失败。 | Implemented |
| AUC-TESTING-009 | Unit | TestLayerTests | 断言 Unit/Contract/Integration/Platform/Dogfood 分层标记。 | 测试缺少 layer 或 layer 非法时门禁失败。 | Required |
| AUC-TESTING-010 | Unit / Contract | DataTestDoublesTests | 断言 FIFO 响应、invocation/credential/handler records、连接 lifecycle/count 和 Data result 映射。 | 空脚本、取消、委托异常、非法 owner/kind、响应类型不匹配。 | Implemented；failure-path 扩展仍为持续门禁 |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
