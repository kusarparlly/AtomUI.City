# 常见问题与排查顺序

排障时先判断故障发生在哪个边界：Build、Start、运行期还是 Stop。不要从最终异常文本直接猜内部原因。

## Build 失败

### 模块循环或缺失依赖

现象：`Build()` 在 Module 构造副作用之前失败。

检查：

- `[DependsOn]` 是否形成 A → B → A；
- 根 Module 是否选择了所有必需能力；
- descriptor 中是否含 null、重复 identity 或非法类型；
- 不相关程序集是否试图成为其他 Module 的 registration owner。

### generated manifest 缺失

检查生成器是否以 analyzer 引用、声明类型是否为 `partial`、入口程序集是否包含相应声明，以及生成器诊断是否被构建配置隐藏。

## Start 失败或卡住

- Module 的异步初始化必须等待完成，不能保存 `next` 或使用未观察任务；
- 把真正的异步 IO 放在 `StartAsync` 阶段，不放在同步 `Build()`；
- 检查 cancellation token 是否错误地使用了已经取消的外部 token；
- 检查 UI 工作是否在 dispatcher 就绪前执行；
- 对外部资源设置有业务意义的 timeout，不使用无限等待。

Start 失败后，除了原始异常，还要查看聚合的 cleanup failures 和 Host diagnostics。原始失败说明“为什么启动失败”，清理失败说明“回滚过程中哪些资源可能没有释放”。

## 服务无法解析

按顺序确认：

1. 服务是手工注册还是生成器 marker；
2. owner Module 是否明确；
3. owner Module 是否被根 Module 选择；
4. 解析位置是否处于正确 scope；
5. 生成的 registrar 是否进入当前入口程序集的 catalog。

仅仅添加程序集引用不会把其中所有 marker 服务送进 Root Provider。

## 事件没有到达

- contract owner 是否已选择；
- publisher 与 subscriber 是否使用同一个 typed channel；
- subscription owner scope 是否已经停止；
- `PostAsync` 是否返回 `Accepted == false`；
- channel 是否发生背压、拒绝或允许的合并/丢弃；
- handler 是否要求 UI dispatcher；
- handler 失败是否被所选 error policy 转换为交付结果。

查看 `IEventBusMonitor`、`IEventChannelMonitor` 和对应 diagnostics code，不要通过重复发布掩盖原因。

## 导航失败

首先检查 `NavigationResult.Status`，再分别处理 NotFound、Rejected、Cancelled 和 Failed。核对稳定 route id、参数约束、parent、outlet、guard、resolver 和 timeout。失败后 snapshot 或 journal 发生改变属于需要报告的事务一致性问题。

## UI 更新线程错误

不要依赖对象在哪个线程构造。把 UI 亲和性表达为显式 `IUiDispatcher` 或对应 dispatch option；测试中使用 fake dispatcher，桌面集成中确认平台 adapter 已注册。

## 进程无法退出

依次检查：

- 所有 `WindowSession` 是否已关闭；
- application/window/navigation scope 是否停止；
- EventBus subscription、State subscription 和 contribution lease 是否释放；
- 后台循环是否观察 cancellation；
- Generic Host 中的 hosted services 是否停止；
- 同步和异步 disposable 是否走了正确路径。

不要强制终止线程作为正常关闭方案。需要临时止损时，应保留 dump、结构化 diagnostics 和首个主异常，再终止进程。

## 提交问题时提供

- City commit 或包版本、SDK、OS、RID；
- 最小可复现工程或对应 fixture 场景；
- 完整异常链和 diagnostics code；
- Build/Start/Stop 中哪个边界失败；
- 是否启用 Native AOT、Headless 或真实桌面平台；
- 退出码和资源清理结果。
