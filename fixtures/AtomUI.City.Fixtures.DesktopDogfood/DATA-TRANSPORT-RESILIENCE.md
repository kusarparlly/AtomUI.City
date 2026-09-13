# Desktop Dogfood Data Transport Resilience V3

## 1. 目标

本轮在现有真实 HTTP、gRPC 和 SignalR 正常路径之上增加可重复的故障与恢复门禁，验证 City Data 在桌面应用持续运行期间面对网络抖动、半响应、流中断、服务进程崩溃和恢复时能够给出确定结果，并且不破坏 EventBus、State、Security、MVVM、Router 和 Presentation 的既有状态。

所有服务只监听 `127.0.0.1`，测试不访问互联网。故障编排属于 Dogfood fixture，不增加 City Data 的产品公共 API，也不把测试控制面注入开发者业务合同。

## 2. 双层测试架构

### 2.1 进程内协议故障层

主 Dogfood 进程继续启动一个 Kestrel loopback server。该服务同时提供 HTTP/1、HTTP/2 gRPC 和 SignalR，并通过固定请求值触发确定性故障。

这一层负责快速验证：

- HTTP 503 重试、请求超时、响应体中途断开、响应乱序和幂等 mutation 重试；
- gRPC `Unavailable`、deadline、server stream 中途失败和 duplex stream 中途失败；
- SignalR 服务端强制断开、自动重连、重复及乱序消息的可观察传递。

### 2.2 独立服务进程层

Dogfood 额外启动自身程序集的 `--data-server-child` 模式。子进程只承载 loopback Data server，不初始化 City Host 或 Avalonia。父进程预留固定 HTTP/gRPC 端口，等待子进程 readiness 信号后才开始测试。

故障阶段由父进程强制终止子进程，形成真实的 socket 断开。恢复阶段必须在相同端口重新启动服务，使现有 HTTP client、gRPC channel 和 SignalR automatic reconnect 面对真实 endpoint 恢复，而不是通过更换 URI 创建一套新链路。

子进程 stdin 关闭或收到 `stop` 时执行正常释放；父进程退出路径必须等待或终止子进程，禁止留下后台服务。

## 3. 故障目录

| ID | Transport | 故障 | 必须证明的结果 |
| --- | --- | --- | --- |
| DTR-01 | HTTP | 连续两次 503 | query 按配置重试后成功，调用次数准确 |
| DTR-02 | HTTP | 响应延迟超过 timeout | 结果归类为 timeout/cancelled，应用可继续请求 |
| DTR-03 | HTTP | 200 响应体写入一部分后断开 | 返回失败，不得把残缺 payload 当作成功 |
| DTR-04 | HTTP | 两个请求按相反顺序完成 | operation identity 唯一，结果与各自请求匹配 |
| DTR-05 | HTTP | mutation 已提交但首个响应连接断开 | 使用同一 idempotency key 重试，只产生一次库存扣减和同一 receipt |
| DTR-06 | gRPC | unary 返回 `Unavailable` | 映射为明确的 Data error，后续正常 unary 成功 |
| DTR-07 | gRPC | unary 超过 deadline | 映射为 deadline error，不污染 channel 后续调用 |
| DTR-08 | gRPC | server stream 发送三条后失败 | 已提交消息有序可见，终态为失败而非静默完成 |
| DTR-09 | gRPC | duplex 收到两条后失败 | 已提交响应有序可见，写入/读取端均可确定收束 |
| DTR-10 | SignalR | Hub 主动断开连接 | 观察到 reconnect 状态并恢复 Connected，恢复后 invoke 成功 |
| DTR-11 | SignalR | 推送重复和乱序 sequence | 传输层按服务端发送顺序交付；业务层不得假设自动去重 |
| DTR-12 | Process | 同时使用三种协议时服务进程崩溃 | HTTP/gRPC/SignalR 均观察到失败或断开，无请求永久挂起 |
| DTR-13 | Process | 相同端口重启服务 | 既有客户端恢复，三种协议再次成功，子进程最终释放 |

## 4. 并发与时序规则

