# 工程工具 API

## AtomUI.City.Build

Build 是 NuGet build assets 包，不是普通运行时类库。开发者引用需要构建集成的 City 包后，MSBuild 自动导入其 props/targets；无需在业务代码中调用 Build 类。

完整合同：[Build API Contracts](../modules/build/api-contracts.md)

Build 负责：

- generated manifest 和 diagnostics 资产接入；
- 项目、包与依赖边界检查；
- 插件/应用打包相关 MSBuild contract；
- 公共 API、XML 文档、AOT 和发布门禁。

只有文档列出的 MSBuild property、item、target 和输出文件名属于公共合同。

## AtomUI.City.Generators

Generators 由编译器加载。应用开发者不应直接实例化 generator 内部 reader、manifest builder 或 model。

完整合同：[Generators API Contracts](../modules/generators/api-contracts.md)

当前生成领域包括模块、服务注册、EventBus、Routing、Presentation、Localization 和 Data client catalog。生成代码中的 registrar 名称与签名属于 Host/Build 连接协议，应用不应手工调用或复制这些方法。

## AtomUI.City.Cli

CLI 的主要边界是进程命令：

```text
atomui city new app <AppName>
atomui city generate module <Name>
atomui city generate page <Name> --route <Path>
atomui city generate test <Name>
atomui city generate config <Name>
atomui city generate localization <Name>
atomui city build
atomui city test
```

完整合同：[CLI API Contracts](../modules/cli/api-contracts.md)

可引用的 CLR API 只有：

| API | 用途 |
| --- | --- |
| `CliApplication.RunAsync` | 在当前进程执行 CLI，并将输出写入调用方 `TextWriter` |
| `CliExecutionEnvironment` | 指定工作目录、CI、非交互和 stdin 状态 |
| `CliExitCodes` | Success、Failure、ArgumentError |

`CliEnvelope`、`CliDiagnostic` 和 `DotnetInvocation` 是内部 CLR 类型，但它们产生的 JSON 字段仍是兼容性合同。自动化调用应解析 JSON，而不是反射内部类型。

## AtomUI.City.Templates

完整合同：[Templates API Contracts](../modules/templates/api-contracts.md)

| API | 用途 |
| --- | --- |
| `ApplicationTemplateOptions` | 应用名、命名空间、目标框架和功能选项 |
| `ApplicationTemplateRenderer` | 创建 plan 或渲染完整应用 |
| `GenerationTemplateOptions` | module/page/test/config/localization 增量输入 |
| `GenerationTemplateKind` | 增量模板种类 |
| `GenerationTemplateRenderer` | create-only 增量生成事务 |
| `TemplatePlan` / `TemplateChange` | 可审查、可验证的变更计划 |
| `TemplateRenderResult` / `TemplateDiagnostic` | 成功、失败、应用路径和诊断 |

```csharp
var renderer = new GenerationTemplateRenderer();
var options = new GenerationTemplateOptions
{
    Kind = GenerationTemplateKind.Module,
    Name = "Orders",
    ProjectName = "Contoso.Inventory",
    RootNamespace = "Contoso.Inventory",
    OutputPath = projectDirectory,
};

var plan = renderer.CreatePlan(options); // 不写文件
var result = renderer.Render(options, cancellationToken);
```

同一 output root 的 Render 在进程内串行。目标冲突不覆盖现有文件；取消或 IO 失败会回滚本次已创建的文件。
