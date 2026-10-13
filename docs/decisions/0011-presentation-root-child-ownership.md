# 0011 Presentation 提供逻辑 Root/Child，MVVM 保持角色无关

状态：Accepted

日期：2026-10-13

## 背景

MVVM 的激活、停用、Command 和 Validation 是原子能力，不应该预设某个 ViewModel 必然是应用根、窗口根或页面子对象。与此同时，桌面应用普遍存在不由 RouteOutlet 创建的长生命周期对象，例如主窗口 ViewModel 及其状态栏、工具栏或工作区子 ViewModel。

City Presentation 已经为路由 Entry 提供 ViewModel lease 和 ActivationScope 所有权，但尚不能明确表达“某个窗口逻辑 Root 拥有若干独立生命周期 Child”。让应用手工 Dispose 会丢失父取消、异步 drain、leaf-to-root 释放和失败聚合语义；把 Root/Child 做成 MVVM 基类又会污染原子层并制造僵硬继承树。

## 决策

- City.Mvvm 不增加 Root、Child、Window 或 Route 等角色概念，只提供角色无关的生命周期原语。
- City.Presentation 增加 `AUC-PRESENTATION-017 Logical Root and Owned Child ViewModels`。
- Root 是显式附着到 Presentation/Window owner 的逻辑角色，不是全局唯一对象，也不是 `RootViewModelBase`。
- Child 是显式所有权关系，不通过反射扫描字段、属性或对象图推导，也不要求 `ChildViewModelBase`。
- attach Root 和 attach Child 都返回唯一 `IAsyncDisposable` ownership lease。调用方必须保存 lease，或把它转交给更上层 owner。
- 父对象持有 Child lease。Child 拥有独立 ActivationScope；其 token 链接父 Scope，但资源集合和任务注册不与父 Scope 共用。
- 结束顺序从 leaf 到 root：先关闭子准入、取消并 drain 子任务、停用和释放子对象，再结束父对象。局部清理失败被聚合并诊断，但不能跳过其他 Child。
- RouteOutlet 页面继续由 PresentationEntry 管理，不自动成为 Window Root 的 Child；应用只有显式调用组合 API 才建立父子关系。
- 一个应用可以按 Presentation Runtime、Window 或其他明确 owner 拥有多个逻辑 Root；“主窗口根”是应用选择的角色，而不是框架强制的单例。

## 影响

- Presentation 新增显式 ViewModel composition public contract、lease、诊断和测试。
- WindowSession 和 Runtime shutdown 必须把 Root composition 纳入现有 leaf-to-root 清理链。
- MVVM 仍不依赖 Presentation 或 Avalonia visual，依赖方向保持 `Presentation -> Mvvm`。
- 应用可以用同一套合同表达主窗口/状态栏等非路由对象，而无需建立第二套生命周期框架。

## 后续约束

- 实现前必须更新 Presentation Feature、API、Architecture、Lifecycle、Diagnostics、Testing、Compatibility 和全局开发跟踪。
- public API 必须显式携带 owner identity，拒绝重复 attach、跨 owner lease 和终态 mutation。
- 必须测试 Root/Child 激活、链接取消、独立 Scope、重复释放、child 部分失败、Window close/Runtime stop 竞态和 RouteOutlet 隔离。
- 禁止通过反射、属性命名、DI lifetime 或 View 层级隐式推断父子关系。
