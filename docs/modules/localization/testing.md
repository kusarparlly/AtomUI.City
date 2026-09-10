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
| AUC-LOCALIZATION-001 | Contract / State | CultureStateTests; LocalizationServiceTests | 断言 City.State 订阅、默认 culture、深只读 culture/集合快照、多节点 fallback、任意长度 cycle、fallback lazy load package id 和重复切换。 | 非法 culture、null/空集合元素、self/multi-node cycle、重复设置。 | Completed |
| AUC-LOCALIZATION-002 | Contract / Integrity | LanguagePackageProviderTests; LocalizationRegistrationTests | 断言三个默认 provider、provider kind mismatch、custom provider 冲突、原子 `RegisterRange`、`(culture, packageId)`、必填 schema/version/id/culture/SHA-256/path-root/resource/枚举校验、16 MiB 上限、重复根属性、取消与 owner revoke。 | 格式错误、超限、重复 JSON 属性、checksum/schema/path/provider kind 不匹配、取消、重复 package、owner 已撤销。 | Completed |
| AUC-LOCALIZATION-003 | Contract / Concurrency / Lifecycle | LocalizationServiceTests | 断言按需加载、共享 load、waiter 取消隔离、失败 fallback、缓存复用/替换、fallback cache state 同步以及并发 Dispose 共享完成任务。 | provider 抛异常、取消污染、inactive completion、忽略取消的 in-flight load。 | Completed |
| AUC-LOCALIZATION-004 | Contract / Concurrency | LocalizationServiceTests; LocalizationRegistrationTests | 断言 scope id、inactive scope、lease/context、动态 scoped fallback、跨 scope fallback 隔离、AUCLOC002、任意 formatter 异常 raw fallback、refresh FIFO、取消传播、Dispose/notification 竞态及 callback 子任务不继承失效重入标记。 | scope 未激活、缺失 key、格式错误、handler 自释放与外部并发释放。 | Completed |
| AUC-LOCALIZATION-005 | Contract / PluginLifecycle | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests | 断言独立 assembly、属性映射、同一/collectible ALC、资源读取、缺失资源和 unload。 | assembly load 失败、manifest 缺失、资源缺失、Default ALC 污染。 | Completed |
| AUC-LOCALIZATION-006 | Contract | LocalizationServiceTests | 断言可选 application bridge 调用、局部失败、批量刷新、提交后调用方取消仍完成全部刷新和不依赖 Avalonia 类型。 | application bridge 失败、target 局部失败、bridge 内触发调用方取消。 | Completed |
| AUC-LOCALIZATION-007 | Contract / Concurrency | LocalizationServiceTests; LocalizationRegistrationTests | 断言撤销后不可 lookup、旧 snapshot 稳定、持有中的文本刷新、提交后调用方取消仍完成刷新、重复 revoke、fallback state 清理、load/revoke 竞态不复活 package，以及 bridge 回调重入快速失败。 | unload 与 lookup/culture switch 并发、mutation callback 重入、撤销提交后取消。 | Completed |
| AUC-LOCALIZATION-008 | Generator / Build | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationMetadataReaderTests; LocalizationManifestBuilderTests | 断言 Generator 主入口输出可编译 registrar、原子 `RegisterRange`、规范化 culture/package identity、key、scope id、load context、contribution id、critical keys 和稳定顺序。 | 空 attribute 参数、显式未知 enum、invalid culture、missing/ambiguous package、duplicate identity/key、scope mismatch、缺少 ScopeId/resource base name、fallback cycle、无执行合同 resource kind。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
