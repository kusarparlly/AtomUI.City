# AtomUI.City.Presentation

程序集：`AtomUI.City.Presentation`
源码：`src/AtomUI.City.Presentation`
测试：`tests/AtomUI.City.Presentation.Tests`

## 定位

Presentation 是 City 业务运行时与 Avalonia 之间的 UI 事务协调层，负责把已经确定的 ViewModel target 安全地落实为 Window/Outlet 中的真实 View，并管理对应 ownership、线程、生命周期、失败与诊断。

规范性总合同见 [industrial-design.md](industrial-design.md)。

## 硬性约束

- 主包只强依赖 Avalonia，不强依赖 AtomUI。
- Router snapshot 是导航真相；Presentation 不解释或补偿导航。
- ViewModel 来自 DI/显式或 generated factory/当前 Entry 复用，不反射猜测构造。
- commit plan 单次使用，候选所有权明确且所有失败路径释放。
- 所有 Avalonia 对象访问进入 UI dispatcher；用户业务代码不在框架锁内执行。
- 同 Outlet 和同 Window modal Interaction 使用有界 FIFO；满载拒绝最新请求。
- 同一 Window 一次关闭事务最多一个可见 confirmation。
- Bind/Unbind 不伪造 VisualTree 事件，只接受真实 Avalonia 物理回执。
- 不提供文案、多语言、language package、fallback 或 `Localized*` binding。
- Windows 是 1.0 正式平台；Linux/macOS 仅 build-only experimental。

## 使用入口

City Host 使用 `UseModule<PresentationModule>()`；独立 DI/测试使用 `AddPresentation()`。Avalonia 初始化完成后调用 `Attach`，Window 在 Show 前调用 `RegisterWindow`。

## 文档索引

| 文档 | 内容 |
| --- | --- |
| [industrial-design.md](industrial-design.md) | 1.0 总合同 |
| [architecture.md](architecture.md) | 对象、所有权和故障域 |
| [lifecycle.md](lifecycle.md) | 五套状态机和关闭流程 |
| [threading.md](threading.md) | UI 白名单、并发与取消 |
| [api-contracts.md](api-contracts.md) | Public API card |
| [features.md](features.md) | Feature 证据与状态 |
| [testing.md](testing.md) | CI、压力和桌面发布门禁 |
| [diagnostics.md](diagnostics.md) | 稳定诊断码 |
| [compatibility.md](compatibility.md) | 1.0 兼容承诺 |
| [state-and-localization.md](state-and-localization.md) | State/Localization 边界 |
| [resources-and-plugins.md](resources-and-plugins.md) | owner-bound UI contribution |

## 成熟度

Windows 1.0 能力已完成 Release Verified：unit、Headless 10,000 操作压力、真实 desktop smoke、批准性能 baseline、公开 API 冻结、SourceLink、strict package validation 和隔离 NuGet consumer 均已通过。Linux/macOS 仍是 build-only experimental，不属于本次平台发布证明。
