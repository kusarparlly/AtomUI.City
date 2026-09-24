# City Learning Workbench

这是一个真正的 Avalonia 桌面应用，而不是框架内部测试器。它假装自己是普通业务项目，通过项目引用“安装”正在开发的 City.Core，并使用 Core 完成：

- ApplicationHost 的 Build、Start、Stop、Dispose；
- ApplicationModule 与生成式 DI 注册；
- Avalonia 桌面生命周期和 City Host 生命周期的衔接；
- 任务新增、完成和 JSON 本地持久化；
- 不依赖 City.Mvvm/Presentation，为后续逐模块演进保留空间。

从本目录运行，以便使用这里的 `global.json`：

```powershell
Push-Location docs/learning/labs
dotnet build AtomUI.City.Learning.slnx
dotnet run --project CityLearning.Workbench
dotnet test CityLearning.Workbench.Tests/CityLearning.Workbench.Tests.csproj
Pop-Location
```

数据默认保存在当前用户本地应用数据目录的 `AtomUI.City.Learning/Workbench/work-items.json`。调试或测试时可通过 `CITY_LEARNING_DATA_FILE` 环境变量改到其他位置。

## 修改边界

课程实验可以修改本目录。不要为了让实验“通过”而修改 `src/AtomUI.City.Core`；如果实验揭示真实缺陷，应回到正式文档先行流程。
