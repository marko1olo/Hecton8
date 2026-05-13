# WORLD_RESOURCE_SPAWNER Rationale

Status: PENDING VERIFICATION
Mandates loaded:
- `OPT_Zero_GC_Policy_AllocFree_Mandate.txt`
- `OPT_Native_Memory_Collections_JobSystem_Protocol.txt`
- `MATH_Deterministic_RNG_SlotMachine.txt`
- `MATH_Coordinate_Precision_AUP_FloatingOrigin.txt`
- `VOX_MapMagic_Voxel_Seam_Alignment_Integration.txt`
- `DATA_Inventory_Resources_Items_SOA_Layout.txt`
- `DATA_Save_Persistence_Binary_Delta_Checksum.txt`
- `DBG_Telemetry_Crash_Reporting_PostMortem.txt`

## 2026-05-12 - Preflight
Problem: The XML extraction initially failed because the batch tag includes attributes after `id`, while the first regex expected the closing `>` immediately after the id.
Solution: Use an attribute-aware CLI regex over `Docs/Tasks/CURRENT_BATCH.md`, isolate only `WORLD_RESOURCE_SPAWNER`, and discard neighboring prompt content from decision scope.
Rejected Alternatives: IDE tab context and broad MCP-style reads were rejected because batch prompts can truncate or leak neighboring agent tasks.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process control, not runtime.
Hardware Impact: Estimated 0 us runtime gain on i3/MX350; prevents architecture contamination.

Problem: The ore work crosses deterministic placement, terrain projection, render instancing, interaction hydration, save deltas, and telemetry.
Solution: Load eight mandates covering zero-GC, native jobs, deterministic RNG, AUP, MapMagic projection, SoA resource layout, binary save delta rules, and blackbox telemetry.
Rejected Alternatives: Loading the entire mandate registry was rejected as high-noise. Loading only RNG was rejected because the task also requires rendering, save, and interaction boundaries.
Scalability potential: Low uses reduced ore iterations and cheap projection; Middle uses full sector masks; High adds denser dormant render instances; Ultra spends saved cycles on richer visual overkill while keeping authority deterministic.
Hardware Impact: Expected gain is from replacing thousands of ore GameObjects with SoA + indirect rendering; exact microseconds remain PENDING VERIFICATION until compile/profile.

## 2026-05-12 - Core Implementation
Problem: The prompt demands ore generation without 10,000 authoritative GameObjects while keeping mining, depletion, save delta, and origin shift behavior deterministic.
Solution: Added `ProceduralOreSpawner` as a dispatcher-owned SoA runtime: `NativeArray<float3> OrePositions`, `NativeArray<int> OreTypes`, `NativeArray<ulong> DepletionMasks`, double-buffered indirect matrices, and a `NativeParallelHashMap` sector-word depletion cache. The Burst job uses an AUP-derived sector hash and LCG constants to keep candidate slots stable.
Rejected Alternatives: Scene-authored ore prefabs, `Instantiate`, and MeshCollider-per-ore were rejected because they reproduce the memory/load-time failure. Random engine state was rejected because it cannot survive AUP shifts or deterministic replay.
Scalability potential: Low/MX350 halves sector iterations and uses the same cheap triangle fallback; Middle uses the configured iteration count; High adds 25% denser dormant ore visuals; Ultra adds 50% density without increasing authoritative GameObjects.
Hardware Impact: Expected low-end gain is removal of thousands of active transforms/colliders; estimated frame savings are 300-900 us in ore-heavy sectors on i3/MX350, pending profiler capture after global compile is repaired.

Problem: Mining yield had to cross domains without direct inventory mutation.
Solution: Added native `ItemAcquiredSignal` and `ResourceDepletionDeltaSignal` lanes in `GlobalSignals`; `ResourceNode` and `ProceduralOreSpawner` push yield through the signal corridor, with depletion represented as a sector hash plus one 64-bit word mask.
Rejected Alternatives: Direct `PlayerInventory.TryAddItem` or a geology-owned inventory reference were rejected as cross-domain coupling. Saving ore positions was rejected; the deterministic LCG only needs destroyed-bit deltas.
Scalability potential: Low/Middle/High/Ultra all share the same signal format; higher tiers spend saved cycles on visual density, not persistence bloat.
Hardware Impact: Signal packet push is fixed-size NativeQueue work; estimated overhead under 10 us per mined ore on low-end silicon.

