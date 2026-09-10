# AtomUI.City.Presentation Diagnostics

诊断码语义稳定且不得复用。message 可以改进；测试至少断言 code 和一个定位字段。

## Runtime、View 和 Outlet

| Code | 名称 | Severity | 关键 Context |
| --- | --- | --- | --- |
| AUCPRS001 | RuntimeReady | Info | scopeId |
| AUCPRS002 | RuntimeStopping | Info | scopeId |
| AUCPRS003 | DispatcherOperationRejected | Warning | operationId, targetAction, thread ids, error |
| AUCPRS004 | DispatcherCallbackFailed | Error | operationId, targetAction, thread ids, error |
| AUCPRS005 | ViewLocatorMatched | Info | viewModelType, viewType, viewKey, owner |
| AUCPRS006 | ViewLocatorFailed | Warning | viewModelType, viewKey, routeId, owner |
| AUCPRS007 | ViewCreated | Info | view types, constructor parameters, elapsed |
| AUCPRS008 | ViewCreationFailed | Error | view types, elapsed, error |
| AUCPRS009 | ViewBound | Info | view types, viewKey, elapsed |
| AUCPRS010 | ViewBindingFailed | Error | view types, viewKey, elapsed, error |
| AUCPRS011 | OutletCommitPlanned | Info | operationId, outlet, operation, route, stage |
| AUCPRS012 | OutletCommitSucceeded | Info | operationId, outlet, operation, route, stage |
| AUCPRS013 | OutletCommitFailed | Error | operationId, outlet, operation, route, stage, error |
| AUCPRS014 | VisualLifecycleAdapterExecuted | Info | Window/Outlet/Operation/Entry/View identity, eventKind |
| AUCPRS015 | VisualLifecycleAdapterFailed | Error | identity, eventKind, error |
| AUCPRS016 | ResourceDictionaryRevoked | Info | pluginId, contributionId, target/failure count |
| AUCPRS017 | ResourceDictionaryRevokeFailed | Error | pluginId, contributionId, target/failure count, error |
| AUCPRS018 | RetiredBefore1.0 | Reserved | 原 culture resource apply 语义，禁止复用 |
| AUCPRS019 | RetiredBefore1.0 | Reserved | 原 culture resource apply failure 语义，禁止复用 |

## Interaction、Validation、Command 和 Plugin

| Code | 名称 | Severity | 关键 Context |
| --- | --- | --- | --- |
| AUCPRS020 | InteractionHandled | Info | request/result type, status, owner |
| AUCPRS021 | InteractionNotHandled | Warning | request/result type, status |
| AUCPRS022 | InteractionFailed | Error | request/result type, owner, error |
| AUCPRS023 | InteractionHandlerRevoked | Info | request/result type, plugin/contribution |
| AUCPRS024 | ValidationVisualStateApplied | Info | status, keys, messageCount, targetType |
| AUCPRS025 | ValidationVisualStateApplyFailed | Error | status, keys, targetType, error |
| AUCPRS026 | CommandStateApplied | Info | canExecute, isExecuting |
| AUCPRS027 | CommandStateApplyFailed | Error | error |
| AUCPRS028 | ResourceContributionRegistered | Info | kind, plugin, contribution, resourceType |
| AUCPRS029 | ResourceContributionRevoked | Info | kind, plugin, contribution, resourceType |
| AUCPRS030 | ResourceContributionRevokeFailed | Error | owner, resourceType, error |
| AUCPRS031 | PluginViewTracked | Info | owner, outlet, view types |
| AUCPRS032 | PluginViewClosed | Info | owner, outlet, view types |
| AUCPRS033 | PluginViewCloseFailed | Error | owner, outlet, error |
| AUCPRS034 | PluginUnloadCleanupCompleted | Info | owner and revoke counts |
| AUCPRS035 | PluginUnloadCleanupFailed | Error | owner, counts, errorKinds |

## 工业化故障与背压

| Code | 名称 | Severity | 关键 Context |
| --- | --- | --- | --- |
| AUCPRS036 | OutletRollbackFailed | Error | operationId, outlet, stage, error |
| AUCPRS037 | PresentationEntryCleanupFailed | Error | operationId, outlet, error |
| AUCPRS038 | FailurePresenterFailed | Error | operationId, outlet, stage, error |
| AUCPRS039 | WindowCleanupFailed | Error | windowId, closeOrigin, error |
| AUCPRS040 | OutletQueueRejected | Warning | operationId, outlet, capacity, pending, rejected |
| AUCPRS041 | InteractionQueueRejected | Warning | request/result type, windowId, capacity, pending, rejected |
| AUCPRS042 | CandidateOwnershipViolation | Error | operationId, outlet, candidate state/owner, error |
| AUCPRS043 | MultipleCloseConfirmations | Error | windowId, confirmationCount, viewModelTypes |

## 演进规则

`AUCPRS001-017`、`AUCPRS020-043` 是 1.0 active 集合；018/019 永久 reserved。新增 code 必须同步源码、Feature、API card、compatibility 和断言测试。
