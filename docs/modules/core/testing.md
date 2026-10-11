# AtomUI.City.Core Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 两轮 Core 门禁

第一轮执行 Unit、Contract 和 RuntimeLifecycle 测试，排除进程夹具：

```text
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --filter "FullyQualifiedName!~CoreHeadlessProcessTests"
```

第二轮执行无 UI Headless Console 进程测试。测试通过 `dotnet AtomUI.City.Core.HeadlessApp.dll --test-scenario <scenario>` 启动真实子进程，不加载 Avalonia 或 AtomUI：

```text
dotnet test tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj --no-build --filter "FullyQualifiedName~CoreHeadlessProcessTests"
```

进程测试必须遵守 `TEST-INFRA-001`：fixture 入口捕获普通顶层异常并以 `stderr + 非零退出码` 报告；测试启动器并行读取 `stdout`/`stderr`，使用有界超时并在超时后终止整个进程树。Windows 下启动器与 fixture 只为测试进程设置 `SEM_NOGPFAULTERRORBOX`，禁止修改全局 WER。任何等待人工点击的错误框、异常进程挂起、错误退出码被当作成功或异常栈丢失都使相应门禁失败。

Headless 必须覆盖正常生命周期、启动失败后的干净补偿与原异常保留、启动主异常和多项回滚异常的有序聚合、关闭失败聚合、lifecycle middleware 诊断字段与事务关联、`next` fire-and-forget 收拢拒绝/过期失效/64 路并发单所有权/合法直接返回、RunAsync cancellation、并发/递归停止、Stop-before-Start 完整清理、Build 失败时非 pumping SynchronizationContext 下的完整 Registry/构造中途异步清理和 Generic Host 异步-only Root 服务清理、永不完成的 `DisposeAsync` 受共享 deadline 限制并产生可观测 timeout、LifecycleScope Parent Stop/Child Dispose 竞态、Host 驱动的 ModuleRegistry 唯一终止事务、public Registry 只读能力边界、跨程序集循环模块在 Build 验证前零实例化、Builder Configuration 递归冻结、Diagnostics 输入/完成/并发边界、public enum/Attribute/descriptor/default struct 非法边界、全局只读表防篡改以及模块后用户服务覆盖场景。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。 | 至少一个明确测试断言，不能只断言流程成功。 |
| Application initialization context 必须携带 Host 创建的真实 ApplicationScope，且不能接受 null 或为 Module 伪造另一个 Scope。 | 单元测试断言三个 initialization hook 共享显式 Scope；Host 集成测试断言 context 与 `IApplicationHost.ApplicationScope` 引用相同；Stop-before-Start 不产生 initialization context。 |
| ApplicationHostBuilder 必须延迟执行用户服务配置，并在 Build 成功或失败后冻结捕获的服务集合及全部公开 Configuration mutation handle。 | 至少断言提前捕获的 section/child/root/provider、延迟 children enumeration 和 Reload 均不能绕过冻结；`Sources`/`Properties` 的 `IsReadOnly` 必须在 Build 前为 false、成功或失败后为 true，不能只断言写入抛异常。 |
| LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定；`next` 不得逃逸当前 invocation/transaction。 | 除顺序外，至少断言正常 await/直接返回、fire-and-forget 收拢后失败、保存后调用拒绝、并发仅一个 owner 进入 terminal，不能只断言流程成功。 |
| StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则；Stop-before-Start 必须清理 Build 资源且不调用未进入运行期的 hook，Stopped 后再次 Start 必须失败。 | 至少一个明确测试断言，不能只断言流程成功。 |
| 模块配置阶段禁止 BuildServiceProvider 和运行期服务解析。 | 至少一个明确测试断言，不能只断言流程成功。 |
| IUiDispatcher 只定义抽象，Core 不提交真实 UI work。 | 至少一个明确测试断言，不能只断言流程成功。 |

## 测试矩阵

