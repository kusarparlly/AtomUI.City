# AtomUI.City.Build Analyzers 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Analyzers` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Build` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-BUILD-001 | Output Layout | OutputLayoutTests |
| AUC-BUILD-002 | Package Metadata | PackageMetadataTests |
| AUC-BUILD-003 | Project Inventory | ProjectInventoryTests |
| AUC-BUILD-004 | Dependency Boundary | ProjectDependencyBoundaryTests |
| AUC-BUILD-005 | Source Generator Packaging | SourceGeneratorProjectStructureTests |
| AUC-BUILD-006 | Release Gates | PackagingReleaseGateTests; EngineeringGateTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时包不依赖 Build 生产程序集` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Build Analyzer 设计

适用范围：构建期 analyzer 规则、诊断 ID、AOT/trimming、插件、架构和测试矩阵诊断

### 1. 目标

Analyzer 用于在构建期发现架构、AOT、插件、manifest 和测试门禁问题，避免问题延迟到运行时。

### 2. 诊断 ID

Build/Generator 诊断建议使用：

```text
AUCBLD0001
AUCGEN0001
AUCANL0001
```

分类：

| 前缀 | 用途 |
|---|---|
| `AUCBLD` | MSBuild task 和 package/publish 诊断。 |
| `AUCGEN` | Source generator 诊断。 |
| `AUCANL` | Analyzer 诊断。 |
| `AUCPLG` | Plugin package 诊断，和 PluginSystem 文档保持一致。 |

当前稳定 Analyzer 诊断：

| ID | Severity | Contract |
|---|---|---|
| `AUCANL0001` | Error | 非测试 City 项目不得调用或引用 City/微软的 `BuildServiceProvider`、`IServiceProviderFactory<T>.CreateServiceProvider` 或 Microsoft Generic Host 构建/启动入口；Root Provider 必须由 `ApplicationHost` 构建。 |

### 3. 诊断级别

| 级别 | 用途 |
|---|---|
| Error | 破坏构建产物、manifest、AOT 或运行时必需能力。 |
| Warning | 影响兼容性、性能、AOT/trimming 或推荐规范。 |
| Info | 优化建议或迁移提示。 |

### 4. 第一版规则

建议 analyzer 覆盖：

- 使用运行时程序集扫描但未显式 opt-in。
- `Strict` 模式下使用 dynamic discovery。
- Route 定义冲突。
- Permission id 冲突。
- View/ViewModel 映射不稳定。
- 插件私有类型泄漏到 Host contract。
- 插件缺少 `PluginId`。
- 插件包多主程序集。
- Contribution 不可撤销。
- EventBus 跨插件事件不在共享 contract。
- Source generator 无法识别需要生成的声明。
- 测试矩阵缺失。
- 生产 City 项目显式调用或引用 `BuildServiceProvider`、主动调用 `IServiceProviderFactory<T>.CreateServiceProvider`，或者调用/引用 `HostApplicationBuilder.Build()`、`IHostBuilder.Build()` 及其实现、`IHostBuilder.Start/StartAsync/RunConsoleAsync`。

`AUCANL0001` 对 `IsTestProject=true` 和 generated code 不生效；服务注册、构造函数注入、`GetRequiredService`、`CreateScope`、仅配置 `IServiceProviderFactory<T>` 或仅创建/配置 Microsoft Generic Host builder 不产生诊断。City `ApplicationHost.CreateBuilder().Build()` 是唯一允许的应用 Root Provider 构建入口。Analyzer 检查源码中的语义调用和方法引用，不把可信进程内代码当作安全沙箱，也不分析第三方已编译程序集内部实现。

### 5. AOT 和 trimming

必须诊断：

- 未声明反射。
- 动态代理。
- 表达式树编译路径。
- Native AOT 模式动态插件。
- 非 source-generated serializer/options binding，如果框架要求生成路径。

`AtomUICityStrictAot=true` 时，上述由 .NET AOT、trimming、single-file analyzer 或 City Analyzer 产生的兼容性诊断必须 fail-closed，不能只显示 warning 后返回成功。项目级 `NoWarn` 不得静默屏蔽相关 IL 诊断；合法例外使用带 justification 的源码级 suppression。Strict AOT 只作用于 runtime closure，不得把 `IsAotCompatible` 等运行时属性传给 Generator/MSBuild Task 项目。最终兼容性结论仍需要真实 NativeAOT publish/run，Analyzer 不能替代原生执行证据。

### 6. 测试门禁

Analyzer 可以辅助发现：

- 模块详细文档缺少测试矩阵。
- 测试项目缺少功能点测试。
- 生成 manifest 中功能点没有对应测试索引。

具体实现可以分阶段，但 Build 文档必须把测试门禁作为目标。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| diagnostic id | Analyzer test | 规则触发稳定 ID。 |
| diagnostic location | Analyzer test | 定位到声明位置。 |
| strict AOT | Analyzer/Build/NativeAOT process test | reflection JSON、RUC、缺失 DAM 与 dynamic code 跨 TFM 阻断；安全 runtime 通过；tooling-only project 不误报；最终应用 publish/run。 |
| plugin leak | Analyzer test | private contract 泄漏报错。 |
| route conflict | Analyzer test | 重复 route 报错。 |
| no false positive | Analyzer test | 正确代码不报错。 |
| root provider ownership | Analyzer/package smoke test | 直接调用、重载、强转、静态调用、方法引用、helper、独立 ServiceCollection、service provider factory 和 Microsoft Generic Host 构建/启动入口报 `AUCANL0001`；City ApplicationHost Build、已有 IHost 的启动、测试项目和 generated code 豁免。 |
