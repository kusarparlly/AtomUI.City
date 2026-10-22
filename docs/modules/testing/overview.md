# AtomUI.City.Testing

文档等级：Level 3
成熟度：Partially Implemented
执行边界：Test-only framework boundary
程序集：`AtomUI.City.Testing`
源码：`src/AtomUI.City.Testing`
测试：`tests/AtomUI.City.Testing.Tests`

## 模块定位

框架测试工具包。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- 生产项目不得引用 Testing。
- 测试不得依赖固定 Task.Delay。
- 释放、取消、unload、dispatcher 和 generated output 必须有断言。

## 模块目标

- 稳定执行边界。
- 输出可验证。
- 失败可诊断。

## 明确非目标

- 不承载业务领域能力。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

Testing 可以创建可控 Host，但生产 Host 不得依赖 Testing 包。

## 与 Data 的关系

Data 定义连接、请求管线、凭据和传输的运行时合同；Testing 通过 `AUC-TESTING-010` 提供 Fake、Recording 和 Scripted 测试实现。依赖方向固定为 `Data contracts -> Testing implementations -> test project`，生产模块不得反向引用 Testing。

## 与 PluginSystem 的关系

插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。

## 与 Testing 的关系

`tests/AtomUI.City.Testing.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

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
| [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) | Feature 到现有基线、缺口、必补测试和实现工作的追踪。 |

## 当前成熟度状态

Partially Implemented

该状态表示模块已有实现基线，但还需要按产品级合同补齐实现和测试。单个功能点状态以 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 为准。
