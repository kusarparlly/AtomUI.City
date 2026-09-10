# AtomUI.City.Presentation UI Runtime

## 主路径

```text
services.AddPresentation() or UseModule<PresentationModule>()
-> Host Build/Start
-> Avalonia framework initialization
-> runtime.Attach(lifetime, hostScope)
-> runtime.RegisterWindow(window, id) before Show
```

Attach 建立 PresentationScope 和 dispatcher bridge，不修改 DI。RegisterWindow 只能在 UI 线程执行，创建唯一 WindowSession/WindowScope 并安装关闭 gateway。Attached Property 和显式 RegisterOutlet 共用该 Session 的唯一 name registry。

## 低级入口

`StartAsync/CreateWindowScope` 供 headless/test/compatibility 使用，不附加 Avalonia lifetime、不创建 WindowSession/Outlet registry，桌面应用不得用它绕过主路径。

## Stop

Stop 发布唯一 task 后进入 Stopping，拒绝新 Window。所有已注册 Window 都尝试不可拒绝关闭；单个失败不阻断其他 Window 和 PresentationScope。零失败进入 Stopped，聚合失败进入 Faulted。caller cancellation 只取消等待，不取消共享 shutdown transaction。

## Window

WindowSession 区分 User/Application/OperatingSystem close。非 OS native Closing 被转换为异步 gateway；OS 不拦截。并发 Close/Dispose/Stop 合并；清理顺序为 Outlet Entry、Outlet、Window event/attached session、WindowScope。

## 测试

必须有 unit、Avalonia.Headless 和 Windows desktop 三层证据，覆盖重复 Attach/Register、Show 后注册、named Outlet、close reject/commit/race、Host Stop 和清理失败。
