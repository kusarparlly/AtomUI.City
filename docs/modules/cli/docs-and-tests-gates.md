# AtomUI.City.Cli Docs And Tests Gates 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Docs And Tests Gates` 相关实现决策，不重新定义模块边界。

## 设计决策

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 释放、取消和诊断必须断言。

## Public Contract

- 只允许通过 `AtomUI.City.Cli` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-CLI-001 | Command Model | CliCommandArchitectureTests |
| AUC-CLI-002 | New App | CliNewAppTests |
| AUC-CLI-003 | Build and Test | CliBuildAndTestCommandTests |
| AUC-CLI-004 | Plugin Inspect Doctor | CliInspectDoctorPluginTests |
| AUC-CLI-005 | AI Envelope | CliCommandArchitectureTests |
| AUC-CLI-006 | Non-Interactive and CI Mode | CliCommandArchitectureTests |
| AUC-CLI-007 | Generation Commands | CliGenerationCommandTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation runtime UI` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## CLI 文档和测试门禁命令设计

适用范围：`docs check`、`tests check`、文档先行检查、测试矩阵检查和功能点测试门禁

### 1. 目标

CLI 必须能检查 AtomUI.City 的工程治理规则，帮助开发者和 AI Agent 在实现前发现文档和测试缺口。

### 2. 命令

```bash
atomui city docs check
atomui city tests check
```

### 3. Docs Check

检查：

- 模块是否有 overview。
- 复杂模块是否有 detailed design。
- 文档链接是否有效。
- 模块文档是否包含测试矩阵。
- 功能点是否进入测试矩阵。
- 公共 API 是否缺设计文档。
- 文档中是否存在明显占位内容。

### 4. Tests Check

检查：

- 功能点是否有单元测试。
- 是否存在只用集成测试替代单元测试的情况。
- 生命周期功能是否有取消和释放断言。
- 插件功能是否有 Lease、Operation、UnloadPending 断言。
- source generator 是否有 generator test。
- analyzer 是否有 analyzer test。
- Build target 是否有 build test。

### 5. 输出

JSON diagnostics 示例：

```json
{
  "code": "AUCCLI0201",
  "severity": "Error",
  "message": "Feature test matrix is missing.",
  "details": {
    "module": "Routing",
    "document": "docs/modules/routing/detailed-design.md"
  },
  "suggestedActions": [
    "Add a test matrix row for the feature."
  ],
  "documentationLinks": [
    "docs/modules/testing/feature-test-gate.md"
  ]
}
```

### 6. 规则

- check 命令默认只读。
- check 命令不自动修复。
- `--json` 输出可被 CI 和 AI Agent 解析。
- 失败时返回非零 exit code。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| docs check | Unit | 缺 overview、缺矩阵、断链。 |
| tests check | Unit | 缺单测、缺释放断言。 |
| JSON diagnostics | Unit | code、details、links。 |
| exit code | Unit | 有错误返回非零。 |
| read-only | Unit | check 不写文件。 |
