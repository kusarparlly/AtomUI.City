# AtomUI.City.Localization Lifecycle

## 生命周期范围

执行边界：Host runtime localization service。

AtomUI.City.Localization 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

Localization 1.0 没有公开状态枚举，采用可观察行为定义隐式状态：

- `LocalizationService`: Running -> Disposing -> Disposed。
- Culture/revoke mutation: Queued -> Preparing -> Committed -> PostCommitRefresh -> Completed/Failed。
- Package: Registered -> Loading -> Loaded/Cached，或 Failed/Cancelled/Revoked。

mutation 在提交前观察调用方 token；提交 descriptor/cache/state 后，bridge 与文本刷新由 service lifetime token 完成，避免部分发布。

## 生命周期流程

- SetCulture 验证 culture 并计算 fallback chain。
- Provider 懒加载 package。
- LocalizationService 查找资源。
- 可选 application-owned bridge 回调与本地文本批量刷新。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 拒绝新的 lookup、scope activation 和 mutation，取消 service-owned package load。
- 等待 mutation queue 和已捕获 load 完成，解除 Registry 事件，再释放 LocalizedText、LanguagePackage cache、Culture State 和 cancellation source。
- 释放失败记录诊断并继续释放其他资源。
- `Dispose` / `DisposeAsync` 并发调用共享同一个完成任务并等待 mutation/load 收束；当前 Localization mutation callback 内重入释放抛 `InvalidOperationException`，避免等待自身。插件 owner revoke 早于 collectible AssemblyLoadContext unload。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 语言包格式错误：跳过并诊断。
- 缺失 key：返回 fallback 或 key。
- culture 切换部分 target 失败。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
