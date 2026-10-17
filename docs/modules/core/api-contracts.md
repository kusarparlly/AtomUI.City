# AtomUI.City.Core API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Hosting | ApplicationHost, ApplicationHostBuilder, IApplicationHost, IApplicationContext | 构建和持有应用 Host。 | Build 后冻结；Start/Stop 幂等；Dispose 后 mutating API 失败。 |
| Lifecycle | LifecycleStage, LifecyclePipeline, LifecycleScope | 阶段、中间件和所有权树。 | 按 stage 稳定执行；leaf-first 释放；异常进入 diagnostics。 |
| Modularity | ModuleBase, IModule, ModuleDescriptor, ModuleOrigin, DependsOnAttribute, ServiceConfigurationContext, ModuleServiceCollection | 模块声明、依赖、配置阶段和生命周期钩子。 | 配置、服务注册、初始化、关闭严格分阶段；PreConfigure 按模块拓扑顺序执行；配置阶段对象结束后冻结。 |
| DependencyInjection | ServiceAttribute, ScopedServiceAttribute, ExposeServicesAttribute, dependency marker interfaces | AOT 友好的服务声明。 | 不依赖运行时扫描作为唯一发现方式。 |
| Threading | IUiDispatcher, UnavailableUiDispatcher | UI 调度抽象。 | Core 不绑定 UI 平台；dispatcher 是 inline/排队执行的唯一裁决者。 |
| Diagnostics | IHostDiagnostics, HostDiagnosticRecord, HostDiagnosticIds | Host 级诊断。 | 现有诊断码保持兼容，目标诊断码新增需迁移。 |

## 首批冻结 API Cards

以下 API 是 Phase 0 到 Phase 3 的产品级冻结范围。没有进入本节的 public 类型可以保留为支持类型，但不能在首批实现中被当作稳定产品 API 对外承诺。

## 工业化新增合同

| API | Feature | 合同 |
| --- | --- | --- |
| `ConfigureLifecycle(Action<LifecyclePipelineBuilder>)` | AUC-CORE-002 | Build 前按调用顺序收集 middleware；null builder/delegate 抛参数异常；Build 后调用因 builder frozen 失败。 |
| `GetBuildDiagnostics()` | AUC-CORE-006 | 返回 builder 拥有的稳定 collector；Build 抛异常后仍可读取 AUCHOST101/105；collector 写入失败不能替换原始 Build 异常。 |
| `ApplicationHostOptions.DiagnosticsCapacity` | AUC-CORE-006 | 默认 1024，Build 时必须大于零，否则抛 ArgumentOutOfRangeException。 |
| `ApplicationHostOptions.ApplicationId` | AUC-CORE-001 | 必填、稳定、非本地化；null、空白或首尾空白导致 Build 在 Options 阶段失败。 |
| `ApplicationHostOptions.ApplicationVersion` | AUC-CORE-001 | 可显式覆盖；未配置时依次读取入口程序集 informational version 和 assembly version，均不可用则 Build 失败。 |
| `ApplicationModuleAttribute` | AUC-CORE-008 | 每个应用程序集最多声明一个默认启动根；Library 和 Plugin 不得用它替代各自入口合同。 |
| `GeneratedModuleManifestAttribute` | AUC-CORE-008 | Generator 写入入口程序集，引用实现 `IModuleRegistrar` 的强类型 registrar；应用代码不直接构造。 |
| `IModuleRegistrar.Register(IModuleRegistrarContext)` | AUC-CORE-008 | Build 冻结前同步登记不可变 descriptor、强类型 factory 和默认根；不得实例化模块或解析运行时服务。 |
| `UseModule<TModule>()` | AUC-CORE-008 | 增加显式启动根；与 generated default root 重复时去重；生成 Catalog 缺失该类型时进入兼容 descriptor 路径。 |
| `InMemoryHostDiagnostics(int capacity)` | AUC-CORE-006 | 有界 FIFO snapshot；容量满后丢弃最旧记录并增加 DroppedCount；无参构造保持原有无界行为。 |
| `IHostDiagnostics.Complete()` | AUC-CORE-006 | Host 在最后一条生命周期诊断之后调用；幂等且与 Write 构成原子边界，完成后拒绝写入但保留 record snapshot 读取。 |
| `LifecycleStage(area, name)` | AUC-CORE-002 | 未知 `LifecycleStageArea` 立即抛 `ArgumentOutOfRangeException`；不得形成数字化 stage key。`default(LifecycleStage)` 在 Context、Pipeline 注册和 Diagnostics 保存边界抛 `ArgumentException`。 |
| `LifecycleStages.All` | AUC-CORE-002 | 返回进程级稳定只读表；不得暴露数组，任何通过集合接口的修改尝试必须失败且不污染后续读取。 |
| `LifecycleScope.CreateRoot(kind, id, diagnostics)` | AUC-CORE-003 | root 和所有 child 共享 diagnostics sink；清理失败写 AUCHOST104 后继续其他 child；sink 异常被隔离。 |
| `LifecycleContext.OperationId` | AUC-CORE-002 | 标识一次 Host lifecycle transaction；Start 的嵌套 stage 和启动回滚共享一个非空 id，独立 Stop 使用新 id。显式空白 id 被拒绝，未提供时生成 GUID `N` 格式字符串。 |
| `LifecyclePipelineBuilder.Use<TMiddleware>(...)` | AUC-CORE-002 | `TMiddleware` 只提供稳定诊断身份，不改变 delegate 执行或 DI 激活语义；原有 `Use(...)` 继续兼容并从 delegate 声明类型尽力推断身份。 |

所有 Core public 类型的冻结命名空间以 `AtomUI.City.Core.*` 开头。旧的 `AtomUI.City.Hosting`、`AtomUI.City.Lifecycle` 等 Preview 命名空间不再构成 1.0 API。

`ApplicationHostOptions.AllowDynamicDiscovery` 不进入 1.0 Preview 合同。Generated Module Catalog 是编译期静态发现，不是运行时 dynamic discovery；后者必须在独立 Feature 中同时定义扫描边界、AOT/trimming 诊断和 Strict/Compatible 策略后才能重新公开。

### `ApplicationHost`

| Field | Contract |
| --- | --- |
| Type | `ApplicationHost` |
| Namespace | `AtomUI.City.Core.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 提供 Host 静态入口，创建 `ApplicationHostBuilder`。 |
| Owner | Build process / application startup caller |
| Created By | 调用方直接调用静态方法。 |
| Lifetime | 静态入口无运行期状态。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 静态创建方法必须无共享可变状态，可并发调用创建独立 builder。 |
| Disposal | 类型本身无释放；创建出的 builder/host 按各自合同释放。 |
| Nullability | `args` 可以为空；配置 delegate 不得为空。 |
| Cancellation | 无异步取消。 |
| Failure Behavior | 参数非法抛 `ArgumentNullException` 或 `ArgumentException`。 |
| Diagnostics | 创建 builder 不写诊断；Build 阶段写 HostBuilt 诊断。 |
| Plugin Boundary | 插件不能通过该入口创建嵌套 Host 修改当前 Host。 |
| AOT / Trimming | 不执行程序集扫描，不依赖 reflection discovery。 |
| Breaking Change Rules | 删除入口、修改默认 options 或改变异常类型属于 breaking change。 |
| Tests | `ApplicationHostBuilderTests` 断言 builder 独立、默认 options、非法参数。 |

### `ApplicationHostBuilder`

| Field | Contract |
| --- | --- |
| Type | `ApplicationHostBuilder` |
| Namespace | `AtomUI.City.Core.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 收集 options、services、modules、configuration 和 lifecycle middleware，并构建 Host。 |
| Owner | Application startup caller |
| Created By | `ApplicationHost.CreateBuilder`。 |
| Lifetime | Created -> Configuring -> Built -> Frozen；Build 后不可变。 |
| DI Lifetime | 不进入 DI；构建后生成 root service provider。 |
| Thread Safety | 配置阶段不保证线程安全；Build 必须串行；Build 后所有 mutating API 必须失败。 |
| Disposal | Builder 不拥有已构建 Host；Host 由调用方释放。 |
| Nullability | 注册 delegate、module type、service action 不得为空。 |
| Cancellation | Build 为同步或短异步准备阶段；不接受 token 的 API 不执行可取消 IO。 |
| Failure Behavior | 重复 Build、Build 后继续注册、无效 module/options 抛声明异常或返回失败 Result；Build 回滚优先通过 `IAsyncDisposable.DisposeAsync` 释放已创建的 Generic Host 和异步-only Root 服务，不支持时才回退 `Dispose`。全部异步 Build cleanup 共享 `ShutdownTimeout` 总 deadline，同步 Build 边界不得捕获调用线程的 `SynchronizationContext`；超时任务不能被强制取消，必须继续被观察并标记为可能仍在运行，同时启动其余独立 cleanup 但不增加等待预算。无清理异常时原样抛出主异常，存在清理异常时以主异常为第一个 inner exception、随后附加全部 cleanup failure 的 `AggregateException` 返回。 |
| Diagnostics | Build 成功写 `AUCHOST001`；Build 失败写结构化失败诊断；每个回滚清理异常写 `AUCHOST109`，Module Dispose 同时写 `AUCHOST106`。 |
| Plugin Boundary | 插件不能写 Host root builder；插件模块只能通过 PluginSystem 贡献。 |
| AOT / Trimming | 默认只消费显式注册或 generated manifest，不把运行时扫描作为唯一路径。 |
| Breaking Change Rules | 修改冻结时机、默认 services、module graph 规则或 Build 失败策略属于 breaking change。 |
| Tests | `ApplicationHostBuilderTests` 断言冻结、重复 Build、无效 options、诊断上下文。 |

