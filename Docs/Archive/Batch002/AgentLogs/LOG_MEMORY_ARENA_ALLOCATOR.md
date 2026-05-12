# LOG_MEMORY_ARENA_ALLOCATOR

## 2026-05-11 - Arena 2.0 Implementation
Status: PENDING VERIFICATION

What was wrong:
- Existing `HectonArenaAllocator` was a single-cursor transient arena with 16MB default capacity, no slabs, no double buffering, weak owner telemetry, and reset access that could happen before dispatcher-owned late work finished.
- Legacy `NativeArenaAllocator` duplicated arena ownership instead of delegating to the authoritative Core arena.
- No custom `NativeArenaArray<T>` existed for Burst-facing arena memory.
- The codebase still has 283 `Allocator.Temp`/`Allocator.TempJob` scan hits across 44 files.

What was done:
- Upgraded `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs` to a 100MB Persistent arena split into two logical arenas and processor-count TLS slabs.
- Implemented CAS bump allocation, 16-byte public alignment math, 64-byte normalized runtime alignment, high-water telemetry, OOM hash telemetry, editor-only overlap assertions, and per-frame double-buffer swap.
- Replaced legacy `NativeArenaAllocator` implementation with a shim into `HectonArenaAllocator`.
- Removed the early `HectonArenaAllocator.Reset()` call from `GameTickManager.Tick`; reset now occurs through the existing `SystemDispatcher.LateUpdate` path via `NativeArenaAllocator.Reset()`.
- Added `Assets/_Project/Scripts/Core/NativeArenaArray.cs` with `[NativeContainer]`, `[NoAlias]`, safety handles, min/max restriction fields, indexer, pointer accessors, and `AsNativeArray()`.
- Added `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs` to prove `NativeArenaArray<int>` use inside `IJobParallelFor`.
- Added `NativeArenaArray.cs` to local `Hecton8.Core.csproj` for dotnet-build visibility.
- Wrote `Docs/AgentLogs/RECON_MEMORY_ARENA_ALLOCATOR.md` with all scanned Temp/TempJob offenders.

Cinematic Cheats used:
- Bump pointer arena instead of honest free/defrag.
- Bitmask alignment: `(offset + 15) & ~15`.
- 64-byte cache-line floor for practical SIMD/cache behavior.
- Double-buffer pointer swap instead of per-allocation lifetime tracking.
- OOM false-return/drop path instead of hidden OS fallback.

Exact microseconds saved:
- Arena bootstrap / OS allocator avoidance: estimated 8-35 us on frames that previously created native scratch.
- TLS slabs / contention avoidance: estimated 2-18 us under worker scratch pressure.
- O(1) bump allocation versus TempJob create/dispose churn: estimated 1-12 us at high call counts.
- Dispatcher reset consolidation: estimated 0-4 us, primary value is avoiding use-after-reset.
- NativeArenaArray replacement target: estimated 3-20 us where future callers replace short-lived NativeArray allocation.
- BRG/scatter candidate not claimed: 0 us saved now; unsafe without Unity Graphics memory contract proof.
- Kinematic candidate not claimed: 0 us saved now; existing buffers are persistent and safe.
- Audio DSP: 0 us saved; existing convolution/delay buffers are AudioKernel/Persistent and must not be frame-reset.

Validation:
- `validate_script` passed with 0 errors for `HectonArenaAllocator.cs`, `NativeArenaAllocator.cs`, `NativeArenaArray.cs`, and `NativeArenaArrayEditTests.cs`.
- Unity console after fixing allocator cast showed no allocator errors.
- `dotnet build Hecton8.Core.csproj -v:minimal --no-restore /m:1` no longer reports `NativeArenaArray<>` missing after local csproj include.
- Full compile/test remains blocked by unrelated errors: Audio missing grain helpers, Boid missing `CeilDivPositive`, Survival missing `SurvivalPhysiologyScalarResult`, Tether missing `TetherVerletTelemetryEntry`, GlobalSignals missing `AupPreShiftSignal`, and `AbyssalThermalManager` missing `IFixedTickable.FixedTick(float)`.

