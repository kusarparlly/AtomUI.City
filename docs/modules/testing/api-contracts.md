# AtomUI.City.Testing API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Test Host | TestHost, TestHostBuilder | 通用 Host 测试工具。 | 测试结束必须可释放并暴露 diagnostics。 |
| Module/Plugin Host | ModuleTestHost, PluginTestHost | 模块和插件生命周期测试。 | 加载、卸载、撤销可断言。 |
| Threading Tools | FakeUiDispatcher, DeterministicScheduler | 可控 UI 和异步调度。 | 不依赖固定 delay。 |
| Generator Kit | SourceGenerationTestCase, GeneratedSourceSnapshot | source generator 测试。 | 输出和 diagnostic 可 snapshot。 |
| AOT Gate | AotCompatibilityCheck | AOT/trimming 规则检查。 | 禁止模式产生 diagnostic。 |

## 首批冻结 API Cards

以下 API 是 Phase 2 的产品级冻结范围。Testing 包的目标是让后续模块不依赖固定时间、不依赖真实 UI、不依赖真实插件包和不依赖手工观察输出即可证明生命周期、线程、诊断和释放行为。

### `TestHost`

| Field | Contract |
| --- | --- |
| Type | `TestHost` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-001 |
| Purpose | 提供可释放、可断言 diagnostics、service 和 lifecycle record 的通用测试 Host。 |
| Owner | Test method / test fixture |
| Created By | `TestHostBuilder.Build`。 |
| Lifetime | Created -> Started -> Stopped -> Disposed。 |
| DI Lifetime | 测试对象，不进入生产 DI。 |
| Thread Safety | 单个 TestHost 默认由一个测试控制；并发访问 service provider 只允许读取和明确线程安全的测试服务。 |
| Disposal | `Dispose`/`DisposeAsync` 幂等；Dispose 后获取 mutable service 或记录新 diagnostic 必须失败。 |
| Nullability | services、diagnostics、root scope 不得为空。 |
| Cancellation | async lifecycle helper 必须观察 token。 |
| Failure Behavior | 配置非法、未释放 resource、Dispose 后使用产生测试失败 record。 |
| Diagnostics | 记录 host id、test name、operation、scope 和 exception。 |
| Plugin Boundary | 不加载真实插件；插件相关测试通过 `PluginTestHost`。 |
| AOT / Trimming | 不进入生产依赖链；可断言生产包未引用 Testing。 |
| Breaking Change Rules | 修改释放断言、diagnostics 记录结构或默认 services 属于 breaking change。 |
| Tests | `TestHostTests` 断言 service、diagnostics、dispose、records 和未释放资源。 |

`TestHost.ApplicationContext` 以 `IApplicationContext` 暴露不可变实例描述；`TestHost.Properties` 是 `UseProperty` 输入的结构只读快照，两者互不混合。

### `TestHostBuilder`

| Field | Contract |
| --- | --- |
| Type | `TestHostBuilder` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-001 |
| Purpose | 构造 `TestHost` 的服务、diagnostics、fake dispatcher 和 disposable tracker。 |
| Owner | Test method / test fixture |
| Created By | 调用方直接创建或 Testing factory。 |
| Lifetime | Configuring -> Built -> Frozen。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 配置阶段不保证线程安全；Build 后 mutating API 必须失败。 |
| Disposal | builder 不释放 TestHost；TestHost 由调用方释放。 |
| Nullability | service action、options action、record sink 不得为空。 |
| Cancellation | Build 不接受 token，不执行可取消 IO。 |
| Failure Behavior | 重复 Build 或非法配置抛声明异常或返回失败 record。 |
| Diagnostics | Build failure 产生 TestDiagnosticEntry。 |
| Plugin Boundary | 不允许把 Testing builder 传入生产 Host 或插件 runtime。 |
| AOT / Trimming | 可用于断言生产项目不引用 Testing。 |
| Breaking Change Rules | 修改默认 fake services、冻结语义或失败 record 属于 breaking change。 |
| Tests | `TestHostTests` 断言 Build、冻结、非法配置和默认服务。 |

### `FakeUiDispatcher`

| Field | Contract |
| --- | --- |
| Type | `FakeUiDispatcher` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-002 |
| Purpose | 模拟 UI dispatcher，允许测试手动 drain UI work。 |
| Owner | TestHost 或单个测试。 |
| Created By | `TestHostBuilder` 或测试直接创建。 |
| Lifetime | Created -> Running -> Draining -> Disposed。 |
| DI Lifetime | 测试 singleton 或 scoped fake。 |
| Thread Safety | enqueue 可从后台测试线程调用；drain 必须由测试串行调用。 |
| Disposal | Dispose 后拒绝新 work；pending work 可被断言或取消。 |
| Nullability | work delegate 不得为空。 |
| Cancellation | work 开始前 token 取消则不执行；执行中取消由 work 自行观察。 |
| Failure Behavior | work exception 记录到 dispatcher diagnostics，不被静默吞掉。 |
| Diagnostics | 记录 work item id、enqueue order、thread marker、exception。 |
| Plugin Boundary | plugin UI work 测试必须携带 owner，插件卸载时可断言 pending work 被取消。 |
| AOT / Trimming | 不引用 Avalonia；只实现 Core `IUiDispatcher` contract。 |
| Breaking Change Rules | 修改 FIFO 顺序、pending count、异常记录或 UI 线程识别属于 breaking change。 |
| Tests | `FakeUiDispatcherTests` 断言 queue、drain、异常、取消、pending count。 |

### `DeterministicScheduler`

