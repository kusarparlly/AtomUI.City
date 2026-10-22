# AtomUI.City.Core Hosting 合同

## 适用范围

本专题属于 `AtomUI.City.Core` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Hosting` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Core` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-CORE-001 | Application Host Builder | ApplicationHostBuilderTests; ApplicationHostRuntimeTests |
| AUC-CORE-002 | Lifecycle Pipeline | ApplicationHostRuntimeTests; LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests; ApplicationHostIndustrialLifecycleTests; CoreHeadlessProcessTests |
| AUC-CORE-003 | Lifecycle Scope Tree | LifecycleScopeTreeTests |
| AUC-CORE-004 | Module Contract | ModuleAttributeTests; ModuleBaseTests; ModuleDescriptorTests |
| AUC-CORE-005 | DI Registration Markers | ServiceRegistrationAttributeTests; AtomUICityIncrementalGeneratorDependencyInjectionTests; GeneratedServiceRegistrationCatalogTests |
| AUC-CORE-006 | Host Diagnostics | HostDiagnosticsTests |
| AUC-CORE-008 | Generated Module Catalog | GeneratedModuleCatalogTests; AtomUICityIncrementalGeneratorModularityTests; CoreHeadlessProcessTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## Verified Runtime Baseline

当前 Core Host 的规范化执行顺序如下：

```text
Build
-> validate ApplicationHostOptions
-> create immutable IApplicationContext descriptor
-> load generated Module Catalog and resolve startup roots
-> build module graph and configure module services
-> apply deferred user ConfigureServices callbacks
-> build GenericHost
-> create bounded diagnostics and HostScope

StartAsync
-> ApplicationStart middleware
-> GenericHost.StartAsync
-> create Application ServiceScope and ApplicationScope
-> ConfigureContributions
-> ModuleInitialize middleware and three initialization hooks
-> ModuleStart middleware
-> Running

StopAsync
-> ApplicationStop middleware
-> cancel HostScope and descendants
-> ModuleStop middleware and reverse module shutdown
-> dispose Application ServiceScope
-> GenericHost.StopAsync
-> Stopped
```

并发 Start/Stop 合并到各自的单一事务。启动失败时必须逆序补偿模块并进入 Faulted：回滚完全成功则原样抛出启动主异常；回滚存在失败则在所有最低清理完成后抛出 `AggregateException`，第一项固定为启动主异常，其后按发生顺序包含全部回滚异常。启动取消且回滚成功时保持 Canceled；启动取消但回滚失败时返回以 `OperationCanceledException` 为第一项的 Faulted 聚合。停止调用方的 cancellation 只取消等待；内部使用 `ShutdownTimeout` 作为协作式 deadline，所有清理错误最终聚合。Stop-before-Start 不执行从未进入运行期的 module shutdown hook 或 `IHostedService.StopAsync`，但仍执行完整 Stop transaction：运行 ApplicationStop middleware、取消 HostScope 及其 descendants、释放 Build 阶段创建的 module instances，并写入 AUCHOST003；Stopped/Faulted Host 不允许重新启动。

Core 在 Windows 默认禁用 GenericHost 的 EventLog provider 输出，避免普通桌面或 CLI 进程因系统事件日志权限覆盖原始启动异常；Console、Debug、EventSource 和应用显式配置的 provider 不受影响。

Build 失败回滚使用 `ShutdownTimeout` 作为全部异步 cleanup 的共享总 deadline。Generic Host、完整 ModuleRegistry 和模块构造中途的局部回滚不能分别获得一份完整预算；超时作为 cleanup failure 进入 `AUCHOST109` 与最终 `AggregateException`。`DisposeAsync` 不接受取消 token，因此 deadline 只能终止等待，不能强制终止用户清理代码；Core 会观察晚到失败并继续发起剩余独立 cleanup，但明确报告资源可能仍在释放。

## 路线图设计内容

