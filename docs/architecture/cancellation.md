# 取消模型

本规范定义 AtomUI.City 跨模块统一的取消合同。它受 [ADR 0012](../decisions/0012-effective-cancellation-token-contract.md) 约束。

## 1. 目标

开发者在一个业务回调中只处理一个 `CancellationToken`：检查它，并原样传给后续异步调用。调用方取消、生命周期结束、应用关闭和框架超时由 City 在进入回调前组合。

## 2. Effective Cancellation Token

Effective Token 是一个操作最终观察到的 token：

```text
caller token
  + owning scope token
  + application shutdown token
  + framework timeout token
  = effective cancellation token
```

某一来源不存在时不参与组合。组合由框架边界完成，创建 linked CTS 的组件负责释放它。

## 3. 公共 API 形状

面向调用方：

```csharp
ValueTask<TResult> ExecuteAsync(
    Request request,
    CancellationToken cancellationToken = default);
```

框架回调：

```csharp
ValueTask HandleAsync(
    OperationContext context,
    CancellationToken cancellationToken);
```

规则：

- token 位于参数列表末尾。
- 调用方入口可使用 `default`；框架回调中的 token 不可省略。
- context 不重复提供常规操作 token。
- 回调只检查并向下传递框架给出的 token，不自行链接 Scope token。
- Scope token 可供框架和高级适配器读取，但不形成第二套业务编程模型。

## 4. 取消与所有权

- 准入前取消：不得执行用户代码或创建需要清理的资源。
- 准入后取消：停止继续提交，完成必要回滚和释放，再报告取消。
- 取消不能跳过 sibling cleanup；多个清理失败按模块合同聚合。
- API 若只允许取消等待，必须使用 `waitCancellationToken`，并明确操作仍在后台继续。

## 5. 模块映射

| 模块 | Effective Token 的组成与交付 |
|---|---|
| Core | Host 调用 token 与 Application/Lifecycle Scope token 合成后传给 middleware 和 module hook。 |
| MVVM | 调用 token 与 Activation Scope token 合成后传给激活、停用和 activation-owned work。 |
| EventBus | publish/dispatch token 与订阅 owner token、bus shutdown token 合成后传给 handler。 |
| Data | 请求 token 与可选 ParentScope、timeout 合成后传给 handler、transport 和 mapper。 |
| Presentation | outlet commit token 与 candidate/entry/runtime 所有权 token 合成；取消后先回滚事务。 |

## 6. 验证门禁

每个采用本规范的模块至少验证：预取消不进入用户代码、每种来源均可取消操作、开发者只收到一个 token、linked CTS 被释放、取消后没有遗留资源或后台任务，以及 wait-only 参数不会伪装成普通 `cancellationToken`。