本矩阵与 [features.md](features.md) 一一对应：当前覆盖 8 个 Core 1.0 Feature（`AUC-CORE-001` 至 `AUC-CORE-008`），验收状态为 `8/8 Verified`。Feature 的增删、拆并或状态变化必须同时更新两份文档，不能仅修改其中一处。

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-CORE-001 | RuntimeLifecycle + Headless | ApplicationHostBuilderTests; ApplicationHostOptionsTests; ApplicationHostRuntimeTests; ApplicationHostModuleLifecycleTests; CoreHeadlessProcessTests | 断言模块后用户服务配置、覆盖顺序、Build 成功/失败后的 Services 与递归 Configuration handle 冻结、64 节点逃逸句柄、应用身份必填、Context 不可变、Local AppDataPath、Dispose 后读取、Build 主异常与多个 cleanup failure 有序聚合、非 pumping SynchronizationContext 下 ModuleRegistry 与 Generic Host 异步-only 服务回滚完成、永不完成的 cleanup 在共享 deadline 后返回并标记后台状态以及真实进程启停。 | Options/UserServices 失败诊断；非法目录名；模块失败跳过用户 delegate；Generic Host/Module 构造或 Dispose 回滚失败不阻止后续资源；完整 Registry、模块构造中途及 Generic Host Root Provider 的异步 Dispose 回滚不得捕获阻塞中的 UI context 或无限等待；每资源独立重置 timeout 必须失败；section/child/root/provider/Reload 绕过必须被拒绝；Run cancellation 后完整清理。 | Verified |
| AUC-CORE-001 default provider options | Unit + RuntimeLifecycle | ApplicationHostBuilderTests | 断言 `UseDefaultServiceProvider` 的 null guard、链式返回、按序合并、Build 后冻结、`ValidateOnBuild` 缺失依赖检查、`ValidateScopes` lifetime 检查、默认行为保持以及唯一 Provider/Singleton 释放。 | 配置 delegate 失败必须归因到 ServiceProviderOptions build stage；不得创建临时或第二个 Root Provider。 | Verified |
| AUC-CORE-002 | RuntimeLifecycle + Headless | ApplicationHostRuntimeTests; LifecycleMiddlewarePipelineTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests | 断言 stage 顺序、Host 管线接入、operationId 传播、准确 middleware 归因、`next` invocation/transaction 双有效期、fire-and-forget 收拢拒绝、32/64 路并发单所有权、required terminal 完整完成、并发事务共享、内部重入拒绝、干净启动回滚保留主异常、普通/取消启动的失败回滚以主异常为第一项有序聚合、Stop-before-Start 的 scope/module 完整清理和 cleanup terminal 保证执行。 | middleware 异常、`next` 丢弃/缓存/延迟/并发重复调用、terminal 异常、启动主异常后的多个 shutdown/scope/host rollback failure、取消启动后的 rollback failure、diagnostics sink 失败、递归调用、调用方取消、并发 Dispose、Created 状态 module dispose 聚合失败和超时均不跳过最小清理。 | Verified |
| AUC-CORE-003 | RuntimeLifecycle + Headless + NativeAOT | LifecycleScopeTreeTests; CoreHeadlessProcessTests | 断言未知 kind 在分配/挂接前拒绝、leaf-first、并发事务合并、锁外 cancellation callback、Parent 对 stale snapshot child 的 tolerant Stop handoff、64-child 混合 Dispose、重入拒绝、故障隔离和诊断。 | 未知 kind 不得产生 root 或污染 Children；正常并发 Dispose 不产生 failure；真实 child/cancellation callback failure 不得被吞掉并继续清理其他 child；公开 Stop-after-Dispose 保持拒绝。 | Verified |
| AUC-CORE-004 | Unit + RuntimeLifecycle + Headless + NativeAOT | ModuleBaseTests; ModuleDescriptorTests; ApplicationHostModuleLifecycleTests; ApplicationHostIndustrialLifecycleTests; ModuleRegistryConcurrencyTests; CoreHeadlessProcessTests | 断言依赖排序、同步服务配置合同、非 pumping SynchronizationContext 下 Build 不发布异步 continuation、异步初始化只在 StartAsync 执行、Application scoped provider、三个初始化阶段收到与 Host 相同的非空 ApplicationScope、正向阶段唯一事务、失败不可重试、Shutdown-first/Dispose-first 唯一终止事务、5 模块逆序关闭和 64 路并发无 hook/dispose 重叠；public Registry 仅公开 Modules，Root DI 不暴露 internal controller 或 disposal capability。 | Build 失败释放实例；服务配置不得执行异步 I/O；初始化 context 的 null Scope 必须立即拒绝；部分初始化回滚；正向阶段与终止竞争；internal 同类/跨类递归；业务代码抢占生命周期；关闭失败聚合。 | Verified |
| AUC-CORE-005 | Generator + RuntimeLifecycle + Headless + Dogfood + NativeAOT | ServiceRegistrationAttributeTests; ServiceRegistrationMetadataReaderTests; AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests; CoreHeadlessProcessTests; CoreMvpCliProcessTests | 断言合法 lifetime/exposed services、未知 lifetime 不降级为 Transient、null exposed type 不过滤、非法声明产生 AUCGEN005 且无 registrar、owner 清单、跨程序集菱形路径按 registrar identity 幂等、不同 registrar 争用同一 owner 确定性失败、本地 Module + DependsOn 扩展隔离、只激活已选 Module、120 种 root 顺序、32 种组合、64 个并发 scope 和 Pre -> Generated -> Configure -> Post 顺序。 | 未知 enum、null Attribute 元素、owner 缺失/重复/非 Module/非本程序集声明、跨程序集 owner 冒充、注册冲突、Replace+TryAdd、disposable 多 contract、未选 Module/HostedService 污染 Root 必须失败或不发生。 | Verified |
| AUC-CORE-006 | RuntimeLifecycle + Headless + NativeAOT | HostDiagnosticsTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests | 断言 AUCHOST001-109、Build failure 读取、每个 Build cleanup failure 的 resource/build stage 上下文、有界顺序、dropped count、middleware 关键字段、Host 摘要 operationId 关联、非法 record 初始化拒绝、Host Dispose 后只读以及 512 路 Write 与 32 路 Complete/Dispose 的原子边界。 | 诊断 collector 写入或完成失败不能中断 Host；Build cleanup diagnostics 失败不能覆盖主异常或清理异常；正常 cancellation 不写 middleware failure；完成后写入必须拒绝。 | Verified |
| AUC-CORE-007 | RuntimeLifecycle | UiDispatcherIntegrationTests | 断言不可用 dispatcher 返回失败且 Core 不引用 Avalonia。 | 默认 dispatcher 不可用；Presentation 必须替换真实实现。 | Verified |
| AUC-CORE-008 | Generator + RuntimeLifecycle + Headless + NativeAOT | ModuleDependencyGraphBuilderTests; ModuleDescriptorTests; GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests | 断言自动根和显式根汇入同一 root set、生成 Catalog 只激活依赖闭包、编译期 AUCGEN003、Build 期先验证再实例化、合法菱形拓扑和跨程序集循环的 constructor count 为零；原生进程执行 generated-modules/module-graph-cycle。 | 多默认根、Library 默认根、非模块默认根、非法 factory、registrar 冲突、缺失依赖、直接/间接/跨程序集循环和 generated registrar metadata 缺失。 | Verified |

