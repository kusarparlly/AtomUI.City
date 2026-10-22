# AtomUI.City.Routing API Contracts

## Stability 基线

本模块当前全部 public 类型和成员均为 `Preview`。`PublicAPI.Unshipped.txt` 只冻结待审签名，不表示 `Stable`；每个类型升为稳定状态时必须在对应 API Card 中明确记录并完成兼容性 review。

## API Family

| Family | Public contracts | 1.0 behavior |
| --- | --- | --- |
| Definition | `RouteTemplate`, route attributes, `RouteReference<T>` | immutable parse/bind; invalid declaration fails deterministically |
| Graph | `RouteDescriptor`, `RouteGraphSnapshot`, `RouteRegistry` | validate candidate then atomically publish; old snapshot remains readable |
| Navigation | `IRouter`, `NavigationScope`, `NavigationResult`, `NavigationCommitReceipt`, `NavigationRestoreResult` | serialized transaction; failure/cancellation never commits; current commit may be conditionally restored |
| Pipeline | guards, match policy, resolver, middleware | DI resolved, ordered, cancellable, short-circuitable |
| Dynamic | `RouteContribution`, `RouteContributionLease` | contribution ownership, extension-point validation, idempotent/retryable revoke |
| Diagnostics | `RoutingDiagnosticIds` | AUCRT code semantics and context fields are stable |

## Method Contracts

| API | Success | Failure / cancellation | Concurrency |
| --- | --- | --- | --- |
| `RouteTemplate.Parse` | immutable parsed template | null/invalid syntax/unknown constraint/invalid constrained default throws | thread-safe |
| `RouteTemplate.TryMatch` | bool + read-only values | mismatch returns false | thread-safe |
| `RouteTemplate.TryBindParameters` | validates required/default/constraint values | returns false | thread-safe |
| `RouteMatcher.Match/MatchAll(path)` | structural match in `primary` outlet | returns NotFound/empty list | thread-safe |
| `RouteMatcher.Match/MatchAll(path, outlet)` | structural match in named outlet | null/blank input throws | thread-safe |
| `RouteGraphSnapshot.Create` | version >= 1 immutable graph | `RouteGraphException` with stable `RouteGraphError` | pure build |
| `WithContribution` / `WithoutContribution` | new higher-version snapshot | invalid graph throws, old snapshot unchanged | caller serializes publication |
| `IRouteRegistry.AddContribution` | stamps unowned descriptors, publishes non-empty graph contribution and returns lease | rejects conflicting ownership/candidate; logs AUCRT008 for graph rejection | Registry synchronized |
| `IRouter.NavigateAsync` | Success/Redirected and atomic snapshot+journal commit | returns NotFound/Rejected/Cancelled/Failed | options policy controls gate |
| `NavigateByPathAsync` | same pipeline as typed route | invalid match/binding does not commit | same gate |
| `NavigateByUriAsync` | path + decoded query + fragment | no matching route returns NotFound | same gate |
| `BackAsync` / `ForwardAsync` | navigate through journal and record the final committed route after redirects | unavailable returns `CITY-NAVIGATION-JOURNAL-NOT-AVAILABLE`; entries whose route id no longer exists are skipped | journal mutation is inside gate |
| `IRouter.TryRestoreAsync` | when receipt is still current, atomically restores its previous snapshot and exact journal/graph checkpoint | foreign, stale/superseded, reentrant and disposed scopes return structured rejected/failed results; caller cancellation before admission returns Cancelled | same scope gate; successful restore advances generation and consumes the receipt |
| `NavigationScope.Router` | returns the same scope as an `IRouter` view | never creates or owns a second router | immutable self-reference |
| `DisposeAsync` | linearizes admission with disposal, cancels gate waiters/running work, waits external code and returns shared completion | own execution-chain disposal throws; post-dispose admission returns `CITY-NAVIGATION-SCOPE-DISPOSED` | idempotent |

## Pipeline Order