以下内容保留长期设计方向，但不自动构成当前 Core 合同。只有已经分配 Feature ID、登记到 `api-contracts.md` 并由 `testing.md` 验证的行为才属于已实现能力。自定义 `IApplicationLifetime`、通用 Contribution/ContributionLease、Core ErrorPolicy 和插件独立 ServiceProvider 编排当前均为 deferred；其中的“必须”只描述未来能力成立后的目标约束。

## AtomUI.City.Core Hosting 设计

适用范围：`AtomUI.City.Core` 中 Host、Application 构建、GenericHost 集成、启动/停止流程、Host 扩展方法 DSL

### 1. 目标

Hosting 是 AtomUI.City 应用运行时的入口。

当前 Hosting 把 .NET GenericHost、Configuration、DependencyInjection、Logging、Options、ModuleSystem、Lifecycle 和 Diagnostics 串成应用启动模型。Contribution registry、PluginSystem 和 Presentation bridge 通过 Core Host contract 接入，但其领域对象和运行时编排不归 Core 所有。

Hosting 的目标：

- 使用 .NET GenericHost 作为底层容器和基础设施。
- 提供 AtomUI.City 自己的 Application Host API。
- 统一应用构建、启动、停止和释放流程。
- 驱动模块发现、模块图构建和模块生命周期。
- 创建 HostScope 和 ApplicationScope。
- 接入 Lifecycle Middleware。
- 为 Presentation 和 PluginSystem 提供受控 Host contract。
- 保持 Core 不依赖 AtomUI/Avalonia。
- 保持 AOT/trimming/source generator 友好。

### 2. 非目标

Hosting 不负责：

- UI 控件、主题、窗口和 Dispatcher 的具体实现。
- Route 到 ViewModel Target 的选择。
- View/ViewModel 绑定。
- 路由图解释。
- Data 请求管线。
- 权限策略解释。
- 插件程序集加载细节。
- CLI 交互体验。
- Build/source generator 实现。

这些能力由对应模块负责。Hosting 只负责启动边界、生命周期调度和基础设施编排。

### 3. Host 与 GenericHost 的关系

AtomUI.City 复用 .NET GenericHost 的成熟基础设施：