Problem: Copper biome restriction initially risked becoming sector-wide instead of heatmap-based.
Solution: Added a native 16x16 biome heatmap lane sampled inside the spawn job. Until Data Monolith owns the source, the lane is filled from MapMagic's current matrix biome. Copper is emitted only when the sampled byte equals biome id 4.
Rejected Alternatives: Sector-global copper gating was rejected after prompt re-read because it would over-spawn copper on biome borders. Per-candidate managed MapMagic calls were rejected because they cannot run in Burst and would allocate or stall.
Scalability potential: Low uses the same 16x16 heatmap with fewer candidates; Ultra can increase visible density without changing biome authority.
Hardware Impact: Heatmap sample is one byte load and two integer clamps per candidate; estimated cost under 25 us per 1024 candidates on i3/MX350.

Problem: Proximity interaction needs collider hits for Laser Cutter without returning to permanent collider spam.
Solution: Prewarmed 24 static collider proxies, baked the shared mesh once with `Physics.BakeMesh`, hydrated using `math.distancesq < 9`, and suppressed the matching indirect matrix while a proxy is active.
Rejected Alternatives: Permanent MeshColliders, per-frame object creation, and sqrt distance checks were rejected as frame-time bloat.
Scalability potential: Low keeps the same 24 proxy cap; Middle/High/Ultra use denser dormant visuals while proxy cap stays bounded.
Hardware Impact: Expected low-end gain is avoiding broadphase registration for thousands of dormant ores; hydration loop is bounded and slow-tick only.

Problem: AUP origin shifts can invalidate runtime positions if the LCG sector is regenerated instead of offset.
Solution: Drained `AupShiftSignal`, offset `OrePositions` and proxy transforms natively, and left the deterministic sector seed untouched. Pending jobs are now retired only when already complete; otherwise the shift is accumulated and applied before the completed output is committed.
Rejected Alternatives: Regenerating after shift was rejected because it could disturb hydrated/depleted slot state and introduce visible popping.
Scalability potential: Low/Middle/High/Ultra all use the same offset path; no tier-dependent correctness divergence.
Hardware Impact: O(active rendered slots) float3 subtraction on shift only; expected under 50 us for 2048 slots on i3/MX350.

## 2026-05-12 - OMEGA POLISH CHANGES
Problem: Fallback terrain projection used `math.sin`, and slope rejection used normalized cross-product math. Both are too honest for a visual ore placement fallback.
Solution: Replaced sine fallback with a triangle-wave cinematic fake and replaced normalized cross product with gradient normal-Y using `math.rsqrt(1 + dx*dx + dz*dz)`. Replaced heightmap division with `math.rcp` and multiplication.
Rejected Alternatives: Keeping sine/normalize was rejected because ore fallback terrain only needs plausible placement, not physically honest waves. A lookup texture was rejected for now because it would add residency/setup work without a measured gain over the triangle wave.
Scalability potential: Low/MX350 gets cheaper terrain fallback and slope rejection; Middle uses full configured count; High/Ultra can spend the saved math on denser visual ore fields.
Hardware Impact: Estimated savings are 20-60 us per 1024 fallback candidates on i3/MX350 versus sine plus normalize.

Problem: Compile verification could not prove the new script through Unity because the project is open and the generated csproj has not regenerated.
Solution: Ran `dotnet build Hecton8.Core.csproj`; current failures are unrelated global dependency holes (`Hecton8.Cartography`, `Hecton8.Physics.Determinism`, `InputSignal`, `PendingSwap`). Ran static audits for forbidden APIs and polish math patterns in touched scripts.
Rejected Alternatives: Editing generated csproj or closing the user's Unity Editor was rejected. Fabricating a green compile report was rejected.
Scalability potential: Runtime scalability unchanged; this is validation state.
Hardware Impact: 0 us runtime gain; prevents false reporting.

Final Git Diff:
- `Assets/_Project/Scripts/Core/GlobalSignals.cs`: added ore yield and depletion delta signal lanes, queue capacities, writers, publish/push/dequeue APIs, and fixed-size signal structs.
- `Assets/_Project/Scripts/ResourceNode.cs`: emits `ItemAcquiredSignal` after successful persistent dropped-item registration.
- `Assets/_Project/Scripts/World/Resources/ProceduralOreSpawner.cs`: added deterministic LCG ore spawner, native SoA storage, MapMagic height projection, heatmap biome gate, indirect rendering, hydration proxies, depletion bitmasks, AUP shift sync, and blackbox dump.
- `Assets/_Project/Scripts/World/Resources.meta` and `ProceduralOreSpawner.cs.meta`: Unity metadata for the new domain folder/script.
- `Docs/Tasks/Status_WORLD_RESOURCE_SPAWNER.md`: state-machine checklist updated.

