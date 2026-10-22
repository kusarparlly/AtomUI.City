# 模块代码与设计文档对齐审计

版本：v1.0  
审计日期：2026-10-22  
状态：审计完成，除明确排除的 PluginSystem 外差异已收口  
基线：当前工作树中的生产源码、Public API baseline、模块设计文档、模块测试与 fixtures

## 1. 审计目的

本报告执行一次后置对齐审计：以当前已经存在的代码能力为事实基线，逐个核对模块设计文档是否准确描述了实际 API、功能、失败行为和验证证据。

本报告不是新的产品设计，也不直接修改任何模块行为。发现差异后，应先修正文档并确认，再决定保持实现、补齐实现或收缩公开合同。

## 2. 范围与边界

纳入审计的模块：

- Build / Build.Tasks
- CLI
- Core
- Data
- EventBus
- Generators
- Localization
- MVVM
- Presentation
- Routing
- Security
- State
- Templates
- Testing

明确排除：

- `AtomUI.City.PluginSystem` 的设计、源码、测试和发布完成度。
- 其他模块文档中涉及 PluginSystem 的未来集成是否已经真实闭环。
- 对生产代码进行修复或重构。

其他模块中已经公开的 plugin-facing contract 仍计入该模块自己的 API 清单，但本报告不据此推导 PluginSystem 已实现。

## 3. 审计方法

本轮采用四条独立证据链：

1. **功能证据**：`features.md` 的 Feature ID、状态、Public/Internal Contract 与源码入口逐项比较。
2. **API 证据**：以各项目的 `PublicAPI.Shipped.txt` 和 `PublicAPI.Unshipped.txt` 为公开面基线，核对 `api-contracts.md` 及模块专题文档。
3. **验证证据**：核对 Feature 表中声明的测试类型名是否真实存在，并抽查 diagnostics code 是否在模块文档中登记。
4. **工程证据**：对 Build machine-readable contract、CLI executable contract、source generator 初始化入口及测试辅助包所有权进行专项核对。

判定术语：

- **Aligned**：当前实现、Feature 状态、公开合同和测试证据没有发现实质差异。
- **Mostly Aligned**：核心行为一致，但存在公开 API 漏记、陈旧名称或精确合同漂移。
- **Needs Alignment**：文档承诺了源码尚未提供的行为，或源码已有可交付能力却没有 Feature/API 合同。

严重度：

- **P0**：会导致现有合同不可安全使用或发布数据破坏。
- **P1**：会误导后续施工、公开 API 使用或完成度判断，必须在下一轮模块施工前收口。
- **P2**：核心实现可用，但精确 API、证据或工程合同不完整。
- **P3**：非阻断的表达、索引或可维护性问题。

## 4. 总体结论

本轮没有发现 P0。共确认 **8 个 P1、6 个 P2**。

当前可以宣称“本轮纳入范围内、除明确排除的 PluginSystem 外，模块代码与设计文档已完成对齐”。CLI 最后三个生产差异已通过 `AUC-CLI-008～011` 的实现、测试与 Tool package-consumer 门禁关闭；其余 Feature/API/visibility/documentation 漂移也已按实际代码或机器合同完成文档收口：

- CLI 原有占位门禁、空 plan/apply、通用 explain 与普通 Exe 分发已替换为生产实现。
- Generators、Testing 曾存在已经进入代码、但没有在其所属模块取得完整 Feature/API 身份的能力；本轮均已完成文档追认。
- Presentation、Routing、Core、EventBus、MVVM、State 的 public 类型卡、陈旧测试名和 visibility 漂移均已关闭。

| 结论 | 模块 |
|---|---|
| Aligned | Build、CLI、Core、Data、EventBus、Generators、Localization、MVVM、Presentation、Routing、Security、State、Templates、Testing |
| Mostly Aligned | 无 |
| Needs Alignment | 无 |

`Templates` 的 `AUC-TEMPLATES-007` 仍为 `Blocked` 是文档明确记录的真实状态，不属于漂移；`Core`、`Generators`、`Testing` 的 `Partially Implemented` 也不能仅凭名称判定为缺陷。

## 5. 差异清单

