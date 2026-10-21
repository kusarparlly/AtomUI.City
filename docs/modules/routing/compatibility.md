# AtomUI.City.Routing Compatibility

## Stable 1.0 Surface

Breaking changes include:

- public type/member removal, rename, default value or nullability change;
- route template parsing, built-in constraint or match-priority changes;
- default/named outlet selection and `NavigationOptions.OutletName` changes;
- generated route method, manifest type/name/content or AUCGEN diagnostic changes;
- `RouteGraphError`, `NavigationResultStatus`, `CITY-NAVIGATION-*` or `AUCRT*` semantic changes;
- guard/resolver/middleware order changes;
- graph version, contribution lease, journal or Dispose behavior changes;
- commit receipt owner/generation、条件恢复、superseded rejection 或 checkpoint 完整性变化；
- DI lifetime changes: Registry singleton, Router/NavigationScope scoped;
- changing Routing to reference Presentation or Avalonia.

## Generated Output

Generation is deterministic, uses LF line endings and is AOT-oriented. Static route discovery is compile-time only. A route map must be a non-generic top-level public static partial class; route methods must be non-generic public static partial parameterless definitions without handwritten implementations, with exactly one definition attribute and the required return type. Generated ViewModel and behavior references must be closed types.

## Supported Constraints

1.0 supports `bool`, `datetime`, `decimal`, `double`, `float`, `guid`, `int`, `long`, `alpha`, `min`, `max`, `range`, `length`, `minlength`, `maxlength`, and `regex`. Unknown/custom constraints are rejected.

## Evolution

New constraints, route kinds, middleware stages, journal payload kinds, receipt/restore semantics, diagnostics or plugin policies require a new Feature ID and compatibility review. Documentation cannot claim a capability before API and tests exist.

## Pre-1.0 Freeze Decisions

The final Routing audits removed never-produced `NavigationResultStatus.StaleRouteGraph` / `ContributionRevoked` and unused `NavigationTargetKind.Redirect`, made the journal-target factory non-public, and internalized the externally ineffective `NavigationOptions.RestoreState` marker. They also froze policy-candidate-before-fallback ordering, effective full-template conflict validation, cumulative redirect context, per-middleware `next` ownership, committed-route journal identity and disposal/admission linearization. These decisions are part of the frozen 1.0 behavior in `api-contracts.md`.

ADR 0016 adds Preview `NavigationCommitReceipt` and `TryRestoreAsync`. The receipt is deliberately conditional and scope-bound: changing it into unconditional rewind, replaying user pipeline code during restore, or allowing stale receipts to overwrite newer commits would be a breaking semantic change. Routing remains independent of Presentation/Avalonia.
