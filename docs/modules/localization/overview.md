# AtomUI.City.Localization

文档等级：Level 3
成熟度：Verified
执行边界：Host runtime localization service
程序集：`AtomUI.City.Localization`
源码：`src/AtomUI.City.Localization`
测试：`tests/AtomUI.City.Localization.Tests`

## 模块定位

桌面应用语言包、文化状态、懒加载、fallback、插件本地化和应用拥有的可选 UI 刷新通知。

## 产品级硬性约束

以下约束是本模块实现和 review 的硬门禁，违反任一条都不能标记 Feature 完成。

- 语言包按当前 culture 懒加载。
- provider 必须可撤销。
- 缺失 key 必须诊断并走 fallback。
- 插件语言包卸载后不得出现在 lookup。

## 模块目标

- 按当前 culture 懒加载语言包。
- 支持文件和独立 assembly 语言包。

## 明确非目标

- 不实现控件样式。
- 不硬编码业务翻译。

## 使用者画像

- 框架开发者：根据模块合同实现 public API、状态机、失败路径、诊断和测试。
- 应用开发者：通过 DI、扩展方法、attribute、manifest、CLI 或模板使用模块能力。
- 插件开发者：通过 Host 共享 contract、manifest 和可撤销贡献接入模块。
- 测试开发者：根据测试矩阵验证成功路径、失败路径、线程、释放和兼容性。

## 与 Host 的关系

AtomUI.City.Localization 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 与 PluginSystem 的关系

插件可以通过 manifest 或 Host 共享 contract 贡献本模块能力；所有插件来源对象必须绑定 plugin owner 并可撤销。

## 与 State、Generators 和 Presentation 的关系

- State 提供 `CultureState` 的 writable 创建能力；Localization 对外只暴露 `IReadOnlyState<CultureState>`。
- Generators 读取 Localization attributes 并生成稳定 manifest、key 常量和原子 Registry registrar。
- 应用组合层或独立可选 UI 适配包可以实现 Localization 定义的 bridge；Presentation 主模块不实现文案/culture binding，Localization Core 不依赖 Presentation 或 Avalonia。

## 与 Testing 的关系

`tests/AtomUI.City.Localization.Tests` 必须覆盖 [features.md](features.md) 中每个 Feature ID。产品级完成不能只看现有测试文件存在，必须补齐 [testing.md](testing.md) 中列出的必断言行为。

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

Verified

八个 Feature 均具备实现、失败路径、并发/释放测试和文档合同。冻结结论仍以全量 build、模块测试与 Generator 测试持续通过为前提；新增能力必须先更新 Feature/API/Testing/Compatibility。