| ID | 等级 | 模块 | 类型 | 差异 | 收口方向 |
|---|---:|---|---|---|---|
| ALIGN-001 | P1 | CLI | RESOLVED-CODE | `docs check` / `tests check` 曾只有目录探测占位。现已落地 versioned framework evaluator、稳定 `AUCCLI06xx`、真实仓库正/负向、取消和只读测试；当前 docs 408 文件、tests 268 文件均通过。 | 后续规则变化同步 rule-set version、治理合同与 `CliGovernanceGateTests`。 |
| ALIGN-002 | P1 | CLI | RESOLVED-CODE | `plan`/`apply`/`explain` 曾是空占位。现已完成 Plan 1.0、隔离 Templates preview、路径/哈希校验、workspace lock、staging/journal、commit/rollback、幂等重放与 immutable explanation catalog。 | 后续 schema 或诊断 identity 变化按 compatibility 合同版本化。 |
| ALIGN-003 | P1 | CLI | RESOLVED-CODE | CLI 曾只能作为普通 Exe 构建。现已设置 `PackAsTool=true`、`ToolCommandName=atomui`，清除 Build runtime 依赖，并通过隔离本地 feed/cache/tool-path 安装及真实成功/失败进程。 | 发布候选持续执行 `check-cli-tool-package-consumer.ps1`。 |
| ALIGN-004 | P1 | Generators | RESOLVED-DOCS | 初始化管线中的 Data 与 EventBus metadata 生成曾缺少 Feature 身份。现已补入 `AUC-GENERATORS-009/010`，并冻结 internal pipeline、generated output、诊断、兼容性和测试矩阵；生产代码未改动。 | 后续变更按新增 Feature 合同和测试门禁维护。 |
| ALIGN-005 | P1 | Core | RESOLVED-DOCS | `hosting.md` 曾声明不存在的 `ILifecycleScope`，并把 `ApplicationScope` 写成非 nullable。现已按 public API baseline 记录 `LifecycleScope HostScope` 与 `LifecycleScope? ApplicationScope`，以及所有权、创建时机和停止后读取语义；生产代码未改动。 | 后续签名或生命周期语义变化必须同步 Public API baseline、Hosting/API/Compatibility 文档和生命周期测试。 |
| ALIGN-006 | P1 | Presentation | RESOLVED-DOCS | 32 个已冻结 public 类型曾缺少可定位 API 卡。现已按注册入口、View/Outlet/Command、Visual lifecycle、Presentation-owned plugin-facing 四组补齐 Purpose、Owner、Lifetime、Threading、Disposal、Failure、Stability 与 Tests；生产代码未改动。 | 后续 public surface 变化必须同步精确类型卡；plugin-facing 合同不得被解释为 PluginSystem 已完成。 |
| ALIGN-007 | P1 | Routing | RESOLVED-DOCS | 30 个已冻结 public 类型曾只以类型族概括。现已按 route declaration attributes、navigation/history/match、guard/match/resolver/middleware 和 DI 入口补齐精确 API 卡及测试/兼容性合同；复核未发现必须收缩的明显误公开类型，生产代码未改动。 | 后续 AttributeUsage、Generator 接纳、enum、middleware next 或 DI lifetime 变化必须同步精确类型卡。 |
| ALIGN-008 | P1 | Testing | RESOLVED-DOCS | Testing 包公开的 7 个 Data test doubles 曾只在 Data 文档中记录。现已建立 `AUC-TESTING-010`，并补齐精确 API 卡、失败/取消/并发边界、测试矩阵、跨模块依赖方向和兼容性所有权；生产代码未改动。 | Data 继续拥有运行时 contract，Testing 拥有测试实现；后续变更按 `AUC-TESTING-010` 和 `DataTestDoublesTests` 维护。 |
| ALIGN-009 | P2 | Build | RESOLVED-DOCS | API 文档曾把七个 buildTransitive 资产写成六个，并错误记录 `AtomUICityPluginProfile` 默认值。现已按 machine-readable contract 修正资产数、精确默认值、非插件 public property、plugin-facing property 和 public target 清单；生产代码未改动。 | `AtomUI.City.Build.contract.json` 继续作为事实基线，文档和精确一致性测试随其同步。 |
| ALIGN-010 | P2 | Core | RESOLVED-DOCS | `LifecycleNext` 与两个 public extension container 曾缺少类型卡，internal `ApplicationHostBuilder` 曾被误列 Public Contract。现已补齐委托/扩展容器合同，并把 public interface 与 internal 默认实现分开；生产代码未改动。 | 后续以 CLR visibility 和 Public API baseline 为准，不把可用概念等同于 public 实现类型。 |
| ALIGN-011 | P2 | EventBus | RESOLVED-DOCS | 已把陈旧订阅测试类名全部修正为真实 `EventSubscriptionTests`，并补齐 diagnostic projector descriptor、plugin access rule、默认 ThreadPool scheduler 与 generated registrar bridge 的合同归属；生产代码未改动。 | generated bridge 的 public 只用于跨程序集装载，PluginSystem 真实集成仍保持 pending/excluded。 |
| ALIGN-012 | P2 | MVVM | RESOLVED-DOCS | 已为 `CommandConcurrencyPolicy`、`CommandExceptionContext` 补齐 API 卡、枚举值/默认值、异常交付、并发准入、兼容性和测试行为；生产代码未改动。 | 后续 CommandFactory 签名、准入策略或 exception handler context 变化同步维护。 |
| ALIGN-013 | P2 | State | RESOLVED-DOCS | 已把 `StateCollectionObservation<TKey,TItem>` 从 Feature 描述提升为正式 API 合同，并冻结初始/变更 observation 的 Snapshot/Changes 互斥关系；生产代码未改动。 | 后续 observation 字段或原子注册语义变化同步 compatibility 与 StateCollectionTests。 |
| ALIGN-014 | P2 | CLI | RESOLVED-DOCS | 已将 `CliCommandLine`、`CliEnvelope`、`CliDiagnostic`、`DotnetInvocation`、`ProcessRunner` 明确标为 internal CLR contract，同时保留命令语法、JSON 和 invocation 字段作为 public process contract；生产代码未改动。 | 不扩大 CLR visibility；进程边界兼容性继续由 CLI API/compatibility 文档维护。 |

