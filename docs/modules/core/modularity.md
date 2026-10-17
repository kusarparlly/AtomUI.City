# AtomUI.City.Core Modularity 合同

## 适用范围

本专题属于 `AtomUI.City.Core` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Modularity` 相关实现决策，不重新定义模块边界。

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
| AUC-CORE-002 | Lifecycle Pipeline | LifecycleMiddlewarePipelineTests; ApplicationHostLifecycleIntegrationTests |
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

## 路线图设计内容

以下内容同时记录已落地模块合同和长期设计方向。只有 `AUC-CORE-004`、`AUC-CORE-008`、`api-contracts.md` 与测试矩阵共同覆盖的行为属于当前合同。通用 Contribution/ContributionLease、插件服务隔离和插件卸载回滚尚未分配 Core Feature ID，相关段落均为 deferred 路线图。

## AtomUI.City.Core Modularity 设计

适用范围：`AtomUI.City.Core` 中模块定义、依赖声明、模块图、模块生命周期、模块贡献、插件模块接入、AOT/source generator 约束。

### 1. 定位

Module 是 AtomUI.City 应用能力组织的基本单位。

Module 用于声明一组框架能力，包括服务注册、配置、路由、权限、本地化、事件处理、数据客户端、Presentation 资源、命令、诊断提供者和插件扩展点。

Module 不是生命周期 Scope，不是 DI Scope，也不是 Plugin。Module 是应用组成单元和能力贡献方，运行时实例生命周期由 Lifecycle Scope 管理。当前 Core 只提供 `ConfigureContributions` 生命周期钩子，不定义通用 Contribution 或 ContributionLease。

```text
Application
  Modules
  Plugins
    Plugin
      Modules
```

### 2. 设计目标

- 提供统一模块编程模型。
- 支持模块依赖声明和确定性启动顺序。
- 支持编译期生成模块清单和依赖图输入。
- 支持启动期模块和插件模块共用同一套模块接口。
- 支持模块服务注册、配置、初始化、启动、关闭。
- 为未来按 Feature 引入可撤销 Contribution 保留模块钩子。
- 保持 AOT/trimming/source generator 友好。
- 保持 Core 不依赖 UI、MVVM、Routing、PluginSystem 的具体实现。

### 3. 非目标

Modularity 不负责：

- UI 控件、窗口、View/ViewModel 绑定。
- 路由匹配和导航执行。
- 权限策略判定。
- 数据请求管线。
- 状态管理。
- 插件程序集加载。
- ViewModel Activation。
- 业务模块分层规范。

这些由对应模块实现。Modularity 只提供模块契约、模块图和生命周期调度。

### 4. 核心概念

| 概念 | 职责 |
|---|---|
| `Module` | 模块基类，提供模块生命周期方法。 |
| `ModuleAttribute` | 可选模块元数据。未指定 id 时使用模块类型全名。 |
| `DependsOnAttribute` | 声明模块依赖，由 source generator 读取。 |
| `ModuleDescriptor` | 模块不可变描述信息；构造时复制依赖集合并拒绝集合内部的 null。 |
| `GeneratedModuleManifestAttribute` / `IModuleRegistrar` | Generator 发出的程序集入口和强类型模块登记合同。 |
| `ModuleGraph` | 解析后的模块依赖图。 |
| `ModuleCatalog` | 当前 Host 可见模块集合。 |
| `IModuleRegistry` | 对业务代码公开的已加载模块元数据只读视图，仅包含 `Modules`。 |
| `IModuleLifecycleController` / `ModuleRegistry` | Core internal 的 Host 生命周期控制面与状态机，不进入 Root DI。 |
| `ModuleLifecycleContext` | 模块生命周期执行上下文。 |

### 5. Module 基类

模块统一继承 `Module`。插件模块也继承同一个 `Module`，不设计单独的 `PluginModule` 公共基类。

```csharp
public abstract class Module
{
    public virtual void PreConfigureServices(ServiceConfigurationContext context) { }

    public virtual void ConfigureServices(ServiceConfigurationContext context) { }

    public virtual void PostConfigureServices(ServiceConfigurationContext context) { }

    public virtual ValueTask ConfigureContributionsAsync(
        ContributionConfigurationContext context,
        CancellationToken cancellationToken)
    {
        ConfigureContributions(context);
        return ValueTask.CompletedTask;
    }

