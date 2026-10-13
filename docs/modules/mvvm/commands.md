# AtomUI.City.Mvvm Commands 合同

> `AUC-MVVM-008` 至 `010` 已批准泛型参数、业务 CanExecute、并发策略、动态 ActivationScope 和异常交付新合同；实现目标见 [structured-concurrency-and-commands-v2.md](structured-concurrency-and-commands-v2.md)。本文其余段落描述当前已实现基线，不能用旧行为覆盖已接受的迁移决定。

## 适用范围

本专题属于 `AtomUI.City.Mvvm` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Commands` 相关实现决策，不重新定义模块边界。

## 设计决策

- 本专题必须绑定 Feature ID。
- 必须说明 public contract、失败行为和测试。
- 不得只描述概念。

## Public Contract

- 只允许通过 `AtomUI.City.Mvvm` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-MVVM-001 | ViewModel Base | ViewModelBaseTests |
| AUC-MVVM-002 | Activation and Deactivation | ActivationScopeTests; DeactivationTests; ViewModelBaseTests |
| AUC-MVVM-003 | Commands | CommandTests |
| AUC-MVVM-004 | Interactions | InteractionTests |
| AUC-MVVM-005 | Validation | ValidationScopeTests |
| AUC-MVVM-006 | Operation Scope | CommandTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Mvvm Commands 设计

适用范围：Command、Async Command、OperationScope、执行状态、错误策略、权限联动、组合命令和测试支持。

### 1. 定位

Command 是 ViewModel 暴露用户动作的主要方式。

AtomUI.City.Mvvm 不重新发明基础命令类型，第一版沿用 `CommunityToolkit.Mvvm` 的命令模型，并在其上补充生命周期、执行状态、取消、错误和诊断。

### 2. 底层命令类型

默认使用：

```text
IRelayCommand
IAsyncRelayCommand
RelayCommand
AsyncRelayCommand
```

不引入 `CityCommand` / `CityAsyncCommand` 这类命名。

### 3. OperationScope

每次 async command 执行都应创建 OperationScope。

OperationScope 负责：

- 提供 CancellationToken。
- 持有 operation id、Running 或终态 status、终态 result、error 和 elapsed。
- 记录 Command 执行诊断。
- 关联当前 ViewModel。
- 关联当前 ActivationScope。
- 记录执行耗时和结果。
- 将错误交给 ErrorPolicy。
- 在外部 token 取消时先提交 Canceled result，再通知执行体观察到 operation token 取消。

执行流程：

```text
CanExecute check
-> Create OperationScope
-> Execute command
-> Capture result
-> Capture error or cancellation
-> Dispose OperationScope
```

终态规则：

- `Complete`、`Cancel`、`Fail`、`Reject` 首次调用决定唯一终态。
- 重复终态返回同一个 `OperationExecutionResult`，不改写 `Elapsed` 或 `Error`。
- `Cancel` 和 active `Dispose` 都必须在取消 token callback 前提交 `Canceled` 状态。
- `Dispose` 后 `Id`、`Status`、`Result`、`Error`、`Elapsed` 仍可读取，mutating API 抛 `ObjectDisposedException`。

Command 失败不应导致 ViewModel 死亡。

### 4. Command 状态

Command 需要标准化运行状态：

| 状态 | 说明 |
|---|---|
| `CanExecute` | 当前是否可执行。 |
| `IsExecuting` | 当前是否正在执行。 |
| `LastResult` | 最近一次执行结果。 |
| `LastError` | 最近一次失败信息。 |
| `CancellationToken` | 当前执行取消令牌。 |

这些状态应可被 UI、Diagnostics 和 Testing 读取。

### 5. 权限和路由联动

Command 可执行状态可以接入：

- Security 权限。
- Routing 当前状态。
- ViewModel active 状态。
- Validation 状态。
- Operation 正在执行状态。

Security 和 Routing 不由 Mvvm 实现。Mvvm 只提供命令状态接入点。

### 6. CompositeCommand / CommandGroup

Mvvm 应支持组合命令，用于菜单、工具栏、全局快捷键和 Shell 级命令。

组合命令规则：

- 可以聚合多个子命令。
- 只执行当前 active 上下文中的子命令。
- 子命令可随 ActivationScope 注册和释放。
- 可执行状态由 active 子命令共同决定。
- 执行结果和错误需要进入 OperationScope 诊断。

实现类型定名为 `CommandGroup`（ICommand 聚合，子命令可随 ActivationScope 注册和释放，Execute 经 OperationScope 记录结果与错误）。

### 7. 取消策略

Command 取消不是错误。

取消来源：

- 用户取消。
- ActivationScope 停用。
- Route 离开。
- Plugin 停用。
- Host shutdown。

Command 必须区分 canceled、failed 和 completed。

### 8. 错误策略

| 场景 | 默认处理 |
|---|---|
| CanExecute 失败 | 记录诊断，命令不可执行。 |
| Execute 抛异常 | Operation failed，不杀死 ViewModel。 |
| Execute 被取消 | Operation canceled，不作为失败统计。 |
| CompositeCommand 子命令失败 | 聚合结果，继续策略由 command policy 决定。 |

### 9. AOT / Source Generator

Generator/Analyzer 可负责：

- 生成 command descriptor。
- 诊断 command 未绑定 ActivationScope。
- 诊断 async command 缺少取消令牌接入。
- 诊断 command id 重复。
- 输出 command manifest，供菜单、工具栏、快捷键和测试使用。

### 10. 测试策略

Testing 包应支持：

- 执行 command 并断言 OperationScope。
- 断言 `IsExecuting`。
- 断言成功、失败和取消结果。
- 断言权限状态影响 `CanExecute`。
- 断言 CompositeCommand active 行为。
- 断言错误诊断。
