# 0022 AOT-safe SignalR JSON Contract

状态：Accepted
日期：2026-10-23

## 背景

`SignalRRealtimeConnection` 当前使用 SignalR 默认 `JsonHubProtocol`，没有接收应用生成的
`JsonSerializerContext` 或 `IJsonTypeInfoResolver`。普通 CoreCLR 可以依靠反射序列化开发者 DTO，
但 NativeAOT 中自定义 invocation argument、response 和 server-push message 会在真实协议调用时失败。
Analyzer clean、连接成功和只使用 primitive payload 的测试都不能证明该路径安全。

业务 DTO、JSON 命名、多态和 converter 属于应用或业务模块，City 不应通过运行时扫描猜测这些类型，
也不应重复实现 System.Text.Json source generator。

## 决策

1. 应用使用 `[JsonSerializable]` 和 `JsonSerializerContext` 为所有 SignalR 请求、返回及推送 DTO 生成静态元数据。
2. Data 提供不可变 `SignalRJsonContract`，组合一个或多个 `IJsonTypeInfoResolver`；`SignalRConnectionOptions` 必须显式携带该合同。
3. City 把合同安装到 SignalR `AddJsonProtocol`，并使用同一序列化配置在 `InvokeAsync` 和 `Subscribe` 入口校验 payload type。
4. 该合同在 CoreCLR、测试和 NativeAOT 中一律 fail-closed，不提供反射 fallback。遗漏 argument/response/message type 必须在进入网络协议前稳定失败并写入结构化诊断。
5. 直接接收任意预构造 `HubConnection` 的公开构造入口不再作为支持合同；高级连接配置通过受控 builder callback 完成，JSON 协议最终由 City 安装。
6. City Generator 不生成 JSON serializer。已有 Data descriptor 可以继续描述 operation type，但实际 JSON 元数据始终由应用提供。

## 影响

- SignalR 调用方必须定义业务 DTO，并把对应 source-generated context 交给 City。
- 多个业务模块可以组合 resolver；解析顺序确定，创建连接后配置冻结。
- 缺少元数据不再表现为底层 `InvalidOperationException` 或模糊 transport failure，而是稳定的 Data serialization failure、typed exception 和 diagnostic。
- 现有使用 `SignalRRealtimeConnection.Create` 的 fixture、dogfood 和应用需要迁移到显式 JSON contract。

## 后续约束

- 真实 NativeAOT 门禁必须执行自定义 DTO 请求、返回和 server push，不能只验证连接或 primitive payload。
- 新增 SignalR payload type 时必须同步更新应用 context 和测试。
- 动态 JSON 使用 `JsonElement`、`JsonDocument` 或明确 converter，不允许用未知 `object` 恢复反射发现。
- Windows `net8.0` 与 `net10.0` 都必须 publish 并运行同一协议场景。

## 实施状态

已完成。公开合同、`AUCDATA036`、调用方迁移和专项测试已落地；真实 HTTP/gRPC/SignalR 原生客户端在 `net8.0/win-x64`、`net10.0/win-x64` 均通过，自定义 DTO invocation、response 与 server push 未使用反射 fallback。
