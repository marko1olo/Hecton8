# Rationale_CORE_TELEMETRY

Status: PENDING VERIFICATION

## Decision 000 - Prompt Isolation
Problem: Initial batch extraction found `Docs/Tasks/CURRENT_BATCH.md` unavailable while `CURRENT_BATCH.txt` contained the active batch; later the `.md` batch became available and had to be re-read without ingesting neighboring prompts.
Solution: Used CLI regex extraction against `CURRENT_BATCH.txt` first, then re-extracted only `<AGENT_PROMPT id="CORE_TELEMETRY">` from `CURRENT_BATCH.md` when present.
Rejected Alternatives: Guessing from domain list; reading unrelated prompt blocks; treating the initial missing `.md` as a permanent blocker.
Scalability potential: Low keeps the agent state minimal; Middle/High/Ultra avoid architecture drift by isolating only the telemetry/watchdog scope.
Hardware Impact: No runtime impact. Estimated workflow overhead avoided: irrelevant prompt ingestion.

## Decision 001 - Disk-Backed State
Problem: Anti-amnesia protocol requires persistent state before implementation.
Solution: Created `Status_CORE_TELEMETRY.md` and `Rationale_CORE_TELEMETRY.md` before code.
Rejected Alternatives: Chat-only progress report; deferred rationale after implementation.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; improves batch coordination with 20+ agents.
Hardware Impact: No runtime impact. Compile/runtime cost: 0 us.

## Decision 002 - Frame Pacing Ring
Problem: Existing frame pacing used a managed fixed `float[]` with 60 samples; task requires a 64-frame moving average using `NativeRingBuffer`.
Solution: Replaced the managed sample array with a persistent `NativeRingBuffer<float>` at exactly 64 slots, registered with `NativeMemorySentinel`, and retained O(1) sum maintenance using the write-slot bitmask.
Rejected Alternatives: `List<float>` rejected for heap and resize risk; modulo ring rejected because the mandated project ring already wraps by power-of-two mask.
Scalability potential: Low uses cheap 64-frame average; Middle/High/Ultra use the same watchdog signal to buy `_MATH_LOD_HIGH` visuals when frame time stays below 14ms.
Hardware Impact: Native payload 256 bytes plus container overhead. Hot-path work remains one read, one write, one sum update. Estimated cost under 2 us/frame on i3/MX350, PENDING VERIFICATION.

## Decision 003 - Dynamic Scalability Dispatch
Problem: Plastic visuals came from cheap math staying active after performance headroom returned.
Solution: Kept the existing 14ms Optimal and 18ms/3s Critical thresholds, and routed them through `PerformanceEvents.RaiseSystemDegradation`, `GlobalTelemetryBus.PublishSystemDegradation`, `GlobalRegistry` math precision, and `DistanceMath.PushShaderMathLod`.
Rejected Alternatives: Direct concrete calls into BIOS/render managers; single-use event IDs; shader-only switch without telemetry.
Scalability potential: Low/Middle throttle to `_MATH_LOD_LOW`; High/Ultra regain `_MATH_LOD_HIGH` and can spend cycles on heavier shader response.
Hardware Impact: Dispatch path runs only on state changes after hysteresis. Estimated steady hot-path cost <4 us/frame, PENDING VERIFICATION.

## Decision 004 - Hysteresis and 1024 Telemetry Ring
Problem: Runtime tier flips can oscillate and poison player perception; telemetry must retain bounded failure context without heap churn.
Solution: Preserved 10-second scalability cooldown and verified `GlobalTelemetryBus` uses a 1024-slot `NativeRingBuffer<TelemetryEvent>` with `CapacityMask = 1023`.
Rejected Alternatives: Immediate state flip; unbounded managed queue; modulo wrap.
Scalability potential: Low avoids flicker downgrade/upgrade loops; High/Ultra maintain premium mode only after sustained headroom.
Hardware Impact: One integer mask per telemetry publish. Estimated cost 1-2 us/event; no frame-scale memory growth.