| Field | Contract |
| --- | --- |
| Type | `DeterministicScheduler` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-003 |
| Purpose | 提供虚拟时间和确定性异步任务推进，替代固定 `Task.Delay`。 |
| Owner | Test method / TestHost |
| Created By | 测试直接创建或 TestHostBuilder 注入。 |
| Lifetime | Active -> Completed -> Disposed。 |
| DI Lifetime | 测试 scoped。 |
| Thread Safety | 单线程推进；并发 schedule 必须由实现同步保护或拒绝。 |
| Disposal | Dispose 后拒绝新 schedule；pending task 可断言。 |
| Nullability | scheduled delegate 不得为空。 |
| Cancellation | 取消任务不得执行，必须记录 cancelled 状态。 |
| Failure Behavior | 负 duration、task exception、Dispose 后 schedule 产生稳定失败。 |
| Diagnostics | 记录 virtual time、task id、state 和 exception。 |
| Plugin Boundary | 插件后台任务测试必须绑定 owner，owner dispose 后取消。 |
| AOT / Trimming | 不依赖 Rx scheduler；不进入生产依赖链。 |
| Breaking Change Rules | 修改虚拟时间推进、同时间任务排序或异常记录属于 breaking change。 |
| Tests | `SharedTestUtilitiesTests` 断言 AdvanceBy、AdvanceUntilIdle、异常和取消。 |

### `ModuleTestHost`

| Field | Contract |
| --- | --- |
| Type | `ModuleTestHost` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-004 |
| Purpose | 测试 Core module graph、配置、初始化、shutdown 和 diagnostics。 |
| Owner | Test method / test fixture |
| Created By | `ModuleTestHostBuilder.Build`。 |
| Lifetime | Created -> Configured -> Started -> Stopped -> Disposed。 |
| DI Lifetime | 测试对象。 |
| Thread Safety | module lifecycle 由测试串行驱动。 |
| Disposal | Dispose 停止未停止模块并释放 service scope；重复释放幂等。 |
| Nullability | module types、module descriptors、host options 不得为空。 |
| Cancellation | Start/Stop helper 必须观察 token。 |
| Failure Behavior | 循环依赖、缺失依赖、初始化失败、shutdown 失败均产生可断言 record。 |
| Diagnostics | 记录 module id、type、stage、dependency path。 |
| Plugin Boundary | 不加载动态插件；插件 module 通过 PluginTestHost 验证。 |
| AOT / Trimming | 可消费 generated module manifest snapshot。 |
| Breaking Change Rules | 修改 graph 排序、hook 顺序或 failure record 属于 breaking change。 |
| Tests | `ModuleTestHostTests` 断言 graph、lifecycle、diagnostics 和 shutdown。 |

### `PluginTestHost`

| Field | Contract |
| --- | --- |
| Type | `PluginTestHost` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-005 |
| Purpose | 测试插件 package、manifest、load、disable、unload 和 contribution revoke。 |
| Owner | Test method / test fixture |
| Created By | `PluginTestHostBuilder.Build`。 |
| Lifetime | Created -> Running -> Disposed；每个插件有独立 state record。 |
| DI Lifetime | 测试对象。 |
| Thread Safety | 同一 plugin id 的 load/unload 串行；不同 plugin 可由测试显式并发。 |
| Disposal | Dispose 必须停用并释放所有测试插件；UnloadPending 可被断言。 |
| Nullability | plugin package、plugin id、manifest path 不得为空。 |
| Cancellation | load/unload/install helper 必须观察 token；取消后执行最小清理。 |
| Failure Behavior | manifest invalid、依赖冲突、contribution 泄漏、unload pending 均产生 PluginTestRecord。 |
| Diagnostics | 记录 plugin id、version、state、contribution id、remaining owner。 |
| Plugin Boundary | 插件私有类型不能泄漏为 Host 长期持有对象。 |
| AOT / Trimming | 支持 static plugin manifest 测试，不要求动态插件在 Native AOT 下可加载。 |
| Breaking Change Rules | 修改 state record、unload 断言或 contribution 追踪属于 breaking change。 |
| Tests | `PluginTestHostTests` 断言 load/unload、contribution、owner revoke 和 leak detection。 |

### `RoutingTestHost`

| Field | Contract |
| --- | --- |
| Type | `RoutingTestHost` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-006 |
| Purpose | 测试 route graph、match、guard、navigation helper 和 failure diagnostics。 |
| Owner | Test method / test fixture |
| Created By | `RoutingTestHostBuilder.Build`。 |
| Lifetime | Created -> GraphBuilt -> Running -> Disposed。 |
| DI Lifetime | 测试对象。 |
| Thread Safety | route graph snapshot 可并发读取；navigation helper 由测试串行驱动。 |
| Disposal | Dispose 释放 navigation scope 和 fake dispatcher work；重复释放幂等。 |
| Nullability | route definition、pattern、target descriptor 不得为空。 |
| Cancellation | navigation helper 必须传递 token 到 guard/resolver。 |
| Failure Behavior | route conflict、match fail、guard deny、resolver fail 可断言。 |
| Diagnostics | 记录 route id、pattern、target、stage、guard type。 |
| Plugin Boundary | 插件 route contribution 必须带 owner，可测试 revoke 后新 snapshot。 |
| AOT / Trimming | 支持 generated route manifest snapshot。 |
| Breaking Change Rules | 修改 match priority、snapshot record 或 navigation failure record 属于 breaking change。 |
| Tests | `RoutingTestHostTests` 断言 route build、match、navigation helper 和 diagnostics。 |

### `SourceGenerationTestCase`

| Field | Contract |
| --- | --- |
| Type | `SourceGenerationTestCase` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-007 |
| Purpose | 运行 Roslyn source generator 测试并输出 generated source snapshot。 |
| Owner | Generator test method |
| Created By | 测试直接创建。 |
| Lifetime | immutable test case；Run 产生独立 result。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | test case 不可变，可并发运行；runner 内部不得共享 mutable compilation。 |
| Disposal | 无长期资源；临时文件由 runner 清理。 |
| Nullability | source files、references、expected diagnostics 不得为空集合对象。 |
| Cancellation | Run 必须传递 token 到 Roslyn pipeline。 |
| Failure Behavior | 编译失败、generator diagnostic、missing generated output 都进入 result，不静默吞掉。 |
| Diagnostics | 记录 diagnostic id、severity、source location、hint name。 |
| Plugin Boundary | plugin manifest generator 测试必须断言 shared contract 不被复制。 |
| AOT / Trimming | 可断言 generated output 不依赖 runtime reflection。 |
| Breaking Change Rules | 修改 snapshot 排序、diagnostic matching 或 generated source normalization 属于 breaking change。 |
| Tests | `SourceGenerationTestKitTests` 断言 source snapshot、diagnostics、references 和 stable ordering。 |

