# AtomUI.City.Build Source Generation 合同

## 适用范围

本专题属于 `AtomUI.City.Build` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Source Generation` 相关实现决策，不重新定义模块边界。

## 设计决策

- 优先 source generator，避免运行时反射扫描。
- generated output 必须稳定排序。
- diagnostics 必须可由 generator tests 断言。

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
| AUC-BUILD-008 | MSBuild Transitive Assets | BuildAssemblyTests; SourceGeneratorProjectStructureTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时包不依赖 Build 生产程序集` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## Build Source Generation 集成设计

适用范围：Source Generator 接入、生成产物收敛、增量生成、输出路径、AOT 约束和测试

### 1. 目标

Build 负责把 `AtomUI.City.Generators` 接入应用和插件项目，并把生成产物收敛为可诊断、可测试、可打包的文件。

Source Generator 的框架级原则见：[Source Generator 设计规范](../../architecture/source-generation.md)。

### 2. 接入方式

应用开发者引用：

```text
AtomUI.City.Build
```

Build 通过 analyzer asset 引入：

```text
AtomUI.City.Generators
```

规则：

- 应用不需要直接引用多个 generator 包。
- 运行时包不依赖 Roslyn。
- generator/analyzer 只在编译期运行。

### 3. Generator 分类

第一版接入：

- Modularity generator。
- Routing generator。
- Presentation generator。
- Security generator。
- EventBus generator。
- Localization generator。
- Data generator。
- Plugin static generator。
- Diagnostics analyzer。

### 4. 输出产物

C# registrar：

```text
obj/.../generated/
  AtomUI.City.Generated.Modularity.g.cs
  AtomUI.City.Generated.Routing.g.cs
  AtomUI.City.Generated.Presentation.g.cs
```

Manifest：

```text
obj/AtomUI.City/manifests/
  modules.json
  routes.json
  presentation.json
  permissions.json
  events.json
  localization.json
  data.json
  plugins.json
```

Build 将最终快照复制到：

```text
output/artifacts/generated/
output/artifacts/manifests/
```

### 5. 增量生成

要求：

- 只使用 incremental generator。
- 输入未变化时输出不变化。
- 输出 hint name 稳定。
- 不读取不稳定环境信息。
- 不执行用户代码。
- 不访问网络。

### 6. Strict 模式

`AtomUICitySourceGenerationMode`：

| 模式 | 行为 |
|---|---|
| `Strict` | AOT-first，禁止默认动态发现，问题尽量报错。 |
| `Compatible` | 为未来明确声明的兼容能力预留；当前不启用运行时模块扫描。 |
| `Off` | 关闭框架生成器，仅用于特殊调试。 |

`AtomUICitySourceGenerationMode` 必须通过 `CompilerVisibleProperty` 进入 Roslyn incremental pipeline；仅把值写入诊断文本不算实现。Generator 的稳定产物是编译进程序集的强类型 C# Catalog，不通过副作用写入任意 JSON 文件。`Build.Tasks` 只处理 package/application 级物理 manifest。

### 7. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| generator 接入 | Build test | 引用 Build 后 generator 自动运行。 |
| generated path | Build test | 产物进入 expected obj/output 路径。 |
| incremental | Generator test | 输入不变不重新生成。 |
| strict mode | Analyzer/Build | 仅接受静态可生成输入；运行时发现另立 Feature 后补充门禁。 |
| manifest output | Generator/Build | JSON manifest 生成稳定。 |
