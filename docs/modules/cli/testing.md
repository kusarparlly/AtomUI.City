# AtomUI.City.Cli Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 命令入口固定为 `atomui city`。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 非交互和 CI 模式不得等待输入。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| JSON envelope 不能混入普通日志。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| exit code、diagnostic code、artifact schema 必须稳定。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-CLI-001 | Unit | CliCommandArchitectureTests | 断言入口名、未知命令、缺参、exit code、usage 输出和 JSON 模式隔离。 | 未知命令、缺参、未知 option、value option 缺值返回 ArgumentError，不执行 handler。 | Completed |
| AUC-CLI-002 | Unit | CliNewAppTests | 断言生成项目、冲突、非法名称、dry-run、JSON artifacts 和取消。 | 缺少 app name、非法 app name、保留 root namespace、AOT/dynamic plugin 冲突、目标文件冲突和取消返回稳定诊断，且不得写入或覆盖文件。 | Completed |
| AUC-CLI-003 | Unit | CliBuildAndTestCommandTests | 断言成功、失败、非零 exit code、取消、CI 模式和输出截断。 | dotnet 非零退出返回原始 exit code；取消返回 `AUCCLI0202`；工作目录不存在返回 `AUCCLI0203` 且不启动进程。 | Completed |
| AUC-CLI-004 | Unit | CliInspectDoctorPluginTests | 断言合法插件、manifest 缺失、版本非法、layout 错误和 JSON diagnostics。 | 缺 path 返回 `AUCCLI0302`；manifest 缺失、版本非法和 layout 错误输出 PluginSystem diagnostics 且不加载插件 assembly。 | Completed |
| AUC-CLI-005 | Unit | CliCommandArchitectureTests | 断言 schema、纯 JSON、artifact 列表、suggested commands、retryable 语义。 | JSON envelope 缺少 AI 字段、普通日志混入 JSON、artifact 未提升、retryable 语义错误必须失败。 | Completed |
| AUC-CLI-006 | Unit | CliCommandArchitectureTests | 断言 CI、non-interactive、stdin unavailable、需要确认时失败。 | `--non-interactive`、`--ci` 或 stdin unavailable 下需要确认且缺少 `--yes` 返回 `AUCCLI0401`，不能等待 stdin。 | Completed |
| AUC-CLI-007 | Unit/TemplateSmoke/Build | CliGenerationCommandTests, GenerationTemplateRendererTests | 五类真实生成、dry-run、project 推导、artifacts、产物 build/test。 | 缺参、plugin/未知 kind、多项目歧义、变量非法、冲突、取消、IO 失败和回滚。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
