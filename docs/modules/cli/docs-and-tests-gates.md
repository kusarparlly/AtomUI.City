# AtomUI.City.Cli Docs And Tests Gates 合同

## 适用范围

本专题定义 `AUC-CLI-008 Repository Governance Gates`。第一版只检查 AtomUI.City 框架仓库及显式采用同一 framework profile 的源码工作区，不因为普通应用引用 City 包而自动启用。

本页必须与 [features.md](features.md)、[api-contracts.md](api-contracts.md)、[diagnostics.md](diagnostics.md)、[testing.md](testing.md) 和 [compatibility.md](compatibility.md) 保持一致。AtomUI.City framework profile 的规范来源是 [文档先行治理规范](../../engineering/documentation-governance.md)、[模块文档设计规范](../../engineering/module-documentation-standard.md)、[模块设计文档 Checklist](../../superpowers/checklists/module-design-docs-checklist.md) 和 [功能点测试门禁](../testing/feature-test-gate.md)。

## 当前状态

`AUC-CLI-008` 状态为 `Completed`。生产 evaluator 与 `CliGovernanceGateTests` 已落地；当前 framework workspace 的 `docs check` 与 `tests check` 均作为真实正向证据执行。

## 第一版目标

```bash
atomui city docs check
atomui city tests check
```

两条命令把现有 framework profile 中可以客观、确定验证的规则转为只读检查。机器只判断能够从仓库结构、Markdown 合同和测试源码身份中证明的事实；不能可靠证明的语义继续由 code review 和真实测试承担。

## 第一版共同边界

- 工作区从显式 working directory 开始识别；无法确认 framework profile 时拒绝执行，不猜测项目类型。
- 扫描只允许读取已识别工作区 root 内的文件，不跟随逃逸 root 的相对路径。
- 命令不创建、修改、移动或删除文件。
- `--json` 只输出 `CliEnvelope`，文本模式输出等价摘要。
- 没有 Error 时返回 `CliExitCodes.Success`；存在任一 Error 时返回 `CliExitCodes.Failure`；命令参数非法返回 `CliExitCodes.ArgumentError`。
- Warning 不改变成功 exit code；第一版规则默认均为 Error，未来引入 Warning 必须先更新本文和 compatibility。
- 每个诊断至少包含 code、message、path；适用时增加 module、featureId、testType 和 position。
- 相同工作区快照、相同 CLI 版本和相同参数必须产生相同规则结果。
- 文件枚举和读取前后观察 cancellation；取消后不得继续发布新诊断。

## 第一版机器解析合同

### 1. Rule-set identity

第一版规则集身份固定为：

```text
ruleSetId      = atomui-city-framework
ruleSetVersion = 1.0
```

- 同一规则版本必须保持 workspace discovery、module catalog、必要文件、解析语法、规则严重度和诊断语义不变。
- 只增加向后兼容的可选输出字段可以提升 minor version；让原本通过的相同输入变成失败、删除字段、改变解析语法或扩大强制扫描范围必须提升 major version。
- 第一版不从工作区读取可覆盖规则的配置文件；运行时 rule catalog 必须与本页 `1.0` 合同一致。
- JSON envelope 必须报告 rule-set identity，不能只报告 CLI package version。

### 2. Workspace root 识别

从 `CliExecutionEnvironment.WorkingDirectory` 开始逐级向父目录查找。一个目录只有同时包含以下实体，才是 framework profile root：

```text
AtomUICity.slnx
global.json
Directory.Build.props
src/
```

- 第一个满足全部 sentinel 的祖先目录是 root；不继续向更高层寻找第二个候选。
- 到达文件系统根仍未找到时输出 `AUCCLI0601`。
- sentinel 名称按 ordinal case-sensitive 比较，避免 Windows 成功、Linux 失败。
- 不跟随 directory symbolic link、junction 或 reparse point 寻找 root。
- 第一版没有 `--profile` 推断，也不把任意具有 `docs`/`tests` 目录的项目识别为 framework workspace。

### 3. Framework module catalog

第一版模块清单固定如下。新增、删除、重命名模块或改变 Level 时，必须先更新本表，再修改 evaluator。

