# Desktop Dogfood 复合场景编排 V2

## 1. 目标

本轮不再增加 Module、Service、Event、State、Route 或 ViewModel 的类型数量，而是把已有资产组织成可重复、可取消、可验证的跨模块业务事务。目标是让缺陷更容易暴露和定位，而不是只增加动作计数。

每次场景执行必须同时满足：

1. 由真实 MVVM Command 或自动 profile 发起；
2. 至少经过 Service、EventBus、State、Data、Routing 和 Presentation；
3. 需要身份或语言语义的场景额外经过 Security 或 Localization；
4. 具有固定 `scenarioId`、由 seed 派生的 `operationId`、`correlationId` 和 `causationId`；
5. 在 `run-report.json` 中留下独立结果和不变量证据；
6. 取消、预期故障或补偿不能产生半提交的 State、Route 或 UI；
7. 同一进程内可以重复运行，结果不得依赖上一轮遗留的临时资源。

## 2. 边界

- 场景编排属于 Dogfood 应用业务代码，不进入任何 City 基础模块。
- 编排器只使用开发者可获得的 City API，不读取模块私有字段。
- Router 成功后由应用 adapter 提交 Presentation；不伪造跨模块原子事务。
- 文案和 culture fallback 仍只由 Localization 处理；Presentation 只承接 UI 更新。
- Security 账号报告只保存模式和不可逆摘要，不保存 token、refresh token 或完整 claims。
- PluginSystem、真实 ALC 卸载、两小时 soak、全量 exported API inventory 不属于本轮完成条件。

## 3. 场景目录

| ID | 场景 | 主要竞争或故障 | 必经模块 | 核心不变量 |
| --- | --- | --- | --- | --- |
| S01 | 账号上下文重绑定 | Alice/Admin 连续切换 | Security、State、EventBus、Data、Routing、Presentation | 最终账号、State revision、UI route 一致 |
| S02 | 搜索请求抢占 | `LatestWins` 旧请求失效 | MVVM、Data、State、EventBus、Routing、Presentation | 旧结果不得覆盖新结果 |
| S03 | 订单提交与缓存失效 | mutation 后重新查询 | Data、Service、State、EventBus、Routing、Presentation | 提交成功且后续查询可见 |
| S04 | 支付失败补偿 | 服务链中途故障并逆序补偿 | Service、EventBus、State、Routing、Presentation | 已完成步骤全部且仅补偿一次 |
| S05 | 库存事件突发 | 多次库存事件和投影 | EventBus、State、Data、Routing、Presentation | revision 单调、最终库存投影收敛 |
| S06 | 离线账号恢复 | Bob 离线受限后恢复 Admin | Security、Data、State、EventBus、Routing、Presentation | 离线模式可辨识，恢复后无跨账号提交 |
| S07 | 语言与导航竞争 | `zh-CN/en-US` 与连续导航交错 | Localization、State、EventBus、Routing、Presentation | 最终 culture revision 与可见投影一致 |
| S08 | Route/Outlet 连续提交 | 同一 Outlet 多次 Replace | Routing、Presentation、MVVM、State、EventBus | Router 真相与 Outlet 当前 Entry 收敛 |
| S09 | 账号、语言、搜索组合 | 三种上下文连续变化 | Security、Localization、Data、State、EventBus、Routing、Presentation | Data 结果只归属最终上下文 |
| S10 | 事件扇出与审计 | 业务事件和审计事件成对发布 | EventBus、Service、State、Routing、Presentation | publication 增量与场景证据一致 |
| S11 | 取消后恢复 | Data 请求被取消后立即重试 | MVVM、Data、State、EventBus、Routing、Presentation | 取消是终态，恢复请求成功 |
| S12 | 全域收敛 | 归一到 Admin、en-US、Dashboard | Core、Security、Localization、Data、State、EventBus、Routing、MVVM、Presentation | 所有最终快照形成一致检查点 |

每个场景还要执行一个由 Service catalog 切片形成的多阶段调用图。切片以 4 个并行分支为一组传递摘要；S04 和 S11 在确定阶段注入预期故障，并对已完成服务逆序补偿。

## 4. 执行模型

场景编排器是进程级单例，但每批执行创建独立 batch：

```text
UI Command / automated profile
-> bounded single-run gate
-> select scenario catalog entries
-> derive deterministic operation identity from seed + batch + scenario
-> run service slice
-> apply Security/Localization context transition when required
-> issue Data query/mutation/cancellation pair
-> mutate State and publish business + audit events
-> navigate twice and commit the resulting ViewModel to an Outlet
-> evaluate cross-module invariants
-> append immutable scenario evidence to run ledger
```

并发调用不允许重叠执行两个 batch。后到调用者在门禁处等待，并受调用方 cancellation 控制。`Cancel` 只取消当前 MVVM Command；已经完成并提交的场景保留证据，未完成场景不得写入成功结果。

