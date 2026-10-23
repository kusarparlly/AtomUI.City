# 工程文档

本目录维护 AtomUI.City 仓库工程化文档。

强制规范：

- [documentation-governance.md](documentation-governance.md)：文档先行治理规范。所有模块必须有完善文档后才能开始写代码，任何改动必须先完成文档对齐并确认。
- [implementation-roadmap.md](implementation-roadmap.md)：实现顺序治理规范。约束从设计文档进入代码实现的阶段顺序、验收标准、测试门禁和暂停条件，不记录完成度。

工程规范：

- [repository-structure.md](repository-structure.md)：仓库目录、源码项目、测试项目、引用项目说明。
- [build-system.md](build-system.md)：MSBuild props/targets、输出目录、构建约定。
- [coding-style.md](coding-style.md)：代码风格、Rider/ReSharper 设置、换行符和格式化规则。
- [module-documentation-standard.md](module-documentation-standard.md)：模块文档分级、功能规格、API 合同、测试矩阵和兼容性文档标准。
- [public-api-review.md](public-api-review.md)：公共 API review 门禁，约束 public API、manifest、generated output、CLI envelope、模板变量和插件 contract。
- [code-documentation-alignment-audit.md](code-documentation-alignment-audit.md)：以当前源码为基线，对 PluginSystem 以外各模块的 Feature、Public API、测试与设计文档执行后置对齐审计。
- [native-aot-module-audit.md](native-aot-module-audit.md)：逐模块审计运行时闭包、AOT/trimming analyzer、双 TFM 原生发布运行和本地候选包消费，并记录 NativeAOT 阻断项。
- [product-vertical-slice.md](product-vertical-slice.md)：首个产品级端到端闭环验收规格。
- [dependency-management.md](dependency-management.md)：NuGet 版本集中管理、升级流程和兼容性验证。
- [versioning.md](versioning.md)：版本号、包版本、模板版本策略。
- [packaging.md](packaging.md)：NuGet 包、模板包、插件包打包规则。
- [release-process.md](release-process.md)：发布流程和发布前验证。
