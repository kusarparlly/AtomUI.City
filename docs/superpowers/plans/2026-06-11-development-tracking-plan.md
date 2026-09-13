# AtomUI.City 1.0 Release Tracking Plan

本文件是 AtomUI.City 1.0 是否可以发布的唯一进度跟踪文档。仓库中的模块文档、API contract、测试矩阵、设计说明和历史提交只作为实现证据，不作为完成度口径。

## 当前结论

- 1.0 发布状态：暂不可发布，CLI Generation 与 Templates 共 6 项规划能力尚待实现。
- 全局进度：137/143。
- 模块 Feature 合同：128/134。
- 最终发布门禁：9/9。
- 最近校准日期：2026-09-11。

## 统计规则

- 只有本文档中的 `- [x]` 和 `- [ ]` 任务计入全局完成度。
- `- [x]` 表示该任务已有实现、产品合同测试、文档同步和必要门禁证据。
- `- [ ]` 表示该任务尚未达到 1.0 发布完成标准；即使已有部分实现或局部测试，也不能计为完成。
- 任何任务完成时，必须在同一个提交中更新本文档。
- `docs/superpowers/plans/` 目录只保留本文档，不再保留批次计划或局部任务跟踪。
- 模块级进度不再维护独立 `implementation-plan.md`；Feature ID 的发布状态以本文档为准。

## 完成标准

一个 1.0 任务只有同时满足以下条件才能勾选：

- Public API 或内部 contract 已稳定，失败行为、取消、生命周期和线程边界明确。
- 产品合同测试覆盖必要断言和失败路径。
- 相关模块文档、诊断说明、测试矩阵和兼容性说明已同步。
- `dotnet build AtomUICity.slnx`、相关测试、文档检查和 public API 检查在对应提交前通过。
- 如任务影响发布包、模板、CLI、source generator 或平台集成，必须通过对应专项门禁。

## 模块汇总

| 模块 | 完成 | 未完成 | 总数 | 当前状态 |
| --- | ---: | ---: | ---: | --- |
| Core | 7 | 0 | 7 | 已完成 |
| Testing | 9 | 0 | 9 | 已完成 |
| Build | 8 | 0 | 8 | 已完成 |
| Generators | 8 | 0 | 8 | 已完成 |
| Routing | 12 | 0 | 12 | 已完成 |
| Presentation | 8 | 0 | 8 | 已完成 |
| MVVM | 6 | 0 | 6 | 已完成 |
| State | 8 | 0 | 8 | 已完成 |
| EventBus | 6 | 0 | 6 | 已完成 |
| PluginSystem | 8 | 0 | 8 | 已完成 |
| Data | 20 | 0 | 20 | 已完成 |
| Localization | 8 | 0 | 8 | 已完成 |
| Security | 9 | 0 | 9 | 已完成 |
| CLI | 6 | 1 | 7 | 进行中 |
| Templates | 5 | 5 | 10 | 进行中 |
| Release Gates | 9 | 0 | 9 | 已完成 |

## Core

- [x] AUC-CORE-001 Application Host Builder。验收重点：Build 后 services 冻结、HostBuilt 诊断、根 scope 创建。
- [x] AUC-CORE-002 Lifecycle Pipeline。验收重点：stage 顺序、同 stage 顺序、异常路径、Stop 不重复执行、Stopped 后再次 Start 被拒绝。
- [x] AUC-CORE-003 Lifecycle Scope Tree。验收重点：leaf-first、parent-child 状态、dispose 后 mutating API 失败。
- [x] AUC-CORE-004 Module Contract。验收重点：依赖排序、默认 id、显式 id、模块来源、PreConfigure 顺序、配置阶段禁止解析运行时服务、配置阶段结束后拒绝继续修改服务注册。
- [x] AUC-CORE-005 DI Registration Markers。验收重点：lifetime、exposed services、AOT metadata 可读。
- [x] AUC-CORE-006 Host Diagnostics。验收重点：现有 AUCHOST001/002/003 和目标失败诊断上下文。
- [x] AUC-CORE-007 UI Dispatcher Contract。验收重点：不可用 dispatcher 返回失败且 Core 不引用 Avalonia。

## Testing

