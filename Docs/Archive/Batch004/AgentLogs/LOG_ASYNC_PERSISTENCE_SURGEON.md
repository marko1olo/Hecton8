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

## 2026-05-13 Background LZ4 Saving Re-Audit

What was wrong:
- Re-audit found `AsyncWriteManager.WriteAll` had async file flags and an async scratch buffer, but temp-file segments were still routed through the synchronous pointer writer.
- Screenshot-size telemetry used `(bytes + 1023) >> 10`, which can overflow if a future metadata provider reports a near-`int.MaxValue` byte count.
- Global compile validation remains blocked outside persistence.

What was done:
- Patched temp-save segments to call `TryWritePointerSegmentAsync`, which copies native chunks into one static 64KB scratch buffer and invokes `FileStream.WriteAsync`.
- Kept `OverwriteAll` on the existing synchronous writer because that path is repair/overwrite specific, not the background temp-save path required by the task.
- Patched screenshot kilobyte rounding to use long math before narrowing.
- Re-ran `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: still blocked by 111 unrelated cross-domain errors.
- Retried Unity MCP console read: still `no_unity_session`.

Cinematic cheats used:
- No new visual simulation. The cheat remains architectural: move expensive save IO behind background cover and communicate status through hash-only signals.

Exact microseconds saved:
- Temp-save write correction: expected 0 us added to the frame because execution is behind `Awaitable.BackgroundThreadAsync`; exact disk latency savings are hardware dependent and profiler proof is blocked.
- Screenshot telemetry overflow guard: no measurable frame gain; correctness guard only.

Blocked / not claimed:
- No claim of clean project compile. Current wall includes missing `Hecton8.Environment.Fluids`, `Hecton8.Core.Memory.Layout`, `Hecton8.Physics.CCD`, `Hecton8.Audio.Propagation`, `IGroundRadarService`, `BinaryBlittableSafe`, `SoundEmissionSignal`, `TetherFiredSignal`, `AcousticAup`, and inventory algorithm/corrosion namespaces.

## 2026-05-13 Background Thread Purity Re-Audit

What was wrong:
- `GetPersistentAbsolutePath` could indirectly touch `Application.persistentDataPath` through `HectonPersistentPathPolicy.CombineFile` from the background save pipeline.
- `SaveBinaryStorage` still had an `Application.version` fallback if metadata arrived empty.
- Load self-repair built fallback metadata after switching to `Awaitable.BackgroundThreadAsync`.
- Backup retention for promotion/repair was resolved from registry-compatible state inside background rotation.

What was done:
- `SaveManager.GetPersistentAbsolutePath` now combines against the cached persistent root with local relative-path normalization.
- `SaveBinaryStorage` now uses `"Unknown"` as the cold fallback game version instead of Unity API.
- Save and load self-repair capture backup retention before background file rotation.
- Load self-repair now constructs fallback metadata before switching to the background thread.
- Temp write and overwrite handles are explicit `using FileStream` scopes.

Cinematic cheats used:
- No physical simulation. Persistence remains a presentation-cover cheat: freeze state for snapshot, resume, and hide compression/IO behind async status feedback.

Exact microseconds saved:
- Background Unity API avoidance: no deterministic microsecond gain; removes rare illegal-thread access and potential stall/fault path.
- Explicit `using FileStream`: no measurable frame gain; guarantees prompt-required handle release shape.
- Backup-retention capture: expected <1 us saved on background path; value is avoiding registry reads during file rotation.

Blocked / not claimed:
- `dotnet build Hecton8.Core.csproj --no-restore -v:q /clp:ErrorsOnly` still fails on 114 unrelated cross-domain errors.
- Unity console check still blocked by `no_unity_session`.
- Profiler/GCMonitor proof remains absent; status stays PENDING VERIFICATION.
