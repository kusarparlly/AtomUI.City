# 兼容性、版本与 API 冻结

## 当前承诺

第一版 API 文档对应首次公开发布前的 Preview surface。签名已由 PublicApiAnalyzers 和项目基线保护，但仍可能在正式 Stable 版本前经过带文档、测试和迁移说明的 review 后调整。

| 标记 | 含义 |
| --- | --- |
| Preview | 可供开发和 dogfood，首次稳定发布前允许受控调整 |
| Preview-frozen | 已进入强机械门禁，任何漂移必须先完成 API review |
| Stable | 随稳定版本发布并遵守 1.x 二进制和行为兼容承诺 |
| Deferred | 当前版本不承诺可用性，例如 PluginSystem |

## 什么属于 API

兼容面不只包括 C# `public`：

- public 类型、成员、参数默认值、枚举值和 attribute；
- NuGet 包 ID、依赖组和资产布局；
- generated source、manifest version 和 registrar 协议；
- MSBuild property、item、target 和输出资产名；
- CLI 命令、exit code 和 JSON schema；
- Templates 的变量、文件布局和生成内容；
- diagnostics code、context 字段和 failure mapping。

## Breaking change 示例

- 删除或重命名公开成员；
- 把同步失败改成静默忽略；
- 改变取消后是否提交状态；
- 改变 Dispose/Stop 的幂等与等待语义；
- 改变事件、路由或状态 identity 比较规则；
- 改变 JSON、manifest、MSBuild 或模板中的稳定字段；
- 让 Native AOT 路径重新依赖运行时程序集扫描。

## 开发者迁移原则

- 只使用本目录、模块 API contracts 和 XML documentation 中声明的入口；
- 不反射 `internal` 类型或依赖生成类型的实现细节；
- 升级 Preview 包时阅读模块 compatibility 文档；
- 对 Host 启停、取消、Dispose、并发和 Native AOT 保留 contract tests；
- 第三方库公开 City 类型时，应同步评估自己的二进制兼容承诺。

## 维护者门禁

公开面修改必须同步：

1. 设计文档与本 API 文档；
2. `PublicAPI.Shipped.txt` 或 `PublicAPI.Unshipped.txt`；
3. XML documentation；
4. 单元、contract、集成或 dogfood 测试；
5. 必要的迁移说明。

完整治理规则：[公共 API Review 门禁](../engineering/public-api-review.md)。
