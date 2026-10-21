# AtomUI.City.Presentation Features

状态定义：`Implemented` 表示源码和模块测试已落地；`Release Validation Pending` 表示仍缺真实平台或发布压力证据；`Release Verified` 只能在全部门禁完成后使用；`Retired Before 1.0` 不构成兼容承诺且编号不得复用。

## Feature 索引

| ID | Feature | 状态 | 主要证据 |
| --- | --- | --- | --- |
| AUC-PRESENTATION-001 | Avalonia UI Dispatcher Bridge | Release Verified | AvaloniaUiDispatcherTests, platform tests |
| AUC-PRESENTATION-002 | Exact View Registry and Locator | Release Verified | ViewLocatorTests, generator tests |
| AUC-PRESENTATION-003 | View Factory and Binding | Release Verified | ViewBindingTests |
| AUC-PRESENTATION-004 | Transactional Route Outlet Commit | Release Verified | RouteOutletTests, industrial contract tests |
| AUC-PRESENTATION-005 | Identity-filtered Real Visual Feedback | Release Verified | ViewBindingTests, VisualFeedbackTests, headless fixture |
| AUC-PRESENTATION-006 | Scoped Interaction and Optional Validation | Release Verified | PresentationInteractionHandlerTests, ValidationVisualStateBindingTests |
| AUC-PRESENTATION-007 | Owner-bound Presentation Resources | Release Verified | PresentationResourceRegistryTests, resource dictionary revoker tests |
| AUC-PRESENTATION-008 | Plugin UI Unload Coordination | Release Verified | ActivePluginViewRegistryTests, plugin unload tests |
| AUC-PRESENTATION-009 | Runtime and Window Sessions | Release Verified | Runtime tests, headless fixture, Windows desktop process test |
| AUC-PRESENTATION-010 | ViewModel Acquisition and Entry Ownership | Release Verified | ViewModelFactoryTests, RouteOutletTests |
| AUC-PRESENTATION-011 | Scoped Modal Interaction Resolution | Release Verified | Interaction handler and queue tests |
| AUC-PRESENTATION-012 | Layered View Overrides | Release Verified | ViewLocatorTests, generator tests |
| AUC-PRESENTATION-013 | Presentation Localization Revision Convergence | Retired Before 1.0 | 迁移到 Localization/application boundary |
| AUC-PRESENTATION-014 | Hierarchical Fault Model | Release Verified | failure injection and diagnostics tests |
| AUC-PRESENTATION-015 | Bounded Backpressure and Candidate Ownership | Release Verified | PresentationIndustrialContractTests |
| AUC-PRESENTATION-016 | Deterministic Window Close Origins and Confirmation | Release Verified | unit close-origin tests, headless fixture, Windows desktop process test |
| AUC-PRESENTATION-017 | Logical Root and Owned Child ViewModels | Release Verified | ViewModelCompositionTests 14/14；Runtime/Window race；Headless Window close；isolated package consumer |
| AUC-PRESENTATION-018 | UI Command Factory Safe Default | Release Verified | UiCommandFactoryTests；DesktopDogfood ViewModels；domain-first overload；Public API/package gates |
| AUC-PRESENTATION-019 | Routing-Presentation Technical Coordination | Release Verified | RoutePresentationCoordinatorTests 10/10；Routing conditional-restore tests；Public API/package consumer |

## Feature 合同摘要

### 001 Dispatcher

后台调用 marshal；UI 调用 inline；Runtime Stopping 允许清理，Stopped/Faulted 拒绝；callback 异常传播并诊断。

### 002-003 View

ViewModel Type + ViewKey 精确查找；显式/generated registration；UI dispatcher 创建和 Avalonia DataContext；无运行时扫描 fallback。

### 004、010、014、015 Outlet/Ownership/Fault

单次 plan ownership、FIFO、有界 admission、temporary attach、final commit、pre-commit rollback、post-commit no rollback；普通失败 OutOfSync，不变量失败 Faulted，Entry 异步且完整释放。

### 005 Visual feedback

仅真实 Avalonia 白名单事件；identity 过滤；handler 失败隔离；不改变业务状态。

### 006、011 Interaction/Validation

分层 handler、每 Window 有界 modal FIFO、跨 Window 并行；可见 UI 与文案由应用实现。Validation 是完全可选 visual adapter。

### 007-008 Plugin UI

resource/view/handler 带 owner 和 revoke；active view 优先清理，局部失败继续；resource dictionary revoke 不承载 Localization。

### 009、016 Runtime/Window

双注册入口、Attach/RegisterWindow 主路径、Show 前注册、close origin、唯一 confirmation、并发事务合并和全量清理。

### 012 Generator

1.0 只生成 View registrar，输出稳定排序；不生成 Interaction/resource/plugin descriptor。

### 013 Retired

Presentation 主包不再引用 Localization，不提供 culture revision、文案 binding 或 language package 处理。该编号永久保留，不得重用。

### 017 ViewModel composition

MVVM 保持无 Root/Child 角色；Presentation 显式把逻辑 Root 附着到 Runtime/Window owner，并以异步 lease 建立 owned Child。Child 使用独立且链接父取消的 ActivationScope，释放 leaf-to-root；不反射扫描、不引入 Root/Child 基类、不把 RouteOutlet Entry 自动纳入组合树。完整合同见 [viewmodel-composition.md](viewmodel-composition.md)。

### 018 UI command factory

Presentation 提供 dispatcher 必选的 `IUiCommandFactory` 作为 Avalonia ViewModel 默认入口。它完整转发 MVVM command 能力，包括 [ADR 0015](../../decisions/0015-shared-command-execution-domain.md) 的 domain-first 泛型/非泛型重载；由 DI 固定注入 `IUiDispatcher` 和 `IHostDiagnostics`，不复制命令或 domain 状态机。底层 `CommandFactory` 继续服务 Headless、CLI、测试和明确承担线程责任的低级代码。双入口裁决见 [ADR 0014](../../decisions/0014-presentation-ui-command-factory.md)。

### 019 Routing-Presentation coordinator

Presentation 按 Outlet 实例串行编排 Router commit、应用 plan factory 与 RouteOutlet commit。Router 成功后必须携带 `NavigationCommitReceipt`；plan/Outlet 失败后使用 receipt 条件恢复，后续导航已经推进 generation 时稳定拒绝迟到恢复。应用继续拥有 route 到 ViewModel/View/lease 的映射以及最终用户错误政策。完整裁决见 [ADR 0016](../../decisions/0016-routing-presentation-coordination.md)。

## 治理门禁

新增能力必须先分配 Feature ID，并在同一提交更新 API、诊断、测试和兼容性文档。Feature 状态只能依据可重复执行的证据提升，不能依据代码存在或人工判断提升。
