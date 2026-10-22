# AtomUI.City.Routing Testing

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-ROUTING-001 | Unit/Generator | RouteTemplateTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests; RouteDefinitionAttributeTests; AtomUICityIncrementalGeneratorRoutingTests | 模板、constraint、typed member 与 generated reference。 | 非法模板、generic/manual/multi-attribute 声明。 | Verified |
| AUC-ROUTING-002 | Unit/Contract | RouteGraphAndMatcherTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests | immutable graph、identity、conflict、cycle 和 version。 | null、冲突、parent/redirect cycle 和不可导航目标。 | Verified |
| AUC-ROUTING-003 | Unit/Contract | RouteTemplateTests; NavigationAdvancedContractTests; RoutingAuditRegressionTests | match priority、named outlet、typed binding 和 URI 边界。 | 编码、参数和 matcher boundary 失败。 | Verified |
| AUC-ROUTING-004 | Unit/RuntimeLifecycle | NavigationScopeTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests | 无部分提交、并发策略、取消、reentrancy 和 dispose/admission。 | detached next、timeout、stale context 和竞争。 | Verified |
| AUC-ROUTING-005 | Unit/Contract | RouteGuardTests; RoutingAuditRegressionTests; RoutingFinalFreezeTests | guard hierarchy、policy、redirect、loop 和取消。 | fallback 顺序、循环和 redirect failure。 | Verified |
| AUC-ROUTING-006 | Unit/Contract | RouteGraphAndMatcherTests; RoutingAssemblyTests | target metadata、无 ViewModel construction 和无 Presentation 引用。 | 类型边界或程序集依赖泄漏。 | Verified |
| AUC-ROUTING-007 | Unit/RuntimeLifecycle | RouteRegistryTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests | 原子 add/remove、ownership、contribution、mount、resolver 和 lease。 | 冲突、空 contribution、candidate failure 和 retry。 | Verified |
| AUC-ROUTING-008 | Unit/RuntimeLifecycle | NavigationScopeTests; NavigationAdvancedContractTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests | journal 行为、rollback、capacity、restore 和 redirect refresh。 | 缺失 route、reuse、取消和 resolver rerun。 | Verified |
| AUC-ROUTING-009 | Unit/Contract | RouteResolverAndDiagnosticsTests; RoutingAuditRegressionTests | resolver hierarchy、data、NotFound、redirect、取消和归因。 | duplicate key、service resolution 和 failure。 | Verified |
| AUC-ROUTING-010 | Unit/RuntimeLifecycle | RouteMiddlewareTests; RoutingAuditRegressionTests; RoutingFreezeAuditTests; RoutingFinalFreezeTests | nesting、short circuit、post-next、next ownership 和 failure。 | repeated/detached/late next、null/foreign result。 | Verified |
| AUC-ROUTING-011 | Unit/FrameworkIntegration | RouteRegistryTests; RouteResolverAndDiagnosticsTests; RoutingAuditRegressionTests | AddRouting 聚合、DI lifetime、graph version 和 diagnostics。 | AUCRT007 分类和 broken sink。 | Verified |
| AUC-ROUTING-012 | Contract | RoutingTestHostTests | Testing host 委托 production matcher/navigation。 | 测试替身语义漂移。 | Verified |
| AUC-ROUTING-013 | Unit/FrameworkIntegration | NavigationCommitReceiptTests; RoutePresentationCoordinatorTests | receipt、generation、single-use restore、journal checkpoint 和 conditional recovery。 | foreign/stale receipt、取消、reentrancy、dispose 和 diagnostics。 | Release Verified |

## Required Suites

- `tests/AtomUI.City.Routing.Tests`: production runtime contracts.
- `tests/AtomUI.City.Generators.Tests`: metadata, manifest, diagnostics, deterministic generated source.
- `tests/AtomUI.City.Testing.Tests`: test-host parity.
- Release builds for every target framework with zero warnings.

Tests involving Source Generator compilations run without class-level parallelism because their metadata reference sets are built from process-loaded assemblies; this removes cross-test dynamic-assembly pollution.

`RoutingAuditRegressionTests`, `RoutingFreezeAuditTests` and `RoutingFinalFreezeTests` directly lock the repair contracts found by repeated four-round audits: delayed commit, per-layer detached/late/repeated middleware next, result correlation, static registration aggregation, full resolver data, pre-cancel isolation, token-correlated cancellation, stale AsyncLocal context, disposal admission, journal rollback/redirect refresh, redirect context/identity, policy fallback order, named outlets, contribution ownership, graph validation/identity and diagnostic attribution/versioning. Generator tests directly cover inherited/indexer/write-only members, ambiguous members, invalid identity/default diagnostics, route-kind validation, open generics, handwritten implementations, multiple definition attributes, stable extension-point references, effective path conflicts, regex boundaries, escaped identifiers, partial deduplication, policy candidates, semantic signatures and LF output.

No UI/headless Avalonia process is required because Routing has no UI dependency. Receipt-based recovery integration is additionally tested through `RoutePresentationCoordinatorTests` and the isolated Presentation package consumer.

## Public API 合同覆盖

- `RouteMapAttribute`、六种 framework route-definition attribute、四种 behavior-list attribute 以及 `QueryAttribute`/`FragmentAttribute` 必须由 `RouteDefinitionAttributeTests` 和 Generator routing tests 覆盖 AttributeUsage、默认值、method shape、类型合法性、组合冲突和失败时不生成输出。
- `NavigationMode`、`NavigationHistoryBehavior`、`NavigationConcurrencyPolicy`、`NavigationError`、`RouteMatchStatus` 与 `RouteTemplateSegmentKind` 的值和行为由 Navigation/Matcher/Template suites 覆盖；未知 options enum 必须返回稳定失败且不提交。
- `RouteGuardContext`、`RouteMatchPolicyContext`、`RouteResolveResult/Status` 和 middleware context/delegate/interface 的只读快照、取消、顺序、short-circuit、next 所有权和 detached/late/repeated 调用由 guard/resolver/middleware 与三轮 audit regression suites 覆盖。
- `RoutingServiceCollectionExtensions.AddRouting` 必须验证 singleton/scoped 映射、重复注册聚合、输入防御复制、无临时 provider 和非法 graph 原子失败。
- 文档与 Public API baseline 必须保留每个公开类型的精确名称；`route attributes`、`pipeline contexts` 等类型族简称不能代替 API 卡。
