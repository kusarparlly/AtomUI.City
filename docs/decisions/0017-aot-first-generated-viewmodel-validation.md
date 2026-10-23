# 0017 ViewModel 验证采用独立基类与 AOT-first 生成代码

状态：Accepted

日期：2026-10-23

## 背景

`ViewModelBase` 当前继承 CommunityToolkit.Mvvm 的 `ObservableValidator`。这使所有 ViewModel 都进入依赖运行时验证元数据的构造路径，并使带 `validate` 的 `SetProperty` 产生 IL2026。普通 ViewModel 即使从不验证，也承担同一 AOT/trimming 风险。

City 尚未正式发布，当前允许为了长期正确性调整 Preview API，不要求保留 `ViewModelBase -> ObservableValidator` 的源码或二进制兼容。

## 决策

- `ViewModelBase` 成为 City 自有的轻量基类，直接提供属性变更通知、`SetProperty`、Activation、Dispose 和 Diagnostics；不再公开继承 `ObservableObject` 或 `ObservableValidator` 等第三方具体基类。
- 新增 `ValidatingViewModelBase : ViewModelBase`。只有需要输入验证的 ViewModel 才继承它；它实现 `INotifyDataErrorInfo`，拥有 `HasErrors`、`ErrorsChanged`、`GetErrors`、错误集合和带验证的 property mutation 入口。
- 开发者继续使用 `System.ComponentModel.DataAnnotations` 的常用 Attribute 声明规则。Attribute 由开发者书写，City Generator 读取声明并生成确定的属性分派和验证调用代码；City 不复制 `RequiredAttribute`、`RangeAttribute` 等规则实现。
- 生成路径不得在运行时反射属性、枚举 Attribute、按字符串查找验证方法或扫描程序集。生成类型只增加实现细节，不扩大未经审阅的 public surface。
- 第一阶段必须覆盖 `Required`、`Range`、`StringLength`、`MinLength`、`MaxLength`、`RegularExpression`、`EmailAddress`、`Phone`、`Url` 和 `CreditCard`。`Compare` 必须生成直接的跨属性访问，不能运行时按名称反射。
- `CustomValidation` 必须在编译期解析目标类型与静态方法并生成直接调用；找不到方法、签名不支持、目标不可访问或调用依赖开放泛型时阻断生成。
- 任意自定义业务规则通过待冻结签名的显式 typed validator contract 接入。Strict AOT 下，未知 `ValidationAttribute` 不得静默退回反射；在 typed contract API card 冻结前不得施工该扩展点。
- 验证不通过是可观察状态，不抛业务异常；验证规则本身异常进入 `Failed`/`AUCMVVM008`，不能伪装为普通字段错误。
- `ObservableValidator` 兼容路径不保留。CommunityToolkit 的 command 接口和实现仍可继续复用；本 ADR 只替代 ADR 0004 中有关公开 ViewModel 基类和 `ObservableValidator` 的决策。

## 影响

- 新增 `AUC-MVVM-013 Generated ViewModel Validation` 和 `AUC-GENERATORS-011 ViewModel Validation Generation`。
- `ViewModelBase` 的父类变化是 Preview breaking change；现有派生类必须重新编译，需要验证的类型改为继承 `ValidatingViewModelBase`。
- 普通 ViewModel 不持有验证状态，不产生 DataAnnotations 反射风险。
- 验证型 ViewModel 保留 DataAnnotations 的声明式体验，同时把属性到规则的连接提前到编译期。
- Mvvm 和 Generators 必须共同完成 API、诊断、generated source、Public API baseline 与双 TFM NativeAOT 验收，不能只提交其中一半。

## 后续约束

- 施工前必须冻结 typed custom validator 的 API card、nullability、错误返回、取消和异常合同。
- Generator 必须输出稳定 hint name、确定性规则顺序和精确 source location diagnostic。
- `AtomUICityStrictAot=true` 下，无法静态生成的验证声明必须以稳定 Error diagnostic 阻断构建。
- 测试必须覆盖每一种首批 Attribute、多个规则顺序、对象级/属性级错误、`Compare`、`CustomValidation`、非法签名、自定义 validator、Dispose 后 mutation、并发读取以及无反射回退。
- `net8.0`、`net10.0` 的 `win-x64` NativeAOT 必须真实 publish/run，并实际断言 Valid/Invalid、`HasErrors`、`ErrorsChanged` 和 `GetErrors`，不得只证明应用启动。

## 实施状态

已于 2026-10-23 完成。Mvvm 与 Generator 代码、Public API baseline、单元/生成器测试和持久化双 TFM NativeAOT Runtime Matrix 均已落地；typed custom validator 扩展点仍按本 ADR 约束保留为未来独立设计，不影响已支持的 `CustomValidation` 静态方法直连。
