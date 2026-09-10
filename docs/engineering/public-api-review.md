# 公共 API Review 门禁

版本：v0.1
状态：强制执行
适用范围：所有 public 类型、public 成员、attribute、options、manifest/schema、generated output、MSBuild property、CLI JSON envelope、template variable 和 plugin contract

## 1. 目标

公共 API review 用于阻止未确认的框架 contract 进入实现和发布。

AtomUI.City 是框架项目。public API 一旦发布，就会影响应用代码、插件包、模板输出、source generator、CLI、文档和测试。因此，公共 API 不能只依赖源码命名和测试通过来确认，必须先有行为合同，再进入实现。

## 2. 适用对象

以下内容都属于公共 API review 范围：

- `public` 类型、成员、构造函数、枚举值、record 字段和扩展方法。
- Attribute 构造参数和命名参数。
- Options 类型、默认值、校验规则和 reload 行为。
- Result、Error、Diagnostic、Snapshot、Manifest、Descriptor 和 Context 类型。
- Source generator 输入 attribute、输出类型、hint name、generated member name 和 analyzer diagnostic。
- MSBuild property、item、target、task、输出路径和 package layout。
- CLI 命令名、参数、exit code、JSON envelope、diagnostic schema 和 artifact schema。
- Template 变量、目录结构、生成文件名和 package id。
- Plugin manifest、capability id、contribution id、shared contract、lock file 和 install record。

内部类型如果被 generated output、template、plugin、test utility 或 manifest 间接依赖，也必须按 public contract 处理。

## 3. 开工前门禁

涉及公共 API 的功能点进入实现前，必须满足：

- 对应 Feature ID 已存在。
- `api-contracts.md` 中已经有 API card。
- API card 覆盖 namespace、assembly、stability、owner、lifetime、DI lifetime、thread safety、disposal、breaking change rules 和 tests。
- 每个关键方法都有 method contract。
- `features.md` 中的失败行为、诊断和测试条目与 API card 一致。
- `testing.md` 中有测试矩阵行。
- 如果涉及 manifest、generated output、MSBuild、CLI 或 template，必须在 `compatibility.md` 写明版本和 breaking change 规则。

没有 API card 的 public API 不允许实现。

## 4. API Card 格式

每个关键 public 类型必须使用以下格式：

```text
Type:
Namespace:
Assembly:
Stability:
Feature:
Purpose:
Owner:
Created By:
Lifetime:
DI Lifetime:
Thread Safety:
Disposal:
Nullability:
Cancellation:
Failure Behavior:
Diagnostics:
Plugin Boundary:
AOT / Trimming:
Breaking Change Rules:
Tests:
```

字段规则：

- `Stability` 只能是 `Experimental`、`Preview`、`Stable` 或 `InternalContract`。
- `Owner` 必须能映射到 Host、Module、Plugin、Route、Operation、Connection、View、Test scope 或 Build process。
- `Thread Safety` 必须写清是否可并发读取、可并发写入、需要外部锁或只允许单线程访问。
- `Disposal` 必须说明 Dispose 后读取、写入和重复释放行为。
- `Failure Behavior` 必须说明抛异常、返回 Result、写诊断或组合策略。
- `Tests` 必须指向具体测试文件和必须断言的行为。

## 5. Method Contract 格式

每个关键 public 方法必须使用以下格式：

```text
Method:
Feature:
Purpose:
Parameters:
Return:
Nullability:
Cancellation:
Exceptions or Result:
Idempotency:
Concurrency:
Side Effects:
Diagnostics:
Tests:
```

关键方法包括：

- 创建、构建、加载、启动、停止、卸载、释放。
- 注册、撤销、发布、提交、写入、迁移。
- 导航、请求、调度、执行 handler、执行 generator、运行 CLI command。
- 任何改变状态、生命周期、manifest、generated output 或外部文件的 API。

## 6. Breaking Change 判定

以下改动默认属于 breaking change：

- 删除、重命名或移动 public 类型或成员。
- 修改 namespace、assembly、package id 或 generated type name。
- 修改默认值、默认策略、默认诊断 severity 或默认线程行为。
- 修改异常类型、Result status、错误码、诊断码语义或 context 字段。
- 修改 manifest required 字段、schema version 兼容策略或 unknown field 策略。
- 修改 CLI JSON envelope、exit code、artifact 字段或 machine-readable message contract。
- 修改 template variable 名称、含义、默认值或生成路径。
- 修改 plugin capability id、contribution id、shared contract 版本规则或 lock file 字段。

