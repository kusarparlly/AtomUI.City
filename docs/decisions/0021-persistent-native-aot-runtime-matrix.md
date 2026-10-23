# 0021 Persistent NativeAOT Runtime Matrix

状态：Accepted
日期：2026-10-23

## 背景

Core、EventBus 和 Data 已有持久的 NativeAOT 验证载体；Presentation 有可发布的 headless fixture。State、Routing、Security、Mvvm 和 Localization 的部分关键路径只在专项审计创建的临时组合应用中完成过双 TFM 原生运行。临时成功能够说明当前快照，但不能在后续修改中自动发现回归，也不能作为发布期可重复证据。

普通 unit/integration test 运行在 CoreCLR 上，无法替代 trimming、NativeAOT 编译和原生进程执行。严格 analyzer build 同样只是必要条件，不能证明裁剪后的最终应用能够启动并完成真实业务路径。

## 决策

1. City 为正式支持的运行时模块建立持久、提交到仓库的 NativeAOT runtime matrix；它是模拟真实开发者应用的实战验收，不是源码字符串检查或只引用程序集的 smoke test。
2. 不为每个类型创建独立应用。使用最小综合 runtime fixture 覆盖 State、Routing、Security、Localization、Mvvm 等模块的关键公开路径；具有不同宿主边界的 Presentation 保留独立 headless fixture；Core、EventBus 和 Data 已有的专项重型 fixture 继续保留。
3. 每个被声明为 NativeAOT 已验证的模块必须把至少一条关键公开业务路径映射到可观察断言。仅成功编译、成功启动或加载程序集不构成模块验收。
4. Windows 当前支持矩阵至少执行 `net8.0/win-x64` 与 `net10.0/win-x64`：先执行 strict AOT/trim build，再执行真实 NativeAOT publish，检查无未处理 IL2xxx/IL3xxx 或 `will always throw`，最后启动生成的原生 EXE 并核验结构化业务结果和退出码。
5. runtime matrix 必须使用源代码生成、DI、模块生命周期和真实包运行时闭包。能够合理隔离时，发布候选阶段还必须使用本轮本地候选 NuGet 包，避免 `ProjectReference` 或旧缓存包伪造通过。
6. PluginSystem 在其正式建设和动态/静态插件 AOT 边界完成前不进入“全运行时模块通过”的矩阵结论；tooling-only 的 Build.Tasks、Generators、Templates 和 Testing 不作为运行时 NativeAOT 应用模块验收。
7. Mvvm、Localization 等已经发现明确 AOT 缺陷的模块，先完成对应设计修复，再把正向路径加入永久矩阵；门禁不得通过 suppression 掩盖未解决诊断。
8. NativeAOT 结果按 OS/RID 独立声明。Windows 通过不能推导 Linux 或 macOS 通过；后两者必须在对应操作系统的本机工具链上 publish/run 后才能标记 Verified。

## 影响

- `fixtures/` 将包含一个长期维护的综合 NativeAOT 实战应用，`engineering/` 提供确定性双 TFM 发布、运行和结果校验脚本。
- CI 和发布流程能够发现普通测试无法捕获的裁剪、反射、动态代码、DI 构造器可达性和 source-generated catalog 回归。
- 矩阵关注关键路径而不是穷举所有类型，控制执行时间；模块新增动态能力或新的公开激活路径时必须扩展场景。
- “Analyzer clean”“NativeAOT publish 成功”“原生业务场景运行成功”作为三层独立证据报告，不互相替代。

## 后续约束

- fixture 的每个场景必须列明覆盖模块、公开入口、预期输出和失败判定，不允许只有总退出码断言。
- 双 TFM 必须运行同一业务场景和同一结构化结果校验，防止只在新 TFM 覆盖完整能力。
- 工程脚本必须在任何 restore、publish、IL 诊断、进程退出或业务断言失败时返回非零。
- 临时审计程序不能作为长期通过证据；通过结论必须能够由仓库中的固定命令重新生成。
- 发布报告必须记录 SDK、TFM、RID、OS、配置、包来源以及实际执行的场景集合。

## 实施状态

已于 2026-10-23 完成。`fixtures/AtomUI.City.NativeAot.RuntimeMatrix`、场景设计文档和 `engineering/check-native-aot-runtime-matrix.ps1` 已提交；Windows `net8.0/win-x64` 与 `net10.0/win-x64` 均完成 clean NativeAOT publish 和原生业务断言。当前执行机器使用 SDK 10.0.113；仓库正式门禁仍以 `global.json` 钉住的 SDK 为准。
