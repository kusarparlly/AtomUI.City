# AtomUI.City.Localization Avalonia And AtomUI Integration Boundary

Localization Core 不引用 Avalonia、AtomUI 或 `AtomUI.City.Presentation`。Presentation 主模块也不实现 culture、文案、fallback 或 localized binding。

需要 UI 资源切换的应用有两条路径：

1. ViewModel 持有 `ILocalizedText`/可观察字符串，Avalonia Binding 自动更新控件。
2. 应用或独立可选适配包实现 Localization 自己的 `IPresentationLocalizationBridge`，在 UI dispatcher 更新 Application/Window/Route resource dictionary、FlowDirection 或控件库 culture。

可选 bridge 的 owner、dispatcher、资源挂载位置、局部失败恢复和插件资源撤销由该适配包定义。Localization 只保证：culture commit 后调用 bridge；bridge 失败不回滚 CultureState；继续刷新本地 text handle；错误进入 Localization Result/diagnostics。

AtomUI 集成必须是独立包，不能让 Presentation 或 Localization 主包增加 AtomUI 强依赖。插件资源撤销顺序由应用的 Plugin/Localization/Presentation adapters 协调，跨插件 contract 类型必须来自 Host 共享程序集。
