# AtomUI.City.Presentation State And Localization Boundary

## 结论

State 和 Localization 都是 Presentation 的上游业务能力，不是 Presentation 的子系统。Presentation 1.0 不提供 State-to-Control 自动同步，也不提供任何文案或 culture bridge。

## State 边界

- 推荐路径是 State -> ViewModel property -> Avalonia Binding -> View。
- 页面临时字段可直接使用普通 ViewModel 属性，不强制使用 City State。
- Presentation 不按 State key 寻找控件，不订阅全部 State，也不自动发布 VisualTree 事件到 State。
- 需要指定 dispatcher 的 State subscription 时，由业务 ViewModel 显式配置；Presentation 不发明 `DispatchPolicy.UiThread`。

## Localization 边界

- 业务代码或 ViewModel 通过 `ILocalizationService`、localized value 或应用自有资源层取得文案。
- 菜单、窗口标题、验证消息、错误消息、Route 标题和 Interaction 文案都由业务层提供最终值。
- culture 切换、revision、fallback、language package、缺失 key 诊断属于 Localization。
- 应用可以把 Localization 结果暴露为 ViewModel 属性，由 Avalonia Binding 刷新 UI；该适配不属于 Presentation 主包。
- Presentation 的通用 resource dictionary revoke 只处理 owner-bound UI 资源释放，不读取 culture/package，不调用 `GetText`。

## 禁止公开面

Presentation 不得新增 `PresentationLocalizationBridge`、`IPresentationCultureApplier`、`Localized*Binding`、`ILocalized*Target`、文案 descriptor 或 Localization project reference。若未来需要官方集成，必须放入独立可选适配包并单独设计 Feature/API/兼容性合同。

## 测试门禁

- 源码和测试对 `AtomUI.City.Localization` 的直接引用数必须为 0。
- Presentation 项目引用中不得出现 Localization。
- Localization 自身负责 culture/fallback/revision 测试；Presentation 只验证 generic resource revoke 与 ViewModel/Avalonia Binding 路径不被拦截。