## 2026-05-13 - Stall and Race Hardening
Problem: `Complete()` existed on disable/dispose, sector switch, and AUP shift paths. That is technically correct but frame-hostile; a sector transition or origin shift could block the main thread on an unfinished spawn job.
Solution: Replaced forced completion with `TryCompleteFinishedSpawnJob()`, which calls `Complete()` only after `IsCompleted`. `Dispose()` now chains `NativeArray.Dispose(JobHandle)` and `NativeParallelHashMap.Dispose(JobHandle)` behind the active spawn handle, then unregisters sentinel ownership without waiting.
Rejected Alternatives: Keeping teardown completion was rejected because teardown/origin-shift stalls are visible hitches on weak CPUs. Cancelling jobs was rejected because Unity jobs have no safe cancel path for these NativeArray writes.
Scalability potential: Low/MX350 gets hitch avoidance during sector churn; Middle/High/Ultra keep dense visual spawn budgets without blocking frame retirement.
Hardware Impact: Estimated 200-2000 us hitch avoidance on i3/MX350 when a sector changes before the spawn job has completed; steady-state cost unchanged.

Problem: Slow tick could hydrate or render old sector data while a scheduled Burst job owned the SoA arrays.
Solution: `SlowTick()` now exits before hydration while `_spawnJobScheduled` is true, and `ScheduleSpawnJob()` zeros render counts plus indirect args until LateFrame commits completed output. AUP shifts received during generation are accumulated in `_pendingRuntimeShift` and applied before bounds/upload state are finalized.
Rejected Alternatives: Reading previous-sector arrays during a write job was rejected as undefined behavior. Blocking for the job was rejected as the same stall problem under a different name.
Scalability potential: Low keeps the cheapest safe behavior; High/Ultra can run denser candidate counts without risking collider hydration against in-flight data.
Hardware Impact: Estimated 0-40 us steady-state cost for the extra branch; prevents undefined read/write races that can cascade into expensive crash recovery.

Problem: Hydration used a proxy-slot scan for each ore candidate, making the slow tick cost `rendered ore count * proxy capacity`.
Solution: Added `_oreProxySlots`, an ore-index to proxy-slot lookup table initialized once with the native state. Hydration and depletion now resolve active proxies in O(1), while free-slot search remains bounded by the 24-slot pool.
Rejected Alternatives: A managed dictionary was rejected because it adds hashing overhead and allocation risk. Keeping the scan was rejected because Ultra density makes unnecessary linear work visible.
Scalability potential: Low/MX350 keeps the same 24 collider cap with less scan waste; High/Ultra can spend saved slow-tick time on denser dormant ore visuals.
Hardware Impact: For 2048 rendered ores and 24 proxies, worst-case comparisons drop from roughly 49,152 to 2,048 per slow tick; estimated 20-120 us saved on i3/MX350 depending on sector density.

Problem: Public ore state and inspector knobs were under-documented, and `_sectorDepletionWords` used the wrong sentinel unregister variant.
Solution: Added XML docs/tooltips, removed dead `_argsMesh`, and switched cleanup to `UnregisterNativeParallelHashMap`.
Rejected Alternatives: Leaving inspector fields opaque was rejected because tuning ore density/visual overkill needs clear knobs. The wrong unregister method was rejected because memory telemetry must stay exact.
Scalability potential: Low/Middle/High/Ultra tuning is clearer for designers; runtime scalability unchanged.
Hardware Impact: 0 us direct frame gain; prevents blackbox/memory-accounting ambiguity during production profiling.

## 2026-05-13 - Signal Consumption and Lifecycle Recheck
Problem: The ore spawner drained `AupShiftSignal` through the legacy `TryDequeueAupShift` path. That API advances a shared legacy cursor over the signal snapshot, so multiple AUP consumers can starve each other depending on tick order.
Solution: Switched the ore spawner to `SignalBus<AupShiftSignal>.GetFrameSnapshot()` and added `_lastAppliedAupShiftFrameId` to process each shift sequence once without consuming it for other systems.
Rejected Alternatives: Keeping destructive reads was rejected because `WorldChunkResidencyManager` also drains AUP shifts. Adding another queue was rejected because `SignalBus` already exposes a zero-alloc read-only snapshot.
Scalability potential: Low/MX350 and High/Ultra all get deterministic origin-shift application independent of consumer ordering.
Hardware Impact: O(number of AUP shifts in frame), normally 0-1. Estimated under 2 us on i3/MX350, with correctness gain over cursor contention.

