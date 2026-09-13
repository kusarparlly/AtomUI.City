# AtomUI.City.Cli Plugin Commands 合同

## 适用范围

本专题属于 `AtomUI.City.Cli` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Plugin Commands` 相关实现决策，不重新定义模块边界。

## 设计决策

- 插件来源对象必须绑定 plugin owner。
- 卸载必须撤销 contribution、subscription、view lease、state 和 connection。
- 跨插件 contract 必须位于 Host 共享程序集。

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

## CLI 插件命令设计

适用范围：插件 list、inspect、install、update、remove、enable、disable、doctor 命令和 PluginSystem metadata 集成

### 1. 目标

插件命令用于管理本地插件包和插件状态，但不能绕过 PluginSystem 生命周期和安装规则。

CLI 不直接加载插件业务代码。

### 2. 命令

```bash
atomui city plugin list
atomui city plugin inspect <PluginId>
atomui city plugin install <PackagePathOrSource>
atomui city plugin update <PluginId>
atomui city plugin remove <PluginId>
atomui city plugin enable <PluginId>
atomui city plugin disable <PluginId>
atomui city plugin doctor <PluginId>
```

### 3. List 和 Inspect

只读命令：

- 读取插件目录。
- 读取 lock file。
- 读取 install record。
- 读取 manifest。
- 输出 PluginId、version、state、source、capabilities、diagnostics。
- `plugin inspect <Path>` 接受 package root 或 `atomui-city/plugin.json` path，只校验 manifest metadata。
- `plugin doctor <Path>` 接受 package root，并执行 package layout validation。
- manifest 缺失、版本非法、main assembly 缺失和 required contribution 缺失必须原样输出 PluginSystem `AUCPLG...` diagnostics。
- inspect/doctor 不加载插件 assembly。

### 4. Install 和 Update

写操作必须支持：

- `--dry-run`。
- plan/apply。
- `--json`。
- `--yes`。

流程：

```text
Resolve package
-> Verify package metadata
-> Build install/update plan
-> Check running plugin state
-> Execute PluginSystem installation operation
-> Emit diagnostics
```

规则：

- 不覆盖运行中插件目录。
- `UnloadPending` 时进入 pending 操作。
- hash、签名、来源、capability 授权结果必须进入诊断。

### 5. Enable 和 Disable

规则：

- 修改插件启用状态必须通过 PluginSystem metadata/lock file 规则。
- 禁用不删除安装目录和用户配置。
- 启用前必须执行兼容性和能力检查。

### 6. Remove

规则：

- remove 默认卸载插件包，但保留用户配置和状态。
- 清理用户数据必须显式参数。
- 插件处于 `UnloadPending` 时不能删除文件。

### 7. Doctor

`plugin doctor` 输出：

- manifest 状态。
- lock file 状态。
- package hash。
- signature/trust。
- dependency。
- capability。
- unload pending reason。
- suggested actions。
- `data.pluginDiagnostics` 保留 PluginSystem diagnostics，CLI 顶层 diagnostics 使用同一 code。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| plugin list | Unit/CLI | 读取 lock file 和 installed 目录。 |
| plugin inspect | Unit/CLI | 输出 manifest、capabilities、state。 |
| install dry-run | CLI | 不写文件，输出 plan。 |
| update pending | Plugin integration | UnloadPending 进入 pending。 |
| disable | Unit/Plugin | 禁用不删除文件。 |
| remove | Plugin integration | active/unload pending 策略。 |
| doctor | Unit | 诊断字段完整。 |