## Build Pass 1
Problem: Tasks 1-5 required compiler proof before moving to 6-10.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore`.
Rejected Alternatives: Static scan-only verification; Unity import claim without compiler evidence.
Scalability potential: All tiers unchanged except the native pacing storage and verified dispatch.
Hardware Impact: Build result green: 0 warnings, 0 errors. Runtime metrics remain PENDING VERIFICATION until Unity profiler/GCMonitor.

## Decision 005 - Binary Dump and Privacy
Problem: Crash dump code must never write raw local paths, usernames, or stack strings, and the main thread cannot perform `.h8dump` I/O during failure.
Solution: Kept binary structs only, added FNV-1a condition/stack hashes, and changed export writes to copy from native scratch buffers through background writer paths without managed `byte[]` dump staging.
Rejected Alternatives: Raw stack trace sidecar; managed `byte[]` export snapshots; synchronous caller-thread flush.
Scalability potential: Low/Middle avoid heap pressure during faults; High/Ultra keep the same diagnostic fidelity via numeric hashes.
Hardware Impact: Main-thread I/O cost remains 0 us. Background export alloc pressure reduced by removing dump-sized managed arrays. Runtime status PENDING VERIFICATION.

## Decision 006 - RAM Bound Cache
Problem: Memory breach logic must compare against a safe bound without polling OS memory every frame.
Solution: Verified `RuntimeWatchdog.CacheRuntimeMemorySafeBoundBytes()` stores `_runtimeMemorySafeBoundBytes` and `_runtimeMemoryBreachBoundBytes` once, then hot cadence uses cached bounds and profiler counters.
Rejected Alternatives: `SystemInfo.systemMemorySize` each Tick; uncached OS queries; threshold recomputation per frame.
Scalability potential: Low uses cached 75% safe bound and 95% breach bound; Middle/High/Ultra use the same event to buy red visor/noir warning effects without repeated OS work.
Hardware Impact: Removes per-frame OS RAM polling. Estimated saving: 3-10 us/sample on weak devices; exact value PENDING VERIFICATION.

## Build Pass 2
Problem: Tasks 6-10 required compiler proof after binary export and FNV hash changes.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -maxcpucount:1 /p:UseSharedCompilation=false`.
Rejected Alternatives: The first default build pass timed out after 120s with no compiler diagnostics; leaving shared `dotnet` processes untouched avoids interfering with other agents.
Scalability potential: Dump path and numeric telemetry are tier-neutral; memory alarm supports low/high visual response.
Hardware Impact: Build result green: 0 warnings, 0 errors. Unity import/profiler proof remains PENDING VERIFICATION.

## Decision 007 - NaN and Bot Math Telemetry
Problem: Tasks 11-12 need math failure recovery and bot distance visibility without turning Burst/hot math helpers into managed telemetry callers.
Solution: Verified `MathGuard.TryAcceptFinite(float3)` already returns a dominant-axis payload and drains `NaN_ERROR_HASH` through `GlobalTelemetryBus`; added `DominantAxisTelemetry` as a 64-byte numeric event and wired headless drones to publish either `math.distancesq` or dominant-axis magnitude-sq at the existing 60-frame fleet telemetry cadence.
Rejected Alternatives: Throwing exceptions on NaN; writing raw bot names; scanning fauna every frame; calling managed telemetry from Burst-decorated `DistanceMath.Normalize`.
Scalability potential: Low uses dominant-axis magnitude-sq as the cheapest bot distance proxy; Middle can keep cadence-only telemetry; High/Ultra use exact `math.distancesq` while preserving the same binary payload shape.
Hardware Impact: Max 8 drone events per 60 frames. Low-end i3/MX350 expected cadence-frame overhead under 20 us, average under 0.4 us/frame, PENDING VERIFICATION.

