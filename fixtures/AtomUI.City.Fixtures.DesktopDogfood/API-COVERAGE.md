# Public API 覆盖设计

> 当前状态：本文件定义最终 API 覆盖门禁，不是当前通过报告。运行账本已经记录 Service、Route、ViewModel、Command、并发和跨模块动作覆盖点，但 `api-coverage.json`、exported API inventory、BUILD/NEG 全量 profile 及 `required-uncovered=0` 尚未落地，因此不得把行为账本等同于全部开发者公开 API 覆盖。

## 1. 目标定义

“使用全部公开给开发者的 API”不能靠人工印象判断。Dogfood 实现必须建立机器可检查的 API inventory，并满足：

1. 9 个运行期模块全部 Completed/Verified 且未 Retired 的 Feature 有场景证据；Testing/Generators/Build/CLI/Templates 的开发者工具入口有工程场景证据。
2. `api-contracts.md` 的 Public 类型和关键方法全部映射到 coverage point。
3. assembly 新增 public API 后，未分类项立即让门禁失败。
4. 可调用成员至少被一个成功路径或失败路径真实执行；纯 descriptor/result/snapshot 成员必须被构造和断言。
5. Attribute、Generator、DI extension 等编译/组合入口由 build fixture 覆盖，不强行在 UI 点击路径重复。
6. 明确不属于开发者入口的 public 实现类型也必须分类并说明由哪个接口场景间接覆盖。

覆盖并不等于每个 API 只调用一次。生命周期、并发、取消、故障、释放和 enum 分支都要分别记 coverage point。

## 2. 权威输入

实现阶段生成并提交 `Automation/api-coverage.json`，输入来源为：

- `docs/modules/<module>/api-contracts.md` 的关键方法与 Public 类型表；
- `docs/modules/<module>/features.md` 的 Feature 索引；
- `PublicAPI.Shipped.txt`/`PublicAPI.Unshipped.txt`（存在时）；
- 编译后 assembly 的 exported type/member inventory；
- Generator 输出的 module/service/event/route/view/localization/data manifest。

若文档、PublicAPI baseline 与 assembly 不一致，门禁先报 contract drift，不允许通过给 coverage manifest 添加任意豁免来掩盖。

## 3. Coverage 分类

| Code | 分类 | 证据 |
| --- | --- | --- |
| `UI` | 人工和自动驾驶均经过的真实 UI 路径 | command、route、Window/VisualTree、report ledger |
| `RT` | 自动化运行期正向路径 | framework result + state/diagnostic/ownership 断言 |
| `NEG` | 非法输入、故障、取消、释放后路径 | 稳定 exception/result/diagnostic 断言 |
| `BUILD` | Attribute、Generator、DI/Module 组合入口 | generated source/manifest/build diagnostic |
| `MODEL` | immutable descriptor/result/snapshot/enum | 构造边界、全部属性和 enum 值断言 |
| `INDIRECT` | public implementation 只由接口/DI 间接消费 | interface scenario + resolved concrete identity |
| `BLOCKED` | 文档已规划但源码尚不存在 | 必须引用 Feature ID 和解除条件，不算 Passed |
| `RETIRED` | 1.0 前撤销且编号保留 | 断言程序集没有重新出现该 API |

`IGNORED`、`N/A` 或无理由 wildcard 不允许出现。

## 4. 运行时探针

每个场景在真实调用边界记录稳定 coverage point：

```text
Module.Feature.ApiMember.Path
例如：Presentation.004.IRouteOutlet.CommitAsync.QueueFull
```

探针只记录 API id、scenario id、start/completed/failed/cancelled 和 operation id，不记录业务 payload。探针不能代替行为断言；每个 point 必须关联至少一个 invariant。`run-report.json` 输出已命中、未命中和 blocked 列表。

为避免“只记探针、不调用 API”，测试还要用普通 .NET code coverage 验证对应 public method 的执行，并抽样进行 mutation/fault gate。API inventory 变化、probe 映射变化和 scenario 变化必须同一提交 review。

## 5. Feature 覆盖总账

| Module | 应覆盖 Feature | 暂不计 Passed |
| --- | --- | --- |
| Core | AUC-CORE-001..008 | 无 |
| EventBus | AUC-EVENTBUS-001..009 | 009 的真实 PluginSystem/ALC 集成；EventBus 侧 contract 仍覆盖 |
| State | AUC-STATE-001..008 | 真实 PluginSystem owner 集成 |
| Routing | AUC-ROUTING-001..012 | 真实 PluginSystem unload；route contribution contract 仍覆盖 |
| MVVM | AUC-MVVM-001..006 | 无 |
| Localization | AUC-LOCALIZATION-001..008 | 真实 PluginSystem unload；package revoke contract 仍覆盖 |
| Security | AUC-SECURITY-001..009 | 无 |
| Data | AUC-DATA-001..020 | 真实 PluginSystem unload；contribution contract 仍覆盖 |
| Presentation | 001..012, 014..016 | 013 `RETIRED`；真实 PluginSystem unload |
| Testing/Generators/Build/CLI/Templates | 各自当前 Completed/Verified Feature | 不进入运行期 Feature 计数，由 test/build/tooling profile 覆盖 |

