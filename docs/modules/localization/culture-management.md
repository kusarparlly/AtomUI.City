# AtomUI.City.Localization Culture Management 合同

## 适用范围

本专题属于 `AtomUI.City.Localization` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Culture Management` 相关实现决策，不重新定义模块边界。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。

## Public Contract

- 只允许通过 `AtomUI.City.Localization` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-LOCALIZATION-001 | Culture State | CultureStateTests |
| AUC-LOCALIZATION-002 | Language Package Providers | LanguagePackageProviderTests |
| AUC-LOCALIZATION-003 | Lazy Loading | LocalizationServiceTests |
| AUC-LOCALIZATION-004 | Lookup and Fallback | LocalizationServiceTests |
| AUC-LOCALIZATION-005 | Assembly Language Packages | LanguagePackageProviderTests; LocalizationDeclarationAttributeTests |
| AUC-LOCALIZATION-006 | Optional Application Refresh Hook | LocalizationServiceTests |
| AUC-LOCALIZATION-007 | Plugin Package Revocation | LocalizationServiceTests |
| AUC-LOCALIZATION-008 | Generated Localization Manifest | AtomUICityIncrementalGeneratorLocalizationTests; LocalizationManifestBuilderTests |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `AtomUI.City.Presentation concrete UI types` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## AtomUI.City.Localization Culture Management 设计

适用范围：当前文化状态、Host 配置的默认 culture、提交前事务式文化切换、提交后 bridge 失败和通知。用户偏好与系统文化选择策略由应用配置层负责。

### 1. 定位

Culture management 负责决定当前应用使用哪个 culture，以及文化切换如何安全完成。

桌面应用必须支持运行时切换文化，不能要求重启应用。

### 2. Culture 来源

文化来源优先级：

```text
Explicit user selection
-> persisted user preference
-> application default
-> system UI culture
-> invariant culture
```

用户选择应保存到用户配置。系统文化变化是否自动跟随由 Host policy 决定。

### 3. Culture State

Culture state 应包含：

- Current culture。
- Current UI culture。
- Fallback culture chain。
- Revision。
- Loaded package set。

Revision 用于让 binding、cache 和 localizer 判断是否需要刷新。

`CultureState` 是深只读快照：集合会复制，暴露的 `CultureInfo` 是只读实例，构造后修改调用方传入的 culture 或集合不会改变已发布状态。`LocalizationOptions` 提供默认 culture、默认 UI culture 和全局 fallback culture。未配置时默认使用 invariant culture。fallback chain 的顺序是当前 lookup 可见 language package descriptor fallback、全局 fallback、culture parent chain、invariant culture，并去重。

### 4. 事务式切换

文化切换流程：

```text
SetCultureAsync
-> calculate active package set
-> load target culture packages
-> validate critical resources
-> prepare optional application resource swap
-> commit culture state
-> apply AtomUI/Avalonia resources on UI Thread
-> notify subscribers
```

提交前失败：

```text
Package load or critical validation failed
-> keep previous culture state
-> dispose partially loaded packages
-> emit diagnostics
```

### 5. 并发策略

同一时间只有一个 Localization mutation 执行；culture switch 与 contribution revoke 共用 FIFO 队列。

规则：

- 新切换请求排队等待前一 mutation 完成。
- 已进入 commit 阶段后不允许抢占。
- 文化切换取消不是 fatal error。
- 调用方取消只控制提交前阶段；`CultureState` 一旦发布，bridge 和 `LocalizedText` 刷新改用 service lifetime token 完成本次事务，避免已切换 culture 却只刷新部分 UI。
- 文化切换和其 service-owned load 必须绑定 `LocalizationService` Host 生命周期。
- provider、可选 application-owned bridge 和 LocalizedText handler 均在框架锁外执行；这些 callback 中重入 mutation 必须快速失败，不能排队等待自身。

### 6. 线程模型

资源加载可以在后台进行。

Localization Core 在当前异步调用链执行 bridge 和 `LocalizedText` handler，不承诺 UI 线程。AtomUI/Avalonia resource swap 和实际 UI binding mutation 必须由应用或独立 UI adapter 调度到 UI Thread。

Localization Core 不依赖 Avalonia；应用组合层或独立可选 UI 适配包实现 bridge。

### 7. 错误策略

| 场景 | 默认处理 |
|---|---|
| culture 不支持 | 拒绝切换，保留旧 culture。 |
| fallback 指向当前 culture | 拒绝切换，保留旧 culture。 |
| 重复设置当前 culture | 返回成功，不重新加载 package，不递增 revision。 |
| package 加载失败 | rollback。 |
| fallback 懒加载失败 | 当前 lookup 继续后续 fallback，最终返回 missing marker；不回滚已提交 culture。 |
| UI apply 失败 | 不回滚已提交 CultureState；返回失败 Result、诊断并继续本地文本刷新。 |
| 并发切换冲突 | FIFO queue；callback 重入返回 `ReentrantOperation`。 |

### 8. 测试策略

测试必须覆盖：

- 默认 culture 选择。
- 应用配置得到的默认 culture。
- 成功切换。
- package 加载失败 rollback。
- UI apply 失败保留已提交 CultureState 并继续 LocalizedText 刷新。
- 并发切换。
- revision 递增。
