# AtomUI.City.Presentation Resources And Plugins

## 通用资源

`IPresentationResourceRegistry` 管理带 `pluginId/contributionId` owner 的 UI resource contribution。lease dispose、按 plugin revoke 和按 contribution revoke 必须幂等；单项 dispose 失败记录 `AUCPRS030` 并继续同批其他资源。

`IPresentationResourceDictionaryTarget` 只定义通用 resource dictionary 撤销。`PresentationResourceDictionaryRevoker` 在 UI dispatcher 按注册顺序调用全部 target，聚合异常为 `PresentationResourceDictionaryRevokeResult`。该合同没有 culture、language package 或文案语义。

## 插件 UI contribution

插件可以贡献：

- 带 owner 的 ViewDescriptor 覆盖。
- Interaction handler。
- 通用 Presentation resource/resource dictionary。
- active View lease。

所有 contribution 必须可撤销，跨插件 contract 类型来自 Host 共享程序集。Presentation 主包不加载程序集、不创建插件 ALC，也不决定插件信任策略。

## 卸载顺序

```text
stop new plugin contributions
-> close active plugin/contribution views
-> reject unload if active views remain
-> revoke interaction handlers
-> revoke view descriptors
-> revoke resource dictionaries on UI dispatcher
-> dispose generic resource leases
-> aggregate diagnostics/result
```

单个 revoke 失败不得跳过后续类别；重复 cleanup 必须幂等。

## 生成边界

Presentation 1.0 generator 只生成 View registrar，不生成 Interaction 或 resource descriptor。插件资源清单属于 PluginSystem 或未来独立适配层。

## 测试要求

覆盖 owner 精确撤销、覆盖栈恢复、active view 阻断、清理顺序、重复 unload、局部失败继续、UI dispatcher 和无残留强引用。