合计 95 个当前运行期 Feature 家族进入 Dogfood 总账；Retired Presentation 013 不得伪装为运行通过。

Testing 的 scripted transport/credential、recording handler、fake connection/dispatcher/scheduler 和 test host 只进入明确的故障 profile；正常业务 profile 禁止解析这些替身。Generators/Build/CLI/Templates 通过隔离临时工程执行 create/build/generated-source/manifest/negative-diagnostic/package-consumer 流程，不能仅因 Dogfood 工程自身编译成功就标记全覆盖。

## 6. Core API 场景

| Family | Dogfood 证据 |
| --- | --- |
| Host builder/context/options | 正常 build/start/stop/run；configuration/provider freeze；ApplicationId/Name/Version/InstanceId/path/context immutable；重复 Build 负向 |
| Module | 默认 generated root、显式 UseModule、required/optional dependency、未选模块、ModuleDescriptor/Origin、三配置阶段、启动/停止/回滚 |
| DI marker/generator | Service/ScopedService/ExposeServices/owner、marker interface、keyed、多 contract、TryAdd/Replace 冲突编译 fixture |
| Lifecycle pipeline | 全 stage middleware、short circuit、`await next`/`return next`、single-next、失败归因、operationId、取消和 rollback |
| Lifecycle scope | Application/Module/PluginContribution/Operation 树、parent-child stop、并发 dispose、Stop-after-Dispose |
| Diagnostics | 全稳定 code 常量、record validation、sink failure isolation、snapshot、Complete 后拒绝写 |
| UI dispatcher | 启动前 Unavailable；Avalonia attach 后 inline/background/cancel/failure/stopping cleanup |
| Generated catalog | 零 UseModule 默认根、显式根 profile、闭包、菱形去重、canary 未构造、manifest metadata |

## 7. EventBus API 场景

必须覆盖 publisher/subscriber/bus、四种 subscribe overload 与同步 extension、default/named channel、Publish/Post、subscription Stop/Dispose/DisposeAsync、contract registry、metrics/monitor、payload projector、diagnostics/runtime options、所有 dispatch/error/backpressure/execution enum。

Generated attribute/manifest/handler descriptor 由 72 contract 和不少于 144 handler 的 build 证明。合成 contribution 覆盖 request/controller/lease、restricted publisher/subscriber、shared/private plane、capability/quota、全部 contribution state 和 drain timeout。

## 8. State API 场景

必须覆盖 `IReadOnlyState`/`IWritableState`/`WritableState`、application registry/read/write/writer authority/factory/scope accessor、computed/evaluation context/reaction、subscription/options/四种 dispatch、snapshot/entry/policy、collection/change/snapshot 及 diagnostics。

所有 access policy、lifetime、authority kind、change kind、scope state 和公开 result/status enum 均逐值构造或执行。负向包含未注册、重复注册、越权、updater throw、computed cycle/first failure、handler throw、dispatcher unavailable、restore reject 和 dispose 后 mutation。

## 9. Routing API 场景

必须覆盖 route map/definition attribute、全部 route kind、typed/untyped reference、template/parser/matcher/constraints、graph builder/snapshot/error、registry/contribution/lease、resolver data、match policy、enter/leave guard、middleware、navigation scope/router/options/result/snapshot、Back/Forward 和 test host parity。

96 静态 + 6 动态 route 负责 BUILD/RT；另设独立 generator-negative project 覆盖非法 template、cycle、duplicate、missing parent、ambiguous candidate、非法 behavior 和 typed parameter 歧义。

## 10. MVVM API 场景

64 个 ViewModel 必须覆盖 `ViewModelBase` notification/dispose，activation context/scope/accessor/state，三类 deactivation contract/guard/result，CommandFactory 同步/异步、CommandExecutionState/Group，OperationScope/Result/Status，Interaction context/result/status，以及 Validation scope/message/event/status。

CommunityToolkit 原生命令与属性通知仍可直接使用；Dogfood 同时验证 City wrapper 没有阻止常用 Toolkit 能力。Presentation handler 和 validation target 是组合证据，不改变 MVVM 无 Avalonia 依赖的边界。

## 11. Localization API 场景

必须覆盖 DI extension/options/service、culture state、lookup context/scope lease、string/message/text、result/error/diagnostics、registry registration/range/revoke、in-memory/file/assembly provider、package descriptor/load result/provider kind/resource scope，以及两个 Generator attribute。

所有 resource kind 中有 1.0 runtime contract 的值都要执行；文档明确拒绝的 kind 由 generator negative fixture 断言。Bridge 由应用实现并 dispatch，不引用 Presentation Localization API，因为该能力已经 Retired。

## 12. Security API 场景

