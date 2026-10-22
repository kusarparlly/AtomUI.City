# AtomUI.City.Cli Threading

## 线程模型范围

执行边界：Command process boundary。

## 模块线程硬约束

- 命令入口固定为 atomui city。

## 并发冲突策略

- 输入非法：拒绝执行并输出诊断。
- 执行失败：返回失败 result 或 gate failure。
- 输出不符合 contract：测试失败。
- `AUC-CLI-009` 对同一 workspace 使用 `.city/transactions/apply.lock` 文件锁；锁已占用时快速失败，不在进程内 semaphore 后无限等待。
- plan 计算可以跨 workspace 并发；同一 workspace 的 apply 必须串行。目标在 preflight 后出现属于竞争冲突，禁止覆盖。
- explain catalog 初始化后不可变，可由多个命令调用并发读取。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。
- apply 的 preflight/staging 可取消；commit 开始后延迟取消，必须先完成 commit 或 rollback，再以稳定 transaction state 返回。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 释放顺序从 leaf owner 到 parent owner。
