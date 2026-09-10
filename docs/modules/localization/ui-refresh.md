# AtomUI.City.Localization UI Refresh

## 边界

Localization 负责 culture 状态、资源查找、fallback、格式化文本和 `ILocalizedText` 刷新。它不引用 Avalonia/AtomUI，也不要求 Presentation 实现文案 binding。

## 标准刷新路径

```text
language packages loaded
-> CultureState committed
-> refresh all live ILocalizedText handles
-> application ViewModel receives Changed/value
-> Avalonia Binding updates the View
```

ViewModel 可以直接暴露 `ILocalizedText` 或把其 value 投影为可观察属性。Window title、菜单、Route metadata、Command、Validation、Dialog、Data/Security error 和 Notification 的最终文案都由 owning application/module 持有；Presentation 不维护这些文案 descriptor。

## 可选应用 Bridge

`IPresentationLocalizationBridge` 是 Localization 程序集定义的可选应用回调，用于需要在 culture commit 后执行资源切换的应用。默认实现是 no-op；实现者只能是应用组合层或独立 Localization-Avalonia/AtomUI 适配包，不是 `AtomUI.City.Presentation` 主模块。

bridge 失败不回滚已提交 CultureState，本地 `ILocalizedText` 仍继续刷新。UI 实现必须自行 dispatch；Localization 不承诺 callback 位于 UI 线程。

## 生命周期

- localized handle 的订阅随 ViewModel/Activation owner 释放。
- 插件 package revoke 后 lookup 不得继续命中插件资源，仍存活 handle 按 fallback/missing 合同刷新。
- XAML markup extension 和强类型 accessor 不属于 1.0，除非另有 Feature ID 和实现证据。

## 测试

覆盖 culture commit、批量 handle refresh、missing/fallback、bridge no-op/failure/cancel/reentrancy、owner dispose 和插件 package revoke。Avalonia VisualTree 测试属于独立适配包或应用测试。
