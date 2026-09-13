# Desktop Dogfood 测试方案

## 1. 测试目标

Dogfood 回答的不是“应用能否打开”，而是以下问题：

1. 九个运行期模块在同一个真实 Avalonia 应用中能否长期协作。
2. 大量跨模块事务遇到失败、取消、并发、窗口关闭和账号变化时能否收敛。
3. City 的开发者公开 API 是否全部有可重复证据。
4. 应用是否能被真实用户操作，而不只是自动测试专用程序。
5. 进程是否能稳定退出，不弹 Windows crash dialog、不残留后台连接和子进程。

单元测试全绿是前置条件，不是 Dogfood 通过条件。

当前已实现行为复杂度基线：真实显示 4 个 Avalonia Window，在同一会话运行 8 条 Service 工作流、全部 Route/ViewModel/Command 遍历、跨模块自动化、确定性失败探针和 12 个复合场景，随后由多个并发关闭调用方收束全部 Window、Host 和 loopback server。`PresentationDesktopProcessTests` 将 Quick 与 Standard 作为 Windows 子进程执行；Headless 入口已完成 20,000 动作实跑，并使用原始输入事件驱动 30 个真实控件。Soak、Extreme 和 API profile 目前仅复用统一动作驱动，尚未完成本文件要求的全部专项门禁。

## 2. 运行模式

| Profile | 目标 | Window | 规模 | Watchdog |
| --- | --- | --- | --- | --- |
| `manual` | 开发者持续人工使用 | 可见，不自动退出 | 无固定上限 | 无；提供安全退出 |
| `gui` | Windows 外部系统输入门禁 | 4 个真实 Window | 30 个 UIA 对象 + 单场景/双矩阵 + 系统输入 + 截图 | 8 分钟 |
| `quick` | PR 基本闭环 | 真实可见或 Windows test desktop | 约 500 UI/业务动作 | 90 秒 |
| `standard` | 每日完整验证 | 真实 desktop | 约 10,000 动作 | 10 分钟 |
| `soak` | 长时间资源和顺序 | 真实 desktop，允许最小化 | 2 小时/不少于 250,000 动作 | 150 分钟 |
| `extreme` | 队列、竞态、故障风暴 | 真实 desktop | 100,000 混沌动作 | 30 分钟 |
| `headless` | Avalonia.Headless 快速并发与控件交互 | Headless Window/VisualTree | 20,000 动作 + 30 控件 + 场景取消恢复/矩阵门禁 | 5 分钟 |
| `api` | 全公开 API 正负路径 | 按 API 需要选择 | coverage manifest 全量 | 15 分钟 |

所有自动 profile 接受 `--seed`、`--report`、`--artifact-root` 和 `--keep-open-on-failure`。CI 默认不保留失败 Window；本地可选择保留以人工检查。

## 3. 自动驾驶原则

12 个跨模块复合场景及其 operation identity、故障计划和逐场景验收以 [SCENARIO-ORCHESTRATION.md](SCENARIO-ORCHESTRATION.md) 为准。Headless 与 Windows GUI 必须通过同一组场景控件调用同一个编排器，不允许测试侧复制一套简化业务流程。

HTTP、gRPC、SignalR 的进程内故障和独立服务进程 crash/restart 以 [DATA-TRANSPORT-RESILIENCE.md](DATA-TRANSPORT-RESILIENCE.md) 为准。所有自动 profile 必须执行同一应用工作负载并从 `data-resilience` coverage 读取结果，不允许仅用测试代码直接探测服务端后宣称 City Data 已覆盖。

Automation 不直接调用私有字段把结果改成“成功”。它通过三层入口操作应用：

1. Avalonia control command、selection、text input 和 Window close event；
2. ViewModel command/Interaction，用于难以稳定合成 OS 输入的操作；
3. 公开 framework API，用于没有可见 UI 的负向和生命周期合同。

至少 60% 的正常业务动作必须从真实 Avalonia control 进入，且经过 binding/command。导航结束后读取真实 Window/Outlet/VisualTree/DataContext；不能只检查 ViewModel state。OS 级鼠标/键盘自动化不是第一阶段硬依赖，但 manual checklist 必须执行真实点击、输入、缩放、最小化/恢复和系统关闭。

