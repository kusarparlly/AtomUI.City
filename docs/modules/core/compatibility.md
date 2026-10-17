# AtomUI.City.Core Compatibility

## 默认 ServiceProvider 配置

`UseDefaultServiceProvider(Action<ServiceProviderOptions>)` 是新增的 Preview API。它配置由 Generic Host 持有的唯一 Microsoft DI Provider，不暴露可变 Root ServiceCollection，也不创建临时 Provider。未调用该 API 的应用继续使用现有 Generic Host 默认行为。修改回调顺序、Builder 冻结行为、官方 `ServiceProviderOptions` 参数类型或单 Provider 所有权规则属于 breaking change。

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- Core 不允许引用 Avalonia、AtomUI、Roslyn、CLI、Templates 或 Testing 生产程序集。
- ApplicationHostBuilder Build 成功或失败后必须冻结服务注册入口及所有通过 Builder 逃逸的 Configuration mutation handle；`Sources`/`Properties.IsReadOnly` 必须同步反映冻结状态。补齐递归 section/child/root/provider Guard 与只读状态报告属于执行既有冻结合同的缺陷修复。
- LifecyclePipeline stage 顺序必须稳定，同一 stage 内 middleware 顺序必须稳定。
- LifecycleScope Parent Stop 必须把快照后正常完成的 child Dispose 视为已清理，并通过 child Stop transaction 保留真实 failure；该内部 handoff 不放宽公开 Stop-after-Dispose 合同。
- StartAsync、StopAsync、DisposeAsync 和 Dispose 必须有明确幂等规则。
- 模块配置阶段禁止 BuildServiceProvider；常用入口由 runtime guard 拒绝，非测试 City 项目中的显式 Provider 创建和 Microsoft Generic Host 构建/启动入口由 `AUCANL0001` 阻止编译。运行期服务解析只能发生在 Provider 构建后的声明生命周期阶段。
- IUiDispatcher 只定义抽象，Core 不提交真实 UI work。
- 公共不可变模型必须在构造或接纳边界拒绝集合内部 null、未知枚举及 `default` struct；公开的进程级表必须使用真正的只读包装，不能只依赖 `IReadOnlyList<T>` 的静态类型。
- `ApplicationInitializationContext.ApplicationScope` 是 Host 创建的 Core 通用生命周期能力，与当前 `IApplicationHost.ApplicationScope` 保持实例同一；它不是具体功能模块参数。Module 可以绑定资源或创建 child scope，但终止所有权保留给 Host。`ApplicationShutdownContext` 不重复注入该 Scope，模块关闭必须加入初始化阶段建立的唯一终止事务。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。

Core 的当前公开签名冻结在 `src/AtomUI.City.Core/PublicAPI.Shipped.txt`。`Microsoft.CodeAnalysis.PublicApiAnalyzers` 在每次编译中拒绝未登记的新 API 和已登记但被删除或改签名的 API；`CS1591` 在 Core 中重新启用并升级为错误。Release 门禁还会构建 `net10.0`/`net8.0` 并通过 SDK package validation 比较兼容目标框架的程序集表面。当前尚无对外发布包，因此首份源码基线就是发布前基线；首次发布后再增加 NuGet baseline package 比较。

Core 包的产品主页固定为上游 `https://github.com/AtomUI/AtomUI.City`，但 NuGet `RepositoryUrl` 与 SourceLink 必须从实际执行构建的标准 GitHub remote 推导，禁止硬编码为尚不包含当前 commit 的仓库。PR 合并前的 Engineering RC 指向 `https://github.com/kusarparlly/AtomUI.City` 中实际存在的干净 commit；正式包只能在合并后从上游仓库的干净 commit 构建，并指向上游。自定义 SSH host alias 可以保留为 push URL，但 SourceLink 所读取的 fetch URL 必须是可识别的标准 GitHub URL。包中的 repository commit、PDB SourceLink 和实际二进制源码必须一致。

`ApplicationHostBuilder`、`ModuleRegistry` 与 `IModuleLifecycleController` 是 Core 内部实现，不属于冻结的 public surface。应用通过 `IApplicationHostBuilder` 建造 Host；public `IModuleRegistry` 只提供已加载模块元数据查询，不提供 Configure、Initialize、Shutdown 或 Dispose 控制能力。Core 尚未对外发布，移除这些误暴露的 Preview 控制成员属于 1.0 前 API 边界纠正。

`IUiDispatcher.InvokeAsync` 使用无 token 的便捷重载和显式 `CancellationToken` 重载，不在多个重载上叠加可选参数。这既保持调用便利，也避免以后增加重载时产生源码绑定歧义。

