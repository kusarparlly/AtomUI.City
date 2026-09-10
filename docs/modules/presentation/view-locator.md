# AtomUI.City.Presentation View Locator

## Key 和注册

View 使用 `(ViewModelType, ViewKey)` 精确键。默认 ViewKey 为空；同一 ViewModel 的多个 View 必须使用不同 key。lookup 不做 assignable type、命名约定或程序集扫描 fallback。

ViewDescriptor 包含 ViewModelType、ViewType、强类型 factory、ViewKey、plugin/contribution owner 和 constructor parameter metadata。显式注册和 generated registrar 进入同一 ViewRegistry。

## 重复和撤销

默认重复注册抛 `DuplicateView` 且不产生部分修改。`ViewRegistrationOptions.ReplaceExisting` 必须带 owner，形成覆盖栈；撤销顶层恢复上一层，撤销中间层不改变当前顶层。registry 读写并发安全，公开列表为 immutable snapshot。

## Generator 1.0

Presentation generator 只处理 `ViewForAttribute`：校验重复 key、插件 owner metadata 和可确定构造函数，生成稳定排序的强类型 View registrar。任何错误不得生成部分 registrar。Interaction/resource/plugin manifest 不在本 Feature 内。

## 失败

未注册或 owner 已撤销返回/抛 `ViewNotFound` 并记录 AUCPRS006；成功记录 AUCPRS005。失败不创建 View、不改变 Outlet。

## 测试

覆盖默认/命名 key、exact type、原子批量注册、override stack、owner revoke、并发读写、诊断和 generator snapshot/incremental stability。