非 breaking change 必须满足：

- 旧调用方无需修改即可继续编译和运行。
- 旧 manifest、snapshot、template、CLI JSON 或 generated output 仍可被当前 reader 理解。
- 新行为有测试证明兼容。

## 7. Review 清单

公共 API review 必须逐项确认：

| 检查项 | 必须回答 |
| --- | --- |
| 模块边界 | 该 API 是否属于当前模块？是否违反包依赖方向？ |
| 使用者 | 应用、插件、模板、CLI、generator 或测试谁会调用？ |
| 生命周期 | 谁创建、谁持有、谁释放？Dispose 后如何表现？ |
| 线程 | 是否可并发？是否需要 UI dispatcher？是否可能死锁？ |
| 取消 | cancellation 何时生效？取消后是否可能提交副作用？ |
| 错误 | 抛异常、返回 Result、写诊断的边界是什么？ |
| 诊断 | code、severity、context、recovery 和 tests 是否稳定？ |
| 插件 | 插件来源对象是否有 owner？卸载时如何撤销？ |
| AOT | 是否依赖运行时扫描、dynamic code、表达式树编译或反射构造？ |
| 兼容 | 哪些改动是 breaking change？迁移策略是什么？ |
| 测试 | 是否有 Unit 或 Contract 测试？生命周期/线程/插件是否有专项测试？ |

## 8. 发布前门禁

发布前必须完成：

- 每个发布包的 public API diff review。
- 每个新增或变更 public API 都能追踪到 API card。
- 每个 API card 的测试文件存在，并覆盖必断言行为。
- 每个新增诊断码在源码、文档和测试中一致。
- 每个 schema、generated output、CLI envelope 和 template variable 的兼容规则已写入文档。
- `engineering/check-public-api.sh` 通过。

公共 API review 未通过时，不允许进入 pack、tag 或 publish。

### 8.1 Core、EventBus 与 Presentation 的机械门禁

`AtomUI.City.Core`、`AtomUI.City.EventBus` 和 `AtomUI.City.Presentation` 当前作为冻结对象执行以下机械检查：

- `PublicAPI.Shipped.txt` 保存已经审阅并冻结的签名；删除、改名、移动或修改签名会使编译失败。
- `PublicAPI.Unshipped.txt` 只保存本轮尚未随版本发布的新签名。新增 API 不允许通过关闭分析器或手工忽略诊断绕过 review。
- `Microsoft.CodeAnalysis.PublicApiAnalyzers` 的 API diff、可选参数、重载和兼容性诊断按编译错误处理。
- Core 单独重新启用 `CS1591` 并按错误处理；EventBus 与 Presentation 保留 API card 为规范文档，并必须随包生成 XML documentation 文件。
- Release 构建同时编译 `net10.0` 和生产基线 `net8.0`；SDK package validation 以 strict compatible-framework 模式比较两个程序集的 API。
- `engineering/check-public-api.sh` 必须真实执行三个冻结模块的 Release build 与 pack，不能只检查 baseline 或 XML 文件是否存在。

由于当前仍处于首次公开发布前，现有审计结果直接形成首份 `PublicAPI.Shipped.txt`。首次 NuGet 版本发布后，还必须配置 package validation baseline version，将新包与已发布包进行二进制兼容比较。

## 9. 首批冻结范围

当前实现冻结以下 API 合同：

- `AtomUI.City.Core` 的 Host、Lifecycle、Modularity、DI marker、Diagnostics 和 UI dispatcher abstraction。
- `AtomUI.City.EventBus` 的 contract、channel、subscription、backpressure、plugin contribution 和 diagnostics。
- `AtomUI.City.Presentation` 的 runtime、Window/Outlet transaction、View、Interaction、resource、plugin cleanup、failure 和 diagnostics。
- `AtomUI.City.Testing` 的 TestHost、FakeUiDispatcher、DeterministicScheduler、ModuleTestHost、PluginTestHost、RoutingTestHost、SourceGenerationTestCase、AOT check 和 TestLayer。

Routing、State、Data、PluginSystem、Build、Generators、CLI 和 Templates 可以继续使用现有设计文档推进细化，但不能在未通过对应门禁前标记产品级完成。