## Decision 008 - Registry, Draw, and Memory Alarms
Problem: Tasks 13-15 require watchdog observability for frozen services, BRG batch counts, and RAM breach without Unity Profiler draw-call reads or direct UI coupling.
Solution: Kept `RuntimeWatchdog.SampleRegistryHeartbeatsIfDue` on a 60s TickCount cadence, retained BRG manager integer reporting through `FrameTimeWatchdog.ReportBatchRendererGroupBatchCount`, and verified cached 95% memory breach dispatch via `GlobalTelemetryBus.PublishMemoryBreachEvent`.
Rejected Alternatives: Per-frame registry polling; Unity Profiler API draw-call scrape; direct references to Visor controller from core memory monitor.
Scalability potential: Low keeps work sparse and event-driven; Middle/High/Ultra can attach richer visor/noir scanline effects to the same memory breach event.
Hardware Impact: Registry check amortizes to near 0 us/frame; draw call path is integer add only; memory alarm samples profiler allocation every 12 frames. Exact CPU remains PENDING VERIFICATION.

## Build Pass 3
Problem: Tasks 11-15 required compiler proof after adding dominant-axis telemetry.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -maxcpucount:1 /p:UseSharedCompilation=false`.
Rejected Alternatives: Editing unrelated celestial/submarine files outside the CORE_TELEMETRY domain; reverting changes made by other agents; claiming green compile with unrelated compiler errors present.
Scalability potential: Telemetry changes are ready for compile proof once external dirty files are repaired.
Hardware Impact: `[BLOCKED BY DEPENDENCY]` build failed in dirty external files: `HectonCelestialEngine.cs` references missing celestial helper methods, and `SubmarineFluidDynamics.cs` references missing `ImpactSignal`. No compiler diagnostics referenced CORE_TELEMETRY edits.

## Decision 009 - Shader and Input Fault Detection
Problem: Tasks 16-17 require detecting failed/pink shaders by shader name and logging input clock lag, while existing code only checked shader support and completed input-to-render latency.
Solution: Added `Material.shader.name` detection for `InternalErrorShader` in `AssetLifecycleGovernor`, and added `InputLatencyTracker.SampleInputSystemClockDeltaMs()` so `RuntimeWatchdog` compares Input System time against `Time.unscaledTimeAsDouble` with a 50ms threshold.
Rejected Alternatives: Waiting for visible pink pixels; Unity Profiler input markers; logging shader/material names; firing input lag telemetry every frame without cooldown.
Scalability potential: Low gets stable checkerboard fallback and cheap lag hash; Middle/High/Ultra retain exact same telemetry path while visual systems can attach richer glitch effects.
Hardware Impact: Shader check is cold asset-load work. Input clock analyzer is one double subtraction/absolute compare per LateFrame, expected under 1 us/frame on i3/MX350, PENDING VERIFICATION.

## Decision 010 - Black Box Stall and Privacy Budget
Problem: Tasks 18-20 require deadlock dumps, privacy-safe `.h8dump`, and sub-5MB telemetry memory.
Solution: Verified `BlackBoxHeartbeatThread` pings every frame and dumps/kills after 2000ms, kept `.h8dump` payloads as fixed binary structs, and removed the 64KB managed crash export staging buffer so crash export writes the native scratch span directly.
Rejected Alternatives: Main-thread stall detection only; raw stack trace dumps; managed `byte[]` staging for crash exports; unbounded event storage.
Scalability potential: Low avoids GC and crash-time heap pressure; Middle/High/Ultra keep the same fixed binary diagnostic fidelity and can spend saved budget on visual alarm effects.
Hardware Impact: Persistent telemetry storage remains far below 5MB. Removing the 64KB managed crash export scratch lowers retained managed heap and eliminates one crash-export copy. Runtime CPU remains PENDING VERIFICATION.

## Build Pass 4
Problem: Tasks 16-20 required compiler proof after shader/input/privacy edits.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore -maxcpucount:1 /p:UseSharedCompilation=false`.
Rejected Alternatives: Editing audio/submarine files outside CORE_TELEMETRY; reverting other agents; suppressing compiler errors.
Scalability potential: CORE_TELEMETRY changes are ready for proof once external dirty files are repaired.
Hardware Impact: `[BLOCKED BY DEPENDENCY]` build failed in dirty external files: `PlayerFootstepAudio.cs` references missing `_surfaceHits`, and `SubmarineFluidDynamics.cs` references missing `ImpactSignal`. No diagnostics referenced CORE_TELEMETRY edits.

