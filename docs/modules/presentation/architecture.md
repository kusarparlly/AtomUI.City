# AtomUI.City.Presentation Architecture

## 核心模型

```text
City Host / Application DI
  -> PresentationRuntime (PresentationScope)
    -> WindowSession (WindowScope)
      -> logical Root composition lease
        -> owned Child composition lease(s)
      -> named RouteOutlet
        -> PresentationEntry
          -> ViewModelLease + BoundViewHandle + ActivationScope + visual subscription
```

Routing-Presentation adapter 是调用编排者，Router snapshot 是导航真相。Presentation 只执行 UI 提交，不反向修改 Router。

## 核心不变量

- 一个 managed Window 只对应一个 WindowSession；Window 必须在 Show 前注册。
- 一个 Window 内 Outlet 名称唯一；一个 Outlet 同时只有一个 current Entry 和一个 in-flight 事务。
- commit plan 单次使用，候选始终只有一个 owner，任何终止路径最终到 `Released`。
- Avalonia 对象只能在 UI dispatcher 访问；用户 guard、activation、deactivation 和 DI 创建不在框架锁内执行。
- final commit 前可以恢复旧 physical content；final commit 后不得回滚 Router 或新 Entry。
- Bind/Unbind 不伪造 VisualTree 事件；回执必须来自真实 Avalonia 事件并匹配 identity。
- Presentation 不含文案、多语言、语言包或 Localization 状态机。
- MVVM 不感知 Root/Child；Presentation 只通过显式 lease 建立逻辑组合，不反射对象图或 visual tree。
- Child 使用独立 ActivationScope 并链接父取消；RouteOutlet Entry 与 composition tree 相互隔离。
- UI command factory 只固定注入 dispatcher/diagnostics 并转发 MVVM 创建参数，不复制命令状态机或接管业务执行线程。

## 所有权表

| 对象 | 创建者 | Owner | 结束条件 |
| --- | --- | --- | --- |
| PresentationScope | PresentationRuntime | Runtime | Runtime Stop |
| WindowScope | PresentationRuntime | WindowSession | Window close/Runtime Stop |
| RouteOutlet | WindowSession | Outlet registration | detach/Window close |
| commit plan candidate | application adapter | adapter -> Outlet -> Entry | reject/rollback/Entry dispose |
| ViewModelLease | IViewModelFactory | PresentationEntry | Entry dispose |
| BoundViewHandle | ViewBinder/application adapter | PresentationEntry | Entry dispose on UI dispatcher |
| modal request | caller | per-Window lane after admission | handled/canceled/failed |
| plugin contribution | plugin adapter | plugin/contribution lease | revoke/unload |
| logical Root ViewModel | application/composition service | Runtime 或 Window composition owner | lease dispose/Window close/Runtime stop |
| owned Child ViewModel | application/composition service | parent composition lease | child lease/parent stop |

## 故障域

| 故障 | 最小故障域 | 状态 |
| --- | --- | --- |
| lookup/create/bind/activation/guard 普通失败 | Operation/Outlet | OutOfSync |
| pre-commit rollback 失败 | Outlet | Faulted |
| failure presenter 失败 | Outlet | Faulted |
| Window 关闭清理失败 | WindowSession | Faulted |
| dispatcher/platform bridge 永久不可用 | Runtime | Faulted |
| post-commit 旧 Entry 清理失败 | 诊断，不回滚新 Entry | Committed |

## 扩展点

公开扩展点只有 DI extension、`PresentationModule`、View attribute/generated registrar、View/Interaction/resource registration、Outlet target 和 failure presenter。新增扩展点必须同时更新 Feature、API card、诊断、测试与兼容性文档。

`AUC-PRESENTATION-017` 完成后，显式 ViewModel composition service/lease 加入上述扩展点；它不允许第三方替换 ownership 状态机。

`AUC-PRESENTATION-018` 增加 `IUiCommandFactory` 作为可替换 DI 扩展点；默认实现保持 internal。替代实现必须继续满足 dispatcher 必选和 MVVM 语义完整转发合同。

## AOT

1.0 只生成 `ViewForAttribute` 对应的强类型 View registrar。运行时不得通过程序集扫描或构造函数猜测发现 View；生成顺序和 hint/type name 必须稳定。
