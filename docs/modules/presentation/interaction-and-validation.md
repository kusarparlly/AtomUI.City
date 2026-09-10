# AtomUI.City.Presentation Interaction And Validation

## Interaction

Presentation 把 MVVM Interaction request 交给应用注册的 UI handler。它负责 handler 定位、UI dispatch、生命周期、取消和模态排队，不实现确认框、输入框、Toast 或业务文案。

解析顺序从近到远为 Activation/Route、Window、Presentation；同层最后一个仍有效的 registration 生效。插件 registration 必须携带 plugin/contribution owner 并可撤销。

同一 Window 的模态请求使用 1 in-flight + 默认 8 pending 的 FIFO 队列；不同 Window 可并行。满载拒绝最新请求并返回 Failed(`InteractionQueueFull`)，不静默丢弃、不自动重试。非模态请求不进入该队列。

`GetModalQueueSnapshot(windowId)` 返回当前活动 lane 的容量与计数。Activity/registration token 取消排队或正在显示的请求，结果为 Canceled；handler 异常为 Failed；无 handler 为 NotHandled。

## 关闭确认约束

Window close 不是普通 Interaction 队列中的多个弹窗编排。WindowSession 先运行全部 `ICanDeactivate`，再要求当前 Entry 中最多只有一个 `IConfirmDeactivate`。多个确认 owner 时直接拒绝关闭并记录 `AUCPRS043`，不调用任意 confirmation。

## Validation

Validation bridge 完全可选。`ValidationVisualStateBinding` 只把调用方显式提供的 `ValidationScope` snapshot 应用到 `IValidationVisualStateTarget`：

- 不自动发现字段，不自动启用。
- 不定义验证规则、成功/失败回调、错误文案或视觉样式。
- 应用可不注册、不解析、不调用该 binding，现有业务代码不受影响。
- target 更新在 UI dispatcher；取消原样传播，target 失败记录诊断并传播。

## Command

Avalonia 原生 command binding 是默认路径。`CommandBinding` 只用于自定义 `IUiCommandSource`、异步命令执行中状态和 ActivationScope 自动释放，不替代 Avalonia command system。

## 测试要求

测试必须覆盖作用域优先级、撤销、无 handler、handler 失败、取消、单 Window FIFO、跨 Window 并行、容量拒绝、Validation 可选性和 UI dispatcher。
