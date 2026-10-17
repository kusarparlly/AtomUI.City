# 0001 context 与 CancellationToken 的双设计

- 日期：2026-10-14
- 复核日期：2026-10-17
- 范围：Core、MVVM、EventBus、Data、Presentation
- 状态：Accepted，由 [ADR 0012](../decisions/0012-effective-cancellation-token-contract.md) 裁决并进入施工。

## 1. 问题

旧 API 把取消暴露成多种不一致形状：token 可能位于 context、独立参数、Scope，或只取消调用方等待。开发者需要记忆模块特例；漏查某个取消来源仍能编译，直到生命周期竞争时才表现为后台工作越界、资源延迟释放或路由事务继续提交。

原文关于“开发者应手工链接两个 token”的建议经代码复核后被否决。Command、Data 和 EventBus 已在部分内部路径执行链接，继续把链接责任交给业务代码会重复组合并扩大误用面。

## 2. 裁决

- 保留 .NET 标准 `CancellationToken`，不创建 City token 数据结构。
- City 在框架边界合成唯一 Effective Token。
- 开发者回调只接收、检查和向下传递这一个 token。
- context 回归描述信息，不再重复携带常规操作 token。
- 仅取消等待的参数必须明确命名为 `waitCancellationToken`。
- 已准入事务先回滚和释放，再向调用方报告取消。

完整合同见 [取消模型](../architecture/cancellation.md)。

## 3. 验收

- Core Lifecycle、MVVM activation、EventBus handler、Data pipeline 和 Presentation outlet 采用统一签名。
- 模板、fixture、public API baseline 与测试同步。
- 预取消、Scope 取消、超时、关闭和回滚路径均有确定性测试。
