# AtomUI.City.Templates Testing

## 测试原则

- 每个 Feature ID 至少有 Unit 或 Contract 测试。
- 集成测试不能替代单元测试。
- 生命周期、线程、插件、订阅、连接、dispatcher、source generator、build 和 template 行为必须有专项测试。
- 诊断码必须断言 code 和关键 context。
- 释放、取消、unload、Dispose 后行为必须有断言。

## 产品级测试门禁

| 必须证明的行为 | 最低测试要求 |
| --- | --- |
| 生成项目必须 restore、build 和 test。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 模板变量必须校验，非法值不写文件。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| 输出不得包含机器绝对路径。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |
| dry-run 只生成 TemplatePlan，不写文件。 | 必须有实现、测试或工程门禁证据，不能只断言流程成功。 |

## 测试矩阵

| Feature ID | Test Type | Test File | Required Assertions | Failure Paths | Status |
| --- | --- | --- | --- | --- | --- |
| AUC-TEMPLATES-001 | TemplateSmoke | ApplicationTemplateBuildSmokeTests | 断言生成、restore/build/test、Host start/stop、命名空间、包引用、solution、Directory.Build、Directory.Packages、docs entry、无绝对路径、冲突、回滚和并发。 | 缺少工作区文件、父级 CPM 污染、生成项目无法 restore/build/test、已有文件被覆盖、失败残留半成品或同目录并发混写必须失败。 | Completed |
| AUC-TEMPLATES-002 | TemplateSmoke | TemplatePackageLayoutTests, DotnetNewTemplateIntegrationTests | 断言 required files、路径规范化、非可移植路径、大小写重复、路径逃逸、package id、runtime assembly 和真实模板包安装。 | 路径逃逸返回 `AUCTPL1001` 或参数异常；重复 normalized path 返回 `AUCTPL1002`；非法 change type 返回 `AUCTPL1003`。 | Completed |
| AUC-TEMPLATES-003 | TemplateSmoke | ApplicationTemplateBuildSmokeTests | 断言变量默认值、非法值、命名空间生成和错误消息。 | 非法 identifier、保留字、空值、路径片段非法返回 `AUCTPL0001`；框架命名空间返回 `AUCTPL0002`；AOT/dynamic plugin 冲突返回 `AUCTPL0301`。 | Completed |
| AUC-TEMPLATES-004 | TemplateSmoke | TemplatePackageLayoutTests, DotnetNewTemplateIntegrationTests | 断言单 assembly、NuGet metadata、manifest、MSBuild 属性、测试项目、项目重命名和 PluginId/TFM 替换。 | 缺少 plugin project、manifest、module、MSBuild 属性、NuGet metadata、测试项目，或 sourceName 污染框架属性名必须失败。 | Completed |
| AUC-TEMPLATES-005 | TemplateSmoke | ApplicationTemplateBuildSmokeTests | 断言测试项目 build/test、测试矩阵、基础 smoke test、Testing 引用边界和命名规则。 | 测试项目名非法、生产项目误引用 Testing、缺少矩阵或 smoke test 失败。 | Completed |
| AUC-TEMPLATES-006 | TemplateSmoke/Build | GenerationTemplateRendererTests | 模块声明、可选依赖、服务注册、测试和 manifest 输入。 | 非法名称、冲突、取消、IO 失败和回滚。 | Completed |
| AUC-TEMPLATES-007 | TemplateSmoke/Build | GenerationTemplateRendererTests | View/ViewModel、route、Presentation binding 和测试。 | 缺少/非法 route、冲突和生成输出无法编译。 | Completed |
| AUC-TEMPLATES-008 | TemplateSmoke/Generator | GenerationTemplateRendererTests | culture 资源、LanguagePackage attribute、key 和 fallback。 | 非法/重复 culture、冲突和生成输出无法编译。 | Completed |
| AUC-TEMPLATES-009 | TemplateSmoke | GenerationTemplateRendererTests | Options、validation、reload policy 和测试。 | 非法名称、冲突和生成输出无法测试。 | Completed |
| AUC-TEMPLATES-010 | TemplateSmoke/FrameworkIntegration/Platform | ApplicationTemplateBuildSmokeTests, DotnetNewTemplateIntegrationTests, ApplicationTemplateDesktopProcessTests, generated ApplicationSmokeTests | 两个入口输出真实 App/XAML/主窗口；生成项目 restore/build/test；Headless attach Presentation 并注册 DI 主窗口；Windows 创建原生窗口、响应关闭、Host cleanup 后零退出。 | 非 classic lifetime、缺失/重复 Host 或 bootstrap 失败不得显示半初始化窗口；UI loop 退出后的 Host stop/dispose 不得依赖已停止 dispatcher。 | Completed |

## 缺口处理

如果现有测试只覆盖 smoke 或 happy path，[全局 1.0 进度](../../superpowers/plans/2026-06-11-development-tracking-plan.md) 必须把缺口标为 `Required`。无法单元测试的功能必须提供 Contract、RuntimeLifecycle、PluginLifecycle、Generator、Build、PlatformIntegration、TemplateSmoke 或 Dogfood 测试替代。