## 6. 逐模块结论

### 6.1 Build / Build.Tasks — Aligned

对齐项：

- Build 是 build-only 包、Build.Tasks 是 tools-only implementation detail 的边界与工程结构一致。
- CLR Public API baseline 为空，机器可读 MSBuild contract 是正确的兼容性基线。

原差异已经收口：文档现按 [AtomUI.City.Build.contract.json](../../src/AtomUI.City.Build/buildTransitive/AtomUI.City.Build.contract.json) 精确记录七个 buildTransitive 资产、`AtomUICityPluginProfile=$(AtomUICityPluginApiVersion)-stable`、此前遗漏的五个非插件 public property，以及当前 public plugin-facing property 和 target 清单。`ALIGN-009` 已关闭，生产代码未修改。

Plugin 相关 property/target 的语义验证不在本轮范围内。

### 6.2 CLI — Aligned

CLI 的命令、治理门禁、生成、事务、诊断解释和分发边界已经与 `AUC-CLI-001～011` 对齐：

- `FrameworkGovernanceGate` 对 framework profile 执行确定性只读检查，真实仓库 `docs check` 与 `tests check` 均为零错误。
- `PlanApplyTransaction` 使用现有 Templates renderer 生成隔离 preview，持久化严格 Plan 1.0，并以 workspace lock、staging、journal、commit/rollback 和 content hash 实现 create-only apply。
- `DiagnosticExplanationCatalog` 覆盖 CLI 产生及明确透传的稳定 code，未知 code 保持失败。
- `AtomUI.City.Cli` 已打包为命令 `atomui` 的 .NET Tool；候选包包含实际运行依赖、不包含 Build runtime assembly，并通过隔离安装与真实进程验证。

`ALIGN-001～003` 已关闭。plugin generation 仍按文档明确延期到 PluginSystem，不属于 CLI 对齐缺口。

### 6.3 Core — Aligned

Core 的 Hosting、Lifecycle、Modularity、DI、Diagnostics 和并发合同整体能够追踪到源码及测试；诊断码抽查没有发现未登记的 Core code。