`IUiDispatcher` 的 Preview 行为由 ADR 0013 冻结：dispatcher 是 inline/排队执行的唯一裁决者；成功接纳的回调最多执行一次；返回任务代表回调完成；回调异常通过任务传播。`CheckAccess` 保留为同步前置条件能力，不得被普通异步调用方用于复制调度和失败分支。

## 1.0 Preview 命名空间标准化

Core 在 1.0 冻结前完成一次显式 breaking migration，程序集不变，命名空间统一增加 `Core` 层级：

| 旧命名空间 | 新命名空间 |
| --- | --- |
| `AtomUI.City.Hosting` | `AtomUI.City.Core.Hosting` |
| `AtomUI.City.Lifecycle` | `AtomUI.City.Core.Lifecycle` |
| `AtomUI.City.Modularity` | `AtomUI.City.Core.Modularity` |
| `AtomUI.City.DependencyInjection` | `AtomUI.City.Core.DependencyInjection` |
| `AtomUI.City.Diagnostics` | `AtomUI.City.Core.Diagnostics` |
| `AtomUI.City.Threading` | `AtomUI.City.Core.Threading` |

仓库内生产模块、测试、模板、MSBuild contract 和 generator metadata 已同步迁移。1.0 发布后这些新命名空间进入常规兼容性承诺。

## 1.0 Preview 服务配置入口收口

`IApplicationHostBuilder.Services` 和 `ApplicationHostBuilder.Services` 在 1.0 冻结前移除。应用必须把直接集合修改迁移为延迟服务配置：

```csharp
// Before
builder.Services.AddSingleton<AppService>();

// After
builder.ConfigureServices(services => services.AddSingleton<AppService>());
```

`ConfigureServices` delegate 从调用时立即执行改为 Build 时在全部模块服务阶段之后执行。依赖 delegate 立即副作用或立即异常的代码必须迁移到显式的普通方法；服务注册失败现在由 `Build()` 抛出并写入 `UserServices` stage 诊断。

模块的 `PreConfigureServicesAsync`、`ConfigureServicesAsync` 和 `PostConfigureServicesAsync` 已收紧为同步的 `PreConfigureServices`、`ConfigureServices` 和 `PostConfigureServices`。原异步 hook 中纯 DI 注册代码直接迁移到对应同步 hook；网络、磁盘、数据库或其他需要等待的初始化迁移到 `OnPreApplicationInitializationAsync`、`OnApplicationInitializationAsync` 或 `OnPostApplicationInitializationAsync`，由 `StartAsync` 驱动。

## 本轮新增 API

- `ApplicationHostOptions.ApplicationId`：必填的稳定应用产品标识。
- `ApplicationHostOptions.ApplicationVersion`：可选显式版本；默认从入口程序集解析。
- `IApplicationContext.ApplicationId`、`ApplicationInstanceId`、`ApplicationVersion`：补齐不可变应用实例身份。
- `ConfigureLifecycle(Action<LifecyclePipelineBuilder>)`：注册 Host lifecycle middleware。
- `GetBuildDiagnostics()`：Build 抛异常后读取结构化诊断。
- `ApplicationHostOptions.DiagnosticsCapacity`：控制默认 Host 诊断容量，默认 1024。
- `InMemoryHostDiagnostics(int capacity)`、`Capacity`、`DroppedCount`：提供有界内存诊断。
- `LifecycleScope.CreateRoot(..., IHostDiagnostics)`：为独立 Scope tree 接入清理诊断。
- `ModuleServiceCollectionBuildGuardExtensions.BuildServiceProvider(..., bool/ServiceProviderOptions)`：补齐常用临时 Provider 入口的稳定失败行为。
- `ApplicationModuleAttribute`：允许可执行应用项目声明唯一 generated default root，从而省略 `UseModule<AppModule>()`。
- `GeneratedModuleManifestAttribute`、`IModuleRegistrar`、`IModuleRegistrarContext`：连接编译期生成的模块 Catalog 与 Host Build；应用业务代码不直接调用。
- `ServiceRegistrationOwnerAttribute`：把当前程序集的自动服务注册唯一归属到由当前程序集声明的静态 Module；registrar 不得登记到其他程序集的 owner，仅当本地 owner 位于启动依赖闭包中时进入 Root DI。
- `GeneratedServiceManifestAttribute`、`IServiceRegistrar`、`IServiceRegistrarContext`：连接生成服务清单与 Host Build；属于 generated-code bridge，应用业务代码不直接调用。
- `LifecycleContext.OperationId`：增加只读 lifecycle transaction id；构造函数末尾的可选参数保持现有调用源码兼容。
- 1.0 Preview 取消合同收口：`LifecycleContext.CancellationToken` 被移除；`LifecycleMiddleware`、pipeline terminal、`IModule` lifecycle callback 和 `ExecuteAsync` 使用显式末尾 token，其中框架回调 token 不可省略。`IApplicationHost.StopAsync` 的 wait-only 参数改名为 `waitCancellationToken`。
- `LifecyclePipelineBuilder.Use<TMiddleware>(...)`：允许显式声明稳定 middleware 诊断类型；现有 delegate overload 保留并使用兼容推断。
- `HostDiagnosticIds.LifecycleMiddlewareFailed` (`AUCHOST108`)：定位 lifecycle middleware 的 stage、类型、operationId 和异常类型。
- `HostDiagnosticIds.HostBuildCleanupFailed` (`AUCHOST109`)：定位 Build 失败回滚中的 Generic Host 或 Module 清理异常。
- `ApplicationInitializationContext.ApplicationScope`：向运行期 Module 提供 Host 已创建的非空 ApplicationScope；初始化 context 构造函数同步要求显式传入该 Scope。当前尚无对外发布包，本次签名调整作为 1.0 前生命周期上下文收口进入 Public API baseline。

