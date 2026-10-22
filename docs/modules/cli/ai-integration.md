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
| AUC-CLI-008 | Repository Governance Gates | CliGovernanceGateTests |
| AUC-CLI-009 | Plan and Apply Transaction | CliPlanApplyTests |
| AUC-CLI-010 | Diagnostic Explain Catalog | CliExplainTests |

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

本节是 `AUC-CLI-009` 的规范来源。第一版把已有非插件 generation 分成“生成并保存计划”和“校验后执行计划”两步：

```bash
atomui city plan generate page Orders/List --route /orders --json
atomui city apply .city/plans/generate-page-4f9c31a2e7d0.json --yes --json
```

#### 4.1 第一版能力边界

| 允许 | 明确拒绝 |
|---|---|
| `generate module`、`page`、`test`、`config`、`localization` | `new app`、plugin install/update/remove、build/test/pack/publish |
| UTF-8 文本文件 `create` | update、delete、move、二进制文件 |
| City 自身 Templates 作为唯一受支持 producer | 第三方 producer 无兼容保证；任意 command/shell/script 一律拒绝 |
| workspace 内 `.city/plans` 文件 | stdin、URL、workspace 外或符号链接逃逸的 plan/target |

`plan` 可以创建 `.city/plans` 和 plan 文件，但不得创建、覆盖或删除业务文件。`generate --dry-run` 继续用于即时预览；可持久化、可重放的 plan 只能由本命令生成。

Templates 当前公开 `TemplatePlan` 只描述变更路径，不携带生成内容。第一版不得复制模板实现或为 CLI 扩大 Templates public API；plan handler 必须在系统 temp 下的专属 `AtomUI.City.Cli/preview/<random-id>` 创建本次唯一 preview root，使用现有 `GenerationTemplateRenderer.Render` 生成预览，从成功结果声明的 changes 精确读取 UTF-8 内容，然后在写 plan 前清理整个 preview root。递归清理前必须重新解析并证明目标仍位于专属 preview base、不是 base 自身且不是 symlink/reparse escape，禁止对未验证路径执行删除。模板失败原样返回 `AUCTPL...`；成功、失败和取消都必须尝试清理，清理失败返回 `AUCCLI0715` 且不得持久化 plan。

#### 4.2 Plan 1.0 Schema

第一版只接受 `schemaVersion` 精确为 `1.0`。未知字段、缺失字段、重复属性、重复目标和未知枚举值均拒绝，不能静默忽略或降级。

```json
{
  "schemaVersion": "1.0",
  "operationId": "generate-page-4f9c31a2e7d0",
  "workspaceId": "sha256:7ac4...",
  "command": "atomui city generate page",
  "inputs": {
    "kind": "page",
    "name": "Orders/List",
    "route": "/orders"
  },
  "changes": [
    {
      "changeId": "change-0001",
      "type": "create",
      "path": "src/App/Routes/Orders/ListViewModel.cs",
      "expectedState": "absent",
      "encoding": "utf-8",
      "content": "namespace App.Routes.Orders;\n",
      "contentSha256": "sha256:2fd4..."
    }
  ],
  "buildTargets": ["src/App/App.csproj"],
  "testTargets": [],
  "docsRequired": [],
  "risks": [],
  "rollback": [
    {
      "changeId": "change-0001",
      "action": "delete-if-content-hash-matches"
    }
  ]
}
```

字段合同：

- `operationId` 使用 `generate-<kind>-<canonical-operation-payload-sha256 前 12 位小写十六进制>`；canonical operation payload 包含 workspaceId、command、规范化 inputs、changes 与验证目标，但明确排除 operationId 本身，避免循环计算。相同 workspace 快照、规范化输入和模板输出必须得到相同 id。
- `workspaceId` 是规范化绝对 workspace root 的 SHA-256，不暴露绝对路径；第一版 plan 不可跨 workspace 或移动后重放。
- `command` 必须是受支持的 canonical command identity，不作为 shell 文本执行；`inputs` 只传给对应的内部 generation adapter。
- `path` 使用 `/`、相对 workspace root、不得为空、绝对化、包含 `.`/`..`、设备路径、NUL，或经过 symlink/reparse point 逃出 root；重复与大小写碰撞按目标文件系统的 path comparison 语义判断。
- `content` 是将要写入的完整 Unicode 文本；应用时按无 BOM UTF-8 编码，换行保持 plan 中内容，不做平台转换。
- `contentSha256` 对上述 UTF-8 bytes 计算；apply 必须重算并匹配。`expectedState` 第一版只能为 `absent`。
- `rollback` 必须与每个 change 一一对应；第一版唯一 action 为 `delete-if-content-hash-matches`。
- `changes` 最多 256 项，单项 UTF-8 content 最多 1 MiB，整个 plan UTF-8 文件最多 4 MiB；超限必须失败，不能截断。
- plan 使用固定 schema property 顺序、稳定数组顺序、LF 和无 BOM UTF-8 序列化；plan digest 对完整 plan bytes 计算，用于 journal 和重复 apply 身份判断。

