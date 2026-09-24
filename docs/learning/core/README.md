# Core 源码学习地图

Core 是 City 最先构建、最先启动、最后释放的运行时地基。源码学习不按目录逐个扫文件，而是沿 Host 事务展开，再回到每个子系统内部。

## 规模与区域

当前 Core 生产源码约 6,079 行，测试约 5,508 行：

| 区域 | 主要责任 | 阅读时重点 |
| --- | --- | --- |
| Hosting | Builder、Host、配置冻结、Generic Host 适配 | Build/Start/Stop/Dispose 事务 |
| Modularity | Module 描述、依赖图、服务配置和生命周期 | 验证先于实例化、唯一状态机 |
| Lifecycle | stage、middleware、scope tree | `next` 所有权、取消、并发释放 |
| DependencyInjection | marker 和 generated registrar 消费端 | Module owner、静态清单、AOT |
| Diagnostics | 结构化诊断与完成边界 | 原始故障与清理故障同时保留 |
| Threading | UI dispatcher 最小抽象 | Core 不依赖具体桌面框架 |
| Public API baseline | 二进制兼容门禁 | public 不等于内部实现需要公开 |

## 必须掌握的总调用链

```text
ApplicationHost.CreateBuilder
-> ApplicationHostBuilder
-> ConfigureHost / ConfigureServices / UseModule
-> Build
   -> freeze configuration
   -> validate module graph
   -> consume generated service registrations
   -> configure module and user services
   -> build Microsoft Generic Host
   -> create DefaultApplicationHost
-> StartAsync
   -> start Generic Host
   -> create application DI scope and LifecycleScope
   -> configure contributions
   -> initialize modules
-> StopAsync
   -> stop scope tree
   -> shutdown modules
   -> dispose application service scope
   -> stop Generic Host
-> DisposeAsync
   -> share/finish stop transaction
   -> dispose modules, scopes, Generic Host and diagnostics
```

## 课程顺序

| 课次 | 主题 | 主要源码区域 |
| --- | --- | --- |
| [00](00-debugging-and-reading.md) | 调试环境、调用栈和阅读方法 | Lab、solution、tests |
| [01](01-host-lifecycle.md) | 最小 Host 完整生命周期 | `ApplicationHost`、builder、default host |
| 02 | Builder、Options、Context、配置冻结 | Hosting |
| 03 | Module 元数据、Catalog、Graph | Modularity |
| 04 | DI owner 与 generated manifest | DependencyInjection + Generators 连接点 |
| 05 | Build 事务与失败回滚 | Hosting + Modularity |
| 06 | LifecyclePipeline 与 middleware | Lifecycle |
| 07 | LifecycleScope 树和并发终止 | Lifecycle |
| 08 | ModuleRegistry 状态机 | Modularity |
| 09 | Start、Run 和启动失败 | Hosting |
| 10 | Stop、Dispose、超时和异常聚合 | Hosting + Lifecycle + Modularity |
| 11 | Diagnostics | Diagnostics |
| 12 | UI dispatcher 与线程 | Threading |
| 13 | Public API、AOT、测试和综合实验 | 全部 |

未链接的课程将在前一课实验确认后逐课落地，不预先用静态讲义替代互动学习。

## 三类资料如何配合

- `docs/guides/core/`：先学会正确使用 Core；
- `docs/modules/core/`：确认正式设计与行为合同；
- `tests/AtomUI.City.Core.Tests/`：用可执行断言证明实现满足合同；
- `src/AtomUI.City.Core/`：解释合同如何落地。

正确顺序不是只读 `src`，而是“外部行为 → 合同 → 实现 → 测试反证”。

## Core 毕业标准

你需要能够在不看答案的情况下：

- 画出 Build、Start、Stop、Dispose 的完整资源图；
- 解释 Host、ModuleRegistry 和 LifecycleScope 为什么各有状态机；
- 指出并发调用如何共享同一个 Task，而不是重复执行；
- 解释循环依赖为何必须在 Module 构造之前失败；
- 区分主异常、回滚异常、诊断记录和最终 AggregateException；
- 为一个新的生命周期边界编写正常、失败、取消、并发和 Dispose 测试；
- 在 Lab 中完成一项受控扩展并预测所有资源的释放顺序。