- `Microsoft.Extensions.Hosting`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Configuration`
- `Microsoft.Extensions.Options`
- `Microsoft.Extensions.Logging`

但 GenericHost 不是 AtomUI.City 的公共编程范式本身。

关系：

```text
ApplicationHost
-> GenericHost
-> IServiceProvider
-> IConfiguration
-> ILogger
-> IOptions
```

GenericHost 负责成熟的 .NET 基础设施；当前 AtomUI.City Core Host 负责：

- Application。
- Module。
- LifecycleScope。
- Application ServiceScope。
- Lifecycle middleware。

Plugin、Contribution/Lease 和 Desktop lifetime 是其他模块通过 Host contract 接入的领域语义，不是当前 Core Host 自身持有的公共模型。

允许提供 GenericHost 桥接入口，但不能绕开 AtomUI.City 的模块和生命周期约束。

### 4. 命名规范

类型名不加 `City` 前缀。命名空间已经是 `AtomUI.City.*`。

推荐类型：

| 类型 | 职责 |
|---|---|
| `ApplicationHost` | 应用 Host 静态入口和默认实现。 |
| `ApplicationHostBuilder` | 应用构建器，包装 GenericHost builder 和框架构建上下文。 |
| `ApplicationHostOptions` | Host 级配置，例如环境、关闭超时、启动模块、动态能力策略。 |
| `IApplicationHost` | Host 运行时接口。 |
| `IApplicationHostBuilder` | Host 构建期接口。 |
| `IApplicationContext` | 应用实例描述符接口，也是唯一公开 Context 合同。 |

避免：

```text
CityApplicationBuilder
ICityHost
CityHostOptions
ModuleScope
PluginScope
PluginModuleScope
```

也避免直接命名为 `IHost`、`HostOptions`、`IHostLifetime`，防止和 `Microsoft.Extensions.Hosting` 冲突。

### 5. 核心抽象

#### ApplicationHost

推荐入口：

```csharp
var builder = ApplicationHost.CreateBuilder(args);
```

职责：

- 创建默认 `ApplicationHostBuilder`。
- 配置 GenericHost defaults。
- 注入 AtomUI.City Core 基础服务。
- 提供 `Build()` / `RunAsync()` 默认路径。

#### ApplicationHostBuilder

构建期对象。

职责：

- 持有 GenericHost builder。
- 持有 AtomUI.City 构建期上下文。
- 收集启动模块。
- 按调用顺序收集用户服务配置动作，并在所有模块服务阶段完成后执行。
- 收集生命周期中间件。
- 收集 framework feature descriptor。
- 输出 `IApplicationHost`。

Builder 不公开可立即修改的 `IServiceCollection`。应用和扩展方法必须通过 `ConfigureServices(Action<IServiceCollection>)` 登记最终服务配置；模块只能通过 `ServiceConfigurationContext.Services` 在自己的三个服务阶段内注册。

Builder 的 `Configuration` 只用于 Build 前组合配置源、读取构建期配置和配置 reload provider。运行时业务服务必须通过 DI 获取 `IConfiguration`、`IOptions<T>` 或 `IOptionsMonitor<T>`。Builder 不得注册到运行时 DI，也不得由业务服务长期持有；为该边界增加 Analyzer 属于后续 deferred item，不进入当前 Core 合同。

#### IApplicationHost

运行期对象。

建议能力：

```csharp
Task StartAsync(CancellationToken cancellationToken = default);
Task StopAsync(CancellationToken waitCancellationToken = default);
Task RunAsync(CancellationToken cancellationToken = default);
IServiceProvider Services { get; }
IApplicationContext Context { get; }
LifecycleScope HostScope { get; }
LifecycleScope? ApplicationScope { get; }
```

`HostScope` 由 `Build()` 创建并且始终非空，是整个 Host 生命周期树的根；调用方可以读取或创建符合所有权规则的 child，但不能代替 Host 终止它。

`ApplicationScope` 只有在 `StartAsync()` 的运行期启动事务创建成功后才非空。在 Created、Stop-before-Start，以及启动尚未创建该 Scope 的阶段，它必须为 `null`。Scope 一旦创建，停止或释放后属性可以继续返回同一实例，以便诊断其最终 `State`；因此调用方不得把“非 null”解释成“应用仍在运行”，也不得缓存该 Scope 绕过 Host 生命周期。Module initialization context 只会收到已经创建且处于 Running 的非空实例。

#### ApplicationContext

`IApplicationContext` 是 Host 在 Build 阶段一次性创建的应用实例描述符：

```csharp
public interface IApplicationContext
{
    string ApplicationId { get; }
    Guid ApplicationInstanceId { get; }
    string ApplicationName { get; }
    string ApplicationVersion { get; }
    string EnvironmentName { get; }
    string ContentRootPath { get; }
    string AppDataPath { get; }
    IReadOnlyList<string> StartupArguments { get; }
}
```

合同：

- 所有字段在 Build 返回前完成计算，之后不可修改。
- `ApplicationId` 是稳定、非本地化的产品标识，必须显式配置。
- `ApplicationInstanceId` 每次 Build 唯一，用于区分同一产品的并发运行实例。
- `ApplicationName` 是稳定、非本地化的可读名称，同时作为当前默认应用数据目录名；必须是单个有效目录段。
- `ApplicationVersion` 优先使用显式配置，其次使用入口程序集 informational version，再回退 assembly version。
- `ContentRootPath` 和 `AppDataPath` 必须是规范化绝对路径。当前 `AppDataPath` 为 `LocalApplicationData/ApplicationName`，Context 不负责创建目录。
- `StartupArguments` 是调用方参数的只读防御性副本；默认诊断不得记录原始参数值。
- Context 作为 `IApplicationContext` Singleton 注册；具体实现不是 public API。
- Host Dispose 后仍允许读取 Context。

Context 不持有 `IConfiguration`、`IServiceProvider`、DI Scope、LifecycleScope、diagnostics、Host state 或任意 `Properties` 动态属性袋。这些运行时能力必须由 `IApplicationHost` 或专门的 DI 服务提供。

#### 跨平台应用目录（Deferred）

当前只提供兼容字段 `AppDataPath`，默认使用 .NET `Environment.SpecialFolder.LocalApplicationData` 与 `ApplicationName` 拼接。本轮不增加 Roaming 路径，也不在 Context 构建时创建目录。

生产级跨平台路径模型留作后续专题：按 data、config、cache、logs 等用途提供语义化路径服务，分别研究 Windows Local/Roaming、Linux XDG Base Directory 和 macOS Library 目录约定，同时明确目录创建、权限、迁移和兼容策略。该专题尚未冻结公开类型名称。

#### 桌面 Application Lifetime 边界

当前 Core 不定义 `IApplicationLifetime`。`IApplicationHost.RunAsync` 使用 Microsoft `IHostApplicationLifetime.ApplicationStopping` 等待 Generic Host 停止信号；Presentation 通过 City Host 的 Start/Stop 和该标准 lifetime 协调 Avalonia，不向 Core 引入 Avalonia 类型。

如果未来需要统一 UI ready、挂起和恢复语义，必须先分配独立 Feature ID，并补齐 Core contract、Presentation adapter、取消语义和 Headless 测试，不能把候选名称 `IApplicationLifetime` 当作现有 API。

### 6. 扩展方法 DSL

AtomUI.City Application 构建使用 .NET 扩展方法风格。

分类：

| 前缀 | 用途 | 顺序语义 |
|---|---|---|
| `Add*` | 注册服务、能力描述、descriptor。 | 通常无顺序语义。 |
| `Use*` | 加入生命周期管线、中间件、模块。 | 有顺序语义。 |
| `Configure*` | 配置 Options、Builder、策略。 | 后者可覆盖。 |
| `Map*` | 映射路由、View、资源、命令入口。 | 需要冲突检测。 |
| `With*` | 给定义对象附加元数据。 | 链式配置。 |
| `Enable*` / `Disable*` | 开关能力。 | 最终配置生效。 |

示例：

```csharp
var builder = ApplicationHost.CreateBuilder(args);

