# 0014 Presentation UI 命令工厂双入口

状态：Accepted

日期：2026-10-17

## 背景

`AtomUI.City.Mvvm.CommandFactory` 同时服务桌面 UI、CLI、Headless 和测试，因此它的 `notificationDispatcher` 必须保持可选。但在 Avalonia ViewModel 中直接调用该底层入口时，开发者可能漏传 dispatcher；命令仍能编译和运行，却会把 `CanExecuteChanged` 和运行状态 `PropertyChanged` 发布到完成命令的线程。正确性因而依赖每个调用点的人工纪律。

## 决策

- 保留 `AtomUI.City.Mvvm.CommandFactory` 作为 UI 框架无关的低级入口，不改变现有签名和 Headless 行为。
- `AtomUI.City.Presentation` 新增公开 `IUiCommandFactory`，作为绑定到 Avalonia View 的命令默认入口；默认实现保持 internal，并由 `AddPresentation()` 注册为 singleton。
- UI 工厂镜像底层工厂现有的同步、异步、泛型、activation、并发和异常处理能力，不重新实现命令状态机。
- UI 工厂从 DI 固定获得 `IUiDispatcher` 和可选 `IHostDiagnostics`，调用方 API 不暴露 `notificationDispatcher` 或逐命令 diagnostics 参数。
- 所有异步命令都由 UI 工厂把同一个 dispatcher 和 diagnostics 转交给 `CommandFactory.CreateAsync`；同步命令转交 diagnostics，但不虚构异步调度语义。
- Presentation/ViewModel 业务代码默认注入 `IUiCommandFactory`。直接调用 `CommandFactory` 仍允许用于 CLI、Headless、测试以及明确承担线程责任的低级代码。
- 不通过检测 Avalonia control、同步上下文或调用栈猜测“当前是否 UI 场景”；UI/Headless 入口由组合根显式选择。

## 影响

- MVVM 继续只依赖 Core，不引用 Presentation 或 Avalonia。
- Presentation 已有的 `Presentation -> Mvvm` 依赖承载该薄适配，不形成循环依赖。
- `AddPresentation()` 后，ViewModel 只需注入 `IUiCommandFactory`，不能在普通创建调用中漏传或替换 dispatcher。
- 高级调用方仍可使用底层工厂；该能力不等于 Presentation 推荐入口。

## 后续约束

- `IUiCommandFactory` 的异步公开重载必须持续覆盖 `CommandFactory.CreateAsync` 的能力；底层新增重载时必须评估并同步 UI 入口。
- 测试必须证明聚合注册可解析该服务、已有自定义注册不被覆盖、全部异步重载使用 DI dispatcher，并且通知失败仍由注入的 diagnostics 观察。
- UI 工厂不得复制命令执行、并发、取消、activation 或异常处理状态机。