- [x] AUC-TESTING-001 Test Host。验收重点：service、diagnostics、dispose、records。
- [x] AUC-TESTING-002 Fake Dispatcher。验收重点：queue、UI 线程识别、异常、pending count。
- [x] AUC-TESTING-003 Deterministic Scheduler。验收重点：虚拟时间推进、任务顺序、异常记录。
- [x] AUC-TESTING-004 Module Test Host。验收重点：module graph、lifecycle、diagnostics。
- [x] AUC-TESTING-005 Plugin Test Host。验收重点：load/unload、contribution、owner revoke。
- [x] AUC-TESTING-006 Routing Test Host。验收重点：route build、match、navigation helper。
- [x] AUC-TESTING-007 Source Generation Kit。验收重点：generated source snapshot、diagnostics、references。
- [x] AUC-TESTING-008 AOT Check。验收重点：反射扫描、dynamic code、trimming 风险诊断。
- [x] AUC-TESTING-009 Test Layers。验收重点：Unit/Contract/Integration/Platform/Dogfood 分层标记。

## Build

- [x] AUC-BUILD-001 Output Layout。验收重点：artifacts、packages、logs、test-results 都在 output 下。
- [x] AUC-BUILD-002 Package Metadata。验收重点：LGPL v3、repository、symbol、package id 和 dependency group。
- [x] AUC-BUILD-003 Project Inventory。验收重点：src/tests 项目被 inventory 覆盖。
- [x] AUC-BUILD-004 Dependency Boundary。验收重点：runtime 不依赖 Testing、Roslyn 或 test packages。
- [x] AUC-BUILD-005 Source Generator Packaging。验收重点：generator target、analyzer layout、runtime 不引用 generator。
- [x] AUC-BUILD-006 Release Gates。验收重点：docs、format、pack、test gate 可本地执行。
- [x] AUC-BUILD-007 Test Naming。验收重点：测试命名和模块对应关系。
- [x] AUC-BUILD-008 MSBuild Transitive Assets。验收重点：Build 包包含 buildTransitive props/targets、自动分发 Generators analyzer、公开 BuildMsBuildContract，project inventory 拒绝空 source project。

## Generators

- [x] AUC-GENERATORS-001 Incremental Infrastructure。验收重点：incremental 输入隔离、hint name 稳定、无 runtime 依赖。
- [x] AUC-GENERATORS-002 Module Graph。验收重点：DependsOn 图、循环诊断、默认 module id。
- [x] AUC-GENERATORS-003 DI Manifest。验收重点：lifetime、ExposeServices、显式注册和冲突诊断。
- [x] AUC-GENERATORS-004 Route Manifest。验收重点：route attribute、template、target、排序和诊断。
- [x] AUC-GENERATORS-005 Plugin Manifest。验收重点：plugin metadata、capability、dependency、contribution。
- [x] AUC-GENERATORS-006 Localization Manifest。验收重点：culture、resource、fallback、空 attribute 参数、未知 enum、重复 key 诊断和原子 registrar。
- [x] AUC-GENERATORS-007 Presentation View Manifest。验收重点：ViewFor、constructor、registrar source 和诊断。
- [x] AUC-GENERATORS-008 Diagnostics。验收重点：diagnostic id、severity、message args 和 source location。

## Routing

- [x] AUC-ROUTING-001 Route Definition Syntax。验收重点：合法模板、非法模板、参数边界、属性默认值和稳定排序。
- [x] AUC-ROUTING-002 Route Graph Build and Snapshot。验收重点：graph 不可变、冲突拒绝、plugin route revoke 后旧 snapshot 仍只读可用。
- [x] AUC-ROUTING-003 Route Matching and Parameters。验收重点：优先级、参数转换、constraint、并发匹配和非法输入。
- [x] AUC-ROUTING-004 Navigation Transaction。验收重点：失败不改变 current snapshot、取消不提交、重复 dispose 幂等、并发策略稳定。
- [x] AUC-ROUTING-005 Guard and Redirect Pipeline。验收重点：enter/leave 顺序、deny、redirect、loop detection、异常映射和取消。
- [x] AUC-ROUTING-006 ViewModel Target Resolution。验收重点：target descriptor 内容完整、Routing 不依赖 Presentation、失败不创建 ViewModel。
- [x] AUC-ROUTING-007 Plugin Route Contribution。验收重点：插件贡献、冲突隔离、卸载撤销、旧 snapshot 只读。
- [x] AUC-ROUTING-008 Navigation Journal and Reuse。验收重点：push/replace/back/forward、容量裁剪、失败不写历史和 reuse key。
- [x] AUC-ROUTING-009 Resolver Transaction Data。验收重点：顺序、原子提交、失败、重定向、重复 key 和取消。
- [x] AUC-ROUTING-010 Route Middleware。验收重点：父子嵌套顺序、短路、异常和取消。
- [x] AUC-ROUTING-011 Host DI and Diagnostics。验收重点：服务生命周期、AUCRT 字段和诊断故障隔离。
- [x] AUC-ROUTING-012 Routing Test Host Parity。验收重点：Testing host 委托生产 matcher/navigation 实现。

