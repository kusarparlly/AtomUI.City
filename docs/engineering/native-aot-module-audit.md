# NativeAOT 模块级专项审计

版本：v1.0
审计日期：2026-10-22
状态：审计完成；本期范围问题已施工并验证，PluginSystem 两项按版本计划延期
基线：当前工作树中的 16 个 `src/AtomUI.City.*` 项目、模块设计文档、测试、fixtures 和本地候选 NuGet 包

## 1. 审计目的

本报告逐个检查 AtomUI.City 模块中可能破坏 NativeAOT、trimming 或 single-file 的实现，并用真实发布和原生进程运行验证静态分析结论。

本报告先形成审计证据和问题清单，随后按 ADR 0017 至 0021 完成范围内整改。普通 build/test 仍不视为 NativeAOT 成功；只有进入最终应用运行时闭包、依赖未保留元数据或依赖动态代码的实现，才构成运行时风险；仅在 Roslyn、MSBuild task 或 CLI 工具进程内执行的代码单独判定。

## 2. 范围与验收边界

纳入全部 16 个源码项目：

- 运行时模块：Core、Data、EventBus、Localization、Mvvm、PluginSystem、Presentation、Routing、Security、State。
- 构建或工具模块：Build、Build.Tasks、Cli、Generators、Templates、Testing。

本轮冻结的验证边界：

- 主要运行平台：Windows `win-x64`。
- 目标框架：`net8.0` 与 `net10.0`，二者都必须保留和验证。
- 验证层级：源码危险模式扫描、AOT/trimming analyzer、真实 `dotnet publish -p:PublishAot=true`、原生 exe 运行、本地 NuGet 消费者。
- Linux 和 macOS：本轮标记为 **未验证**，不以 Windows 交叉发布伪造通过结论。.NET NativeAOT 不支持跨操作系统交叉编译，参见 [.NET Native AOT deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/cross-compile)。
- PluginSystem：动态插件和静态插件分别判断；“动态加载天然不支持 NativeAOT”不能掩盖安装、清单或静态插件路径中的可修复问题。

判定术语：

- **Verified**：当前快照已经真实发布并运行，且模块自身没有已知 AOT/trimming 诊断。
- **Conditional**：可生成并运行原生程序，但存在 IL 诊断、未覆盖公开路径或传递风险，不能声明工业级完成。
- **Failed**：真实 NativeAOT 发布或原生运行已确定失败。
- **Tooling-only**：在构建期/工具进程运行，不进入最终应用 NativeAOT 闭包；它自身是否作为 NativeAOT 工具发布不在本轮合同内。
- **Gate incomplete**：生产能力可能可用，但自动门禁不能阻止回归或会给出虚假安全感。

严重度：

- **P0**：默认核心路径无法发布/启动，或会造成不可恢复的数据破坏。
- **P1**：公开或默认路径存在真实 AOT 风险，或名义上的严格门禁不能可靠发现/阻止风险。
- **P2**：非默认、内部或测试基础设施问题；当前生产路径可用，但会降低持续验证可信度。

## 3. 总体结论

本轮未发现 P0，确认 **7 个 P1、3 个 P2**。

原始审计确认 7 个 P1、3 个 P2；截至 2026-10-23 的整改结论是：

- Core、Data、EventBus 的专项原生路径保持通过；EventBus projector 已加入双 TFM 原生 DI 解析与投影证据。
- 新增持久化 `AtomUI.City.NativeAot.RuntimeMatrix`，在 `net8.0/win-x64` 与 `net10.0/win-x64` 原生进程中同时验证 Core、State、Routing、Security、Mvvm 与 Localization。
- Mvvm 已拆分普通/验证型基类并由 Generator 静态生成验证；Localization 已删除按路径动态加载语言 DLL；两者均 clean publish/run。
- `AtomUICityStrictAot` 已 fail-closed，Testing API 已明确降级为 source-pattern lint；本地候选 NuGet 综合消费者已无 ProjectReference 通过。
- PluginSystem 的 CITY-AOT-004/005 按用户确认延后到该模块正式建设版本，不计入当前 runtime matrix 的通过声明。
- Windows `win-x64` 以外的平台没有验证结论。

## 4. 模块判定地图

