# 发布流程规范

版本：v0.1
状态：正式初版
适用范围：发布流程、发布前验证、包发布、模板发布和发布记录

## 1. 目标

发布流程必须保证文档、实现、测试、包和模板一致。没有文档确认和测试门禁通过，不允许发布。

## 2. 发布前检查

发布前必须完成：

- 文档链接检查。
- 设计文档和实现一致性检查。
- 公共 API review。
- full build。
- full test。
- package validation。
- template smoke test。
- plugin lifecycle smoke test。
- platform integration test。
- analyzer/generator tests。
- license 检查。
- 隔离本地 NuGet consumer：候选包必须从本次生成的本地源恢复，禁止通过 `ProjectReference` 或全局缓存中的同版本 City 包通过测试。
- 对声明 Windows 正式支持的 Presentation 运行真实 desktop、隔离 NuGet consumer 和批准性能 baseline 比较。
- 对声明 Windows NativeAOT 支持的运行时模块执行持久 runtime matrix：`net8.0/win-x64` 与 `net10.0/win-x64` strict build、真实 NativeAOT publish、原生进程运行和结构化业务断言。

Windows 综合运行时矩阵的固定入口为 `engineering/check-native-aot-runtime-matrix.ps1`。不同宿主边界必须拆分为可独立定位的阻断门禁：综合 Core/runtime matrix、Data 托管本地服务 + 原生协议客户端、Presentation Windows GUI。三类门禁都执行 `net8.0/win-x64` 与 `net10.0/win-x64` strict build、publish、原生进程和结构化断言；任一缺失均不得宣称完整 NativeAOT 通过。

## 3. 发布流程

```text
Confirm docs
-> review public API
-> run full verification
-> pack packages
-> validate package layout
-> restore/build/run isolated local package consumer
-> generate release notes
-> tag release
-> publish packages
-> publish templates/tool
-> archive diagnostics
```

## 4. 暂停条件

出现以下情况必须暂停发布：

- 文档和实现不一致。
- 公共 API 未记录。
- 公共 API review 未通过。
- 单元测试缺失。
- 集成测试失败。
- AOT/trimming 诊断未处理。
- NativeAOT runtime matrix 任一支持 TFM/RID 未发布、原生进程未运行或关键业务断言失败。
- package layout 不符合规范。
- 隔离 consumer 未从当前本地候选源恢复全部 City 包，或仍包含 `ProjectReference`。
- 插件安装/卸载 smoke test 失败。
- License 元数据不正确。

## 5. Release notes

Release notes 至少包含：

- 版本号。
- 新增功能。
- 破坏性变更。
- 修复。
- 已知限制。
- 迁移说明。
- 插件 API 兼容性说明。

## 6. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| full build | Build | solution build 成功。 |
| full test | Test | 全部测试通过。 |
| package validation | Pack test | 包布局和元数据有效。 |
| isolated local package consumer | Package/Integration | `net8.0` 与 `net10.0` 编译成功，`net10.0` 运行通过；全部 City 依赖来自本次本地源且无 `ProjectReference`。 |
| public API review | Contract test | public API、diagnostic、schema、generated output、CLI envelope 和 template variable 均有文档合同。 |
| template smoke | Template test | 生成项目可 build/test。 |
| plugin smoke | Integration | 插件安装、启用、停用、卸载链路可跑。 |
| release notes | Docs | 版本变更可追踪。 |
| Presentation Windows RC | Platform/Package/Benchmark | Headless 与 desktop 正常退出，包消费者成功，时间回退不超过 15%，分配回退不超过 10%。 |
| NativeAOT runtime matrix | AOT/Integration/Product | 支持的运行时模块关键公开路径在 `net8.0/win-x64` 与 `net10.0/win-x64` 中均完成 strict build、零未处理 IL 诊断、真实 publish 和原生运行；结果不得外推到未实测 OS/RID。 |

Presentation 1.0 的 Windows 模块发布入口为 `engineering/check-presentation-release.ps1`。共享 Windows CI 使用 benchmark `Verify` 模式；正式候选必须在批准开发机使用默认 `Compare` 模式，且不得自动重写 baseline。