### `AotCompatibilityCheck`

| Field | Contract |
| --- | --- |
| Type | `AotCompatibilityCheck` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-008 |
| Purpose | 检查运行时扫描、dynamic code、unbounded reflection 和 trimming 风险。 |
| Owner | Build/test gate |
| Created By | 测试或 engineering gate。 |
| Lifetime | 单次 run 产生 immutable diagnostics。 |
| DI Lifetime | 不进入生产 DI。 |
| Thread Safety | 输入不可变时可并发运行；文件系统扫描由调用方隔离 root。 |
| Disposal | 无长期资源；文件句柄必须在 run 内关闭。 |
| Nullability | project path、assembly metadata、rule set 不得为空。 |
| Cancellation | 文件读取和 metadata 扫描必须观察 token。 |
| Failure Behavior | 发现禁止模式返回 diagnostic；扫描失败返回 failed check result。 |
| Diagnostics | 记录 rule id、symbol、assembly、path、reason。 |
| Plugin Boundary | 插件包检查必须区分 dynamic plugin 和 static plugin 支持范围。 |
| AOT / Trimming | 该类型本身是 AOT gate，不进入运行时包。 |
| Breaking Change Rules | 修改 rule id、severity 或禁止模式语义属于 breaking change。 |
| Tests | `AotCompatibilityCheckTests` 断言 reflection scan、dynamic code、trimming 风险和误报边界。 |

### `TestLayerAttribute`

| Field | Contract |
| --- | --- |
| Type | `TestLayerAttribute` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-009 |
| Purpose | 标记测试层级，支持功能点测试门禁。 |
| Owner | Test method / test class |
| Created By | 测试源码。 |
| Lifetime | 编译期 metadata 和测试发现 metadata。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | Attribute metadata 只读。 |
| Disposal | 无释放。 |
| Nullability | layer name 不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | layer name 非法或缺失时 tests check gate 失败。 |
| Diagnostics | 记录 test type、declared layer、expected layer。 |
| Plugin Boundary | PluginLifecycle 测试必须声明对应 layer。 |
| AOT / Trimming | 只用于测试程序集。 |
| Breaking Change Rules | 修改 layer 名称或默认发现规则属于 breaking change。 |
| Tests | `TestLayerTests` 断言合法 layer、非法 layer 和缺失 layer。 |

### `TestLayerNames`

| Field | Contract |
| --- | --- |
| Type | `TestLayerNames` |
| Namespace | `AtomUI.City.Testing` |
| Assembly | `AtomUI.City.Testing` |
| Stability | Preview |
| Feature | AUC-TESTING-009 |
| Purpose | 定义标准测试层级名称。 |
| Owner | Testing contract |
| Created By | Source code constants。 |
| Lifetime | 包版本生命周期内稳定。 |
| DI Lifetime | 不进入 DI。 |
| Thread Safety | 常量只读。 |
| Disposal | 无释放。 |
| Nullability | 常量不得为空。 |
| Cancellation | 无取消语义。 |
| Failure Behavior | 不允许复用或重定义已有 layer 名称。 |
| Diagnostics | tests check gate 使用名称输出缺失或非法 layer。 |
| Plugin Boundary | 包含 `PluginLifecycle` 标准名称。 |
| AOT / Trimming | 只用于测试和 gate。 |
| Breaking Change Rules | 删除、重命名或改变 layer 语义属于 breaking change。 |
| Tests | `TestLayerTests` 断言标准名称集合和 feature gate 映射。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| TestHostBuilder.Build | 构建测试 Host。 | services/options。 | TestHost。 | 配置非法返回失败或抛声明异常。 | 同步构建无 token。 | 每个 host 独立 service provider。 |
| FakeUiDispatcher.RunQueuedWork | 手动推进 UI work。 | max items 可选。 | 执行数量或 result。 | work exception 记录到 dispatcher diagnostics。 | 取消 work 不执行。 | 队列 FIFO，测试可断言 pending count。 |
| DeterministicScheduler.AdvanceBy | 推进虚拟时间。 | duration >= 0。 | 执行的 task 数。 | task 异常进入 scheduler diagnostics。 | 取消任务跳过或标记 cancelled。 | 同一 scheduler 单线程推进。 |
| PluginTestHost.InstallAsync | 安装已由 builder 声明的测试插件。 | plugin id。 | PluginTestRecord。 | 未声明的 plugin id 或安装失败通过异常暴露。 | 必须观察 token。 | 同一 plugin id 的生命周期操作由测试串行驱动。 |
| SourceGenerationTestCase.Run | 运行 generator 测试。 | source files、additional files、references。 | GeneratedSourceSnapshot 和 diagnostics。 | 编译失败保留 diagnostics。 | 测试 token 传递给 runner。 | 输出按 hint name 稳定排序。 |
| AotCompatibilityCheck.Run | 检查 AOT 禁止模式。 | assembly/project metadata。 | diagnostic list。 | 发现反射扫描兜底等模式返回 diagnostic。 | 纯 CPU/文件读取可观察 token。 | 结果不可变。 |

## 关键方法详细合同

### `TestHostBuilder.UseProperty`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 在 `Build` 前写入 TestHost 专用测试属性；该数据不进入 `IApplicationContext`。 |
| Parameters | `key` 不能为空或空白；`value` 可以为 null。 |
| Return | 当前 `TestHostBuilder`。 |
| Nullability | `key` 不接受 null；`value` 接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | `key` 非法抛 `ArgumentException`；`Build` 后调用抛 `InvalidOperationException`。 |
| Idempotency | 同一 key 重复设置时，最后一次 Build 前设置生效。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 pending properties。 |
| Diagnostics | Build 前不写诊断；非法状态通过异常暴露。 |
| Tests | `TestHostTests`。 |

