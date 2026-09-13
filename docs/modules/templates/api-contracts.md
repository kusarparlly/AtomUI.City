# AtomUI.City.Templates API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Options | ApplicationTemplateOptions | 模板输入和命名规则。 | 非法名称不写文件。 |
| Generation Options | GenerationTemplateOptions, GenerationTemplateKind | module/page/test/config/localization 输入和类型。 | kind、名称、工程、namespace、route 和 culture 在 plan 前验证。 |
| Planning | ApplicationTemplateRenderer, TemplatePlan | 生成可 review 的文件变更计划。 | plan 先于写入；可校验重复路径和路径逃逸。 |
| Rendering | TemplateChange, TemplateRenderResult | 应用模板文件变更。 | 路径必须规范化为 `/` 分隔的可移植相对路径；冲突、路径逃逸、写入失败有稳定 result；成功结果的 AppliedPaths 与 plan 完全一致。 |
| Generated Application | `.slnx`, `Directory.Build.props`, `Directory.Packages.props`, docs entry, Avalonia desktop app/test projects | 生成可独立 restore/build/test/run 的 City 桌面应用工作区。 | 输出只包含相对路径，不写入机器绝对路径；`ManagePackageVersionsCentrally=false` 隔离父目录 CPM；Host 先启动、Presentation 再 attach、UI loop 退出后 Host 再停止并释放。 |
| Generated Test Project | `<AppName>.Tests.csproj`, `ApplicationSmokeTests.cs`, `FeatureTestMatrix.md` | 生成可独立 restore/build/test 的 xUnit 测试项目。 | 测试项目默认只引用被测项目和必要测试/平台包，不强制依赖 `AtomUI.City.Testing`；生产项目不得引用 Testing。 |
| Generated Plugin | plugin csproj, `atomui-city/plugin.json`, module, plugin tests | 生成单 assembly 插件包骨架。 | Plugin MSBuild 属性、NuGet metadata、manifest 和测试项目必须稳定。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| ApplicationTemplateRenderer.CreatePlan | 生成模板变更计划。 | options。 | TemplatePlan。 | null 抛 `ArgumentNullException`；options 非法抛 `ArgumentException`。不读取文件系统，因此不裁决目标冲突。 | 纯 CPU，无 token。 | 不写文件，可重复调用；files、build targets、test targets、docs、risk 和 rollback 均为只读快照。 |
| ApplicationTemplateRenderer.Render | 执行模板写入。 | options，或 options + CancellationToken。 | TemplateRenderResult。 | 变量校验失败返回 diagnostics 且不写文件；已有文件返回 `AUCTPL1004` 且不覆盖；IO 失败返回 `AUCTPL1005` 并回滚本次文件；回滚失败追加 `AUCTPL1006`。 | 每个文件写入前后观察 token；取消时回滚后抛 `OperationCanceledException`。 | 同一规范化目标目录在进程内串行；跨进程依靠 create-new 原子文件语义防覆盖。 |
| ApplicationTemplateOptions.Validate | 校验模板变量。 | AppName、RootNamespace、TargetFramework、OutputPath、UseAot、UseDynamicPlugins。 | `IReadOnlyList<TemplateDiagnostic>`。 | 非法变量返回 `AUCTPL0001`；RootNamespace 使用框架命名空间返回 `AUCTPL0002`；AOT/dynamic plugin 冲突返回 `AUCTPL0301`。 | 纯 CPU，无 token。 | 无副作用，可重复调用。 |
| ApplicationTemplateOptions.EffectiveRootNamespace | 计算实际 root namespace。 | RootNamespace、AppName。 | string。 | RootNamespace 为空白时派生自 AppName。 | 纯 CPU，无 token。 | 同一 options 结果稳定。 |
| TemplatePlan.Validate | 校验计划。 | plan entries。 | `IReadOnlyList<TemplateDiagnostic>`。 | 重复 normalized path 返回 `AUCTPL1002`；路径逃逸返回 `AUCTPL1001`；非法 change type 返回 `AUCTPL1003`。 | 纯 CPU，无 token。 | plan 不可变，返回 diagnostics 不可变。 |
| TemplateChange.Create | 创建文件变更。 | relative path。 | normalized create change。 | 空路径、绝对路径、路径逃逸、控制字符、Windows device name 和非可移植 segment 抛 `ArgumentException`。 | 纯 CPU，无 token。 | 同一路径变体归一化为同一 `Path`；plan 按忽略大小写检测跨平台冲突。 |
| TemplateRenderResult.Success | 创建成功结果。 | valid plan、可选 applied paths。 | TemplateRenderResult。 | plan 非法或 applied paths 与 plan 不完全一致时抛 `ArgumentException`。 | 无 token。 | immutable；Succeeded 为 true。 |
| TemplateRenderResult.Failed | 创建失败结果。 | 可选 plan、至少一个 diagnostic。 | TemplateRenderResult。 | 零 diagnostic 或 null diagnostic 抛 `ArgumentException`。 | 无 token。 | immutable；Succeeded 恒为 false，AppliedPaths 为空。 |
| Generated application solution | 表达应用和测试项目 build graph。 | AppName、IncludeTests。 | `<AppName>.slnx`。 | 缺少 app/test project 时 smoke test 失败。 | 随 Render 观察 token。 | path 固定为相对路径。 |
| Generated desktop bootstrap | 组合 City Host 与 Avalonia classic desktop lifetime。 | 生成的 `Program`、`App`、`DesktopBootstrap` 和 `MainWindow`。 | 进程退出码、已注册的 `WindowSession` 和完整 Host cleanup。 | 非 classic desktop lifetime、缺失 Host、重复 attach 或窗口注册失败时抛明确异常并由进程入口返回非零；不得继续显示半初始化窗口。 | Host start/stop 观察各自 token；Avalonia UI loop 为同步平台边界。 | 单进程只允许一个已 attach Host；主窗口显示前完成注册；UI loop 退出后从线程池等待 Host stop/dispose。 |
| Plugin template package | 表达插件模板包布局。 | `atomui-city-plugin` template package。 | solution、plugin project、manifest、module、test project。 | 缺少必需文件、metadata 或未替换 token 时 smoke test 失败。 | 文件读取测试无 token。 | `sourceName=TemplatePlugin` 只负责项目命名；`__PLUGIN_ID__` 只负责 PluginId，禁止改写 `AtomUICityPlugin*` MSBuild 属性。 |
| GenerationTemplateRenderer.CreatePlan | 生成非插件工作区增量计划。 | GenerationTemplateOptions。 | TemplatePlan。 | null 抛 `ArgumentNullException`；非法 options 抛 `ArgumentException`。 | 纯 CPU。 | 无 IO、可重复调用。 |
| GenerationTemplateRenderer.Render | 执行 create-only 增量事务。 | options、CancellationToken。 | TemplateRenderResult。 | 非法变量返回 `AUCTPL2001~2005`；冲突/IO/回滚复用 `AUCTPL1004~1006`。 | 写入前后观察；取消回滚后抛 `OperationCanceledException`。 | 同一 output root 串行；不同 root 可并发。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ApplicationTemplateOptions` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `ApplicationTemplateRenderer` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateChange` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplatePlan` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateRenderResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `TemplateDiagnostic` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `GenerationTemplateKind` | 关键 contract | 枚举值固定为 Module、Page、Test、Configuration、Localization。 |
| `GenerationTemplateOptions` | 支持类型 | 默认 culture、IncludeTests 和 reload policy 变化必须 review。 |
| `GenerationTemplateRenderer` | 支持类型 | plan、输出路径、失败和回滚语义变化必须 review。 |

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