Problem: Slow tick can miss one-frame signal snapshots when the origin shifts between geology evaluations.
Solution: `LateFrameTick()` now drains the AUP snapshot every frame before job retirement and rendering; slow tick keeps the same drain as a secondary catch path.
Rejected Alternatives: Polling floating-origin singleton state was rejected because the prompt specified the AUP signal path. Relying on slow tick was rejected because signal snapshots are frame-scoped.
Scalability potential: Low tier keeps cheap scan cost; high tiers do not risk rendering stale ore after origin shifts.
Hardware Impact: One frame-snapshot length branch per LateFrame; estimated under 1 us when no shift is present.

Problem: Disabling the spawner while generation is in flight could later commit old sector output after re-enable.
Solution: Added `_discardSpawnJobOutput`, `DiscardSpawnJobOutput()`, and `ClearPresentationState()`. Disable clears active draw/proxy presentation, marks in-flight output as disposable, and forgets the loaded sector so re-enable regenerates against current AUP state.
Rejected Alternatives: Completing the job on disable was rejected as a hitch. Letting old matrices commit was rejected as visible stale geology.
Scalability potential: Low/MX350 avoids both hitch and stale draw; High/Ultra keep dense generation without stale re-enable presentation.
Hardware Impact: Estimated 0-20 us on disable for clearing counters/args; avoids 200-2000 us forced completion hitch and prevents stale render frames.

## 2026-05-13 - Compile-Risk and Contract Recheck
Problem: The ore spawner used `ReadOnlySpan`, `Exception`, and `[NonSerialized]` after the latest hardening edits, but the `System` namespace import was absent during source re-read.
Solution: Restored a single `using System;` at the top of `ProceduralOreSpawner.cs`.
Rejected Alternatives: Depending on implicit imports was rejected because Unity C# project settings are not a runtime contract.
Scalability potential: Low/Middle/High/Ultra unaffected; this is compile correctness.
Hardware Impact: 0 us runtime gain; prevents a namespace compile failure.

Problem: Yield/depletion emissions had drifted to direct `SignalBus<T>.Push`, while the XML prompt explicitly requires `GlobalSignals.Push`.
Solution: Restored `GlobalSignals.Push(in signal)` for `ItemAcquiredSignal` and `ResourceDepletionDeltaSignal` producers in both procedural ore and legacy `ResourceNode`. Kept direct `SignalBus<AupShiftSignal>.GetFrameSnapshot()` only for non-destructive read access because `GlobalSignals` exposes no snapshot wrapper.
Rejected Alternatives: Keeping direct yield push through `SignalBus<T>` was rejected because it contradicts the prompt and the documented integration contract. Reintroducing destructive `GlobalSignals.TryDequeueAupShift` was rejected because it starves other AUP consumers.
Scalability potential: All tiers share one signal corridor; high-tier visual density does not change inventory or persistence handoff.
Hardware Impact: 0 us meaningful runtime delta; the alias delegates to the same typed lane.

Problem: A malformed AUP shift packet could inject NaN/Infinity into every ore position before the blackbox detected it.
Solution: Added an `math.isfinite(signal.ShiftMeters)` guard before shift accumulation.
Rejected Alternatives: Relying only on post-write telemetry dump was rejected because prevention is cheaper than crash diagnosis.
Scalability potential: Low/MX350 avoids poisoned transforms on weak devices; High/Ultra keep visual density without amplifying a bad signal.
Hardware Impact: Estimated under 1 us per shift frame; prevents catastrophic invalid transform propagation.

Problem: The Burst spawn job mixed slot index with a signed multiplication expression, and the job called the outer MonoBehaviour matrix helper.
Solution: Changed slot seed mixing to `unchecked((uint)slot * 747796405u)` and added a job-local `BuildMatrix` helper.
Rejected Alternatives: Signed multiplication was rejected because checked build settings could turn deterministic seeding into overflow failure. Calling the outer helper was rejected as unnecessary Burst ambiguity.
Scalability potential: Low/MX350 and High/Ultra share deterministic seed math; no tier divergence.
Hardware Impact: 0-1 us direct gain; removes compile/runtime ambiguity from deterministic placement.

Problem: Public `Dispose()` could be called without `OnDisable`, leaving dispatcher registrations alive while native arrays were disposed behind a job handle.
Solution: Added `UnregisterDispatchers()` and call it from both `OnDisable()` and `Dispose()`.
Rejected Alternatives: Assuming Unity lifecycle ordering was rejected because `IDisposable` makes direct disposal a public contract.
Scalability potential: All tiers avoid disposed-memory tick calls during scene/tooling teardown.
Hardware Impact: 0 us steady-state gain; prevents invalid callback risk after manual disposal.
