# AtomUI.City.Presentation ViewModel Composition

状态：Approved Design / Implementation Pending

Feature ID：`AUC-PRESENTATION-017`

关联决策：[ADR 0011](../../decisions/0011-presentation-root-child-ownership.md)

## 定位

本能力组织不由 RouteOutlet 创建的逻辑 ViewModel，例如 Window Root、状态栏、工具栏或工作区。它不改变 MVVM 的原子生命周期，也不把 ViewModel 关系等同于 Avalonia visual tree。

## Public contract

- Presentation 提供显式 composition service；调用方使用 owner identity attach Root，并在 Root 下 attach Child。
- attach 成功返回唯一异步 ownership lease。lease 暴露稳定 identity 和终态只读信息，但不暴露内部可变 children 集合。
- Root owner 必须属于当前 Runtime 或 WindowSession；跨 Runtime/Window 的 parent/child 组合稳定拒绝。
- 同一 ViewModel 实例不能同时被两个 composition owner 以 owned 方式 attach。
- `Borrowed` 对象的最终 Dispose 仍由外部 owner 负责，但其 activation session 必须由 composition lease 完整停用。

最终 public 类型名称在 API review 中确定，行为合同不得弱化为同步 `IDisposable` 或反射发现。

## 所有权模型

```text
PresentationRuntime / WindowSession
  -> Root composition lease
      -> Root ActivationScope
          -> Child composition lease (0..n)
              -> independent Child ActivationScope
```

- parent 持有 child lease；child Scope 的取消链接 parent Scope，但 task/resource registry 独立。
- Child 可以继续显式拥有下一层 Child；释放始终深度优先、leaf-to-root。
- RouteOutlet 的 `PresentationEntry` 继续独立拥有 route ViewModel，不自动加入 composition tree。
- Root 不是进程全局单例。每个明确 owner 可以拥有零个或多个命名 Root，但 identity 在 owner 内唯一。

## 生命周期

```text
Created
  -> Attaching
  -> Active
  -> Stopping
  -> ChildrenStopped
  -> RootDeactivated
  -> Released
```

- attach 在用户 activation 前完成 ownership 注册，activation 失败会回滚并释放全部候选资源。
- lease dispose 先关闭 child 准入，再并行或按稳定顺序请求 child 停止，最终按逆 attach 顺序完成 leaf-to-root 清理。
- 一个 child 失败不能跳过 siblings；所有失败聚合并写诊断。
- Window close 和 Runtime stop 接管所有仍存活 Root lease；重复 dispose/stop 等待同一关闭事务。
- caller token 只取消等待，不能让已经开始的所有权清理半途失去 owner。

## 失败和诊断

| Planned Code | Name | 必需上下文 |
| --- | --- | --- |
| `AUCPRS044` | ViewModelCompositionAttached | `ownerId`, `compositionId`, `parentId`, `viewModelType`, `role` |
| `AUCPRS045` | ViewModelCompositionRejected | `ownerId`, `compositionId`, `parentId`, `viewModelType`, `reason` |
| `AUCPRS046` | ViewModelCompositionCleanupFailed | `ownerId`, `compositionId`, `viewModelType`, `stage`, `exceptionType` |

源码落地前上述 code 属于 approved/reserved。identity 冲突、owner 终态、重复 owned instance 和跨 owner parent 都必须稳定拒绝。

## 非目标

- 不提供 `RootViewModelBase`、`ChildViewModelBase` 或全局 `CurrentRoot`。
- 不扫描 ViewModel 属性、DI scope、View DataContext 或 visual tree 推断关系。
- 不把 route ViewModel 自动变成 Window Root 的 child。
- 不替应用决定哪个 Root 是“主”Root。

## 测试矩阵

- Root 正常 attach/activate/dispose；重复 identity、重复 instance 和终态 owner 拒绝。
- Child 独立 Scope、父取消链接、任务 drain、nested child 和 leaf-to-root 顺序。
- child activation/cleanup 部分失败仍清理 siblings 和 parent，并聚合诊断。
- caller 取消、Window close、Runtime stop 和显式 lease dispose 竞态共享关闭事务。
- RouteOutlet Entry 与 composition tree 相互隔离。
- Borrowed/owned ViewModel 的最终 Dispose 行为符合 lease ownership。

## 完成标准

- Feature、API、Architecture、Lifecycle、Diagnostics、Testing、Compatibility 和 PublicAPI baseline 同步。
- Unit/contract、失败注入、Window close/Runtime stop 集成测试通过后，Feature 状态才可提升。
