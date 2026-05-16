# LOG_SENTINEL_DISPOSAL_GUARD

## 2026-05-16 - CORE/MEMORY Lifecycle Guard

What was wrong:
- `H8Memory` tracked pointer ownership but lacked an automated owner lifecycle purge API, so scene-owned native allocations could survive Space Prologue -> Ocean transitions.
- There was no generation fence for scene transitions, no baseline byte verification, no forced dump on fatal leak, and no owner-level `JobHandle` fence before native free.
- Scene transition code did not pause/unpause through a memory proof gate, so a transition could continue without proving the old generation was released.

What was done:
- Added `H8Memory.ReleaseAll(SystemID)` and owner-indexed native pointer lanes using `NativeParallelHashMap<ushort, NativeList<IntPtr>>`; `ushort` is the exact underlying `SystemID` storage and avoids Unity's enum-key `IEquatable<T>` compile failure.
- Added generation-tagged allocation records, scene transition cutoff capture, `BeginSceneTransitionPurge()`, `CompleteSceneTransitionVerification()`, and cold `SceneManager.sceneUnloaded` release hooks.
- Added `RegisterActiveJob(SystemID, JobHandle)` and owner/transition job completion before forced release.
- Added `IntPtr.Zero` guards and fatal leak blackbox append to `Docs/AgentLogs/Dump_SENTINEL_DISPOSAL_GUARD.bin` with the marker `[FATAL LEAK: SystemID]`.
- Bridged lifecycle control through `SceneRuntimeService`: transitions now publish `SystemPauseSignal` while memory purge is pending and only clear pause after baseline verification.
- Kept edits inside CORE/MEMORY plus the existing core scene transition bridge. No ecosystem/audio/player domains were edited.

Cinematic Cheats used:
- No physical simulation was added. The memory guard is a cold-path transition fence; it buys immersion by preventing old-scene memory from consuming Ocean scene budget.
- Low tier: release old generation before Ocean activation and fail closed with pause if baseline verification fails.
- Middle tier: keep persistent owners while purging scene-owned allocations.
- High/Ultra tier: saved memory and transition determinism can be spent on later Ocean visual overkill; leaked buffers are never treated as acceptable high-end overhead.

Exact Microseconds saved:
- Gameplay hot path: 0.0 us expected delta. New owner maps are touched on allocation/release and scene transition paths, not per-frame gameplay loops.
- Transition path: not honestly measurable without Unity runtime profiling in this blocked build. Expected saving is avoiding the reported 200MB stale allocation carryover, not a claimed CPU microbenchmark.
- Explicit `ReleaseAll(SystemID)` improves teardown lookup from broad record sweep to owner pointer lane traversal in the common case; exact us saved depends on allocation count and was not measured.