| 模块 | 类型 | 当前判定 | 主要证据 / 风险 |
|---|---|---|---|
| AtomUI.City.Build | Build tooling | Verified gate | Strict AOT 对 runtime project fail-closed，拒绝 IL suppression/downgrade，并隔离 tooling-only project。 |
| AtomUI.City.Build.Tasks | Build tooling | Tooling-only | 反射和普通 `JsonSerializer` 在 MSBuild 进程执行，不进入应用运行时闭包。 |
| AtomUI.City.Cli | Tooling | Tooling-only | 普通 JSON 序列化属于 CLI 进程；不影响由 CLI 生成的应用闭包。CLI/template 仅能检查已声明的 AOT/dynamic-plugin 选项冲突。 |
| AtomUI.City.Core | Runtime | Verified | testing factory 已移出 runtime assembly；Core MVP 与综合 runtime matrix 双 TFM 原生通过。 |
| AtomUI.City.Data | Runtime | Verified | 双 TFM `Data.AotApp` 发布和原生运行通过，输出 `DATA_AOT_OK`，未发现模块自身 IL 诊断。 |
| AtomUI.City.EventBus | Runtime | Verified | generated catalog 与带 DI 依赖 projector 均通过双 TFM NativeAOT 进程测试。 |
| AtomUI.City.Generators | Compiler tooling | Tooling-only / generated path verified | Roslyn 代码不进入运行时；Core、EventBus、Data、Routing、Localization、Mvvm 生成结果已有真实 AOT 证据。 |
| AtomUI.City.Localization | Runtime | Verified | embedded `SourceAssembly`、file、in-memory 三条默认安全路径在双 TFM 原生进程通过；无路径 DLL 加载。 |
| AtomUI.City.Mvvm | Runtime | Verified | 自有轻量 ViewModel 与 generated validation 在双 TFM 原生进程中验证错误产生、清除和通知。 |
| AtomUI.City.PluginSystem | Runtime | Deferred | CITY-AOT-004/005 保留原审计结论，等待 PluginSystem 正式建设版本；不计入当前矩阵。 |
| AtomUI.City.Presentation | Runtime | Verified（当前 Windows 边界） | 双 TFM headless 原生发布/运行通过，上游 Mvvm/Core 传递诊断已消除。 |
| AtomUI.City.Routing | Runtime | Verified | generated route manifest、reference 与 matcher 已进入持久双 TFM runtime matrix。 |
| AtomUI.City.Security | Runtime | Verified | permission registry/checker 的 allowed/challenge 路径进入持久双 TFM runtime matrix。 |
| AtomUI.City.State | Runtime | Verified | registry、write、observe、computed state 进入持久双 TFM runtime matrix。 |
| AtomUI.City.Templates | Tooling | Tooling-only | 模板 smoke 中的反射是测试生成物校验；生成的应用模板启用 AOT 配置。本轮不要求模板渲染器自身 NativeAOT。 |
| AtomUI.City.Testing | Test tooling | Tooling-only / correctly scoped | API 已更名为 source-pattern lint，并明确不能作为 AOT-compatible 证明；真实门禁由 Build 与 publish/run 承担。 |

## 5. P1 问题

### CITY-AOT-001：Mvvm 默认基类不是 trimming-clean

位置：

- `src/AtomUI.City.Mvvm/ViewModelBase.cs:10`
- `src/AtomUI.City.Mvvm/ViewModelBase.cs:25`
- `src/AtomUI.City.Mvvm/ViewModelBase.cs:338`
- `src/AtomUI.City.Mvvm/ViewModelBase.cs:353`

`ViewModelBase` 直接继承 CommunityToolkit 的 `ObservableValidator`。严格 analyzer 在构造函数以及两个带 `validate` 的 `SetProperty` 转发处报告 IL2026。由于每个业务 ViewModel 都会经过基类构造，这不是孤立辅助方法；启用 DataAnnotations validation 时还可能依赖被 trimming 删除的元数据。

实测结果：基础属性、activation 和 disposal 在双 TFM 原生应用中能够运行，但“当前样例运行成功”不能消除 `RequiresUnreferencedCode` 合同。文档将 Mvvm 描述为默认 AOT-first，因此在给出明确的 validation 策略、DAM/生成方案或 AOT 专用基类前，不能判定完成。

