# AtomUI.City.Mvvm Lifecycle

## 生命周期范围

执行边界：ViewModel programming model。

AtomUI.City.Mvvm 作为 Host 服务或模块贡献接入 Core 生命周期，必须在 Host start/stop/dispose 中遵守本模块状态机。

## 模块特有状态机

- ViewModel: Constructed -> Activating -> Active -> Deactivating -> Deactivated -> Disposed
- Command: Idle -> Running -> Completed 或 Failed 或 Canceled 或 Rejected 或 Rejected
- OperationScope: Running -> Completed、Failed、Canceled 或 Rejected；首次终态胜出，Dispose 后保持只读结果。
- ActivationScope task supervisor（Planned）：Open -> Closing -> Cancelling -> Draining -> DisposingResources -> Disposed/DisposedWithFailures。
- Activation-bound Command（Planned）：Idle -> Admitted -> Running -> Completed/Failed/Canceled/Rejected；Allow 策略允许多个独立 operation 同时处于 Running。

## 生命周期流程

- Presentation 创建或解析 ViewModel。
- ActivationScope 调用 IActivatable。
- CommandFactory 创建 command。
- Interaction 由 handler 返回结果。
- `AUC-MVVM-007` 落地后，激活域后台工作必须通过 Scope 准入；停用按“关闭准入、取消、drain、释放资源”执行。
- `AUC-MVVM-009` 落地后，Command 在每次调用时绑定当前 Scope，不保留上一次激活的 Scope 引用。

## Host Shutdown / 执行结束行为

- Host 停止时阻止新操作进入。
- 取消未完成后台任务；OperationScope 必须先提交 Canceled 状态，再通知 operation token callback。
- await 每个 ActivationScope 的 drain；超时只诊断，不能声称已强杀 CLR Task。
- 从 leaf owner 到 root owner 释放资源。
- 释放失败记录诊断并继续释放其他资源。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- CanDeactivate 拒绝：导航或关闭中止。
- Command 抛异常：记录 `OperationExecutionResult` Failed；awaited 调用重抛原异常，`ICommand.Execute` 交给应用 handler；取消不进入 handler。该行为随 `AUC-MVVM-010` 落地。
- Interaction 无 handler：返回 Failed。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