- Match policy: structural specificity is compared first; for the same effective template, policy-bearing candidates run before the single unconditional fallback; first false skips that candidate.
- Middleware: parent-to-child enter, child-to-parent exit.
- Leave guard: current leaf-to-root.
- Enter guard: target root-to-leaf.
- Resolver: target root-to-leaf, declaration order.
- Resolver runs for the complete target hierarchy on every normal navigation; journal restore may use saved scalar data instead.
- Non-success guard/resolver/middleware result short-circuits before commit. Every middleware invocation owns a distinct `next` boundary: `next` may be called at most once and only before that middleware returns. Middleware success commits only after the complete outer chain exits, the terminal was reached exactly once, and the returned result belongs to the current operation/target. A started, unawaited `next` remains inside the transaction; late or repeated calls fail and cannot authorize commit.
- An `OperationCanceledException` maps to Cancelled only when the navigation token is cancelled; unrelated component cancellation exceptions map to Failed.
- Reentrant navigation on the same async chain returns `CITY-NAVIGATION-REENTRANT`.
- `RouteMatcher` is deliberately structural and never invokes async match policies; `NavigationScope` applies policies to structural candidates.

## Parameters

- Path values override same-name query values.
- URI query and fragment values are percent-decoded; fragment key is `fragment`.
- `[Query]` and `[Fragment]` members are included by generated typed binders.
- Returned parameter dictionaries are read-only copies. Redirects inherit the source operation options/outlet and cumulative parameters; parameters explicitly supplied by the newer redirect override same-name inherited values.
- Unknown `NavigationOptions` enum values return `CITY-NAVIGATION-OPTIONS-INVALID`.
- `NavigationOptions.OutletName` defaults to `primary`; blank names return `CITY-NAVIGATION-OPTIONS-INVALID`.
- `JournalCapacity < 1` retains only current entry.
- `Timeout` must be positive or `Timeout.InfiniteTimeSpan`.
- Route/parent/contribution/extension-point/outlet identities use ordinal case-sensitive comparison; route parameter names and literal path matching are case-insensitive.
- `NavigationTargetKind` contains RouteReference, Path, DeepLink and Journal; Journal is emitted internally for Back/Forward admission. `NavigationResultStatus` contains Success, Rejected, Redirected, Cancelled, Failed and NotFound.
- `NavigationResult.CommitReceipt` is non-null only for a committed Success/Redirected result issued by `NavigationScope`; factory-created or non-committed results do not carry a receipt.
- `NavigationRestoreResultStatus` contains Restored, Rejected, Cancelled and Failed. `CITY-NAVIGATION-RESTORE-SUPERSEDED` means a later commit/restore already won and must never be overwritten.

## Graph Validation

Graph build rejects null descriptors, duplicate ids/index routes/extension points, missing or circular parent, unknown route kinds, invalid route-kind combinations, missing/circular/non-navigable redirects, invalid versions, empty/null/mismatched contributions, every missing/invalid ExtensionPoint mount, and behavior types that are not concrete closed implementations of their required contract. Template conflicts are evaluated by effective full template plus outlet, including equivalent flat and parent-composed paths. Any number of policy-bearing candidates may coexist with at most one unconditional fallback.

## Public API Cards

| Card | Types | Ownership / mutation rule |
| --- | --- | --- |
| Definitions | route attributes, `RouteReference`, `RouteReference<T>`, `RouteExtensionPoint` | declarations are generated at compile time; references are immutable values |
| Template | `RouteTemplate`, `RouteTemplateSegment`, `RouteMatcher`, `RouteMatch` | immutable structural parsing/matching; no service resolution or policy execution |
| Graph | `RouteDescriptor`, `RouteMetadataDescriptor`, `ViewModelTargetDescriptor`, `RouteGraphSnapshot`, graph enums/error | immutable copied values; graph transformations return a new version |
| Navigation | `IRouter`, `NavigationScope`, `NavigationTarget`, `NavigationOptions`, `NavigationResult`, `NavigationSnapshot`, `NavigationCommitReceipt`, `NavigationRestoreResult`, navigation enums/error | scope owns mutable session and receipt checkpoints; targets/results/snapshots/receipts are read models; only issuing scope may restore |
| Pipeline | guard/match/resolver/middleware interfaces, contexts and result types | application DI owns components; Routing orders and invokes them outside internal locks |
| Dynamic | `IRouteGraphProvider`, `IRouteRegistry`, `RouteRegistry`, `RouteContribution`, `RouteContributionLease` | Registry publishes graph; contributor owns and disposes lease |
| Hosting | `RoutingModule`, `AddRouting`, `RoutingDiagnosticIds` | Registry/provider singleton; router/scope scoped; diagnostics observational |

Every public type in `src/AtomUI.City.Routing` belongs to one card above. Adding or changing a public member requires updates to this file, [compatibility.md](compatibility.md), [features.md](features.md), and [testing.md](testing.md).

