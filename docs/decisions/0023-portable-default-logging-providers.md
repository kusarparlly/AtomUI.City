# 0023 Portable Default Logging Providers

状态：Accepted
日期：2026-10-23

## 背景

`Host.CreateApplicationBuilder` 在 Windows 默认注册 `EventLogLoggerProvider`。Core 当前只添加拒绝全部
EventLog 输出的 filter；Provider、实现程序集及传递依赖仍在应用闭包中。普通 `net8.0/win-x64`
NativeAOT 消费者因此出现缺少 `System.Threading.AccessControl` 的 ILC `will always throw`，而仓库 fixture
通过额外包引用掩盖了真实消费者边界。

EventLog 是 Windows 专属输出目标，不是每个桌面或 CLI 应用的默认需求。禁用输出不等于移除依赖。

## 决策

1. Core 使用 `Host.CreateEmptyApplicationBuilder` 避免默认 EventLog 调用进入可达闭包，再显式恢复标准可移植配置源以及 Console、Debug、EventSource 三个 provider。仅在普通 builder 创建后调用 `ClearProviders` 不足以满足 NativeAOT 合同。
2. Core 默认不注册、不引用、不过滤 `EventLogLoggerProvider`。
3. 需要 Windows EventLog 的应用通过 `services.AddLogging(logging => logging.AddEventLog())` 显式启用，并自行声明和验证相应平台依赖。
4. Core 的 `ILogger<T>`、logging configuration、应用自定义 provider 和 `IHostDiagnostics` 合同保持不变。
5. 所有 Core 相关 NativeAOT fixture 删除仅用于补齐隐藏闭包的 `System.Threading.AccessControl` 引用。

## 影响

- Windows、Linux 和 macOS 的 City 默认日志 provider 集合一致。
- 普通消费者不再为未使用的 EventLog 承担 Windows 专属闭包、体积和 NativeAOT 风险。
- 显式启用 EventLog 的应用拥有平台、包依赖、权限和发布验证责任。

## 后续约束

- Core 测试必须断言默认 provider 集合不含 EventLog，并在 Windows 验证显式 opt-in 仍可注册。
- `net8.0/win-x64` 和 `net10.0/win-x64` NativeAOT 门禁不得引用 `System.Threading.AccessControl` 来掩盖默认闭包问题。
- 后续新增默认 provider 必须先证明跨平台和 NativeAOT 边界，不得依赖 filter 代替移除。

## 实施状态

已完成。Core 已改用 empty Generic Host builder 并显式恢复可移植配置源和日志 provider；全部隐藏 AccessControl fixture 引用已删除。Core 单元测试和 Core MVP `net8.0/win-x64`、`net10.0/win-x64` strict NativeAOT publish/run 均通过，未再出现 EventLog `will always throw`。