| Module key | Feature prefix | Level | Documents directory | Required test project |
| --- | --- | ---: | --- | --- |
| build | BUILD | 3 | `docs/modules/build` | `tests/AtomUI.City.Build.Tests/AtomUI.City.Build.Tests.csproj`; `tests/AtomUI.City.Build.Tasks.Tests/AtomUI.City.Build.Tasks.Tests.csproj` |
| cli | CLI | 2 | `docs/modules/cli` | `tests/AtomUI.City.Cli.Tests/AtomUI.City.Cli.Tests.csproj` |
| core | CORE | 3 | `docs/modules/core` | `tests/AtomUI.City.Core.Tests/AtomUI.City.Core.Tests.csproj` |
| data | DATA | 3 | `docs/modules/data` | `tests/AtomUI.City.Data.Tests/AtomUI.City.Data.Tests.csproj` |
| eventbus | EVENTBUS | 3 | `docs/modules/eventbus` | `tests/AtomUI.City.EventBus.Tests/AtomUI.City.EventBus.Tests.csproj` |
| generators | GENERATORS | 3 | `docs/modules/generators` | `tests/AtomUI.City.Generators.Tests/AtomUI.City.Generators.Tests.csproj` |
| localization | LOCALIZATION | 3 | `docs/modules/localization` | `tests/AtomUI.City.Localization.Tests/AtomUI.City.Localization.Tests.csproj` |
| mvvm | MVVM | 2 | `docs/modules/mvvm` | `tests/AtomUI.City.Mvvm.Tests/AtomUI.City.Mvvm.Tests.csproj` |
| plugins | PLUGIN | 3 | `docs/modules/plugins` | `tests/AtomUI.City.PluginSystem.Tests/AtomUI.City.PluginSystem.Tests.csproj` |
| presentation | PRESENTATION | 3 | `docs/modules/presentation` | `tests/AtomUI.City.Presentation.Tests/AtomUI.City.Presentation.Tests.csproj` |
| routing | ROUTING | 3 | `docs/modules/routing` | `tests/AtomUI.City.Routing.Tests/AtomUI.City.Routing.Tests.csproj` |
| security | SECURITY | 2 | `docs/modules/security` | `tests/AtomUI.City.Security.Tests/AtomUI.City.Security.Tests.csproj` |
| state | STATE | 3 | `docs/modules/state` | `tests/AtomUI.City.State.Tests/AtomUI.City.State.Tests.csproj` |
| templates | TEMPLATES | 2 | `docs/modules/templates` | `tests/AtomUI.City.TemplateSmokeTests/AtomUI.City.TemplateSmokeTests.csproj` |
| testing | TESTING | 3 | `docs/modules/testing` | `tests/AtomUI.City.Testing.Tests/AtomUI.City.Testing.Tests.csproj` |

PluginSystem 在本表中只参与文档/测试身份检查；第一版仍不执行或判断插件运行时语义。

当前 framework profile 以 [模块设计文档 Checklist](../../superpowers/checklists/module-design-docs-checklist.md) 的强制完成标准为准，每个目录都必须包含以下十个文件；Level 在第一版中用于诊断上下文和未来演进，不减少该文件集合：

```text
overview.md
architecture.md
features.md
api-contracts.md
lifecycle.md
threading.md
diagnostics.md
testing.md
compatibility.md
integration.md
```

### 4. 扫描根、排除项和文本规则

