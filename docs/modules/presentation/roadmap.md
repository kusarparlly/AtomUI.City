# AtomUI.City.Presentation Roadmap

以下能力不属于 1.0 承诺：

- AtomUI 或其他控件库的独立可选适配包。
- Linux/macOS 正式 runtime 支持及对应 desktop smoke。
- Navigation View/ViewModel keep-alive cache。
- Presentation Interaction/resource descriptor generator；1.0 只生成 View registrar。
- 更丰富的 queue fairness、coalescing 或动态容量；任何演进都必须保持显式 options 和可诊断拒绝。
- 独立 Localization-Avalonia 可选适配包；不得把 culture/文案职责重新放回 Presentation 主包。
- 官方 StorageProvider 薄适配和更多平台能力 wrapper。

Roadmap 不是 API 合同，不能把候选能力标记为 Implemented/Verified。
