# AtomUI.City.Presentation View Binding

## 职责

`ViewFactory` 在 UI dispatcher 使用 ViewDescriptor 的强类型 factory 创建 View。`ViewBinder` 把 ViewModel 设置为 `IViewDataContextAware.DataContext` 并返回 `BoundViewHandle`。ViewModel 不知道具体 View，View 不决定导航。

## 线程与所有权

- Avalonia View 创建、DataContext 设置/清理和 View dispose 必须在 UI dispatcher。
- `ViewBinder.Bind` 是同步低级 API；Avalonia View 在非 UI 线程调用时必须失败，不允许同步阻塞 marshal。
- adapter 创建 handle 后在 plan admission 前持有；admission 后由 Outlet/Entry 持有。
- handle dispose 幂等。binding 失败清理已设置的 DataContext 和已创建 View。
- UI event subscription、ActivationScope 和 ViewModelLease 由 PresentationEntry 分别持有，不混入 BoundViewHandle。

## Visual lifecycle

Bind 成功不等于 Attached，handle dispose 也不等于 Detached。ViewBinder 不发布 visual event。只有 View 真实进入/离开 Avalonia VisualTree 时，内部 adapter 才发布带 identity 的物理回执。

## AOT

ViewDescriptor factory 来自显式注册或 Presentation View source generator。运行时不扫描程序集，不猜测 constructor。`ConstructorParameterTypes` 用于生成与诊断，不用于反射 fallback。

## 测试

覆盖 dispatcher 创建、错误 ViewType、DataContext、幂等释放、失败回滚、无合成 visual event、后台 Avalonia binding 拒绝和 generated factory。
