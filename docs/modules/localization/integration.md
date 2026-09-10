# AtomUI.City.Localization Integration

## 集成矩阵

| Provider | Consumer | Contract | Direction/Ownership | Threading | Failure |
| --- | --- | --- | --- | --- | --- |
| Core | Localization | Host lifecycle、diagnostics | Host -> module | 不在 lock 内执行用户 callback | 启停失败可诊断 |
| State | Localization | `IStateFactory`、只读 CultureState | State -> Localization -> consumers | 遵循 State 合同 | factory 缺失时显式 fallback |
| Localization declarations | Generators | package/resource attributes、generated registrar | build-time -> application registry | 编译期确定性 | 非法声明阻断生成 |
| PluginSystem | Localization | owner-bound language package descriptor | plugin owner -> registry | load 可后台，commit 串行 | revoke 与并发 load 重验 owner |
| Localization | application/optional UI adapter | `ILocalizedText`、CultureState、可选 `IPresentationLocalizationBridge` | application owns adapter | Localization 不保证 UI 线程 | bridge 失败不回滚 culture |
| Testing | Localization | Feature/API/diagnostic contracts | test -> module | deterministic fakes | 失败阻断 Feature 完成 |

## 硬约束

- Localization 只能依赖 Core/State contracts，不引用 Avalonia、AtomUI 或 Presentation concrete type。
- `IPresentationLocalizationBridge` 名称是 Localization 的历史 public contract；实现责任属于应用组合层或独立可选 UI 适配包，不属于 `AtomUI.City.Presentation`。
- Presentation 不提供 `Localized*Binding`；标准 UI 路径是 `ILocalizedText`/ViewModel observable property -> Avalonia Binding。
- 插件 package 卸载后 lookup 不得命中；generator registrar 必须原子发布完整 manifest。

新增跨模块合同必须同步 Feature、API card、testing、compatibility 和全局进度。
