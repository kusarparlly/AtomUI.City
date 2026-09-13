# AtomUI.City.Cli Inspect Commands 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Inspect Commands` 相关实现决策，不重新定义模块边界。

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

## CLI Inspect 命令设计

适用范围：workspace、project、module、route、manifest 的结构化只读检查命令

### 1. 目标

Inspect 命令为开发者、CI 和 AI Agent 提供工作区事实。Inspect 命令只读，不修改文件。

### 2. 命令

```bash
atomui city inspect workspace
atomui city inspect project <ProjectName>
atomui city inspect module <ModuleName>
atomui city inspect route <RouteIdOrPath>
atomui city inspect manifest
```

### 3. Workspace

输出：

- solution。
- projects。
- package references。
- AtomUI.City package versions。
- modules。
- routes。
- plugins。
- docs status。
- test matrix status。
- build output status。

### 4. Project

输出：

- project path。
- target frameworks。
- package references。
- project references。
- module declarations。
- generated manifests。
- test project mapping。

### 5. Module

输出：

- ModuleId。
- module type。
- dependencies。
- contributions。
- options。
- tests。
- diagnostics。

### 6. Route

输出：

- RouteId。
- path。
- parameters。
- ViewModel target。
- guards。
- resolvers。
- outlet。
- plugin source，如果适用。

### 7. Manifest

输出：

- manifest files。
- schema version。
- hash。
- validation result。
- source project。
- generated time 作为非核心诊断信息。

### 8. JSON 输出

Inspect 命令必须支持 `--json`。

JSON 输出不能依赖人类终端格式解析。

### 9. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| workspace inspect | Unit/CLI | solution、projects、docs、tests。 |
| project inspect | Unit/CLI | references、TFM、manifest。 |
| module inspect | Unit/CLI | dependencies、contributions。 |
| route inspect | Unit/CLI | path、target、guards。 |
| manifest inspect | Unit/CLI | hash、schema、validation。 |
| read-only | Unit | inspect 不写文件。 |
