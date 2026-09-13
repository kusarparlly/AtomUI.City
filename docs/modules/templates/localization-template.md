# AtomUI.City.Templates Localization Template 合同

## 适用范围

本专题属于 `AtomUI.City.Templates` 模块文档体系，必须与 [overview.md](overview.md)、[features.md](features.md)、[api-contracts.md](api-contracts.md)、[testing.md](testing.md) 保持一致。它只细化 `Localization Template` 相关实现决策，不重新定义模块边界。

Feature：`AUC-TEMPLATES-008`。状态：Completed。公开入口为 `GenerationTemplateRenderer` 的 Localization kind。

## 设计决策

- 语言包按当前 culture 懒加载。
- assembly 语言包必须支持运行时加载和撤销。
- 缺失 key 必须输出诊断并走 fallback。

## Public Contract

- 只允许通过 `AtomUI.City.Templates` 的 public API、attribute、options、manifest、generated output 或 DI extension 暴露本专题能力。
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
| AUC-TEMPLATES-001 | Application Template | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-002 | Package Layout | TemplatePackageLayoutTests |
| AUC-TEMPLATES-003 | Template Variables | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-004 | Plugin Template | TemplatePackageLayoutTests |
| AUC-TEMPLATES-005 | Test Template | ApplicationTemplateBuildSmokeTests |
| AUC-TEMPLATES-006 | Module Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-007 | Page Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-008 | Localization Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-009 | Configuration Template | GenerationTemplateRendererTests |
| AUC-TEMPLATES-010 | Avalonia Desktop Application Template | Pending |

本专题涉及的每个新增行为必须补充测试矩阵。涉及线程、插件、source generator、build、UI dispatcher、连接或状态的行为必须增加对应专项测试。

## 完成标准

- 设计决策能回答对象由谁创建、谁持有、谁释放。
- API contract、失败行为、诊断和测试矩阵一致。
- 不出现业务领域假设。
- 不引入 `运行时 Host 不依赖 Templates` 等禁止依赖。

## 既有细化设计内容

以下内容保留上一轮设计中的专题细节。后续修改必须与本页上方合同、Feature ID、API 行为、诊断和测试矩阵保持一致。

## 本地化模板设计

适用范围：语言资源目录、资源 key、懒加载语言包、assembly 语言包、`.locpack` 和本地化测试

### 1. 目标

本地化模板用于生成符合 Localization 模块约定的资源结构。

设计目标：

- 支持按当前 culture 懒加载语言包。
- 支持插件资源撤销。
- 支持 assembly 语言包和 `.locpack`。
- 默认生成资源测试。

### 2. 默认结构

```text
Localization/
  en-US/
    Resources.resx
  zh-CN/
    Resources.resx
```

插件模板可生成：

```text
atomui-city/locales/
  en-US/
  zh-CN/
```

### 3. Resource Key

规则：

- 展示文本使用 resource key。
- 插件 display name 和 description 使用 resource key。
- 路由 title 使用 resource key。
- validation error 使用 resource key。
- 不在模板中硬编码多语言展示文本到业务代码。

### 4. 懒加载

模板必须生成可被 Localization manifest 识别的结构。

规则：

- 启动期只加载 manifest。
- 当前 culture 的语言包按需加载。
- culture 切换时触发资源刷新。
- 插件卸载时资源可撤销。

### 5. AOT

Native AOT 模式下：

- 支持 `.locpack`。
- 不默认依赖动态 assembly loading。
- 生成的资源索引必须 AOT 友好。

### 6. 测试矩阵

| 功能点 | 测试类型 | 必测场景 |
|---|---|---|
| 资源目录 | Unit/Build | culture 目录生成。 |
| resource key | Unit | key 存在，不硬编码展示文本。 |
| manifest | Build | localization manifest 生成。 |
| lazy load | Unit | 当前 culture 包加载。 |
| fallback | Unit | 缺失 key fallback。 |
| plugin unload | Plugin test | 资源撤销。 |
