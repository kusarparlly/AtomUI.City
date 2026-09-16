# AtomUI.City.Routing API Contracts

## Stability 基线

本模块当前全部 public 类型和成员均为 `Preview`。`PublicAPI.Unshipped.txt` 只冻结待审签名，不表示 `Stable`；每个类型升为稳定状态时必须在对应 API Card 中明确记录并完成兼容性 review。

## API Family

| Family | Public contracts | 1.0 behavior |
| --- | --- | --- |
| Definition | `RouteTemplate`, route attributes, `RouteReference<T>` | immutable parse/bind; invalid declaration fails deterministically |
| Graph | `RouteDescriptor`, `RouteGraphSnapshot`, `RouteRegistry` | validate candidate then atomically publish; old snapshot remains readable |
| Navigation | `IRouter`, `NavigationScope`, `NavigationResult` | serialized transaction; failure/cancellation never commits |
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

## Graph Validation

Graph build rejects null descriptors, duplicate ids/index routes/extension points, missing or circular parent, unknown route kinds, invalid route-kind combinations, missing/circular/non-navigable redirects, invalid versions, empty/null/mismatched contributions, every missing/invalid ExtensionPoint mount, and behavior types that are not concrete closed implementations of their required contract. Template conflicts are evaluated by effective full template plus outlet, including equivalent flat and parent-composed paths. Any number of policy-bearing candidates may coexist with at most one unconditional fallback.

## Public API Cards

| Card | Types | Ownership / mutation rule |
| --- | --- | --- |
| Definitions | route attributes, `RouteReference`, `RouteReference<T>`, `RouteExtensionPoint` | declarations are generated at compile time; references are immutable values |
| Template | `RouteTemplate`, `RouteTemplateSegment`, `RouteMatcher`, `RouteMatch` | immutable structural parsing/matching; no service resolution or policy execution |
| Graph | `RouteDescriptor`, `RouteMetadataDescriptor`, `ViewModelTargetDescriptor`, `RouteGraphSnapshot`, graph enums/error | immutable copied values; graph transformations return a new version |
| Navigation | `IRouter`, `NavigationScope`, `NavigationTarget`, `NavigationOptions`, `NavigationResult`, `NavigationSnapshot`, navigation enums/error | scope owns mutable session; targets/results/snapshots are copied read models |
| Pipeline | guard/match/resolver/middleware interfaces, contexts and result types | application DI owns components; Routing orders and invokes them outside internal locks |
| Dynamic | `IRouteGraphProvider`, `IRouteRegistry`, `RouteRegistry`, `RouteContribution`, `RouteContributionLease` | Registry publishes graph; contributor owns and disposes lease |
| Hosting | `RoutingModule`, `AddRouting`, `RoutingDiagnosticIds` | Registry/provider singleton; router/scope scoped; diagnostics observational |

Every public type in `src/AtomUI.City.Routing` belongs to one card above. Adding or changing a public member requires updates to this file, [compatibility.md](compatibility.md), [features.md](features.md), and [testing.md](testing.md).

## Ownership and Dispose

- Descriptor, graph, snapshot, result and match objects stay readable indefinitely.
- `NavigationScope` rejects mutation after Dispose.
- Lease Dispose is idempotent; a failed revoke resets the lease so dependency order can be corrected and retried.
- Journal never stores ViewModel, provider, delegate, descriptor, or arbitrary resolver object. Only route identity, string parameters, metadata and safe scalar resolved values are retained; resolver execution is skipped only when the complete resolved-data set was safely retained.
