# AtomUI.City.Cli Generation 合同

> Status: Completed (`AUC-CLI-007`). 1.0 范围为 module、page、test、config 和 localization；plugin generation 延期到 PluginSystem 后续迭代。

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Generation` 相关实现决策，不重新定义模块边界。

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

## CLI 生成命令设计

适用范围：module、page、test、config、localization 的生成命令和模板调用

### 1. 目标

生成命令调用 Templates，不在 CLI 内硬编码模板结构。

命令：

```bash
atomui city generate module Sales
atomui city generate page Sales/List --route /sales
atomui city generate test Sales/List
atomui city generate config Sales
atomui city generate localization Sales
```

### 2. 通用流程

```text
Parse command
-> Inspect workspace
-> Resolve target project
-> Validate template variables
-> Build generation plan
-> Apply template
-> Update test matrix
-> Emit diagnostics
```

### 3. Module

`generate module` 生成：

- Module 类。
- Options，如果请求。
- Contribution 入口，如果请求。
- 模块测试。
- 测试矩阵条目。

### 4. Page

`generate page` 生成：

- Route。
- ViewModel。
- View。
- route test。
- activation test。
- View mapping 生成输入。

规则：

- 必须明确 RoutePath。
- Routing 只生成 Route -> ViewModel Target。
- Presentation 负责 ViewModel -> View。

### 5. Test

`generate test` 生成：

- 功能点测试文件。
- FeatureTestMatrix 条目。
- TestHost 使用入口。

### 6. Config 和 Localization

`generate config` 生成 Options、validator 和测试。

`generate localization` 生成 culture 目录、resource key 和本地化测试入口。

### 7. 事务、项目解析和诊断

- CLI 通过 `--project` 和 `--namespace` 接收显式目标；未提供时只允许在 `src/` 下恰好存在一个项目时推导。
- 增量输出固定采用 `src/<Project>/<Project>.csproj` 工作区布局；非标准嵌套工程返回 `AUCCLI0503`，不得静默写入另一个目录。
- `--dry-run` 只返回 `TemplatePlan` 和 artifacts，不读取或写入目标文件。
- apply 前统一预检全部目标；任一冲突都不得写入文件。
- 写入中途取消或 IO 失败必须逆序删除本次创建的文件和空目录。
- 同一规范化 output root 的进程内生成事务串行，不同 root 可并发。
- `generate plugin` 返回 `AUCCLI0502`，不得调用现有插件模板或返回成功。
- 缺少 kind/name 返回 `AUCCLI0501`；项目无法唯一解析返回 `AUCCLI0503`；取消返回 `AUCCLI0504`。
- Templates 的 `AUCTPL...` 诊断原样进入 CLI envelope。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| module generation | CLI/Template | 文件、测试、矩阵。 |
| page generation | CLI/Template | route、VM、View、测试。 |
| test generation | CLI/Template | 测试文件和矩阵条目。 |
| dry-run | Unit/CLI | 不写文件。 |
| plan/apply | CLI | plan 可执行。 |
| cancellation/rollback | CLI/Template | 无半成品文件和目录。 |
| generated workspace | Build | module、page、test、config、localization 产物可编译，测试可执行。 |
