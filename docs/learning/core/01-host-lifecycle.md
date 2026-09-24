# 第 1 课：桌面应用中的 Host 生命周期

本课沿 Workbench 的真实启动和退出路径，建立 Core 最重要的第一张心智图。

## 本课完成后能够

- 区分 City Host、Microsoft Generic Host 与 Avalonia desktop lifetime；
- 解释 CreateBuilder、Build、Start、GUI 消息循环、Stop、Dispose 的职责；
- 解释生成式 DI 如何把窗口和业务服务交给 Root Provider；
- 预测 singleton 的创建和释放时机；
- 理解手工 `DesktopBootstrap` 为什么只是当前学习阶段的连接层。

## 先预测

运行前先写下答案：

1. `MainWindow` 在 Build、Start，还是 Avalonia 初始化时构造？
2. `JsonWorkItemRepository` 在什么时候构造？
3. 关闭窗口后，City 先 Stop 还是先 Dispose？
4. City Host、Generic Host、Avalonia lifetime 谁拥有 Root Provider？

```powershell
Push-Location docs/learning/labs
dotnet run --project CityLearning.Workbench
Pop-Location
```

## 总调用链

```text
Program.Main
-> Program.CreateHost
   -> ApplicationHost.CreateBuilder
   -> UseModule<WorkbenchApplicationModule>
   -> ConfigureServices(WorkbenchOptions)
   -> Build
-> host.StartAsync
-> DesktopBootstrap.Attach(host)
-> Avalonia StartWithClassicDesktopLifetime
   -> App.OnFrameworkInitializationCompleted
   -> DesktopBootstrap.Initialize
   -> host.Services.GetRequiredService<MainWindow>()
   -> MainWindow.Opened -> ViewModel -> Service -> Repository
-> 用户关闭 MainWindow，Avalonia 消息循环返回
-> host.StopAsync
-> using 触发 host.Dispose
```

## 1. Builder 与 Module

`WorkbenchApplicationModule` 同时声明 `[ApplicationModule]` 和 `[ServiceRegistrationOwner]`。编译器运行 City.Generators，生成属于该 Module 的服务清单。`UseModule<WorkbenchApplicationModule>()` 选择它后，Core 才把 MainWindow、ViewModel、WorkItemService 和 Repository 注册进 Root DI。

`WorkbenchOptions` 通过 `ConfigureServices` 手工注册，用来对比两种正常方式：稳定基础设施服务可由 Attribute 生成；运行时才知道的数据文件路径由应用组合根显式传入。

## 2. Build

`Build()` 是同步组合事务：

```text
冻结 builder
-> 验证 Module graph
-> 读取入口程序集的 generated manifests
-> 按已选 Module owner 应用服务注册
-> 构建 Microsoft Generic Host 与 Root Provider
-> 创建 DefaultApplicationHost
```

Build 不应执行真正的异步 IO。此时只是登记了 singleton 的创建规则，尚未读取 JSON，也未构造窗口。

## 3. Start 与 GUI

`host.StartAsync()` 完成 Core/Generic Host/Module 的启动事务。随后 `DesktopBootstrap.Attach(host)` 暂存 Host 引用，Avalonia 才开始 desktop lifetime。

Avalonia 初始化完成时，`DesktopBootstrap.Initialize` 从 `host.Services` 解析 MainWindow。DI 为构造窗口继续创建：

```text
MainWindow
-> WorkbenchViewModel
-> WorkItemService
-> IWorkItemRepository -> JsonWorkItemRepository
-> WorkbenchOptions
```

因此应用只有一个 Root Provider；Avalonia 没有另造一套业务容器。窗口 `Opened` 后才执行异步文件读取，避免把 IO 塞进 Build。

## 4. 为什么有 DesktopBootstrap

Core 只定义通用 Host 和 `IUiDispatcher` 抽象，不应依赖 Avalonia。当前 App 又尚未学习 Presentation，所以业务工程用一个很小的桥接类完成两件事：

- 把已经启动的 City Host交给 Avalonia App；
- 从 City DI 解析 MainWindow，设置为 desktop lifetime 的主窗口。

这不是 Core 的隐藏职责。学习 Presentation 时，应审视框架提供的正式桌面接入如何替代它。

## 5. Stop 与 Dispose

当主窗口关闭，Avalonia 消息循环返回，`finally` 调用 `host.StopAsync()`：停止 LifecycleScope、Module 和 Generic Host。随后 `using` 调用 `Dispose()`，释放 ModuleRegistry、scope、Root Provider 和 diagnostics。

WorkItemService 是实现 `IDisposable` 的 Root singleton，因此 Root Provider Dispose 时释放其 `SemaphoreSlim`。Stop 表示停止运行，Dispose 才是最终资源释放边界。

## 断点观察表

| 断点 | 观察内容 |
| --- | --- |
| `Program.CreateHost` | 应用配置与 Module 选择 |
| `ApplicationHostBuilder.Build` | Module graph、generated services、Generic Host |
| `DefaultApplicationHost.StartAsync` | `_state` 与 `_startTask` |
| `DesktopBootstrap.Initialize` | City Host 如何进入 Avalonia 生命周期 |
| `MainWindow` 构造函数 | DI 构造链和 Root Provider |
| `JsonWorkItemRepository.ReadAllAsync` | 真正 IO 为何发生在窗口打开后 |
| `DefaultApplicationHost.StopAsync` | `_stopTask` 和停止顺序 |
| `DefaultApplicationHost.DisposeCoreAsync` | Root Provider 和 singleton 的释放 |

## 实验

### 实验 A：验证 singleton 惰性创建

在 `DesktopBootstrap.Initialize` 临时不解析 MainWindow，先预测 MainWindow、ViewModel 和 Repository 的构造断点是否命中。实验后还原代码。

### 实验 B：验证持久化跨进程

设置 `CITY_LEARNING_DATA_FILE` 指向临时路径，启动应用新增任务，完全关闭并重新启动。断点观察第二次启动如何创建一批全新的 singleton，却从同一文件恢复业务数据。

### 实验 C：理解入口程序集

阅读 `WorkbenchApplicationTests.ApplicationProcess_UsesGeneratedServices_AndPersistsWorkItems`。它必须启动 Workbench 独立进程，因为 Core 从进程入口程序集读取 generated manifest；直接在 `testhost` 内调用不能伪装真实应用入口。

## 理解检查

1. 为什么先 Start City Host，再进入 Avalonia 消息循环？
2. 为什么窗口由 City DI 创建，而 App 由 Avalonia 创建？
3. 为什么生成了服务代码仍必须选择其 Module owner？
4. 为什么文件读取属于窗口打开后的业务操作，而不属于 Build？
5. Stop 与 Dispose 分别负责什么？
6. 将来 Presentation 模块应替换当前哪一层代码？

## 完成标准

- 能不看本文画出完整调用链；
- 能解释三个生命周期对象的职责与所有权；
- 能从 MainWindow 构造调用栈证明生成式 DI 生效；
- 三个实验的预测与结果均有记录；
- 已在 [学习进度](../progress.md) 勾选第 1 课。
