# AtomUI.City.Presentation Compatibility

## 1.0 稳定面

- `PublicAPI.Shipped.txt` 中的公开类型和成员签名；新签名先进入 Unshipped，删除或改变 shipped 签名必须按 breaking change 处理。
- public API、enum 值、attribute/options 默认值、diagnostic code、generated View registrar 形状。
- 主包只硬依赖 Avalonia；增加 AtomUI 或 Localization 硬依赖是 breaking change。
- `PresentationModule` 与 `AddPresentation` 的等价完整注册。
- `Attach/RegisterWindow` 主路径、Show 前注册、WindowSession/WindowScope 一一对应。
- exact View key、override owner 栈、generated registrar 稳定排序。
- ViewModel ownership、async-only lease、single-use candidate、FIFO/final commit/rollback/cancellation 语义。
- queue 默认容量 32/8、reject-newest、错误分类和 snapshot 字段。
- close origin、唯一 confirmation、并发 Close/Stop 合并和不可拒绝 shutdown。
- Visual identity 过滤、真实事件规则和不自动写 State/EventBus。
- Validation 完全可选且应用拥有规则、文案、回调和样式。
- plugin UI owner/revoke/unload 顺序。
- NuGet 包的 net8.0/net10.0 资产以及 Avalonia/Core/Mvvm/Routing/Security/State 依赖边；AtomUI、Localization、fixture、test 和 benchmark 不得进入主包。

## 不属于 Presentation 兼容面

- Localization 的 culture、revision、fallback、文案 key/value 和 language package。
- 应用自有 Dialog/Toast/Validation UI、Routing-Presentation adapter 业务编排。
- roadmap 候选、测试替身内部结构、diagnostic message 文案。
- Linux/macOS runtime 行为；1.0 仅承诺 build-only experimental。

## Retired before 1.0

原 `PresentationLocalizationBridge`、`Localized*Binding/Target`、culture applier、flow direction applier 以及 AUCPRS018/019 active 语义在 1.0 冻结前移除。类型不构成 1.0 compatibility，但诊断编号保留且不得复用。

## 演进

改变默认值、线程、ownership、取消、提交点、Result/exception、状态终态或关闭可拒绝性均为 breaking change。Deprecated API 必须记录版本、replacement、最早删除版本、迁移方法和 Analyzer diagnostic。