`ApplicationHostBuilder` 和 `IApplicationHostBuilder` 不公开可变 `IServiceCollection`。Root DI 的应用级配置只能通过 `ConfigureServices` 登记；Build 内部和模块上下文持有的 service collection 不得泄漏为长期可写引用。

### `IApplicationHost`

| Field | Contract |
| --- | --- |
| Type | `IApplicationHost` |
| Namespace | `AtomUI.City.Core.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001, AUC-CORE-002 |
| Purpose | 表达应用 Host 的启动、停止、上下文访问和释放 contract。 |
| Owner | Application root owner |
| Created By | `ApplicationHostBuilder.Build`。 |
| Lifetime | Created -> Starting -> Running -> Stopping -> Stopped -> Disposed；失败进入 Faulted 后必须可 Stop/Dispose。 |
| DI Lifetime | Root object；不作为普通 scoped service 创建。 |
| Thread Safety | lifecycle transaction Task 在 lock 内先发布、在 lock 外执行；`StartAsync` 并发调用必须串行或拒绝；`StopAsync` 并发调用合并为同一停止事务；查询上下文可并发读取。 |
| Disposal | `Dispose` 和 `DisposeAsync` 幂等；Dispose 后 mutating API 失败，immutable context 可读取。 |
| Nullability | `CancellationToken` 可为默认值；返回值不得为空。 |
| Cancellation | `StartAsync(cancellationToken)` 的首次调用令牌驱动启动事务；middleware 和 terminal 只接收框架合成后的单一有效令牌。`StopAsync(waitCancellationToken)` 的令牌只影响当前等待，不跳过最小清理。 |
| Failure Behavior | middleware、module initialization 或 shutdown 失败进入 diagnostics；Start 回滚成功时原样抛出主异常，回滚失败时完成全部最低清理后抛出以主异常为第一项的有序 `AggregateException`；启动取消且回滚失败时任务为 Faulted 聚合而不是 Canceled；Host 状态必须稳定；进入 Stopped 后再次 Start，以及同一 Host lifecycle 调用链中的 Start/Stop/Dispose 公共 API 重入，抛 `InvalidOperationException`。 |
| Diagnostics | Start/Stop 写 `AUCHOST002`、`AUCHOST003` 和失败诊断。 |
| Plugin Boundary | Host 停止时必须请求插件来源 owner 释放，但不直接加载插件程序集。 |
| AOT / Trimming | Host 启动不执行默认程序集扫描。 |
| Breaking Change Rules | 修改状态机、幂等规则、取消语义或诊断码语义属于 breaking change。 |
| Tests | `ApplicationHostRuntimeTests` 断言 Start/Stop 幂等、Faulted 清理、Dispose 后行为。 |

### `IApplicationContext`

| Field | Contract |
| --- | --- |
| Type | `IApplicationContext` |
| Namespace | `AtomUI.City.Core.Hosting` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-001 |
| Purpose | 暴露当前应用实例的不可变身份、版本、环境、路径和启动参数描述。 |
| Owner | Application Host |
| Created By | Host Build 阶段。 |
| Lifetime | 随 Host 创建，Host Dispose 后只允许读取 immutable descriptor。 |
| DI Lifetime | Singleton。 |
| Thread Safety | immutable 字段可并发读取；mutable runtime state 必须由专门服务管理。 |
| Disposal | 不单独释放；随 Host 释放。 |
| Nullability | 所有 string 字段不得为 null 或空白；`ApplicationInstanceId` 不得为 `Guid.Empty`；集合不得为 null。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 缺失 ApplicationId、无法解析版本、非法 ApplicationName 或无法得到绝对路径时 Build 失败。 |
| Diagnostics | Build 失败诊断包含缺失字段名。 |
| Plugin Boundary | 插件只可读取 Host 暴露的共享 context，不能获得 Host 内部可变对象。 |
| AOT / Trimming | Context 字段不依赖动态类型发现。 |
| Breaking Change Rules | 删除字段、改变字段含义或把可读字段改为延迟失败属于 breaking change。 |
| Tests | `ApplicationHostBuilderTests` 和 `ApplicationHostRuntimeTests` 断言字段来源、不可变性、实例唯一性、DI 注册和 Dispose 后读取。 |

公开字段固定为：`ApplicationId`、`ApplicationInstanceId`、`ApplicationName`、`ApplicationVersion`、`EnvironmentName`、`ContentRootPath`、`AppDataPath`、`StartupArguments`。Context 不得暴露 `Configuration`、`Services`、Scope、Diagnostics、Host state 或 `Properties`。

### `LifecyclePipeline`

| Field | Contract |
| --- | --- |
| Type | `LifecyclePipeline` |
| Namespace | `AtomUI.City.Core.Lifecycle` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-002 |
| Purpose | 按稳定 stage 顺序执行 lifecycle middleware。 |
| Owner | Application Host |
| Created By | `ApplicationHostBuilder.Build`。 |
| Lifetime | 随 Host 创建和释放。 |
| DI Lifetime | Singleton。 |
| Thread Safety | pipeline 构建后不可变；执行阶段必须由 Host 串行调度。每个 `next` 使用原子单所有权，只允许当前 middleware 调用一次；并发重复调用只有一个调用者可以进入下游。 |
| Disposal | pipeline 本身无外部资源；middleware 释放通过 scope 处理。 |
| Nullability | stage、context、middleware delegate 不得为空。 |
| Cancellation | `ExecuteAsync(context, cancellationToken)` 将显式有效令牌传给每个 `LifecycleMiddleware(context, next, cancellationToken)` 和 terminal；`LifecycleContext` 不保存第二份 token。每个 middleware 调用前后观察 token；取消不得跳过 cleanup stage。 |
| Failure Behavior | middleware 异常进入 Faulted，错误聚合后继续必要清理。middleware 可以显式 short-circuit；一旦调用 `next`，必须 `await` 或直接返回，禁止丢弃、缓存或延迟调用。middleware 提前返回时 Pipeline 必须收拢已启动的下游任务后以 `InvalidOperationException` 拒绝，transaction 结束后的旧 `next` 立即失败。 |
| Diagnostics | middleware 失败写 `AUCHOST108`，强类型 `Stage` 和 context 必须包含 `middlewareType`、`operationId`、`exceptionType`；同一异常只归因到实际失败 middleware 一次，terminal 异常不得误报为 middleware 失败。正常 cancellation 不写该错误，diagnostics sink 失败不得替换原始异常。 |
| Plugin Boundary | 插件 middleware 必须绑定 owner；插件停用时撤销后不再执行。 |
| AOT / Trimming | middleware 来源优先显式注册或 generated manifest。 |
| Breaking Change Rules | 修改 stage 顺序、同 stage 排序、异常聚合或取消语义属于 breaking change。 |
| Tests | `LifecycleMiddlewarePipelineTests` 断言 stage 顺序、同 stage 顺序、直接返回/正常 await、fire-and-forget 收拢拒绝、过期调用、并发单所有权、异常和取消路径。 |

### `LifecycleScope`

| Field | Contract |
| --- | --- |
| Type | `LifecycleScope` |
| Namespace | `AtomUI.City.Core.Lifecycle` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-003 |
| Purpose | 表达 application、module、plugin contribution、route、activation 和 operation 的所有权树。 |
| Owner | Parent scope 或 Host root |
| Created By | Host、ModuleSystem 或上层运行时模块。 |
| Lifetime | 创建完成后立即处于 Running；停止时 Running -> Stopping -> Stopped/Faulted；释放时 Stopped/Faulted -> Disposing -> Disposed。 |
| DI Lifetime | Scope object；可被对应 owner 持有，不注册为 root singleton。 |
| Thread Safety | child 创建和状态发布必须同步保护；真实 Stop/Dispose、cancellation callback 和 Disposed notification 在 lock 外执行；Parent Stop 对快照 child 原子加入其 Stop transaction，与并发 Dispose 汇合但不等待完整 Dispose；读取状态可并发；公开 mutating API 在 Disposing/Disposed 失败。 |
| Disposal | leaf-first；重复 Dispose 幂等；正常并发 child Dispose 不构成 Parent Stop failure；child 的真实 Stop/Dispose failure 仍记录诊断并继续清理其他 child。 |
| Nullability | scope id、owner 不得为空；未知 `LifecycleScopeKind` 在分配或挂接 scope 资源前抛 `ArgumentOutOfRangeException`。 |
| Cancellation | Dispose 不启动新 operation；active operation 必须在 parent stop 时被取消。 |
| Failure Behavior | parent disposed 后创建 child 失败；公开 Stop-after-Dispose 抛 `ObjectDisposedException`；Parent 内部 stale snapshot 遇到已 Dispose child 时加入其历史 Stop transaction；真实释放失败聚合到 diagnostics。 |
| Diagnostics | 诊断包含 scopeId、kind、owner、child scope 和 exception type。 |
| Plugin Boundary | plugin owner 停用时必须释放该插件创建或贡献的 scope。 |
| AOT / Trimming | scope 不保存插件私有类型作为 Host 长期 key。 |
| Breaking Change Rules | 修改释放顺序、幂等行为或 Dispose 后访问规则属于 breaking change。 |
| Tests | `LifecycleScopeTreeTests; CoreHeadlessProcessTests` 断言 leaf-first、parent-child 状态、重复释放、Stop/Dispose 竞态和失败诊断。 |

`LifecycleScope` 当前只产生 `Running`、`Stopping`、`Stopped`、`Faulted`、`Disposing` 和 `Disposed`。`LifecycleScopeState` 中的 `Created`、`Starting`、`CancelRequested` 和 `UnloadPending` 是保留枚举值，当前状态机不会产生，调用方不得依赖这些值处理当前运行时行为。

### `ModuleBase`

| Field | Contract |
| --- | --- |
| Type | `ModuleBase` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 为模块配置、服务注册、初始化和关闭提供基类 contract。 |
| Owner | Module system |
| Created By | Module registry 或 DI。 |
| Lifetime | Declared -> ServicesConfigured -> Initialized -> Shutdown。 |
| DI Lifetime | 默认 transient 或由 module registry 控制；不得作为 Host root mutable singleton 暴露给插件。 |
| Thread Safety | 单个 module 实例的 lifecycle hook 由 module system 串行调用。 |
| Disposal | 如果 module 实例实现 dispose，Host shutdown 按 module 逆序释放。 |
| Nullability | context 参数不允许为空。 |
| Cancellation | 三个服务配置 hook 为同步且不接受 token；ModuleRegistry 在每个模块调用前后观察配置事务 token。Contribution、Initialize 和 Shutdown 异步 hook 必须观察 token。 |
| Failure Behavior | hook 抛异常时当前阶段失败并记录 module 诊断；shutdown 继续后续 module 清理。 |
| Diagnostics | 诊断包含 module id、module type、stage。 |
| Plugin Boundary | 插件 module 只能写插件 service scope 和受控 contribution。 |
| AOT / Trimming | 模块发现默认依赖显式注册或 generated manifest。 |
| Breaking Change Rules | 修改 hook 顺序、context 能力或默认 module id 规则属于 breaking change。 |
| Tests | `ModuleBaseTests` 断言 hook 顺序、null context、取消、异常和 shutdown 行为。 |

### `ApplicationInitializationContext`

| Field | Contract |
| --- | --- |
| Type | `ApplicationInitializationContext` |
| Feature | AUC-CORE-004 |
| Purpose | 向 Module 的三个 application initialization hook 提供当前应用身份、Application DI provider 和由 Host 拥有的 ApplicationScope。 |
| Construction | 构造函数要求 `applicationContext`、`services` 和 `applicationScope` 全部非 null；非法输入立即抛 `ArgumentNullException`。 |
| `ApplicationScope` | 与当前 `IApplicationHost.ApplicationScope` 是同一实例，并且在 Module 初始化期间处于 `Running`；不得由 Module 主动 Stop 或 Dispose。 |
| Ownership | Host 创建并终止 Scope；Module 只用它创建更小的 child scope 或绑定运行时资源。具体模块状态不得反向进入 Core context。 |
| Lifecycle | 只在 ApplicationScope 成功创建后产生；Stop-before-Start 不会伪造 initialization context。shutdown hook 通过模块自身已保存的唯一终止事务清理资源。 |
| Compatibility | 构造函数和只读属性属于 public API；变更必须同步 compatibility 和 Public API baseline。 |
| Tests | `ModuleBaseTests`、`ModuleRegistryConcurrencyTests` 和 `ApplicationHostModuleLifecycleTests` 断言非空校验、实例同一性、阶段共享与 Host 所有权。 |

### `IModuleRegistry` / `IModuleLifecycleController` / `ModuleRegistry`

| Field | Contract |
| --- | --- |
| Type | public `IModuleRegistry`; internal `IModuleLifecycleController`; internal `ModuleRegistry` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | `IModuleRegistry` 只公开不可变模块元数据；internal controller 按拓扑顺序执行唯一、串行的配置、贡献、初始化和终止事务。 |
| Owner | Application Host |
| Public Surface | `IModuleRegistry` 仅包含 `Modules`。ConfigureServices、ConfigureContributions、Initialize、Shutdown 和 Dispose 均属于 internal Host control plane。 |
| DI Boundary | Root DI 注册独立的只读 `ModuleRegistryView`，不得注册 controller 或 controller 本体；公开 view 不实现 `IDisposable`/`IAsyncDisposable`，不能通过强转取得终止能力。 |
| Lifetime | 只由已验证且拓扑排序的内部 `ValidatedModuleGraph` 创建；随后 Created -> ServicesConfigured -> ContributionsConfigured -> Initialized -> Terminating -> Disposed；正向阶段失败进入 Faulted。 |
| Thread Safety | ConfigureServices 是唯一同步事务，执行期间的外部并发调用快速失败，完成后的重复调用观察第一次结果；其他同阶段并发调用共享唯一 Task；不同正向阶段不并行；Shutdown 与 Dispose 共享唯一 terminal Task。 |
| Idempotency | 每个阶段最多执行一次。部分失败的正向阶段不可重试；后续同阶段调用观察第一次事务结果。 |
| Terminal Precedence | 第一个终止调用决定路径：Shutdown-first 执行 shutdown + dispose；Dispose-first 只执行 dispose；后续调用加入同一结果。 |
| Disposal | 终止等待已发布的正向阶段退出；module hook 与 module Dispose 不得并行；所有释放按拓扑逆序且单实例最多尝试一次。 |
| Cancellation | 第一个阶段调用者的 token 属于共享事务；取消或阶段失败不阻止后续 terminal cleanup。 |
| Failure Behavior | 正向阶段失败进入 Faulted；shutdown/dispose 聚合失败并继续清理其他模块；递归 lifecycle 调用快速失败。 |
| Construction Boundary | `ModuleRegistry` 不接受原始 registration；重复 id、缺失依赖和循环依赖必须在任何 factory 执行前由 Host Build 拒绝。 |
| Breaking Change Rules | 向 public `IModuleRegistry` 增加生命周期控制能力，或修改阶段顺序、失败重试语义、终止优先级或共享事务规则属于 breaking change。 |
| Tests | `ModuleRegistryConcurrencyTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests`。 |

### `ModuleDescriptor`

| Field | Contract |
| --- | --- |
| Type | `ModuleDescriptor` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 描述模块 id、type、依赖、origin、plugin owner 和 source。 |
| Owner | Module registry |
| Created By | Attribute reader、generated manifest 或显式注册。 |
| Lifetime | immutable descriptor；可在 Host 生命周期内共享读取。 |
| DI Lifetime | 不作为服务实例注册。 |
| Thread Safety | 不可变，可并发读取。 |
| Disposal | 无释放。 |
| Nullability | module id、module type、origin 不得为空；plugin origin 必须提供 plugin id；application origin 不允许提供 plugin id。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 缺失 id/type、非 `IModule` 类型、重复 id、循环依赖、缺失依赖或非法 origin 导致 graph build 失败。 |
| Diagnostics | 诊断包含 module id、type、dependency path。 |
| Plugin Boundary | 插件 module descriptor 必须设置 `Origin = Plugin` 并包含稳定 plugin id；application module 不得携带 plugin id。 |
| AOT / Trimming | 支持 generator 输出，不要求运行时扫描。 |
| Breaking Change Rules | 修改默认 id、依赖排序或 descriptor 字段含义属于 breaking change。 |
| Tests | `ModuleDescriptorTests` 断言默认 id、显式 id、依赖排序、origin、plugin id、非模块类型、依赖集合内部 null 和错误诊断。 |

### `ModuleOrigin`

| Field | Contract |
| --- | --- |
| Type | `ModuleOrigin` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 标识模块描述来自应用静态模块还是插件模块。 |
| Owner | Module descriptor |
| Created By | `ModuleDescriptor` 构造或 generated manifest。 |
| Lifetime | enum metadata，无运行期状态。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 无 mutable state。 |
| Disposal | 无释放。 |
| Nullability | 不适用；未知 enum 值必须被 descriptor 拒绝。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | `ModuleDescriptor` 遇到未知 origin 值抛 `ArgumentOutOfRangeException`。 |
| Diagnostics | graph build 失败诊断必须包含 module id 和 origin。 |
| Plugin Boundary | `Plugin` origin 必须和 plugin id 成对出现。 |
| AOT / Trimming | enum 常量可由 generator 直接输出。 |
| Breaking Change Rules | 删除、重命名或改变 enum 值语义属于 breaking change。 |
| Tests | `ModuleDescriptorTests` 断言默认 application origin 和 plugin origin 校验。 |

### `ServiceConfigurationContext`

| Field | Contract |
| --- | --- |
| Type | `ServiceConfigurationContext` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 在模块服务配置阶段提供 application context、受控 service collection 和 early options preconfigure store。 |
| Owner | Module registry |
| Created By | `ModuleRegistry.ConfigureServices` 或测试代码。 |
| Lifetime | 只在 PreConfigureServices、ConfigureServices、PostConfigureServices 阶段有效。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 与 module configuration 调度一致，由 module registry 串行调用；不保证并发写安全。 |
| Disposal | 无外部资源；阶段结束后内部 `ModuleServiceCollection` 冻结。 |
| Nullability | application context、services、preconfigure action 和 options instance 不得为空。 |
| Cancellation | context 本身无取消；ModuleRegistry 在进入和退出每个同步服务配置 hook 时观察事务 token。 |
| Failure Behavior | null action 或 null options 抛 `ArgumentNullException`；options action 异常由当前 module stage 诊断承接。 |
| Diagnostics | action 异常由 module registry 写 `AUCHOST106`，context 包含 moduleType 和 stage。 |
| Plugin Boundary | 插件模块使用插件自己的 context 和 preconfigure store，不能修改 Host 全局 store。 |
| AOT / Trimming | 不扫描程序集，不读取 runtime attributes；只执行显式注册的同步 action。 |
| Breaking Change Rules | 修改 PreConfigure 顺序、null 行为、阶段生命周期或冻结时机属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言 PreConfigure 拓扑顺序、null 参数和阶段边界。 |

### `ModuleServiceCollection`

| Field | Contract |
| --- | --- |
| Type | `ModuleServiceCollection` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 在 module service configuration 阶段暴露受控 service registration collection。 |
| Owner | Module registry |
| Created By | `ServiceConfigurationContext`。 |
| Lifetime | 只在 module service configuration 阶段有效；阶段结束后由 module registry 冻结。 |
| DI Lifetime | 不进入 DI；包装 Host build 阶段的 `IServiceCollection`。 |
| Thread Safety | 与底层 `IServiceCollection` 一致，不保证并发写安全。 |
| Disposal | 无资源；Host Build 结束后 module 不应继续持有。 |
| Nullability | service descriptor 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | City 提供的无参数、`validateScopes` 和 `ServiceProviderOptions` 临时 provider 入口必须抛 `InvalidOperationException`；生产项目中的其他 `BuildServiceProvider`、`IServiceProviderFactory<T>.CreateServiceProvider` 和 Microsoft Generic Host 构建/启动调用或引用由 `AUCANL0001` 阻止编译；阶段冻结后所有 mutating API 抛 `InvalidOperationException`。 |
| Diagnostics | 触发 guard 时由 module lifecycle 记录 `AUCHOST106`。 |
| Plugin Boundary | 插件 module 只能通过该 collection 注册受控服务，不能写 Host runtime provider。 |
| AOT / Trimming | 不执行扫描，不依赖 dynamic code。 |
| Breaking Change Rules | 修改临时 provider guard、注册转发语义或生命周期属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言禁止 `BuildServiceProvider`、冻结后拒绝 mutation 并记录诊断。 |

### `ModuleServiceCollectionBuildGuardExtensions`

| Field | Contract |
| --- | --- |
| Type | `ModuleServiceCollectionBuildGuardExtensions` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 用更具体的 extension method 阻止 module service configuration 阶段通过常用微软 DI 调用形式创建临时 provider。 |
| Owner | Module registry |
| Created By | 静态扩展类型。 |
| Lifetime | 编译期 API。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 无状态。 |
| Disposal | 无释放。 |
| Nullability | services 为 null 时抛 `ArgumentNullException`；`ServiceProviderOptions` overload 的 options 为 null 时同样抛参数异常。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 三个 `BuildServiceProvider(ModuleServiceCollection, ...)` 重载对非 null 参数始终抛 `InvalidOperationException`；显式强转、静态调用、方法引用、独立 `ServiceCollection`、service provider factory 和 Microsoft Generic Host 构建/启动入口由生产项目 Analyzer `AUCANL0001` 阻止编译。 |
| Diagnostics | module registry 捕获异常后写 `AUCHOST106`。 |
| Plugin Boundary | 插件 module 同样受 guard 约束。 |
| AOT / Trimming | 静态扩展方法，不依赖 reflection。 |
| Breaking Change Rules | 放宽或移除 guard 属于 breaking change。 |
| Tests | `ApplicationHostModuleLifecycleTests` 断言 guard message 和 diagnostics context。 |

### `DependsOnAttribute`

| Field | Contract |
| --- | --- |
| Type | `DependsOnAttribute` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-004 |
| Purpose | 声明模块依赖，用于 graph build 和 generator metadata。 |
| Owner | Module type declaration |
| Created By | 应用或插件模块源码。 |
| Lifetime | 编译期 metadata 和运行期 descriptor 输入。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | dependency type 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 依赖类型不是 module、重复依赖、循环依赖进入 graph diagnostic。 |
| Diagnostics | 诊断包含 source module、dependency module 和 cycle path。 |
| Plugin Boundary | 插件不能依赖未暴露的 Host 私有 module 类型。 |
| AOT / Trimming | Attribute 必须可被 source generator 读取。 |
| Breaking Change Rules | 修改构造参数、allow multiple 规则或继承语义属于 breaking change。 |
| Tests | `ModuleAttributeTests` 断言 attribute metadata 和无效依赖。 |

### `ApplicationModuleAttribute`

| Field | Contract |
| --- | --- |
| Type | `ApplicationModuleAttribute` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-008 |
| Purpose | 声明可执行应用程序集的唯一 generated default root。 |
| Owner | Application executable project |
| Created By | 应用模块源码。 |
| Lifetime | 编译期 metadata；Host Build 时转为 root type。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | 无参数。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 非 IModule 类型、Library/Plugin 中使用、多个默认根或不可生成工厂产生 `AUCGEN007/008` 编译错误。 |
| Diagnostics | 诊断定位到被标记的模块声明。 |
| Plugin Boundary | Library 和 Plugin 不得声明应用默认根。 |
| AOT / Trimming | 仅由 generator 读取，不进行运行时程序集扫描。 |
| Breaking Change Rules | 修改唯一根、目标程序集或继承规则属于 breaking change。 |
| Tests | `ModuleAttributeTests; AtomUICityIncrementalGeneratorModularityTests`。 |

### `GeneratedModuleManifestAttribute`

| Field | Contract |
| --- | --- |
| Type | `GeneratedModuleManifestAttribute` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-008 |
| Purpose | 把入口程序集连接到 generated registrar。 |
| Owner | AtomUI.City.Generators |
| Created By | Source generator；应用代码不直接构造。 |
| Lifetime | Assembly metadata，进程生命周期内只读。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | registrarType 不得为 null。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | registrar 类型非法或无法构造时 Host Build 在 ModuleGraph 阶段失败。 |
| Diagnostics | Build 失败写 `AUCHOST101/105`。 |
| Plugin Boundary | 插件入口使用插件合同，不用它声明应用根。 |
| AOT / Trimming | registrarType 保留 public parameterless constructor。 |
| Breaking Change Rules | 修改 AttributeUsage、构造参数或 registrar 可达性合同属于 breaking change。 |
| Tests | `GeneratedModuleCatalogTests; CoreHeadlessProcessTests; NativeAOT headless run`。 |

### `IModuleRegistrar` / `IModuleRegistrarContext`

| Field | Contract |
| --- | --- |
| Type | `IModuleRegistrar`; `IModuleRegistrarContext` |
| Namespace | `AtomUI.City.Core.Modularity` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview generated-code contract |
| Feature | AUC-CORE-008 |
| Purpose | 同步登记不可变 ModuleDescriptor、强类型 factory 和默认根。 |
| Owner | Generated registrar / Host ModuleCatalog |
| Created By | Generator / Host Build。 |
| Lifetime | 仅 Host Build 的 ModuleGraph 阶段有效；解析后冻结。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 单线程 Build 配置合同，不保证并发写。 |
| Disposal | 无释放；不得创建模块实例。 |
| Nullability | context、descriptor、factory、root type 不得为 null。 |
| Cancellation | 同步短执行，不接受取消。 |
| Failure Behavior | 重复 id、冲突 descriptor、非法 root/registrar 或冻结后登记确定性失败。 |
| Diagnostics | 异常由 Host Build 记录为 ModuleGraph failure。 |
| Plugin Boundary | application registrar 可聚合引用程序集 registrar，但 Library registrar 不得增加应用根。 |
| AOT / Trimming | 生成代码直接 `new TModule()`；不得通过反射构造模块。 |
| Breaking Change Rules | 修改 Register 签名、登记顺序或冻结语义属于 breaking change。 |
| Tests | `ModuleRegistrarSourceBuilderTests; AtomUICityIncrementalGeneratorModularityTests; GeneratedModuleCatalogTests`。 |

### Generated service registration bridge

| Field | Contract |
| --- | --- |
| Types | `ServiceRegistrationOwnerAttribute`; `GeneratedServiceManifestAttribute`; `IServiceRegistrar`; `IServiceRegistrarContext` |
| Namespace | `AtomUI.City.Core.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 把编译期服务声明归属到唯一 Module，并将生成 registrar 接入 Host Build。 |
| Owner | `[ServiceRegistrationOwner]` 标注且由当前程序集声明的 `IModule`；每个包含自动服务的程序集恰好一个，一个 owner 只允许其定义程序集的唯一 generated registrar 持有。 |
| Runtime Behavior | Host 全局读取生成清单，但只应用 ModuleCatalog 已选依赖闭包中的本地 owner 注册；所有已选静态模块共享一个 Root Provider。A 依赖 B 时必须用本地 `AModule -> DependsOn(BModule)` 建模，A 不得向 B 的 owner 注入业务服务。 |
| Registrar Identity | 同一 registrar 经菱形路径重复到达时按 registrar type 幂等跳过；owner type 不得充当 registrar 去重键。 |
| Registrar Graph API | `RegisterRegistrar(Type, Func<IServiceRegistrar>)` 表达一条强类型 registrar 引用边；Catalog 必须先去重再调用 factory，factory 不得返回 null 或另一种 registrar 类型。 |
| Context Ownership | `IServiceRegistrarContext` 由 Catalog 创建并绑定当前实际 registrar，只在同步 `IServiceRegistrar.Register` 调用期间有效；不得保存后逃逸，也不得由业务代码伪造 registrar identity。 |
| Ordering | `PreConfigureServices -> Generated registrations -> ConfigureServices -> PostConfigureServices -> User ConfigureServices`。 |
| Failure Behavior | owner 缺失、重复、不是 Module 或不是 registrar 所在程序集声明的类型时失败；不同 registrar 争用同一 owner 时 Host Build 必须报告双方 registrar/assembly 并失败，不得静默丢弃。 |
| Plugin Boundary | Core Root 只接收静态应用模块；PluginSystem 必须把插件 registrar 应用到独立插件 Provider。 |
| AOT / Trimming | 通过程序集 manifest 和强类型 registrar 聚合，不扫描程序集服务类型。 |
| Breaking Change Rules | owner 选择、Module 过滤、阶段顺序或 registrar 签名变化属于 breaking change。 |
| Tests | `AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests`。 |

