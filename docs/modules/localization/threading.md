# AtomUI.City.Localization Threading

## 线程模型范围

执行边界：Host runtime localization service。

## 模块线程硬约束

- 语言包按当前 culture 懒加载。
- provider 必须可撤销。
- 缺失 key 必须诊断并走 fallback。
- 插件语言包卸载后不得出现在 lookup。

## 并发冲突策略

- 语言包格式错误：跳过并诊断。
- 缺失 key：返回 fallback 或 key。
- culture 切换部分 target 失败。
- `SetCultureAsync` 与 contribution revoke 进入同一 FIFO mutation queue；队列只在入队时短暂加锁，不在 provider、bridge、dispatcher 或 text handler 执行期间持有锁。
- Registry 直接撤销可以与 package load 并发；commit 必须再次确认 descriptor 仍处于注册状态，已撤销 load result 立即释放且不得写入 cache/state。
- mutation callback 内再次发起同一 service 的 mutation 属于重入：culture switch 返回 `ReentrantOperation`，无 Result 的 revoke API 抛 `InvalidOperationException`，禁止等待自身队尾形成死锁。

## UI 线程规则

- 非 Presentation 模块不得直接操作 Avalonia visual tree。
- 需要 UI 更新的结果必须通过 Presentation dispatcher、State 或 EventBus contract 间接到达 UI。
- Presentation 的 VisualTree 修改必须在 UI dispatcher 上执行。
- Localization Core 的 provider、bridge 调用入口和 `ILocalizedText.Changed` handler 在当前异步调用链执行，不保证位于 UI 线程；业务 handler 若操作 UI，必须显式通过 Presentation dispatcher。

## 后台任务和取消

- IO、package load、可选 application-owned bridge 和文本刷新在可取消阶段必须观察对应 token。
- mutation 的调用方 token 只控制提交前阶段；一旦 descriptor/cache/state 已提交，后续 bridge 和文本刷新由 service lifetime token 完成，调用方取消不得制造部分发布状态。
- 长生命周期后台任务必须绑定 owner；owner 释放时取消。

## 死锁规避

- 不在 UI 线程同步等待异步操作。
- 不在 lock 内调用用户 handler、插件代码、dispatcher、transport 或外部 process。
- mutation queue 使用异步调用链标记识别重入；排队事务之间共享顺序，不共享线程，也不持有 Monitor/Semaphore 等待外部代码。
- 释放顺序从 leaf owner 到 parent owner。
