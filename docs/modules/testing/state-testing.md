# AtomUI.City.Testing State Testing 合同

> 当前实现说明（2026-10-14）：本页后半部分的 `StateTestStore`、状态记录器、computed/snapshot 断言和 scoped state host 仍是目标设计，尚未分配独立 Feature ID，也未进入 `AtomUI.City.Testing` 源码或 public API。当前可用能力以 [features.md](features.md) 和 [api-contracts.md](api-contracts.md) 为准；在相应 Feature 完成前，不得把这些目标类型写成已经交付的测试工具。

## 适用范围

本专题属于 `AtomUI.City.Testing` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `State Testing` 相关实现决策，不重新定义模块边界。

## 设计决策

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

## State 测试设计

适用范围：应用状态、作用域状态、订阅、computed、snapshot、dispatcher、插件状态和线程安全

### 1. 目标

State 测试必须证明状态读写、通知、快照和释放在多线程桌面环境中可控。

### 2. StateTestStore

Testing 的目标设计包括（当前尚未实现）：

- fake application state。
- scoped state host。
- state change recorder。
- computed invalidation assertion。
- snapshot assertion。
- subscription leak assertion。
- dispatcher target assertion。

### 3. 单元测试范围

必须覆盖：

- state get/set。
- 相等值不通知。
- change version。
- subscription notification。
- subscription disposal。
- computed cache。
- computed invalidation。
- snapshot save。
- snapshot restore。
- invalid mutation。
- diagnostics。

### 4. 线程测试

必须覆盖：

- background mutation。
- UI dispatch notification。
- concurrent mutation policy。
- late notification suppression。
- Scope stop 后 notification 不执行。

### 5. 插件状态测试

必须覆盖：

- 插件状态隔离。
- 插件停用取消 subscription。
- 插件卸载释放 state reaction。
- 插件状态 snapshot。
- 插件配置和状态不写安装目录。

### 6. 集成测试范围

Framework integration test 覆盖：

```text
State
-> ViewModel reaction
-> Fake dispatcher
-> Lifecycle scope
```

真实 UI 绑定刷新由 Presentation 平台集成测试覆盖。
