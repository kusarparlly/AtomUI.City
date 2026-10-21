# AtomUI.City.Routing Testing

## Matrix

| Feature | Required assertions |
| --- | --- |
| 001 | valid/invalid templates, escaped regex parentheses/comma quantifier, constrained defaults/catch-all, inherited typed members, generator output compilation, generic/manual/multi-attribute declarations rejected without generator crash |
| 002 | immutability, null descriptor/contribution member, all graph errors, structured ordinal identity, effective full-template conflicts, policy/fallback cardinality, parent/redirect cycles and navigable redirect target, version monotonicity |
| 003 | match priority, named outlet, structural matcher boundary, read-only values, typed binding, path/query/fragment, percent encoding |
| 004 | no partial commit including middleware post-next and detached-next failure, per-layer late/repeated-next rejection, foreign middleware result rejection, timeout, token-correlated cancellation, pre-cancel isolation, all concurrency policies, newest-wins, active/stale ExecutionContext reentrancy, dispose/admission race |
| 005 | guard hierarchy, policy candidates before unconditional fallback including static redirect, static/dynamic redirect, cumulative context preservation, structural loop identity/limit, cancellation |
| 006 | target metadata and no ViewModel construction; no Presentation reference |
| 007 | atomic add/remove, generated descriptor ownership stamping, conflicting/empty contribution rejection, failed candidate isolation, extension mount, service resolver, lease retry |
| 008 | Push/Replace/Reset, Back/Forward, cancelled journal rollback, capacity, missing-route skip after contribution-id reuse, redirected current-entry refresh, complete scalar restore and non-scalar resolver rerun |
| 009 | complete hierarchy resolver order/data/failure/NotFound/duplicate key/redirect/cancellation/service-resolution attribution |
| 010 | middleware nesting/short circuit/post-next failure/repeated-next rejection/null result/failure |
| 011 | repeated AddRouting aggregation, scoped router/singleton registry, captured graph-version AUCRT fields, AUCRT007 classification, broken diagnostic sink isolation |
| 012 | Testing host delegates to production matcher/navigation behavior |
| 013 | receipt previous/current snapshots and monotonic generation, single-use restore, exact Push/Replace/Back/Forward journal checkpoint, redirected commit receipt, foreign receipt, newer commit rejection, cancellation/reentrancy/disposal and diagnostic isolation |

## Required Suites

- `tests/AtomUI.City.Routing.Tests`: production runtime contracts.
- `tests/AtomUI.City.Generators.Tests`: metadata, manifest, diagnostics, deterministic generated source.
- `tests/AtomUI.City.Testing.Tests`: test-host parity.
- Release builds for every target framework with zero warnings.

Tests involving Source Generator compilations run without class-level parallelism because their metadata reference sets are built from process-loaded assemblies; this removes cross-test dynamic-assembly pollution.

`RoutingAuditRegressionTests`, `RoutingFreezeAuditTests` and `RoutingFinalFreezeTests` directly lock the repair contracts found by repeated four-round audits: delayed commit, per-layer detached/late/repeated middleware next, result correlation, static registration aggregation, full resolver data, pre-cancel isolation, token-correlated cancellation, stale AsyncLocal context, disposal admission, journal rollback/redirect refresh, redirect context/identity, policy fallback order, named outlets, contribution ownership, graph validation/identity and diagnostic attribution/versioning. Generator tests directly cover inherited/indexer/write-only members, ambiguous members, invalid identity/default diagnostics, route-kind validation, open generics, handwritten implementations, multiple definition attributes, stable extension-point references, effective path conflicts, regex boundaries, escaped identifiers, partial deduplication, policy candidates, semantic signatures and LF output.

No UI/headless Avalonia process is required because Routing has no UI dependency. Receipt-based recovery integration is additionally tested through `RoutePresentationCoordinatorTests` and the isolated Presentation package consumer.