## 5. 确定性与故障计划

- `operationId` 只由固定 seed、batch sequence 和场景索引生成，不使用随机 GUID。
- `correlationId` 等于 `scenarioId + operationId` 的稳定文本形式。
- `causationId` 标识入口：`gui`、`headless` 或自动 profile。
- S04 在服务切片中点失败；S11 先启动慢请求，再由同资源的新请求使旧请求进入 `Cancelled` 或 `StaleSuppressed`。
- 故障属于场景定义的一部分，不由墙上时钟或非确定随机数触发。
- 所有等待都有 timeout 或上层 lifecycle cancellation；测试禁止无限等待。

## 6. UI 合同

主窗口交互区新增以下稳定 `AutomationId`：

- `dogfood-scenario-selector`
- `dogfood-scenario-run`
- `dogfood-scenario-run-matrix`
- `dogfood-scenario-cancel`
- `dogfood-scenario-status`
- `dogfood-scenario-summary`

UI 必须支持选择并单独执行一个场景、执行完整矩阵、取消当前矩阵，以及显示最终完成数和跨模块动作摘要。场景列表是固定目录，不允许运行期静默跳过失败项。

场景状态和摘要属于动态高度内容。交互面板必须在主窗口最小尺寸 `1080 x 700`、矩阵摘要换行以及状态文本增长时保持全部控件可达；空间不足时由面板自身提供纵向滚动，不得裁切末尾命令或把内容覆盖到 Activity Outlet。Headless 与 Windows GUI 截图复核均属于该布局合同的验收证据。

## 7. 报告合同

`run-report.json` schema 升级为 2，并新增 `scenarioResults`。每条结果至少包含：

- batch sequence、scenario id、operation id、correlation id、causation id；
- completed/compensated 状态和耗时；
- Service calls、Data operations、Event publications、State mutations、navigations；
- 最终 culture、account mode、模块覆盖和逐项 invariant 名称。

报告写出前继续执行凭据泄露扫描。场景失败必须进入主故障并令自动门禁失败，不能只显示在 UI 后继续以成功退出。

## 8. Profile 与验收

| Profile | 场景入口 | 最低要求 |
| --- | --- | --- |
| Quick/Standard/Soak/Extreme/Api | coordinator 自动执行完整矩阵 | 12/12 成功 |
| Headless | Avalonia.Headless 原始控件执行单场景、取消和完整矩阵 | 至少 13 条成功证据；取消后可恢复 |
| Gui | Windows UI Automation + `SendInput` 执行单场景和两轮完整矩阵 | 至少 25 条成功证据；截图和应用报告一致 |
| Manual | 开发者使用相同控件触发 | 不作为自动发布门禁 |

本轮通过条件：

1. 12 个场景 ID 全部出现在报告中；
2. 每条成功证据至少覆盖 6 个运行期模块、2 次真实 Route/Presentation 提交、2 次 EventBus publication、2 次 Data pipeline 调用和一个多阶段 Service 切片；
3. Headless 三个固定 seed 全部通过；
4. Windows GUI 使用真实系统输入完成 1 个单场景和 2 个矩阵，应用正常退出、鼠标位置恢复；
5. 运行报告无凭据、无主故障，全部 Window/Outlet/Scope/连接资源被释放；
6. Presentation 测试程序集全绿，Release build 为零 warning、零 error。

## 9. 当前实现基线

V2 场景编排已落地为应用级统一编排器，12 个场景均执行 24 个 Service 调用、2 次 Data pipeline、2 次 EventBus 发布、3 次 State mutation 和 2 次 Router/Presentation 提交；S04、S11 额外执行确定性故障与逆序补偿。

Headless 门禁使用 3 个固定 seed，完成单场景、运行中取消、取消后恢复和完整矩阵；Windows GUI 门禁使用 30 个 UIA 控件与系统输入完成 S12 和两轮矩阵，并输出 13 张截图及 GUI/Application 双报告。交互面板在标准尺寸和 `1080 x 700` 最小尺寸下均通过截图复核，动态文本继续由纵向滚动兜底。

## 10. 合成 Contribution 事务

`contribution` profile 使用唯一 `seasonal-operations-NNN` ID 依次激活 EventBus lease、Routing contribution、Localization package、Data contribution、Security permission/policy、Presentation view/resource 和 scoped State。前七轮分别在一个阶段提交后注入失败；宿主协调器按阶段逆序回滚，同阶段由四个并发调用者验证幂等收敛。

每轮撤销后检查 route、language package、permission、policy 和 Presentation resource 均不可见，最终对 resource sentinel 执行最多三轮强制 GC，要求 retained 为零。该测试验证宿主编排和现有 contribution/lease 合同，不加载真实插件程序集，也不证明 ALC 卸载或签名信任链。
