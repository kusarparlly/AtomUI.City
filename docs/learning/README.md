# AtomUI.City 源码学习手册

本目录用于从使用者视角进入 AtomUI.City 源码，并通过可运行实验理解框架为什么这样设计。它不是模块设计合同，也不替代 [用户指南](../guides/overview.md) 或 [API 文档](../API/README.md)。

## 学习目标

完成一个模块不等于“读过所有文件”。你需要能够：

1. 从公开入口画出主要调用链；
2. 解释关键对象的所有权、状态和释放时机；
3. 在调试器中验证自己的推断；
4. 从测试找到正常、失败、取消和并发合同；
5. 修改 Learning Lab 构造反例，而不靠猜测理解行为；
6. 在不破坏兼容性的前提下设计一项小改动。

## 固定学习循环

每一课采用相同方法：

```text
运行 Demo
-> 记录外部现象
-> 在公开入口设置断点
-> 跟随调用栈进入实现
-> 记录状态、所有权和线程变化
-> 阅读对应测试反证理解
-> 修改 Lab 构造边界场景
-> 用自己的语言复述
```

遇到 async/await、DI、Generic Host、锁、Task 共享、Dispose 或 UI 线程等 .NET 基础时，在当前源码现场补齐，不先脱离 City 单独学习一整套理论。

## 模块顺序

顺序以当前项目依赖关系和认知前置条件为依据：

| 阶段 | 模块 | 原因 |
| --- | --- | --- |
| 1 | [Core](core/README.md) | Host、Module、DI、生命周期与诊断是所有运行时模块的地基 |
| 2 | Generators 连接层 | 解释 Core 使用的静态 Module/Service manifest 从哪里产生 |
| 3 | EventBus、State、Mvvm、Routing | 都直接建立在 Core 上，可分别学习事件、状态、激活和导航事务 |
| 4 | Localization、Security、Data | 分别建立在 State、Routing/Security 等前置能力上 |
| 5 | Presentation | 汇合 Core、MVVM、Routing、Security 与 State，并进入桌面生命周期 |
| 6 | Testing | 横向验证全部运行时模块 |
| 7 | Build、Templates、CLI | 学习编译、生成、打包和开发者工具链 |
| 后续 | PluginSystem | 等该模块进入正式建设范围后再学习 |

Generators 在 Core 阶段只学习连接协议；完整增量生成器实现留到 Generators 专题。

## 学习工程

[`labs/AtomUI.City.Learning.slnx`](labs/AtomUI.City.Learning.slnx) 是贯穿全部模块的单一演进工程：

- `CityLearning.Workbench`：普通开发者视角的 Avalonia 个人工作台；
- `CityLearning.Workbench.Tests`：验证业务服务、真实进程入口和生成式 DI；
- 学习工程不加入仓库生产 solution，不参与正式包发布。

从仓库根目录进入 Lab 后运行。必须先切换目录，因为 .NET SDK 从当前工作目录向上查找 `global.json`：

```powershell
Push-Location docs/learning/labs
dotnet build AtomUI.City.Learning.slnx
dotnet run --project CityLearning.Workbench
dotnet test CityLearning.Workbench.Tests/CityLearning.Workbench.Tests.csproj
Pop-Location
```

## 学习纪律

- 第一遍只读生产源码；实验只修改 `docs/learning/labs`。
- 断点优先使用类型和方法名，不依赖会漂移的行号。
- 先写下预测，再运行程序；否则调试器只能告诉你“发生了什么”，不能检验理解。
- 发现真实缺陷时另开正式的文档设计、实现和测试流程，不在学习代码中顺手修复。
- 每一课完成后更新 [学习进度](progress.md)。

## 当前入口

- [Core 课程地图](core/README.md)
- [第 0 课：准备调试与阅读源码](core/00-debugging-and-reading.md)
- [第 1 课：跟踪最小 Host 生命周期](core/01-host-lifecycle.md)
- [课程编写模板](lesson-template.md)
- [学习进度](progress.md)
