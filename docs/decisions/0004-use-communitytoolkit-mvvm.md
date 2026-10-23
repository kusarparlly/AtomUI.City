# 0004 MVVM 基础使用 CommunityToolkit.Mvvm

状态：Accepted（公开 ViewModel 基类与 ObservableValidator 部分已由 ADR 0017 替代）
日期：2026-06-11

## 背景

AtomUI.City 需要提供 ViewModel、属性通知、命令和验证基础能力。重复实现这些成熟能力会增加维护成本，并让开发者远离 .NET MVVM 生态习惯。

## 决策

`AtomUI.City.Mvvm` 使用 CommunityToolkit.Mvvm 作为默认 MVVM 基础设施。

框架复用：

- `ObservableObject`
- `ObservableValidator`
- `IRelayCommand`
- `IAsyncRelayCommand`
- `RelayCommand`
- `AsyncRelayCommand`
- CommunityToolkit.Mvvm source generators

## 影响

正向影响：

- 降低样板代码。
- 命令和属性通知符合 .NET MVVM 生态习惯。
- 避免引入 `CityCommand`、`CityViewModel` 这类重复命名。

约束：

- CommunityToolkit.Mvvm 不进入 Core。
- EventBus 不使用 WeakReferenceMessenger 作为默认底层。
- ReactiveUI 可作为可选适配，不作为默认 ViewModel 基类。

## 执行约束

- MVVM 设计见 `docs/modules/mvvm/detailed-design.md`。
- 命令设计见 `docs/modules/mvvm/commands.md`。
- 新增 MVVM 公共 API 前必须先更新文档和测试矩阵。

## 后续修订

[ADR 0017](0017-aot-first-generated-viewmodel-validation.md) 为 NativeAOT 冻结了新的 ViewModel 验证边界：City 自有 `ViewModelBase` / `ValidatingViewModelBase`，不再公开继承 `ObservableObject` 或 `ObservableValidator`。本 ADR 对 CommunityToolkit command 接口、实现和其他 source generator 能力的采用继续有效。
