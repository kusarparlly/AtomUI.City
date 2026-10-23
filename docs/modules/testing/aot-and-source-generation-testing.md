# AtomUI.City.Testing AOT And Source Generation Testing 合同

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `AOT And Source Generation Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

- 优先 source generator，避免运行时反射扫描。
- generated output 必须稳定排序。
- diagnostics 必须可由 generator tests 断言。
- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Testing` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-TESTING-001 | Test Host | TestHostTests |
| AUC-TESTING-002 | Fake Dispatcher | FakeUiDispatcherTests |
| AUC-TESTING-003 | Deterministic Scheduler | SharedTestUtilitiesTests |
| AUC-TESTING-004 | Module Test Host | ModuleTestHostTests |
| AUC-TESTING-005 | Plugin Test Host | PluginTestHostTests |
| AUC-TESTING-006 | Routing Test Host | RoutingTestHostTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `生产运行时反向依赖 AtomUI.City.Testing` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AOT 和 Source Generator 测试设计

适用范围：source generator、analyzer、manifest 生成、AOT/trimming diagnostics、Build target 和静态插件测试

### 1. 目标

AtomUI.City 把 AOT 友好作为全局约束。Testing 必须支持编译期能力的验证，避免实现依赖运行时反射扫描。

### 2. Source Generator 测试

必须支持：

- 输入源码。
- 运行 generator。
- 断言生成源码。
- 断言生成 manifest。
- 断言 diagnostics。
- 断言 incremental generator cache 行为。

生成结果应优先做结构化断言。源码 snapshot 可作为补充。

### 3. Analyzer 测试

必须覆盖：

- 正确代码无诊断。
- 错误代码有诊断。
- diagnostic id。
- diagnostic location。
- code fix，如果该能力存在。

### 4. Manifest 测试

必须覆盖：

- module manifest。
- route manifest。
- plugin manifest。
- localization index。
- permission index。
- data client index。
- presentation mapping。

Manifest 字段顺序必须稳定。

### 5. AOT 和 trimming 测试

`AotSourcePatternCheck` 只提供快速源码字符串 lint：它可以固定团队禁止文本，但不解析 C# 语义、不检查依赖闭包、不运行 linker/analyzer，也不产生 “AOT compatible” 结论。下列验收项由 Build strict analyzer、应用 NativeAOT publish 和原生进程运行实现，不能由 pattern lint 代替。

必须覆盖：

- dynamic discovery 在 strict AOT 模式下被拒绝或诊断。
- 未声明反射被诊断。
- source-generated options binding。
- source-generated serialization context。
- Native AOT 模式拒绝动态插件。
- 静态插件 manifest 生效。

### 6. Build Target 测试

Build 模块实现后，Testing 应支持：

- MSBuild target 执行。
- package layout 验证。
- output 目录验证。
- generated artifact 验证。
- diagnostic code 验证。

### 7. 测试要求

编译期功能点必须有 generator/analyzer/build test。运行时集成测试不能替代编译期测试。
