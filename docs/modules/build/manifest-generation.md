# AtomUI.City.Build Manifest Generation 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Manifest Generation` 相关实现决策，不重新定义模块边界。

## 设计决策

- schema version 必须显式记录并测试。
- required field 缺失必须产生诊断。
- reader 必须拒绝未知的破坏性版本。

## Public Contract

- 只允许通过 `AtomUI.City.Build` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-BUILD-001 | Output Layout | OutputLayoutTests |
| AUC-BUILD-002 | Package Metadata | PackageMetadataTests |
| AUC-BUILD-003 | Project Inventory | ProjectInventoryTests |
| AUC-BUILD-004 | Dependency Boundary | ProjectDependencyBoundaryTests |
| AUC-BUILD-005 | Source Generator Packaging | SourceGeneratorProjectStructureTests |
| AUC-BUILD-006 | Release Gates | PackagingReleaseGateTests; EngineeringGateTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时包不依赖 Build 生产程序集` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Build Manifest 生成设计

适用范围：模块、路由、权限、Presentation、Data、Localization、Plugin 和应用 manifest 的生成、校验和输出

### 1. 目标

Manifest 是 AtomUI.City 在构建期沉淀框架元数据的核心产物。Build 负责汇总 source generator、MSBuild items 和资源索引，生成稳定 manifest。

### 2. Manifest 类型

| Manifest | 文件 | 来源 |
|---|---|---|
| Module | `modules.json` | Modularity generator。 |
| Routing | `routes.json` | Routing generator。 |
| Permission | `permissions.json` | Security generator。 |
| Presentation | `presentation.json` | Presentation generator。 |
| Data | `data.json` | Data generator。 |
| Localization | `localization.json` | Localization generator。 |
| EventBus | `events.json` | EventBus generator。 |
| Plugin | `plugin.json` | MSBuild task 汇总。 |
| Contribution index | `plugin.manifest.json` | MSBuild task 汇总。 |
| Application | `application.manifest.json` | Build task 汇总。 |

### 3. 生成流程

```text
Source generators emit deterministic strongly typed C# catalogs
-> compiler includes catalogs in the target assembly
-> MSBuild task collects explicit package/application properties and items
-> Normalize paths and ids
-> Validate schema
-> Sort deterministic
-> Compute hashes
-> Write final manifests
-> Copy manifest snapshots to output/artifacts/manifests
```

### 4. 中间产物

package/application 中间 manifest 位于：

```text
obj/AtomUI.City/manifests/
```

Module、DI、Event、Route 等 Generator Catalog 不另外写入 JSON；其真实来源是目标程序集中的 generated catalog。最终 package/application 快照进入：

```text
output/artifacts/manifests/
```

规则：

- 中间 manifest 服务 Build task。
- 最终 manifest 服务打包、诊断、测试和 CLI。
- 插件包内 manifest 必须来自最终校验后的产物。

### 5. 稳定性规则

Manifest 必须 deterministic：

- 字段顺序稳定。
- 数组顺序稳定。
- 路径分隔符稳定。
- 不包含绝对临时路径。
- 不包含构建时间戳作为核心 hash 输入。
- hash 计算规则稳定。

### 6. 校验规则

必须校验：

- schema version。
- required fields。
- duplicate ids。
- route conflict。
- permission conflict。
- View/ViewModel mapping conflict。
- Data client duplicate。
- localization culture format。
- plugin capability 超出声明。
- contribution manifest 缺失。

校验失败时构建失败，除非对应规则明确允许 warning。

### 7. AOT 关系

Manifest 生成必须支持 AOT-first：

- 不通过运行时反射扫描生成 manifest。
- 不执行用户代码。
- 动态发现必须 opt-in，并产生 AOT 诊断。
- Native AOT 发布使用静态 plugin/application manifest。

### 8. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| manifest 收集 | Build test | 多 generator 产物汇总。 |
| schema 校验 | Unit/Build | 缺少必填字段失败。 |
| deterministic order | Unit | 输入顺序变化输出稳定。 |
| hash | Unit | 内容变化 hash 变化。 |
| plugin manifest | Build | `plugin.json` 和 contribution index 正确生成。 |
| output copy | Build | 快照进入 `output/artifacts/manifests`。 |