- 文档输入是 root 下的 `docs/**/*.md`。
- 测试源码输入是 root 下的 `tests/**/*.cs` 和测试项目 `tests/**/*.csproj`。
- 任何层级的 `.git`、`.vs`、`bin`、`obj`、`artifacts`、`TestResults` 目录均排除。
- 不跟随 directory symbolic link、junction 或 reparse point；指向 root 外的 Markdown link 按路径逃逸处理。
- 输入必须能以 UTF-8（允许 BOM）读取；解码、权限或 IO 失败输出 `AUCCLI0615`，不能降级成“文件不存在”。
- 所有逻辑路径先转为 root-relative `/` 分隔形式，再按 ordinal 排序和比较；诊断输出禁止绝对路径。
- Markdown 按 GitHub Flavored Markdown pipe table 与 link destination 语法解析；表格单元格 trim 首尾空白，escaped pipe 不作为列分隔符，CRLF/LF 不影响结果。
- Markdown fenced code block 内的表格、链接、Feature ID 和禁用占位标记都不参与规则识别。
- 禁用占位标记集合与 `engineering/check-docs.sh` 的文档 token 门禁保持一致；改变集合必须先更新治理文档、脚本、本合同和测试。
- inline/reference link 和 image destination 都参与本地文件检查；`http`、`https`、`mailto` 和纯 fragment destination 第一版不访问网络，也不验证远端或 heading anchor；其他相对 destination 去除 query/fragment 并完成 URI decode 后必须解析到 root 内现有文件。
- 绝对文件路径、解析到 root 外的相对路径和大小写不匹配均输出 `AUCCLI0604`。

本节是 `AUC-CLI-008` 的机器解析权威合同。现有 engineering shell gate 在该 Feature 实现前继续服务 CI；如果脚本行为与本节不同，实施 `AUC-CLI-008` 时必须先对齐脚本或明确退役脚本，不能长期维护两套互相冲突的规则。

### 5. Feature 定义与引用

- Feature ID 必须符合 `^AUC-<FEATURE_PREFIX>-[0-9]{3}$`；`FEATURE_PREFIX` 来自 module catalog。
- 唯一权威定义是对应模块 `features.md` 中 `## Feature 索引` 下、第一列标题为 `Feature ID` 的 Markdown 表格行。
- 每个 canonical Feature ID 在全部模块中只能有一个权威定义；重复定义输出 `AUCCLI0606`。
- Feature 详细章节标题、`testing.md`、全局进度表、专题文档和普通文字中的同一 ID 都是引用，不参与重复定义判断。
- module catalog 已登记前缀的引用必须能解析到一个 canonical definition；无法解析的引用输出 `AUCCLI0605`。不属于 module catalog 的工程级编号由其自身治理入口负责，本门禁不把它误判为模块 Feature。
- canonical row 至少包含状态列和主测试/主要证据列。列名可以是 `状态`/`Status`，以及 `主测试`/`主测试族`/`主要证据`/`Primary evidence`；其他别名第一版不接受。
- `features.md` 缺少 canonical index、表头非法或 ID 前缀与 module catalog 不符，均输出 `AUCCLI0605`。

### 6. Testing matrix 语法

- 每个模块的 `testing.md` 必须包含 `## 测试矩阵` 标题及紧随其后的 canonical table。
- canonical table 表头固定为 `Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status`；第一版不接受合并 Feature、纯数字 Feature、`Test Family` 或自由文本矩阵作为机器证据。
- 每个 canonical Feature 必须且只能有一行同 ID 测试矩阵；缺失输出 `AUCCLI0607`，重复或引用未知 Feature 输出 `AUCCLI0611`。
- `Test Type` 使用 `/` 分隔多个 token，trim 后逐个精确匹配；空 token、重复 token、逗号分隔和未知大小写均输出 `AUCCLI0612`。
- 合法 matrix Test Type 固定为：`Unit`、`Contract`、`FrameworkIntegration`、`RuntimeLifecycle`、`PluginLifecycle`、`PlatformIntegration`、`Generator`、`Analyzer`、`Build`、`TemplateSmoke`、`Dogfood`。
- `Test File` 使用英文分号或中文分号分隔 evidence token；逗号不作为分隔符，避免自然语言名称产生歧义。
- 只有符合 C# identifier 且以 `Tests` 结尾的 token 才是测试类型身份；其他证据文字可以保留，但不能满足“测试类型存在”规则。
- 每行至少包含一个可解析的 `*Tests` 类型。该类型必须在排除目录之外的 `tests/**/*.cs` 中存在 top-level class/record/struct declaration；namespace、partial 和 generic arity 不影响简单类型名匹配，注释、字符串和嵌套类型不计。
- Feature index 的主测试/主要证据列中出现的每个 `*Tests` 类型，都必须出现在同 Feature 的 testing matrix；testing matrix 可以声明更多测试类型。

### 7. 专项证据与 TestLayer