## Presentation

- [x] AUC-PRESENTATION-001 UI Dispatcher Bridge。验收重点：UI 线程识别、后台 marshal、取消、异常映射和平台不可用。
- [x] AUC-PRESENTATION-002 View Registry and Locator。验收重点：manifest 注册、显式覆盖、重复拒绝、插件撤销和 O(1) lookup 路径。
- [x] AUC-PRESENTATION-003 View Factory and Binding。验收重点：构造参数、DataContext、失败回滚、handle dispose 和 lifecycle event。
- [x] AUC-PRESENTATION-004 Route Outlet Commit。验收重点：成功替换、失败回滚、取消、重复 commit、旧 view dispose 和结果状态。
- [x] AUC-PRESENTATION-005 Visual Lifecycle Feedback。验收重点：attach/detach、focus、visibility、反馈顺序和 handler 失败隔离。
- [x] AUC-PRESENTATION-006 Interaction and Validation Bridge。验收重点：handler 注册撤销、无 handler、验证消息变化、控件释放和取消。
- [x] AUC-PRESENTATION-007 Localization and Resource Bridge。验收重点：culture 切换、fallback、resource revoke、插件资源卸载和局部失败隔离。
- [x] AUC-PRESENTATION-008 Plugin UI Unload Coordination。验收重点：active view lease、卸载撤销、拒绝卸载、资源释放和重复 unload。

## MVVM

- [x] AUC-MVVM-001 ViewModel Base and Notification。验收重点：PropertyChanged、释放幂等、无 UI 依赖和继承扩展点。
- [x] AUC-MVVM-002 Activation and Deactivation。验收重点：状态机、拒绝停用、取消、异常映射和资源释放。
- [x] AUC-MVVM-003 Command Execution。验收重点：成功、失败、取消、并发拒绝、CanExecute 变化和异常不泄漏到 UI。
- [x] AUC-MVVM-004 Interaction Requests。验收重点：有 handler、无 handler、异常、取消、泛型 result 和 handler scope 释放。
- [x] AUC-MVVM-005 Validation Model。验收重点：消息增删、状态聚合、重复处理、释放和 Presentation binding 输入。
- [x] AUC-MVVM-006 Operation and Cancellation Scope。验收重点：状态转换、取消顺序、重复终态、耗时字段和资源释放。

## State

- [x] AUC-STATE-001 Writable State。验收重点：原子更新、version、提交后通知、相等值不通知、订阅 dispose、disposed mutation rejection、updater 异常诊断和写拒绝/access policy。
- [x] AUC-STATE-002 Application State。验收重点：并发注册/读取、DI、factory/scope accessor、五种 access policy writer、not registered、StateDefinition enum/schema/授权元数据边界。
- [x] AUC-STATE-003 Computed State。验收重点：lazy invalidation、循环依赖拒绝、锁外计算、失效世代提交、首次失败重抛、缓存和异常诊断。
- [x] AUC-STATE-004 State Subscription。验收重点：dispose 后不通知、Background 不阻塞状态提交、Background handler 失败诊断。
- [x] AUC-STATE-005 State Snapshot。验收重点：不可变、过滤、scope kind、restore diagnostics、entry version/schema 边界、entries 不含 null。
- [x] AUC-STATE-006 Collection State。验收重点：change kind、item/collection version、重复 key 合并、restore 版本纪律、快照不可变和 dispose 合同。
- [x] AUC-STATE-007 Diagnostics。验收重点：AUCSTA001-011。
- [x] AUC-STATE-008 Threading。验收重点：不隐式 UI、延迟通知串行 FIFO、有界队列与溢出诊断。