## Compile Wall Attempt 3
Problem: Fail-fast protocol requires repeated compile proof before accepting an external compile wall.
Solution: Ran the same single-threaded `dotnet build` a third time after self-review.
Rejected Alternatives: Fixing unrelated voxel/audio/submarine/celestial systems; reverting other agents; stopping with only one failed build.
Scalability potential: CORE_TELEMETRY remains isolated; external domains must restore compile before profiler verification.
Hardware Impact: Third build failed in `VoxelDeltaProcessor.cs` with parameter type mismatches, missing `UniformFlag`, and missing `DebrisSpawnSignal`. No diagnostics referenced CORE_TELEMETRY files.

## Self-Review Loops
Problem: The SOP requires at least five strict review loops before completion.
Solution: Ran five focused scans: privacy/dump strings, ring/struct alignment, reciprocal/RAM polling, draw/input/stall paths, and resource footprint.
Rejected Alternatives: Treating build failure as a reason to skip review; reviewing only docs; relying on memory instead of disk/source scans.
Scalability potential: Low keeps bounded telemetry; Middle/High/Ultra retain deterministic event shape and can attach richer visual responses without changing core.
Hardware Impact: Resource estimate remains below 5MB. Hot path remains branch/ring/cadence work; exact profiler CPU still PENDING VERIFICATION due compile wall.

## OMEGA POLISH CHANGES
Problem: Polish mandate required anti-bloat proof after all core tasks were done or blocked; it also requested `VERIFIED MASTER GRADE`, which conflicts with the CORE_TELEMETRY prompt requiring `PENDING VERIFICATION` and with the external compile wall.
Solution: Kept status as `PENDING VERIFICATION`, performed diff-level scans for managed allocations/string formatting/foreach/sqrt/normalize, removed the remaining 32-byte managed live telemetry file scratch, and used stack memory plus `ReadOnlySpan<byte>` for live telemetry writes.
Rejected Alternatives: Claiming verified status without a clean build; leaving a managed byte scratch in diff; moving cross-domain drone telemetry into fauna internals.
Scalability potential: Low path uses dominant-axis magnitude-sq for drone telemetry; High/Ultra path uses `math.distancesq`. Frame scalability still promotes `_MATH_LOD_HIGH` under 14ms and demotes to `_MATH_LOD_LOW` after 18ms sustained for 3s with 10s cooldown.
Hardware Impact: Removed 64KB crash export managed scratch plus 32-byte live telemetry scratch. Honest calculation replaced: low-tier drone distance telemetry uses dominant-axis magnitude-sq instead of exact distance squared. Final diff after continuation polish: 7 code files changed, 316 insertions, 237 deletions; docs/log files added/updated. Post-polish build still `[BLOCKED BY DEPENDENCY]` in `VoxelDeltaProcessor.cs`; no CORE_TELEMETRY diagnostics.

## Continuation Polish - Scratch Buffer Audit
Problem: The latest on-disk `CrashTelemetryBuffer.cs` variant still carried managed live/crash file scratch buffers after concurrent edits shifted the implementation shape.
Solution: Re-audited the current file, removed the actual retained managed scratch fields, kept live telemetry on `stackalloc` and crash export on native scratch `ReadOnlySpan<byte>`, then re-scanned the diff for managed allocation/string-formatting and whitespace failures.
Rejected Alternatives: Editing unrelated `VoxelDeltaProcessor.cs` compile blockers; accepting stale log numbers; claiming clean verification while external files still fail compile.
Scalability potential: Low keeps crash/live telemetry out of managed retained buffers; Middle/High/Ultra retain the same binary event fidelity without adding heap pressure.
Hardware Impact: Confirmed no diff-level `new byte[]`, `string.Format`, interpolation, `foreach`, `ToString`, `math.sqrt`, or `math.normalize` additions in CORE_TELEMETRY files. Current code diff: 7 files changed, 316 insertions, 237 deletions. Runtime CPU remains PENDING VERIFICATION.
