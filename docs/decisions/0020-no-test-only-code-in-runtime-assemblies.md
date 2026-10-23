# 0020 No Test-only Code in Runtime Assemblies

状态：Accepted
日期：2026-10-23

## 背景

审计时，`AtomUI.City.Core.Modularity.ModuleRegistry` 包含 `CreateForTesting(IReadOnlyList<Type>, ...)`。该方法只被 `AtomUI.City.Core.Tests` 调用，却编译进发货的 `AtomUI.City.Core.dll`，并通过 `Activator.CreateInstance(Type)` 产生 IL2067。它不属于 Core 生产模块创建路径，正式路径已经使用生成的强类型 factory。

`internal` 只限制源代码调用权限，不会把方法排除出生产程序集，也不会让 AOT/trimming analyzer 忽略方法体。用 suppression 或 DAM 把测试专属反射留在发货程序集，会掩盖而不是消除边界错误。

## 决策

1. City 的 runtime `src` 项目不得包含仅供测试使用、没有生产合同的 factory、branch、backdoor 或反射辅助代码。
2. 从 Core 生产代码删除 `ModuleRegistry.CreateForTesting`。等价便利逻辑移动到 `tests/AtomUI.City.Core.Tests`，由测试项目所有。
3. Core 已通过 `InternalsVisibleTo("AtomUI.City.Core.Tests")` 暴露必要 internal seam；测试辅助工厂在测试项目内组合 `ModuleDescriptorFactory`、`ModuleRegistration`、`ModuleGraphValidator` 和 `ModuleRegistry.Create`。
4. 不为 `CreateForTesting` 添加 `RequiresUnreferencedCode`、`DynamicallyAccessedMembers` 或 warning suppression，也不保留兼容转发方法。
5. 生产可测试性能力只有在它本身是明确的运行时合同（例如抽象时钟、调度器或注入接口）时才能进入 runtime assembly；“测试更方便”本身不是生产合同。
6. `ForTesting`、`TestOnly` 以及只被 friend test assembly 调用的 runtime symbol 必须在工程审查中作为 test-code leakage 候选检查；发现后要么迁移到测试项目，要么用 Feature/API 合同证明其生产职责。

## 影响

- Core public API 和生产行为不变；删除的是 internal test hook。
- 现有 Core 测试调用点改用测试项目内的辅助工厂。
- 测试项目可以保留反射便利逻辑，因为它不进入开发者应用或 Core NuGet 的运行时闭包。
- Core runtime assembly 消除该 IL2067 来源，Strict AOT 不需要为测试便利代码设置例外。

## 后续约束

- 迁移全部 `ModuleRegistry.CreateForTesting` 调用，并执行 Core 全量测试。
- 扫描整个 `src` 的 `ForTesting`、`TestOnly` 和等价 friend-only helper；当前已知命中只有 `ModuleRegistry.CreateForTesting`。
- 对 Core 执行 strict AOT analyzer，要求该问题对应的 IL2067 消失且无 suppression。
- 继续执行 Core 双 TFM NativeAOT publish/run，确认 generated module factory 生产路径不受影响。

## 实施状态

已于 2026-10-23 完成：`CreateForTesting` 已从 Core runtime assembly 删除，等价反射便利逻辑迁移为 `AtomUI.City.Core.Tests` 所有的 `ModuleRegistryTestFactory`。Core 全量测试 `269/269` 通过；`net10.0` 与 `net8.0` strict AOT/trim build 零 IL 警告；重新打包后的两个 Core runtime DLL 均不含测试工厂；Core MVP 双 TFM NativeAOT 发布和完整运行矩阵通过。