## EventBus

- [x] AUC-EVENTBUS-001 Typed Publish。验收重点：delivery/post result 边界、null event、预取消 token、disposed bus、publish options 边界、result immutable/null delivery、error policy、diagnostics。
- [x] AUC-EVENTBUS-002 Subscription Lifecycle。验收重点：dispose 后不再收到事件、StopAsync 移除新发布快照、等待 in-flight handler、owner stop/cancellation 释放、bus dispose 清理 active subscriptions、已 Disposed 后 StopAsync 幂等。
- [x] AUC-EVENTBUS-003 Contract Registry。验收重点：shared contract assembly match、重复 contract id、稳定默认映射、plugin-private descriptor default id 拒绝、shared registry 拒绝 plugin-private descriptor。
- [x] AUC-EVENTBUS-004 Dispatch Policy。验收重点：顺序、异常聚合、停止策略、未知 error policy 拒绝。
- [x] AUC-EVENTBUS-005 Diagnostics。验收重点：EventBus.Event* 现有代码、failure/cancellation 诊断包含 contract id、event id 和 subscription id。
- [x] AUC-EVENTBUS-006 DI Registration。验收重点：默认服务、可替换 diagnostics 和 provider dispose 释放 EventBus singleton。

## PluginSystem

- [x] AUC-PLUGIN-001 Plugin Metadata。验收重点：id、version、mainAssembly、schema 和 required fields。
- [x] AUC-PLUGIN-002 Dependency Validation。验收重点：missing、cycle、version mismatch diagnostics。
- [x] AUC-PLUGIN-003 Package Installation。验收重点：staging cleanup、installed record、path normalization。
- [x] AUC-PLUGIN-004 Discovery。验收重点：invalid install record diagnostics 且继续扫描其他插件。
- [x] AUC-PLUGIN-005 Loading。验收重点：Loaded/Faulted 状态和 diagnostics。
- [x] AUC-PLUGIN-006 MSBuild Contract。验收重点：MSBuild property、output path、package content roots。
- [x] AUC-PLUGIN-007 Diagnostics。验收重点：AUCPLG0000-0023 catalog 唯一、连续、不可变。
- [x] AUC-PLUGIN-008 Unload Contract。验收重点：Active -> Unloading -> Unloaded/UnloadPending。

## Data

- [x] AUC-DATA-001 Request Pipeline。验收重点：执行顺序、取消不写缓存、retry diagnostics。
- [x] AUC-DATA-002 HTTP Transport。验收重点：status -> DataErrorKind 映射。
- [x] AUC-DATA-003 gRPC Unary Adapter。验收重点：显式 invoker 与 GrpcStatusCode 映射，不代表原生 streaming。
- [x] AUC-DATA-004 SignalR Invocation Adapter。验收重点：显式 invoker context，不代表原生 HubConnection/realtime。
- [x] AUC-DATA-005 Connection Lifecycle。验收重点：状态转换、owner 释放。
- [x] AUC-DATA-006 Authentication。验收重点：credential before transport。
- [x] AUC-DATA-007 Request Cache Baseline。验收重点：canonical identity、TTL、精确与多维批量撤销。
- [x] AUC-DATA-008 Error Model。验收重点：result 不混用 success/error。
- [x] AUC-DATA-009 DI Registration。验收重点：默认服务。
- [x] AUC-DATA-010 Host Lifecycle Integration。验收重点：Host shutdown、并发幂等、启动回滚、关闭失败继续清理和注册撤销。
- [x] AUC-DATA-011 Native gRPC and Streaming。验收重点：官方 client、四种 call、metadata/deadline、owner、backpressure。
- [x] AUC-DATA-012 SignalR Realtime Connection。验收重点：官方 HubConnection、push/subscription/reconnect/token switch/owner shutdown。
- [x] AUC-DATA-013 Operation Concurrency Policies。验收重点：六种并发策略和确定性竞态。
- [x] AUC-DATA-014 Advanced Resilience Policies。验收重点：circuit breaker、fallback、rate limit、作用域和诊断。
- [x] AUC-DATA-015 Cache Consistency and Invalidation。验收重点：canonical identity、TTL 和多来源批量失效。
- [x] AUC-DATA-016 Client Descriptors and Generation。验收重点：typed/generated descriptor、AOT catalog、零运行时扫描。
- [x] AUC-DATA-017 Plugin Data Contributions。验收重点：拒绝、撤销、取消、缓存清理和可卸载。
- [x] AUC-DATA-018 Large Payload and Progress。验收重点：流式 IO、进度、range/resume、取消和内存上限。
- [x] AUC-DATA-019 Pipeline Extensibility and Capability。验收重点：handler、metadata validation、capability 和异常映射。
- [x] AUC-DATA-020 Testing Infrastructure and Dogfood。验收重点：Testing doubles、真实本地三传输 headless 和压力门禁。