### `ServiceAttribute`

| Field | Contract |
| --- | --- |
| Type | `ServiceAttribute` |
| Namespace | `AtomUI.City.Core.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 声明 AOT 友好的服务注册 metadata。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata 和 generated manifest 输入。 |
| DI Lifetime | 由 attribute 或 marker 决定。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放；服务实例由 DI container 释放。 |
| Nullability | exposed service type、lifetime metadata 不得非法。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 运行时构造遇到未知 `ServiceLifetime` 抛 `ArgumentOutOfRangeException`；源码声明遇到未知 lifetime、冲突 lifetime、exposed type 不可赋值或重复注册策略冲突产生 `AUCGEN005` 且不生成 registrar；不得降级为 Transient。 |
| Diagnostics | 诊断包含 service type、exposed type、lifetime。 |
| Plugin Boundary | 插件服务注册只能进入插件 service scope 或受控 contribution。 |
| AOT / Trimming | 必须可由 generator 读取，不依赖运行时程序集扫描。 |
| Breaking Change Rules | 修改默认 lifetime、expose 规则或 replace/try-add 语义属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 Attribute 边界；`ServiceRegistrationMetadataReaderTests; AtomUICityIncrementalGeneratorDependencyInjectionTests` 断言 lifetime、exposed services 和冲突诊断。 |

### `ScopedServiceAttribute`

| Field | Contract |
| --- | --- |
| Type | `ScopedServiceAttribute` |
| Namespace | `AtomUI.City.Core.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 简化 scoped service 声明。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata。 |
| DI Lifetime | Scoped。 |
| Thread Safety | Attribute metadata 只读；实际服务线程安全由服务自身声明。 |
| Disposal | 无释放。 |
| Nullability | service type 数组不得为 null 或包含 null；空数组保留“使用默认实现类型”的现有语义。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 数组包含 null 时运行时构造立即失败；源码声明产生 `AUCGEN005` 且不生成 registrar；与其他 lifetime marker 冲突产生 diagnostic。 |
| Diagnostics | 诊断包含 service type 和冲突 lifetime。 |
| Plugin Boundary | 插件 scoped service 进入插件 service scope。 |
| AOT / Trimming | 可被 generator 读取。 |
| Breaking Change Rules | 修改 scoped 默认语义属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 scoped Attribute；`ServiceRegistrationMetadataReaderTests` 断言生成器解释的 scoped lifetime。 |

