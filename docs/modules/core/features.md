# AtomUI.City.Core Features

本文件是 Core 模块 Feature 数量、编号和状态的权威清单。当前 Core 1.0 合同共定义 8 个 Feature（`AUC-CORE-001` 至 `AUC-CORE-008`），状态为 `8/8 Verified`。没有 Feature ID 的功能不能进入实现；新增、拆分、合并或变更 Feature 状态时，必须先更新本清单，并同步更新 [testing.md](testing.md) 的测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | 公开合同 | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-CORE-001 | Application Host Builder | Verified | ApplicationHost, ApplicationHostOptions, IApplicationHostBuilder, IApplicationHost, IApplicationContext；internal implementation: ApplicationHostBuilder | ApplicationHostBuilderTests; ApplicationHostRuntimeTests; ApplicationHostIndustrialLifecycleTests |
| AUC-CORE-002 | Lifecycle Pipeline | Verified | LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage | ApplicationHostRuntimeTests; LifecycleMiddlewarePipelineTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests |
| AUC-CORE-003 | Lifecycle Scope Tree | Verified | LifecycleScope, LifecycleScopeKind, LifecycleScopeState | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | Verified | ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext | ModuleBaseTests; ModuleDescriptorTests; ApplicationHostModuleLifecycleTests; ApplicationHostIndustrialLifecycleTests |
| AUC-CORE-005 | DI Registration Markers | Verified | ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, ServiceRegistrationOwnerAttribute, GeneratedServiceManifestAttribute, IServiceRegistrar, IServiceRegistrarContext, 标记接口 | ServiceRegistrationAttributeTests; AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests; CoreMvpCliProcessTests; Core MVP NativeAOT |
| AUC-CORE-006 | Host Diagnostics | Verified | IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds | HostDiagnosticsTests |
| AUC-CORE-007 | UI Dispatcher Contract | Verified | IUiDispatcher, UnavailableUiDispatcher | UiDispatcherIntegrationTests |
| AUC-CORE-008 | Generated Module Catalog | Verified | ApplicationModuleAttribute, GeneratedModuleManifestAttribute, IModuleRegistrar, IModuleRegistrarContext | GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests; NativeAOT headless publish/run |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。 | 必须有实现、测试或工程门禁证据。 |
| ApplicationHostBuilder Build 后必须冻结服务注册入口。 | 必须有实现、测试或工程门禁证据。 |
| LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。 | 必须有实现、测试或工程门禁证据。 |
| StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则，Stopped 后再次 Start 必须失败。 | 必须有实现、测试或工程门禁证据。 |
| 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。 | 必须有实现、测试或工程门禁证据。 |
| IUiDispatcher 只定义抽象，Core 不提交真实 UI work。 | 必须有实现、测试或工程门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-CORE-001 Application Host Builder