Owned diff paths:
- `Assets/_Project/Scripts/Core/HectonArenaAllocator.cs`
- `Assets/_Project/Scripts/Core/NativeArenaAllocator.cs`
- `Assets/_Project/Scripts/Core/NativeArenaArray.cs`
- `Assets/_Project/Scripts/Core/NativeArenaArray.cs.meta`
- `Assets/_Project/Scripts/GameTickManager.cs`
- `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs`
- `Assets/_Project/Tests/Editor/NativeArenaArrayEditTests.cs.meta`
- `Docs/Tasks/Status_MEMORY_ARENA_ALLOCATOR.md`
- `Docs/AgentLogs/Rationale_MEMORY_ARENA_ALLOCATOR.md`
- `Docs/AgentLogs/RECON_MEMORY_ARENA_ALLOCATOR.md`
- `Docs/AgentLogs/LOG_MEMORY_ARENA_ALLOCATOR.md`

## 2026-05-12 00:45 +04:00 - Honest R&D Byte-Count Fix
Status: PENDING VERIFICATION

What was wrong:
- Typed arena wrappers used the raw byte overload with `count`, so `NativeArenaArray<int>(64)` could reserve 64 bytes instead of 256 bytes.
- This was not a polish issue; it was a memory-corruption path inside the allocator API.
- BRG visible-index staging remains a candidate, but migrating it before proving allocator wrapper correctness would produce false evidence.

What was done:
- Changed `TryAllocateNativeArray<T>` and `TryAllocateNativeArenaArray<T>` to call `TryAllocateBlock<T>`.
- Added `NativeArrayOptions` overloads for `NativeArray<T>` and `NativeArenaArray<T>` allocation.
- Updated `NativeArenaArrayEditTests` to use `NativeArrayOptions.ClearMemory` and assert `ByteCount == Count * UnsafeUtility.SizeOf<int>()`.
- Re-extracted the `MEMORY_ARENA_ALLOCATOR` prompt from `CURRENT_BATCH.md` after the audit loop.

Cinematic Cheats used:
- Single generic byte-count path instead of duplicated wrapper math.
- `NativeArrayOptions` bridge so future TempJob migrations stay mechanical and low-risk.

Exact microseconds saved:
- Direct frame-time savings: 0 us claimed; this is correctness-critical.
- Avoided worst-case: out-of-bounds arena writes can cause undefined stalls, corrupted culling data, or crash. The only honest estimate is unbounded failure avoided.
- Future migration friction: estimated 1-3 us engineering/runtime glue avoided per converted hot path by eliminating bool-option adapters.

Validation:
- Text audit confirms both typed wrappers now call `TryAllocateBlock<T>`.
- `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` reports no allocator errors and fails on unrelated `Assets/_Project/Scripts/HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`.
- Unity MCP `validate_script` retries failed because the Unity session was not ready / plugin session disconnected.
- Non-Unity `dotnet` child processes left by timed-out builds were terminated; Unity editor runtime process was left alive.

## 2026-05-12 00:55 +04:00 - Honest R&D Spare-Slab Fallback
Status: PENDING VERIFICATION

What was wrong:
- TLS slab assignment removed contention, but it also created a false-OOM case: one thread could fill its own slab while other slabs in the 100MB arena remained empty.
- That made allocator capacity depend on thread allocation skew, not actual arena exhaustion.

What was done:
- Split allocation into preferred-slab fast path plus bounded fallback probe across remaining slabs.
- `ARENA_OOM_HASH` is now published only after every slab in the active arena fails the request.
- Added `ArenaAllocation_SpillsToFreeSlabBeforeOom` to cover the false-OOM case.

Cinematic Cheats used:
- Fast TLS slab remains the normal path.
- Bounded ring probe is used only on pressure.
- No free list, no defrag, no OS fallback.

Exact microseconds saved:
- Normal allocation path: no claimed change; it still pays one CAS on the assigned slab.
- Pressure path: fallback can cost up to 63 extra slab probes on a 64-thread machine, but avoids false OOM and optional-work disable.
- Expected low-end impact: avoids unbounded visual degradation/crash path from a single hot owner monopolizing one slab; no measured profiler number yet.