必须覆盖 auth store/snapshot/event/state/provider、principal accessor/helpers、permission registry/checker/descriptor/event、policy/request/requirement/evaluator/result、route policy provider/guard/options/result code、command descriptor/provider/source/state/change reason/unauthorized behavior、token request/result/provider/delegate、账号/凭据文件 store、account session manager 和 DI extension。

所有 auth、authorization、requirement、token result、account switch/store result、session mode、failure kind enum 均逐值覆盖。任何输出不得出现 token、refresh token、完整 claims 或 raw principal。多账号场景必须覆盖 3 个持久化账号、在线/离线受限切换、失败保持旧 session、重复切换幂等、显式刷新当前账号并发布单一新 revision、资源凭据隔离、活动指针、账号删除和 process-like 重启恢复。

## 13. Data API 场景

20 个 Feature 的 public family 全部进入清单：request/context/pipeline/handler、client/factory/descriptor/generated catalog、HTTP、gRPC adapter/native/四种 stream、SignalR adapter/native/subscription、credential、cache/fingerprint/invalidation、resilience/fallback、scheduler/all concurrency policy、connection owner/manager/registration、contribution/lease/capability、large payload/progress、result/error/diagnostics 和 DI/Module。

Testing 模块的 scripted/fake contract 只用于故障 profile；正常 profile 必须走真实 loopback transport。所有 transport/status/error/concurrency/connection/access/result enum 均逐值覆盖。

## 14. Presentation API 场景

必须覆盖两种注册入口、runtime Attach/低级 Start（低级入口只在独立 profile）、Window registration/session/outlet、View registry/locator/factory/binder/handle、ViewModel factory/lease 三 ownership、RouteOutlet plan/commit/result/entry/target、visual identity/hub、Interaction registry/queue、optional Validation/Command binding、resource registry/revoker、active view/plugin unload coordinator、failure presenter/exception/diagnostics。

Headless 用户控件门禁从 Avalonia 原始输入进入，不绕过 control command：30 个唯一控件覆盖 Button、TextBox、ComboBox、CheckBox、Slider、ListBox 和 ScrollViewer，并通过同一组场景控件执行单场景、取消恢复和完整矩阵；其行为证据同时落入 Router 导航、MVVM command state、Data 请求、EventBus publication、State mutation、Localization culture projection、Security account session 和 Presentation VisualTree/rendering。进程测试读取 schema 2 `run-report.json`，逐项断言 `ui-control`、`ui-pointer`、`ui-keyboard`、`ui-binding`、`ui-navigation`、`ui-search`、`ui-localization`、`ui-security`、`ui-state`、`ui-scroll`、`ui-render`、`ui-scenario` 和 `ui-scenario-cancel` 分类，避免仅凭退出码宣称覆盖。

Windows `InteractiveDesktop` 门禁补充 Headless 无法证明的 OS 边界：真实 HWND、前台焦点、UIA provider、物理坐标、`SendInput`、系统滚轮、窗口最小化/恢复/调整尺寸、条件化多显示器移动、屏幕合成截图和 Alt+F4。GUI 驱动只读取 Automation Pattern，不使用 `InvokePattern` 触发业务命令；结果由 `gui-report.json` 与应用 `run-report.json` 交叉验证。

全部 runtime/window/outlet/operation/ownership/interaction/failure/error enum 逐值覆盖。真实桌面路径必须使用 Avalonia Window、Control、DataContext、AttachedToVisualTree 和 DetachedFromVisualTree，不允许仅使用 mock target 计为 UI 证据。

## 15. Build 与变更门禁

实现后的统一命令必须：

1. build 9 个生产程序集和 Dogfood；
2. 生成 exported API inventory；
3. 与文档/PublicAPI baseline/coverage manifest 做三向比较；
4. 运行 BUILD/NEG fixture；
5. 运行 Dogfood automation profiles；
6. 合并 run report，确保 `unclassified=0`、`required-uncovered=0`；
7. 对 `BLOCKED` 项验证原因仍真实，能力一旦出现就强制改为 required；
8. 对 `RETIRED` 项验证 public surface 未复活。

因此，后续某个 City 模块增加一个开发者 public member 而 Dogfood 没使用，CI 会直接失败，而不是等下一轮人工审计才发现。

## 16. 机械实现

`Automation/api-coverage.json` 固化九个运行时程序集的分类、运行证据类别和规范化 SHA-256。`api` profile 反射枚举 exported type 以及 declared public constructor/method/property/event/field，排除 special-name accessor 后形成稳定排序清单。公开面哈希变化视为未分类漂移；证据类别在本轮 ledger 中没有动作视为 required-uncovered。

运行报告保留完整成员清单与 observed/expected hash。提交基线只保存九个摘要，避免维护数千行易受格式变化影响的手工清单。允许分类只有 `UI/RT/NEG/BUILD/MODEL/INDIRECT/BLOCKED/RETIRED`，禁止 `IGNORED/N/A`。任何公开面有意变化都必须先补测试证据，再审核更新摘要。
