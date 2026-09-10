# 产品级 Vertical Slice 验收规格

版本：v0.1
状态：强制执行
适用范围：AtomUI.City 首个端到端产品闭环、dogfood 测试和发布前验收

## 1. 目标

本规格定义 AtomUI.City 第一个产品级端到端闭环。

它不是业务示例项目的功能需求，而是框架验收场景：证明 Host、Module、Routing、MVVM、State、Data、Security、Localization、Presentation、PluginSystem、Build、Generators、CLI、Templates 和 Testing 能按文档合同协同工作。

只有该 vertical slice 通过后，才能说框架具备产品级全栈闭环，而不是只有独立模块能力。

## 2. 场景名称

`SalesDesk`

场景定位：一个桌面业务应用的最小销售工作台。

业务内容保持极简：

- 一个 `Sales` 模块。
- 一个 `/sales/orders` 页面。
- 一个订单列表 ViewModel。
- 一个通过 Data 管线获取的订单摘要请求。
- 一个权限守卫。
- 一个本地化标题和错误消息。
- 一个插件贡献的 `/sales/reports` 子页面。
- 一个插件卸载路径。

## 3. 范围

必须覆盖：

- 模板创建应用。
- 模块注册和生命周期。
- Source generator 输出 module、route、presentation、permission、localization 和 plugin manifest。
- RouteGraph 构建和导航。
- Security route guard。
- ViewModel activation 和 command execution。
- Data request pipeline、credential 注入、取消和错误映射。
- State 写入、订阅和释放。
- Localization lookup、fallback 和文化切换刷新。
- Presentation ViewLocator、ViewFactory、RouteOutlet commit 和 visual detach feedback。
- Plugin install、load、enable、route contribution、disable、contribution revoke 和 unload。
- Testing fake dispatcher、deterministic scheduler、diagnostics assertion 和 lifecycle cleanup assertion。
- CLI JSON 输出和 docs/tests gate。

明确不覆盖：

- 真实远程服务。
- 复杂业务领域模型。
- 真实插件市场。
- 多窗口高级交互。
- 大规模性能压测。
- 进程外不可信插件沙箱。

## 4. 端到端流程

```text
atomui city new app SalesDesk
-> build generates module/route/presentation/security/localization manifests
-> TestHost starts Core host
-> SalesModule initializes
-> navigate /sales/orders
-> Security guard allows current principal
-> Data request injects token and returns fake order summary
-> ViewModel writes State
-> Presentation resolves View and commits outlet through fake dispatcher
-> Localization resolves title
-> plugin package contributes /sales/reports
-> route graph publishes new snapshot
-> navigate /sales/reports
-> plugin unload rejects new plugin navigation
-> active plugin route scope closes
-> contribution leases revoke
-> old graph remains readable and new graph removes plugin route
-> host stops and all scopes dispose
```

## 5. 必须证明的合同

| 能力 | 必须断言 |
| --- | --- |
| Host lifecycle | Start、Stop、Dispose 幂等；失败进入 diagnostics；leaf-first 释放。 |
| Module graph | `SalesModule` 默认 id、依赖排序、初始化和 shutdown 顺序稳定。 |
| Build/Generators | manifest 输出稳定排序；runtime 不依赖 generator；无运行时程序集扫描默认路径。 |
| Routing | `/sales/orders` 匹配成功；guard deny 不提交；plugin route revoke 后发布新 snapshot。 |
| MVVM | ViewModel activation 后建立订阅；deactivation 释放订阅；command 取消不提交状态。 |
| State | 数据写入产生一次通知；相等值不重复通知；scope dispose 后不通知。 |
| Data | credential 在 transport 前注入；取消后不写缓存和 State；transport error 映射为 `DataResult`。 |
| Security | 未授权 principal 被 guard 拒绝；授权变化刷新 command 可执行状态。 |
| Localization | `Sales.Orders.Title` 成功 lookup；缺失 key 走 fallback；culture switch 通知应用拥有的可选刷新 bridge。 |
| Presentation | ViewLocator 不使用命名反射兜底；RouteOutlet commit 失败不替换旧 view；detach 触发 lifecycle feedback。 |
| PluginSystem | 插件 contribution 有 lease；disable 先拒绝新入口再 revoke；unload 失败进入 UnloadPending。 |
| CLI | JSON envelope 包含 command、status、diagnostics、artifacts 和 suggested next commands。 |
| Templates | 生成项目可 restore/build/test；不包含绝对路径；测试项目引用 Testing，不让生产项目引用 Testing。 |

