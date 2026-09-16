# AtomUI.City.Build Features

本文件是模块功能规格表。没有 Feature ID 的功能不能进入实现；实现完成必须同步更新测试矩阵。

## Feature 索引

| Feature ID | 名称 | 状态 | Public Contract | 主测试 |
| --- | --- | --- | --- | --- |
| AUC-BUILD-001 | Output Layout | 已实现并通过产品合同测试 | Directory.Build.props, output path convention | OutputLayoutTests |
| AUC-BUILD-002 | Package Metadata | 已实现并通过产品合同测试 | csproj package properties, pack target | PackageMetadataTests |
| AUC-BUILD-003 | Project Inventory | 已实现并通过产品合同测试 | solution/project inventory conventions | ProjectInventoryTests |
| AUC-BUILD-004 | Dependency Boundary | 已实现并通过产品合同测试 | project reference and package reference rules | ProjectDependencyBoundaryTests |
| AUC-BUILD-005 | Source Generator Packaging | 已实现并通过产品合同测试 | AtomUI.City.Generators package layout | SourceGeneratorProjectStructureTests |
| AUC-BUILD-006 | Release Gates | 已实现并通过产品合同测试 | engineering/check-docs.sh, pack/test gates | EngineeringGateTests; PackagingReleaseGateTests |
| AUC-BUILD-007 | Test Naming | 已实现并通过产品合同测试 | test project and test file naming convention | TestNamingConventionTests |
| AUC-BUILD-008 | MSBuild Transitive Assets | 已实现并通过产品合同测试 | buildTransitive props/targets, analyzer package assets, BuildMsBuildContract | BuildAssemblyTests; SourceGeneratorProjectStructureTests; ProjectInventoryTests |
| AUC-BUILD-009 | Build Task Execution | 已实现并通过产品消费测试 | Build properties/items, AtomUI.City.Build.Tasks, plugin/application manifests | ManifestTaskTests; IncrementalGeneratorInfrastructureTests; AtomUI.City.Build.PackagingSmoke |

## Feature 硬门禁

| 约束 | 验收要求 |
| --- | --- |
| 所有构建输出集中到 `output`。 | 必须有实现、测试或工程门禁证据。 |
| pack warning 必须失败。 | 必须有实现、测试或工程门禁证据。 |
| 运行时包不得依赖 Testing 或 Roslyn。 | 必须有实现、测试或工程门禁证据。 |
| generator 包输出到 `analyzers/dotnet/cs`。 | 必须有实现、测试或工程门禁证据。 |
| Build 包必须包含 buildTransitive 资产和 generator analyzer。 | 必须有实现、测试和 package validation 门禁证据。 |

## Feature 实现合同