    public virtual void ConfigureContributions(ContributionConfigurationContext context) { }

    public virtual ValueTask OnPreApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        OnPreApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnPreApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        OnApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnPostApplicationInitializationAsync(
        ApplicationInitializationContext context,
        CancellationToken cancellationToken)
    {
        OnPostApplicationInitialization(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnPostApplicationInitialization(ApplicationInitializationContext context) { }

    public virtual ValueTask OnApplicationShutdownAsync(
        ApplicationShutdownContext context,
        CancellationToken cancellationToken)
    {
        OnApplicationShutdown(context);
        return ValueTask.CompletedTask;
    }

    public virtual void OnApplicationShutdown(ApplicationShutdownContext context) { }
}
```

运行时只调用 async 方法。同步方法只是开发便利入口。

`ApplicationInitializationContext` 由 Host 在 `ApplicationScope` 创建完成后构造，并且必须携带该次运行期唯一、非空的 `ApplicationScope`。Module 可以使用它为运行时资源创建子 Scope 或建立 owner 绑定，但不得主动停止或释放 Host 拥有的 `ApplicationScope`。具体功能模块只能消费这一 Core 生命周期能力，Core 不得引用或识别 EventBus、Router、State 等上层模块类型。

`ApplicationShutdownContext` 不重复携带 `ApplicationScope`。Module 在初始化阶段建立的资源所有权必须由自身 internal lifecycle controller 保存，并在 shutdown hook 中加入同一终止事务；不得依赖关闭阶段重新注入 owner，也不得建立第二套清理事务。

### 6. 模块标识

模块 id 默认使用模块类型全名。

```csharp
namespace AtomUI.City.Routing;

public sealed partial class RoutingModule : Module
{
}
```

默认模块 id：

```text
AtomUI.City.Routing.RoutingModule
```

`ModuleAttribute` 是可选的，只用于覆盖 id 或补充元数据。

```csharp
[Module(Version = "1.0.0", Description = "Routing module")]
public sealed partial class RoutingModule : Module
{
}
```

显式 id 只在需要稳定公开 id、跨版本兼容、插件发布或清单对外暴露时使用。

### 7. 模块依赖

模块依赖通过 Attribute 声明。

```csharp
[DependsOn(typeof(RoutingModule))]
[DependsOn(typeof(SecurityModule))]
public sealed partial class AppModule : Module
{
}
```

可选依赖：

```csharp
[DependsOn(typeof(LocalizationModule), Optional = true)]
public sealed partial class PresentationModule : Module
{
}
```

依赖声明使用 `typeof(TModule)`，不使用字符串。字符串 id 只用于模块标识，不作为依赖引用的主路径。

### 8. Source Generator 模块图

模块依赖图由 source generator 在编译期建立输入。

Generator 识别全部可生成模块并形成 `ModuleCatalog`，但运行时只激活启动根的依赖闭包。`ModuleCatalog` 表示可用模块，`ModuleRegistry` 表示当前 Host 实际创建并运行的模块，两者不得混用。

应用可通过两种方式声明启动根：

```csharp
[ApplicationModule]
[DependsOn(typeof(EditorModule))]
public sealed class AppModule : ModuleBase
{
}

// 可选：显式增加根模块；与 generated default root 重复时去重。
builder.UseModule<AppModule>();
```

没有显式 `UseModule<TModule>()` 时，Host 使用 generated default root。每个应用程序集最多存在一个 `[ApplicationModule]`；测试或特殊 Host 可以通过多个 `UseModule<TModule>()` 组合多个显式根。

编译期流程：

```text
Find Module-derived types
-> Read ModuleAttribute
-> Read DependsOnAttribute
-> Read ApplicationModuleAttribute
-> Generate ModuleDescriptor
-> Generate IModuleRegistrar implementation
-> Emit GeneratedModuleManifestAttribute
-> Generate module dependency graph input
-> Emit diagnostics
```

运行时流程：

```text
ApplicationHost
-> Load registrar from GeneratedModuleManifestAttribute
-> Build ModuleCatalog
-> Merge generated default root and explicit UseModule roots
-> Resolve startup dependency closure
-> ModuleGraphValidator validates duplicate ids, missing dependencies and cycles
-> Produce immutable topologically ordered ValidatedModuleGraph
-> ModuleRegistry accepts only ValidatedModuleGraph
-> Run strong-typed module factories in topological order
-> Run module lifecycle
```

模块图验证和模块实例化是两个不可交换的 Build 阶段。`ModuleGraphValidator` 只读取 descriptor/registration 元数据，不调用用户 factory；检测到直接或间接循环时，`Build()` 必须立即失败且图中所有 module constructor/factory 调用次数为零。`ValidatedModuleGraph` 是 Core 内部验证凭证，应用开发者不可见；`ModuleRegistry` 不提供接收原始 registration 列表的创建入口。

Generator 对当前编译可见的循环使用 `AUCGEN003` 提前拒绝；Host Build 验证仍是跨程序集 registrar、显式 `UseModule<TModule>()` 和最终 root 合并结果的权威兜底。两层使用相同的有向图语义，合法菱形依赖不得误判为循环。

默认不允许运行时扫描程序集寻找模块。当前冻结合同不提供 dynamic discovery；未来只能作为 opt-in fallback，并必须在独立 Feature 中同时提供扫描边界、Analyzer 和 AOT/trimming 诊断。

### 9. 模块类型

第一版区分两种来源：

| 来源 | 说明 |
|---|---|
| Application | 随应用启动加载的静态模块。 |
| Plugin | 插件携带的普通模块。 |

插件模块不特殊化 API。它通过 descriptor 表达来源：

```text
ModuleDescriptor.Origin = Plugin
ModuleDescriptor.Plugin = PluginDescriptor
```

`ModuleOrigin.Plugin` 和 plugin id 只表达 descriptor 来源。插件隔离、领域 ContributionLease 和卸载规则由 PluginSystem 与对应能力模块实现，不属于当前 Core ModuleRegistry 的 `Verified` 行为。

### 10. 生命周期阶段

模块启动阶段：

```text
Discovered
-> GraphResolved
-> PreConfigureServices
-> ConfigureServices
-> PostConfigureServices
-> ConfigureContributions
-> OnPreApplicationInitialization
-> OnApplicationInitialization
-> OnPostApplicationInitialization
-> Running
```

模块关闭阶段：

```text
OnApplicationShutdown
-> Dispose module resources
-> Stopped
```

启动顺序按拓扑排序执行：依赖先启动，被依赖方后启动。关闭顺序反向执行。

`ModuleRegistry` 使用单向生命周期状态机管理同一批模块实例：

```text
Created
-> ConfiguringServices -> ServicesConfigured
-> ConfiguringContributions -> ContributionsConfigured
-> Initializing -> Initialized
-> Terminating -> Disposed

任一正向阶段失败 -> Faulted -> Terminating -> Disposed
```

每个正向阶段只发布一个内部 transaction Task；同步 `ConfigureServices` 执行期间的外部并发调用快速失败，完成后的重复调用观察第一次结果，异步阶段的并发调用者共享 Task。阶段必须按上图顺序进入，部分执行后失败的阶段不可重试，避免重复服务、贡献、订阅或初始化副作用。

`ShutdownAsync` 与 `DisposeAsync` 共享唯一 terminal transaction。第一个成功发布的终止操作决定路径：Shutdown-first 逆序运行所有已进入运行期模块的 shutdown hook，再逆序释放模块；Dispose-first 只逆序释放，后续 Shutdown 不得在已开始释放的模块上补跑 hook。终止操作若与正向阶段竞争，必须等待已发布阶段退出后才开始清理。

生命周期控制权不属于 module 或业务服务。Builder 以 internal `IModuleLifecycleController` 将真实 Registry 直接交给 Host；Root DI 只注册一个与 controller 分离的 `IModuleRegistry` 只读 view。应用仍可查询当前模块 descriptor，但不能调用 Configure、Initialize、Shutdown，也不能把该 view 强转为 disposal 接口提前释放模块。该能力隔离与状态机互补：前者阻止无权调用者抢占事务，后者保证 Core 内部合法调用的顺序、并发共享与幂等性。

### 11. 阶段语义

| 阶段 | 职责 |
|---|---|
| `PreConfigureServices` | 注册早期配置、Options 默认值、能力开关。 |
| `ConfigureServices` | 注册服务描述。禁止构建 ServiceProvider。 |
| `PostConfigureServices` | 对服务注册和 Options 做后置调整。 |
| `ConfigureContributions` | 声明路由、权限、本地化、命令、事件处理等贡献。 |
| `OnPreApplicationInitialization` | ServiceProvider 可用后的早期初始化。 |
| `OnApplicationInitialization` | 模块主初始化。 |
| `OnPostApplicationInitialization` | 所有模块主初始化之后的后置初始化。 |
| `OnApplicationShutdown` | 应用关闭或插件停用时的模块关闭。 |

`PreConfigureServices`、`ConfigureServices`、`PostConfigureServices` 发生在 ServiceProvider 构建前。

三个服务配置 hook 是严格同步的，只允许声明 DI、Options 和其他确定性元数据，不得执行网络、磁盘、数据库、进程、UI dispatch 或其他异步初始化。需要等待的资源加载和业务初始化必须放入 `OnPreApplicationInitializationAsync`、`OnApplicationInitializationAsync` 或 `OnPostApplicationInitializationAsync`，由 `IApplicationHost.StartAsync` 执行。
`ConfigureContributions` 发生在 ServiceProvider 可用后。当前 context 只暴露 `IApplicationContext` 和 Application ServiceScope provider；Core 不统一校验领域贡献，也不返回通用 ContributionLease。

### 12. DI 规则

启动期模块可以注册 Root `IServiceCollection`，但只能发生在 ServiceProvider 构建前。

插件模块不能修改 Host Root ServiceProvider。插件服务上下文、隔离 Provider 和受控 Host contract 由 PluginSystem 负责；Core 当前只提供该不可修改约束和模块基础合同。

模块规则：

- 不允许在服务配置阶段调用 `BuildServiceProvider()`。
- 不允许在模块构造函数中解析服务。
- 不允许把模块实例作为全局状态使用。
- 服务覆盖必须可诊断。
- 插件服务不能泄漏为 Host 长期持有实例。

### 13. Contribution 规则（Deferred）

当前不存在 Core 通用 Contribution Request、Registry 或 Lease。以下流程是未来统一贡献模型的候选设计；在独立 Feature 落地前，Module 只能调用各能力模块公开的领域注册 API，并遵守其撤销合同。

```text
Module
-> Contribution Request
-> Host validation
-> Target Registry
-> ContributionLease
```

模块可贡献：

- Routes。
- Permissions。
- Localization resources。
- EventBus handlers。
- Commands / Actions。
- Data clients。
- Presentation resources。
- Diagnostics providers。
- Plugin extension points。

插件模块的运行时贡献必须遵守对应能力模块的可撤销合同；跨领域反向撤销顺序由 PluginSystem 编排，不由当前 Core Host 持有通用 Lease。

### 14. 错误策略

启动期 required 模块失败：Host 启动失败，并输出模块 id、类型、阶段、依赖图和诊断上下文。

optional 模块失败：默认禁用该模块并记录 warning，是否继续由 Host policy 决定。

插件模块激活失败后的领域 Lease 回滚和插件服务上下文释放属于 PluginSystem 的失败合同，不属于当前 Core ModuleRegistry。

关闭失败：进入错误处理管线，继续尝试关闭后续模块并聚合诊断。

### 15. AOT 约束

Modularity 默认 AOT-first。

要求：

- 模块发现依赖 source generator 生成 registrar/catalog metadata。
- 模块依赖图由编译期生成输入。
- 模块工厂使用强类型生成代码。
- Analyzer 检测重复 id、循环依赖、缺失依赖和动态扫描。
- Runtime 不依赖反射扫描作为默认路径。

禁止默认行为：

- 启动时扫描所有程序集找模块。
- 通过反射读取模块元数据作为主路径。
- 动态代理生成模块实例。
- Generator 执行用户代码。

### 16. 测试策略

Testing 包应支持：

- 构造 ModuleDescriptor。
- 构造 ModuleGraph。
- 断言拓扑排序。
- 模拟重复模块 id。
- 模拟缺失依赖和循环依赖。
- 驱动模块生命周期。
- 断言服务配置阶段没有构建 ServiceProvider。
- 通用 ContributionLease 创建/反向撤销和插件模块回滚测试在对应 Feature 或 PluginSystem 中落地，不作为当前 Core Module Contract 的完成证据。