已实现的 Headless 控件门禁通过 `Avalonia.Headless` 向内存 Window 注入 pointer、key、text 和 wheel 原始事件，不直接调用目标控件的 command 或业务方法。门禁检查 30 个唯一 `AutomationId`、6 个导航入口、搜索校验/成功/取消、MVVM 执行状态、Data 调用、EventBus 发布、State 提交、中英文投影、在线/离线受限账号切换、单场景、矩阵取消恢复、完整 12 场景矩阵、双向 binding、列表选择、滚动和 100%/150%/200% 渲染帧；每帧同时验证尺寸和采样像素差异，并使用 3 个固定 seed 运行完整进程。输入不接管系统鼠标；IME、操作系统焦点、窗口最小化/恢复、真实 DPI/多显示器仍属于 Windows desktop/manual 门禁。

Windows GUI 门禁只在 `ATOMUI_CITY_RUN_INTERACTIVE_GUI=1` 时执行。驱动先以 Win32 HWND 绑定 4 个 Avalonia Window，再通过 UIA `AutomationId` 获取物理坐标和状态；关键操作必须使用 `SendInput`，不能以 `InvokePattern` 代替点击。测试保存系统屏幕截图、验证像素差异、恢复鼠标位置并读取 GUI/Application 双报告。普通 `dotnet test` 不得接管系统输入。

每一步记录 action id、operation id、route、Window/Outlet、principal revision、culture revision、结果和耗时。失败报告保留最近 200 个动作，不包含敏感 payload。

## 4. 正常业务阶段

| 阶段 | 操作 | 主要模块 |
| --- | --- | --- |
| A | Build/Start 52 个 Host module，打开主 Window 并注册 4 个 Outlet | Core, Presentation |
| B | 登录模拟 Operator，装载权限、token context、租户和中英文资源 | Security, State, Localization |
| C | 遍历 96 条静态 route，验证 typed/path/deep-link 与 View commit | Routing, Presentation, MVVM |
| D | 创建商品、价格、促销、库存、客户与订单 | Data, State, EventBus |
| E | 执行税、账单、风控、支付、履约、物流和退货链 | 全部业务模块 |
| F | 打开订单、客服和诊断多 Window，交叉导航和编辑 | Presentation, Routing, MVVM |
| G | SignalR 推送库存、价格、订单和物流；UI 实时收敛 | Data, EventBus, State, Presentation |
| H | 生成报表、流式导出、大文件下载和断点续传 | Data, MVVM, State |
| I | 切换 tenant/principal/permission，验证 route、command、cache | Security, Routing, Data, State |
| J | 连续切换 100 次语言，同时导航、推送和关闭 Window | Localization, State, Presentation |
| K | attach/revoke seasonal contribution，验证跨模块回收 | EventBus, Routing, Data, Localization, Presentation |
| L | Back/Forward/redirect/restore，处理 stale journal | Routing, Presentation |
| M | 用户关闭被 dirty guard 拒绝，保存后关闭；应用和 OS 关闭 | MVVM, Presentation, Core |
| N | 停止 Host，检查 Event/Data/Window/Scope 的逆序清理 | 全部运行期模块 |

每一阶段可以独立执行，也可以在同一进程完整执行。独立执行由相同 bootstrap 建立真实 Host，不允许构造简化 fake host。

## 5. UI 操作矩阵

自动与人工至少覆盖：

- 左侧 navigation 点击、键盘选择、搜索导航和 URI deep link；
- 列表分页、排序、筛选、快速重复选择和 master-detail；
- 新建/编辑/撤销/保存表单，Validation 成功、警告和错误；
- command disabled/hidden、执行中、取消、失败和重试；
- 模态确认、选择器、无 handler、handler revoke、队列满载；
- 主/命名 Outlet 同时更新，弹出工作区 Window，再合并关闭；
- `en-US`/`zh-CN` 切换，长中文/英文、格式化数字日期和 missing marker；
- 窗口缩放至 800x600、1920x1080、150% DPI；检查文字不遮挡、按钮不变形；
- Window 最小化/恢复、失焦/聚焦、多 Window 并行操作；
- 用户关闭、应用菜单退出、Host stop 和操作系统关闭来源。

视觉验收不要求像素完全相同，但必须保存关键 checkpoint 截图，并检查主 Window 非空、Outlet 内容和 DataContext 正确、没有明显重叠或裁切。

## 6. 故障注入矩阵

### 6.1 Core

- Module constructor、三个配置阶段、Starting/Started/Stopping/Stopped hook 分别失败。
- middleware 在调用 next 前、next 中和 next 后失败；保存后迟到 next、重复 next。
- diagnostics sink 抛异常；scope child stop/dispose 多项失败。
- 并发 Start/Stop/Dispose、Stop-before-Start 和 UI close 同时发生。

