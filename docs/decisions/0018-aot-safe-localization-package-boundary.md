# 0018 AOT-safe Localization Package Boundary

状态：Accepted
日期：2026-10-23

## 背景

Localization 同时支持 embedded locpack、文件 locpack 和运行时按路径加载语言包 DLL。当前 `AddLocalization()` 默认注册 `AssemblyLanguagePackageProvider`，而其可达加载路径调用 `AssemblyLoadContext.LoadFromAssemblyPath`。这使没有使用外部 DLL 语言包的 NativeAOT 应用也会受到 trimming/AOT 诊断污染。

语言内容本质上是数据。让默认 Localization 负责发现并动态装载新的托管程序集，会把插件加载、程序集隔离、卸载和 NativeAOT 不兼容性一并带入基础本地化能力，收益不足以覆盖边界成本。

## 决策

1. City 1.0 的默认 Localization 只接受以下三类来源：
   - 由编译期生成 descriptor 引用的、已经静态链接或已经由 owner 加载的 `SourceAssembly` 内嵌 locpack；
   - `FileLanguagePackageProvider` 加载的纯数据 `.locpack`；
   - `InMemoryLanguagePackageProvider` 提供的内存数据。
2. `AssemblyLanguagePackageProvider` 不得根据文件路径调用 `AssemblyLoadContext.LoadFromAssemblyPath`，也不得承担插件或任意外部 DLL 的加载职责。它只读取 descriptor 已持有的 `SourceAssembly`。
3. `AddLocalization()` 的默认 provider 组合必须通过严格 trimming 和 NativeAOT 发布；应用不需要通过“少注册一个默认 provider”规避 AOT 警告。
4. 可更新、可下载或独立部署的语言包统一使用纯数据 `.locpack`，不以托管 DLL 作为默认扩展格式。
5. Generator 生成的 descriptor 必须直接保存声明程序集的 `SourceAssembly`；严格 AOT 路径不运行程序集扫描。`Discover(Assembly)` 仅保留为显式 CoreCLR 兼容入口，不进入默认启动链路。
6. CoreCLR 动态插件可以把自己已经加载的程序集作为 `SourceAssembly` 贡献给 Localization；Localization 不替 PluginSystem 加载该程序集。NativeAOT 静态插件遵循相同 descriptor 合同。
7. 如果未来确有动态语言 DLL 的产品需求，必须以独立、显式 opt-in、标记为 CoreCLR-only 的扩展重新立项；它不得回到默认 Localization 包或 `AddLocalization()` 路径。

## 影响

- 外部语言更新不再需要加载代码，降低攻击面、ALC 泄漏和卸载复杂度。
- 普通 CoreCLR、trimming、single-file 与 NativeAOT 使用同一套默认 provider 语义，不再维护两套配置基线。
- 已有按 DLL 路径加载的 Preview 行为将被移除；项目尚未正式发布，因此不承诺该行为的兼容性。
- Assembly 语言资源能力仍然存在，但其含义收窄为“从已知 `SourceAssembly` 读取 embedded locpack”，而不是“Localization 动态加载 assembly”。

## 后续约束

- 删除 `AssemblyLanguagePackageProvider` 内的路径装载分支和对应测试。
- 对 `ProviderKind.Assembly` 且缺少 `SourceAssembly` 的 descriptor 返回稳定的 `InvalidDescriptor`，不得回退到 `Location` 动态加载。
- 增加默认 `AddLocalization()` 的双 TFM NativeAOT publish/run 门禁，并覆盖静态 embedded、file 和 in-memory 三种来源。
- `.locpack` 的 schema、大小、checksum、allowed-root 和重复属性校验继续由现有合同约束。

## 实施状态

已于 2026-10-23 完成。Assembly provider 的路径加载分支和 descriptor `LoadContext` 已删除；缺少 `SourceAssembly` 稳定返回 `InvalidDescriptor`；embedded、file、in-memory 三条路径已由 Localization 全量测试和 Windows 双 TFM NativeAOT Runtime Matrix 验证。