Core Headless NativeAOT 门禁（AUC-CORE-001 / AUC-CORE-002 / AUC-CORE-003 / AUC-CORE-004 / AUC-CORE-005 / AUC-CORE-006 / AUC-CORE-008）：

```powershell
dotnet restore fixtures/AtomUI.City.Core.HeadlessApp/AtomUI.City.Core.HeadlessApp.csproj -r win-x64 -p:AtomUICityHeadlessPublishAot=true
dotnet publish fixtures/AtomUI.City.Core.HeadlessApp/AtomUI.City.Core.HeadlessApp.csproj -c Release -r win-x64 -p:AtomUICityHeadlessPublishAot=true --no-restore
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario generated-modules
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario startup-rollback-failure
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario stop-before-start
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario configuration-freeze
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario scope-dispose-race
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario module-registry-race
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario module-registry-ownership
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario module-graph-cycle
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario diagnostics-contract
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario public-boundaries
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario sync-service-configuration
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario build-cleanup-failure
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario build-async-cleanup-context
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario build-generic-host-async-cleanup
output/bin/Release/AtomUI.City.Core.HeadlessApp/net10.0/win-x64/publish/AtomUI.City.Core.HeadlessApp.exe --test-scenario build-async-cleanup-timeout
```

