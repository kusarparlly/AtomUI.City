# AtomUI.City 用户指南

本目录是 AtomUI.City 的任务型使用手册。它回答“怎样用 City 完成一个应用”，而不是逐项罗列类型和方法。精确签名、异常和兼容性承诺请查阅 [开发者 API 文档](../API/README.md)；框架内部原理请查阅 `docs/modules/`。

当前指南以仓库中的生产代码和可运行 fixtures 为事实来源，适用于首次公开发布前的 Preview 版本。尚未进入当前版本建设范围的 PluginSystem 不在本指南内。

## 推荐阅读路线

| 阶段 | 指南 | 完成后能够 |
| --- | --- | --- |
| 1 | [从零启动应用](getting-started.md) | 构建并正确启动、停止最小 City Host |
| 2 | [组织应用与模块](application-structure.md) | 按业务边界拆分 Module、Service 和组合根 |
| 3 | [Core 专题](core/overview.md) | 使用配置、DI、生命周期与诊断 |
| 4 | [发布和订阅事件](eventbus/publish-subscribe.md) | 建立有所有权、可释放、有背压的进程内事件流 |
| 5 | [定义路由并导航](routing/navigation.md) | 使用生成路由、参数、历史和导航结果 |
| 6 | [管理应用状态](state/manage-state.md) | 使用 writable、computed、collection 和 scoped state |
| 7 | [编写 ViewModel 与交互](desktop/viewmodels-and-interactions.md) | 把加载、命令和对话框绑定到激活生命周期 |
| 8 | [访问数据与设计韧性](services/data.md) | 建立有取消、错误、重试和流式边界的数据调用 |
| 9 | [认证与授权](services/security.md) | 管理身份快照、permission 和 policy |
| 10 | [本地化应用](services/localization.md) | 配置 culture、资源包和 UI 更新 |
| 11 | [组合桌面应用](desktop/compose-application.md) | 把 Core、Routing、State、EventBus 和 Presentation 接入同一 Host |
| 12 | [测试 City 应用](testing/test-application.md) | 用确定性时钟、dispatcher 和真实 Host 分层测试 |
| 13 | [CLI 与模板](tooling/project-tools.md) | 创建工程并执行可自动化的增量生成 |
| 14 | [构建与发布](deployment/build-and-publish.md) | 理解生成器、Build assets、AOT 与发布检查 |
| 排障 | [常见问题与排查顺序](troubleshooting.md) | 从 Build、Start、运行期和 Stop 四个边界定位故障 |

## 按应用形态选择入口

### 命令行或后台应用

从 [Core 快速开始](core/getting-started.md) 开始，然后阅读模块、DI、配置和测试章节。Core 自带的 `TodoCli` 示例覆盖完整的 Host 生命周期。

### 桌面应用

先完成最小 Host 与模块章节，再阅读 [组合桌面应用](desktop/compose-application.md)。桌面 dogfood 是当前最完整的跨模块证据，但它是验收工程，不是建议直接复制的应用模板。

### 框架贡献者

先阅读本指南理解使用者路径，再进入 [模块设计文档](../modules/overview.md) 和 [公共 API Review 门禁](../engineering/public-api-review.md)。不要从内部实现类型推导面向用户的使用方式。

## 示例可信度

本指南中的模式来自三类持续验证资产：

- [`docs/guides/core/samples`](core/samples/)：最小 CLI 与 Todo CLI；
- [`AtomUI.City.CoreEventBus.DogfoodApp`](../../fixtures/AtomUI.City.CoreEventBus.DogfoodApp/README.md)：Core 与 EventBus 的组合验证；
- [`AtomUI.City.Fixtures.DesktopDogfood`](../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/README.md)：桌面端多模块、跨能力、并发和释放验证。

示例代码为了说明一个任务会省略业务细节。涉及生产部署时，应同时遵守对应模块的 API contracts 和 compatibility 文档。
