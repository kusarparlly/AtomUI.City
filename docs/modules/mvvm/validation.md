# AtomUI.City.Mvvm Validation 合同

## 适用范围

本专题属于 `AtomUI.City.Mvvm` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Validation` 相关实现决策，不重新定义模块边界。

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
| AUC-MVVM-013 | Generated ViewModel Validation | GeneratedValidationTests; MvvmNativeAotProcessTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Mvvm Validation 设计

适用范围：ViewModel 验证、验证状态、Command 联动、Presentation 集成、错误策略和测试支持。

### 1. 定位

Validation 负责 ViewModel 层的输入验证和状态暴露。

验证失败不是异常。验证失败应作为状态暴露给 UI、Command、Diagnostics 和 Testing。

### 2. 基类与所有权边界

依据 [ADR 0017](../../decisions/0017-aot-first-generated-viewmodel-validation.md)，验证不再由所有 `ViewModelBase` 默认继承的 `ObservableValidator` 提供：

```text
ViewModelBase
├── property notification / activation / dispose / diagnostics
└── no validation state

ValidatingViewModelBase : ViewModelBase
├── INotifyDataErrorInfo
├── HasErrors / ErrorsChanged / GetErrors
├── generated synchronous validation
└── ValidationScope integration
```

需要验证的开发者 ViewModel 必须继承 `ValidatingViewModelBase`；普通 ViewModel 只继承 `ViewModelBase`。City 不保留 `ObservableValidator` 反射兼容路径。

Mvvm 在验证基类上提供：

- ValidationScope。
- 同步验证和异步验证结果归一。
- Command 与验证状态联动。
- Presentation 可观察验证结果。
- Diagnostics 记录验证失败来源。

### 3. ValidationScope

ValidationScope 表示一组验证状态的生命周期边界。

ValidationScope 可以绑定：

- ViewModel。
- ActivationScope。
- RouteScope。
- OperationScope。

ViewModel 停用时，临时验证状态应随 ActivationScope 释放。

### 4. 验证结果模型

验证结果应区分：

| 结果 | 含义 |
|---|---|
| Valid | 验证通过。 |
| Invalid | 验证失败。 |
| Pending | 异步验证中。 |
| Canceled | 验证被取消。 |
| Failed | 验证逻辑异常。 |

验证逻辑异常和验证失败必须区分。

### 5. Command 联动

Command 可执行状态可以依赖 Validation 状态。

规则：

- Invalid 时 command 默认不可执行。
- Pending 时 command 是否可执行由 command policy 决定。
- Validation Failed 进入 ErrorPolicy，但不杀死 ViewModel。
- Command 执行前可以触发一次验证。

### 6. Presentation 集成

Presentation 负责把验证状态展示为 UI。

Mvvm 只提供：

- 属性级错误。
- 对象级错误。
- 验证状态变化通知。
- 验证诊断。
- Presentation 绑定输入：field key、Status、Errors、Messages 和 owner scope id。

UI 展示形式由 Presentation 和应用决定。

### 7. 插件边界

插件 ViewModel 的验证状态绑定插件 ActivationScope。

插件停用时：

- 取消 pending validation。
- 释放 ValidationScope。
- 清理 validation subscriptions。

插件验证失败不能影响 Host 全局验证状态。

### 8. AOT / Source Generator

Generator/Analyzer 必须负责：

- 编译期读取 `ValidatingViewModelBase` 派生类上的 DataAnnotations，不运行时枚举 Attribute。
- 为常用 Attribute 生成确定性属性分派和静态规则调用；`Compare` 生成直接属性访问。
- 把 `CustomValidation` 的类型与方法名解析成直接静态调用，非法或不可访问签名输出 `AUCGEN009`。
- 未知 `ValidationAttribute` 在 Strict AOT 下输出 `AUCGEN009`，不得静默反射降级。
- 生成属性验证和全量验证入口，生成成员不扩大 public surface。
- 诊断异步验证未接入 cancellation token。

首批标准 Attribute 范围固定为 `Required`、`Range`、`StringLength`、`MinLength`、`MaxLength`、`RegularExpression`、`EmailAddress`、`Phone`、`Url`、`CreditCard`；新增规则必须同步 Feature、测试和 AOT 证据。typed custom validator 的精确 API 在施工前另行冻结。

### 9. 错误策略

| 场景 | 默认处理 |
|---|---|
| 验证失败 | 暴露 Invalid，不抛异常。 |
| 验证取消 | 暴露 Canceled。 |
| 验证逻辑异常 | 暴露 Failed，记录诊断。 |
| ViewModel 停用 | 取消 pending validation。 |

### 10. 测试策略

Testing 包应支持：

- 触发属性验证。
- 触发对象验证。
- 断言 Valid/Invalid/Pending/Canceled/Failed。
- 断言 Command 与验证状态联动。
- 断言 ActivationScope 释放时取消验证。
- 断言诊断记录。
