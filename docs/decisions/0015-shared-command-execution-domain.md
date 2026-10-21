# 0015 共享 CommandExecutionDomain 只提供 latest-wins 执行所有权

状态：Accepted

日期：2026-10-21

## 背景

`CommandConcurrencyPolicy.Reject`、`Allow` 和 `RejectByKey` 都只约束单个 Command 实例。两个表达互斥用户意图的 Command（例如“打开文件”和“打开文件夹”）各自都可以成功准入，因此后发意图无法原子地取消前发工作，也没有共享代际可供提交结果前检查。

把这一问题留给每个应用手写 `CancellationTokenSource`、锁和版本号，会重复制造取消/释放竞态。反过来，把队列、优先级、重试、debounce 或任意 policy engine 一并放入框架，又超出了当前已证明的通用需求。

## 决策

- 新增 `CommandExecutionDomain`。多个 Command 只有在创建时显式传入同一个 domain，才属于同一执行所有权边界。
- domain 的策略固定为 latest-wins：新执行先原子取得新 generation 和 current lease，再请求取消此前 current execution；新执行不等待旧执行退出，因此不合作的旧执行不能阻塞新意图开始。
- `CommandExecutionLease` 向执行体公开 `Generation`、有效 `CancellationToken` 和 `IsCurrent`。框架负责 lease 的创建和完成，应用不能手工释放或伪造 lease。
- `CommandFactory.CreateAsync` 新增 domain-first 泛型和非泛型重载；Presentation 的 `IUiCommandFactory` 同步镜像这些重载。既有重载和三种单 Command 并发策略不改变。
- 单 Command 并发门禁先执行，domain 准入后执行。需要同一 Command 的重复调用也 latest-wins 时，调用方必须显式选择 `Allow`；框架不暗改默认 `Reject`。
- domain token 作为 `OperationScope` 的外部 token，因此被替换的命令稳定提交 `Canceled`，而不是误报 `Failed` 或 `Completed`。
- activation owner 仍由既有 `IActivationScope.StartAsync` 监督整个 domain execution。Activation 停止会取消并 drain 命令；domain 不持有、复原或回放 ActivationScope。
- domain 异步释放按“关闭准入、取消全部 active execution、限时 drain”执行。释放幂等；关闭后的命令直接执行稳定映射为 `Rejected`。
- City 只提供执行所有权和 currentness。应用仍负责业务结果的 generation/version 提交栅栏；取消不是撤销已经发生的业务副作用。
- 本次不增加 queue、priority、retry、debounce、throttle、fairness 或可插拔 policy 抽象。

## 影响

- 新增 `AUC-MVVM-012 Shared Command Execution Domain`。
- MVVM 新增两个 Preview public 类型，并扩展 `CommandFactory`；Presentation 扩展 `IUiCommandFactory`，但不复制 domain 状态机。
- 多个 Command 可以共享一个工业级取消和代际边界，同时仍保留每个 Command 自身的 CanExecute、状态、异常和并发策略。
- latest-wins 只保证后发执行立即取得 current 身份并请求取消旧执行；不能强杀不观察 token 的用户代码。

## 后续约束

- 任何框架锁不得跨 `await`，也不得在锁内调用命令执行体、取消回调、dispatcher、异常 handler 或 diagnostics provider。
- 测试必须覆盖跨 Command 替换、generation/currentness、旧执行不合作、activation 停止、domain dispose/drain、关闭竞态、取消回调失败隔离和独立 domain 隔离。
- `IUiCommandFactory` 的 domain 重载必须继续只转交 DI dispatcher 和 diagnostics。
- 未出现第二种经过 dogfood 证明的共享执行策略前，不创建通用 policy 接口。