### `TestHostBuilder.UseDirectoryName`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 设置测试临时目录名称前缀，便于失败时定位。 |
| Parameters | `name` 不能为空或空白。 |
| Return | 当前 `TestHostBuilder`。 |
| Nullability | `name` 不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | `name` 非法抛 `ArgumentException`；`Build` 后调用抛 `InvalidOperationException`。 |
| Idempotency | 多次调用时最后一次 Build 前设置生效。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 directory name。 |
| Diagnostics | Build 前不写诊断。 |
| Tests | `TestHostTests`。 |

### `TestHostBuilder.KeepDirectoryOnDispose`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 配置测试 Host dispose 时保留临时目录。 |
| Parameters | 无。 |
| Return | 当前 `TestHostBuilder`。 |
| Nullability | 无。 |
| Cancellation | 无。 |
| Exceptions or Result | `Build` 后调用抛 `InvalidOperationException`。 |
| Idempotency | 重复调用在 Build 前幂等。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 keep flag。 |
| Diagnostics | Build 前不写诊断。 |
| Tests | `TestHostTests`。 |

### `TestHostBuilder.Build`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 冻结 builder，创建不可变 `IApplicationContext` 测试实现、只读 TestHost properties、临时目录、共享 diagnostics、fake dispatcher 和 deterministic scheduler。 |
| Parameters | 无。 |
| Return | 新的 `TestHost`。 |
| Nullability | 返回值不能为空。 |
| Cancellation | 同步构建，不接收 token。 |
| Exceptions or Result | 重复 Build 抛 `InvalidOperationException`；目录创建失败按 IO 异常抛出。 |
| Idempotency | 每个 builder 只允许成功 Build 一次。 |
| Concurrency | Build 必须外部串行。 |
| Side Effects | 创建临时目录；成功后冻结所有 builder mutation entrypoint。 |
| Diagnostics | Host 生命周期 diagnostics 在返回的 `TestHost.Diagnostics` 中记录。 |
| Tests | `TestHostTests`。 |

### `TestHost.StopAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 将测试 Host 标记为 stopped，并作为 Dispose 前的统一停止入口。 |
| Parameters | 无。 |
| Return | Host 停止后完成的 `ValueTask`。 |
| Nullability | 返回 task 不为空。 |
| Cancellation | 当前版本无 token；停止必须快速且确定。 |
| Exceptions or Result | Dispose 后重复 Stop 不抛出，保持 stopped。 |
| Idempotency | 幂等；重复调用不重复释放资源。 |
| Concurrency | 单测试线程串行调用；并发调用必须保持 stopped 状态稳定。 |
| Side Effects | 设置 `IsStopped`。 |
| Diagnostics | Stop 本身不写成功诊断；Dispose 写 `AUCTEST001`。 |
| Tests | `TestHostTests`。 |

### `TestHost.Dispose` 和 `TestHost.DisposeAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-001 |
| Purpose | 停止 Host，释放 dispatcher、scheduler 和临时目录，并阻止后续 mutable API 使用。 |
| Parameters | 无。 |
| Return | `Dispose` 无返回；`DisposeAsync` 返回释放完成的 `ValueTask`。 |
| Nullability | 无。 |
| Cancellation | Dispose 不接受 token；已经开始的最小清理不能跳过。 |
| Exceptions or Result | 重复 Dispose 幂等；释放后 dispatcher/scheduler mutation 抛 `ObjectDisposedException`。 |
| Idempotency | 幂等；`AUCTEST001` 只记录一次。 |
| Concurrency | 单测试线程串行释放；并发释放不得重复删除目录。 |
| Side Effects | 删除或保留临时目录，取决于 `KeepDirectoryOnDispose`。 |
| Diagnostics | 成功释放写 `AUCTEST001`，消息包含 Host directory。 |
| Tests | `TestHostTests`。 |

### `FakeUiDispatcher.CheckAccess`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-002 |
| Purpose | 判断当前执行是否处于 fake UI work 上下文。 |
| Parameters | 无。 |
| Return | fake UI marker 激活时返回 true，否则返回 false。 |
| Nullability | 无。 |
| Cancellation | 无。 |
| Exceptions or Result | Dispose 后可继续读取 marker，返回 false。 |
| Idempotency | 只读。 |
| Concurrency | marker 只代表当前测试执行流；不模拟真实 UI thread affinity。 |
| Side Effects | 无。 |
| Diagnostics | 不写诊断。 |
| Tests | `FakeUiDispatcherTests`。 |

### `FakeUiDispatcher.Post`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-002 |
| Purpose | 将同步 UI work 排入 fake dispatcher 队列，等待 `Drain` 手动执行。 |
| Parameters | `callback` 不能为 null。 |
| Return | `FakeUiWorkItem`，用于断言 id、取消、完成和失败状态。 |
| Nullability | `callback` 不接受 null；返回值不能为空。 |
| Cancellation | 调用方可通过返回的 work item 在执行前取消。 |
| Exceptions or Result | Dispose 后调用抛 `ObjectDisposedException`；callback 执行异常由 `Drain` 捕获并写诊断。 |
| Idempotency | 每次调用创建独立 work item。 |
| Concurrency | enqueue 使用内部锁保护；drain 由测试串行调用。 |
| Side Effects | 增加 `PendingCount`。 |
| Diagnostics | work 执行异常写 `AUCTEST101`，消息包含 work item id。 |
| Tests | `FakeUiDispatcherTests`。 |

