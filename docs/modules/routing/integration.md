# AtomUI.City.Routing Integration

## Dependency Direction

```text
Core <- Routing <- application
Core <- PluginSystem
Routing -> Core diagnostics/modularity/DI abstractions
Presentation -> Routing
MVVM -> consumes activation decisions independently
```

Routing does not reference Presentation, Avalonia, PluginSystem, Data, Localization or Security.

Routing 也不引用 State；resolver/guard 可以通过应用 DI 使用 State，但该关系属于应用组合而非 Routing 程序集依赖。

## Core

`RoutingModule.ConfigureServices` calls `AddRouting`. Registry/provider are singleton; `NavigationScope` and `IRouter` are scoped. Applications pass generated descriptors to `AddRouting` or pass them directly to `RouteContribution`; contribution construction stamps immutable owned copies. 多次 `AddRouting` 的非空静态 descriptor 会在 DI provider 创建 Registry 时聚合，因此 Module 的空基础注册不会覆盖或吞掉应用随后注册的 routes。

## Presentation

Presentation consumes a successful Routing target and owns ViewModel creation, activation, View resolution, dispatcher and VisualTree commit. Low-level `IRouteOutlet.CommitAsync` does not modify Router state。可选的 `IRoutePresentationCoordinator` 在 Presentation 层使用 Routing 的公开 `NavigationCommitReceipt` / `TryRestoreAsync` 合约：Router 先提交，UI 失败时仅在该提交仍为 current 的条件下恢复。route 到 View/ViewModel/lease 的业务映射仍由应用提供。

## PluginSystem

PluginSystem creates `RouteContribution`, supplies its service resolver, owns the returned lease, drains active work, revokes routes, and only then disposes the plugin provider/ALC. An old graph snapshot remains readable, but it does not lease a contribution service resolver; direct Registry users must therefore drain owner work before revoke as well. Routing removes its resolver reference when revoke succeeds.

## Data / Security / Localization

Resolvers may call injected Data services. Guards may call injected Security services. Route metadata stores localization keys only. Failures are represented by guard/resolver results; Routing does not implement these modules' policies.
