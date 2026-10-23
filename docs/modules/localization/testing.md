# AtomUI.City.Localization Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 语言包按当前 culture 和 fallback chain 懒加载，不在启动时全量加载。 | 必须断言未使用 culture 不加载。 |
| 每种语言自己的语言包独立缓存、独立诊断、独立撤销。 | 必须断言不同 culture 缓存隔离。 |
| 语言包可以来自 Host、模块、插件或独立 assembly，但都必须有 owner。 | 必须断言 owner revoke 后不可 lookup。 |
| 缺失 key 必须诊断并走 fallback，不允许静默返回空字符串。 | 必须断言 diagnostics 和 fallback 顺序。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-LOCALIZATION-001 | Contract | CultureStateTests; LocalizationServiceTests | 断言 City.State 订阅、默认 culture、只读快照、fallback、cycle、lazy load 和重复切换。 | 非法 culture、null/空集合元素、cycle、重复设置。 | Completed |
| AUC-LOCALIZATION-002 | Contract | LanguagePackageProviderTests; LocalizationRegistrationTests | 断言 provider、原子注册、identity、schema/hash/path/resource/枚举、大小上限、取消与 revoke。 | 格式、超限、重复属性、checksum/schema/path/kind、取消和重复 package。 | Completed |
| AUC-LOCALIZATION-003 | Contract/RuntimeLifecycle | LocalizationServiceTests | 断言按需/共享 load、waiter 取消、fallback、缓存、SynchronizationContext 与并发 Dispose。 | provider 异常、取消污染、inactive completion、in-flight load 和同步死锁。 | Completed |
| AUC-LOCALIZATION-004 | Contract/RuntimeLifecycle | LocalizationServiceTests; LocalizationRegistrationTests | 断言 scope、lease/context、fallback 隔离、格式失败、refresh FIFO、取消和 Dispose/notification 竞态。 | inactive scope、缺失 key、格式错误、自释放和并发释放。 | Completed |
| AUC-LOCALIZATION-005 | Contract/PluginLifecycle/NativeAOT | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests; NativeAOT Runtime Matrix | 断言已知 SourceAssembly、属性、资源读取、owner revoke、默认 provider 的双 TFM AOT 发布与原生运行。 | 缺少 SourceAssembly、仅 path descriptor、manifest/resource 失败和任何动态 DLL 加载。 | Verified |
| AUC-LOCALIZATION-006 | Contract | LocalizationServiceTests | 断言 application bridge、局部失败、批量刷新、提交后取消和无 Avalonia 依赖。 | bridge/target 失败和回调取消。 | Completed |
| AUC-LOCALIZATION-007 | Contract/RuntimeLifecycle | LocalizationServiceTests; LocalizationRegistrationTests | 断言 revoke、snapshot、文本刷新、取消、fallback 清理、load/revoke 竞态和重入。 | unload/lookup/culture switch 并发、重入和提交后取消。 | Completed |
| AUC-LOCALIZATION-008 | Generator | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationMetadataReaderTests; LocalizationManifestBuilderTests | 断言 registrar、原子注册、identity、key/scope/load context/contribution 和稳定顺序。 | attribute、enum、culture、package、identity/key、scope 和 fallback 非法。 | Completed |
| AUC-LOCALIZATION-009 | Build/TemplateSmoke | ManifestTaskTests; GenerationTemplateRendererTests | RESX 生成 locpack/attribute source，模板 build 后加载真实资源。 | 非字符串/空资源、DTD、metadata、重复 identity/resource 和卫星程序集残留。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