## 精确 Public API 类型卡

本节为已经进入 Public API baseline、但此前只用类型族概括的类型建立可定位合同。全部类型当前为 `Preview`；精确签名、默认值、enum 值、线程/取消和失败语义受兼容性门禁保护。

### 路由声明 Attribute

所有 route-definition attribute 只在 `[RouteMap]` 标记的类型中构成 Generator 输入。Route Map 必须是 public、top-level、non-generic、static partial class；route method 必须是 public static partial、non-generic、无参数、没有手写实现，并且恰好声明一个 framework route-definition attribute。非法声明输出 Generator diagnostic，不生成不完整 route source；运行时不扫描程序集补缺。

| Type / Feature | Target and purpose | Values / defaults | Failure and generation contract | Tests |
| --- | --- | --- | --- | --- |
| `RouteMapAttribute` / 001 | class；标记静态 Route Map 的唯一编译期根；不可重复、不继承。 | 无参数、无运行时状态。 | 非 public、嵌套、泛型、非 static/partial map 由 Generator 拒绝。 | Generator routing tests |
| `RouteDefinitionAttribute` / 001 | framework route definition 的抽象 metadata base，不是第三方自定义 route-kind 扩展点。 | 保存 Kind、Template、ViewModelType；`Outlet` 默认 `primary`；Id/Parent/ExtensionPoint/Target/localization keys/ReuseKey/ActivationHint 可选。 | Generator 只识别 framework sealed derived attributes；空 identity、未知 kind 和非法 kind-field 组合必须拒绝。 | `RouteDefinitionAttributeTests`, generator routing tests |
| `RouteDefinitionKind` / 001、002 | 区分 Route、Layout、Index、Group、Redirect、ExtensionPoint。 | enum 数值和语义属于兼容面。 | 未知值在 graph/generator 接纳边界拒绝，不能映射为普通 Route。 | `RouteDefinitionAttributeTests`, `RouteGraphAndMatcherTests` |
| `RouteAttribute` / 001 | method；声明带 template 和 ViewModel target 的普通可导航 route；不可重复、不继承。 | template 非空白，ViewModel type 非空；公共 metadata 继承 base 默认值。 | Generator 校验 template、target 可访问/closed、method shape 和冲突。 | `RouteDefinitionAttributeTests`, generator routing tests |
| `LayoutRouteAttribute` / 001 | method；声明具有 ViewModel target 的 layout 节点；不可重复、不继承。 | ViewModel type 必填；自身 template 为 null；outlet 默认 `primary`。 | 缺 target、非法 parent/method shape 或不可构造 target 由 Generator/graph 拒绝。 | `RouteDefinitionAttributeTests`, generator routing tests |
| `IndexRouteAttribute` / 001 | method；声明 parent 下的 index target；不可重复、不继承。 | ViewModel type 必填；自身 template 为 null；outlet 默认 `primary`。 | 无合法 parent、重复 index、非法 target/method shape 时拒绝。 | `RouteDefinitionAttributeTests`, generator routing tests |
| `RouteGroupAttribute` / 001 | method；声明只参与层次和 template 组合、不直接导航到 ViewModel 的 group。 | template 非空白；ViewModelType 为 null。 | 非法 template、parent/cycle 或把 group 当可导航 target 时拒绝。 | `RouteDefinitionAttributeTests`, graph/generator tests |
| `RedirectRouteAttribute` / 001、005 | method；声明静态 redirect source。 | template 非空白；`Target` 通过公共 metadata 指定，ViewModelType 为 null。 | Target 缺失、未知、循环或指向不可导航节点时 Generator/graph 失败。 | `RouteDefinitionAttributeTests`, redirect tests |
| `RouteExtensionPointAttribute` / 001、007 | method；声明动态贡献允许挂载的稳定 extension-point identity。 | extensionPoint 非空白并写入 `ExtensionPoint`；无 template/ViewModel。 | 重复、空白或无效 mount/identity 由 Generator/graph/registry 拒绝。 | `RouteDefinitionAttributeTests`, `RouteRegistryTests` |
| `RouteGuardsAttribute` / 005 | method；按声明顺序提供 enter/leave guard types；不可重复、不继承。 | 构造数组防御复制并只读暴露。 | null 数组立即失败；null/open/abstract/不可访问或未实现 guard contract 的元素由 Generator 拒绝。 | Generator routing tests, `RouteGuardTests` |
| `RouteMatchPoliciesAttribute` / 005 | method；按声明顺序提供 async match policy types。 | 构造数组防御复制并只读暴露。 | 非 concrete closed `IRouteMatchPolicy`、不可访问或不可构造类型由 Generator/graph 拒绝。 | Generator routing tests, policy tests |
| `RouteResolversAttribute` / 009 | method；按声明顺序提供 resolver types。 | 构造数组防御复制并只读暴露。 | 非 concrete closed `IRouteResolver`、不可访问或不可构造类型由 Generator/graph 拒绝。 | Generator routing tests, `RouteResolverAndDiagnosticsTests` |
| `RouteMiddlewareAttribute` / 010 | method；按声明顺序提供 route middleware types。 | 构造数组防御复制并只读暴露。 | 非 concrete closed `IRouteNavigationMiddleware`、不可访问或不可构造类型由 Generator/graph 拒绝。 | Generator routing tests, `RouteMiddlewareTests` |
| `QueryAttribute` / 001、003 | property/field；把 typed parameter member 绑定到 URI query；不可重复、可继承。 | 空白 name 规范化为 null，Generator 此时使用 member name。 | 不可读/不可写、ambiguous、indexer 或不支持的 member/type 由 Generator 拒绝。 | Generator routing tests, parameter tests |
| `FragmentAttribute` / 001、003 | property/field；把 typed parameter member 绑定到 URI fragment；不可重复、可继承。 | 空白 name 规范化为 null，Generator 此时使用固定 key `fragment`。 | 非法 member/type 或 ambiguous binding 由 Generator 拒绝。 | Generator routing tests, deep-link tests |

