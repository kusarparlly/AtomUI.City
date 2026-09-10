# AtomUI.City.Presentation Activation Integration

## 两类事实

MVVM Activation 是逻辑生命周期；Avalonia attached/detached 是物理回执。二者不可互相替代：visual event 不重复 Activate/Deactivate，也不直接改变 Router、State 或 EventBus。

## 提交顺序

```text
acquire ViewModel lease and bound View
-> current Entry leave/confirm guard (non-UI)
-> subscribe real Avalonia events and temporary attach (UI)
-> candidate ActivateAsync (non-UI)
-> final commit publishes Entry
-> previous Entry DeactivateAsync and dispose
```

候选在 temporary attach 前拥有独立 ActivationScope。activation 失败时恢复旧 visual 并释放候选。只有 final commit 后 candidate 才是 current Entry。

## Identity 核验

每个 visual receipt 包含 View 引用、WindowId、OutletName、OperationId 和 EntryId。订阅可按这些字段过滤；迟到事件或其他 View/Entry 的事件不能被当前 UI 小模块误接收。handler 失败只诊断并继续其余 handler。

## Window close intent

Closing 先转换为 `WindowCloseOrigin`，可拒绝来源运行全部 leave guards 和最多一个 confirmation；允许后再提交原生 Close。OS shutdown 不阻止 native close，只执行 best-effort cleanup。

## 测试

覆盖 activation success/failure、rollback、deactivation、真实 attached/detached、identity mismatch、迟到事件、handler failure、close allow/reject 和多 confirmation 拒绝。
