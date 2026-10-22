# AtomUI.City.Cli Lifecycle

## 生命周期范围

执行边界：Command process boundary。

CLI 默认不启动桌面 Host；只创建工程、调用构建、检查插件和输出诊断。

## 模块特有状态机

- Created -> Executing -> Completed 或 Failed
- Failure -> Diagnostics 或 non-zero gate result
- Apply transaction: Created -> Validating -> Locked -> Staging -> Committing -> Completed；失败进入 RollingBack -> RolledBack 或 RollbackFailed。
- completed journal + 相同 plan digest 的重复 apply 直接进入 Completed/no-op；不得重复写文件。

## 生命周期流程

- 读取输入。
- 验证输入和边界。
- 执行模块动作。
- 输出结果和诊断。
- 测试或 CI 断言结果。

## Host Shutdown / 执行结束行为

- 命令退出前必须结束子进程或按 cancellation kill。
- JSON envelope 在退出前完整写出。
- 非交互模式不等待输入。
- apply 退出前必须释放 workspace lock；临时 staging 在成功或完整回滚后清理，journal 保留最终状态和 plan digest。

## 插件动态变更行为

- 本模块不持有运行时插件对象。
- 只通过 manifest、package、template、CLI 或 generator 支持插件开发和检查。

## 异常中断行为

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。
- apply 原始失败与 rollback failure 同时保留；RollbackFailed 不能伪装成普通 RolledBack 或 success。

## 生命周期测试要求

生命周期测试必须覆盖：正常路径、重复调用、取消、失败中断、释放、插件撤销或执行边界结束。具体用例见 [testing.md](testing.md)。
