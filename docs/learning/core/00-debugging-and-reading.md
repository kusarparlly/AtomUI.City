# 第 0 课：准备调试真实桌面应用

本课不先做框架内部实验，而是把自己当成 City 使用者：运行一款会保存真实数据的 Avalonia 桌面应用，再沿应用入口进入 Core 源码。

## 本课完成后能够

- 构建、运行和测试 `CityLearning.Workbench`；
- 找到桌面应用真正的入口 `Program.Main`；
- 区分业务工程、City.Core 与源码生成器；
- 在公开接口与 internal 实现之间跟踪调用栈；
- 查看线程、Task 和 Host 状态。

## 1. 工程角色

```text
CityLearning.Workbench
├─ Avalonia：窗口、控件、桌面消息循环
├─ City.Core：Host、Module、DI、生命周期、Diagnostics
├─ City.Generators：编译期生成 Module/Service 清单
└─ Workbench 业务：任务、JSON Repository、ViewModel
```

当前故意不引用 City.Presentation 和 City.Mvvm。这样可以先看清 Core 与 GUI 的最小衔接；学到对应模块时，再用框架能力替换临时的手工桥接。

## 2. 构建、运行和测试

SDK 从当前目录向上寻找 `global.json`，因此先进入 Lab：

```powershell
Push-Location docs/learning/labs
dotnet build AtomUI.City.Learning.slnx
dotnet run --project CityLearning.Workbench
dotnet test CityLearning.Workbench.Tests/CityLearning.Workbench.Tests.csproj
Pop-Location
```

运行后应出现“个人工作台”窗口。添加任务、关闭程序、再次启动，任务仍存在。数据默认写入当前用户本地应用数据目录，也可用 `CITY_LEARNING_DATA_FILE` 指定实验文件。

## 3. 第一次设置断点

按方法符号设置，不依赖行号：

1. `CityLearning.Workbench.Program.Main`
2. `CityLearning.Workbench.Program.CreateHost`
3. `AtomUI.City.Core.Hosting.ApplicationHost.CreateBuilder`
4. `AtomUI.City.Core.Hosting.ApplicationHostBuilder.Build`
5. `AtomUI.City.Core.Hosting.DefaultApplicationHost.StartAsync`
6. `CityLearning.Workbench.DesktopBootstrap.Initialize`
7. `CityLearning.Workbench.Views.MainWindow.OnOpened`
8. `AtomUI.City.Core.Hosting.DefaultApplicationHost.StopAsync`
9. `AtomUI.City.Core.Hosting.DefaultApplicationHost.Dispose`

第 4、5、8、9 项位于 Core 的 internal 实现中。应用依赖本仓库 ProjectReference，所以调试器可以直接进入源码。

## 4. 调试器中关注什么

- Call Stack：从业务入口到 Core 的实际调用关系；
- `host.GetType().FullName`：公开接口后的真实对象；
- `Environment.CurrentManagedThreadId`：进入 Avalonia 消息循环前后的线程；
- `Task.CurrentId`：当前 continuation 是否由 Task 执行；
- Core 私有 `_state`、`_startTask`、`_stopTask`：只用于理解实现，不得成为业务依赖；
- `host.Services`：Root Provider 如何创建 MainWindow 和业务服务。

`await` 表示异步等待，不等于创建新线程。桌面程序还必须特别观察 UI `SynchronizationContext` 和 Avalonia UI 线程。

## 5. 从实现找到测试

```powershell
rg -n "StartAsync|StopAsync|DisposeAsync" tests/AtomUI.City.Core.Tests
rg -n "StopBeforeStart|Concurrent|Rollback" tests/AtomUI.City.Core.Tests
```

测试名称通常更接近合同语言。记录准备条件、触发动作、预期状态、释放断言和 diagnostics 断言。

## Lab 实验

1. 在 `Program.Main` 停住并写下你预测的下一层 City 方法；
2. Step Into 到 `ApplicationHostBuilder` 构造函数；
3. 观察 City 内部创建的 Microsoft Generic Host builder；
4. 继续执行到窗口出现；
5. 添加任务、关闭窗口，观察 Stop 与 Dispose；
6. 再次启动，确认 JSON Repository 恢复数据。

## 理解检查

1. 为什么这个应用仍使用 ProjectReference，而正式开发者通常使用 NuGet？
2. 为什么 Avalonia 不能替代 City Host？
3. 为什么 City Host 也不能替代 Avalonia 消息循环？
4. 为什么可以在学习时观察 private `_state`，业务代码却不能依赖它？
5. `await` 为什么不等于“新建线程”？

## 完成标准

- GUI、构建和测试全部成功；
- 能命中上述九个断点；
- 能从用户代码进入 Core，并返回 Avalonia UI；
- 能指出数据文件和 Root Provider 的所有者；
- 已在 [学习进度](../progress.md) 勾选第 0 课。