Validation:
- `validate_script` passed with 0 errors / 0 warnings for `HectonArenaAllocator.cs`.
- `validate_script` passed with 0 errors / 0 warnings for `NativeArenaArrayEditTests.cs`.
- `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` reports no allocator errors and stops on unrelated:
  - `Assets/_Project/Scripts/HectonBoidController.cs`: missing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)`.
  - `Assets/_Project/Scripts/VoxelDeltaProcessor.cs`: missing `SaveVoxelDeltaRun8`.
- Unity console readback failed because MCP ping did not answer after validation.

## 2026-05-12 01:25 +04:00 - Honest R&D Owner Telemetry Fix
Status: PENDING VERIFICATION

What was wrong:
- Arena owner telemetry used slab cursor end offset as an owner value.
- A 64B allocation near the end of a slab could be reported as a large owner allocation.
- That would make watchdog decisions and future TempJob migration audits blame the wrong subsystem.

What was done:
- Added fixed owner current-frame byte totals and last-frame byte totals.
- Owner high-water now records maximum per-frame bytes allocated by that owner, not slab offset.
- `EndFrameSwap()` snapshots current owner totals into last-frame readback and resets current counters.
- Added `TryGetOwnerLastFrameBytes` and `TryGetOwnerHighWaterBytes` readback APIs with XML summaries.
- Added `OwnerTelemetry_TracksAllocatedBytesNotSlabOffset` regression coverage.

Cinematic Cheats used:
- Fixed-slot owner table.
- Per-frame byte counters.
- Last-frame snapshot during arena swap.
- No dictionary, no per-allocation log, no managed telemetry storage.

Exact microseconds saved:
- Direct frame-time saving: 0 us claimed.
- Avoided cost: false owner pressure can disable or down-tier the wrong visual scratch path; this prevents bad load-shed decisions.
- Runtime cost remains bounded: one `Interlocked.Add` and high-water CAS path per successful owner-tagged allocation.

Validation:
- `validate_script` passed with 0 errors / 0 warnings for `HectonArenaAllocator.cs`.
- `validate_script` passed with 0 errors / 0 warnings for `NativeArenaArrayEditTests.cs`.
- `dotnet build .\Hecton8.Core.csproj -v:minimal --no-restore --no-dependencies /p:BuildProjectReferences=false /m:1` reports no allocator errors and stops on unrelated generated-project dependency wall including `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HardwareTierDetector`, and native bridge symbols.
- Unity console readback failed because MCP ping did not answer after validation.

## 2026-05-12 01:48 +04:00 - Honest R&D Telemetry Lifecycle Fix
Status: PENDING VERIFICATION

What was wrong:
- Owner telemetry slot registration had a cold-path race: if another owner claimed the first observed empty slot before CAS, the successful allocation could be omitted from owner accounting.
- `Shutdown()` freed arena memory but left scalar diagnostics stale: last-frame high-water, OOM count, thread-slab state, and initialization flag.

What was done:
- Reworked owner slot registration into a bounded retry over the fixed telemetry table after failed CAS contention.
- Added `ResetScalarState()` and call it from both shutdown paths, including the already-null base pointer path.
- Added `Shutdown_ClearsArenaTelemetryState` regression coverage for post-shutdown readback cleanup.

Cinematic Cheats used:
- Fixed-slot owner table stays allocation-free.
- Bounded CAS retry only on new-owner contention.
- Scalar reset instead of managed telemetry object rebuild.
- No dictionary, no unbounded retry, no OS/native allocation fallback.

Exact microseconds saved:
- Direct frame-time saving: 0 us claimed.
- Existing-owner hot path unchanged.
- New-owner contention pays bounded table scans only during cold registration.
- Practical saved cost is diagnostic correctness: avoids false load-shed and missed owner pressure after domain reload or concurrent subsystem startup.

Validation:
- `validate_script` passed with 0 errors / 0 warnings for `NativeArenaArrayEditTests.cs`.
- `validate_script` for `HectonArenaAllocator.cs` was attempted twice; Unity MCP plugin disconnected both times, so no fresh MCP diagnostic was returned for that file.
- `dotnet build .\Hecton8.Core.csproj --no-restore --no-dependencies /p:BuildProjectReferences=false /p:UseSharedCompilation=false /nr:false /m:1 -v:q` reports no allocator errors and stops on unrelated dependency wall: `HectonPersistentPathPolicy`, `PlatformPrecisionClock`, `HectonThreadPriorityPolicy`, `SteamDeckInputPal`, `HapticWaveformLibrary`, `HardwareTierDetector`, scatter telemetry helper methods, `HectonNativeBridge`, and `HectonNativeLibrary`.
- Non-Unity `dotnet` child processes spawned by the timed-out build were terminated after evidence capture.