### `FakeUiDispatcher.Drain`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-002 |
| Purpose | 按 FIFO 顺序执行 pending UI work。 |
| Parameters | 无。 |
| Return | 无。 |
| Nullability | 无。 |
| Cancellation | 已取消 work item 不执行。 |
| Exceptions or Result | work exception 不向外抛出，记录诊断后继续执行后续 work。 |
| Idempotency | 队列为空时调用幂等。 |
| Concurrency | 不支持并发 drain；测试必须串行推进。 |
| Side Effects | 减少 `PendingCount`，执行回调时激活 fake UI marker。 |
| Diagnostics | work exception 写 `AUCTEST101`；取消 work 不写错误诊断。 |
| Tests | `FakeUiDispatcherTests`。 |

### `FakeUiDispatcher.InvokeAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-002 |
| Purpose | 立即在 fake UI marker 下执行同步回调，用于模拟 UI thread direct invoke。 |
| Parameters | `callback` 不能为 null；`cancellationToken` 在执行前观察。 |
| Return | 回调完成后的 `ValueTask` 或 `ValueTask<T>`。 |
| Nullability | callback 不接受 null。 |
| Cancellation | token 已取消时抛 `OperationCanceledException`，且不执行 callback。 |
| Exceptions or Result | Dispose 后调用抛 `ObjectDisposedException`；callback 异常按调用栈传播。 |
| Idempotency | 每次调用只执行一次 callback。 |
| Concurrency | 不模拟真实 dispatcher 重入队列；直接执行。 |
| Side Effects | 执行期间 `CheckAccess()` 返回 true。 |
| Diagnostics | 直接 invoke 的 callback 异常由调用方断言，不写 dispatcher work 诊断。 |
| Tests | `FakeUiDispatcherTests`。 |

### `FakeUiDispatcher.PostAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-002 |
| Purpose | 实现 Core `IUiDispatcher.PostAsync`，把异步 UI work 排入 fake dispatcher。 |
| Parameters | `callback` 不能为 null；`cancellationToken` 在入队前和执行前观察。 |
| Return | work 成功入队后的 `ValueTask`。 |
| Nullability | callback 不接受 null。 |
| Cancellation | token 已取消时抛 `OperationCanceledException`；执行前取消则 work 标记为 canceled。 |
| Exceptions or Result | Dispose 后调用抛 `ObjectDisposedException`；执行异常由 `Drain` 写诊断。 |
| Idempotency | 每次调用创建独立 queued work。 |
| Concurrency | enqueue 使用内部锁保护；drain 串行。 |
| Side Effects | 增加 `PendingCount`。 |
| Diagnostics | work 执行异常写 `AUCTEST101`。 |
| Tests | `FakeUiDispatcherTests`。 |

### `DeterministicScheduler.Schedule`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-003 |
| Purpose | 在虚拟时间线上安排 callback，替代真实 `Task.Delay`。 |
| Parameters | `delay` 必须大于等于 0；`callback` 不能为 null。 |
| Return | `DeterministicScheduledWorkItem`，用于取消和断言状态。 |
| Nullability | callback 不接受 null；返回值不能为空。 |
| Cancellation | 调用方可在执行前通过返回句柄取消 work。 |
| Exceptions or Result | 负 delay 抛 `ArgumentOutOfRangeException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | 每次调用创建独立 work item。 |
| Concurrency | 单线程推进；内部 priority 包含 due time 和 enqueue id，保证同一 due time 稳定排序。 |
| Side Effects | 增加 `ScheduledCount`。 |
| Diagnostics | callback 执行异常由 `RunDueWork` 写 `AUCTEST201`。 |
| Tests | `SharedTestUtilitiesTests`。 |

### `DeterministicScheduler.AdvanceBy`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-003 |
| Purpose | 推进虚拟时间并执行所有 due work。 |
| Parameters | `duration` 必须大于等于 0。 |
| Return | 无。 |
| Nullability | 无。 |
| Cancellation | 已取消 work item 不执行。 |
| Exceptions or Result | 负 duration 抛 `ArgumentOutOfRangeException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | duration 为 0 时只执行当前 due work。 |
| Concurrency | 单线程推进。 |
| Side Effects | 更新 `Now`，减少 `ScheduledCount`，执行 due callbacks。 |
| Diagnostics | callback 异常写 `AUCTEST201` 并继续后续 due work。 |
| Tests | `SharedTestUtilitiesTests`。 |

### `DeterministicScheduler.RunDueWork`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-003 |
| Purpose | 在不改变 `Now` 的情况下执行当前 due work。 |
| Parameters | 无。 |
| Return | 无。 |
| Nullability | 无。 |
| Cancellation | 已取消 work item 被跳过并标记完成。 |
| Exceptions or Result | Dispose 后调用抛 `ObjectDisposedException`；callback exception 不向外抛出。 |
| Idempotency | 没有 due work 时幂等。 |
| Concurrency | 单线程推进。 |
| Side Effects | 执行 callback，移除 due work。 |
| Diagnostics | callback 异常写 `AUCTEST201`，消息包含 work id 和 due time。 |
| Tests | `SharedTestUtilitiesTests`。 |

### `ModuleTestHostBuilder.UseModule`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 注册测试模块实例，并记录测试可读 module name。 |
| Parameters | `name` 不能为空或空白；`module` 不能为 null。 |
| Return | 当前 `ModuleTestHostBuilder`。 |
| Nullability | `name` 和 `module` 不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | 参数非法抛 `ArgumentException` 或 `ArgumentNullException`；Build 后调用抛 `InvalidOperationException`。 |
| Idempotency | 每次调用追加一个模块；duplicate module name 在 Build 时失败；同一 module type 可用不同 name 注册多个测试实例。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 module registration。 |
| Diagnostics | Build 前不写诊断；生命周期失败由 `ModuleTestHost` 写 diagnostics。 |
| Tests | `ModuleTestHostTests`。 |

