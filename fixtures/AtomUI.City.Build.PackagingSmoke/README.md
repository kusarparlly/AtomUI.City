# AtomUI.City.Build Packaging Smoke

该 fixture 只消费本地生成的 `AtomUI.City.Build` NuGet 包，用于验证包内 `buildTransitive`、Tasks、插件布局和应用发布 manifest 的真实消费链。先将 Build 包输出到 `output/NuGet/Debug`，再分别执行：

```bash
dotnet pack fixtures/AtomUI.City.Build.PackagingSmoke/PluginSmoke/PluginSmoke.csproj -c Debug
dotnet publish fixtures/AtomUI.City.Build.PackagingSmoke/ApplicationSmoke/ApplicationSmoke.csproj -c Debug
```

它不引用 Build 或 Tasks 的源码工程，避免项目引用掩盖 NuGet 资产缺失问题。
