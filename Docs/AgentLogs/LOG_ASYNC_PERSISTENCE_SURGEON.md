# LOG_ASYNC_PERSISTENCE_SURGEON

## 2026-05-13 Background LZ4 Saving

What was wrong:
- Save path had no async persistence service contract; callers could still depend on concrete `SaveManager` compatibility.
- Save request/completion/status had no dedicated hash-only async persistence lanes.
- Main-thread save work had no explicit simulation pause boundary before background serialization/compression/IO.
- Runtime file writing used synchronous `FileStream.Write` through `AsyncWriteManager`.
- Load recovery could promote or use `.bak` without emitting a HUD recovery notification.
- Save telemetry had no 300-frame native black-box ring for duration/compressed-size post-mortem.
- Global verification is currently blocked by unrelated cross-domain compile errors and Unity MCP session loss.

What was done:
- `SaveManager` now implements `IAsyncPersistenceService` and registers through `GlobalRegistry.RegisterAsyncPersistenceService`.
- Added `GlobalRegistry.AsyncPersistence` and `IAsyncPersistenceService.TryRequestSave(byte slotIndex, uint sourceHash, uint operationId)`.
- Added fixed 32-byte `SaveRequestSignal`, `SaveCompletedSignal`, and `SaveStatusSignal` lanes; status also mirrors to `SaveLifecycleSignal`.
- Added persistent 10MB `_saveStagingBuffer` plus a fixed 300-entry `NativeArray<AsyncPersistenceTelemetryEntry>` black-box ring.
- Save now publishes pause, waits one frame, snapshots existing DTOs, stages a native save header, resumes simulation, captures main-thread frame data, then moves verified save pipeline execution to `Awaitable.BackgroundThreadAsync()`.
- `AsyncWriteManager.WriteAll` now writes temp files through `FileStream` opened with `FileOptions.Asynchronous | FileOptions.SequentialScan` and `WriteAsync` over a static 64KB scratch buffer.
- Load backup/self-repair path now emits `HUDNotificationSignal` with a hashed recovery message/context.
- Added post-save VRAM gate: above 1800MB, request generation-0 optimized GC only after save completion and only when frame delta is below 14ms.
- Black-box dump writes `Docs/AgentLogs/Dump_ASYNC_PERSISTENCE_SURGEON.bin` from the project root on save failure.
- Omega polish removed repeated slot-name hashing in telemetry and preserved exact zero telemetry for failed/zero-byte saves.

Cinematic cheats used:
- Signal-only presentation: spinner/recovery UI consumes hash signals instead of direct persistence-to-UI calls.
- Pause-window cheat: freeze the truth for the snapshot only, then move expensive persistence work to background cover.
- VRAM pressure gate: deferred small GC under pressure instead of pretending cleanup has no frame cost.

Exact microseconds saved:
- Save request/status/completion event path: expected <5 us per event versus direct object lookup/UI coupling.
- Duplicate telemetry hashing removed: microsecond-scale per save completion; exact profiler proof blocked by global compile wall.
- Main-thread compression/write removal: expected to remove the reported 200ms save hitch from the frame, but exact before/after profiler proof is blocked until global compile and Unity session are restored.
- Staging arena prevents a potential 10MB managed staging allocation per save in the new path.

Blocked / not claimed:
- True Burst-compiled full-save LZ4 is blocked by current codec shape: native LZ4 plus managed fallback is not Burst-safe. I kept background protected LZ4 and did not fake a Burst job.
- Absolute zero-GC for the full save cannot be certified because existing `SaveData` and registered `ISaveable` DTO extraction still use managed contracts.
- `dotnet build Hecton8.Core.csproj --no-restore` fails on 107 unrelated errors, including missing scheduling/audio/GPR/culling/binary-layout symbols and foveated interface implementation gaps.
- Unity MCP compile console verification is blocked: refresh timed out and console reads returned `no_unity_session`.