builder
    .UseModule<AppModule>()
    .ConfigureHost(options =>
    {
        options.ApplicationId = "Company.Product";
        options.ApplicationName = "Product";
    })
    .ConfigureLifecycle(lifecycle => { })
    .ConfigureServices(services => { });

await builder.Build().RunAsync();
```

规则：

- 扩展方法默认只收集配置、descriptor、服务注册或中间件。
- `ConfigureServices` 只登记 delegate；delegate 在 Build 的 `UserServices` 阶段按登记顺序执行。
- 扩展方法不得执行真实启动逻辑。
- 扩展方法不得调用 `BuildServiceProvider()`。
- 扩展方法不得启动线程、加载插件、创建 ViewModel 或触发导航。
- `Use*` 必须保留调用顺序。
- `Add*` 应尽量幂等。
- `Map*` 必须冲突检测。
- `Configure*` 使用 Options 模式。
- 所有扩展方法返回原 builder 或更具体的 feature builder。

### 7. Application / Module / Plugin 组成模型

Host 管理 Application 的组成：

```text
Application
  Modules
    AppModule
    RoutingModule
    SecurityModule

  Plugins
    SalesPlugin
      Modules
        SalesModule
        SalesReportModule
```

Module 和 Plugin 是能力贡献方，不是 Scope。

Module 可以贡献：

- Service registration。
- Configuration。
- Route。
- Permission。
- Localization resource。
- Event handler。
- Data client。
- Presentation resource。
- Plugin extension point。

Plugin 可以携带自己的 Modules，这些插件模块也通过 Contribution 向 Host 贡献能力。

### 8. Contribution 与 ContributionLease（Deferred）

当前 Core 只调度 `ConfigureContributions` 模块钩子；`ContributionConfigurationContext` 仅提供应用描述和 Application ServiceScope provider，不提供通用 registry、request 或 lease API。Routing、Presentation、Localization 等模块拥有各自领域 registry/lease，PluginSystem 负责插件卸载时的跨领域编排。

以下模型是未来统一贡献合同的候选设计，不属于 `AUC-CORE-001` 到 `AUC-CORE-008`：

```text
Module or Plugin Module
-> Contribution
-> Host Registry
-> ContributionLease
```

例如：

```text
RouteContribution("/sales")
  Module = SalesModule
  Plugin = SalesPlugin
  Lease = RouteContributionLease("/sales")