Verification:
- Extracted the `SENTINEL_DISPOSAL_GUARD` XML prompt from `Docs/Tasks/CURRENT_BATCH.md` by CLI and followed the 18-task state loop.
- Read and applied relevant mandates before coding: native memory/job system, non-reload transitions, bootstrap init safety, global registry, signal lanes, telemetry crash reporting, zero-GC, and asset lifecycle.
- Ran static scan on touched files for stale `NativeParallelHashMap<SystemID>` use, direct owner-index map mistakes, `Debug.Log`, `TODO`, and `FIXME`; no matches.
- Ran `git diff --check` on touched files; only line-ending warnings, no whitespace errors.
- Ran `dotnet build Hecton8.Core.csproj --no-restore`. Local H8Memory compile failure was fixed. Final build remains blocked by unrelated cross-domain compile errors: ambiguous `BrineLayerSample`, `MacroSwarm`, `MacroSwarmArrival`, `AcousticAup`, and `VirtualVoice` failing `NativeList<T>` unmanaged constraints.
- No compiler errors were reported in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs` or `Assets/_Project/Scripts/Core/SceneRuntimeService.cs` after the local fix.

Omega Polish:
- Extracted `<POLISH_MANDATE>` from `Docs/Tasks/CURRENT_BATCH.md` only after all tasks were checked or blocked.
- Result: `NO_POLISH_MANDATE_TAG_FOUND`.
- No invented polish scope was applied.

## 2026-05-16 - Continuation Inquisition / Owner Purge Index

What was wrong:
- `ReleaseAll(SystemID)` had owner pointer lanes, but pointer-to-record resolution still fell back to scanning the allocation table when the exact record index was needed.
- `Shutdown()` completed no registered owner job fences before force-freeing raw tracked records.

What was done:
- Added `NativeParallelHashMap<long, int>` pointer-to-record-index tracking in `H8Memory`.
- Maintained the index lane on pointer register, capacity growth, lookup repair, and swap-back record removal.
- Added `_ownerJobKeys` plus `CompleteAllOwnerJobs()` so shutdown drains every registered owner `JobHandle` before native frees.
- Converted owner job-fence registration failure into `FatalMemoryException.ThrowAllocationTrackingFailed()` instead of silently dropping the fence.

Cinematic Cheats used:
- No visual code belongs in CORE/MEMORY. The cheat is budget recovery: owner teardown gets cheaper and shutdown becomes deterministic so Ocean/VFX can spend memory on actual scene work instead of leaked Prologue state.
- Toaster mode: no gameplay-loop work, no per-frame disk writes, no managed event lane.
- God-mode: saved transition budget remains available for high-tier systems after memory baseline verification.

Exact Microseconds saved:
- Gameplay hot path: 0.0 us claimed; new lanes are touched on allocation/release/teardown, not Tick.
- Owner teardown: complexity improves from owner pointer count multiplied by active allocation count to O(1) common pointer lookup. Exact microseconds were not measured.
- Shutdown thread sync: 0.0 us gameplay; shutdown may block by the actual outstanding job duration.

Verification:
- `dotnet build Hecton8.Core.csproj --no-restore --nologo /clp:ErrorsOnly` completed in 14.40s and failed on 15 external errors: missing `Hecton8.VFX.Wakes`, missing screen-space light shaft types, missing wake telemetry types, and `EcosystemDirector` interface drift.
- No compiler errors were reported in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, or `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`.
- `git diff --check` reported only line-ending warnings.
- Static scans found no CORE/MEMORY `StructLayout` without `Pack = 1`, and no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, legacy `EventBus`, custom `event`, `Action<>`, or `Func<>` in CORE/MEMORY.

## 2026-05-16 - Continuation Inquisition / Frame Heartbeat

What was wrong:
- The H8Memory blackbox ring existed, but it was allocation/free/transition event-driven. That is not the same as the last 300 frames of system heartbeat.
- A crash between memory events would leave no frame-by-frame sentinel pulse.

What was done:
- Added `Heartbeat` telemetry flag and `H8Memory.RecordHeartbeat()`.
- Added a frame index to `H8MemoryTelemetryEntry` by replacing two reserved ushort fields with one `uint Frame`; manual packed entry size remains 64 bytes.
- Routed `SceneRuntimeService.Tick` to write one H8Memory heartbeat per frame.
- Forced H8Memory initialization from `SceneRuntimeService.InitializeService` so the Tick path does not allocate.

Cinematic Cheats used:
- No visual code belongs in this domain. The cheat is diagnostic certainty at tiny cost: a 300-frame native ring gives crash context without disk writes or managed logging.
- Toaster mode: one NativeArray struct write per frame, no GC, no per-frame I/O.
- God-mode: same telemetry footprint; no memory growth hidden behind high-tier hardware.

Exact Microseconds saved:
- No exact microseconds claimed. The new hot-path work is one fixed NativeArray struct store per frame; runtime profiling is blocked by external compile errors.
- Persistent memory remains 19,200 bytes for the 300-entry ring.
- GC impact is 0 B/frame by static inspection.

Verification:
- `rg --pcre2` found no `StructLayout` in CORE/MEMORY without `Pack = 1`.
- Static domain scan found no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, legacy `EventBus`, custom `event`, `Action<>`, or `Func<>` in CORE/MEMORY.
- `git diff --check` reported only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --no-restore --nologo /v:minimal` completed in 01:42.88 and failed on 85 external errors across Items, World, Physics, Homeostasis, Determinism, and Fauna. No CORE/MEMORY errors were reported.