Feature ID: `AUC-CORE-001`
Status: Verified
Default ServiceProvider 配置：应用可以通过 `UseDefaultServiceProvider` 注册有序的 `ServiceProviderOptions` 回调。回调随 Builder 一起冻结，并在 `HostApplicationBuilder.Build` 前应用到由 Generic Host 持有的唯一 `DefaultServiceProviderFactory`；未使用该 API 时保持 Generic Host 默认行为。
Goal: 构建 Host、冻结注册入口、创建不可变应用实例描述符。
Public Contract: ApplicationHost, ApplicationHostOptions, IApplicationHostBuilder, IApplicationHost, IApplicationContext
Internal Contract: ApplicationHostBuilder（`ApplicationHost.CreateBuilder` 返回的默认实现，不属于 CLR public surface）
Runtime / Build Behavior: 验证应用身份、版本和路径，构建 Host，递归冻结所有通过 Builder 逃逸的 Configuration mutation handle，并在 Build 阶段一次性创建 IApplicationContext。
Failure Behavior: Build 成功或失败后继续注册、通过已捕获 section/child/root/provider 修改配置或主动 Reload 均失败；重复 Build 行为明确；缺失 ApplicationId、无法解析版本或非法 ApplicationName/路径时 Build 失败并写 AUCHOST101；回滚清理失败不阻止其他资源释放，原始 Build 异常与全部 cleanup failure 聚合返回并写 AUCHOST109；异步模块清理不得因调用线程的非 pumping `SynchronizationContext` 死锁。
Threading / Cancellation: 遵守 [threading.md](threading.md)；Build 失败后的异步清理必须从默认线程池调度器启动并同步观察；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ApplicationHostBuilderTests; ApplicationHostRuntimeTests; CoreHeadlessProcessTests`。
Required Assertions: 断言 Build 成功或失败后 services/configuration 冻结，递归 section/children、公开 root、Reload 和 provider 无绕过入口，HostBuilt 诊断、根 scope 创建、Context 不可变且 Host Dispose 后仍可读取；在非 pumping `SynchronizationContext` 下，完整 Registry 清理和构造中途局部回滚都能等待包含真正异步 continuation 的 `DisposeAsync` 完成。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-002 Lifecycle Pipeline

Feature ID: `AUC-CORE-002`
Status: Verified
Goal: 按阶段执行 middleware，支持取消、异常聚合和幂等停止。
Public Contract: LifecyclePipeline, LifecyclePipelineBuilder, LifecycleStage
Runtime / Build Behavior: 按阶段执行 middleware，支持取消、异常聚合和幂等停止；一次 Host transaction 的嵌套 stage 和 rollback 传播同一 operationId；`next` 具备 middleware invocation + pipeline transaction 双重有效期和原子单调用所有权；Stop-before-Start 跳过未进入运行期的 hook，但必须取消 HostScope tree 并释放 Build 阶段创建的 module instances。
Failure Behavior: middleware 异常或丢弃/缓存/延迟调用 `next` 进入 Faulted 并写 AUCHOST108；已启动的逃逸下游必须先被收拢和观察，不能在状态推进或 rollback 后继续执行；启动失败且回滚成功时原样抛出主异常，回滚失败时完成全部最低清理后以主异常为第一项聚合返回所有回滚异常；启动取消只有在回滚成功时保持 Canceled，回滚失败时返回 Faulted 聚合；取消或 Created 状态的 module dispose failure 不跳过其他 cleanup；重复 Stop 幂等；Stopped 后再次 Start 抛 InvalidOperationException。
Threading / Cancellation: `await next()` 与 `return next()` 合法；显式 short-circuit 可以不调用 `next`；并发重复调用只有一个调用者取得所有权。其余规则遵守 [threading.md](threading.md)。
Diagnostics: AUCHOST108 包含 stage、middlewareType、operationId、exceptionType；原始异常传播和 diagnostics sink 故障隔离必须稳定。
Tests: `ApplicationHostRuntimeTests; LifecycleMiddlewarePipelineTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests`。
Required Assertions: 断言 stage 顺序、同 stage 顺序、准确 middleware 归因、operationId 传播、直接返回与正常 await、fire-and-forget 收拢后拒绝、保存后调用失效、32/64 路并发仅一次进入 terminal、required terminal 完整完成前 Host 不推进状态、干净启动回滚保留原异常、普通/取消启动的失败回滚有序聚合且 Diagnostics 逐项记录、Stop 不重复执行、Stop-before-Start 完整清理、Stopped 后再次 Start 被拒绝。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-003 Lifecycle Scope Tree

Feature ID: `AUC-CORE-003`
Status: Verified
Goal: 表达 application/module/plugin contribution/operation 所有权树。
Public Contract: LifecycleScope, LifecycleScopeKind, LifecycleScopeState
Runtime / Build Behavior: 表达 application/module/plugin contribution/operation 所有权树；Parent Stop 对 children 快照执行 leaf-first 停止，并通过内部 handoff 与并发 Child Dispose 共享 child Stop transaction。
Failure Behavior: 未知 LifecycleScopeKind 在资源分配或 parent 挂接前失败；parent disposed 后禁止创建 child；正常并发 child Dispose 不产生 Parent failure；真实 child Stop/Dispose failure 保留并诊断；重复释放幂等；公开 Stop-after-Dispose 继续失败。
Threading / Cancellation: 遵守 [threading.md](threading.md)；Parent 不等待 child 完整 Dispose transaction，不在 lock 内执行 cancellation callback、Stop 或 Dispose。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `LifecycleScopeTreeTests; CoreHeadlessProcessTests`。
Required Assertions: 断言 leaf-first、parent-child 状态、64-child cancellation Dispose 竞态、进行中 Dispose 汇合、真实 failure 不被吞掉以及 dispose 后公开 mutating API 失败。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-004 Module Contract

Feature ID: `AUC-CORE-004`
Status: Verified
Goal: 模块声明、依赖、配置、服务注册和初始化钩子。
Public Contract: ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext
Runtime / Build Behavior: 模块声明、依赖、PreConfigure 配置、服务注册阶段边界和初始化钩子。
Failure Behavior: 非模块类型、循环依赖、缺失依赖、重复 id、非法 origin、插件 descriptor 缺失 plugin id、服务配置阶段后继续 mutation 失败；默认 id 使用类型全名。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests`。
Required Assertions: 断言依赖排序、默认 id、显式 id、模块来源、PreConfigure 顺序、配置阶段禁止解析运行时服务、配置阶段结束后拒绝继续修改服务注册。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-005 DI Registration Markers

