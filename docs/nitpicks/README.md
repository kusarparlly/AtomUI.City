# 设计挑刺记录（Nitpicks）

本目录收录对**已落地设计**的苛刻评审：只讲证据、只打分、只给可执行的补强。

## 定位

- 只收已落地的设计（有代码、有文档），不收想法。没代码的想法去写文档，别来这。
- 每篇都是一次“红队”记录：默认立场是找茬，不是表扬。
- Nitpick 是输入，ADR 是输出。挑完必须回答：要不要提请 `docs/decisions/` 立项。

## 准入三条（缺一不收录）

1. 只收带证据的批评，感觉流不收。每条指控必须贴源码路径与行号。
2. 每篇必须有打分（10 分制）与评分拆解。
3. 每篇必须有可执行的补强建议（三选一粒度：helper / analyzer / 文档铁律等）。

## 命名规则

```text
0001-short-title.md
0002-short-title.md
```

与 ADR 共用一套编号习惯，但两个序列**独立编号**、互相引用。

## 格式模板

```markdown
# 000N 标题

- 日期：YYYY-MM-DD
- 范围：涉及的包/模块
- 结论：一句话 + 打分

## 1. 背景（核实过的事实，带出处）

## 2. 好的地方

## 3. 欠的地方（带证据）

## 4. 补强建议

## 5. 是否提请 ADR

- 提请 / 不提请，理由；若提请，给出建议的 ADR 标题。
```

## 记录列表

- [0001-context-cancellation-token-dual-design.md](0001-context-cancellation-token-dual-design.md)
- [0002-command-ui-notification-dispatch.md](0002-command-ui-notification-dispatch.md)
