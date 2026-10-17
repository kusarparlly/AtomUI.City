# AtomUI.City.Core Lifecycle

## 生命周期范围

执行边界：Host runtime kernel。

Core 定义 Host 本体，所有运行时模块都通过 IApplicationHostBuilder、LifecyclePipeline 和 ModuleBase 接入。

## 模块特有状态机

- Builder: Created -> Configuring -> Built -> Frozen
- Host: Created -> Starting -> Running -> Stopping -> Stopped -> Disposed
- Host failure: Starting -> Faulted -> Stopping -> Stopped 或 Disposed
- LifecycleScope: Running -> Stopping -> Stopped/Faulted -> Disposing -> Disposed
- Module: Declared -> ServicesConfigured -> Initialized -> Shutdown

`LifecycleScope` 创建完成后立即进入 `Running`，当前只产生 `Running`、`Stopping`、`Stopped`、`Faulted`、`Disposing` 和 `Disposed`。`LifecycleScopeState.Created`、`Starting`、`CancelRequested` 和 `UnloadPending` 作为保留枚举值继续存在，但当前不会被产生，调用方不得依赖这些值处理运行时行为。

`LifecycleStage` 和 `LifecycleScope` 的公开创建入口只接受已定义的 `LifecycleStageArea` / `LifecycleScopeKind`；整数强制转换得到的未知枚举值立即失败，不能进入 stage key、scope tree 或 diagnostics。由于任何 struct 都能通过 `default` 绕过构造函数，默认 stage 读取 `Name`/`Key` 时快速失败，不能从非空属性泄漏 null 或形成 `Application.`；`LifecycleContext`、stage-specific `LifecyclePipelineBuilder.Use` 和 `HostDiagnosticRecord.Stage` 还必须再次验证 stage 的 area 与 name 并拒绝该输入。

`LifecycleStages.All` 返回稳定的只读包装，不暴露底层数组；转换到 `IList<LifecycleStage>` 后的写入必须抛 `NotSupportedException`，失败写入不得改变进程级阶段表。

## 生命周期流程

- CreateBuilder 收集 options、module registration、service actions 和 middleware。
- Build 验证应用身份与路径并一次性创建不可变 IApplicationContext，然后创建 GenericHost、LifecycleScope root 和 diagnostics collector。
- StartAsync 依次执行 ApplicationStart、Generic Host、Application DI scope、module contribution/init/start，并在任一步失败时逆序补偿。
- StopAsync 阻止新操作进入，取消 scope tree，逆序执行 module shutdown，释放 Application DI scope，最后停止 Generic Host。
- DisposeAsync 从 leaf scope 到 root scope 释放；已提前释放的 child 会从 parent 脱离；释放异常进入 diagnostics 且不阻断同级清理。
- Host 和 Scope 的并发 Start/Stop/Dispose 调用合并到同一事务；`StopAsync(waitCancellationToken)` 的调用方取消只取消等待，不中断已经开始的共享 Stop 事务。
- Parent Scope 使用 children 快照执行 leaf-first Stop；child 在快照后并发 Dispose 时，Parent 通过内部 tolerant handoff 加入 child 已发布或即将发布的 Stop transaction，不等待完整 Dispose transaction，也不把正常 Dispose 报告为 shutdown failure。
- lifecycle transaction 在内部锁中先发布 Task，再在锁外执行 middleware、module hook、cancellation callback 和 dispose；同一 owner 的内部递归调用快速失败，外部调用方继续共享事务。
- lifecycle middleware 可以通过 `context.ShortCircuit()` 不调用 `next`；一旦调用 `next`，必须 `await` 或直接返回该 `ValueTask`。每个 `next` 由当前 middleware 原子取得一次调用权，只在该 middleware 调用窗口和当前 pipeline transaction 内有效，禁止丢弃、缓存、延迟或并发重复调用。
- middleware 提前完成而已启动的 `next` 尚未完成时，Pipeline 必须先收拢并观察下游任务，再以合同违规失败当前 stage；Host 不得提前写入 Started/Stopped 或推进状态。required terminal 只有完整成功后才视为完成，开始调用不等于完成。
- 每次真实 Start 或 Stop transaction 创建一个 `operationId`。Start 内的 ApplicationStart、ModuleInitialize、ModuleStart 和失败 rollback 共享该 id；Stop 内的 ApplicationStop、ModuleStop 共享另一个 id；并发调用方因共享同一事务 Task 而观察同一个 id。
- `LifecycleContext` 只承载 Stage、Services、ApplicationContext、Items 和 OperationId；有效取消令牌通过 `LifecycleMiddleware(context, next, cancellationToken)` 与 pipeline terminal 的显式末尾参数传递，不在 context 中重复保存。
- IApplicationContext 不参与 Host 状态迁移；Host Dispose 后其 immutable descriptor 仍可读取。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- Created 状态收到 StopAsync 后进入共享 Stopping transaction；跳过未进入运行期的 module shutdown hook 和 Generic Host stop，但仍运行 ApplicationStop、取消 HostScope tree、逆序释放 Build 阶段创建的 module instances，最后进入 Stopped 并禁止重新 Start。
- 取消未完成后台任务。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 插件来源对象必须绑定 plugin owner。
- 插件停用时先拒绝新贡献，再撤销现有贡献，最后释放对象。
- 跨插件 contract 类型必须来自 Host 共享程序集。

## 异常中断行为

- 模块依赖循环：Build 失败，诊断包含 cycle path。
- 重复 module id：Build 失败，诊断包含已有 module 和重复 module。
- lifecycle middleware 抛异常或让 `next` 逃逸：写 `AUCHOST108`，包含 stage、middleware type、operationId 和 exception type；当前 stage 失败，Host 进入 Faulted 并执行清理。下游 middleware 或 terminal 的异常不得错误归因给上游 middleware，正常 cancellation 不作为 middleware failure。
- Build 后继续注册服务：抛 InvalidOperationException 或返回失败 Result。
- UnavailableUiDispatcher 被执行：返回调度失败，不触碰 UI。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
