# 访问数据与设计韧性

Data 模块为桌面应用提供统一的请求管线、连接、认证、缓存、韧性和流式传输边界。它不要求所有远程调用使用同一种协议；HTTP、gRPC、SignalR 或 IPC 可以共享上层错误和生命周期模型。

## 接入 Host

```csharp
builder.ConfigureServices(services =>
{
    services.AddData();
    services.AddHttpClient("inventory", client =>
        client.BaseAddress = new Uri("https://inventory.example"));
});

builder.UseModule<DataModule>();
```

协议 client、credential provider 和业务 handler 仍由应用按需要注册。不要在 ViewModel 中直接创建 `HttpClient` 或长期连接。

## 一次请求的边界

```text
业务请求
-> canonical identity
-> authentication
-> cache policy
-> timeout / retry / circuit breaker / rate limit
-> transport
-> response mapping
-> cache invalidation
-> 可观察结果
```

每一层都观察同一个调用取消语义，但不能把 timeout、用户取消和远端失败混为一个错误。

## 重试前先判断是否安全

- 查询通常可以重试，但仍应限制次数和总时长；
- mutation 只有在具备幂等 key 或服务端去重时才能自动重试；
- 收到部分响应后不能假设服务端没有提交；
- authentication refresh 应单飞，避免并发请求触发刷新风暴；
- circuit breaker 打开时返回明确失败，不把请求无限排队。

## 长连接和流

连接应绑定 application、account 或 window 等明确 owner。停止 owner 时先阻止新工作，再取消读写、等待 in-flight 操作并释放 transport。流式 payload 使用有界 buffer 和背压；不能把无限数据累计到内存后一次返回。

## 缓存和一致性

cache key 必须包含会改变响应语义的租户、身份、culture 和参数。mutation 成功后按文档化规则失效；离线数据要携带版本或新鲜度信息，不能伪装成在线最新结果。

## 观察与测试

分别记录逻辑请求和物理尝试次数，避免把三次 retry 误报成三次用户操作。测试至少覆盖 timeout、503、半响应、断连、重连、乱序、取消和幂等 mutation。

可运行的多协议与故障注入参考见 [Desktop Dogfood Data](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/Data/) 和 [传输韧性说明](../../../fixtures/AtomUI.City.Fixtures.DesktopDogfood/DATA-TRANSPORT-RESILIENCE.md)。
