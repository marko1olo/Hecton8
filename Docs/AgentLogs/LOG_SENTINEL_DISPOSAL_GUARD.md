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
