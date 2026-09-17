# AtomUI.City.Build API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块当前全部 MSBuild property/item/target、诊断码和 package layout contract 均为 `Preview`。Build 是纯构建资产包，不发布供应用引用的 CLR 程序集 API；`PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt` 因而只包含 nullable header。真实兼容性基线是随包分发的 `buildTransitive/AtomUI.City.Build.contract.json`。任何合同只有在本文件逐项改标 `Stable` 并通过发布 review 后才形成 1.x 稳定承诺。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Output Layout | Directory.Build.* conventions, output path contract | 约束构建输出位置。 | 所有产物必须落在 output 下。 |
| Package Contract | project metadata, pack target, nupkg layout | 约束 NuGet 内容和 metadata。 | pack warning 和 metadata 缺失失败。 |
| MSBuild Integration | buildTransitive props/targets、contract baseline、analyzer assets | 让应用和插件引用 Build 包后自动获得构建约定和 generator/analyzer，并通过机器可读 baseline 固定名称、默认值、可见性与包资产。 | Build 包出现 `lib/`、缺少 contract/buildTransitive/analyzer entry，或 baseline 与真实资产不一致时失败。 |
| Dependency Boundary | project reference rules | 阻止 runtime 依赖 testing/generator internals，并显式维护 CLI 到 PluginSystem 的允许依赖。 | 边界测试失败阻止发布。 |
| Release Gate | engineering scripts and tests | 聚合 format/docs/test/pack 验证，真实 src/tests 项目必须覆盖，模板 payload 项目不进入仓库项目清单。 | CI 和本地命令语义一致。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| Build target ResolveOutputPath | 计算输出目录。 | Configuration、TargetFramework、PackageId。 | normalized output path。 | 路径逃逸或为空失败。 | MSBuild cancellation 由进程处理。 | 不同 project 输出目录隔离。 |
| Pack target VerifyPackageMetadata | 校验 NuGet metadata。 | project properties。 | pack success/failure。 | license、repository、symbols、readme policy 不满足失败。 | MSBuild cancellation 由进程处理。 | 重复 pack 输出可覆盖同配置产物。 |
| GenerateAtomUICityManifests | 收集 City manifest 输出。 | MSBuild properties/items。 | `@(AtomUICityGeneratedManifest)`。 | 非法配置由前置验证 target 以稳定诊断阻止构建。 | MSBuild 进程取消。 | 相同输入产生相同 Item 集合。 |
| PublishAtomUICityApplication | 发布应用并生成 application manifest。 | application properties、static plugin 与 resource pack Items。 | 发布目录和版本化 manifest。 | 路径、重复 id 或资产错误以 `AUCBLD0401` 阻止发布。 | MSBuild 进程取消。 | 相同输入产生确定性 manifest。 |
| DependencyBoundaryTests | 校验项目引用。 | 真实 src/tests project graph，排除模板 payload 项目。 | test pass/fail。 | runtime 引用 Testing/Roslyn test 包失败，允许依赖表与真实 source project 不一致失败。 | 测试进程 token。 | 读取项目文件无副作用。 |
| EngineeringGateTests | 执行仓库规则检查。 | docs、format、scripts、package layout。 | test pass/fail。 | 任一规则失败阻止完成。 | 测试进程 token。 | 门禁结果确定性。 |

## 公开合同覆盖

| Contract | 分类 | Review 规则 |
| --- | --- | --- |
| `AtomUI.City.Build.contract.json` | Machine-readable Preview contract | Property、Item、Target、默认值、public/infrastructure 分类或 package asset 的增删改必须先更新设计文档，并由精确一致性测试审阅。 |
| `AtomUI.City.Build.props/targets` | Public MSBuild contract | 开发者可配置项和可显式调用 Target 属于兼容承诺；`infrastructure` 项只允许包内资产使用，但仍受 baseline 防漂移保护。 |
| NuGet package layout | Build-only package contract | 不得包含 `lib/`；必须包含 contract、五个 buildTransitive 资产、Generator analyzer 和 Build.Tasks tool。 |

Build 模块当前没有 public CLR 类型。未来如果 IDE、CLI 或第三方工具需要编程式读取 Build 元数据，必须建立独立 Tooling Feature；不得通过恢复一个与真实 `.props/.targets` 重复维护的字符串目录解决。

## Nullability 和参数规则

- 参数为 `null` 且合同不接受 `null` 时，抛出 `ArgumentNullException`。
- 字符串 id、path、key、route、permission、culture、package id 必须在边界校验空值、空白和非法字符。
- 文件路径必须规范化并限制在声明 root 下。
- 枚举未知值必须拒绝或映射为明确失败结果。

## Cancellation 合同

- 接收 `CancellationToken` 的 API 必须在 IO、子进程、网络、dispatcher work、插件代码、handler 调用前后观察取消。
- 取消后不得提交状态、缓存、事件、UI 或 manifest 输出。
- 取消结果必须稳定：返回 Cancelled Result 或抛 `OperationCanceledException`，不能混用成功结果。

## Dispose 后行为

- mutating API 在 Dispose 后必须失败。
- 查询 immutable descriptor、manifest、snapshot、result 的 API 可以继续读取。
- 重复 Dispose、Stop、Unload、Unsubscribe、Revoke 必须幂等。

## Public API Review 门禁

以下改动必须先更新文档并 review：新增 public 类型或成员；修改异常、Result status、诊断码、默认 options、manifest/schema、generated output、MSBuild property、CLI JSON envelope 或模板变量。
