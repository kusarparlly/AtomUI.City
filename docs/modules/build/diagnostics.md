# AtomUI.City.Build Diagnostics

## 诊断原则

- 诊断码稳定，不能复用。
- 文档必须区分“当前源码已有诊断码”和“产品级目标诊断”。
- message 可以优化，但 code 含义不能漂移。
- 重要失败路径必须有诊断、Result 或声明异常。
- 测试必须断言 code 和至少一个定位字段。

## 当前源码诊断码

| Code | Name | Meaning |
| --- | --- | --- |
| `AUCBLD0001` | InvalidOutputPath | 构建输出路径为空、越界或无法规范化。 |
| `AUCBLD0002` | InvalidBuildProperty | Build Property 值不在公开合同允许范围内。 |
| `AUCBLD0101` | ManifestGenerationFailed | package/application manifest 无法生成。 |
| `AUCBLD0102` | ManifestValidationFailed | manifest 内容或 Item metadata 不合法。 |
| `AUCBLD0201` | InvalidPluginPackageLayout | 插件包缺少必需资产或存在路径逃逸。 |
| `AUCBLD0202` | MultiplePluginMainAssemblies | 插件包主程序集布局不唯一。 |
| `AUCBLD0301` | DynamicPluginNotAotCompatible | Strict AOT 与动态插件配置冲突。 |
| `AUCBLD0401` | InvalidApplicationPublishLayout | 应用发布布局或 application manifest 无效。 |

## 产品级必须诊断的失败

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。

## 上下文字段

推荐字段：`operationId`、`scopeId`、`module`、`pluginId`、`routeId`、`stateKey`、`eventType`、`handlerType`、`assembly`、`path`、`featureId`、`threadId`、`attempt`、`transportKind`。

## 诊断缺口处理

- 如果当前源码没有对应诊断码，必须在 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 中标记为 product gap。
- 新增诊断码必须同时更新源码、本文档、测试矩阵和 compatibility。
- 已存在诊断码不能因为重构改变语义。

## 测试门禁

`tests/AtomUI.City.Build.Tests` 必须断言当前源码诊断码；产品级目标诊断补齐后必须增加对应测试。