### `ExposeServicesAttribute`

| Field | Contract |
| --- | --- |
| Type | `ExposeServicesAttribute` |
| Namespace | `AtomUI.City.Core.DependencyInjection` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-005 |
| Purpose | 声明服务实现对外暴露的 service contract。 |
| Owner | Service type declaration |
| Created By | 应用、模块或插件源码。 |
| Lifetime | 编译期 metadata。 |
| DI Lifetime | 跟随实际 service registration。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | exposed type collection 不得包含 null。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 数组包含 null 时运行时构造立即失败；源码声明产生 `AUCGEN005` 且不生成 registrar；exposed type 非 assignable 或重复冲突产生 diagnostic。 |
| Diagnostics | 诊断包含 implementation type 和 exposed service type。 |
| Plugin Boundary | 插件不能把插件私有类型暴露给 Host 长期持有。 |
| AOT / Trimming | generator 必须能生成强类型注册。 |
| Breaking Change Rules | 修改暴露默认值或 type matching 规则属于 breaking change。 |
| Tests | `ServiceRegistrationAttributeTests` 断言 exposed service 数组边界；`ServiceRegistrationMetadataReaderTests` 断言生成器解释的暴露规则和非法暴露。 |

### `IHostDiagnostics`

| Field | Contract |
| --- | --- |
| Type | `IHostDiagnostics` |
| Namespace | `AtomUI.City.Core.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 接收 Host build/start/stop/failure 诊断记录。 |
| Owner | Host diagnostics service |
| Created By | DI registration。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton，测试可替换。 |
| Thread Safety | 必须支持多阶段并发记录或由实现显式串行化。 |
| Disposal | Host 在 Dispose 事务末尾调用幂等 `Complete()`；完成后 `Write` 抛 `ObjectDisposedException`，已有 immutable record 可读取。实现持有的真实资源仍遵循 DI disposal ownership。 |
| Nullability | record 不得为空。 |
| Cancellation | 记录诊断不接受取消；不能阻塞清理。 |
| Failure Behavior | diagnostics collector 的 Write 或 Complete 失败不得中断 Host 清理。 |
| Diagnostics | 自身失败写入 fallback sink 或测试 record。 |
| Plugin Boundary | 插件诊断必须带 pluginId，不能绕过 Host diagnostics。 |
| AOT / Trimming | record 使用稳定字段，不依赖动态序列化 contract。 |
| Breaking Change Rules | 修改 record 字段、severity 或 code 语义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言 code、severity、context 和 collector failure。 |

### `HostDiagnosticRecord`

| Field | Contract |
| --- | --- |
| Type | `HostDiagnosticRecord` |
| Namespace | `AtomUI.City.Core.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 表示单条 Host 诊断。 |
| Owner | Diagnostics collector |
| Created By | Host、Lifecycle、ModuleSystem。 |
| Lifetime | immutable record；可 snapshot。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 不可变，可并发读取。 |
| Disposal | 无释放。 |
| Nullability | code、message、severity 不得为空或未知；context 可为空集合。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 空或空白 code/message、未知 severity 在构造或 `with` 初始化时立即失败。 |
| Diagnostics | record 本身是诊断输出。 |
| Plugin Boundary | 插件相关 record 必须包含 pluginId。 |
| AOT / Trimming | context key 使用字符串常量，避免依赖 runtime type name 作为唯一 contract。 |
| Breaking Change Rules | 删除字段、改变 code 含义或 context key 含义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言 immutable、snapshot 和 context 字段。 |

