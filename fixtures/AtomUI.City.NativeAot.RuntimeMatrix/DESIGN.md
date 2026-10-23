# City NativeAOT Runtime Matrix

该 fixture 是 City 正式运行时模块的持久化 NativeAOT 实战验收应用。它必须像真实开发者应用一样经过 source generator、DI、Host/module 生命周期、NativeAOT publish 和原生进程运行；普通单元测试或“只引用程序集即可发布”不能替代它。

## 场景与断言

| 模块 | 公开路径 | 原生进程断言 |
| --- | --- | --- |
| Core | `ApplicationHost`、generated module catalog、module init/shutdown | 初始化与关闭各执行一次。 |
| State | `IStateRegistry`、observe、write、`ComputedState<T>` | 初始/变更通知各一次，计算值为 42。 |
| Routing | route attributes、generated manifest、matcher、DI resolver activation | generated reference 与 route id 一致，`orders/42` 提取参数并由运行时 DI resolver 提交 resolved data。 |
| Security | `AddSecurity`、permission registry/checker、`FileCredentialStore` | 已认证权限 Allowed，匿名主体 Challenge；credential 完成保存、读取、删除且 source-generated JSON 可用。 |
| Mvvm | `ViewModelBase` 通知、`ValidatingViewModelBase`、generated DataAnnotations | Required/Range 产生并清除错误，无反射验证。 |
| Localization | generated embedded descriptor、known `SourceAssembly`、file locpack、in-memory descriptor | 三类 provider 都切换 culture 并得到预期文本；generated descriptor 直接持有静态程序集并由 assembly provider 加载，无路径 DLL 加载。 |

Core、EventBus、Data 与 Presentation 的现有专项 NativeAOT fixture 继续提供各自更深的边界覆盖；本应用不复制那些重型矩阵。PluginSystem 在正式建设前不纳入通过声明。

## 固定矩阵

Windows 发布门禁执行：

- `net8.0 / win-x64`
- `net10.0 / win-x64`

每个组合必须满足：严格 analyzer build 成功；NativeAOT publish 无未处理 `IL2xxx`、`IL3xxx` 或 `will always throw`；原生 EXE 退出码为 0；最后一行 JSON 中所有业务断言为真且计数精确。Windows 结果不得外推到 Linux 或 macOS。

执行入口：

```powershell
./engineering/check-native-aot-runtime-matrix.ps1
```