实施状态（2026-10-23）：已完成。`ViewModelBase` 已成为 City 自有轻量基类，需要验证的类型显式继承 `ValidatingViewModelBase`；Generator 将首批 DataAnnotations、Compare 和 CustomValidation 转换为静态代码，不保留 `ObservableValidator` 反射兼容路径。Generator `209/209`、Mvvm `117/117` 及双 TFM NativeAOT Runtime Matrix 通过。

### CITY-AOT-002：Localization 默认注册了 NativeAOT 不允许的 assembly provider

位置：

- `src/AtomUI.City.Localization/LocalizationServiceCollectionExtensions.cs:37`
- `src/AtomUI.City.Localization/LocalizationServiceCollectionExtensions.cs:38`
- `src/AtomUI.City.Localization/AssemblyLanguagePackageProvider.cs:143`
- `src/AtomUI.City.Localization/AssemblyLanguagePackageProvider.cs:153`
- `docs/modules/localization/language-package-assemblies.md:194`

设计文档要求 NativeAOT 发布“不注册 assembly provider descriptor，应用显式注册 file locpack descriptor”，但 `AddLocalization()` 无条件注册 File、Assembly、InMemory 三种 provider。`AssemblyLanguagePackageProvider` 的同一个可达方法包含 `AssemblyLoadContext.LoadFromAssemblyPath`，因此即便应用只使用 file/in-memory locpack，严格发布仍会得到 IL2026/IL2104。

双 TFM组合原生应用证明 file/in-memory 能力本身可运行；隔离 NuGet 消费者则证明该默认注册会污染真实包消费：`net10.0` 在 warning-as-error 下被 `AtomUI.City.Localization` 的 IL2104 阻断，放宽警告后才能发布和运行。

实施状态（2026-10-23）：已完成。默认 Localization 保留 file、in-memory 与已知 `SourceAssembly` embedded locpack，已删除按路径动态加载语言 DLL 与 `LoadContext` descriptor API；location-only assembly descriptor 稳定返回 `InvalidDescriptor`。Localization `97/97` 与双 TFM NativeAOT Runtime Matrix 通过。

### CITY-AOT-003：EventBus diagnostic projector 的公开注册入口缺少构造器可达性合同

位置：

- `src/AtomUI.City.EventBus/EventBusServiceCollectionExtensions.cs:100`
- `src/AtomUI.City.EventBus/EventBusServiceCollectionExtensions.cs:106`

`AddEventPayloadDiagnosticProjector<TEvent,TProjector>()` 把 `TProjector` 交给 `TryAddSingleton<TProjector>()`，但泛型参数没有声明 `DynamicallyAccessedMembers(PublicConstructors)`。严格 analyzer 报告 IL2091。链接器可能无法证明 DI 需要的公开构造器必须保留。

现有 EventBus 双 TFM NativeAOT fixture 覆盖 generated event catalog、静态 handler invoker 和投递流程，但没有调用这一公开入口。因此“EventBus AOT 测试通过”不能覆盖本问题。

实施状态（2026-10-23）：已完成。公开扩展方法已为 `TProjector` 传播 `DynamicallyAccessedMembers(PublicConstructors)`；带构造依赖 projector 的 `net8.0`/`net10.0` NativeAOT 进程测试均完成注册、DI 解析和实际投影。

### CITY-AOT-004：Plugin package installer 在 NativeAOT 原生进程中确定性失败

位置：

- `src/AtomUI.City.PluginSystem/PluginPackageInstaller.cs:141`
- `src/AtomUI.City.PluginSystem/PluginPackageInstaller.cs:152`

安装器使用基于运行时反射的 `JsonSerializer.Serialize(installation, options)` 写入安装记录，没有 source-generated `JsonTypeInfo`。严格 analyzer 报告 IL2026 和 IL3050。

本轮构造合法插件目录并在 `net8.0/win-x64`、`net10.0/win-x64` 原生进程中实际调用 `InstallFromDirectoryAsync`，两者均确定性失败：

```text
System.InvalidOperationException: Reflection-based serialization has been disabled...
```

这发生在包安装/元数据持久化阶段，不是动态 assembly 加载阶段；即使未来只支持静态插件，该问题仍必须单独解决。

