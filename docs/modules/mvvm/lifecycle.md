# AtomUI.City.Mvvm Lifecycle

## 生命周期范围

执行边界：ViewModel programming model。

AtomUI.City.Mvvm 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- ViewModel: Constructed -> Activating -> Active -> Deactivating -> Deactivated -> Disposed
- Command: Idle -> Running -> Completed、Failed、Canceled 或 Rejected
- OperationScope: Running -> Completed、Failed、Canceled 或 Rejected；首次终态胜出，Dispose 后保持只读结果。
- ActivationScope task supervisor（Implemented / Release Validation Pending）：Open -> Closing -> Cancelling -> Draining -> DisposingResources -> Disposed/DisposedWithFailures。
- Activation-bound Command（Implemented / Release Validation Pending）：Idle -> Admitted -> Running -> Completed/Failed/Canceled/Rejected；Allow 策略允许多个独立 operation 同时处于 Running。
- CommandExecutionDomain（AUC-MVVM-012）：Open -> Closing -> Cancelling -> Draining -> Disposed；每个 execution 具有递增 generation，current 身份可在新准入或关闭时失效。

## 生命周期流程

- Presentation 创建或解析 ViewModel。
- ActivationScope 调用 IActivatable。
- CommandFactory 创建 command。
- Interaction 由 handler 返回结果。
- `AUC-MVVM-007` 已提供激活域后台工作准入；停用按“关闭准入、取消、drain、释放资源”执行。
- `AUC-MVVM-009` 已要求 owner-bound Command 在每次调用时解析当前 Scope，不保留上一次激活的 Scope 引用。
- `AUC-MVVM-012` 要求共享 domain 的新 execution 先成为 current、再取消旧 current；ActivationScope 监督包含 domain operation 在内的整个 Command Task，domain 不保存或复原 ActivationScope。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务；OperationScope 必须先提交 Canceled 状态，再通知 operation token callback。
- await 每个 ActivationScope 的 drain；超时只诊断，不能声称已强杀 CLR Task。
- 永久释放 CommandExecutionDomain 时关闭新准入、取消全部 active execution 并限时 drain；重复释放幂等，超时后旧 Task 仍可能继续运行。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- CanDeactivate 拒绝：导航或关闭中止。
- Command 抛异常：记录 `OperationExecutionResult` Failed；awaited 调用重抛原异常，`ICommand.Execute` 交给应用 handler；取消不进入 handler。该行为由已实现的 `AUC-MVVM-010` 定义，仍待完整发布验证。
- Interaction 无 handler：返回 `NotHandled`；handler 抛异常才返回 `Failed`。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
