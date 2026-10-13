# AtomUI.City.State Application State 合同

## 适用范围

本专题属于 `AtomUI.City.State` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Application State` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.State` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
- 新增 contract 必须进入 [api-contracts.md](api-contracts.md)。
- 新增功能必须分配 Feature ID，并进入 [features.md](features.md)。
- 修改失败行为、默认值、诊断码或生命周期状态必须进入 [compatibility.md](compatibility.md)。

## 运行时边界

- Owner 必须明确：Host、Module、Plugin、Route、Operation、Connection、View 或 Test scope。
- 释放必须幂等；释放后 mutating API 必须失败或返回声明的 Result。
- Cancellation 必须在进入外部调用、用户 handler、插件代码、IO、dispatcher work 前后观察。
- 插件来源对象必须可撤销，不能泄漏到 Host 根单例。

## 失败行为

- 输入无效：使用标准参数异常或模块 Result。
- 生命周期状态非法：返回失败 Result、模块异常或稳定诊断。
- 依赖缺失：阻止当前功能启用，不影响无关功能。
- 插件卸载中：拒绝创建新贡献，并撤销已有贡献。
- 释放失败：记录诊断并继续释放其他资源。

## 测试要求

| Feature ID | 相关能力 | 测试文件 |
| --- | --- | --- |
| AUC-STATE-001 | Writable State | WritableStateTests |
| AUC-STATE-002 | Application State | ApplicationStateTests |
| AUC-STATE-003 | Computed State | ComputedStateTests |
| AUC-STATE-004 | State Subscription | StateScopeTests; StateThreadingTests |
| AUC-STATE-005 | State Snapshot | StateSnapshotTests |
| AUC-STATE-006 | Collection State | StateCollectionTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 模块 Writer 工厂（AUC-STATE-002 / AUC-STATE-009）

`AddState()` 必须注册 `IApplicationStateWriterFactory`。模块不得从 DI 直接取得 Host writer 来绕过 OwnerWrite；标准用法是：

```csharp
var writer = writerFactory.CreateWriter(
    StateWriteAuthority.Module("Sample.Module"));
```

返回的 writer 固定持有该 authority，并在每次 `GetWritable`、`Set`、`Update` 时由 registry 校验 `StateDefinition.OwnerModule` 或 capability。工厂不缓存业务状态、不扩大 authority，也不改变 `IApplicationState` 的只读消费合同。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.State 应用级共享状态设计

适用范围：应用级共享状态、DI 访问、写入策略、状态注册表和访问边界

### 1. 定位

桌面软件天然存在应用级共享状态，例如主题、语言、当前用户、当前工作区、网络状态、窗口布局策略、全局忙碌状态和授权状态。

这些状态必须由 Host 管理并绑定 `ApplicationScope`，不能做成静态全局变量。

```text
ApplicationScope
-> Application State Registry
   -> Theme state
   -> Culture state
   -> Auth state
   -> Current workspace state
   -> Network status state
```

### 2. DI 入口

Host 启动时注册：

| 服务 | 职责 |
|---|---|
| `IApplicationState` | 读取和监听应用级共享状态。 |
| `IApplicationStateWriter` | 写入应用级共享状态。 |
| `IStateRegistry` | 底层状态注册表。 |
| `IStateScopeAccessor` | 获取当前生命周期状态作用域。 |

使用方通过构造函数注入状态服务，不允许访问静态对象。

### 3. 读取和监听 API

建议语义：

```csharp
public interface IApplicationState
{
    IReadOnlyState<T> Get<T>(StateKey<T> key);

    IStateSubscription OnChange<T>(
        StateKey<T> key,
        Action<StateChangedEventArgs<T>> handler);
}
```

监听必须绑定生命周期 Scope。

```csharp
activationScope.OnStateChanged(ThemeStates.CurrentTheme, args => { });
```

这样监听会随 ViewModel 停用自动释放。

### 4. 写入 API

建议语义：

```csharp
public interface IApplicationStateWriter
{
    IWritableState<T> GetWritable<T>(StateKey<T> key);

    bool Set<T>(StateKey<T> key, T value);

    bool Update<T>(StateKey<T> key, Func<T, T> updater);
}
```

`IApplicationState` 和 `IApplicationStateWriter` 分离，方便 Host 给插件或普通模块只暴露只读接口。

`ApplicationStateRegistry` 本身是 Host writer。模块和插件不得直接共享这个 writer；Host 根据模块身份、插件身份和已授予 capability 创建 `StateWriteAuthority`，再通过 `ApplicationStateRegistry.CreateWriter` 取得受约束 writer。

`StateWriteAuthority` 是进程内可信代码的纪律级策略描述，不是不可伪造的安全凭据。公开工厂允许应用 Host 组装 writer；运行在同一进程且能够直接访问 registry 的代码也能自行构造 authority。需要承载不可信插件时必须使用进程隔离，不能依赖该类型建立安全边界。

`Update` 必须在 registry lookup 前拒绝 null updater。这样缺参调用不会写入未注册 state 诊断，也不会隐式创建或访问 state。

### 5. 写入策略

全局状态必须有写入规则。

| 策略 | 说明 |
|---|---|
| `ReadOnly` | 所有模块可读，只有 Owner 可初始化。 |
| `OwnerWrite` | 只有声明模块可写。 |
| `HostWrite` | 只有 Host writer 可写。 |
| `AuthorizedWrite` | 通过权限或 capability 授权后可写。 |
| `PluginIsolated` | 插件只能写自己的状态分区。 |

执行规则：

- `ReadOnly` 拒绝所有运行时 writer；默认值只在注册时初始化。
- `OwnerWrite` 要求 definition 声明 `ownerModule`，且 writer 的模块身份必须匹配。
- `HostWrite` 只允许 Host writer。
- `AuthorizedWrite` 要求 definition 声明 `writeCapability`，且 writer 持有该 capability。
- `PluginIsolated` 要求 definition 声明 `pluginId`，且 plugin writer 身份必须匹配。

默认不允许隐式创建应用级状态。读取未注册 key 必须返回诊断错误。

### 6. 生命周期

应用级状态绑定 `ApplicationScope`。

规则：

- ApplicationScope 停止时释放所有应用级状态订阅。
- 应用关闭前可以按 snapshot policy 保存状态。
- 运行时插件不能让自己的私有 state 升级成 Host 应用级状态。
- 当前用户、认证状态等安全敏感状态必须由 Security 模块控制写入。

### 7. 模块和插件边界

模块可以贡献应用级状态定义。

插件默认只能读取应用级状态。插件需要写入时必须同时满足：

- 插件 manifest 声明 capability。
- Host 授权该 capability。
- 目标状态的 `StateAccessPolicy` 允许写入。
- 写入过程进入诊断链路。

### 8. 测试矩阵

| 功能点 | 测试类型 | 断言 |
|---|---|---|
| DI 读取应用状态 | Unit | 构造函数注入可读取注册状态。 |
| 未注册状态 | Unit | 返回诊断错误，不隐式创建。 |
| 只读入口 | Unit | `IApplicationState` 不能写入。 |
| Writer 写入 | Unit | 授权 writer 可更新状态。 |
| Writer 参数边界 | Unit | null updater 先于 registry lookup 被拒绝。 |
| 写入策略拒绝 | Unit | 拒绝写入并记录诊断。 |
| StateDefinition 边界 | Unit | 未知 enum 和非法 schema version 被拒绝。 |
| ActivationScope 监听 | Unit | Scope 停用后自动解除订阅。 |
| 插件只读访问 | Unit | 插件默认不能写 Host 状态。 |