### `HostDiagnosticIds`

| Field | Contract |
| --- | --- |
| Type | `HostDiagnosticIds` |
| Namespace | `AtomUI.City.Core.Diagnostics` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-006 |
| Purpose | 定义 Host 诊断码常量。 |
| Owner | Core diagnostics contract |
| Created By | Source code constant。 |
| Lifetime | 包版本生命周期内稳定。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 常量只读。 |
| Disposal | 无释放。 |
| Nullability | 诊断码常量不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 诊断码不能复用；废弃必须保留迁移说明。 |
| Diagnostics | 当前已实现并进入兼容合同的诊断码：`AUCHOST001` 到 `AUCHOST003`、`AUCHOST101` 到 `AUCHOST109`；名称、触发语义和必需 context 见 `diagnostics.md` 与 `compatibility.md`。 |
| Plugin Boundary | plugin 相关 Host 诊断使用独立 code 或 context，不复用普通 Host code。 |
| AOT / Trimming | 常量不依赖运行时生成。 |
| Breaking Change Rules | 删除、复用或改变诊断码语义属于 breaking change。 |
| Tests | `HostDiagnosticsTests` 断言当前代码存在且语义稳定。 |

### `IUiDispatcher`

| Field | Contract |
| --- | --- |
| Type | `IUiDispatcher` |
| Namespace | `AtomUI.City.Core.Threading` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-007 |
| Purpose | 为 Presentation 提供 UI 调度抽象，不绑定 Avalonia。 |
| Owner | Presentation runtime 或 Testing fake dispatcher |
| Created By | Core 默认注册 unavailable 实现；Presentation 替换真实实现。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton 或 platform-specific dispatcher lifetime。 |
| Thread Safety | 任意线程可调用调度 API；实现负责让回调从 UI 线程开始。成功接纳的回调最多执行一次；调用方不得用 `CheckAccess()` 复制 `InvokeAsync` 的线程分支。 |
| Disposal | Host Stop 后拒绝新 work；已排队 work 按实现策略取消或 drain。 |
| Nullability | work delegate 不得为空。 |
| Cancellation | 入队或执行前取消不得启动回调；回调开始后 token 仅为协作信号。命令最终状态通知不得复用已经取消的业务 token。 |
| Failure Behavior | `InvokeAsync` / `PostAsync` 返回任务只在回调执行完成后结束；dispatcher unavailable、runtime 不可用和回调异常通过该任务传播。实现允许 inline，但不能因 inline 改成直接穿透调用方的另一套失败语义。 |
| Diagnostics | 失败诊断包含 dispatcher type、thread id、operation id。 |
| Plugin Boundary | 插件 UI work 必须绑定 plugin owner，卸载时取消未完成 work。 |
| AOT / Trimming | Core 接口不引用 Avalonia/AtomUI 类型。 |
| Breaking Change Rules | 修改调度、取消、异常或线程识别语义属于 breaking change。 |
| Tests | `UiDispatcherIntegrationTests` 断言 Core 无 UI 引用和 unavailable 行为；MVVM `CommandTests` 断言 inline/排队通知失败采用相同隔离语义。 |

