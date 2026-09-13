# AtomUI.City.Cli Ai Integration 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Ai Integration` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

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

## CLI AI 集成设计

适用范围：AI-friendly 输出、JSON schema、plan/apply、inspect、doctor、explain、docs/tests gate 和 Agent 调用边界

### 1. 目标

CLI 必须从第一版支持 AI Agent 和自动化工具稳定调用。

AI 友好不是增加聊天能力，而是让命令输出、计划、诊断和执行行为可被机器可靠解析。

### 2. 核心原则

| 原则 | 说明 |
|---|---|
| Machine-readable first | 关键命令支持 JSON 输出。 |
| Dry-run first | 复杂操作支持 `--dry-run`。 |
| Plan/apply | 复杂写操作可先生成 plan，再执行。 |
| Stable schema | JSON schema、参数、exit code 稳定。 |
| Explainable diagnostics | 诊断有 code、原因、修复建议和文档链接。 |
| Workspace-aware | 能输出工作区结构化事实。 |
| Gate-aware | 能检查文档和测试门禁。 |
| No hidden prompt | 自动化模式下不出现隐式交互。 |

### 3. JSON 输出

统一结构：

```json
{
  "schemaVersion": "1.0",
  "command": "atomui city generate page",
  "success": true,
  "exitCode": 0,
  "diagnostics": [],
  "data": {},
  "artifacts": [],
  "suggestedCommands": [],
  "changedFiles": [],
  "retryable": false,
  "suggestedActions": [],
  "documentationLinks": []
}
```

规则：

- `--json` 模式不能输出彩色文本。
- 所有错误也必须输出相同 envelope。
- `schemaVersion` 变更必须兼容或提升主版本。
- `diagnostics` 必须是数组。
- `artifacts` 和 `changedFiles` 必须位于顶层，便于 agent 不解析业务 data 也能定位文件。
- 失败 envelope 必须根据 diagnostic code 生成 `suggestedCommands`。
- `retryable` 只表达同一命令在环境修复或暂时性失败后是否值得重试，不替代 exit code。

### 4. Plan / Apply

复杂操作支持：

```bash
atomui city plan generate page Orders/List --route /orders --json
atomui city apply .city/plans/2026-06-11-generate-page.json
```

Plan 文件包含：

```json
{
  "schemaVersion": "1.0",
  "operationId": "2026-06-11-generate-page",
  "command": "atomui city generate page",
  "inputs": {},
  "changes": [
    {
      "type": "create",
      "path": "src/App/Routes/Orders/ListViewModel.cs"
    }
  ],
  "buildTargets": [],
  "testTargets": [],
  "docsRequired": [],
  "risks": [],
  "rollback": []
}
```

规则：

- Plan 生成不修改业务文件。
- Apply 必须校验 plan schema。
- Apply 前检查文件是否被外部修改。
- Apply 结果输出 diagnostics。
- destructive 操作必须有 rollback 描述。

### 5. Inspect

AI Agent 可以使用 inspect 命令获取事实。

```bash
atomui city inspect workspace --json
atomui city inspect module Sales --json
atomui city inspect manifest --json
```

inspect 命令只读，不修改文件。

### 6. Doctor 和 Explain

```bash
atomui city doctor --json
atomui city explain AUCCLI0201 --json
```

`explain` 输出：

- diagnostic code。
- 触发原因。
- 严重级别。
- 修复建议。
- 相关文档。
- 相关命令。

### 7. Agent 调用边界

CLI 可以：

- 生成符合模板的结构。
- 输出工作区事实。
- 生成 dry-run plan。
- 检查文档和测试门禁。
- 执行明确的 build/template/plugin 操作。

CLI 不应该：

- 根据自然语言自由生成业务代码。
- 绕过文档确认和测试门禁。
- 自动修改未确认的大范围文件。
- 在非交互模式下弹 prompt。
- 输出只有人能读懂的错误。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| JSON envelope | Unit | 成功和失败输出结构。 |
| plan schema | Unit | 必填字段、非法 schema。 |
| dry-run | Unit/CLI | 不写文件。 |
| apply | CLI | 校验 plan、写文件、输出诊断。 |
| inspect | Unit/CLI | 只读、结构化输出。 |
| explain | Unit | code、建议、文档链接。 |