## 2026-05-16 - Continuation Inquisition / Vault Owner Eviction

What was wrong:
- GlobalDataVault had per-buffer owner metadata, but scene transition only released top-level H8Memory records.
- Scene-owned vault buffers could survive as stale suballocations inside the CoreDataVault arena.

What was done:
- Added `IDataVault.ReleaseOwnerBuffers(SystemID, out long)` and `IDataVault.ReleaseSceneOwnedBuffers(out long)`.
- Implemented cold owner eviction in `GlobalDataVault`, freeing scene-owned blocks while retaining the reusable arena.
- Wired `SceneRuntimeService.CompleteMemoryLifecycleTransition()` to evict scene-owned vault buffers before H8Memory baseline verification.
- Locked vault blocks are skipped and routed to the existing Phi/VOD blackbox path instead of being force-freed.

Cinematic Cheats used:
- Retained the arena as a reusable memory budget rather than shrinking/reallocating it during transitions. That avoids transition churn while evicting stale scene data.
- Toaster mode: cold O(vault buffer count) transition scan, no per-frame GC, no per-frame disk I/O.
- God-mode: old scene buffers stop occupying vault slots, but high-tier Ocean/VFX can immediately reuse the warm arena.

Exact Microseconds saved:
- No exact microseconds claimed. Runtime profiling is blocked by external compile errors.
- Gameplay hot path: 0 B GC/frame and no new per-frame scan.
- Transition path: one cold scan over vault keys; exact cost depends on buffer count and was not measured.

Verification:
- Static scan found no CORE/MEMORY `StructLayout` without `Pack = 1`.
- Static domain scan found no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, legacy `EventBus`, custom `event`, `Action<>`, or `Func<>` in CORE/MEMORY.
- `git diff --check` reported only line-ending warnings.
- `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` completed in 01:47.73 and failed on 39 external errors outside CORE/MEMORY. No CORE/MEMORY errors were reported.

## 2026-05-16 - Continuation Inquisition

What was wrong:
- CORE/MEMORY binary records used default sequential packing. That leaves implicit padding and makes dump/interop layouts less deterministic across ARM64/Quest/Android, Mono, IL2CPP, and editor builds.
- H8Memory fatal dumps had leak allocation records but no fixed 300-entry heartbeat ring.
- Raw allocation alignment trusted caller input instead of enforcing a safe power-of-two floor.

What was done:
- Added `Pack = 1` to CORE/MEMORY binary records and reordered fields where packed layout would otherwise put 4-byte or 8-byte values at bad offsets.
- Left `VaultGapAuditJob` unpacked by removing its binary-layout attribute; it contains Unity `NativeArray<T>` wrappers and is not a persisted blackbox record.
- Added `H8MemoryTelemetryEntry[300]` as a persistent native heartbeat ring and writes it into `Dump_SENTINEL_DISPOSAL_GUARD.bin` before allocation detail records.
- Added raw alignment normalization for `AllocateRaw` and `ReallocateRaw`.

Cinematic Cheats used:
- No visual code was touched. Memory's job is to free budget for Ocean/VFX overkill, not to fake shaders inside CORE/MEMORY.
- Toaster mode contribution: deterministic purge, no gameplay disk writes, no per-frame managed events.
- God-mode contribution: old-scene leaks no longer reserve memory that should be spent by high-tier Ocean/VFX systems.

