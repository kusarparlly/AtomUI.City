# AtomUI.City.PluginSystem API Contracts

本文件是实现 public API 的行为合同。它不是源码目录索引；每个关键 API 必须说明用途、生命周期、失败行为、取消、并发和兼容性。

## Stability 基线

本模块当前全部 public 类型、成员、manifest/schema 和 plugin boundary contract 均为 `Preview`。`PublicAPI.Unshipped.txt` 只冻结待审签名，不表示 `Stable`；跨 Host/Plugin 的稳定承诺必须逐项完成版本和卸载兼容 review。

## API Family 合同

| API Family | 关键类型 | 职责 | 硬性行为 |
| --- | --- | --- | --- |
| Manifest | PluginManifest, PluginManifestReader, PluginManifestValidator | 读取和校验 plugin.json。 | 不执行插件代码；schema、version、mainAssembly、targetFramework 和 required field 稳定。 |
| Package | PluginPackageInstaller, PluginPackageLayoutValidator, PluginInstallationReader | 安装、布局校验和安装记录。 | staging 原子切换；非法路径拒绝；安装记录路径必须规范化。 |
| Runtime | PluginLoader, PluginRuntime, PluginRuntimeState, PluginLoadResult | 加载、启用、停用、卸载。 | 状态机显式；失败有 diagnostics 且 result state 为 Faulted。 |
| Dependencies | PluginDependencyAttribute, PluginDependencyValidator, PluginSemanticVersion | 依赖和版本校验。 | 缺失、循环、版本不满足和重复 plugin id 结构化返回；循环中的每个 plugin id 必须有诊断。 |
| Diagnostics | PluginDiagnosticIds, PluginDiagnostic | 插件错误码和诊断。 | 现有 AUCPLG0000-0023 保持稳定；All catalog 不可变；diagnostic code/message 非空。 |

## 关键方法合同

| Method | Purpose | Parameters | Return | Failure Behavior | Cancellation | Concurrency / Idempotency |
| --- | --- | --- | --- | --- | --- | --- |
| PluginManifestReader.Read | 读取 manifest。 | path 必须规范化且在包根下。 | 返回 manifest 或 diagnostics。 | 缺失、schema 不兼容、JSON 无效返回诊断。 | 可取消用于 IO。 | 无共享可变状态。 |
| PluginPackageInstaller.InstallAsync | 安装插件包。 | package path、install root、cancellation。 | 成功返回 PluginInstallation。 | 失败清理 staging。 | 必须观察取消并清理临时目录。 | 并发安装同一 plugin/version 必须串行或拒绝。 |
| PluginLoader.LoadAsync | 加载插件运行时。 | manifest、root path、owner。 | PluginLoadResult；成功时 State 为 Loaded 且 Runtime 非空，失败时 State 为 Faulted 且 Runtime 为空。 | 主程序集缺失、id mismatch、依赖失败进入 diagnostics。 | 可取消。 | 不得污染 Host root provider。 |
| PluginMsBuildContract.GetManifestOutputPath | 计算构建期 plugin.json 输出路径。 | intermediate output path。 | 规范化后的 manifest 输出路径。 | 空路径抛标准参数异常。 | MSBuild 进程级取消。 | 同一输入必须稳定返回同一路径。 |
| PluginRuntime.RegisterUnloadLease | 登记插件贡献或资源 lease。 | lease id、kind、revoke callback。 | PluginRuntimeLease。 | 卸载中、已卸载或 pending 时拒绝新增 lease。 | revoke callback 在卸载时接收取消。 | lease 按反向登记顺序撤销。 |
| PluginRuntime.UnloadAsync | 卸载插件运行时。 | cancellation。 | PluginUnloadResult。 | lease 撤销失败进入 UnloadPending 并返回 diagnostics。 | 取消时不提交 Unloaded。 | 重复卸载已 Unloaded 返回成功。 |
| PluginRuntime.DisableAsync | 停用插件。 | 只能 Enabled 状态。 | Disabled 或失败结果。 | 贡献撤销失败仍继续其他撤销。 | 取消后仍执行最小清理。 | 并发 Disable/Unload 串行化。 |

## Public 类型覆盖

| Type | 分类 | Review 规则 |
| --- | --- | --- |
| `ContributionManifestAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityAttribute` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyAttribute` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyValidator` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDiagnostic` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginValidationResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDiagnosticIds` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDiscoveryScanner` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDiscoveryResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginInstallation` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginInstallResult` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginInstallationReader` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginLoadResult` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginLoader` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifest` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginCapabilityDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginContributionDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginDependencyDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginModuleDescriptor` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifestBuilder` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifestReader` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginManifestValidator` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginMsBuildContract` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginPackageInstaller` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginPackageLayoutValidator` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginPackagePaths` | 支持类型 | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginRuntime` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginRuntimeLease` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginRuntimeLeaseState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginRuntimeState` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |
| `PluginUnloadResult` | 关键 contract | 新增、删除、重命名或默认行为变化必须更新本文档和 compatibility。 |

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
