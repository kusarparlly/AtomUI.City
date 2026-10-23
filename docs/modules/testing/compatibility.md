# AtomUI.City.Testing Compatibility

## 兼容性范围

本模块兼容面包括 public API、options、attribute、diagnostics code、manifest/schema、generated output、MSBuild property、CLI envelope、template layout、snapshot 或 plugin contract 中实际适用的部分。

## 模块兼容性硬边界

- 生产项目不得引用 Testing。
- 测试不得依赖固定 Task.Delay。
- 释放、取消、unload、dispatcher 和 generated output 必须有断言。

## API 兼容规则

- public 类型、成员、枚举值、attribute 参数和扩展方法默认视为兼容性承诺。
- 删除、重命名、改变默认行为、异常类型、Result status 或诊断码语义属于 breaking change。
- 新增 API 可以 minor 版本发布，但必须有文档、测试和迁移说明。
- `AUC-TESTING-010` 的七个 Data test doubles 由 Testing 包拥有兼容性责任；Data 文档中的测试需求不能替代这些 public 类型的 API 合同。
- scripted response/credential 的 FIFO 消费、空队列结果、`DataResultStatus` 映射、记录快照字段和 `FakeDataConnection` 状态/计数语义均属于 Preview 兼容面；变更必须同步 Feature、API 卡与 `DataTestDoublesTests`。
- Data 测试替身不得进入生产依赖图；未来新增 Data contract 时，应由 Testing 增量提供测试实现，不能让 Data 反向引用 Testing。
- Preview API `AotCompatibilityCheck` / `AotCompatibilityDiagnostic` 已分别更名为 `AotSourcePatternCheck` / `AotSourcePatternDiagnostic`，`ForbidDefaultAotPatterns` 已更名为 `ForbidDefaultAotRiskPatterns`。这是对既有 ordinal string scan 原意的命名纠正，不改变 pattern matching 算法；旧名称不再作为公开 API 或 AOT gate。真实兼容性由 Build strict analyzer 和 NativeAOT publish/run 负责。

## 数据格式兼容

- manifest、snapshot、generated output、CLI JSON、template variables 和 MSBuild properties 必须有版本或稳定字段说明。
- reader 必须拒绝高于支持版本的不可理解格式，并输出稳定诊断。
- 生成输出 hint name、type name 和 field name 改变属于兼容性风险。

## 插件兼容

- 跨插件边界 contract 必须来自 Host 共享程序集。
- 插件依赖的 capability id、manifest 字段、event contract、route target、state key、permission id 改变必须提供迁移策略。

## 废弃规则

废弃 API 必须说明 Deprecated Since、Replacement、Removal Earliest Version、Migration、Analyzer Diagnostic。