Feature ID: `AUC-CORE-005`
Status: Verified
Goal: 以 AOT 友好的生成代码把服务 lifetime、暴露 contract 和 Module 所有权接入生产 Host。
Public Contract: ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, ServiceRegistrationOwnerAttribute, GeneratedServiceManifestAttribute, IServiceRegistrar, IServiceRegistrarContext, 标记接口
Runtime / Build Behavior: Generator 在编译期发现并聚合服务 registrar；Host 解析启动 Module 闭包后，只在 PreConfigure 与 Configure 之间应用已选静态 Module owner 的注册。Owner 必须由 registrar 所在程序集声明，一个 owner 只允许其定义程序集的唯一 generated registrar 持有；项目扩展使用本地 Module + DependsOn，不得向外部 owner 注入业务服务。共享一个 Root Provider 不表示接收未选 Module 的服务。
Failure Behavior: 未知 ServiceLifetime、null exposed type、owner 缺失/重复/非 Module/非本程序集声明、冲突 lifetime、Replace+TryAdd、非法 exposed type、重复 service contract 和 disposable 多 contract 必须失败；同一 registrar 的菱形重复按 registrar identity 幂等跳过，不同 registrar 争用同一 owner 必须在 Build 确定性报错，不得静默丢弃；运行时 Attribute 构造立即拒绝相同非法值。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `ServiceRegistrationAttributeTests; AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests; CoreHeadlessProcessTests; CoreMvpCliProcessTests; Core MVP NativeAOT`。
Required Assertions: 断言 Attribute/marker lifetime、未知 lifetime 不降级、null exposed type 不被过滤、keyed registration、跨程序集 registrar 聚合、按 registrar identity 的菱形去重、跨程序集 owner 冒充失败、本地 Module + DependsOn 隔离、未选 owner 隔离、非 disposable 多 contract 共享实例、120 种 root 顺序、32 种组合、64 个并发 scope、阶段顺序以及 net10/net8 win-x64 NativeAOT 原生运行。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-006 Host Diagnostics

Feature ID: `AUC-CORE-006`
Status: Verified
Goal: 记录 Host 构建、启动、停止和失败上下文。
Public Contract: IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds
Runtime / Build Behavior: 记录 Host 构建、启动、停止和失败上下文；Host lifecycle 摘要与 middleware 详细诊断通过 operationId 关联。
Failure Behavior: 诊断 collector 不可用不能中断 Host；记录必须可 snapshot；空或空白 code/message、未知 severity 创建失败；Host diagnostics 完成后写入抛 ObjectDisposedException。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `HostDiagnosticsTests; CoreHeadlessProcessTests`。
Required Assertions: 断言 AUCHOST001-003、AUCHOST101-109、失败诊断上下文及 operationId 关联、Build cleanup failure 的逐项记录、所有 record 初始化入口的输入验证、Host Dispose 后拒绝写入并保留 snapshot，以及 Write/Complete 并发原子边界。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。
## AUC-CORE-007 UI Dispatcher Contract

Feature ID: `AUC-CORE-007`
Status: Verified
Goal: 为 Presentation 提供调度抽象和不可用实现。
Public Contract: IUiDispatcher, UnavailableUiDispatcher
Runtime / Build Behavior: 为 Presentation 提供调度抽象和不可用实现。
Failure Behavior: 默认 dispatcher 不可用；Presentation 必须替换真实实现。
Threading / Cancellation: 遵守 [threading.md](threading.md)；涉及异步、IO、dispatcher、plugin、connection、process 或 generator 的操作必须显式处理 cancellation。
Diagnostics: 现有诊断码见 [diagnostics.md](diagnostics.md)；产品级缺口必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中追踪。
Tests: `UiDispatcherIntegrationTests`。
Required Assertions: 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-CORE-008 Generated Module Catalog

Feature ID: `AUC-CORE-008`
Status: Verified
Goal: 在编译期生成 AOT 友好的模块描述、依赖和强类型工厂，并在 Host Build 时从启动根解析实际模块闭包。
Public Contract: ApplicationModuleAttribute, GeneratedModuleManifestAttribute, IModuleRegistrar, IModuleRegistrarContext
Runtime / Build Behavior: Generator 为可用模块生成 Catalog registrar；`ApplicationModuleAttribute` 声明默认根，`UseModule<TModule>()` 增加显式根；Host 合并并去重根模块，先把 required/available optional 依赖闭包验证并拓扑排序为内部 `ValidatedModuleGraph`，随后才执行强类型 factory。生成 descriptor 优先于兼容反射 descriptor。
Failure Behavior: 多个默认应用根、非法模块工厂、重复 id、缺失 required 依赖、循环依赖、非法 registrar 或 registrar 注册冲突必须在编译期或 Build 阶段确定性失败；graph validation 失败时任何 module factory/constructor 均不得执行。缺少生成清单时仅显式 `UseModule<TModule>()` 进入兼容路径。
Threading / Cancellation: Registrar 只在单线程 Build 配置阶段执行，不运行模块业务代码；Build 后 Catalog 和 roots 冻结。
Diagnostics: Generator 诊断使用 `AUCGEN*`；Build 失败写入 Host build diagnostics，默认路径不得扫描程序集模块类型。
Tests: `GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests`；`win-x64` NativeAOT headless publish/run。
Required Assertions: 零 `UseModule` 自动根、显式根、重复根去重、仅加载依赖闭包、生成 factory、编译期 AUCGEN003、Build 期直接/间接/跨程序集循环的零构造副作用、合法菱形图、无生成清单兼容路径和真实进程启停。
Acceptance Criteria: 默认路径不通过反射读取 Module/DependsOn metadata，不调用 Activator 创建模块；trim/AOT 可达性由强类型生成代码和发布测试证明。