`UseModule<TModule>()` 保留为显式附加根和无生成清单时的兼容入口；它与 generated default root 合并并去重。Library 和 Plugin 中的 `[ApplicationModule]` 现在由 `AUCGEN008` 拒绝。

自动服务的兼容语义是“编译期全局发现、运行时按已选 Module 激活”。引用一个程序集本身不会把其中所有服务注入 Root。Singleton/Scoped 多 contract 的非 disposable 服务共享一个容器拥有的 backing instance；disposable 多 contract 当前由 Generator 拒绝，避免 net8/net10 forwarding registration 的重复 disposal 风险。Strict AOT 强类型构造 factory 仍为后续能力，当前生成代码使用强类型 service descriptor，构造函数激活遵循 Microsoft DI。

DI Attribute 解释以 Generator 的 Roslyn metadata reader 为唯一生产规则源。Core runtime 不提供反射式 `ServiceRegistrationMetadata` reader；重新引入第二套 Attribute/lifetime/expose 解释实现属于兼容性与 NativeAOT 风险，必须作为独立设计变更审查。

Windows `net8.0` NativeAOT 应用若保留 Generic Host 的 EventLog 可达路径，需要显式提供 `System.Threading.AccessControl`；Core MVP AOT 产品夹具已采用仅 AOT 模式的直接引用，并把 ILC `will always throw` 纳入失败门禁。该要求来自 Generic Host 的 Windows 日志依赖闭包，不改变自动服务注册协议。

引用程序集 registrar 形成的是图而不是树。同一 registrar 可能经多条引用路径到达应用聚合入口，必须按 registrar identity 幂等跳过，不能重复产生 descriptor。Owner type 不是 registrar identity：不同 registrar 声明同一 owner 属于跨程序集所有权冲突，必须在 Host Build 确定性失败并报告双方身份，禁止静默忽略后到注册。不同 owner 的同 contract 注册不属于 registrar 去重规则，继续遵守普通冲突、TryAdd 或 Replace 合同。

Preview 阶段的 generated-code bridge 使用 `IServiceRegistrarContext.RegisterRegistrar(Type, Func<IServiceRegistrar>)` 表达引用边。Catalog 在 factory 执行前按 registrar type 去重，并为每次真实执行建立不可逃逸、绑定实际 registrar identity 的 context。未来改变该签名、把去重键改回 owner，或允许调用方自行声明 registrar identity，均属于生成器与 Core 必须同步升级的协议变更。

跨项目扩展采用“本地 Module + `DependsOn`”模型：A 引用 B 时，A 的业务服务归属 A 自己声明的 Module；选择 B 不得隐式激活 A。允许第三方 registrar 无条件向外部 owner 注入服务、或把程序集引用等同于 owner 授权，均属于破坏 Root DI 隔离的兼容性变更。

## Host 诊断码兼容合同

