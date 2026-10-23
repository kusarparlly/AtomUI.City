# 0019 Strict AOT Is Fail-Closed

状态：Accepted
日期：2026-10-23

## 背景

`AtomUICityStrictAot=true` 当前会声明 `IsAotCompatible` 并启用 AOT、trimming 和 single-file analyzer，但分析器产生的 IL 诊断不一定阻断构建。项目是否失败取决于调用方是否额外启用 warning-as-error，并且不同目标框架和 SDK 的结果并不一致。

“Strict” 如果只负责显示 warning，会让开发者把退出码为零误认为 NativeAOT 兼容证明。另一方面，普通类库构建不能替代最终应用的真实 NativeAOT publish/run，两者必须分层。

## 决策

1. `AtomUICityStrictAot=true` 是 fail-closed 构建合同。它必须启用 AOT、trimming 和 single-file analyzer，并把这些分析器报告的未处理 IL 兼容性诊断升级为构建失败。
2. Strict AOT 不自动设置 `PublishAot=true`。类库阶段负责 analyzer-clean；最终应用发布门禁另外执行真实 NativeAOT publish 和原生进程运行。
3. Strict AOT 的阻断行为必须由 `AtomUI.City.Build` 自身提供，不依赖消费项目额外设置 `TreatWarningsAsErrors`。
4. 行为必须在 City 支持的全部目标框架中一致。相同风险不得在一个 TFM 返回 warning/exit 0、在另一个 TFM 返回 error。
5. Strict AOT 只作用于进入最终应用运行时闭包的项目，不得把运行时属性错误传递给 Roslyn Generator、MSBuild Task 等 tooling-only 项目。
6. Strict AOT 下不得用项目级 `NoWarn` 静默屏蔽 AOT/trimming/single-file IL 诊断。确属安全的例外必须使用可定位、带 justification 的源码级 suppression，并接受测试和审查。
7. Analyzer-clean 是必要条件而不是充分条件。发布批准仍要求支持 TFM/RID 的真实包消费、NativeAOT publish 和原生进程运行证据。

## 影响

- 启用 Strict AOT 的项目可能从“带 warning 构建成功”变为确定性失败，这是该 Preview 属性的预期语义修正。
- Build 必须增加正向和负向消费测试，不能只检查属性值或 analyzer 是否开启。
- NativeAOT 不再由一个布尔属性单独宣称完成；文档和 release gate 必须同时报告 analyzer 与 publish/run 两层证据。

## 后续约束

- 为反射式 JSON、未传播 DI 构造器可达性、`RequiresUnreferencedCode` 调用和动态代码分别提供负向构建样例。
- 对安全样例执行所有支持 TFM 的 strict build；对应用样例执行支持 RID 的 NativeAOT publish/run。
- 验证 Build targets 不向 tooling-only project 传播不适用的 `IsAotCompatible`/AOT 属性。
- `ValidateAtomUICityAotCompatibility` 的退出码、诊断 ID 和失败上下文必须进入 Build 合同测试。

## 实施状态

已于 2026-10-23 完成。Build targets 对 runtime project 启用 fail-closed analyzer/linker/ILC 行为，`AUCBLD0302` 拒绝项目级 IL suppression/downgrade，tooling-only project 保持隔离；正向、风险调用、suppression 与 tooling 场景均进入可执行 Build 门禁。
