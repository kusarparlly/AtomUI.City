# AtomUI.City Release Notes

## 1.0.0 (unreleased candidate)

### New features

- Candidate for the first stable AtomUI.City package line for Avalonia and AtomUI desktop applications.
- Product-level contracts for Core host lifecycle, module ordering, diagnostics, DI markers, and dispatcher abstraction.
- Runtime modules for Routing, Presentation, MVVM, State, EventBus, Localization, Security, Data, and PluginSystem.
- Build and source generator integration plus implemented CLI, template, and testing baselines with local release gates.
- Implemented application, plugin, test, module, page, localization, and configuration templates; incremental generation uses create-only plans, cancellation, conflict detection, and rollback.

### Breaking changes

- At the eventual stable 1.0 release, APIs, diagnostics, generated output, template variables, package layout, and CLI JSON contracts documented as complete will become compatibility commitments.

### Fixes

- Hardened project inventory and dependency boundary gates so template payload projects are not treated as repository source projects.
- Hardened generated test templates with `AtomUI.City.Testing` references and `TestLayerNames.TemplateSmoke` metadata.
- Verified package generation, package validation, public API, documentation, CI-equivalent, platform integration, template smoke, and isolated local NuGet consumer gates for the candidate line.

### Known limitations

- The full 1.0 package family is not publishable yet: the Avalonia Desktop application template (`AUC-TEMPLATES-010`) remains incomplete.
- The isolated local package consumer currently validates Build, Core, EventBus, State, MVVM, Routing, Data, Localization, Security, and Presentation. PluginSystem, Testing, CLI, and Templates are outside that consumer's scope and retain their own gates.
- Platform integration coverage is intentionally narrow in 1.0 and currently validates the Avalonia dispatcher bridge contract.
- Templates target the current AtomUI.City package family and are not a replacement for application-specific architecture decisions.
- Plugin package compatibility is enforced through manifest and package-layout contracts; host-side dynamic unload scenarios should still be validated by applications that use dynamic plugins.

### Migration notes

- Projects evaluating the local candidate should use the version emitted by `engineering/check-local-package-consumer.ps1`; published applications must wait for an actual stable package release.
- Regenerate or review template-created test projects so they reference `AtomUI.City.Testing` and include `TestLayer` metadata.
- Re-run local release gates after upgrading package references: build, tests, docs, public API, package validation, and template smoke.

### Plugin API compatibility

- Plugin API compatibility starts only when stable `1.0` is published.
- Plugin manifests, package layout, capability declarations, dependency validation, and unload state contracts documented for 1.0 are compatibility commitments.
- Plugin packages should declare host compatibility against the 1.0 package line unless they require a newer documented contract.