## Localization

- [x] AUC-LOCALIZATION-001 Culture State and Fallback。验收重点：City.State 发布、深只读 culture/集合快照、递归 fallback、任意长度 cycle、缓存/撤销后的 loaded package state 和重复切换。
- [x] AUC-LOCALIZATION-002 Language Package Provider。验收重点：三个默认 provider、provider kind、原子批量注册、16 MiB 上限、重复根属性、必填 schema 与 id/culture/version/checksum/path/resource 校验、取消和 owner revoke。
- [x] AUC-LOCALIZATION-003 Lazy Package Loading。验收重点：按需加载、并发合并、waiter 取消隔离、失败 fallback、culture/package cache identity 和共享 Dispose 完成事务。
- [x] AUC-LOCALIZATION-004 Lookup and Missing Key Fallback。验收重点：scope lookup、context-specific fallback 隔离、缺失 key、任意 formatter 异常和订阅更新。
- [x] AUC-LOCALIZATION-005 Assembly Language Packages。验收重点：独立 assembly、属性声明、精确/唯一资源解析、缺失资源和 unload owner。
- [x] AUC-LOCALIZATION-006 Presentation Refresh Bridge。验收重点：bridge 调用、局部失败、提交后 service-owned 完成、批量刷新和不依赖 Avalonia 类型。
- [x] AUC-LOCALIZATION-007 Plugin Package Revocation。验收重点：撤销后不可 lookup、旧 snapshot 稳定、持有文本刷新、提交后取消一致性、fallback state 清理、在途 load 不复活和重复 revoke。
- [x] AUC-LOCALIZATION-008 Generated Localization Manifest。验收重点：主 Generator 接线、原子 registrar、规范化 culture/package identity、声明 ALC、强类型 key constant、critical key、fallback cycle、空参数和未知 enum 诊断。

## Security

- [x] AUC-SECURITY-001 Authentication State Store。验收重点：snapshot/Actor chain 输入输出不可变、所有层级 BootstrapContext 清除、token hint 原子继承、状态切换、有序 revision、观察者异常隔离、重复设置和 logout。
- [x] AUC-SECURITY-002 Current Principal Access。验收重点：authenticated、anonymous、多 identity、claims 稳定读取和 mutation 隔离。
- [x] AUC-SECURITY-003 Permission Registry and Checker。验收重点：注册、重复、未注册、contribution 撤销/tombstone、并发有序通知、观察者隔离和 checker result。
- [x] AUC-SECURITY-004 Authorization Policy Evaluation。验收重点：成功、Denied/Forbidden、可变 requirement 输入先快照再校验、非法 Failed kind、取消语义、多 requirement、provider 异常和诊断。
- [x] AUC-SECURITY-005 Route Authorization Guard。验收重点：allow、deny、redirect login、contribution 撤销、异常取消、诊断和 Routing 无 Security 反向依赖。
- [x] AUC-SECURITY-006 Command Authorization。验收重点：状态变化、有序 revision、禁用/隐藏、contribution 继承/冲突、失败构造隔离、订阅回滚/释放聚合、contribution 撤销、观察者隔离和诊断。
- [x] AUC-SECURITY-007 Access Token Provider。验收重点：成功、null/异常失败、不可用、调用前后取消、诊断脱敏、DI 默认 provider 和 Data 集成前置条件。
- [x] AUC-SECURITY-008 Multi-Account File Persistence。验收重点：多账号文件持久化、账号隔离、路径约束、原子写入、重启恢复、删除无残留，以及凭据不进入普通配置、State、日志或诊断。
- [x] AUC-SECURITY-009 Active Account Switching and Restore。验收重点：全局单活动账号、启动恢复、原子切换、失败/取消回滚、单次 revision/通知和离线受限模式。