## 6. 测试矩阵

| Feature Area | Test Type | Test File | Required Cases |
| --- | --- | --- | --- |
| Template creation | TemplateSmoke | `SalesDeskTemplateSmokeTests` | 生成 app、restore、build、test、无绝对路径。 |
| Generated manifests | Generator | `SalesDeskManifestGenerationTests` | module、route、view、permission、localization、plugin manifest byte-stable。 |
| Host lifecycle | RuntimeLifecycle | `SalesDeskHostLifecycleTests` | Start/Stop/Dispose 幂等、scope 释放顺序、diagnostics。 |
| Navigation to orders | FrameworkIntegration | `SalesDeskNavigationTests` | match、guard allow、resolver/data success、outlet commit。 |
| Guard deny | FrameworkIntegration | `SalesDeskSecurityTests` | 未授权不提交 navigation snapshot。 |
| Data cancellation | Unit | `SalesDeskDataPipelineTests` | 取消后不写 cache、State 或 UI。 |
| Localization refresh | FrameworkIntegration | `SalesDeskLocalizationTests` | culture switch 刷新 title，缺失 key fallback。 |
| Plugin route contribution | PluginLifecycle | `SalesDeskPluginLifecycleTests` | install/load/enable、route contribution、navigate plugin route。 |
| Plugin unload | PluginLifecycle | `SalesDeskPluginLifecycleTests` | reject new entry、close active scope、revoke lease、unload or UnloadPending。 |
| CLI envelope | Contract | `SalesDeskCliEnvelopeTests` | `new`、`build`、`docs check`、`tests check` JSON schema。 |

## 7. 诊断要求

每条失败路径必须断言诊断 code 和至少一个定位字段。

| 失败 | 必需字段 |
| --- | --- |
| Host start failure | `operationId`、`stage`、`module` |
| Route conflict | `routeId`、`owner`、`graphVersion` |
| Guard deny | `routeId`、`guardType`、`principalState` |
| Data transport failure | `operationId`、`transportKind`、`dataClientId` |
| Localization missing key | `culture`、`resourceScope`、`key` |
| Presentation commit failure | `outletName`、`viewModelType`、`viewType` |
| Plugin unload pending | `pluginId`、`version`、`remainingReferences` |
| CLI command failure | `command`、`exitCode`、`diagnosticCode` |

## 8. 发布门禁

该 vertical slice 进入发布门禁时必须满足：

- 所有测试文件存在。
- 所有 Required Cases 有明确断言。
- `engineering/check-docs.sh` 通过。
- `engineering/check-public-api.sh` 通过。
- 生成项目 `dotnet build` 和 `dotnet test` 通过。
- 插件生命周期测试覆盖 `Enabled -> Disabling -> Disabled -> Unloading -> Unloaded` 和 `UnloadPending`。
- 失败路径不会污染 Host root service provider。
- 生产项目不引用 `AtomUI.City.Testing`。

## 9. 实施顺序

该规格不改变实现顺序治理规范，也不改变 [全局 1.0 进度](../superpowers/plans/2026-06-11-development-tracking-plan.md) 的完成度口径。

实施顺序仍然是：

```text
Phase 0
-> Phase 1
-> Phase 2
-> Phase 3
-> Phase 4+
-> SalesDesk vertical slice
```

在 Phase 0 到 Phase 3 完成前，不允许为了 SalesDesk 提前实现 Routing、Presentation 或 PluginSystem 的完整动态能力。