需要直接复核 `net8.0` Headless 原生程序时，可在 restore/publish 命令中同时覆盖
`AtomUICityDevelopTargetFramework=net8.0` 与 `AtomUICityTargetFrameworks=net8.0`。测试产品仅在
`net8.0 + NativeAOT` 条件下引用 `System.Threading.AccessControl`，用于补齐 Generic Host Windows
EventLog 可达路径的依赖闭包；该引用不会进入 Core 包，也不会进入普通 JIT Headless 构建。

## AUC-CORE-005 Core MVP Dogfood 门禁

`fixtures/AtomUI.City.Core.Mvp` 是只引用 Core 与 Generator 的永久无 UI 产品夹具，不依赖 `AtomUI.City.Cli`、Testing 或其他 City 产品模块。它包含 8 个独立 Module owner 程序集和 32 个自动服务声明；正常闭包选择 6 个 Module/27 个服务，另外两个 Module 分别验证未选隔离与跨程序集冲突。

JIT 产品矩阵：

```powershell
dotnet output/bin/Debug/AtomUI.City.Core.MvpCli/net10.0/AtomUI.City.Core.MvpCli.dll verify --scenario all
```

成功 JSON 必须包含：`selectedModuleCount=6`、`selectedServiceCount=27`、`permutationCount=120`、`combinationCount=32`、`concurrentScopeCount=64`，且 `failures` 为空。

Windows NativeAOT 门禁同时覆盖 Core 的两个 Release TFM：

```powershell
./engineering/check-core-mvp-aot.ps1
```

该脚本必须使用仓库锁定 SDK，分别发布并运行 `net10.0/win-x64` 与 `net8.0/win-x64`，且发布日志不得出现 `IL2xxx` trimming warning、`IL3xxx` AOT warning 或 ILC `will always throw` 风险。CI 通过独立 Windows matrix job 强制执行。普通 JIT Headless 成功不能替代本门禁。

Core MVP 的 AOT 模式显式引用 `System.Threading.AccessControl`，补齐 Generic Host Windows EventLog 可达路径在 `net8.0` 原生发布中的依赖闭包；普通 JIT 构建不携带该测试产品专用引用。

## 性能基线建设

Core 的功能门禁不能替代性能测量。待当前功能工程基本稳定后，建立独立、可重复运行的 Benchmark，并以首次稳定测量结果作为观察基线。必测场景包括：

- Host 冷启动，以及 Build/Start/Stop 的耗时和分配量；
- 10、50、100、500 等不同模块图规模下的构建成本和增长曲线；
- Diagnostics 高并发写入的吞吐、分配和丢弃行为；
- 批量 LifecycleScope 创建、停止和释放；
- DI 首次解析、重复解析及不同 lifetime 的解析成本。

基线建立分三步执行：先保存结果但不阻断；数据稳定后对明显退化产生告警；完成性能优化并确认环境波动范围后，才为关键分配量、复杂度趋势和耗时指标设置 CI/发布阈值。任何基线都必须附带机器和运行环境，禁止直接把一次本地耗时写成跨机器的工业承诺。

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