- Feature 必须先定义 public contract 或 internal contract。
- Feature 必须定义非法输入、生命周期状态非法、取消、重复调用和释放后的行为。
- Feature 必须定义诊断码或明确说明由上层诊断承接。
- Feature 必须至少有 Unit 或 Contract 测试；涉及生命周期、插件、线程、UI、连接、build 或 generator 的功能必须增加专项测试。
- Feature 完成状态必须能从 [全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 追踪。

## AUC-BUILD-001 Output Layout

Feature ID: `AUC-BUILD-001`
Status: 已实现并通过产品合同测试
Goal: 定义 repo 统一输出目录。
Public Contract: Directory.Build.props, output path convention
Runtime / Build Behavior: build/test/pack/logs 都写到 output 子目录。
Failure Behavior: 路径为空、路径逃逸、散落 bin/obj 规则不一致失败。
Threading / Cancellation: MSBuild 进程处理取消；测试读取文件系统无副作用。
Diagnostics: 失败必须指出 project 和 escaped path。
Tests: `OutputLayoutTests`
Required Assertions: 断言 artifacts、packages、logs、test-results 都在 output 下。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-002 Package Metadata

Feature ID: `AUC-BUILD-002`
Status: 已实现并通过产品合同测试
Goal: 保证每个 NuGet 包 metadata 可发布。
Public Contract: csproj package properties, pack target
Runtime / Build Behavior: 检查 PackageId、Description、RepositoryUrl、license LGPL v3、symbols 和 dependency group。
Failure Behavior: metadata 缺失、license 错误、pack warning 返回失败。
Threading / Cancellation: pack 进程可被 CLI 取消。
Diagnostics: 失败必须指出 property name 和 project。
Tests: `PackageMetadataTests`
Required Assertions: 断言 LGPL v3、repository、symbol、package id 和 dependency group。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-003 Project Inventory

Feature ID: `AUC-BUILD-003`
Status: 已实现并通过产品合同测试
Goal: 跟踪 src/tests/docs 项目边界。
Public Contract: solution/project inventory conventions
Runtime / Build Behavior: 所有真实 src/tests 项目必须在 solution 和 inventory 规则内被识别；模板 payload 下的 `.csproj` 不计入仓库项目清单。
Failure Behavior: 新增项目未登记、测试项目缺失、孤儿项目失败；模板 payload 被误计入真实项目时失败。
Threading / Cancellation: 文件扫描只读。
Diagnostics: 失败必须指出 project path。
Tests: `ProjectInventoryTests`
Required Assertions: 断言 src/tests 项目被 inventory 覆盖。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-004 Dependency Boundary

Feature ID: `AUC-BUILD-004`
Status: 已实现并通过产品合同测试
Goal: 阻止 runtime 包带入测试或编译期依赖。
Public Contract: project reference and package reference rules
Runtime / Build Behavior: 扫描真实 source project references 和 package references，按 runtime/test/generator 分类校验；CLI 可引用 PluginSystem 读取插件 manifest 和 package layout contract。
Failure Behavior: runtime 引用 Testing、test packages、Roslyn analyzer internals 失败。
Threading / Cancellation: 文件扫描只读。
Diagnostics: 失败必须指出 source project 和 illegal dependency。
Tests: `ProjectDependencyBoundaryTests`
Required Assertions: 断言 runtime 不依赖 Testing/Roslyn/test packages。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-005 Source Generator Packaging

Feature ID: `AUC-BUILD-005`
Status: 已实现并通过产品合同测试
Goal: 约束 generator analyzer 分发。
Public Contract: AtomUI.City.Generators package layout
Runtime / Build Behavior: Generator target 为 netstandard2.0，nupkg 包含 analyzers/dotnet/cs，不进入 runtime lib。
Failure Behavior: target 错误、analyzer 路径缺失、runtime 依赖失败。
Threading / Cancellation: pack 进程可取消。
Diagnostics: 失败必须指出 nupkg entry。
Tests: `SourceGeneratorProjectStructureTests`
Required Assertions: 断言 generator target、analyzer layout、runtime 不引用 generator。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-006 Release Gates

Feature ID: `AUC-BUILD-006`
Status: 已实现并通过产品合同测试
Goal: 聚合发布前工程验证。
Public Contract: engineering/check-docs.sh, pack/test gates
Runtime / Build Behavior: 本地和 CI 都执行 docs、format、build、test、pack、package verification。
Failure Behavior: 任一 gate 失败阻止 release。
Threading / Cancellation: CI 取消停止后续 gate。
Diagnostics: 失败必须保留 gate name 和 command。
Tests: `EngineeringGateTests; PackagingReleaseGateTests`
Required Assertions: 断言 docs、format、pack、test gate 可本地执行。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-007 Test Naming

Feature ID: `AUC-BUILD-007`
Status: 已实现并通过产品合同测试
Goal: 保证测试文件和模块可追踪。
Public Contract: test project and test file naming convention
Runtime / Build Behavior: 测试项目按 AtomUI.City.<Module>.Tests 命名，测试文件对应 feature 或 contract。
Failure Behavior: 测试项目命名偏离、模块缺少测试项目失败。
Threading / Cancellation: 文件扫描只读。
Diagnostics: 失败必须指出 test file 和 expected module。
Tests: `TestNamingConventionTests`
Required Assertions: 断言测试命名和模块对应关系。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-008 MSBuild Transitive Assets

Feature ID: `AUC-BUILD-008`
Status: 已实现并通过产品合同测试
Goal: 保证 `AtomUI.City.Build` 不是空壳包，应用和插件引用后自动获得框架构建资产。
Public Contract: buildTransitive props/targets, analyzer package assets, BuildMsBuildContract
Runtime / Build Behavior: `AtomUI.City.Build` 包包含 `buildTransitive/AtomUI.City.Build.props`、`buildTransitive/AtomUI.City.Build.targets`、应用/插件/诊断 targets，并把 `AtomUI.City.Generators.dll` 分发到 `analyzers/dotnet/cs`。
Failure Behavior: 缺少 build asset、generator analyzer、package entry 或 source project 只有 `.csproj` 时，测试和工程门禁失败。
Threading / Cancellation: MSBuild 进程处理取消；inventory 和 package validation 只读扫描文件系统。
Diagnostics: 失败必须指出缺失 package entry、缺失 analyzer path 或空 source project path。
Tests: `BuildAssemblyTests; SourceGeneratorProjectStructureTests; ProjectInventoryTests`
Required Assertions: 断言 BuildMsBuildContract、buildTransitive 文件、generator analyzer package entry、package validation 和 project inventory 空项目门禁。
Acceptance Criteria: API 行为、失败路径、诊断上下文、释放或撤销、兼容性影响均可由测试证明。

## AUC-BUILD-009 Build Task Execution

Feature ID: `AUC-BUILD-009`
Status: 已实现并通过 Tasks、Generator 与 NuGet-only 产品消费测试
Goal: 让公开的 Build Property、Item 和 Target 具有真实、可验证的生产消费链，禁止静默空实现。
Public Contract: `AtomUICitySourceGenerationMode`、`AtomUICityStrictAot`、插件和应用打包 Property/Item、package/application manifest v1。
Runtime / Build Behavior: `AtomUI.City.Build` 负责编排；包内 `AtomUI.City.Build.Tasks` 负责确定性 JSON、路径校验、资产布局、Hash 和最终包验证；Generator 继续输出强类型 C# Catalog。
Failure Behavior: 非法值、缺少必需 metadata、路径逃逸、重复声明、无效 manifest 或包布局必须以稳定 `AUCBLD` 诊断阻止构建。
Threading / Cancellation: Task 不保存静态可变状态；MSBuild 取消终止当前构建；相同输入产生相同字节输出。
Diagnostics: `AUCBLD0001/0002`、`AUCBLD0101/0102`、`AUCBLD0201/0202`、`AUCBLD0301`、`AUCBLD0401`。
Tests: `ManifestTaskTests; IncrementalGeneratorInfrastructureTests; AtomUI.City.Build.PackagingSmoke`
Required Assertions: 真实 build/publish/pack、九类 Item 消费、Strict/Compatible/Off、AOT、模板缺包门禁、最终应用不携带 Build/Tasks/Generator。
Acceptance Criteria: 不存在只登记名称而无生产消费者的 Build Property、Item 或 Target。
