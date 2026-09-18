# CLI 与模板

CLI 面向人和自动化脚本创建、生成、构建与检查 City 工程；Templates 负责把输入转换为可审查、create-only 的文件变更。

## 创建和增量生成

```text
atomui city new app Inventory
atomui city generate module Orders
atomui city generate page OrderDetails --route orders/{id:guid}
atomui city generate test Orders
atomui city generate config RemoteApi
atomui city generate localization Orders
```

先查看生成计划，再提交到版本控制。增量生成默认不覆盖已有文件；发生目标冲突时应失败并报告，而不是猜测如何合并开发者代码。

## 在 CI 中调用

CI 使用 non-interactive 模式和结构化 JSON 输出，按 `CliExitCodes` 判断成功、普通失败或参数错误。不要解析彩色控制台文本，也不要依赖内部 CLR envelope 类型。

CLI 启动外部 `dotnet` 时应继承明确的工作目录和环境，并把外部退出码、标准输出和标准错误映射到稳定结果。超时或取消后还要等待子进程终止，不能遗留后台编译进程。

## 在代码中使用模板计划

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

`CreatePlan` 用于审查路径和内容；`Render` 才执行写入。同一 output root 的 Render 在进程内串行，取消或 IO 失败会回滚本轮已经创建的文件。

## 生成后的责任

生成代码只是起点。开发者仍需：

1. 确认 Module owner 和依赖方向；
2. 补充业务实现和测试；
3. 运行格式化、build 和适用门禁；
4. 审查是否意外暴露 public API；
5. 将生成结果作为普通源代码提交。

命令和退出码合同见 [CLI API contracts](../../modules/cli/api-contracts.md)，模板行为见 [Templates API contracts](../../modules/templates/api-contracts.md)。