### `ModuleTestHostBuilder.UseHostProperty`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 透传测试属性到内部 `TestHostBuilder`，最终由 `TestHost.Properties` 只读暴露。 |
| Parameters | `key` 不能为空或空白；`value` 可以为 null。 |
| Return | 当前 `ModuleTestHostBuilder`。 |
| Nullability | `key` 不接受 null；`value` 接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | 参数非法由内部 `TestHostBuilder` 抛出；Build 后调用抛 `InvalidOperationException`。 |
| Idempotency | 同一 key 重复设置时，最后一次 Build 前设置生效。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 修改内部 host builder pending test properties，不修改 `IApplicationContext`。 |
| Diagnostics | Build 前不写诊断。 |
| Tests | `ModuleTestHostTests`。 |

### `ModuleTestHostBuilder.Build`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 冻结 builder，验证模块依赖图，按依赖序创建 `ModuleTestHost`。 |
| Parameters | 无。 |
| Return | 新的 `ModuleTestHost`。 |
| Nullability | 返回值不能为空。 |
| Cancellation | 同步构建，不接收 token。 |
| Exceptions or Result | 重复 Build、duplicate module、required dependency missing 或 dependency cycle 抛 `InvalidOperationException`。 |
| Idempotency | 每个 builder 只允许成功 Build 一次。 |
| Concurrency | Build 必须外部串行。 |
| Side Effects | 创建内部 `TestHost` 和临时目录。 |
| Diagnostics | graph failure 通过异常消息暴露；module lifecycle failure 写入 Host diagnostics。 |
| Tests | `ModuleTestHostTests`。 |

### `ModuleTestHost.InitializeAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 按依赖序执行 module service、contribution 和 application initialization 阶段。 |
| Parameters | `cancellationToken` 在每个 module stage 前后传递和观察。 |
| Return | 初始化完成后的 `ValueTask`。 |
| Nullability | 返回 task 不为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不得把 Host 标记为 initialized。 |
| Exceptions or Result | lifecycle stage 抛出的异常写诊断后重新抛出；重复初始化幂等。 |
| Idempotency | 成功初始化后重复调用不重复执行 module stage。 |
| Concurrency | 生命周期由测试串行驱动。 |
| Side Effects | 构建 service provider，执行 module contribution 和 initialization hook。 |
| Diagnostics | stage 失败写 `AUCTEST301`，消息包含 module name、module type 和 stage。 |
| Tests | `ModuleTestHostTests`。 |

### `ModuleTestHost.ShutdownAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 按反向依赖序执行 module shutdown，并停止内部 `TestHost`。 |
| Parameters | `cancellationToken` 传递给每个 shutdown hook。 |
| Return | shutdown 完成后的 `ValueTask`。 |
| Nullability | 返回 task 不为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`；已经开始的最小清理不能跳过。 |
| Exceptions or Result | shutdown stage 异常写 `AUCTEST302` 后重新抛出；重复 shutdown 幂等。 |
| Idempotency | 成功 shutdown 后重复调用不重复执行 module stage。 |
| Concurrency | 生命周期由测试串行驱动。 |
| Side Effects | 停止内部 Host 并释放 service provider。 |
| Diagnostics | shutdown 失败写 `AUCTEST302`，消息包含 module name、module type 和 stage。 |
| Tests | `ModuleTestHostTests`。 |

### `ModuleTestHost.Dispose` 和 `ModuleTestHost.DisposeAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-004 |
| Purpose | 自动 shutdown 未停止的模块并释放内部 `TestHost`。 |
| Parameters | 无。 |
| Return | `Dispose` 无返回；`DisposeAsync` 返回释放完成的 `ValueTask`。 |
| Nullability | 无。 |
| Cancellation | Dispose 不接收 token。 |
| Exceptions or Result | 重复 Dispose 幂等；shutdown 失败按原异常抛出并写诊断。 |
| Idempotency | 幂等。 |
| Concurrency | 单测试线程串行释放。 |
| Side Effects | 释放 service provider、fake runtime 和临时目录。 |
| Diagnostics | shutdown 失败写 `AUCTEST302`；内部 Host dispose 写 `AUCTEST001`。 |
| Tests | `ModuleTestHostTests`。 |

