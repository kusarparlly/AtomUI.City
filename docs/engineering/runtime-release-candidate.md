# Runtime Local Release Candidate

Date: 2026-09-13  
Status: local candidate verified; no remote NuGet publication performed

## Scope

The isolated consumer validates these packages as one application dependency graph:

- `AtomUI.City.Build`
- `AtomUI.City.Core`
- `AtomUI.City.EventBus`
- `AtomUI.City.State`
- `AtomUI.City.Mvvm`
- `AtomUI.City.Routing`
- `AtomUI.City.Data`
- `AtomUI.City.Localization`
- `AtomUI.City.Security`
- `AtomUI.City.Presentation`

`AtomUI.City.PluginSystem`, `AtomUI.City.Testing`, `AtomUI.City.Cli`, and `AtomUI.City.Templates` are not part of this isolated consumer. This scope does not change their module Feature status. The full 1.0 package family remains blocked by `AUC-CLI-007` and `AUC-TEMPLATES-006` through `AUC-TEMPLATES-010`.

## Package Gate

Run:

```powershell
./engineering/check-local-package-consumer.ps1 -Configuration Release
```

The gate:

1. Packs every scoped package as the unique local version `1.0.0-local-gate`.
2. Creates a consumer with package references and rejects any `ProjectReference`.
3. Restores into an isolated package cache and verifies every City package originated from the current local feed.
4. Builds the consumer for `net8.0` and `net10.0` with warnings treated as errors.
5. Publishes and runs the `net10.0` target available on the validation machine.
6. Starts and stops a real City host and exercises EventBus, State, MVVM, Routing, Data, Localization, Security, and Presentation through package assets.

Successful execution ends with:

```text
RELEASE_PACKAGE_CONSUMER_OK host=stopped eventbus=delivered state=written mvvm=executed routing=navigated data=completed localization=resolved security=switched presentation=committed
```

The script never invokes `dotnet nuget push` and never publishes to a remote feed.

## Verification Evidence

| Gate | Result |
| --- | --- |
| Release solution build | Passed with 0 warnings and 0 errors. |
| Full solution tests | 2,088/2,088 passed across 15 test projects. |
| PluginSystem Windows lifecycle tests | 74/74 passed, including deterministic unload and cleanup paths. |
| EventBus Native AOT | `net8.0` and `net10.0` checks passed. |
| Isolated local package consumer | Both target frameworks built; the `net10.0` packaged application ran successfully. |
| Two-hour DesktopDogfood soak | Exit code 0 after 7,212,861 ms and 24,652,659 actions; all resources released. |
| Soak resource evaluation | Passed; managed heap slope 3.498 MiB/hour, no evaluation failures, no pending subscriptions/windows/outlets at shutdown. |

The two-hour run predates only PluginSystem test hardening, the package-consumer gate, and documentation changes. No runtime module exercised by DesktopDogfood changed after that run, so the soak evidence remains applicable to this runtime candidate.

## Release Decision

The scoped runtime packages are suitable for local consumer evaluation and continued Dogfood use. This is not approval to publish the full AtomUI.City 1.0 package family: the six open CLI/Templates Features must be implemented and all final gates rerun before a stable tag or remote NuGet publication.