### 6.2 EventBus

- handler 同步/异步异常、超时、取消、连续失败禁用。
- 12 个 channel 分别达到 capacity，验证 Wait/Reject/DropOldest/DropNewest/CoalesceLatest。
- serialized handler 递归 publish 并等待自身；partition key 缺失/误用。
- owner stop 与 subscribe/publish 竞争；contribution drain timeout。

### 6.3 State

- updater、subscriber、dispatcher、computed 函数抛异常。
- computed 首算失败、依赖恢复、直接/动态循环、dispose/invalidate 竞态。
- registry 并发 add/read/snapshot；五种 access policy 越权。
- collection 重复 key、restore 版本/schema 不兼容、批量中途失败。

### 6.4 Routing

- guard Reject/Cancel/Redirect/Failed；resolver null/throw/cancel/redirect。
- middleware short-circuit、post-next throw、invalid result、late next。
- redirect cycle/超过 8 次、timeout、三种并发策略。
- route contribution 在 match/resolve/adapter commit 期间撤销。

### 6.5 MVVM/Presentation

- activation/deactivation/confirmation/factory/lookup/bind/dispatcher 失败。
- Outlet temporary attach 后失败、rollback 失败、旧 Entry cleanup 失败、presenter 失败。
- candidate plan 重复、queue full、caller/lifecycle cancellation。
- 多 confirmation owner、Window close/Dispose/Host stop 竞态。
- Interaction handler throw/cancel/revoke/full；Validation target throw。

### 6.6 Localization/Security/Data

- locpack 缺失/损坏/checksum/version/oversize、provider throw、load cancel、bridge throw、missing key/format throw、revoke/load 竞态。
- auth provider failed/expired/unavailable/cancel；permission/policy 缺失和 evaluator throw。
- HTTP 401/403/404/409/429/500/503/504、连接重置和 malformed payload。
- gRPC 全标准 status、deadline、四类 stream 半途断开。
- SignalR start/reconnect/invoke/push 失败和 sequence gap。
- cache read/write/invalidate failure、circuit open、rate limit、fallback throw。
- upload/download cancel、长度不符、range 不支持和临时文件删除失败。

## 7. 确定性竞态

不用“多跑几次碰碰运气”验证并发。`FaultGateRegistry` 在关键阶段提供到达/放行 gate，至少实现：

1. Router 已提交、Outlet 尚未 admission 时关闭 Window。
2. Outlet 临时 attach、ViewModel activation 尚未完成时撤销 route contribution。
3. culture package load 完成、commit 前撤销 owner。
4. SignalR 收到旧 principal revision，账号切换刚提交。
5. Data query 已读 cache miss，mutation invalidation 后才返回。
6. Event subscription 已预留、owner scope 开始 stop。
7. computed 正在计算，依赖并发改变并触发 dispose。
8. Window close confirmation 正在等待，Host stop 开始不可拒绝关闭。
9. large payload 已创建 temp file，取消与成功 rename 竞争。
10. connection registration 提交与 DataModule shutdown 竞争。

每个 gate 都有最大等待时间，测试失败时自动释放，避免测试基础设施自身造成永久 hang。

## 8. Seeded chaos

`standard/extreme` 使用 8 个 worker 从同一 seed 生成动作，但每个动作先取得显式逻辑 sequence。动作包括导航、命令、State update、Event post、HTTP/gRPC 请求、SignalR push、语言切换、权限变化、Window open/close、contribution attach/revoke 和 fault toggle。

允许中间结果随线程调度不同，但最终必须满足模型不变量；与顺序相关的 channel/partition/outlet 使用 ledger 对比接受顺序。失败报告输出 seed 和最小可重放动作前缀。

## 9. 长时间与资源测试

Soak 每 5 分钟记录：

- process working set、managed heap、GC count、thread count、handle count；
- EventBus channel pending/in-flight/active subscriptions；
- Presentation Outlet/Interaction pending/in-flight/peak/rejected；
- Data active request/stream/subscription/connection；
- Window、View、ViewModel、ActivationScope、State subscription 和 contribution lease 数；
- diagnostics 增长率和未观察 Task exception。

warm-up 后 retained managed memory 的线性趋势必须接近零；短期 cache 可增长到配置上限，达到上限后稳定。关闭后所有 queue pending/in-flight 为 0，Window 为 0，连接和 subscription 为 0。被替换 View/ViewModel/contribution 用 WeakReference 在最多三轮强制 GC 后验证可回收。

