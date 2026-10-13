# AtomUI.City.Mvvm

文档等级：Level 3
成熟度：Verified baseline / Implemented expansion pending release validation
执行边界：ViewModel programming model
程序集：`AtomUI.City.Mvvm`
源码：`src/AtomUI.City.Mvvm`
测试：`tests/AtomUI.City.Mvvm.Tests`

## 模块定位

ViewModel 编程模型，提供 activation、command、operation、interaction 和 validation contract。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- MVVM 不依赖具体 View、Avalonia visual 或 Presentation 实现类型。
- Interaction 只表达请求，UI 展示由 Presentation handler 完成。
- Command 状态支持完成、取消、异常和并发拒绝结果。
- ViewModel 生命周期必须能被 Routing 和 Presentation 组合使用。

## 模块目标

- 建立推荐 MVVM 基类和生命周期接口。
- 复用 CommunityToolkit.Mvvm。

## 明确非目标

- 不创建 View。
- 不匹配路由。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

AtomUI.City.Mvvm 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 与 PluginSystem 的关系

插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。

## 与 Testing 的关系

`tests/AtomUI.City.Mvvm.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

## 文档索引

| 文档 | 用途 |
| --- | --- |
| [architecture.md](architecture.md) | 核心不变量、对象模型、状态机、流程、失败矩阵、性能边界。 |
| [features.md](features.md) | Feature ID、实现合同、public contract、失败行为和验收标准。 |
| [api-contracts.md](api-contracts.md) | API family、关键方法、参数/返回/异常/取消/并发/Dispose 后行为。 |
| [lifecycle.md](lifecycle.md) | 模块特有生命周期、Host shutdown、插件动态变更和失败处理。 |
| [threading.md](threading.md) | 线程边界、UI dispatcher、后台任务、并发冲突和死锁规避。 |
| [diagnostics.md](diagnostics.md) | 现有诊断码、产品级目标诊断、上下文字段和测试断言。 |
| [testing.md](testing.md) | 具体测试矩阵、必须断言的行为、测试类型和缺口处理。 |
| [compatibility.md](compatibility.md) | public API、配置、manifest、snapshot、generated output、CLI envelope 和包布局兼容。 |
| [integration.md](integration.md) | 跨模块依赖方向、生命周期、线程和失败行为。 |
| [activation.md](activation.md) | Activation 设计：激活模型、ActivationScope、State/EventBus 订阅绑定。 |
| [commands.md](commands.md) | Command 设计：OperationScope、执行状态、取消策略、组合命令。 |
| [structured-concurrency-and-commands-v2.md](structured-concurrency-and-commands-v2.md) | 已实现的 ActivationScope 结构化任务和 Command V2 合同。 |
| [interactions.md](interactions.md) | Interaction 设计：请求模型、handler 生命周期、结果模型。 |
| [validation.md](validation.md) | Validation 设计：ValidationScope、结果模型、Command 联动。 |
| [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) | Feature 到现有基线、缺口、必补测试和实现工作的追踪。 |

## 当前成熟度状态

Verified baseline / Implemented expansion pending release validation

`AUC-MVVM-001` 至 `006` 维持既有 Verified 基线；`AUC-MVVM-007` 至 `010` 已实现并通过模块测试，但在全套发布门禁完成前保持 `Implemented`，不能报告为 `Verified`。单个功能点状态以 [features.md](features.md) 和 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 为准。
