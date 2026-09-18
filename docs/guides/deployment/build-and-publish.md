# 构建与发布

City 的构建链由普通项目编译、Roslyn generators 和 NuGet/MSBuild build assets 共同完成。应用业务代码不需要调用 `AtomUI.City.Build` 中的 CLR 方法。

## 开发期构建

```powershell
dotnet restore
dotnet build -c Release
dotnet test -c Release --no-build
```

生成器错误首先表现为编译诊断。不要通过关闭 analyzer 或手工复制 generated source 绕过问题；应修复声明、owner、重复 identity 或 manifest 不匹配。

## Build assets 如何进入应用

开发者引用带构建集成的 City 包后，NuGet 自动导入其 `build`/`buildTransitive` 资产。它们负责连接生成清单、验证项目和包边界、产生约定输出。通常不需要额外写一条 `PackageReference` 来“调用 Build 模块”，除非具体包文档明确要求直接引用。

只有 [Build API contracts](../../modules/build/api-contracts.md) 中列出的 property、item、target 和资产名可以被应用工程依赖。内部 target 名不是扩展点。

## 本地候选包

正式 NuGet 发布前，使用仓库发布流程生成本地候选源，然后让示例应用像消费 NuGet 一样引用 `.nupkg`。这样可以发现 ProjectReference 会掩盖的问题：

- 包中漏文件或依赖；
- analyzer/build assets 未随包分发；
- props/targets 导入顺序错误；
- runtime/native 资产布局错误；
- XML 文档与 symbol 包缺失。

包验证记录应包含包哈希、目标框架、运行命令和退出码。

## Native AOT

```powershell
dotnet publish -c Release -r win-x64 -p:PublishAot=true
```

具体 RID 按目标平台调整。发布成功还不够，必须启动发布目录中的最终可执行文件并执行核心场景。City 的模块、服务、事件和路由发现优先依赖生成清单；应用代码仍应避免无界反射扫描、运行时动态代码和未登记的序列化类型。

## 发布前检查

1. Release build 和全部适用测试通过；
2. 文档链接、公共 API baseline 与 XML documentation 门禁通过；
3. 本地候选包被独立消费工程验证；
4. Native AOT 构建和运行验证通过；
5. 正常退出、启动失败回滚和取消退出均无资源泄漏；
6. diagnostics 不含凭据、token 或未脱敏 payload；
7. 版本、changelog 和兼容性说明一致。

框架仓库的详细发布门禁见 [运行时本地候选版验收记录](../../engineering/runtime-release-candidate.md)。