### `PluginTestHostBuilder.UsePlugin`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 声明测试插件包 id 和 version。 |
| Parameters | `id` 和 `version` 不能为空或空白。 |
| Return | 当前 `PluginTestHostBuilder`。 |
| Nullability | `id` 和 `version` 不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | 参数非法抛 `ArgumentException`；Build 后调用抛 `InvalidOperationException`。 |
| Idempotency | 每次调用追加一个 package；duplicate plugin id 在 Build 时失败。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 package registration。 |
| Diagnostics | Build 前不写诊断。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHostBuilder.Build`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 冻结 builder，验证 duplicate plugin id，并创建内部 `TestHost`。 |
| Parameters | 无。 |
| Return | 新的 `PluginTestHost`。 |
| Nullability | 返回值不能为空。 |
| Cancellation | 同步构建，不接收 token。 |
| Exceptions or Result | 重复 Build 或 duplicate plugin id 抛 `InvalidOperationException`。 |
| Idempotency | 每个 builder 只允许成功 Build 一次。 |
| Concurrency | Build 必须外部串行。 |
| Side Effects | 创建内部 `TestHost` 和 plugin-host 临时目录。 |
| Diagnostics | 插件 unload/revoke 行为写入返回 Host 的 diagnostics。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHost.InstallAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 在测试目录中安装声明过的插件 package，并写入最小 `plugin.json`。 |
| Parameters | `pluginId` 不能为空或空白；`cancellationToken` 在 IO 前观察。 |
| Return | `PluginTestRecord`。 |
| Nullability | `pluginId` 不接受 null；返回值不能为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不提交 install record。 |
| Exceptions or Result | 未声明 package 抛 `KeyNotFoundException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | 同一插件重复 install 返回并更新同一 record，状态为 Installed。 |
| Concurrency | 同一 plugin id lifecycle 由测试串行驱动。 |
| Side Effects | 创建测试插件目录和 manifest 文件。 |
| Diagnostics | install 成功不写诊断；失败由异常暴露。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHost.ActivateAsync` 和 `PluginTestHost.DeactivateAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 切换已安装插件的测试状态。 |
| Parameters | `pluginId` 不能为空或空白；`cancellationToken` 在状态变更前观察。 |
| Return | 更新后的 `PluginTestRecord`。 |
| Nullability | `pluginId` 不接受 null；返回值不能为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不提交状态变更。 |
| Exceptions or Result | 未安装插件抛 `KeyNotFoundException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | 重复 activate/deactivate 将状态稳定设置为目标状态。 |
| Concurrency | 同一 plugin id lifecycle 由测试串行驱动。 |
| Side Effects | 修改 record state。 |
| Diagnostics | 成功状态切换不写诊断。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHost.RegisterContribution`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 记录插件贡献 owner，用于 unload 或 Dispose 时断言 revoke。 |
| Parameters | `pluginId` 和 `contributionId` 不能为空或空白。 |
| Return | 更新后的 `PluginTestRecord`。 |
| Nullability | 参数不接受 null；返回值不能为空。 |
| Cancellation | 无。 |
| Exceptions or Result | 未安装插件抛 `KeyNotFoundException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | 同一 contribution id 重复注册只保留一个 owner entry。 |
| Concurrency | 同一 plugin id 由测试串行驱动。 |
| Side Effects | 修改 record contributions snapshot。 |
| Diagnostics | register 成功不写诊断；unload revoke 写 `AUCTEST401`。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHost.UnloadAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 卸载测试插件，撤销该插件的所有 contribution owner。 |
| Parameters | `pluginId` 不能为空或空白；`cancellationToken` 在状态变更前观察。 |
| Return | 更新后的 `PluginTestRecord`。 |
| Nullability | `pluginId` 不接受 null；返回值不能为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不提交 unload。 |
| Exceptions or Result | 未安装插件抛 `KeyNotFoundException`；Dispose 后调用抛 `ObjectDisposedException`。 |
| Idempotency | 重复 unload 保持 Unloaded 状态，不重复增加 revoke counter。 |
| Concurrency | 同一 plugin id lifecycle 由测试串行驱动。 |
| Side Effects | 清空 contributions，设置 state 为 Unloaded。 |
| Diagnostics | 撤销至少一个 contribution 时写 `AUCTEST401`，消息包含 plugin id 和 revoke count。 |
| Tests | `PluginTestHostTests`。 |

### `PluginTestHost.Dispose` 和 `PluginTestHost.DisposeAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-005 |
| Purpose | 自动 unload 所有未卸载插件并释放内部 `TestHost`。 |
| Parameters | 无。 |
| Return | `Dispose` 无返回；`DisposeAsync` 返回释放完成的 `ValueTask`。 |
| Nullability | 无。 |
| Cancellation | Dispose 不接收 token。 |
| Exceptions or Result | 重复 Dispose 幂等；Dispose 后 mutating API 抛 `ObjectDisposedException`。 |
| Idempotency | 幂等，不重复增加 revoke counter。 |
| Concurrency | 单测试线程串行释放。 |
| Side Effects | 清空所有 record contributions，设置未卸载 record 为 Unloaded。 |
| Diagnostics | 自动 revoke contribution 时写 `AUCTEST401`；内部 Host dispose 写 `AUCTEST001`。 |
| Tests | `PluginTestHostTests`。 |

### `RoutingTestHostBuilder.MapRoute`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-006 |
| Purpose | 声明测试 route name、pattern 和 view model target。 |
| Parameters | `name`、`pattern` 不能为空或空白；`viewModelType` 不能为 null。 |
| Return | 当前 `RoutingTestHostBuilder`。 |
| Nullability | 参数不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | 参数非法抛声明异常；Build 后调用抛 `InvalidOperationException`。 |
| Idempotency | 每次调用追加一个 route；duplicate route name 或 normalized pattern 在 Build 时失败。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 只修改 builder 内部 route registration。 |
| Diagnostics | Build 前不写诊断。 |
| Tests | `RoutingTestHostTests`。 |

### `RoutingTestHostBuilder.Build`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-006 |
| Purpose | 冻结 builder，验证 route conflict，并创建 immutable route graph snapshot。 |
| Parameters | 无。 |
| Return | 新的 `RoutingTestHost`。 |
| Nullability | 返回值不能为空。 |
| Cancellation | 同步构建，不接收 token。 |
| Exceptions or Result | 重复 Build、duplicate route name 或 duplicate pattern 抛 `InvalidOperationException`。 |
| Idempotency | 每个 builder 只允许成功 Build 一次。 |
| Concurrency | Build 必须外部串行。 |
| Side Effects | 创建 route snapshot 和 test diagnostics。 |
| Diagnostics | match/navigation failure 写入返回 Host 的 diagnostics。 |
| Tests | `RoutingTestHostTests`。 |

### `RoutingTestHost.Match`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-006 |
| Purpose | 匹配 path 到 route target，并抽取 route parameters。 |
| Parameters | `path` 不能为空或空白。 |
| Return | `RouteTestMatch`。 |
| Nullability | `path` 不接受 null；返回值不能为空。 |
| Cancellation | 无。 |
| Exceptions or Result | 未匹配时返回 not-found result，不抛异常。 |
| Idempotency | 只读；重复匹配同一路径返回等价 result。 |
| Concurrency | route snapshot 可并发读取。 |
| Side Effects | not found 时写 diagnostics。 |
| Diagnostics | not found 写 `AUCTEST501`，消息包含 path。 |
| Tests | `RoutingTestHostTests`。 |

### `RoutingTestHost.NavigateAsync`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-006 |
| Purpose | 提供可取消 navigation helper，复用 `Match` 的 route graph 结果。 |
| Parameters | `path` 不能为空或空白；`cancellationToken` 在 match 前观察。 |
| Return | 匹配完成后的 `RouteTestMatch`。 |
| Nullability | `path` 不接受 null；返回 task 不为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不写 not-found diagnostic。 |
| Exceptions or Result | 未匹配时返回 not-found result。 |
| Idempotency | helper 不提交真实 navigation 状态。 |
| Concurrency | 测试默认串行调用。 |
| Side Effects | 未取消时与 `Match` 一致。 |
| Diagnostics | not found 写 `AUCTEST501`。 |
| Tests | `RoutingTestHostTests`。 |

