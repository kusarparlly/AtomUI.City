# AtomUI.City.EventBus Testing

本文件与 [features.md](features.md) 一一对应，是 EventBus 九个 Feature 的唯一验收矩阵。Application Plane 已验证；涉及真实 PluginSystem 的集成状态单独保留为 Pending。

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 生命周期、线程、插件、dispatcher、generator、build、NativeAOT 和真实进程行为必须有专项测试。
- 诊断测试必须断言稳定 code 和最小定位 context，不能只断言出现日志。
- 释放、取消、超时、撤销、Unload 和 Dispose 后行为必须有断言。
- 并发测试必须包含可控 barrier、高重复次数和唯一事务断言，不能用偶发 timing 作为证据。
- CLI dogfood 验证真实产品组合，但不能替代 Feature 级测试。
- Headless、NativeAOT 和外部包消费者进程必须遵守 `TEST-INFRA-001`：顶层异常写入 `stderr` 并返回非零退出码，启动器有界等待、收集双输出并在 Windows 下抑制仅属于测试子进程的系统错误框；不得吞异常或修改全局 WER。

## 完成口径

- `AUC-EVENTBUS-001` 至 `AUC-EVENTBUS-008` 全部 `Verified`，才能认定 Application Plane MVP 通过。
- `AUC-EVENTBUS-009` 的 EventBus 侧 contract tests 在本模块完成；真实动态插件生命周期测试等待 PluginSystem，并且只有两侧证据都通过才能把 Plugin Plane 标记为 `Verified`。
- 性能 Benchmark 初期作为观察门禁；稳定基线形成后再为分配量、吞吐和复杂度趋势设置阻断阈值。

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-EVENTBUS-001 | Unit/RuntimeLifecycle/Dogfood | EventPublicationTests | PublishAsync/PostAsync 完成语义、EventId、因果链、快照时点、无订阅者、取消和 immutable result。 | null/default、非法 options/result、预取消、bus/channel 停止、Post 拒绝。 | Verified |
| AUC-EVENTBUS-002 | Unit/RuntimeLifecycle | EventSubscriptionTests | 唯一 owner、隔离释放、scope/lease 绑定、状态、原子注册/撤销、quiescing、drain 和唯一终止事务。 | stop/注册竞争、owner cancellation、并发终止、超时、handler 创建/释放失败和 Faulted。 | EventBus Contract Verified / PluginSystem Integration Pending |
| AUC-EVENTBUS-003 | Unit/Contract | EventContractRegistryTests; EventBusRegistrationTests | 稳定 ContractId、双向匹配、排序快照、Register/Freeze 原子竞争和生产冻结。 | 重复/default id/type、冻结后写入、未知 contract、错误 ALC 和 descriptor 丢失。 | Verified |
| AUC-EVENTBUS-004 | Unit/RuntimeLifecycle | EventDispatchAndFailurePolicyTests | 线程策略、dispatcher、scheduler、delivery status、错误策略、取消、timeout、cleanup 聚合和后台错误观察。 | dispatcher 不可用、handler failure/cancellation/timeout、lingering handler 和失败策略。 | Verified |
| AUC-EVENTBUS-005 | Unit/Contract | EventDiagnosticsTests | 稳定诊断、因果链、context、metrics、限流、payload 投影和 sink 隔离。 | dropped/coalesced/rejected、递归、timeout、capability、sink 失败和插件残留。 | Verified |
| AUC-EVENTBUS-006 | FrameworkIntegration/RuntimeLifecycle | EventBusHostIntegrationTests | Module DI、Host-managed 隔离、internal controller、初始化阶段、Start/Stop/Dispose、Stop-before-Start 和 deadline。 | 重复注册、初始化前操作、非法 Scope、启动回滚、并发终止和清理失败。 | Verified |
| AUC-EVENTBUS-007 | Unit/RuntimeLifecycle | EventChannelRuntimeTests | channel 隔离、共享 admission、顺序/并发策略、容量、分区回收、背压、指标、重入和关闭。 | 非法配置、queue full/closed、上限、等待取消/timeout、自身 await 和 shutdown drain。 | Verified |
| AUC-EVENTBUS-008 | Generator/RuntimeLifecycle | EventBusGeneratorTests; EventBusNativeAotProcessTests | Attribute 到 registrar/manifest/Host、owner contribution、编译闭包、对象图、稳定输出、强类型 invoker、scope 释放、trimming 和 NativeAOT。 | owner/ContractId/manifest/catalog/object graph/handler/channel/plane/version 非法。 | Verified |
| AUC-EVENTBUS-009 | Contract/PluginLifecycle | EventBusPluginContractTests | capability、Shared/Private、lease、激活回滚、subscription barrier、quiescing、deadline drain、配额和 ALC 回收。 | denied/incompatible、竞争、重入、半激活、忽略取消、drain timeout、重入和缓存残留。 | EventBus Contract Verified / PluginSystem Integration Pending |

## 横向产品验收

统一执行入口为 `bash engineering/check-eventbus-release.sh`；它固定执行 Release 构建、范围内格式门禁、全量/专项/20 轮压力测试、双 TFM NativeAOT、Public API、本地 NuGet 外部消费、Benchmark 及候选指纹。

Application Plane MVP 还必须通过：

- 五个以上 Module、二十个以上 handler、多个 channel/partition 的真实 CLI 组合测试。
- 发布、订阅、Stop、Dispose、owner cancellation 和配置更新的高并发随机化测试。
- Host 冷启动、首次/重复 publish、队列压力、diagnostics on/off 和大规模订阅的 Benchmark。
- `net8.0` 与 `net10.0` 的 Release build、trimming 和 Windows NativeAOT 真实进程测试。
- Public API baseline、XML 文档、SourceLink 和 package validation。
- `bash engineering/check-eventbus-package-consumer.sh` 使用本地 NuGet 源、独立包缓存和无 `ProjectReference` 的 net8 外部消费者，验证打包后的 Module manifest 及 Host/EventBus 真实行为。
- 故障注入进程以非零退出码和完整异常栈结束，自动化期间不出现需要人工处理的 Windows 错误弹窗。

### Benchmark 观察门禁

首份可重复基线由 `benchmarks/AtomUI.City.EventBus.Benchmarks` 提供，使用 Release 构建执行：

```powershell
bash engineering/check-eventbus-benchmarks.sh
```

当前场景覆盖无订阅/单订阅/20 订阅 Publish、1/64/256 个已实例化 channel 下的重复 Publish、达到全局 runtime 上限后的拒绝成本、diagnostics on/off、订阅建立释放和 Wait/Reject burst。入口会拒绝零执行和无有效统计结果，并要求生成 CSV 证据。Benchmark 结果是观察数据，不提交机器相关的绝对耗时作为兼容性承诺；发布候选之间必须比较吞吐、延迟和分配量趋势，出现数量级退化时阻断并调查。

## 缺口处理

无法由当前依赖模块完成的测试必须明确登记责任方，不能用 smoke test 替代：

- UI Dispatcher 集成由 EventBus fake dispatcher 先验证合同，Presentation 建成后补真实 UI 测试。
- Plugin Plane 由 EventBus contract fake 先验证边界，PluginSystem 建成后补动态 ALC 生命周期测试。
- Generator/NativeAOT 是 Application Plane MVP 阻断门禁，不能延期为普通集成工作。