### Navigation、历史与匹配模型

| Type / Feature | Purpose / ownership | Behavior | Failure / concurrency | Tests |
| --- | --- | --- | --- | --- |
| `NavigationMode` / 004、008 | 选择 journal commit 形态：Push 追加、Replace 替换 current、Reset 清空后建立 current。 | options 值随单次 transaction 读取，不拥有资源。 | 未知值返回 `CITY-NAVIGATION-OPTIONS-INVALID`，不提交 snapshot/journal。 | `NavigationAdvancedContractTests`, `NavigationScopeTests` |
| `NavigationHistoryBehavior` / 008 | 选择 Record、Skip 或 ReplaceCurrent 的历史策略。 | Record 按 mode 写入，Skip 不增加历史，ReplaceCurrent 替换当前 journal entry。 | 未知值在 admission validation 拒绝。 | `NavigationAdvancedContractTests` |
| `NavigationConcurrencyPolicy` / 004 | 选择 CancelPrevious、Queue、RejectIfBusy。 | scope gate 拥有排队和取消；调用方不直接操作内部 transaction。 | 未知值拒绝；Queue 按 admission 顺序，RejectIfBusy 不进入 pipeline，CancelPrevious 只取消被替代 transaction。 | `NavigationAdvancedContractTests`, `NavigationScopeTests` |
| `NavigationError` / 004、014 | `NavigationResult` 的不可变结构化 code/message/optional exception payload。 | 无释放责任，可在结果终态后读取；Exception 只作诊断上下文。 | Result factory/transaction 决定稳定 code；读取线程安全，不以 message 文案作程序分支。 | Navigation result/failure tests |
| `RouteMatchStatus` / 003 | 结构化区分 Success 与 NotFound。 | enum 值是兼容面；match model 不执行 async policy。 | 未知值不得由 matcher 产生。 | `RouteGraphAndMatcherTests` |
| `RouteTemplateSegmentKind` / 001、003 | 区分 Literal、Parameter、CatchAll segment。 | segment/template 是不可变读取模型。 | parser 拒绝未知/非法 segment 语法；CatchAll 的位置和 constraint 规则由 template contract 管理。 | `RouteTemplateTests` |

### Guard、Match、Resolver 与 Middleware

这些上下文和结果是单次 navigation transaction 的只读快照，不拥有 DI component、provider 或 scope；参数集合均为防御复制。应用组件由 DI 创建，Routing 在内部锁外调用，并传递唯一有效 cancellation token。