### `UnavailableUiDispatcher`

| Field | Contract |
| --- | --- |
| Type | `UnavailableUiDispatcher` |
| Namespace | `AtomUI.City.Core.Threading` |
| Assembly | `AtomUI.City.Core` |
| Stability | Preview |
| Feature | AUC-CORE-007 |
| Purpose | Core 默认 UI dispatcher，占位表达 UI 平台未接入。 |
| Owner | Core DI default |
| Created By | Core service registration。 |
| Lifetime | Host 生命周期内有效。 |
| DI Lifetime | Singleton。 |
| Thread Safety | 无内部 mutable UI state，可并发调用并稳定失败。 |
| Disposal | 无资源；Dispose 后仍不得执行 UI work。 |
| Nullability | work delegate 为 null 时优先参数异常。 |
| Cancellation | 已取消 token 返回取消结果，不执行 work。 |
| Failure Behavior | 未取消时返回 dispatcher unavailable 失败，不触碰 UI。 |
| Diagnostics | 诊断包含 dispatcher unavailable code 和 operation id。 |
| Plugin Boundary | 插件不能依赖 unavailable dispatcher 执行 UI work。 |
| AOT / Trimming | 不引用 UI 平台。 |
| Breaking Change Rules | 把默认行为改为静默成功或隐式线程切换属于 breaking change。 |
| Tests | `UiDispatcherIntegrationTests` 断言失败结果、取消和无 Avalonia 引用。 |

## 关键方法合同

### `ApplicationHost.CreateBuilder`

```text
Method: ApplicationHost.CreateBuilder
Feature: AUC-CORE-001
Purpose: 创建独立 Host builder。
Parameters: args 可为空，必须 defensive copy。
Return: 新的 IApplicationHostBuilder。
Nullability: args 可以为 null；返回值不能为空。
Cancellation: 无。
Exceptions or Result: 未来 options 非法时抛 ArgumentException。
Idempotency: 每次调用返回独立 builder。
Concurrency: 静态方法没有共享可变状态。
Side Effects: 读取 generic host 默认值和命令行配置。
Diagnostics: Build 前不写诊断。
Tests: ApplicationHostBuilderTests。
```