### CITY-AOT-005：动态插件的不兼容边界没有形成可靠的 Host 级拒绝

位置：

- `src/AtomUI.City.PluginSystem/PluginLoader.cs:35`
- `src/AtomUI.City.PluginSystem/PluginLoader.cs:39`
- `src/AtomUI.City.PluginSystem/PluginLoader.cs:81`
- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.targets:43`
- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.targets:44`

`PluginLoader` 依赖 collectible `AssemblyLoadContext` 和 `LoadFromAssemblyPath`，这是动态插件模式的本质能力，不能在 NativeAOT 中伪装为支持。问题不在于保留这段 CoreCLR 实现，而在于边界不完整：

- runtime 没有基于 `RuntimeFeature` 的明确、稳定、可诊断拒绝；
- build target 只在当前项目设置 `AtomUICityPlugin=true` 时检查 `AtomUICityPluginNativeAotCompatible`；
- NativeAOT Host 引用 PluginSystem 并调用动态 loader 时，不会因为这一使用事实在构建期被拒绝；
- 静态插件路径尚没有独立的端到端原生发布证据。

工业级合同应让“动态插件不受支持”成为构建期或调用边界的确定性诊断，而不是等待底层运行时 API 失败。

版本状态（2026-10-23）：CITY-AOT-004/005 按用户确认延期到 PluginSystem 正式建设版本；本轮不修改尚未进入当前产品范围的插件实现，也不把 PluginSystem 纳入全运行时模块通过声明。

### CITY-AOT-006：`AtomUICityStrictAot` 名义严格，实际不会统一阻断 IL 诊断

位置：

- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.props:32`
- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.props:33`
- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.props:36`
- `src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.targets:43`

该开关会设置 `IsAotCompatible` 并启用 AOT、trim、single-file analyzer，但没有定义一致的 IL warning 阻断策略；专用 target 只有一条 plugin project 属性检查。因此：

- Mvvm、Localization、EventBus、PluginSystem 的严格 library build 都能带着 IL 警告退出 0；
- 插件 installer 原生运行会崩溃，但 strict publish 本身仍可成功；
- 隔离 NuGet 消费者在 `net10.0` 因项目自身 `TreatWarningsAsErrors=true` 才阻断，在 `net8.0` 同类 IL2104 仍以 warning 通过，行为不统一。

`StrictAot` 可以不等于自动设置 `PublishAot`，但必须对其承诺检查的风险给出确定、跨 TFM 一致的失败合同。

实施状态（2026-10-23）：已完成。Strict AOT 对 runtime project 启用 AOT/trim/single-file analyzer 与 warning-as-error，`AUCBLD0302` 拒绝项目级 IL suppression/downgrade；tooling-only project 不接收运行时属性。真实正向/负向 Build 门禁与双 TFM publish/run 均通过。

### CITY-AOT-007：Testing 的默认 AOT 检查会产生明显假阴性

位置：

- `src/AtomUI.City.Testing/AotCompatibilityCheck.cs:43`
- `src/AtomUI.City.Testing/AotCompatibilityCheck.cs:45`
- `src/AtomUI.City.Testing/AotCompatibilityCheck.cs:47`
- `src/AtomUI.City.Testing/AotCompatibilityCheck.cs:71`

`ForbidDefaultAotPatterns()` 只在源码文本中搜索：

- `Assembly.GetTypes`
- `Activator.CreateInstance`
- `DynamicMethod`

它不会运行 linker/analyzer，也不能识别本轮实际发现的 `LoadFromAssemblyPath`、reflection JSON、泛型 DI 构造器可达性、`RequiresUnreferencedCode` 调用或不当 suppression。若把它当作 AOT 兼容证明，会漏掉本报告全部 P1 生产风险。

该 API 可以保留为轻量 lint，但必须更名/降级其承诺，或由真实 analyzer/publish gate 承担 AOT 验收。

实施状态（2026-10-23）：已完成。API 已更名为 `AotSourcePatternCheck` / `AotSourcePatternDiagnostic` 和 `ForbidDefaultAotRiskPatterns`，测试固定假阳性/假阴性边界；Testing `64/64` 通过。真实兼容性仍由 Build strict analyzer 与 NativeAOT publish/run 承担。

## 6. P2 问题

### CITY-AOT-008：Core 内部 testing factory 污染 library analyzer 结果

位置：

- `src/AtomUI.City.Core/Modularity/ModuleRegistry.cs:84`
- `src/AtomUI.City.Core/Modularity/ModuleRegistry.cs:94`

内部 `CreateForTesting` 接收普通 `Type` 并调用 `Activator.CreateInstance`，没有 public constructor 的 DAM 合同，严格 library build 报 IL2067。该方法不是开发者公开 API，Core 双 TFM MVP 原生程序的生产 generated path 已通过，因此不把它升级为 P1；但它让被声明为 AOT-compatible 的发货程序集无法做到 analyzer-clean。

Core 生产路径中的 generated registrar type 已通过 `GeneratedModuleManifestAttribute` / `GeneratedServiceManifestAttribute` 的 `PublicParameterlessConstructor` 标注保留，并由真实原生测试验证，不属于本问题。

实施状态（2026-10-23）：已完成。`ModuleRegistry.CreateForTesting` 已从 Core runtime assembly 删除，等价便利逻辑已迁移到 `AtomUI.City.Core.Tests`；未使用 suppression、RUC 或 DAM。全量 `src` 边界扫描未发现第二处同类泄漏；Core 全量测试 `269/269`、双 TFM strict AOT/trim build、Core NuGet 内容检查，以及双 TFM NativeAOT generated factory 发布和完整运行矩阵均通过。

### CITY-AOT-009：缺少覆盖全部运行时模块的持久双 TFM 原生门禁

当前已有持久证据集中在 Core、EventBus 和 Data。Presentation 有可发布 headless fixture，但没有统一的、零诊断双 TFM发布门禁；State、Routing、Security、Mvvm、Localization 主要依赖普通测试或综合 dogfood。

本轮临时组合应用证明 State、Routing、Security 和 Localization file/in-memory 路径能在两个 TFM 原生运行，也暴露了 Mvvm/Localization 警告；但临时审计产物不能代替提交到仓库的回归门禁。

建议后续建立一个最小 runtime matrix，而不是为每个类型创建独立应用：模块自己的关键公开路径必须能被映射到真实 publish/run 断言，且两个 TFM 都执行。

实施状态（2026-10-23）：已完成。仓库新增 `fixtures/AtomUI.City.NativeAot.RuntimeMatrix` 与 `engineering/check-native-aot-runtime-matrix.ps1`，固定覆盖 Core、State、Routing、Security、Mvvm 和 Localization；`net8.0/win-x64` 与 `net10.0/win-x64` 均无未处理 IL 诊断并由原生 EXE 完成结构化业务断言。Windows 结果不外推到 Linux/macOS，PluginSystem 不计入本矩阵。

### CITY-AOT-010：本地候选包消费者模板已经与 EventBus API 漂移

位置：

- `engineering/package-consumers/release/Program.cs.template:37`

模板中的 `ConsumerEventHandler.HandleAsync` 仍使用旧签名，缺少当前接口要求的 `CancellationToken`，导致 `engineering/check-local-package-consumer.ps1` 在进入发布验证前编译失败。本轮只在被 git 忽略的审计工作区临时修正后继续验证，未修改仓库模板。

这不是生产 NativeAOT 实现缺陷，但它会让官方本地 NuGet 消费门禁长期无法提供 AOT 证据，因此作为 P2 基础设施问题记录。

实施状态（2026-10-23）：已完成。综合 release consumer 与 EventBus 专用 consumer 模板已跟随当前 `CancellationToken` 合同并主动观察取消，工程测试锁定两套签名；完整本地候选包门禁已在隔离 package cache、无 `ProjectReference` 下完成 restore、双 TFM build、net10 publish 和运行，输出 `RELEASE_PACKAGE_CONSUMER_OK`。

## 7. 已排除的误报与安全边界

### 7.1 构建期反射不等于应用运行时反射

- Build.Tasks 的 assembly metadata 读取和 manifest JSON 在 MSBuild 进程执行。
- Generators 的 Roslyn symbol 分析与生成器内部反射不进入最终应用。
- CLI 的 plan/envelope JSON 在 `atomui` 工具进程执行。
- Templates 的反射主要位于生成物 smoke test。

这些代码只有在未来要求“工具自身也以 NativeAOT 分发”时才需要另立验收，不应混入当前桌面应用运行时结论。

### 7.2 Core 的少量反射桥接有明确可达性标注

Core 从 application assembly 读取 generated manifest attribute，再构造 registrar。registrar type 属性具有 `DynamicallyAccessedMembers(PublicParameterlessConstructor)`，生成代码提供强类型 descriptor/factory；双 TFM Core MVP 真实运行通过。不能因为看到 `Activator.CreateInstance` 就把整条生产链判定为不兼容。

### 7.3 EventBus 的 `AssemblyLoadContext` 查询不是动态加载

EventBus 只使用 `AssemblyLoadContext.GetLoadContext` 校验 shared/private contract 的所有权，没有调用 `LoadFromAssemblyPath`。该检查自身未产生 IL 警告。真正的问题是 CITY-AOT-003 的 DI projector 注册。

### 7.4 Security 的 JSON 路径符合 AOT 设计

Security 持久化使用 source-generated `SecurityJsonSerializerContext` / `JsonTypeInfo`，严格 analyzer 和组合原生运行均未发现模块自身问题。

## 8. 动态插件与静态插件结论

| 模式 | 当前结论 | 说明 |
|---|---|---|
| CoreCLR 动态插件 | NativeAOT 不支持，属预期边界 | 可以保留，但必须在 AOT Host 中确定性拒绝并给出 City diagnostic。 |
| 插件包发现/校验 | 未证明 AOT 完成 | 与动态 load 可分离，仍需检查 JSON、文件和签名路径。 |
| 插件包安装 | Failed | reflection JSON 已在双 TFM 原生运行中确定失败。 |
| NativeAOT 静态插件 | 未验证/未闭环 | 文档有方向，当前没有完整生成、链接、manifest、启停和原生运行证据。 |

因此，PluginSystem 不能整体贴上“NativeAOT 不支持”后跳过审计。动态加载可以明确排除，静态可用部分必须继续满足 AOT 合同。

## 9. 实际执行证据

### 9.1 Analyzer 矩阵

对 10 个运行时模块分别执行 `net8.0`、`net10.0` strict library build，并启用：

```text
IsAotCompatible=true
EnableAotAnalyzer=true
EnableTrimAnalyzer=true
EnableSingleFileAnalyzer=true
SuppressTrimAnalysisWarnings=false
```

结果摘要：

| 模块 | 结果 |
|---|---|
| Core | 审计时发现内部 `CreateForTesting` 导致 IL2067；已按 ADR 0020 修复并通过双 TFM strict AOT 与 NativeAOT 回归。 |
| EventBus | IL2091，diagnostic projector DI 注册。 |
| Localization | IL2026，动态 assembly path。 |
| Mvvm | IL2026，`ObservableValidator` 构造和 validation overload。 |
| PluginSystem | IL2026/IL3050，动态 load 与 reflection JSON。 |
| Data / Presentation / Routing / Security / State | 未发现模块自身 IL2xxx/IL3xxx。Presentation 组合发布仍继承 Core/Mvvm 诊断。 |

源码级 EventBus strict build 还出现了 `NETSDK1210`：命令行 AOT 属性继续传入其 `netstandard2.0` Generator 项目，而该工具项目本身不支持 `IsAotCompatible`。该提示不是 EventBus 运行时风险，但进一步说明当前 strict 开关尚未按 runtime/tooling 项目边界收口，归入 CITY-AOT-006。

### 9.2 真实原生发布与运行

| 验证载体 | net8.0/win-x64 | net10.0/win-x64 | 结论 |
|---|---|---|---|
| Core MVP AOT script | Passed | Passed | 6 modules、27 services、120 permutations、32 combinations、64 concurrent scopes。 |
| EventBus NativeAOT process tests | Passed | Passed | generated catalog、20 handlers/6 modules 路径通过。 |
| Data AOT app | Passed | Passed | 输出 `DATA_AOT_OK`。 |
| Presentation HeadlessApp | Passed | Passed | 原生进程完成 window/outlet/ViewModel lifecycle 与 stress；有传递的 Core/Mvvm warning。 |
| 临时 supported-runtime app | Passed | Passed | State、Routing、Security、Localization file/in-memory、Mvvm 基础 lifecycle 通过；Mvvm/Localization 有 warning。 |
| 临时 Plugin installer app | Failed | Failed | reflection-based JSON disabled，安装记录序列化抛异常。 |
| 本地候选 NuGet 消费者（strict） | Passed + run | Failed at publish | net10.0 被 Localization IL2104 + warning-as-error 阻断。 |
| 本地候选 NuGet 消费者（放宽 warning） | 未重复（strict 已运行） | Passed + run | 输出 `RELEASE_PACKAGE_CONSUMER_OK`，证明是 clean gate 失败而非所有功能均不能运行。 |

所有临时审计应用与发布产物均位于 git 忽略的 `output/` 或 `.artifacts/`，不构成生产源码修改。

### 9.3 DesktopDogfood 不能替代 AOT gate

对九模块 DesktopDogfood 的实验性 NativeAOT 发布产生大量 fixture 自身诊断，原生进程还因以下启动方式失败：

- `fixtures/AtomUI.City.Fixtures.DesktopDogfood/Data/DogfoodExternalDataServerProcess.cs:146` 硬编码启动 `dotnet`；
- 同文件 `:154` 使用当前 assembly `Location` 作为子进程参数，而 NativeAOT 中该值不可作为 managed DLL 路径。

其 `DESIGN.md` 已明确“不用 Dogfood 代替 NativeAOT”，因此这不是新的生产模块 P1；它只证明不能把现有综合 dogfood 的普通运行结果当作全模块 AOT 证据。

### 9.4 完整 Release test 的边界

本轮额外执行全解决方案 Release test，但没有取得全绿结果，失败项不能用于改写上述原生发布证据：

- 并行构建发生 Build.Tasks 输出文件占用，且先前按单 TFM 的 AOT restore 改写了部分共享 `project.assets.json`；
- Template smoke 依赖预先 pack 的候选包，并有子进程使用系统 `dotnet`、找不到仓库锁定 SDK 10.0.300 的环境问题；
- EventBus AOT 子进程未继承本地 HTTP proxy，restore 失败；该用例已在独立、带代理执行中双 TFM 通过；
- `ProjectDependencyBoundaryTests` 另有一个与 NativeAOT 无关的真实依赖边界断言失败，应单独分流处理；
- restore 还报告 `Microsoft.AspNetCore.SignalR.Common 10.0.8` 的 NU1903 高危漏洞，属于依赖安全审计，不计入 AOT 问题数量。

报告不宣称全仓普通回归已通过。

## 10. 建议修复顺序

本轮不实施修复。建议后续按以下顺序逐项讨论、文档先行后施工：

1. Plugin installer source-generated JSON（CITY-AOT-004），因为已有双 TFM 真实运行失败。
2. Localization AOT provider selection（CITY-AOT-002），因为它污染默认注册和隔离 NuGet 消费者。
3. Mvvm validation AOT 合同（CITY-AOT-001），先决定保留 ObservableValidator、拆分 AOT 基类还是引入生成路径。
4. EventBus projector 泛型构造器可达性（CITY-AOT-003）。
5. Plugin dynamic/static mode 的确定性边界（CITY-AOT-005）。
6. 收紧 `AtomUICityStrictAot` 并用真实 publish 作为发布门禁（CITY-AOT-006）。
7. 将 Testing 的字符串 lint 与真实 AOT gate 分层（CITY-AOT-007）。
8. 清理三个 P2，并把临时成功证据转成持久双 TFM回归矩阵。

## 11. 发布判定

在 CITY-AOT-001～007 关闭前：

- 可以宣称：Core、Data、EventBus generated catalog 等关键路径已具备 Windows 双 TFM NativeAOT 实证；State/Routing/Security 当前快照可在组合原生应用运行。
- 不可以宣称：City 全模块、默认组合或 PluginSystem 已达到工业级 NativeAOT。
- 不可以用 `AtomUICityStrictAot=true`、`AotCompatibilityCheck` 通过或普通 dogfood 通过，单独作为 NativeAOT 发布批准依据。
- Linux/macOS NativeAOT 状态必须继续标记为未验证，直到各平台使用本机构建工具链完成 publish/run。