原差异已经收口：[hosting.md](../modules/core/hosting.md) 已按源码修正 Host/Application Scope 签名；`LifecycleNext`、`ApplicationHostBuilderDiagnosticsExtensions` 与 `ApplicationHostBuilderLifecycleExtensions` 已取得精确 API 卡；public `IApplicationHostBuilder` 与 internal `ApplicationHostBuilder` 默认实现已分开表述。`ALIGN-005/010` 均已关闭，生产代码未修改。

Core 文档声明 `Partially Implemented` 且明确列出未分配 Feature 的未来方向，这一成熟度表述与当前治理规则一致。

### 6.4 Data — Aligned

- `AUC-DATA-001` 至 `AUC-DATA-020` 的实现、公开 API、测试类和诊断合同可互相追踪。
- Public API baseline 中的类型均能在 `api-contracts.md` 找到。
- Testing 包中的 Data doubles 已由 Data Feature 说明 consumer-side 需求，并由 Testing 的 `AUC-TESTING-010` 承担 provider-side 公开合同与兼容性所有权；原 `ALIGN-008` 已关闭。

本轮未发现 Data 模块自身的阻断差异。

### 6.5 EventBus — Aligned

Event contract、registry、dispatch、backpressure、diagnostics、module lifecycle 和 AOT/generator 边界整体与实现一致。PluginSystem 的真实 lifecycle integration 在文档中被明确标记为 pending，本轮不判定其完成度。

原差异已经收口：测试证据已统一为真实 `EventSubscriptionTests`；`EventPayloadDiagnosticProjectorDescriptor`、`EventPluginAccessRule`、`ThreadPoolEventBackgroundScheduler` 与 generated registrar bridge 均已进入精确 API/兼容性合同。generated registrar 的 public visibility 只服务跨程序集生成/装载，不构成普通开发者调用入口。`ALIGN-011` 已关闭，生产代码未修改。

### 6.6 Generators — Aligned

[AtomUICityIncrementalGenerator.Initialize](../../src/AtomUI.City.Generators/AtomUICityIncrementalGenerator.cs) 当前初始化 DI、Modularity、Routing、Presentation、Localization 和 Data。EventBus contract/handler generation 也已经进入 DI generation pipeline。

[features.md](../modules/generators/features.md) 现已用 `AUC-GENERATORS-009` 和 `AUC-GENERATORS-010` 追认 EventBus/Data 生成能力，并在 API、diagnostics、testing、compatibility、architecture 与 integration 文档中冻结其 internal pipeline 和 generated output 合同。Generators 的两个 CLR public tooling entry 保持不变；本项修复不修改生产代码。

### 6.7 Localization — Aligned

- 九个 Localization Feature、公开 API、generated manifest/registrar、fallback、formatting、State integration 和测试证据一致。
- Public API baseline 类型均进入模块 API 文档。
- 诊断码抽查未发现源码已用但文档未登记的 code。

本轮未发现阻断差异。

### 6.8 MVVM — Aligned

Activation、structured concurrency、commands、interaction、validation 和 diagnostics 的设计与实现总体一致；文档也准确区分 Verified baseline 与尚待发布验证的 expansion。

`CommandConcurrencyPolicy` 与 `CommandExceptionContext` 现已取得精确 API 卡，并记录固定枚举值、默认 Reject、RejectByKey selector、原始异常恰好一次、取消排除和 handler failure 隔离规则。`ALIGN-012` 已关闭，生产代码未修改。

### 6.9 Presentation — Aligned

Presentation 的 Release 验证、诊断码、composition/coordination、Headless 和 package consumer 证据较完整。此前 public surface 增长后有 32 个类型未同步 API 文档；现已在 [api-contracts.md](../modules/presentation/api-contracts.md) 中按类型族补齐精确 API 卡：

```text
ActivePluginView
ActivePluginViewRegistry
ActivePluginViewRegistryServiceCollectionExtensions
AvaloniaUiDispatcherServiceCollectionExtensions
IActivePluginViewLease
ICommandBindingHandle
IPresentationResourceLease
NullPresentationFailurePresenter
PresentationInteractionServiceCollectionExtensions
PresentationPluginUnloadCoordinator
PresentationPluginUnloadCoordinatorServiceCollectionExtensions
PresentationPluginUnloadError
PresentationPluginUnloadErrorKind
PresentationPluginUnloadRequest
PresentationPluginUnloadResult
PresentationResourceContribution
PresentationResourceDictionaryRevocation
PresentationResourceRegistry
PresentationResourceRegistryServiceCollectionExtensions
PresentationRuntimeServiceCollectionExtensions
RouteOutletCommitResult
RouteOutletProperties
UiCommandState
UiStateFeedbackKind
UiStateFeedbackPolicy
ValidationVisualStateSnapshot
ViewFactoryContext
ViewModelAcquisitionRequest
ViewRegistryServiceCollectionExtensions
VisualLifecycleEvent
VisualLifecycleEventKind
VisualLifecycleSubscriptionOptions
```