## CLI

- [x] AUC-CLI-001 Command Model。验收重点：入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。
- [x] AUC-CLI-002 New App Command。验收重点：生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。
- [x] AUC-CLI-003 Build and Test Commands。验收重点：成功、失败、非零 exit code、取消、CI 模式和输出截断。
- [x] AUC-CLI-004 Plugin Inspect and Doctor。验收重点：合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。
- [x] AUC-CLI-005 AI-Friendly Envelope。验收重点：schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。
- [x] AUC-CLI-006 Non-Interactive and CI Mode。验收重点：CI、non-interactive、stdin unavailable、需要确认时失败。
- [ ] AUC-CLI-007 Generation Commands。验收重点：真实生成、dry-run、冲突、取消、回滚和产物可构建。

## Templates

- [x] AUC-TEMPLATES-001 Application Template。验收重点：生成、restore/build/test、命名空间、包引用、无绝对路径。
- [x] AUC-TEMPLATES-002 Package Layout。验收重点：required files、路径规范化、重复文件、路径逃逸和 package id。
- [x] AUC-TEMPLATES-003 Template Variables。验收重点：变量默认值、非法值、命名空间生成和错误消息。
- [x] AUC-TEMPLATES-004 Plugin Template。验收重点：单 assembly、NuGet metadata、manifest、msbuild 属性和测试项目。
- [x] AUC-TEMPLATES-005 Test Template。验收重点：测试项目 build/test、TestLayer、Testing 引用边界和命名规则。
- [ ] AUC-TEMPLATES-006 Module Template。验收重点：模块类型、依赖声明、服务注册、manifest/source generator 输入和 build test。
- [ ] AUC-TEMPLATES-007 Page Template。验收重点：View/ViewModel、route declaration、Presentation 绑定和激活测试。
- [ ] AUC-TEMPLATES-008 Localization Template。验收重点：culture package、fallback、generated manifest 和 Localization 测试。
- [ ] AUC-TEMPLATES-009 Configuration Template。验收重点：Options binding、validation、reload policy 和配置测试。
- [ ] AUC-TEMPLATES-010 Avalonia Desktop Application Template。验收重点：Avalonia Application、desktop lifetime、主窗口、Presentation bootstrap 和 headless/platform smoke。

## Release Gates

- [x] AUC-RELEASE-001 Full solution build。通过 `dotnet build AtomUICity.slnx`，且无 warning、无 error。
- [x] AUC-RELEASE-002 Full solution tests。通过 `dotnet test AtomUICity.slnx --no-build`。
- [x] AUC-RELEASE-003 Documentation gate。通过 `bash engineering/check-docs.sh`。
- [x] AUC-RELEASE-004 Public API gate。通过 `bash engineering/check-public-api.sh`，并确认 public API 变化已审阅。
- [x] AUC-RELEASE-005 Package generation。通过 `bash engineering/pack.sh --configuration Release`。
- [x] AUC-RELEASE-006 Package validation。通过 `bash engineering/validate-packages.sh --configuration Release`。
- [x] AUC-RELEASE-007 Template smoke gate。通过 `bash engineering/check-template-smoke.sh`。
- [x] AUC-RELEASE-008 CI-equivalent local gate。通过 `bash engineering/test-ci.sh` 和必要的 platform integration gate。
- [x] AUC-RELEASE-009 Release notes and versioning。通过 `bash engineering/generate-release-notes.sh`，并完成 1.0 版本号、包元数据和发布说明审阅。

## 后续维护规则

- 新增、拆分或删除 1.0 任务时，必须同步更新当前结论、模块汇总和对应任务段落。
- 不允许通过模块局部文档覆盖本文档中的完成状态。
- 不允许新增其他任务跟踪文件；临时执行计划完成后必须折叠回本文档或删除。
- 每次工作对齐时，按本文档输出 `已完成数/总数`。