| Code | Name | 当前稳定语义 |
| --- | --- | --- |
| `AUCHOST001` | HostBuilt | Generic Host 与 Core runtime services 成功构建后写入 Info 记录。 |
| `AUCHOST002` | HostStarted | Host Start transaction 成功完成并进入 Running 后写入 Info 记录。 |
| `AUCHOST003` | HostStopped | Host Stop transaction 完成且状态发布为 Stopped 后写入 Info 记录；存在清理失败时仍先写本记录，再写 `AUCHOST103`。 |
| `AUCHOST101` | HostBuildFailed | Host Build 任一阶段失败时写入 Error 记录，context 至少定位 build stage 和 exception type。 |
| `AUCHOST102` | HostStartFailed | 非正常取消导致 Start transaction 失败时写入 Error 记录，context 包含 operation、operationId 和 exception type。 |
| `AUCHOST103` | HostStopFailed | Stop 清理聚合失败或启动 rollback 失败时写入 Error 记录；同一 transaction 可因多个 rollback failure 写入多条。 |
| `AUCHOST104` | LifecycleScopeCleanupFailed | LifecycleScope child 或 cleanup operation 失败时写入 Error 记录，并继续清理其他资源。 |
| `AUCHOST105` | ModuleGraphFailed | Build 的 ModuleGraph 阶段失败时与 `AUCHOST101` 一起写入 Error 记录。 |
| `AUCHOST106` | ModuleLifecycleFailed | 模块 lifecycle hook 失败时写入 Error 记录，context 定位 module id、module type、stage 和 exception type。 |
| `AUCHOST107` | DispatcherUnavailable | 默认 `UnavailableUiDispatcher` 被调用时以该 code 标识失败消息；当前不向 `IHostDiagnostics` 额外写 record。 |
| `AUCHOST108` | LifecycleMiddlewareFailed | 非正常取消的 lifecycle middleware 异常写入 Error 记录，强类型 Stage 及 middlewareType、operationId、exceptionType 为必需定位字段。 |
| `AUCHOST109` | HostBuildCleanupFailed | Build 失败回滚中的每个 cleanup failure 写入 Error 记录，context 包含 buildStage、resourceKind、exceptionType；Module 构造回滚还包含 moduleId 和 moduleType；异步 cleanup timeout 另外包含总预算、剩余等待、是否已启动及是否可能仍在运行。 |

诊断码一经公开不得删除、复用或改变名称映射。修改触发条件、severity、错误/成功含义或删除必需 context 字段属于兼容性变更；message 文案可以优化，新增可选 context 字段属于向后兼容扩展。

## LifecycleScope 状态机兼容合同

`LifecycleScope` 当前稳定状态迁移为 `Running -> Stopping -> Stopped/Faulted -> Disposing -> Disposed`。Scope 创建完成后立即处于 `Running`；正常停止进入 `Stopped`，停止失败进入 `Faulted`，两者均可继续释放并最终进入 `Disposed`。

`LifecycleScopeState.Created`、`Starting`、`CancelRequested` 和 `UnloadPending` 是保留枚举值，当前 `LifecycleScope` 不会产生。保留值不构成当前可观察行为；未来开始产生任一保留状态属于状态机行为兼容性变更，必须更新 API 合同、迁移说明和测试后才能启用。

## 1.0 Preview ApplicationContext 收口

`IApplicationContext` 从可变运行时数据袋收口为不可变应用实例描述符。以下成员被移除：`Configuration`、`Services` 和 `Properties`；配置与服务分别迁移到 DI 和 `IApplicationHost.Services`。具体 `ApplicationContext` 类型不再公开或注册到 DI，模块上下文统一依赖 `IApplicationContext`。

`IApplicationHostBuilder.Properties` 和具体 Builder 的同名属性被移除。Core 构建诊断继续通过 `GetBuildDiagnostics()` 提供，内部状态不再暴露给应用开发者。

现有应用必须在 Build 前配置稳定 id：

```csharp
builder.ConfigureHost(options =>
{
    options.ApplicationId = "Company.Product";
    options.ApplicationName = "Product";
});
```

`AppDataPath` 的 Windows 默认基础目录由 Roaming `ApplicationData` 改为 `LocalApplicationData`，末级目录仍使用 `ApplicationName`。框架不自动创建该目录；需要旧目录数据的应用必须自行迁移。本轮不提供 Roaming 替代属性，跨平台语义化路径服务记录在 Hosting 的 deferred 专题中。

## 1.0 Preview 动态发现入口撤回

`ApplicationHostOptions.AllowDynamicDiscovery` 和 MSBuild 属性 `AtomUICityAllowDynamicDiscovery` 被移除。它们此前没有任何运行时或构建行为，不能作为兼容性合同保留。Generated Module Catalog 只进行编译期静态发现；运行时程序集扫描未来必须作为独立 Feature 同时交付扫描边界、AOT/trimming 诊断和测试后才能重新公开。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