plugin-facing 类型已与通用 Presentation 类型分组；前者只冻结本模块合同，不宣称 PluginSystem 集成完成。`ALIGN-006` 已关闭，生产代码未修改。

### 6.10 Routing — Aligned

Routing 的 route graph、matching、navigation、guard、resolver、history、coordinator 和 commit receipt 功能均有实现和测试证据。此前有 30 个 public 类型只以类型族概括；现已在 [api-contracts.md](../modules/routing/api-contracts.md) 中补齐精确 API 卡：

```text
FragmentAttribute
IndexRouteAttribute
IRouteNavigationMiddleware
LayoutRouteAttribute
NavigationConcurrencyPolicy
NavigationError
NavigationHistoryBehavior
NavigationMode
QueryAttribute
RedirectRouteAttribute
RouteAttribute
RouteDefinitionAttribute
RouteDefinitionKind
RouteExtensionPointAttribute
RouteGroupAttribute
RouteGuardContext
RouteGuardResultStatus
RouteGuardsAttribute
RouteMapAttribute
RouteMatchPoliciesAttribute
RouteMatchPolicyContext
RouteMatchStatus
RouteMiddlewareAttribute
RouteNavigationDelegate
RouteNavigationMiddlewareContext
RouteResolveResult
RouteResolveResultStatus
RouteResolversAttribute
RouteTemplateSegmentKind
RoutingServiceCollectionExtensions
```

这些类型中相当一部分位于开发者直接使用的 attribute 和 navigation result surface，现已记录 AttributeUsage、默认值、Generator 接纳、生命周期、线程、失败和测试合同。`ALIGN-007` 已关闭，生产代码未修改。

### 6.11 Security — Aligned

- `AUC-SECURITY-001` 至 `009`、公开 API、诊断和测试证据一致。
- 文档准确声明 Security generator、Plugin capability/ContributionLease、系统安全保险库 Provider 和 Testing helper 不是当前能力。
- Public API baseline 类型均进入 API 文档。

本轮未发现阻断差异。

### 6.12 State — Aligned

State 的 44 个 public 类型均能在模块文档中找到，Feature 与测试映射完整。`StateCollectionObservation<TKey,TItem>` 已进入 `api-contracts.md`，并冻结初始/变更 observation 的 Snapshot/Changes 互斥和版本语义。`ALIGN-013` 已关闭，生产代码未修改。

### 6.13 Templates — Aligned

- 已完成 Feature 与 renderer/plan/transaction/smoke 实现一致。
- 九个 public 类型均进入 API 文档。
- `AUC-TEMPLATES-007 Page Template` 明确标为 `Blocked`，文档没有把它误报为已交付能力。

本轮未发现额外漂移。

### 6.14 Testing — Aligned

Test Host、Fake Dispatcher、Deterministic Scheduler、Module/Routing host、source generation kit、AOT check 和 test layer 均有 Feature 与实现。State Testing 文档也明确将尚未实现的 `StateTestStore` 标为目标设计，不构成虚假完成声明。

以下已公开 Data test doubles 现已作为 `AUC-TESTING-010` 进入 Testing 模块自己的 Feature/API/Testing/Compatibility 文档：

```text
FakeDataConnection
RecordedDataRequest
RecordingDataRequestHandler
ScriptedDataCredentialProvider
ScriptedDataInvocation
ScriptedDataResponse
ScriptedDataTransport
```

所有权已明确为：Data 定义运行时接口与结果模型，provider package `AtomUI.City.Testing` 拥有测试实现及其公开兼容性合同；生产项目不得引用 Testing。现有 `DataTestDoublesTests` 是实现证据，测试矩阵同时保留空脚本、取消、委托异常和类型不匹配等持续门禁。

