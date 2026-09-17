# AtomUI.City 开发者 API 文档

版本：第一版  
成熟度：Preview  
适用范围：除 `AtomUI.City.PluginSystem` 外的当前公开 API

本目录是面向应用开发者的 API 入口。它说明应该引用哪个包、从哪个类型开始、对象由谁创建和释放，以及失败、取消和线程行为。模块设计文档仍是行为合同，`PublicAPI.Shipped.txt` 与 `PublicAPI.Unshipped.txt` 是编译器门禁使用的精确 CLR 签名基线。

## 从这里开始

1. [安装与快速开始](getting-started.md)
2. [Core：Host、Module、DI 与生命周期](core.md)
3. [EventBus：发布、订阅与队列](eventbus.md)
4. [应用模型：Routing、State、MVVM 与 Presentation](application-model.md)
5. [应用服务：Data、Security 与 Localization](application-services.md)
6. [Testing：确定性测试工具](testing.md)
7. [工程工具：Build、Generators、CLI 与 Templates](tooling.md)
8. [兼容性、版本与 API 冻结](compatibility.md)

## 包目录

| 包 | 开发者用途 | 首要入口 | 状态 |
| --- | --- | --- | --- |
| `AtomUI.City.Core` | Host、Module、DI、生命周期、诊断、UI dispatcher 抽象 | `ApplicationHost`、`ModuleBase` | Preview-frozen |
| `AtomUI.City.EventBus` | 进程内强类型事件、队列、订阅、背压和观测 | `EventBusModule`、`IEventPublisher`、`IEventSubscriber` | Preview-frozen |
| `AtomUI.City.Routing` | 路由图、匹配、导航事务、guard/resolver/middleware | `RoutingModule`、`IRouter` | Preview |
| `AtomUI.City.State` | 状态定义、computed state、collection、scope 和持久化边界 | `AddState`、`IStateFactory`、`IStateRegistry` | Preview |
| `AtomUI.City.Mvvm` | ViewModel 激活、命令、验证和消息 | `ViewModelBase`、`CommandGroup` | Preview |
| `AtomUI.City.Presentation` | View 映射、窗口、outlet、interaction 和资源 | `PresentationModule`、`IPresentationRuntime` | Preview-frozen |
| `AtomUI.City.Data` | 请求管线、连接、认证、缓存、韧性和流式传输 | `DataModule`、Data client APIs | Preview |
| `AtomUI.City.Security` | principal、权限、授权策略和安全诊断 | `AddSecurity`、`IAuthorizationEvaluator` | Preview |
| `AtomUI.City.Localization` | culture、资源包、作用域和本地化文本 | `AddLocalization`、`ILocalizationService` | Preview |
| `AtomUI.City.Testing` | TestHost、fake dispatcher、虚拟调度器和测试替身 | `TestHost`、`ModuleTestHost` | Preview |
| `AtomUI.City.Build` | 通过 NuGet build assets 接入生成和验证 | MSBuild properties/items | Preview |
| `AtomUI.City.Generators` | 编译期生成服务、模块、事件、路由等清单 | Roslyn 自动入口 | Preview |
| `AtomUI.City.Cli` | 创建、生成、构建、检查 City 工程 | `atomui city ...`、`CliApplication` | Preview |
| `AtomUI.City.Templates` | 应用及增量代码模板 | `ApplicationTemplateRenderer`、`GenerationTemplateRenderer` | Preview |
| `AtomUI.City.PluginSystem` | 动态插件 | 不在第一版 API 文档支持范围 | Deferred |

## 稳定性说明

“Preview-frozen”表示签名已经进入机械门禁，未经 API review 不能漂移；它不等于已经对外发布的 Stable 1.x 承诺。“Preview”同样允许首次发布前的受控调整。开发者不应依赖：

- `internal` 类型、反射发现的实现类型或生成器内部模型；
- 文档未声明的初始化顺序和线程行为；
- `PluginSystem` 当前实现；
- 仅为测试程序集提供的 `InternalsVisibleTo` 入口。

## 精确签名在哪里

每个源码项目包含：

- `PublicAPI.Shipped.txt`：已经随版本发布的签名；
- `PublicAPI.Unshipped.txt`：已登记但尚未随稳定版本发布的签名。

这两个文件用于兼容性检查，不建议作为学习材料。开发者应先阅读本目录，再按页面链接进入 `docs/modules/<module>/api-contracts.md` 查看完整异常、取消、并发和诊断合同。
