# 0016 Routing-Presentation 技术协调与条件恢复

状态：Accepted
日期：2026-10-21

## 背景

Routing 已能原子提交 `NavigationSnapshot` 与 journal，Presentation 已能原子接管候选并提交 `RouteOutlet`。两者之间仍由应用手工串接：Router 成功而 View 创建、绑定、激活或 Outlet 提交失败时，应用既缺少可验证的提交身份，也只能通过普通 `Back` 猜测恢复。普通 journal 导航会重新执行 guard/resolver、产生历史副作用，并可能撤销已经发生的更新导航。

## 决策

Routing 新增 `NavigationCommitReceipt`。每次成功提交生成单调递增 generation，并在回执中保存 previous/current snapshot；`IRouter.TryRestoreAsync` 只在回执属于当前 Router、该 generation 仍为当前提交且 current snapshot 身份仍匹配时恢复。恢复同时还原提交前的 journal checkpoint，并推进 generation，使同一回执不能重复使用。条件恢复不运行 guard、resolver、middleware，也不等价于 `BackAsync`。

Presentation 新增 `IRoutePresentationCoordinator` 与 `IRoutePresentationPlanFactory`。协调器按 `IRouteOutlet` 实例串行化完整技术流程：执行应用提供的 Router 导航委托、取得回执、创建单次 plan、提交 Outlet；若 plan 创建或 Presentation 提交失败，则用回执请求条件恢复，并返回结构化 `RoutePresentationResult`。

plan factory 仍由应用实现，负责 route 到 ViewModel/View/lease 的业务映射。factory 在成功返回前拥有并清理其部分构造资源；返回后协调器立即把 plan 交给 Outlet，Outlet 的既有单次 ownership 合同负责候选恰好释放一次。

## 影响

- Router 提交和 Presentation 提交仍是两个事务，不宣称视觉树、activation 或业务副作用具有分布式原子性。
- 恢复被后续导航拒绝时，协调器绝不撤销更新状态，而是返回明确结果供应用重新协调当前 Router 真相。
- Router 未提交、恢复成功、路由保持已提交、恢复被更新导航拒绝和结构性故障具有稳定状态。
- caller cancellation 只中止仍可安全中止的前向阶段；Router 已提交后的候选清理与条件恢复不使用已取消的 caller token。

## 后续约束

- 不得用 `BackAsync`、直接写 snapshot 或 journal 模拟条件恢复。
- 不得把 route/参数解析、错误页、重试、workspace 回收或业务补偿加入协调器。
- 新增或修改回执、恢复或协调结果必须同步 Routing/Presentation Public API、诊断、故障注入测试和兼容性文档。
- 应用绕过协调器直接调用 Router 与 Outlet 时，自行承担两阶段一致性责任。