Plugin Test Host 的实际集成完成度按本轮排除边界不作结论。

## 7. 交叉验证结果

### 7.1 Feature 到测试类

非 PluginSystem 模块的 `features.md` 共提取到 157 个 `*Tests` 类型引用。原 EventBus 订阅测试陈旧名称已经对齐为真实 `EventSubscriptionTests`。`EventBusPluginLifecycleTests` 仍由文档明确写为 PluginSystem 可用后补充，属于本轮排除范围，不构成本轮未收口漂移。

### 7.2 Diagnostics

对 Core、Data、Localization、MVVM、Routing、Security、State、Templates、Testing 的源码诊断 code 与模块文档进行字符串级抽查，没有发现生产源码使用但模块文档完全未登记的 code。EventBus 和 Presentation 的模块前缀不同，另行核对后也未发现明确缺口。

该结果只证明“code 已登记”，不等于每个诊断的 message/context/recovery 均已完成语义验收。

### 7.3 Public API 覆盖

| 模块 | Public 类型数 | 整个模块文档未按类型名记录 |
|---|---:|---:|
| CLI | 3 | 0；internal CLR 类型与 public process contract 已明确分离 |
| Core | 56 | 0 |
| Data | 117 | 0 |
| EventBus | 62 | 0 |
| Generators | 0 | 0 |
| Localization | 34 | 0 |
| MVVM | 33 | 0 |
| Presentation | 100 | 0 |
| Routing | 66 | 0 |
| Security | 69 | 0 |
| State | 44 | 0 |
| Templates | 9 | 0 |
| Testing | 39 | 0 |

Build 不发布 CLR library public API，因此未计入该表；其公开面以 MSBuild machine-readable contract 为准。

“未按类型名记录”是 API 文档完整性指标，不自动等价于实现缺陷。某个概念可能已有专题设计，但按照当前 [模块文档设计规范](module-documentation-standard.md)，public 类型仍应有可定位的行为合同。

## 8. 建议收口顺序

遵循文档先行原则，建议按以下顺序处理：

1. **CLI**：门禁、事务、explain 与 Tool 分发已经完成，后续持续执行 contract/package-consumer 门禁。
2. **Generators + Testing**：已为进入代码的能力补齐 Feature 身份、provider 所有权与兼容性合同；后续只需持续维护。
3. **Core**：Hosting public signature、delegate 与 extension container 类型卡已经修正；后续按 baseline 维护。
4. **Presentation + Routing**：两模块的 public API 类型卡缺口均已完成文档收口；后续只按精确类型卡维护。
5. **Build、EventBus、MVVM、State**：精确合同、陈旧名称和 API 漏项已经收口；后续按机器合同和 Public API baseline 维护。
6. CLI `AUC-CLI-008～011` 与 `ALIGN-001～003` 已关闭；本轮没有剩余施工项。

## 9. 完成门禁

本报告中的某项只有同时满足以下条件才可关闭：

- 差异对应的模块文档已更新并经过确认。
- Feature ID、Stability、Public/Internal Contract 与实际 visibility 一致。
- 如选择补实现，测试矩阵先于代码更新，并增加所需 Unit/Contract/Runtime/Build 验证。
- Public API baseline 与最终决策一致。
- 模块文档中的测试类型真实存在。
- `git diff --check` 和文档链接检查通过。
- PluginSystem 相关项继续保持明确的 pending/excluded 标记，直到其独立建设开始。

## 10. 审计限制

- 本报告是“设计文档与代码的一致性审计”，不是新一轮完整并发、安全、性能或内存泄漏审计。
- 类型名覆盖扫描不能替代逐方法语义审查；本轮对高风险入口做了重点抽查，但没有声称逐个 public member 都已人工证明。
- 本机重跑非 PluginSystem 测试时，`global.json` 要求 .NET SDK `10.0.300`，而环境只安装了 `10.0.113` 与 `9.0.319`；测试未进入编译阶段。因此本报告只引用仓库内已有测试源码、测试矩阵和历史验证证据，不声称已在本机重新执行通过。
- 历史 RC 报告按当时快照保留，不应为了与当前代码一致而覆写历史结论。
- PluginSystem 被明确排除，因此任何 plugin integration 状态均不能从本报告获得发布背书。