### `ApplicationHostBuilder.ConfigureServices`

```text
Method: ApplicationHostBuilder.ConfigureServices
Feature: AUC-CORE-001
Purpose: 在 Build 前登记应用级最终服务配置。
Parameters: configureServices 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException；delegate 失败由 Build 原样抛出并记录 UserServices stage。
Idempotency: Build 前多次调用按顺序登记，并在所有模块服务阶段完成后按相同顺序执行。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 调用本身只登记 delegate；delegate 仅在 Build 的 UserServices 阶段修改 Root service collection。模块阶段失败时不执行。
Diagnostics: delegate 导致 Build 失败时，失败诊断的 stage 为 UserServices。
Tests: ApplicationHostBuilderTests。
```

### `ApplicationHostBuilder.ConfigureHost`

```text
Method: ApplicationHostBuilder.ConfigureHost
Feature: AUC-CORE-001
Purpose: 在 Build 前注册 host options 修改。
Parameters: configureOptions 不能为 null。
Return: 当前 builder。
Nullability: delegate 不能为 null。
Cancellation: 无。
Exceptions or Result: null delegate 抛 ArgumentNullException；Build 后调用抛 InvalidOperationException。
Idempotency: Build 前多次调用按注册顺序执行。
Concurrency: 配置阶段不保证线程安全。
Side Effects: 只修改 pending host options。
Diagnostics: Build 失败诊断包含 options 校验上下文。
Tests: ApplicationHostBuilderTests; ApplicationHostOptionsTests。
```

### `ApplicationHostBuilderServiceProviderExtensions.UseDefaultServiceProvider`

```text
Method: ApplicationHostBuilderServiceProviderExtensions.UseDefaultServiceProvider
Feature: AUC-CORE-001
Purpose: 配置 Generic Host 最终创建的默认 Microsoft DI ServiceProvider。
Parameters: builder 和 configure 均不得为 null；configure 接收 Microsoft.Extensions.DependencyInjection.ServiceProviderOptions。
Return: 当前 IApplicationHostBuilder。
Nullability: null builder 或 configure 抛 ArgumentNullException。
Cancellation: 无；配置回调必须同步、短执行且不执行 IO。
Exceptions or Result: 配置回调异常在 Build 的 ServiceProviderOptions 阶段原样传播；Build 开始后再次调用抛 InvalidOperationException。
Idempotency: 每次调用追加回调，不去重；所有回调按注册顺序作用于同一个 ServiceProviderOptions。
Concurrency: 配置阶段不保证并发写安全；Build 串行冻结并应用快照。
Side Effects: 仅在至少注册一个回调时，以 DefaultServiceProviderFactory 配置内部 HostApplicationBuilder；不直接调用 BuildServiceProvider，不创建临时或第二个 Root Provider。未调用时保持 Generic Host 默认行为。
Diagnostics: 配置或容器构建失败沿用 HostBuildFailed，stage 为 ServiceProviderOptions 或 GenericHost。
Tests: ApplicationHostBuilderTests。
```

### `ApplicationHostBuilder.Build`

```text
Method: ApplicationHostBuilder.Build
Feature: AUC-CORE-001
Purpose: 冻结 builder，配置 modules，构建 root provider，并创建 Host。
Parameters: 无。
Return: 新的 IApplicationHost。
Nullability: 返回 Host 不能为空。
Cancellation: 无。
Exceptions or Result: 重复 Build 或 module graph 非法抛 InvalidOperationException；module 配置异常写入 diagnostics 后重新抛出。Build 主流程失败但回滚无异常时保留原异常和调用栈；回滚存在异常时抛 AggregateException，第一项为主失败，其后按清理发生顺序包含全部 cleanup failure。异步 ModuleRegistry 清理及 Generic Host 内异步-only Root 服务清理必须在共享 `ShutdownTimeout` deadline 内等待，不能因调用线程的非 pumping SynchronizationContext 或永不完成的 `DisposeAsync` 永久阻塞；Generic Host 支持 `IAsyncDisposable` 时不得退化为同步 `Dispose`。超时产生 `TimeoutException` cleanup failure，底层任务继续被观察但完成状态不作成功保证。
Idempotency: Build 只允许成功一次，后续调用必须失败。
Concurrency: Build 必须外部串行；失败回滚中的 `DisposeAsync` 从默认线程池调度器启动，调用线程只同步观察最终结果，不把异步 continuation 发布回正在等待的 UI SynchronizationContext。
Side Effects: 冻结 Services、Configuration 和注册方法；Configuration 冻结覆盖 Build 前后取得的递归 section/children、公开 Build root、Reload 和 provider mutation handle，`Sources`/`Properties.IsReadOnly` 同步切换为 true；创建不可变 IApplicationContext；成功时写 HostBuilt。
Diagnostics: 成功写 AUCHOST001；失败写 AUCHOST101；每个 Build 回滚清理失败另写 AUCHOST109，Module Dispose 同时写 AUCHOST106。
Tests: ApplicationHostBuilderTests; ApplicationHostModuleLifecycleTests; CoreHeadlessProcessTests; HostDiagnosticsTests。
```

### `ApplicationHostBuilderModularityExtensions.UseModule<TModule>`

```text
Method: ApplicationHostBuilderModularityExtensions.UseModule<TModule>
Feature: AUC-CORE-008
Purpose: 在 generated default root 之外显式增加一个启动根。
Parameters: builder 不能为 null；TModule 必须实现 IModule 且具有 parameterless constructor。
Return: 当前 IApplicationHostBuilder。
Nullability: builder 为 null 时抛 ArgumentNullException。
Cancellation: 无。
Exceptions or Result: Build 后调用因 builder frozen 失败；缺失 required dependency 在 ModuleGraph 阶段失败。
Idempotency: 同一 TModule 重复登记，或与 generated root 重复时去重。
Concurrency: 配置阶段不保证并发调用安全。
Side Effects: 仅登记强类型 factory 和显式 root；模块实例到 Host Build 验证 graph 后才创建。
Diagnostics: ModuleGraph 失败写 AUCHOST101/105。
Tests: ApplicationHostBuilderTests; GeneratedModuleCatalogTests; CoreHeadlessProcessTests。
```

### `IApplicationHost.StartAsync`

```text
Method: IApplicationHost.StartAsync
Feature: AUC-CORE-002
Purpose: 启动 generic host，创建 application scope，配置 contributions，并初始化 modules。
Parameters: cancellationToken 控制启动等待和 module 初始化。
Return: Host 进入 Running 后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 完成前取消时 Host 必须保持稳定且可释放。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；Stop 后再启动抛 InvalidOperationException；启动失败且回滚成功时写 diagnostics 后原样重新抛出；回滚存在失败时抛 AggregateException，第一项为启动主异常，其后按清理发生顺序包含全部 rollback failure。启动取消且回滚成功保持 Canceled；回滚失败时以 OperationCanceledException 为第一项返回 Faulted 聚合。
Idempotency: Running 状态重复调用不重新执行启动流程。
Concurrency: 并发调用由 Host state lock 串行化。
Side Effects: 创建 ApplicationScope，并且只执行一次 module initialization。
Diagnostics: 成功写 AUCHOST002；失败写 AUCHOST102；每个启动回滚失败另写 AUCHOST103，并与启动 transaction 共享 operationId。Diagnostics 不能替代异常传播。
Tests: ApplicationHostRuntimeTests; ApplicationHostLifecycleIntegrationTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests。
```

### `IApplicationHost.StopAsync`