### `SourceGenerationTestCase.Run`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-007 |
| Purpose | 在内存 compilation 中运行 Roslyn `ISourceGenerator`，生成稳定 snapshot 和 diagnostics。 |
| Parameters | `generator` 不能为 null；`cancellationToken` 在 parse 和 driver 执行前观察。 |
| Return | `SourceGenerationTestResult`。 |
| Nullability | `generator` 不接受 null；返回值不能为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不返回部分 result。 |
| Exceptions or Result | Roslyn diagnostic 不抛出，保留在 result；generator 抛出的异常由 Roslyn diagnostic 表达。 |
| Idempotency | 同一 test case 可重复 Run，每次创建独立 compilation 和 driver。 |
| Concurrency | test case 输入集合只读时可并发运行。 |
| Side Effects | 不写文件。 |
| Diagnostics | result 暴露 driver diagnostics 和 output compilation diagnostics。 |
| Tests | `SourceGenerationTestKitTests`。 |

### `SourceGenerationTestResult`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-007 |
| Purpose | 保存 source generation run 的 snapshot 和 diagnostics。 |
| Parameters | 由 `SourceGenerationTestCase.Run` 创建。 |
| Return | 不适用。 |
| Nullability | `Snapshot` 和 diagnostics 集合不得为空。 |
| Cancellation | 无。 |
| Exceptions or Result | diagnostics 集合为 immutable snapshot。 |
| Idempotency | immutable result。 |
| Concurrency | 可并发读取。 |
| Side Effects | 无。 |
| Diagnostics | `Diagnostics` 保存 generator diagnostics；`CompilationDiagnostics` 保存 output compilation diagnostics。 |
| Tests | `SourceGenerationTestKitTests`。 |

### `AotCompatibilityCheck.ForbidDefaultAotPatterns`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-008 |
| Purpose | 注册 Testing 内置 AOT 风险规则，包括 runtime reflection scan、unbounded activator 和 dynamic code。 |
| Parameters | 无。 |
| Return | 当前 `AotCompatibilityCheck`。 |
| Nullability | 无。 |
| Cancellation | 无。 |
| Exceptions or Result | 重复注册同一 diagnostic id 和 pattern 时不重复添加。 |
| Idempotency | 幂等。 |
| Concurrency | 配置阶段不保证线程安全。 |
| Side Effects | 修改内部 forbidden pattern rule set。 |
| Diagnostics | Evaluate 时返回匹配 diagnostics。 |
| Tests | `AotCompatibilityCheckTests`。 |

### `AotCompatibilityCheck.Evaluate`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-008 |
| Purpose | 扫描 source files，返回命中的 AOT risk diagnostics。 |
| Parameters | `sources` 不能为 null；`cancellationToken` 在每个 source 和 rule 扫描前观察。 |
| Return | immutable `AotCompatibilityDiagnostic` snapshot。 |
| Nullability | `sources` 不接受 null；返回集合不为空。 |
| Cancellation | token 取消时抛 `OperationCanceledException`，不返回部分 diagnostics。 |
| Exceptions or Result | source 为 null 时由枚举访问抛出；pattern match 不抛异常。 |
| Idempotency | 同一输入重复 Evaluate 返回等价 diagnostics。 |
| Concurrency | rule set 不变时可并发 Evaluate。 |
| Side Effects | 无。 |
| Diagnostics | diagnostic message 包含 source path 和 pattern。 |
| Tests | `AotCompatibilityCheckTests`。 |

### `TestLayerNames.AllCategories` 和 `IsKnownCategory`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-009 |
| Purpose | 暴露标准测试层级 category snapshot，并判断字符串 category 是否为标准层级。 |
| Parameters | `IsKnownCategory` 的 `category` 不能为空或空白。 |
| Return | `AllCategories` 返回 immutable snapshot；`IsKnownCategory` 返回 bool。 |
| Nullability | category 不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | category 非法抛 `ArgumentException`。 |
| Idempotency | 只读。 |
| Concurrency | 可并发读取。 |
| Side Effects | 无。 |
| Diagnostics | 不写诊断。 |
| Tests | `TestLayerTests`。 |

### `TestLayerAttribute`

| Field | Contract |
| --- | --- |
| Feature | AUC-TESTING-009 |
| Purpose | 标记测试方法或测试类的标准层级。 |
| Parameters | `TestLayer` enum 或标准 category string。 |
| Return | Attribute metadata。 |
| Nullability | string category 不接受 null。 |
| Cancellation | 无。 |
| Exceptions or Result | 未知 enum 或未知 string category 抛声明异常。 |
| Idempotency | metadata immutable。 |
| Concurrency | 可并发读取。 |
| Side Effects | 无。 |
| Diagnostics | 不写诊断；外部门禁可读取 metadata 输出缺失 layer。 |
| Tests | `TestLayerTests`。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `AotCompatibilityCheck` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `AotCompatibilityDiagnostic` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DeterministicScheduler` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DeterministicScheduledWorkItem` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `DisposableTracker` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ExpectedDiagnostic` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `FakeUiDispatcher` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `FakeUiWorkItem` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratedSource` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GeneratedSourceSnapshot` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleTestHost` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleTestHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ModuleTestRecord` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginTestHost` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginTestHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginTestRecord` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginTestState` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteTestDefinition` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RouteTestMatch` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RoutingTestHost` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `RoutingTestHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SourceFile` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SourceGenerationTestCase` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `SourceGenerationTestResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestDiagnosticEntry` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestDiagnostics` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestDirectory` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestHost` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestHostBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestLayer` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestLayerAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TestLayerNames` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
