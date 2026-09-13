# AtomUI.City.Security Threading

## 线程模型范围

执行边界：Host runtime security service。

## 模块线程硬约束

- Security 不实现登录 UI。
- 授权评估不操作 UI 或导航。

## 并发冲突策略

- AuthenticationStateStore、PermissionRegistry 和内存 providers 以实例锁保护共享状态；读者只观察完整 snapshot/descriptor。
- Authentication、Permission 和 Command 事件在各自 revision 内按提交顺序进入单消费者队列，用户观察者在内部锁外执行。
- 观察者异常被逐个隔离并记录诊断，不能阻断后续观察者或回滚 mutation。
- CommandAuthorizationSource 的 authentication、permission 和 descriptor 并发变化由同一 revision 序列归并；Dispose 后拒绝新通知入队。
- 当前事件 publisher 是逐条 FIFO 单消费者队列，不合批且不设容量上限。观察者必须短时、非阻塞；持续并发 mutation 遇到阻塞观察者会让队列增长，调用方需要把慢 IO/长任务转交到自己的受控执行器。
- 默认文件 store singleton 串行化账号/凭据 IO；`AccountSessionManager` 通过独立 gate 按 Host 串行化 token 读取、恢复、切换、刷新和删除，避免读取账号凭据时与删除/切换形成半状态。所有文件 await、认证/会话观察者均在内部锁外执行。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。

## 后台任务和取消

- IO、网络、子进程、编译分析、插件扫描、缓存清理、streaming 和 handler 调用必须可取消。
- 取消后不得提交后续状态、缓存、事件、UI 或 generated output。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。
- 只有调用方传入的 token 已请求取消时才返回 Cancelled；其他来源的 `OperationCanceledException` 属于 provider/evaluator failure。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- 用户 handler 重入状态 mutation 时只追加下一 revision，由当前 drainer 在本次通知结束后继续处理。
- 释放顺序从 leaf owner 到 parent owner。