- 每个故障由固定 fault ID、特殊请求值或 request ID 触发，不读取随机数。
- 所有等待都设置有限 deadline；失败不得依赖 `Task.Delay` 后直接假定状态已经改变。
- HTTP 乱序测试并发发起请求，并记录真实完成顺序。
- 幂等测试的首次失败必须发生在服务端提交之后；重试使用同一个 request ID。
- gRPC stream 必须读取终态，不能只统计故障前收到的消息。
- SignalR 必须订阅 `StateChanged` 并等待可观察状态转换；重连后复用原连接对象。
- 子进程 crash 与 restart 串行执行，同一批次不允许两个控制器竞争端口。

## 5. 跨模块不变量

Data 故障期间同时维持以下约束：

1. Security credential 不得进入 stdout、diagnostics 或 `run-report.json`；
2. 每个故障及恢复结果写入 `data-resilience` coverage category；
3. 预期失败写入 ledger 的 `expected-failure`，不得成为应用主故障；
4. State 只提交已验证的最终 transport health，不提交残缺响应；
5. EventBus 只发布已收束的恢复事件，不为每次内部 retry 制造业务事件；
6. MVVM、Router、Presentation 在故障工作负载完成后仍能继续运行场景矩阵；
7. Host 关闭后 in-process server、外部子进程、HTTP response、gRPC stream 和 SignalR connection 全部释放。

## 6. 报告与门禁

控制台输出一条稳定摘要：

```text
DESKTOP_DOGFOOD_DATA_RESILIENCE faults=13 expectedFailures=<n> restarts=1 http=ready grpc=ready signalR=ready childReleased=true
```

`run-report.json` 必须包含 13 个唯一 `data-resilience` coverage point。Quick、Standard 和三个固定 seed 的 Headless profile 都执行完整故障目录；Windows GUI 读取同一应用报告，不复制协议测试逻辑。

通过条件：

1. DTR-01 至 DTR-13 全部命中；
2. 所有预期失败均在 deadline 内收束，恢复后正常请求成功；
3. 子进程发生一次真实 crash、一次同端口 restart，并在最终报告前释放；
4. `resourcesReleased=true`、`failure=null` 且报告无 credential；
5. Release build 零 warning、零 error，Presentation 完整测试程序集全绿。

## 7. 当前验证基线

2026-09-11 的第三阶段实现已经达到本文件门禁：

- Quick、Standard 与三个 Headless seed 均执行 13 个唯一故障点；稳定摘要为 `faults=13 expectedFailures=8 restarts=1 http=ready grpc=ready signalR=ready childReleased=true`；
- Windows GUI Automation 使用同一应用 workload 和报告通过，覆盖 30 个 UIA 控件、两轮场景矩阵、13 张截图和 Alt+F4 关闭；
- `AtomUI.City.Data.Tests` 为 300/300，`AtomUI.City.Presentation.Tests` 为 151/151；
- 首轮实跑发现并关闭两个 Data 回归：空 gRPC status detail 不再击穿错误映射，HTTP 响应体 IO 中断映射为 `TransportError` 并可进入幂等重试；
- 以上结论只证明本文件列出的确定性故障矩阵，不替代长时间网络抖动、资源趋势与 soak 测试。

## 8. Network Lab 扩展门禁

`network` profile 在 DTR-01..13 之后增加 11 个 wire-level 场景：HTTP healthy/latency/blackhole/reset，gRPC throttled/reset，SignalR reset/reconnect，HTTPS pinned certificate、logical DNS unavailable/recovered，以及 TLS pin mismatch。故障代理工作在 TCP 字节流层，不调用服务端测试 hook；每条连接有独立 cancellation，场景切换必须先终止旧连接，禁止连接池或黑洞任务污染下一场景。

该实验室不修改系统 DNS、不信任全局开发证书、不访问公网。HTTPS pinning 与逻辑 DNS 使用同一进程内 Kestrel 的独立 HTTPS/HTTP listener，以便把证书错误和名称解析错误分别归因；Windows HTTPS 测试需要用户密钥存储权限。逻辑 DNS 只验证客户端端点刷新和恢复纪律，不宣称覆盖操作系统 DNS cache；真实公网代理、NAT、Wi-Fi 漫游和企业 MITM 仍属于环境集成测试。