实现参数固定为：每 30 秒采样、每 5 分钟执行三轮 full GC retained checkpoint。正式 `burn-in` 为 15 分钟，正式 `soak` 同时要求不少于 2 小时和 250,000 个动作。混合门禁为 managed heap 斜率不超过 8 MiB/hour、最终 managed heap 增量不超过 `max(32 MiB, baseline * 20%)`、working set 增量不超过 `max(128 MiB, baseline * 30%)`、线程增量不超过 8、Windows handle 增量不超过 16。短 profile 只执行 shutdown/quiescence 门禁，不用两个时间距离过近的样本计算泄漏结论。

Security/Data 长时门禁使用 5 分钟 access token、2 分钟 token 提前刷新窗口、10 分钟权限提前刷新窗口和 30 分钟续期权限快照。启动探针对同一短有效期 resource 并发发起 16 次请求，必须全部成功且只计一次 token 刷新和一次权限刷新；任何 `MinimumDuration >= 7 分钟` 的自动化还必须累计至少 3 次 token 刷新，`MinimumDuration >= 35 分钟` 还必须累计至少 2 次权限刷新。权限刷新先原子回写账号快照，再调用 `RefreshAccountAsync` 发布一个完整新 revision。默认 `AccountSessionManager` 不负责网络刷新，该协调器属于 Dogfood 应用组合层。

长时运行结束后的场景矩阵还必须覆盖非活动账号恢复：目标账号的权限快照和业务 resource 凭据即使已经过期，Dogfood 应用组合层也要先模拟服务器同步并更新两个持久化存储，再调用 `SwitchAccountAsync`。Security 只负责验证和提交完整会话，不得把过期缓存静默提升为 Online。启动验证后会把 Alice 重置为过期的非活动账号，启动期另用一个过期临时账号验证存储准备流程，使该合同无需等待两小时也能确定性回归。

长时运行结束后的场景矩阵还必须覆盖非活动账号恢复：目标账号的权限快照和业务 resource 凭据即使已经过期，Dogfood 应用组合层也要先模拟服务器同步并更新两个持久化存储，再调用 `SwitchAccountAsync`。Security 只负责验证和提交完整会话，不得把过期缓存静默提升为 Online。启动期另用一个过期临时账号执行同一准备流程，使该合同无需等待两小时也能确定性回归。

网络专项完全离线运行：HTTP、gRPC、SignalR 通过 loopback TCP fault proxy 注入 latency、throttle、blackhole、reset 和 reconnect；本地 HTTPS 服务使用临时自签名证书与 thumbprint pin；逻辑 DNS 使用 `ConnectCallback` 切换解析结果，每次 DNS 周期创建新连接池以避免旧连接掩盖解析行为。

## 10. 验收不变量

