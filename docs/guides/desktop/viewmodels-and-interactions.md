# 编写 ViewModel 与交互

MVVM 层负责可观察状态、命令、验证和 UI 交互意图。ViewModel 不直接创建窗口或控件；Presentation 把 ViewModel 映射到 View，并管理二者的生命周期。

## 把工作放到正确阶段

```csharp
public sealed class DashboardViewModel : ViewModelBase
{
    protected override async ValueTask OnActivatedAsync(
        ActivationContext context,
        CancellationToken cancellationToken)
    {
        await LoadDashboardAsync(cancellationToken);
    }

    protected override ValueTask OnDeactivatedAsync(
        CancellationToken cancellationToken)
    {
        return ValueTask.CompletedTask;
    }
}
```

构造函数只接收依赖并建立初始不变量。网络加载、事件订阅和与可见性相关的工作放到 activation；对应清理放到 deactivation 或 activation scope。不要让一个已离开 VisualTree 的 ViewModel 继续接收通知。

## 命令代表一次事务

命令执行通常具有 Idle、Running、Succeeded、Failed、Cancelled 或 Rejected 等状态。设计命令时：

- 重复点击策略要明确：拒绝、取消前一次或串行；
- cancellation token 要传到底层业务调用；
- 失败进入可观察结果与 diagnostics，不靠 `async void` 抛到 UI 线程；
- `CanExecute` 是交互提示，不替代服务端或领域权限校验；
- ViewModel 释放后不得启动新命令。

多个相关命令可以通过 `CommandGroup` 聚合观察，但每个命令仍保留独立执行结果。

## 验证输入

`ViewModelBase` 建立在 observable validation 模型上。验证错误属于 ViewModel 状态，Presentation 将其映射为视觉反馈。不要让 View 通过解析异常文本推断字段错误，也不要在 getter 中执行昂贵或有副作用的验证。

## 请求 UI 交互

ViewModel 用 `Interaction<TRequest,TResult>` 表达“需要用户确认或选择”，View 层在自己的 activation scope 注册 handler：

```csharp
var result = await ConfirmDelete.RequestAsync(
    new ConfirmDeleteRequest(orderId),
    cancellationToken);

if (result.Status == InteractionResultStatus.Completed && result.Value)
{
    await DeleteAsync(orderId, cancellationToken);
}
```

如果没有 handler，结果是 `NotHandled`；取消和异常也通过稳定状态返回。交互 handler 跟随 View 的 activation scope 释放，避免关闭窗口后仍弹出对话框。

## View 映射

用 `[ViewFor]` 或 Presentation 注册 API 建立 ViewModel 到 View 的映射。应用代码发起路由或 interaction，不直接调用内部 `ViewFactory`。这样 City 才能统一处理 ViewModel ownership、service scope、激活和释放。

真实 ViewModel 激活、命令和交互用法见 [Desktop Dogfood ViewModels](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/ViewModels/)。