```text
Method: IApplicationHost.StopAsync
Feature: AUC-CORE-002
Purpose: 关闭 modules，停止 application 和 host scopes，并停止已经启动的 generic host；Created 状态也必须清理 Build 阶段已经拥有的资源。
Parameters: cancellationToken 控制等待，但不能跳过已经开始的最小清理。
Return: Host 停止后完成的 Task。
Nullability: 返回 task 不能为空。
Cancellation: 取消只影响可取消等待；清理失败必须写 diagnostics。
Exceptions or Result: Dispose 后抛 ObjectDisposedException；shutdown 或 module dispose 失败聚合并写 diagnostics；同一 Host lifecycle 调用链中的递归 Stop 抛 InvalidOperationException，cleanup terminal 仍必须执行。Created 状态不调用未进入运行期的 module shutdown hook 或 `IHostedService.StopAsync`，但必须释放 module instances。
Idempotency: Stopped 后重复调用不重新执行 shutdown。
Concurrency: transaction Task 在 Stopping 可见前发布；外部并发调用由同一个停止事务处理，事务内部递归调用被拒绝。
Side Effects: 执行 ApplicationStop middleware，取消 host scope tree，逆序关闭或释放 modules，释放已创建的 application scope，并停止已经启动的 generic host。
Diagnostics: Stop cleanup transaction 完成时写 AUCHOST003；存在 cleanup failure 时另外写 AUCHOST103，并在全部最小清理结束后聚合抛出。
Tests: ApplicationHostRuntimeTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests。
```

### `LifecycleScope.CreateChild`

```text
Method: LifecycleScope.CreateChild
Feature: AUC-CORE-003
Purpose: 创建链接父级 cancellation 的 child scope。
Parameters: kind 和 id 标识 child scope。
Return: 新的 LifecycleScope。
Nullability: id 不能为 null 或空白。
Cancellation: child token 链接 parent token。
Exceptions or Result: parent 处于 stopping、stopped、disposing 或 disposed 时抛 InvalidOperationException。
Idempotency: parent running 时每次调用创建一个 child。
Concurrency: child 创建由 scope lock 保护。
Side Effects: 把 child 加入 parent 的 Children 快照来源。
Diagnostics: Host-owned cleanup 报告非法 scope mutation 时写 AUCHOST104。
Tests: LifecycleScopeTreeTests。
```

### `LifecycleScope.StopAsync`

```text
Method: LifecycleScope.StopAsync
Feature: AUC-CORE-003
Purpose: 取消当前 scope，并 leaf-first 停止 child scopes。
Parameters: 无。
Return: scope 停止后完成的 ValueTask。
Nullability: 返回 ValueTask 有效。
Cancellation: Stop 会取消 scope token；不接受外部 cancellation。
Exceptions or Result: 重复 Stop 是 no-op；同一 scope lifecycle 调用链中的递归 Stop 抛 InvalidOperationException；child failure 在挂接 diagnostics sink 时写 diagnostics。
Idempotency: 重复调用不改变顺序，也不重复取消。
Concurrency: scope lock 只负责发布唯一 transaction Task；外部并发调用共享事务，取消回调和 child stop 在 lock 外执行。
Side Effects: State 从 Running 变为 Stopping，再变为 Stopped。
Diagnostics: child stop 或 dispose 失败写 AUCHOST104。
Tests: LifecycleScopeTreeTests。
```

### `ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider`

```text
Method: ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider
Feature: AUC-CORE-004
Purpose: 阻止 module service configuration 阶段创建临时 ServiceProvider。
Parameters: services 不能为 null；`ServiceProviderOptions` overload 的 options 不能为 null；validateScopes 不改变 guard 行为。
Return: 无成功返回。
Nullability: services 或 options 为 null 时抛 ArgumentNullException。
Cancellation: 无。
Exceptions or Result: 三个 City overload 对有效参数始终抛 InvalidOperationException，错误消息必须说明禁止创建 temporary service provider；非测试 City 项目中的 City/微软 BuildServiceProvider、`IServiceProviderFactory<T>.CreateServiceProvider` 和 Microsoft Generic Host 构建/启动调用或方法引用产生 AUCANL0001 编译错误。
Idempotency: 每次调用都失败，不产生副作用。
Concurrency: 无共享状态。
Side Effects: 不构建 provider，不解析任何服务。
Diagnostics: module registry 捕获异常后写 AUCHOST106，context 包含 moduleType 和 stage。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ServiceConfigurationContext.PreConfigure`

```text
Method: ServiceConfigurationContext.PreConfigure<TOptions>
Feature: AUC-CORE-004
Purpose: 注册早期 options 默认值或模块约定。
Parameters: configure 不能为 null。
Return: void。
Nullability: configure 为 null 时抛 ArgumentNullException。
Cancellation: 无；action 必须是同步、短执行、无 IO 的配置动作。
Exceptions or Result: action 本身不在注册时执行；执行异常由 ExecutePreConfigure 调用所在模块阶段承接。
Idempotency: 每次调用追加一个 action，不去重。
Concurrency: module registry 串行调用；不保证并发写安全。
Side Effects: 只写当前 ServiceConfigurationContext 的 preconfigure store。
Diagnostics: action 执行异常由 module registry 记录 module lifecycle failure。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ServiceConfigurationContext.ExecutePreConfigure`

```text
Method: ServiceConfigurationContext.ExecutePreConfigure<TOptions>
Feature: AUC-CORE-004
Purpose: 对指定 options instance 按注册顺序执行早期配置动作。
Parameters: options 不能为 null。
Return: void。
Nullability: options 为 null 时抛 ArgumentNullException。
Cancellation: 无；调用者所在 async hook 必须在进入阶段前观察 token。
Exceptions or Result: action 抛出的异常原样向上抛出，并由 module registry 记录当前阶段诊断。
Idempotency: 每次调用都会重新执行当前 TOptions 的全部 action。
Concurrency: 不保证并发执行安全；由模块配置阶段串行调用。
Side Effects: 修改传入的 options instance，不创建 ServiceProvider。
Diagnostics: action 失败诊断包含 moduleType 和 stage。
Tests: ApplicationHostModuleLifecycleTests。
```

### `ModuleServiceCollection` mutating APIs

```text
Method: ModuleServiceCollection.Add/Clear/Insert/Remove/RemoveAt/index setter
Feature: AUC-CORE-004
Purpose: 在模块服务配置阶段转发受控服务注册，并在阶段结束后拒绝继续修改。
Parameters: service descriptor 或 index 遵守 IServiceCollection 原始规则。
Return: 与 IServiceCollection 原始 mutating API 一致。
Nullability: service descriptor 为 null 时遵守底层 collection 或 DI 扩展方法的参数异常。
Cancellation: 无。
Exceptions or Result: 服务配置阶段结束后抛 InvalidOperationException。
Idempotency: Freeze 后重复 mutating 调用稳定失败。
Concurrency: 不保证并发写安全。
Side Effects: 冻结前修改底层 IServiceCollection；冻结后无副作用。
Diagnostics: module 持有并越阶段修改时由调用方测试或上层诊断承接。
Tests: ApplicationHostModuleLifecycleTests。
```

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ExposeServicesAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceRegistrationOwnerAttribute` | 关键 contract | 新增、删除、重命名或 owner 激活语义变化必须更新本文档和 compatibility。 |
| `GeneratedServiceManifestAttribute` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IServiceRegistrar` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IServiceRegistrarContext` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IScopedDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ISingletonDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ITransientDependency` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ScopedServiceAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticRecord` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `HostDiagnosticSeverity` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IHostDiagnostics` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `InMemoryHostDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHost` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationContext` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationHost` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IApplicationHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecyclePipeline` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecyclePipelineBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScope` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScopeKind` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleScopeState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStage` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStageArea` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `LifecycleStages` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostBuilderModularityExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationHostBuilderServiceProviderExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationModuleAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratedModuleManifestAttribute` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModuleRegistrar` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModuleRegistrarContext` | generated contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationInitializationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationShutdownContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ContributionConfigurationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DependsOnAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModule` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IModuleRegistry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleBase` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDependencyDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleDescriptor` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleOrigin` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleServiceCollection` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleServiceCollectionBuildGuardExtensions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ServiceConfigurationContext` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `IUiDispatcher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `UnavailableUiDispatcher` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 遵守全局 [Effective Cancellation Token 合同](../../architecture/cancellation.md)：业务回调只接收一个显式末尾 token，context 不重复承载普通操作 token。
- 仅取消等待的参数必须命名为 `waitCancellationToken`；当前 `IApplicationHost.StopAsync` 属于 wait-only，真实 Stop transaction 继续执行最低清理。
- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
