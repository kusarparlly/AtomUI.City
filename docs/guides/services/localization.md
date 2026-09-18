# 本地化应用

Localization 管理 culture、资源包、fallback 和变更通知。它不是简单的全局字典：资源有 owner、版本和撤销边界，窗口或业务 scope 也可以选择不同 culture。

## 注册与默认 culture

```csharp
builder.ConfigureServices(services =>
    services.AddLocalization(options =>
    {
        options.DefaultCulture = CultureInfo.GetCultureInfo("en-US");
        options.DefaultUICulture = CultureInfo.GetCultureInfo("en-US");
        AppLocalizationCatalog.AddTo(options);
    }));
```

资源包可以来自生成清单、程序集或应用明确配置的文件来源。Native AOT 应优先使用生成清单，不做运行时无界程序集扫描。

## 切换并读取文本

```csharp
var localization = services.GetRequiredService<ILocalizationService>();

await localization.SetCultureAsync("zh-CN", cancellationToken);
var title = await localization.GetStringAsync(
    "Orders.Title",
    cancellationToken);

var current = localization.CultureState.Value;
```

切换完成后发布新的只读 culture snapshot。旧 snapshot 仍然是有效历史值，但不会跟随后续切换改变。

## 设计资源 key

- 使用稳定业务命名空间，例如 `Orders.Editor.Save`；
- 不用英文原文充当 key；
- 区分缺失 key、缺失 culture 和 fallback 命中；
- 格式化参数名称和类型属于资源合同；
- 用户可见文本不应散落在 ViewModel、异常和 diagnostics 中。

## UI 更新

ViewModel 或 Presentation 订阅 culture state，在 UI dispatcher 上重新投影可见文本。不要销毁并重建整个应用来切换语言，也不要让后台线程直接修改控件。

资源包撤销会产生新快照；正在使用被撤销 owner 资源的视图应回退到其他 package 或明确显示缺失状态，不能继续持有可卸载程序集中的对象。

中英文 package、交叉切换和 UI 投影示例见 [Desktop Dogfood Localization](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Localization/)。