```

如果未来由 Core 定义通用 ContributionLease，Host 才负责持有它并用于停用、卸载、关闭和诊断。

ContributionLease 需要支持：

- 可撤销。
- 可诊断。
- 可追踪 Module / Plugin。
- 按反向顺序撤销。
- 撤销失败汇总。

### 9. Lifecycle Scope 模型

Scope 只表示运行实例的生命周期边界。

```text
HostScope
  -> ApplicationScope
    -> PresentationScope
      -> WindowScope
        -> NavigationScope
          -> RouteScope
            -> ActivationScope
              -> StateScope
              -> OperationScope
              -> SubscriptionScope
```

这里没有 `ModuleScope`、`PluginScope`。

原因：

- Module / Plugin 是组成和贡献方。
- RouteScope / OperationScope 是运行实例。
- 二者通过 Contribution 关联，而不是通过父子 Scope 关联。

例如插件路由：

```text
RouteScope("/sales")
  Parent = NavigationScope
  Contribution = RouteContribution("/sales")
  Contribution.Module = SalesModule
  Contribution.Plugin = SalesPlugin
  Services = SalesPlugin ServiceScope
```

插件卸载时：

```text
SalesPlugin stopping
-> stop new entries from SalesPlugin contributions
-> find active RouteScope where Contribution.Plugin == SalesPlugin
-> deactivate routes
-> cancel operations
-> dispose activation scopes
-> revoke contribution leases
-> dispose plugin ServiceScope
-> unload plugin assemblies
```

### 10. Host 构建流程

Build 前完成服务注册和静态模块图准备。

```text
ApplicationHost.CreateBuilder(args)
-> create GenericHost builder
-> load configuration
-> configure logging
-> validate application identity, version and paths
-> create immutable IApplicationContext descriptor
-> register Core infrastructure
-> collect startup modules
-> load generated registrar from GeneratedModuleManifestAttribute
-> build ModuleCatalog
-> build module graph
-> run module PreConfigureServices
-> run module ConfigureServices
-> run module PostConfigureServices
-> apply user ConfigureServices
-> build GenericHost
-> create HostScope
-> create ApplicationHost
```

原则：

- Static Module 的服务注册必须发生在 GenericHost Build 之前。
- 用户 `ConfigureServices` 在所有 Static Module 服务阶段成功后执行，并拥有 Root DI 的最终应用级配置权。
- 模块服务阶段失败时不执行用户 `ConfigureServices`，避免产生部分应用配置副作用。
- Build 后不允许普通 Module 或 Plugin 修改 Root ServiceProvider。
- 插件独立 ServiceProvider、ServiceScope 和动态贡献 registry 由 PluginSystem 与能力模块定义，不属于当前 Core Host 实现。

### 11. Host 启动流程

Build 后进入运行阶段。

```text
StartAsync
-> Run HostStarting middleware
-> Create ApplicationScope
-> Initialize modules
-> Start modules
-> Apply static contributions
-> Run ApplicationStarting middleware
-> Wait Presentation lifetime ready
-> Create PresentationScope
-> Navigate initial route
-> Run ApplicationStarted middleware
-> Enter Running
```

注意两段式：

```text
Service registration phase: Build 前
Runtime initialization phase: Build 后
```

这点必须硬性规定，否则模块系统、DI 和插件系统都会混乱。

### 12. Host 停止流程

停止必须以尽可能释放为原则。

```text
Stop requested
-> reject new operations
-> Run ApplicationStopping middleware
-> stop new route activation
-> deactivate active routes
-> cancel running operations
-> deactivate plugins
-> optionally unload plugins
-> stop modules in reverse order
-> revoke remaining contribution leases
-> dispose PresentationScope
-> dispose ApplicationScope
-> stop GenericHost
-> dispose HostScope
-> Run ApplicationStopped middleware
```

规则：

- Stop 必须幂等。
- Stop 支持超时。
- Cancellation 不是 error。
- 多个 dispose 错误要汇总。
- 不能因为一个插件卸载失败阻断整个 Host 关闭。
- 插件卸载失败进入 `UnloadPending`。

### 13. ModuleSystem 集成

Hosting 驱动模块系统，但不解释模块贡献内容。

边界：

```text
Hosting
-> collect startup modules
-> build module graph
-> run service configuration stages
-> build GenericHost
-> run module initialization/start/stop stages
```

Hosting 不负责解释：

- 路由贡献。
- 权限贡献。
- 本地化贡献。
- Presentation 资源。
- EventBus handler。
- Data client。

当前由各能力模块解释自己的贡献并决定是否返回领域 lease；Core 不把它们转换为通用 ContributionLease。统一转换和跨领域撤销属于后续独立 Feature。

### 14. Configuration 集成

Hosting 负责建立配置根对象。

配置来源建议：

```text
Default framework settings
-> appsettings.json
-> appsettings.{Environment}.json
-> environment variables
-> command line arguments
-> app local settings
-> user settings
```

实际顺序在 `configuration.md` 中细化。

Hosting 只定义配置入口：

```csharp
builder.ConfigureConfiguration(configuration => { });
builder.ConfigureOptions<ApplicationHostOptions>(options => { });
```

### 15. DI 集成

Hosting 负责：

- 创建服务注册阶段。
- 调用模块服务注册。
- Build Root ServiceProvider。
- 创建 HostScope 和 ApplicationScope。

DI 细节放到 `dependency-injection.md`。

Hosting 必须明确：

- 不在 Build 后修改 Root ServiceProvider。
- 不在扩展方法中调用 `BuildServiceProvider()`。
- 插件不写 Root ServiceProvider。
- Scope Tree 和 DI scope 需要明确绑定。

### 16. Presentation 集成边界

Core Hosting 不依赖 AtomUI/Avalonia。

Presentation 负责提供扩展：

```csharp
builder.UseAtomUIPresentation(...);
```

Presentation 扩展负责：

- 注册 Avalonia/AtomUI 集成服务。
- 通过 City Host Start/Stop 与 Microsoft `IHostApplicationLifetime` 协调 Avalonia lifetime。
- 提供 UI Dispatcher 实现。
- 创建 PresentationScope。
- 创建 WindowScope。
- 创建 NavigationScope。
- 提供 initial route 启动桥接。
- 提供 View/ViewModel activation 接入。

Hosting 的 `RunAsync` 只等待 Microsoft `IHostApplicationLifetime.ApplicationStopping`，不直接操作 Avalonia 类型。

### 17. PluginSystem 集成边界

Host 是插件运行的协调者，但插件加载细节属于 PluginSystem。

Core Host 当前提供：

- Host contract。
- Lifecycle pipeline。
- Diagnostics。
- Root ServiceProvider 冻结和应用停止边界。

PluginSystem 负责：

- 插件发现。
- 元数据验证。
- 依赖解析。
- `AssemblyLoadContext`。
- Plugin ServiceProvider / ServiceScope。
- 插件模块图。
- 各领域 ContributionRequest/Lease 的协调与撤销。
- Stop/unload 调度。
- Unload diagnostics。

Plugin 不能：

- 修改 Root ServiceProvider。
- 绕过 Host Registry。
- 绕过 Security/Data pipeline。
- 保存 Host、Scope、ServiceProvider、ViewModel 到静态字段。

### 18. AOT / Source Generator 策略

Hosting 必须 AOT-first。

默认路径：

```text
Explicit registration
GeneratedModuleManifestAttribute
Generated registrar
ModuleCatalog and strongly typed descriptor
```

不默认：

```text
Assembly scanning
Naming convention reflection
Dynamic proxy
Expression tree compilation
```

推荐：

```csharp
builder.UseModule<AppModule>();
```

不推荐默认启用：

```csharp
builder.ScanAllAssemblies();
```

Generated Module Catalog 不属于动态发现。当前冻结合同不提供 `EnableDynamicDiscovery()` 或 `ApplicationHostOptions.AllowDynamicDiscovery`；运行时扫描必须在独立 Feature 中完成 opt-in、Analyzer warning、AOT/trimming 诊断和 Strict mode 拒绝策略后才能公开。

### 19. 错误策略

默认策略：

| 阶段 | 策略 |
|---|---|
| Builder 创建失败 | Fatal。 |
| Configuration 加载失败 | 默认 Fatal，可配置 optional。 |
| Core service 注册失败 | Fatal。 |
| Module graph 构建失败 | Fatal。 |
| Module service registration 失败 | Fatal。 |
| Module initialization 失败 | 默认 Fatal，可配置降级。 |
| Presentation lifetime 启动失败 | Fatal。 |
| Plugin 加载失败 | Non-fatal。 |
| Plugin 卸载失败 | 标记 `UnloadPending`。 |
| Stop / Dispose 失败 | 汇总错误，继续释放。 |

### 20. 诊断要求

Hosting 必须记录：

- 应用 id、实例 id、应用名、版本和环境。
- 启动参数是否存在或参数数量；默认不得记录原始参数值。
- 配置来源。
- 启动模块列表。
- generated registrar/catalog 使用情况。
- 模块图。
- GenericHost build 耗时。
- HostScope / ApplicationScope / PresentationScope 创建释放。
- 通用 ContributionLease 创建和撤销、插件停用/卸载状态属于对应 deferred Feature 或 PluginSystem 的诊断责任，不计入当前 Core Host 诊断合同。
- Lifecycle middleware 执行顺序。
- Startup / Stop 各阶段耗时。
- Fatal / non-fatal 错误。

### 21. 测试要求

Testing 包后续要支持：

- `TestApplicationHost`。
- 无真实 Presentation 启动。
- 注入测试配置。
- 注入测试模块。
- 手动 Start/Stop。
- 断言模块顺序。
- 断言 Scope 创建和释放顺序。
- 断言 generated module registrar 不扫描模块类型，动态发现公开 API 不得以 no-op 形式存在。
- 断言 Stop 幂等。
- 断言 Dispose 错误汇总。

通用 ContributionLease 和插件 Contribution/Scope 反查测试必须在对应 Feature 分配并实现后加入，当前不作为 Core `Verified` 证据。

### 22. 开发者约束

应用开发者应遵守：

- 通过 `ApplicationHost.CreateBuilder(args)` 创建应用。
- 通过 `[ApplicationModule]` 声明 generated default root，或通过 `UseModule<TModule>()` 增加显式启动根。
- 不直接绕过 AtomUI.City Host 修改运行时流程。
- 不在扩展方法中执行真实运行时逻辑。
- 不依赖默认程序集扫描。
- 不在模块构造函数中启动任务或订阅事件。
- 不在 Build 后修改 Root ServiceProvider。
- 不把 Builder 注入运行时 DI，也不在业务服务中持有 Builder。
- Presentation、Plugin、Routing 等能力通过对应扩展点接入。