| ID | 不变量 |
| --- | --- |
| D01 | 48 个应用 Module 和 4 个 City Module 恰好进入预期依赖闭包，canary 不构造 |
| D02 | 110 个业务 Service 均可解析、被调用且 lifetime/dispose 次数正确 |
| D03 | 72 个 Event contract 均投递成功，全部 channel 顺序/背压与指标一致 |
| D04 | 72 application、20 computed、16 collection、20 scoped State 全部被实际操作 |
| D05 | 96 条静态 route 与 6 条动态 route 均完成匹配、事务和 journal 验证 |
| D06 | 64 个 ViewModel、82 个 View descriptor、96 个 command 均有执行证据 |
| D07 | Router committed snapshot 与各 Outlet Entry 最终收敛；不一致可诊断并可 reconcile |
| D08 | 四类 Window 和九种 Outlet 均使用真实 Avalonia visual attach/detach |
| D09 | 480 个 localization key 完整性通过，切换后可见文本 culture revision 一致 |
| D10 | 64 permission、24 policy、36 guarded route、40 authorized command 均经过 allow/deny |
| D11 | 正常 profile 的 HTTP/gRPC/SignalR 均走真实 loopback server，不走 fake transport |
| D12 | 取消后不提交 State、cache、Event 或 UI；已提交事务完成 owner consistency work |
| D13 | 账号/tenant/permission revision 改变后旧 Data/route/command 结果不再提交 |
| D14 | contribution revoke 后 route/view/resource/text/handler/cache/connection 均不可继续使用 |
| D15 | Window/Outlet/Interaction/Event/Data 有界队列达到容量时按合同拒绝或背压，不无界增长 |
| D16 | 所有 fault 都有稳定 result/exception/diagnostic，诊断 sink failure 不覆盖主失败 |
| D17 | 用户可拒绝关闭；Application/OS/Host 强制关闭最终都能完整收束 |
| D18 | Host stop 后没有活动 Window、scope、subscription、connection、stream 或后台 task |
| D19 | 日志、State、diagnostics、截图 metadata 和 report 不含 token/refresh token/完整 claims |
| D20 | public API inventory `unclassified=0`，required coverage `uncovered=0` |
| D21 | Security 008/009 使用框架文件 Provider 完成 3 账号切换、离线受限、删除和 process-like 重启恢复，且 Data 使用当前账号资源凭据 |
| D22 | 子进程正常退出码 0；失败退出码非 0；watchdog 超时可终止且不弹系统 crash dialog |
| D23 | Headless 原始输入可唯一命中 30 个控件，并经 control/binding/command 驱动 Router、MVVM、Data、EventBus、State、Localization、Security 与 Presentation；场景取消后恢复且 12 个场景全部留证；3 个缩放等级均产生尺寸正确且具有像素差异的渲染帧 |
| D24 | Windows GUI 驱动发现 4 个真实窗口和 30 个 UIA 对象，完成单场景和两轮矩阵，且系统输入、窗口操作、截图、Alt+F4、鼠标恢复及 GUI/Application 双报告全部通过 |
| D25 | DTR-01 至 DTR-13 在所有自动 profile 中执行；独立 Data 子进程真实 crash 后在相同 HTTP/gRPC 端口重启，既有 HTTP client、gRPC channel、SignalR connection 全部恢复且子进程最终释放 |
| D26 | NET network matrix 全部在 deadline 内收束；TLS pin、DNS 失效恢复、HTTP/gRPC reset 和 SignalR reconnect 均有独立报告证据 |
| D27 | 100 个七阶段合成 contribution 完成；前七轮逐阶段故障均逆序回滚，并发撤销收敛且三次 GC 后 sentinel 全部回收 |
| D28 | 正式 soak 至少运行 2 小时并达到动作目标；managed heap/working set/线程/handle 通过趋势门禁，shutdown 后 Window、Outlet、EventBus 和 Presentation 队列归零且无未观察异常 |
| D29 | 16 个并发短有效期 token 请求合并为一次刷新并发布一次权限快照续期；7 分钟以上运行至少完成两次主凭据续期，35 分钟以上至少完成第二次权限续期；OfflineRestricted 账号对仍有效的次级 resource token 也返回 Expired，切回 Online 账号后 Data 与场景恢复 |

## 11. 输出报告

每次自动 run 生成：

```text
run-report.json          # profile、seed、版本、计数、不变量、API coverage
timeline.ndjson          # 最近动作和 operation correlation
diagnostics.json         # 脱敏后的各模块 diagnostics
resource-snapshots.json  # 内存/队列/连接/ownership 时间序列
network-chaos-report.json # 本地 TLS/DNS/TCP 故障矩阵
contribution-lifecycle-report.json # 动态贡献阶段、回滚与回收结果
api-coverage.json         # 九程序集公开面、哈希、分类与证据
screenshots/             # 关键真实 UI checkpoint
failure.txt              # 首个主失败和全部 cleanup failure
```

当前生成 `run-report.json`、`resource-snapshots.json`，并由对应专项生成 network、contribution 和 API 报告。timeline/diagnostics 的独立文件和通用原子替换仍是后续工作；失败路径会尽量生成主报告，但进程级崩溃无法保证落盘。

## 12. 分层测试与发布门禁

1. 现有各模块 unit/contract/generator tests 全绿。
2. Dogfood domain tests 验证 48/110/72/128/96 静态目录和纯业务模型。
3. Avalonia.Headless 跑并发与故障 profile，并通过 30 控件、场景取消恢复和完整矩阵的原始输入与跨模块报告断言。
4. Windows desktop child process 跑 quick/standard，Window 必须真实 Show。
5. opt-in `InteractiveDesktop` 使用系统鼠标键盘跑 `gui` profile，并保留截图和双报告。
6. manual checklist 至少完成一次连续 30 分钟操作。
7. soak 在发布候选前完成并附 machine report。
8. 公开 API coverage gate、Release 零 warning build、SourceLink/package consumer 与既有 Presentation RC 并行通过。

Dogfood 失败应首先归因到应用、测试基础设施或 City 模块。只有可最小重现且违反对应模块文档合同，才登记为 City 缺陷；不得为了让 Dogfood 变绿而在应用中吞异常、无限重试或跳过释放断言。
