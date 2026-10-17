# 0012 统一有效取消令牌合同

状态：Accepted

日期：2026-10-17

## 背景

City 同时存在调用方取消、生命周期 Scope 取消、应用关闭和超时等取消来源。旧 API 在不同模块采用了不同形状：有的把 token 放在 context，有的同时暴露 context token 与方法参数，有的仅让方法参数取消等待而不取消已准入的操作。开发者必须记忆每个入口的特殊规则，遗漏任一来源仍可编译，并可能造成后台工作越过页面、路由或应用生命周期继续运行。

## 决策

- City 不定义自有 token 类型，统一使用 `System.Threading.CancellationToken`。
- 每个异步操作在框架边界合成一个 **Effective Cancellation Token**。它至少包含调用方 token，并按操作所有权链接 Scope、应用关闭和框架超时 token。
- 开发者回调只接收一个显式的 Effective Token，并把它继续传给下游异步 API；开发者不负责手工链接框架 token。
- context 只承载 operation id、correlation id、descriptor、metadata 等描述信息，不再重复承载常规操作 token。
- 面向调用方的异步 API 使用末尾参数 `CancellationToken cancellationToken = default`；框架调用的异步回调使用末尾、非可选的 `CancellationToken cancellationToken`。
- 若 token 只取消调用方等待而不取消底层操作，参数必须命名为 `waitCancellationToken`，文档必须明确底层操作继续运行。普通 `cancellationToken` 不允许具有 wait-only 语义。
- 已准入且拥有资源的事务收到取消后，必须先完成回滚、释放或所有权移交，再向调用方报告取消。
- 生命周期 Scope 继续公开自己的 cancellation token，供框架组合和高级适配器使用；它不是业务回调需要额外检查的第二个 token。

## 影响

- Core Lifecycle middleware、MVVM activation、EventBus handler、Data handler 和 Presentation outlet transaction 采用同一 token 形状。
- 旧 context token 属性和双 token callback 属于 preview 阶段的 breaking change，不保留长期兼容入口。
- diagnostics 继续通过 operation id、correlation id 和阶段信息定位取消来源；token 本身不承担追踪数据。

## 后续约束

- 新 public async API 必须通过 API review 检查参数位置、命名和实际取消语义。
- 链接 CTS 的创建方负责释放；不得把框架内部 `CancellationTokenSource` 暴露给业务代码。
- 必须测试预取消、运行中调用方取消、Scope 取消、应用关闭、超时、并发取消和取消后的回滚完成性。
- 模块文档、模板、fixture 和 public API baseline 必须与本 ADR 同步。