- `Generator` 和 `Analyzer` evidence 必须解析到 `tests/AtomUI.City.Generators.Tests` 内的测试类型。
- `Build` evidence 必须解析到 `tests/AtomUI.City.Build.Tests` 或 `tests/AtomUI.City.Build.Tasks.Tests` 内的测试类型。
- `TemplateSmoke` evidence 必须解析到 `tests/AtomUI.City.TemplateSmokeTests` 内的测试类型。
- `Dogfood` 是 testing matrix 的合法 Test Type，但不是当前 `TestLayerAttribute` 的合法值。
- 源码显式使用 `TestLayerAttribute` 时，只接受当前 `TestLayerNames` 定义的 `Unit`、`Contract`、`FrameworkIntegration`、`RuntimeLifecycle`、`PluginLifecycle`、`PlatformIntegration`、`TemplateSmoke`、`Generator`、`Analyzer`、`Build`。
- 第一版不要求每个测试类型都使用 `TestLayerAttribute`；它只拒绝已经显式声明但值未知的 layer。

### 8. Rule 与诊断码映射

| Rule | Diagnostic |
| --- | --- |
| framework workspace 无法识别 | `AUCCLI0601` |
| documentation root 缺失 | `AUCCLI0602` |
| 必要模块文档缺失 | `AUCCLI0603` |
| Markdown link 断裂、非法或逃逸 root | `AUCCLI0604` |
| Feature 格式、前缀、表头或引用非法 | `AUCCLI0605` |
| canonical Feature 重复定义 | `AUCCLI0606` |
| canonical Feature 缺 testing matrix row | `AUCCLI0607` |
| 禁用占位标记 | `AUCCLI0608` |
| test root 或 required test project 缺失 | `AUCCLI0609` |
| `*Tests` 类型无法解析 | `AUCCLI0610` |
| Feature index 与 testing matrix 不一致 | `AUCCLI0611` |
| Test Type token 非法 | `AUCCLI0612` |
| Generator/Analyzer/Build/TemplateSmoke 专项证据缺失 | `AUCCLI0613` |
| 显式 TestLayer 非法 | `AUCCLI0614` |
| 文件读取、解码或权限失败 | `AUCCLI0615` |
| 检查被取消 | `AUCCLI0616` |

## `docs check` 第一版必须做

| Rule | 必须验证的事实 | 失败结果 |
| --- | --- | --- |
| Workspace | 当前 root 是可识别的 AtomUI.City framework profile 工作区。 | 无法识别时拒绝执行。 |
| Documentation root | `docs` 及 framework profile 要求的治理入口存在。 | 缺失时 Error。 |
| Required module documents | Checklist 中登记模块具备其 Level 要求的必要文档。 | 每个缺失文档独立 Error。 |
| Markdown links | 工作区内 Markdown 相对文件链接能解析到 root 内已存在目标。 | 断链或路径逃逸为 Error。 |
| Feature identity | Feature ID 格式、模块前缀、canonical definition 和引用关系合法；权威定义唯一，合法引用可以重复。 | 非法、未知引用或重复定义为 Error。 |
| Test matrix mapping | 每个已登记 Feature 在所属模块 `testing.md` 中存在测试矩阵行。 | 缺少映射为 Error。 |
| Forbidden placeholders | 文档不包含仓库文档门禁脚本与治理规范列出的禁用占位标记。 | 每个位置产生 Error。 |

## `tests check` 第一版必须做

| Rule | 必须验证的事实 | 失败结果 |
| --- | --- | --- |
| Test root | framework profile 要求的 `tests` 根和测试项目存在。 | 缺失时 Error。 |
| Declared test identity | Feature/testing matrix 引用的 `*Tests` 类型能在工作区测试源码中找到。 | 引用不存在为 Error。 |
| Feature/test consistency | Feature 表与 testing matrix 对同一 Feature 的主测试身份不矛盾。 | 不一致为 Error。 |
| Test Type vocabulary | 测试矩阵只使用 `Unit`、`Contract`、`FrameworkIntegration`、`RuntimeLifecycle`、`PluginLifecycle`、`PlatformIntegration`、`Generator`、`Analyzer`、`Build`、`TemplateSmoke`、`Dogfood`。 | 未知 Test Type 为 Error。 |
| Specialized evidence | Generator、Analyzer、Build 和 Template Feature 的测试矩阵声明相应专项 Test Type，并引用存在的测试类型。 | 缺失或类型不符为 Error。 |
| Test layer values | 源码显式使用 `TestLayerAttribute` 时，其值必须属于 `TestLayerNames` 已定义集合。 | 未知 layer 为 Error。 |