Plan 1.0 的 hash 用于一致性、竞争和幂等判断，不是数字签名，也不证明 producer 身份。Apply 始终把文件视为不可信输入；第一版不承诺第三方 producer 兼容，但不能仅凭 producer 声明放宽任何校验。

Plan 文件原子写入 `.city/plans/<operationId>.json`。同名且 bytes 相同可以复用；同名但内容不同视为完整性故障，禁止覆盖。

#### 4.3 Apply 安全与事务

Apply 必须按以下顺序执行：

1. 识别 workspace，验证 plan file 位于该 workspace 的 `.city/plans`，并应用大小上限。
2. 将 JSON 作为不可信输入严格解析；验证 schema、operationId、workspaceId、所有 path、枚举、数量、content hash 和 rollback 对应关系。
3. 获取 `.city/transactions/apply.lock` 的 workspace 级独占文件锁；不排队等待，锁冲突快速失败。
4. 对全部 change 做 preflight：目标必须不存在，父路径不得通过 symlink/reparse point 逃出 workspace；任一失败时不创建业务文件。
5. 在 `.city/transactions/<operationId>/staging` 写入全部内容并复核 hash，持久化 journal 后才进入 commit。
6. commit 只把 staging 文件原子移动到仍然不存在的目标；目标在 preflight 后出现也必须冲突，禁止覆盖。
7. 任一 commit 失败时，按逆序删除本事务已创建且 hash 仍匹配的文件；不匹配的文件不得删除，并记录 rollback failure。
8. 全部成功后把 journal 标记为 `completed`，输出实际 `changedFiles`。相同 operationId 和 plan digest 的 completed journal 再次 apply 返回成功 no-op；digest 不同或没有可信 completed journal 时，既存目标仍然失败。

Apply 不能加载程序集、调用 shell、执行 plan 中的 command 字符串或访问网络。交互模式必须在 commit 前确认；CI、`--non-interactive` 或 stdin 不可用时必须提供 `--yes`，否则沿用 `AUCCLI0401`。

#### 4.4 取消与终态

- plan 在模板 preview、目录 IO 和写 plan 前后观察 cancellation；取消不留下临时 plan，并且必须清理本次专属 preview root 后才返回。
- apply 在读取、校验、preflight 和 staging 阶段立即响应 cancellation 并清理 staging。
- commit 开始后进入不可中断临界区：记录取消请求，但必须先完成 commit 或 rollback，工作区稳定后再返回取消失败。
- 原始 apply failure 与 rollback failure 必须聚合进入同一失败 envelope；不得因回滚异常覆盖原始故障。
- 所有失败输出 diagnostics 和非零 exit code；不存在“未执行却 success”或“部分提交却 success”。

#### 4.5 输出合同

Plan 成功的 `data` 至少包含 `operationId`、workspace-relative `planFile`、`planDigest` 和完整 `plan`；`artifacts` 列出计划目标但 `changedFiles` 为空。Apply 成功的 `data` 至少包含 `operationId`、`planFile`、`planDigest`、`transactionState`（`completed` 或 `already-completed`）；`changedFiles` 只列本次实际创建的文件。

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

`explain` 属于 `AUC-CLI-010`，是版本化诊断目录查询，不是自由文本搜索：

- 第一版覆盖 CLI 当前能够产生的 `AUCCLI...`，以及 CLI 明确原样透传的 `AUCTPL...`、`AUCPLG...` code。
- 输入去除首尾空白后使用 ordinal ignore-case 匹配，输出 canonical uppercase code。
- 已知 code 输出 `code`、`severity`、`summary`、`cause`、`suggestedActions`、`documentationLinks`、`relatedCommands` 和 `ownerModule`。
- 缺少 code 返回 `AUCCLI0801` 和 ArgumentError；未知 code 返回 `AUCCLI0802` 和 ArgumentError，不能返回通用 success 文本。
- 目录是编译进 CLI 的 immutable 数据，不在运行时读取 Markdown、扫描程序集、启动插件或访问网络。
- 每个 code 必须恰有一个条目；重复、空字段、未知 severity 或不受信链接产生 `AUCCLI0803`，并作为构建/contract test 阻断项。文档链接只允许 workspace-relative 文档路径，或 host 为 `atomui.net`、其子域名、`github.com/AtomUI` 的 HTTPS URL。
- `AUCCLI0802` 必须有静态 fallback 解释，未知 code 处理不得递归依赖一次可能失败的目录查找。

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
| plan schema | Unit/Contract | 完整 round-trip、严格字段、大小上限、未知 schema、hash、重复目标和路径逃逸。 |
| dry-run | Unit/CLI | 不写文件。 |
| apply | CLI/Integration | preflight、锁、staging、commit、重复 apply、竞争、取消、完整 rollback 和 rollback failure。 |
| inspect | Unit/CLI | 只读、结构化输出。 |
| explain | Unit/Contract | 全目录覆盖/唯一性、已知/未知 code、大小写、建议、文档链接、并发和纯离线读取。 |