| Type / Feature | Purpose / lifetime | Result / next contract | Failure / concurrency | Tests |
| --- | --- | --- | --- | --- |
| `RouteGuardContext` / 005 | 向 enter/leave guard 提供 navigationId、target、route、current snapshot 和只读 parameters。 | 只在当前 guard invocation 有效；不得缓存为跨导航状态。 | null target/route/snapshot/parameters 立即失败；取消通过 guard 方法 token 表达。 | `RouteGuardTests`, parameter boundary tests |
| `RouteGuardResultStatus` / 005 | Allow、Reject、Redirect、Cancel、Failed 的稳定分类。 | 由 `RouteGuardResult` factory 建立匹配 payload。 | 未知或 status/payload 不一致的结果不得进入 commit。 | `RouteGuardTests`, final freeze tests |
| `RouteMatchPolicyContext` / 005 | 向同结构候选的 async policy 提供 navigationId、target、candidate route 和 current snapshot。 | 单次候选评估只读上下文。 | null 输入立即失败；false 跳过当前 candidate，不代表全局 NotFound 前已尝试后续候选。 | policy/fallback tests |
| `RouteResolveResult` / 009 | 报告 Success data、NotFound、Redirect、Cancelled 或 Failed。 | Success data 防御复制为 ordinal read-only dictionary；其他状态使用稳定 code/target/error payload。 | redirect target、failed code/message 必须有效；非成功短路 commit；resolver token 相关取消与普通 component failure 区分。 | `RouteResolverAndDiagnosticsTests`, audit regression tests |
| `RouteResolveResultStatus` / 009 | Success、NotFound、Redirect、Cancelled、Failed 的稳定分类。 | 与 `RouteResolveResult` factory/payload 一一对应。 | 未知状态不得被 transaction 静默当作 Success。 | Resolver tests |
| `RouteNavigationMiddlewareContext` / 010 | 向 middleware 提供 navigationId、target、selected route、current snapshot 和只读 parameters。 | 仅在当前 middleware invocation/transaction 内有效。 | null 输入失败；不得持有用于迟到提交。 | `RouteMiddlewareTests`, freeze/final-freeze tests |
| `RouteNavigationDelegate` / 010 | 表示当前 middleware 的无参数 next boundary；target/context/token 已由 transaction 闭包固定。 | 可以 `await next()` 或 `return next()`；最多调用一次，只能在 middleware 返回前取得调用所有权。 | 重复、迟到、保存后调用或 fire-and-forget 不能授权 commit；已启动下游被 transaction 收拢观察。 | `RouteMiddlewareTests`, audit/freeze tests |
| `IRouteNavigationMiddleware` / 010 | 应用实现的 navigation pipeline component。 | 可 short-circuit 返回自己的当前-operation result；调用 next 时必须遵守独立 boundary。 | foreign/null result、post-next failure、token-correlated cancellation 和 detached next 均按 pipeline 合同失败且不提交。 | `RouteMiddlewareTests`, `RoutingAuditRegressionTests`, `RoutingFinalFreezeTests` |

### DI 入口

| Type / Feature | Purpose / ownership | Lifetime | Failure / idempotency | Tests |
| --- | --- | --- | --- | --- |
| `RoutingServiceCollectionExtensions` / 011 | `AddRouting` 注册静态 descriptors、Registry/GraphProvider 和 scoped Router；由应用或 `RoutingModule` 在 Build 前调用。 | Registry/GraphProvider 为同一 singleton；`NavigationScope`/`IRouter` 为同一 scoped instance；输入 route 数组防御复制。 | 不构建临时 provider；无 routes 的重复调用幂等，有 routes 的重复调用按所有 `StaticRouteRegistration` 聚合；候选 graph 在首次 registry 解析时整体验证，非法输入不发布部分图。 | `RouteRegistryTests`, `RoutingAuditRegressionTests`, DI contract tests |

## Ownership and Dispose

- Descriptor, graph, snapshot, result and match objects stay readable indefinitely.
- A receipt stays readable indefinitely but its restore authority is conditional and normally expires after any later commit/restore; it does not keep ViewModel, View, provider or arbitrary resolver objects alive.
- `NavigationScope` rejects mutation after Dispose.
- Lease Dispose is idempotent; a failed revoke resets the lease so dependency order can be corrected and retried.
- Journal never stores ViewModel, provider, delegate, descriptor, or arbitrary resolver object. Only route identity, string parameters, metadata and safe scalar resolved values are retained; resolver execution is skipped only when the complete resolved-data set was safely retained.