`tests check` 验证的是静态测试证据身份，不等价于测试已经执行或测试断言充分。真实运行结果由 `atomui city test` / `dotnet test` 负责。

## 第一版明确不做

- 不提供 `atomui city docs init` 或 `atomui city docs generate`；文档生成属于独立、会写入文件的后续 Feature。
- 不自动修复文档、测试矩阵或测试源码。
- 不执行 `dotnet test`，不加载测试程序集，不运行用户代码。
- 不自动判断架构设计是否合理、文字内容是否充分或 API 设计是否优良。
- 不通过搜索 `Dispose`、`CancellationToken`、`Assert` 等字符串声称已经证明释放、取消或异常断言。
- 不自动决定某个 Integration test 是否可以替代 Unit test。
- 不提供 application、library 或 minimal profile，也不把 framework profile 强制应用到普通 City 应用。
- 不验证 PluginSystem 的加载、停止、卸载、Lease 或 Operation 运行时语义；第一版只允许验证文档和测试身份是否存在。
- 不验证测试是否通过、覆盖率、性能、NativeAOT、平台行为或资源泄漏。
- 不把目录存在本身视为检查成功。

## 输出合同

JSON `data` 至少包含：

```json
{
  "gate": "docs",
  "ruleSetId": "atomui-city-framework",
  "ruleSetVersion": "1.0",
  "root": ".",
  "workingDirectory": "docs/modules/cli",
  "checkedFiles": 42,
  "errorCount": 1,
  "warningCount": 0
}
```

错误通过 envelope 的 `diagnostics` 返回。诊断顺序必须按 normalized path、position、code 稳定排序，避免文件枚举顺序导致 CI 输出漂移。

- `root` 对已识别 workspace 固定输出 `.`，禁止写入机器相关绝对路径。
- `workingDirectory` 是输入 working directory 相对于 root 的 normalized path；在 root 执行时也是 `.`。
- `checkedFiles` 是本 gate 纳入且成功读取至少一次的去重文件数；读取失败的文件不计入该值，但必须产生 `AUCCLI0615`。
- `errorCount` 与 `warningCount` 分别等于 envelope 中相应 severity 的 diagnostic 数量。

## 失败与取消

- 单个文件问题记录诊断后继续检查其他独立文件，以一次返回完整问题集合。
- 工作区无法识别、root 不可访问或规则目录无法初始化时立即失败。
- 无权限、读取失败或非法编码不能被当作“文件不存在”；必须输出读取失败诊断。
- 收到 cancellation 后停止调度新文件，结束已有安全读取，并返回取消诊断；不得把部分结果报告为成功。
- 检查器异常必须映射为稳定失败 envelope，不允许只在 stderr 输出异常后返回 0。

## 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
| --- | --- | --- |
| workspace discovery | Unit | 合法 framework root、子目录启动、无法识别、root 逃逸。 |
| docs check | Unit/Contract | 必要文档、断链、Feature ID、测试矩阵、占位符、稳定排序。 |
| tests check | Unit/Contract | 测试项目/类型、Feature 映射、Test Type、专项证据、显式 layer。 |
| JSON diagnostics | Contract | rule-set identity、workspace-relative path、code、module、featureId、position 和汇总计数。 |
| exit code | Unit | 成功为 0，规则错误为 1，参数错误为 2。 |
| cancellation | Unit | 停止新增扫描、返回取消、不得成功。 |
| read-only | Unit | 成功、失败和取消路径都不改变工作区文件。 |

## 后续扩展门禁

application/library/minimal profile、测试 assertion metadata、豁免文件、规则版本迁移以及文档生成能力都必须先分配独立 Feature 或扩展 Feature 合同。未经文档确认，不得直接把这些能力加入 `AUC-CLI-008`。
