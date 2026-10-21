# AtomUI.City.Routing Threading

## Rules

- Route descriptors, templates, graphs, matches and snapshots are immutable and safe for concurrent reads.
- Registry graph publication and contribution resolver maps are synchronized; diagnostic callbacks run outside Registry/lifecycle monitor locks.
- Navigation user code never runs under `_lifecycleGate`.
- One asynchronous gate serializes snapshot and journal mutation per `NavigationScope`.
- Navigation commit and receipt-based conditional restore share that gate and one monotonic commit generation; validation and checkpoint replacement occur inside the same critical section.
- Navigation diagnostics remain inside the transaction gate lifetime to preserve event ordering; a slow sink can delay queued navigation but cannot change its result.
- Cancellation callbacks are triggered outside internal locks.
- The current snapshot is published with volatile reference semantics.
- A navigation captures one graph version and never consults a newer graph for matching or commit.
- Routing does not marshal to a UI thread. Guard, resolver and middleware preserve normal async continuation behavior.
- Admission and disposal are linearized by `_lifecycleGate`: once disposal publishes disposed state, no caller can begin waiting on or acquiring primitives that disposal may release.

## Deadlock Prevention

Same-chain reentrant navigation is rejected while the owning transaction is active. The AsyncLocal marker carries an explicit active lifetime, so a delayed child task that inherited ExecutionContext can navigate after the owner finishes. Different callers may await the same serialized scope safely. `DisposeAsync` from the scope's own active execution chain throws because waiting for itself cannot complete.

`Queue` guarantees serialization, not scheduler fairness or strict FIFO ordering.

## Cancellation

`CancelPrevious` observes an already-cancelled caller token before cancelling the prior transaction, and cancellation is generation-checked so a queued older replacement cannot cancel a newer request. Scope disposal cancels both gate waiters and running user code; post-dispose admission deterministically returns `CITY-NAVIGATION-SCOPE-DISPOSED` without touching disposed synchronization primitives. Stubborn user code delays `DisposeAsync` by design.

`TryRestoreAsync` observes caller cancellation while waiting for admission, but once it owns the gate the restore is a short non-user-code atomic operation. It never invokes guards, resolvers or middleware. A late recovery request sees the newer generation and returns `CITY-NAVIGATION-RESTORE-SUPERSEDED` instead of rolling back newer work.