Exact Microseconds saved:
- Gameplay hot path: 0.0 us claimed. New telemetry writes are allocation/free/transition events, not Tick/Update.
- Persistent memory cost: 19,200 bytes for the H8Memory heartbeat ring.
- Runtime microseconds for the 200MB leak prevention remain unmeasured because the Unity/runtime build is still blocked outside CORE/MEMORY.

Verification:
- `rg --pcre2` found no `StructLayout` in CORE/MEMORY without `Pack = 1`.
- Domain scan found no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, custom `event`, `Action<>`, `Func<>`, or legacy `EventBus` in `Assets/_Project/Scripts/Core/Memory/`.
- `dotnet build Hecton8.Core.csproj --no-restore` still fails outside CORE/MEMORY: missing `Hecton8.Animation.Locomotion`, `Hecton8.Core.Determinism`, `Hecton8.Physics.KCC`, missing `ProceduralLadderClimbRuntime`, and Core.Contracts/domain type conflicts.
- No compiler errors were reported in the touched CORE/MEMORY files.

## 2026-05-16 - Continuation Inquisition / ABI Guard and Blackbox Separation

What was wrong:
- H8Memory had frame heartbeats and lifecycle allocation/release/transition snapshots sharing one bounded 300-entry ring.
- A teardown event burst could evict frame heartbeat evidence before a fatal dump, violating the last-300-frame blackbox requirement.
- Packed binary layout attributes existed, but the vault needed an explicit runtime ABI size guard to fail closed if struct sizes drift.

What was done:
- Kept `_blackBox` as the exact 300-frame heartbeat ring.
- Added `_eventBlackBox` as a separate 300-entry lifecycle snapshot ring.
- Routed `RecordBlackBox()` so `Heartbeat` flags write only to the frame ring and all other lifecycle flags write to the event ring.
- Serialized both rings into `Dump_SENTINEL_DISPOSAL_GUARD.bin`.
- Verified `H8Memory.ValidateAbiLayout()` and `GlobalDataVault.ValidateAbiLayout()` check packed binary sizes through `UnsafeUtility.SizeOf` and throw `FatalMemoryException.ThrowAbiLayoutMismatch()` on drift.

Cinematic Cheats used:
- No visual-domain edit belongs here. The memory-domain contribution is preserving crash evidence and evicting old-scene data so Ocean/VFX can spend memory on actual visuals.
- Toaster mode: no per-frame disk writes, no managed queue, one heartbeat struct store per frame.
- God-mode: lifecycle event forensics stay available without sacrificing the mandatory frame heartbeat ring.

Exact Microseconds saved:
- No exact microseconds claimed. Runtime profiling is blocked by external compile errors.
- Gameplay hot path: one fixed native heartbeat struct store per frame, exact CPU cost unmeasured.
- Persistent memory cost: 38,400 bytes total for two 300-entry 64-byte H8Memory blackbox rings.
- GC impact: 0 B/frame by static inspection.

Verification:
- Re-read the exact `SENTINEL_DISPOSAL_GUARD` XML block from `Docs/Tasks/CURRENT_BATCH.md`.
- Static scan found no CORE/MEMORY `StructLayout` without `Pack = 1`.
- Static scan found no `Update`, `FixedUpdate`, `LateUpdate`, `string.Format`, legacy `EventBus`, custom `event`, `Action<>`, or `Func<>` in CORE/MEMORY.
- `git diff --check` reported no whitespace errors for the touched runtime files.
- `dotnet build Hecton8.Core.csproj --nologo /clp:ErrorsOnly` completed in 13.22s and failed on 3 external Construction errors in `VehicleDockingModule`: duplicate `IsLowDockingMathTier`, `ResolveSystemStress01`, and `ResetDockingRuntimeCaches`.
- No compiler errors were reported in `Assets/_Project/Scripts/Core/Memory/H8Memory.cs`, `Assets/_Project/Scripts/Core/Memory/GlobalDataVault.cs`, or `Assets/_Project/Scripts/Core/SceneRuntimeService.cs`.
