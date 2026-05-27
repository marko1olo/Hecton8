# Agent 1328 Rationale

## Decision 000 - Startup Memory Authority

Problem: The task requires removal of persistent native aliases from procedural wreck generation, but no source mutation is defensible before proving the exact field set and GlobalDataVault API surface.
Solution: Start with CLI prompt extraction, mandate loading, Roslyn/static scans, and a disk checklist before editing runtime code.
Rejected Alternatives: Regex-only replacement of `NativeArray` fields would risk local variables, DTO fields, or unrelated safe usage. Blind Vault API calls would risk inventing signatures.
Scalability potential: Low uses no new runtime work; Middle/High/Ultra only become relevant after continuous `GlobalQualityWeight` paths are verified in source.
Hardware Impact: i3/MX350 runtime gain is not claimed. Current action is static safety work only.

## Decision 001 - Evidence Boundary

Problem: Runtime GC and frame-time claims are impossible without Unity/GCMonitor artifacts.
Solution: Mark all runtime performance claims as PENDING VERIFICATION until fresh logs exist; produce static JSON proof for persistent native field counts first.
Rejected Alternatives: Reporting "Zero GC achieved" from code review alone.
Scalability potential: Static proof prevents allocator hazards across all tiers; it does not prove visual scalability.
Hardware Impact: No microsecond gain claimed before source analysis.

## Decision 002 - Vault Migration Shape

Problem: `ProceduralWreckGenerator.cs` owns 13 scene-lifetime native containers directly: 9 `NativeArray<T>`, 2 `NativeList<T>`, 1 `NativeQueue<int3>`, and 1 `NativeParallelMultiHashMap<int,int>`. GlobalDataVault owns `NativeArray<T>` buffers only, so a direct hash/queue/list descriptor substitution does not exist.
Solution: Replace persistent native fields with `VaultGenerationHandle<T>` descriptors and scalar counts. Lists become vault arrays plus explicit counts/capacities. The propagation queue becomes a vault array ring/stack with scalar cursor state. The debris spatial hash becomes a vault-owned spatial-key array / record scan using `WreckDebrisRecord.SpatialHashKey`; it is slower than a hash map but deterministic, relocatable, and bounded by existing quality-scaled debris budgets.
Rejected Alternatives: Keeping `NativeParallelMultiHashMap` as a "special case" preserves the violation. Inventing a DataVault hash-map API would be a fake dependency. Hot registry polling for vault access is rejected; cache `IDataVault` cold and resolve phase-local views from cached handles.
Scalability potential: Low uses the existing `GlobalQualityWeight` debris budget and gravity slice to keep the O(n) scan capped. Middle keeps visual debris density under the same scalar. High/Ultra spend the saved allocation safety margin on larger debris/render budgets through existing continuous quality functions, not binary switches.
Hardware Impact: i3/MX350 expected gain is allocator-risk removal, not raw scan speed. Estimated hot saved cost from removing persistent queue/hash/list allocator ownership during scene teardown/bootstrap is 20-80 us per wreck lifecycle; near-field scan may cost extra microseconds but stays slow-tick bounded.

## Decision 003 - Write Lock Boundary

Problem: Locking every `WreckGridCell`, placement, debris, and telemetry scalar write would satisfy a literal lock-every-write reading but would turn WFC collapse and debris generation into thousands of lock/unlock calls.
Solution: Treat generator writes as owner-phase DataVault writes through current-phase resolved handles. Use explicit `TryLockBuffer` / `TryUnlockBuffer` with `finally` only when a DataVault-backed alias crosses into a scheduled job or frame-yield boundary: BRG placement payload publish and async proxy mesh generation.
Rejected Alternatives: Per-index `TryAcquireWriteLock` in the wrapper setter would be mechanically safe but would spend frame budget on lock churn rather than visuals. Passing `VaultGenerationHandle<T>` into jobs is rejected because jobs require concrete `NativeArray<T>` aliases.
Scalability potential: Low avoids lock churn during WFC and debris passes; Middle/High/Ultra keep the same continuous quality paths while scheduled consumers remain relocation-protected.
Hardware Impact: i3/MX350 avoids thousands of owner-phase lock transitions per generated wreck. Estimated saved overhead versus per-index locks: 200-900 us per 4096-cell solve, static estimate only.

## Decision 004 - Verification Wall

Problem: The repository already has external compiler work active (`csc.exe`, `dotnet.exe`, `VBCSCompiler.exe`) and CPU sampled at 100%, so launching Roslyn or dotnet violates the project build-lane guard.
Solution: Stop at non-compiler evidence until the lane is quiet: diff review, persistent native field audit, brace/parenthesis count, `git diff --check`, and file SHA-256.
Rejected Alternatives: Forcing a second compiler process would create false failures and violate the user's explicit CPU/build rule.
Scalability potential: No runtime scalability claim; this protects shared workstation throughput while preserving objective static evidence.
Hardware Impact: No runtime gain claimed. Prevented additional compiler contention on current machine.

## Decision 005 - Editor Validator Scope

Problem: The one-time static report can rot after later agents edit `ProceduralWreckGenerator.cs`.
Solution: Add an editor-only validator in `Assets/_Project/Scripts/World/Editor/ProceduralWreckGeneratorMemorySovereigntyValidator1328.cs` that scans the target source for forbidden persistent native field ownership and rewrites the agent JSON report. It is outside runtime and does not allocate during gameplay.
Rejected Alternatives: Runtime validator would add managed file IO and regex work to play mode. A validator depending directly on internal DTO types could break across assembly boundaries.
Scalability potential: All tiers unaffected at runtime. Editor validation catches regressions before they reach low-end devices.
Hardware Impact: No runtime cost. Editor-only file scan is sub-millisecond to low-millisecond depending disk cache, not part of frame budget.

## Decision 006 - BufferID Collision Correction

Problem: The first migrated buffer range 70858-70870 collided with existing PowerGridJacobi buffer ownership. A collision destroys GlobalDataVault descriptor sovereignty because two domains can resolve unrelated memory through the same numeric route.
Solution: Move the wreck generator to agent-scoped BufferID range 132800-132812: grid, propagation queue, all placements, filtered placements, runtime definitions, loot, debris, debris keys, clusters, artifacts, scorch decals, burial cuts, telemetry.
Rejected Alternatives: Keeping the previous range and relying on type mismatch to catch it is unsafe. Random free IDs are rejected because the proof artifact must be deterministic.
Scalability potential: Low/Middle/High/Ultra all benefit equally because handle identity no longer aliases another domain. Visual budget is not changed.
Hardware Impact: i3/MX350 gain is failure avoidance, not frame-time speed. Estimated avoided recovery cost after a collision: entire wreck generation failure; steady-state microsecond claim is 0.

## Decision 007 - Write-Lock Wrapper Correction

Problem: The first pass wrote vault buffers through TryResolveHandle views. That made compaction-safety dependent on an implicit contract instead of a visible TryAcquireWriteLock/ReleaseWriteLock route.
Solution: VaultArrayBuffer<T>.TrySet now checks IsCompactionFenceActive, calls TryAcquireWriteLock, validates bounds, writes one element, rechecks the fence, and releases in finally. TryLockWreckVaultBuffer also checks the compaction fence before pinning scheduled-job views.
Rejected Alternatives: Direct TryResolveHandle writes are too weak for this mandate. Holding one write lock across yields or dispatcher phase boundaries is rejected because Vault compaction must stay free to move buffers.
Scalability potential: Low uses the same bounded generation budgets with stronger failure-closed semantics. Middle/High/Ultra can spend quality weight on more fragments without unmanaged pointer lifetime risk.
Hardware Impact: i3/MX350 may pay per-write lock overhead during generation; this is accepted for memory sovereignty until a phase-batched Vault writer exists. Estimated safety cost: +200-900 us per large WFC solve versus direct writes; estimated saved crash/recovery cost is unbounded.

## Decision 008 - AUP Height-Sampling Purge

Problem: Terrain height sampling used absolute AUP values cast directly to float Vector3 before observer-origin subtraction. At large map offsets this can jitter wreck terrain snapping.
Solution: Height sampling now uses runtime-local positions after the double precision origin-relative transform has already been computed. Absolute burial data remains double3 in WreckBurialCutRecord at offset 0.
Rejected Alternatives: Passing absolute Vector3 into MapMagic bridge is rejected. Storing all runtime mesh vertices as double3 is rejected because the render/job pipeline needs float3 local deltas.
Scalability potential: Low/Middle keep stable local wreck placement. High/Ultra can add visual overkill fragments without precision loss at distance.
Hardware Impact: i3/MX350 cost is neutral; the change removes precision failure rather than CPU work. Estimated saved correction/jitter debugging cost: runtime 0 us, visual stability gain high at 100 km boundaries.

## Decision 009 - No-Throw Hot-Path and Numeric Telemetry

Problem: SolveGridAsync and diagnostic branches contained managed exception handling and interpolated debug strings. They are not acceptable as production simulation hot-path failure policy.
Solution: SolveGridAsync returns bool and fails closed through numeric telemetry. Interpolated H8Debug calls in runtime branches were replaced with WriteBlackBoxTelemetry and GlobalTelemetryBus numeric payloads. DumpBlackBox no longer catches Exception.
Rejected Alternatives: catch(Exception) around simulation memory paths is rejected. String logs are editor-friendly but violate the zero-GC gate when reachable from runtime.
Scalability potential: Low avoids GC spikes during failure. Middle/High/Ultra preserve budget for visuals instead of managed diagnostics.
Hardware Impact: i3/MX350 saves unpredictable GC stalls in failure branches. Static estimate: 10-200 us and one managed allocation burst avoided per diagnostic event.

## Decision 010 - Static Scanner Route Under Compiler Lane Block

Problem: The master task requested Roslyn AST proof, but active dotnet compiler processes were present and the project rule forbids launching dotnet/csc while the build lane is occupied.
Solution: Use a Node-based syntax scanner on disk for this pass, backed by targeted rg checks, brace/paren count, git diff hygiene, and an editor-only UnsafeUtility validator that will execute inside Unity when the compile lane clears.
Rejected Alternatives: Starting dotnet anyway would violate the build-lane guard and create false contention. Chat-only proof is rejected.
Scalability potential: Scanner has no runtime cost. It protects all device tiers by blocking persistent native alias regressions before play mode.
Hardware Impact: i3/MX350 runtime cost is 0. Workstation impact stays low because Node scan completed without csc contention.

## Decision 011 - Pinned Vault View Across Await Purge

Problem: BuildProxyMeshAsync pinned WreckGeneratorFilteredPlacementsBufferId through TryLockWreckVaultBuffer, scheduled BuildProxyMeshJob, then awaited WaitForJobHandleAsync before UnlockWreckVaultBuffer. That held a DataVault view across an async yield and could block compaction or leave a stale alias after dispatcher phase movement.
Solution: Complete the proxy mesh job inside the same try/finally lock scope with DispatcherJobSwap.TryComplete(forceComplete: true). This is a cold navigation proxy generation path, not Tick/SlowTick/LateFrameTick. The lock is now released before any later async boundary.
Rejected Alternatives: Copying placements into a managed list or fresh native scratch buffer would create allocation pressure and duplicate ownership. Keeping the async wait was rejected because it violates the compaction fence law.
Scalability potential: Low devices avoid compaction stalls and stale view risk during wreck generation. Middle/High/Ultra keep the same visual budgets; the cold blocking completion is paid once per generated proxy mesh, not every frame.
Hardware Impact: i3/MX350 estimated saved failure recovery is unbounded; steady-state frame gain is 0 us because this is not per-frame. Worst-case cold generation stall may rise by the remaining job time, but DataVault correctness is no longer deferred across frames.

## Decision 012 - Explicit TrySet Failure Telemetry

Problem: The first vault migration allowed indexer setters to fail closed internally but did not expose numeric failure telemetry to the owning system. A stale handle or compaction collision could silently truncate loot, debris, artifact, scorch, burial, runtime definition, grid, or telemetry writes.
Solution: Replace direct vault-backed indexer writes and list appends in the runtime generator with TrySet/TryAddNoResize checks. Failure writes numeric WFAI telemetry flags 3-21 and stops or truncates the local phase before dependent counters claim valid records.
Rejected Alternatives: Throwing exceptions is rejected in production paths. Returning to direct NativeArray writes is rejected because it bypasses lock/fence proof. Per-frame managed logs are rejected because they allocate and hide under editor-only diagnostic comfort.
Scalability potential: Low devices get bounded, fail-closed generation with no silent corrupt counts. Middle/High/Ultra can continue scaling debris, artifacts, and fragment counts through GlobalQualityWeight without violating descriptor consistency.
Hardware Impact: i3/MX350 pays the existing per-write vault lock cost during cold generation. Estimated runtime hot-path cost for SlowTick debris/artifact updates is one TrySet lock per successful state mutation, typically sparse and below 10-40 us per event; avoided corruption/debug recovery cost is materially higher.

## Decision 013 - Scanner Hardening After Rejection

Problem: A green report that cannot detect pinned vault views across awaits or direct vault-buffer indexer writes is insufficient. The previous report missed the BuildProxyMeshAsync lock/yield defect.
Solution: Extend .tmp/agent1328_static_scan.js with pinnedVaultAwaitHits and directVaultIndexerWriteHits gates. The report now fails if await appears between TryLockWreckVaultBuffer and UnlockWreckVaultBuffer, or if vault-backed runtime buffers use direct indexer writes instead of explicit TrySet.
Rejected Alternatives: Manual line-by-line assurance without executable disk scanner is rejected. Roslyn remains deferred because active dotnet processes occupy the compiler lane.
Scalability potential: No runtime cost. The scanner protects all device tiers by catching future lock lifetime and fail-closed regressions before play mode.
Hardware Impact: i3/MX350 runtime cost is 0. Workstation scan cost is sub-second Node execution without csc/dotnet contention.

## Decision 014 - Byte-Only Padding Enforcement

Problem: The DTOs were explicit-layout and 8-byte sized, but several alignment holes were represented as `ushort`, `uint`, or `ulong` padding fields. That satisfies raw size but not the stricter instruction to close holes with explicit private byte padding variables.
Solution: Replace all padding/reserved holes in the wreck DTOs with `private byte _padN` fields at exact byte offsets. Move `WreckDebrisRecord.Quantity`, `Flags`, and `LootTableIndex` to offsets 44, 46, and 47 so data order remains 4-byte fields, then 2-byte field, then 1-byte fields, then byte padding.
Rejected Alternatives: Keeping `ulong` padding is compact but hides byte-level holes. Relying only on `StructLayout.Size` is rejected because it proves total size, not field-level alignment intent.
Scalability potential: Low/Middle/High/Ultra are runtime-neutral; this is data-contract hardening so all tiers receive identical native layout.
Hardware Impact: i3/MX350 steady-state cost is 0 us. Benefit is prevention of ARM64 unaligned/ambiguous DTO regressions.

## Decision 015 - Explicit View Resolution Before Job Use

Problem: Scheduled job initializers still used `_allPlacements.AsArray()` / `_filteredPlacements.AsArray()` directly. If a handle went stale after the scalar count remained valid, the job could receive a default view instead of failing closed before schedule.
Solution: Add `VaultListBuffer<T>.TryResolve(out NativeArray<T>)` and resolve placement views into local variables before job schedule/execute. Failure writes WFAI flags 22-23 and exits before allocating downstream mesh buffers or scheduling jobs.
Rejected Alternatives: Keeping direct `AsArray()` in object initializers is too implicit. Copying placements to managed or TempJob scratch buffers is rejected because it introduces allocation and duplicate ownership.
Scalability potential: Low devices avoid stale-view job failures. Middle/High/Ultra preserve existing quality-scaled placement and debris budgets without new ownership routes.
Hardware Impact: i3/MX350 adds one explicit view resolution per cold render/proxy payload job. Estimated cost is below 5 us per cold generation phase; avoided failure path is materially larger.

## Decision 016 - Build Wall Classification

Problem: The re-audit required compile verification once the build lane cleared, but the solution build failed with 233 errors in other active agents' domains: Inventory, Audio, Fluid, PDA, SubmarineAtmosphere, Flora, Vegetation, and adjacent World vegetation files. A false 1328 failure report would hide the real compile wall owner.
Solution: Run a guarded single-worker `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1`, then capture a filtered log to `.tmp/agent1328_build_errors.log`. The filtered log contains zero matches for `ProceduralWreck`, `ProceduralWreckGeneratorMemorySovereigntyValidator1328`, or `1328`. Keep 1328 status as static VERIFIED_GREEN and compile as BLOCKED BY DEPENDENCY.
Rejected Alternatives: Editing Inventory/Audio/Fluid/PDA/SubmarineAtmosphere/Flora/Vegetation files would violate 1328 domain ownership. Claiming full compile green would be false. Running repeated builds while compiler processes were active was rejected under the build-lane guard.
Scalability potential: No runtime visual tier change. The value is proof isolation: low/middle/high/ultra wreck generation remains governed by the already-audited continuous `GlobalQualityWeight` paths.
Hardware Impact: i3/MX350 runtime impact is 0 us. Workstation impact was one guarded compile attempt plus one filtered capture; no gameplay cost.

## Decision 017 - Verification Hash Scope Correction

Problem: The static report hash covered source, scanner, prompt, and agent logs, but omitted `.tmp/agent1328_build_errors.log`. That log is a touched proof artifact for the compile-wall claim, so leaving it outside the hash made the final verification evidence weaker than the requested touched-file hash.
Solution: Add an optional hash lane for `.tmp/agent1328_build_errors.log` in `.tmp/agent1328_static_scan.js`. The scanner now includes the build-error evidence when present while still running cleanly if a future clean build deletes the log. Rerun scanner result: VERIFIED_GREEN; exact current hash is emitted only by the JSON report to avoid self-referential log churn.
Rejected Alternatives: Hashing generated JSON reports would create self-referential churn. Leaving the build log un-hashed would keep a gap in the proof chain. Relaunching `dotnet build` during 88% CPU and active dotnet/csc processes was rejected by the build-lane guard.
Scalability potential: No runtime tier change. Low/Middle/High/Ultra behavior remains governed by the existing `GlobalQualityWeight` paths; this is proof hardening only.
Hardware Impact: i3/MX350 runtime impact is 0 us. Workstation impact is sub-second Node scanner work; no gameplay cost.

## Decision 018 - Native View Escape Hatch Removal

Problem: `VaultArrayBuffer<T>.AsArray()` and `VaultListBuffer<T>.AsArray()` were unused after explicit `TryResolve` call sites were introduced, but their existence still allowed a future call site to extract a `NativeArray<T>` view without an explicit failure branch or scanner-classified job boundary.
Solution: Remove both `AsArray()` methods and the unused fire-and-forget `AddNoResize(T)` wrapper. Keep only `TryResolve`, `TrySet`, and `TryAddNoResize` so every view or mutation can fail closed. Harden `.tmp/agent1328_static_scan.js` to classify `NativeSlice<T>` field declarations and fail on any `NativeArray<T>`/`NativeSlice<T>` `AsArray()` escape method.
Rejected Alternatives: Keeping unused methods because current call sites are clean is rejected; it leaves an API path for a later persistent alias bug. Replacing them with comments is also rejected because comments do not block regressions.
Scalability potential: No visual tier change. Low/Middle/High/Ultra keep the same `GlobalQualityWeight` budgets; this only narrows the unsafe surface.
Hardware Impact: i3/MX350 runtime impact is 0 us. The removed methods were unused; the gain is correctness and regression prevention.

## Decision 019 - Silent Vault Read Purge

Problem: Vault wrapper indexers returned `default` when a handle failed to resolve during compaction or stale-generation checks. That could turn a DataVault read failure into valid-looking zero WFC cells, zero loot records, or empty telemetry.
Solution: Remove the indexer properties from `VaultArrayBuffer<T>` and `VaultListBuffer<T>`. Convert every runtime vault read in the generator to `TryGet` and fail closed with numeric WFAI telemetry flags 24-48.
Rejected Alternatives: Keeping indexers and relying on code review is rejected because future call sites would reintroduce default-read corruption. Throwing on read failure is rejected because production simulation paths must not use managed exceptions.
Scalability potential: Low devices get deterministic truncation/failure instead of corrupted wreck state. Middle/High/Ultra keep the same continuous quality budgets; no visual tier path changes.
Hardware Impact: i3/MX350 steady-state cost is one explicit boolean branch per vault read. Estimated extra cold generation cost is below 20-80 us; avoided corruption recovery is materially larger.

## Decision 020 - Owner-Phase Async WFC

Problem: `SolveGridAsync` moved to `Awaitable.BackgroundThreadAsync` and then mutated DataVault-backed buffers. `TryResolveHandle` returns transient views and does not pin relocation metadata; background owner-phase mutation is not a proven contract.
Solution: Keep WFC initialization, collapse, and placement assembly on the owner phase, yielding only between coarse generation phases. Add a scanner gate that fails on `Awaitable.BackgroundThreadAsync` in the target file.
Rejected Alternatives: Holding a pinned buffer across async yields is rejected because it blocks compaction. Rebuilding WFC as a full Burst job is a larger rewrite and not needed for this defect; the current safe correction is owner-phase execution plus explicit failure checks.
Scalability potential: Low avoids unsafe worker-thread DataVault access. Middle/High/Ultra can still scale grid, placement, debris, and BRG counts through existing `GlobalQualityWeight`; future jobification must use locked views and bounded completion windows.
Hardware Impact: i3/MX350 may see more cold-generation main-thread work than the old unsafe background path. Runtime frame safety is preferred over unproven parallelism; no per-frame hot cost added.

## Decision 021 - Reentrant Generation Guard and Hot Prewarm Removal

Problem: Public async generation could be called again while a previous generation was between awaits, mutating shared vault buffers and scratch arrays. `SlowTick` debris pickup could also call `QueueWorldPrefabPrewarm`, whose allocation/Addressables behavior cannot be proven alloc-free from this domain.
Solution: Add an interlocked `_generationInFlight` guard around all public generation entry points and release it in `finally`. Move loot prefab prewarm to cold `RefreshLootRecords`; `SlowTick` now only consumes already-loaded prefabs.
Rejected Alternatives: Per-request DataVault buffer ranges are rejected for this pass because they multiply ownership routes and require broader streaming contracts. Leaving prewarm in `SlowTick` is rejected because it hides cold loading work behind a hot cadence.
Scalability potential: Low prevents shared-state collision and hot loading spikes. Middle/High/Ultra preserve visual overkill paths without concurrent generator state corruption.
Hardware Impact: i3/MX350 avoids unpredictable async collision and possible runtime prewarm stalls. Estimated saved hot spike is dependent on catalog implementation; no exact microsecond claim without profiler evidence.

## Decision 022 - MeshData Await Boundary Purge

Problem: `BuildMergedMeshAsync` allocated writable mesh data and acquired read-only mesh snapshots, then yielded across frames while those native views remained live. That violates the no-escaping native view rule and risks native resource lifetime ambiguity if the generator is disabled or disposed during the await.
Solution: Keep only the pre-allocation source scan yield. Once `Mesh.AllocateWritableMeshData` or `Mesh.AcquireReadOnlyMeshData` succeeds, complete copy scheduling, forced cold job completion, snapshot disposal, submesh setup, and `Mesh.ApplyAndDisposeWritableMeshData` in one bounded no-await window. Harden `.tmp/agent1328_static_scan.js` to fail any await between MeshData allocation/snapshot acquisition and disposal/apply.
Rejected Alternatives: Keeping the old async wait loop is rejected because it preserves native resources across frames. Moving all merge work into managed staging arrays is rejected because it adds duplicate ownership and managed allocation pressure. Full per-module GPU assembly is a larger renderer contract change outside this pass.
Scalability potential: Low devices get predictable cold stalls instead of native lifetime hazards. Middle/High/Ultra still scale mesh count through existing `GlobalQualityWeight` and BRG paths; future overkill should move toward BRG/GPU batching, not longer native lifetimes.
Hardware Impact: i3/MX350 may pay a bounded cold-generation sync window during mesh finalization. Hot frame cost is unchanged; saved risk is native leak/use-after-release, not a reliable microsecond gain.

## Decision 023 - Fail-Closed Async Generation Continuation

Problem: `Dispose()` can release DataVault-backed buffers while an async generation path is parked at a frame yield. On resume, the continuation could read `_allPlacements`, `_grid`, or telemetry buffers after release unless every yield boundary revalidated ownership.
Solution: Add `CanContinueGeneration()` and call it after WFC yields, mesh-stage awaits, proxy resolution, BRG-only yield, and final generation-stage yield. The check requires `_initialized` and all wreck vault buffers to still be created; failure returns default/null and writes WFAI flag 49 when telemetry is still available.
Rejected Alternatives: Blocking `Dispose()` until async generation completes is rejected because Unity teardown must not wait on arbitrary frame debt. Adding per-request buffer arenas is rejected for this pass because it requires a broader streaming ownership contract.
Scalability potential: Low/Middle/High/Ultra behavior remains quality-weighted. The change prevents lifecycle corruption equally across tiers.
Hardware Impact: i3/MX350 cost is several boolean checks per async cold generation path, below measurable gameplay cost. It prevents a use-after-release class with unbounded recovery cost.

## Decision 024 - Loot Tick Registration and Pool Expansion Proof

Problem: Once queued wreck loot finished spawning, the generator stayed registered for empty `Tick` and `LateFrameTick` calls. Pool spawns also relied on the default overload even though the object-pool contract explicitly exposes `allowExpand`.
Solution: Unregister loot tick ownership when `_pendingLootCount <= 0` in `Tick`. Call the explicit `Spawn(..., allowExpand: false)` overload for both collision proxy and wreck loot spawns.
Rejected Alternatives: Leaving one empty registered tick is acceptable for a frame, but leaving it indefinitely is hot-path debt. Relying on the current default overload is rejected because the call site should encode the no-expansion requirement directly.
Scalability potential: Low devices avoid permanent no-op dispatcher calls after loot drains. Middle/High/Ultra keep the same visual payload; no gameplay truth changes.
Hardware Impact: i3/MX350 saves a small persistent dispatcher no-op after wreck loot queues drain, estimated below 5 us per registered generator per frame. It also prevents accidental runtime pool expansion if the pool default changes later.

## Decision 025 - Placement Vault Lock Scope Shortening

Problem: `PublishWreckRenderPayload` and `BuildProxyMeshAsync` released DataVault placement locks in `finally`, but the lock span included non-vault work after forced job completion: material registry publication, bounds calculation, and mesh data apply. That widened the compaction-blocking window beyond the actual native view lifetime.
Solution: Release `WreckGeneratorAllPlacementsBufferId` and `WreckGeneratorFilteredPlacementsBufferId` immediately after `DispatcherJobSwap.TryComplete(forceComplete: true)` succeeds or fails. Keep the final `finally` release only as a fallback for early exits. Treat failed forced completion as fail-closed telemetry flags 50-52 before any publish/apply side effects.
Rejected Alternatives: Holding the lock until method exit is simpler but blocks compaction during unrelated renderer/mesh publication. Copying placement records to managed memory before publishing is rejected because it adds allocation and duplicate ownership.
Scalability potential: Low devices keep shorter compaction stalls during cold wreck generation. Middle/High/Ultra preserve existing visual-overkill scatter and proxy paths without extending vault pin lifetimes.
Hardware Impact: i3/MX350 steady-state frame gain is not claimed. The expected improvement is shorter cold-generation compaction lock residency; exact microseconds require Unity profiler markers.

## Decision 026 - Quality-Scaled Debris Terrain Sampling Cheat

Problem: `BuildDebrisSpatialHash` called MapMagic terrain height resolution for every debris record. At the configured debris cap this can be up to 10000 bridge calls during generation, even on weak devices where debris is mostly a visual pickup/sink effect.
Solution: Add `ResolveDebrisTerrainSampleStride(GlobalQualityWeight)` and sample terrain every 8th debris record at minimum quality, continuously converging to every record at quality 1.0. Un-sampled records use their runtime-local fallback sink height. This keeps gameplay truth bounded because debris records already use visual proximity, not rigidbody terrain truth.
Rejected Alternatives: A binary low/high quality switch is rejected by project doctrine. Full terrain sampling for all tiers wastes CPU on a visual detail. Removing terrain sampling entirely would flatten high-tier wreck debris and lose visual grounding.
Scalability potential: Low uses coarse sampling for cheap visual sinking. Middle samples more often through the continuous curve. High and Ultra reach per-record sampling for visual grounding while still avoiding a separate physical debris solver.
Hardware Impact: On i3/MX350, worst-case bridge calls drop from 10000 to about 1250 at minimum quality. Microsecond savings depend on MapMagic bridge cost and terrain cache state; no exact runtime claim without profiler data.

## Decision 027 - Runtime Prefab Prewarm Fallback Purge

Problem: `SpawnWreckLoot` still called `itemCatalog.QueueWorldPrefabPrewarm` when a loot prefab was not loaded. `RefreshLootRecords` already performs the cold prewarm pass, so the spawn path was a redundant route into Addressables/asset dispatch during generation.
Solution: Remove the fallback prewarm call from `SpawnWreckLoot`. If a prefab is not loaded by spawn time, the generator skips that attempt and remains fail-closed. Harden the static scanner so `QueueWorldPrefabPrewarm` is permitted only in `RefreshLootRecords`.
Rejected Alternatives: Keeping the fallback as "harmless" is rejected because `ItemCatalog.QueueWorldPrefabPrewarm` can enqueue asset-load requests and mutate runtime lookup state. Moving prewarm into `SlowTick` is rejected because the tick path must only consume already-loaded prefabs.
Scalability potential: Low devices avoid repeated asset-dispatch touches during wreck generation. Middle/High/Ultra preserve visual loot when cold prewarm succeeds; unloaded prefabs degrade by skipping a spawn attempt rather than adding runtime loading work.
Hardware Impact: i3/MX350 avoids redundant catalog/dispatcher work per failed loot prefab attempt. Exact microsecond savings depend on Addressables state and are not claimed without profiler data.

## Decision 028 - Loot Catalog Snapshot Cache

Problem: `TryQueueDebrisPickup` ran from `SlowTick` and still called `ItemCatalog.FindByHash` plus `TryGetLoadedWorldPrefab`. The catalog contract can rebuild lookups, pump world-prefab dispatch tickets, mutate runtime dictionaries, and touch asset lifecycle services.
Solution: Add fixed cold caches `_lootItemDataCache[MaxLootRecords]` and `_lootPrefabCache[MaxLootRecords]`. `RefreshLootRecords` clears and fills these slots while it owns the cold loot definition pass. `SpawnWreckLoot` and `TryQueueDebrisPickup` only read cache slots by loot record index and fail closed if a prefab is not loaded.
Rejected Alternatives: Calling catalog APIs from `SlowTick` is rejected because hidden work violates hot-path proof. Storing managed object references inside `WreckLootRecord` is rejected because Vault DTOs must remain unmanaged and relocatable. Starting an asset load on player proximity is rejected because it makes pickup interaction trigger streaming work.
Scalability potential: Low avoids catalog/Addressables work in debris proximity checks. Middle/High/Ultra still get loot when the cold prewarm completed; missing assets degrade by skipping spawn/pickup instead of stalling.
Hardware Impact: i3/MX350 removes two catalog calls from each successful near-field debris pickup attempt. Exact microsecond savings depend on item catalog state and asset-load dispatcher pressure; static proof only.

## Decision 029 - Debris Cluster Generation Reset

Problem: `WreckDebrisCluster` records are keyed by spatial cell but the previous reset path did not clear the full cluster buffer between wreck generations. A reused cluster slot could carry old center/extents/count data into a new wreck if the scalar count was reset but the vault-backed buffer content remained.
Solution: Add `ClearDebrisClusters()` and call it from `ResetWreckWorldState()`. The clear uses one `TryLockForWrite` / `ReleaseWriteLock` window over the cluster buffer and resets `_debrisNearFieldCursor`. `ResolveDebrisClusterIndex` now reinitializes a slot when `ClusterKey` differs from the requested key.
Rejected Alternatives: Relying on `_debrisClusterCount = 0` is rejected because the buffer is persistent vault storage. Clearing through per-index `TrySet` is rejected because it multiplies lock traffic.
Scalability potential: Low/Middle/High/Ultra get deterministic cluster state. Visual density remains governed by existing continuous debris budgets.
Hardware Impact: i3/MX350 pays one cold generation buffer clear over `MaxDebrisClusters`; it removes a stale-data class with no per-frame cost.

## Decision 030 - Sliced Near-Field Debris and Batched Gravity Lock

Problem: `ProcessNearFieldDebris` started at index 0 each SlowTick and `UpdateDebrisGravityStateless` locked/wrote debris records one at a time. On dense wrecks this biases pickups toward low indices and adds avoidable DataVault lock churn.
Solution: Add `_debrisNearFieldCursor`, `ResolveDebrisNearFieldScanSlice(float)`, local resolved debris/key views, and cursor advancement. Gravity now locks the debris buffer once per slice with `TryLockForWrite` and releases in `finally`.
Rejected Alternatives: Reintroducing a persistent `NativeParallelMultiHashMap` is rejected by the memory mandate. Full scan per tick is rejected because it burns CPU on far debris. Per-record locks are rejected because the owner phase can batch the slice safely.
Scalability potential: Low scans about 192 records per SlowTick; Ultra reaches 2048 through continuous `GlobalQualityWeight`. Pickup truth degrades by cadence, not by binary feature removal.
Hardware Impact: On i3/MX350, near-field scan work is bounded to a slice instead of the full debris count. Exact saved microseconds depend on debris count and player proximity; static proof only.

## Decision 031 - AUP Terrain Snap Route

Problem: `SnapOriginToTerrainHeight` used runtime-space x/z probes against terrain height. That is safer than direct absolute float casts, but it still assumes the terrain bridge receives the same coordinate space as the generated wreck and does not exercise the AUP-aware route.
Solution: Convert the wreck `AbsoluteUniversePosition` to a double3 absolute probe, clamp each component to float only at the bridge boundary, call `TryGetHeightAUP`, then rebuild the snapped AUP and convert back through the origin-relative `ToRuntimeFloat3()` path.
Rejected Alternatives: Calling `TryGetHeight(runtimeOrigin.x, runtimeOrigin.z)` is rejected because it bypasses the AUP bridge contract. Storing all mesh vertices as double is rejected because render payload jobs need local float deltas.
Scalability potential: Low/Middle/High/Ultra keep identical gameplay placement; high-distance wrecks retain stable terrain contact when quality raises debris/fragment density.
Hardware Impact: Runtime CPU cost is effectively neutral. The gain is precision-contract correctness at large world offsets, not a measurable per-frame speedup.

## Decision 032 - Cold Black-Box Dump Deferral and Ordering

Problem: NaN/bounds validation requested a black-box dump from the detection path and `DumpBlackBox()` wrote ring entries in raw buffer order. That made the dump less useful after wraparound and could perform managed IO inside `SlowTick` fault detection.
Solution: Replace direct dump calls with `RequestBlackBoxDump()`. `LateFrameTick` and `OnDisable` flush once through `FlushBlackBoxDumpIfRequested()`. The dump writes a fixed magic, entry size, count, cursor, and chronological ring entries using `_telemetryWrittenCount`.
Rejected Alternatives: Synchronous dump in the detection method is rejected because file IO is a cold diagnostic side effect. Raw ring order is rejected because the last 300-frame history should be directly replayable.
Scalability potential: All tiers preserve the same telemetry DTO layout. Low devices avoid immediate fault-path IO inside simulation cadence.
Hardware Impact: No steady-frame gain claimed. Fault-path IO is moved to LateFrameTick or teardown and guarded to run once.

## Decision 033 - Proxy Mesh Lock Window Narrowing

Problem: `BuildProxyMeshAsync` still allocated writable MeshData and configured vertex/index buffers while the filtered placement vault buffer was pinned. Those operations do not need the DataVault view and widen the compaction-blocking window.
Solution: Move MeshData allocation and buffer setup before `TryLockWreckVaultBuffer`. The lock now covers only view resolution, job initialization, scheduling, forced completion, and immediate unlock.
Rejected Alternatives: Holding the lock through MeshData setup is simpler but blocks Vault compaction for unrelated Unity mesh allocation work. Copying placements into managed staging memory is rejected because it creates another allocation route.
Scalability potential: Low devices see shorter vault pin windows during cold proxy generation. Middle/High/Ultra keep the same proxy fidelity and module placement counts.
Hardware Impact: No honest frame-time claim without profiler markers. The correction reduces lock residency and compaction risk in cold generation.

## Decision 034 - Sync Proxy MeshData Dispose Guard

Problem: `BuildProxyMesh()` allocated `Mesh.MeshDataArray` and only disposed it through `Mesh.ApplyAndDisposeWritableMeshData`. If a bounds/submesh/result setup path failed after allocation, the native MeshData allocation had no local `finally` guard.
Solution: Wrap synchronous proxy mesh generation in the same `meshApplied` / `finally writableMeshData.Dispose()` pattern used by merged mesh builders. Harden the scanner with `writableMeshDataFinally` so every `Mesh.AllocateWritableMeshData` method must have a dispose fallback.
Rejected Alternatives: Trusting Unity mesh setup to never fail is rejected because stale DataVault reads and invalid mesh descriptors must fail closed without leaking native allocations. Moving proxy build fully async is rejected because it would not fix the sync path and would add scheduler overhead for small proxy meshes.
Scalability potential: Low/Middle/High/Ultra preserve the same proxy mesh fidelity. The gain is leak prevention during cold generation failure, not visual change.
Hardware Impact: No steady-frame speedup claimed. It prevents native MeshData leakage on failure paths, which protects memory pressure on i3/MX350-class devices.

## Decision 035 - AUP Terrain Snap Without Absolute Float Bridge

Problem: The previous terrain snap correction called `TryGetHeightAUP(Vector3)`, but the bridge contract still accepts absolute coordinates as `float Vector3`. That can lose precision before the runtime-origin subtraction, violating AUP determinism at large world offsets.
Solution: Keep MapMagic bridge files untouched because they are already dirty under other agents. In `ProceduralWreckGenerator`, resolve current runtime origin as `double3`, compute `localDelta = absolute - originAbsolute` in double precision, validate it, clamp only local x/z to float, then call `TryGetHeight(localX, localZ)`.
Rejected Alternatives: Editing dirty MapMagic bridge files is rejected in this pass to avoid cross-agent interference. Passing absolute doubles through the existing `Vector3` bridge overload is rejected because the cast happens too early.
Scalability potential: All quality tiers get identical wreck placement truth. High/Ultra keep stable terrain contact at large world offsets when visual density increases.
Hardware Impact: Runtime CPU cost is neutral. The gain is precision correctness, preventing large-world jitter rather than saving frame time.

## Decision 036 - Debris Construction Batch Locks

Problem: `BuildDebrisSpatialHash()` used `_debrisRecords.TrySet`, `_debrisSpatialKeys.TrySet`, and `_debrisClusters.TryGet/TrySet` inside the debris loop. At high debris budgets this created thousands of DataVault lock transitions during cold generation.
Solution: Acquire write locks for debris records, spatial keys, and debris clusters once for the construction window, write direct local views, and release all locks in reverse order in `finally`. Harden the scanner with `debrisBuildBatchLock`.
Rejected Alternatives: Per-record `TrySet` is simpler but burns lock traffic. Reintroducing a persistent native hash map is rejected by the memory sovereignty mandate. Managed staging arrays are rejected because they add another allocation route and duplicate ownership.
Scalability potential: Low uses smaller continuous debris budgets; Middle/High/Ultra can spend saved lock overhead on denser visual debris without changing gameplay truth ownership.
Hardware Impact: On i3/MX350, lock transitions collapse from roughly `2 * debrisCount + cluster updates` to three bounded construction locks. Exact microseconds require Unity profiler evidence.

## Decision 037 - Debris Visual Sink Instead Of Terrain Bridge In Lock Window

Problem: `BuildDebrisSpatialHash()` resolved terrain height while debris records, spatial keys, and clusters were batch write-locked. That introduced an external terrain/bridge call inside a DataVault compaction-blocking window.
Solution: Replace debris terrain bridge sampling with `ResolveDebrisVisualSinkHeight(position, worldBounds, stableId, qualityWeight01)`, a deterministic visual sink fake based on local bounds, stable hash, and continuous `GlobalQualityWeight`.
Rejected Alternatives: Calling `MapMagicBridge.TryGetHeight` inside the debris loop is rejected because it widens lock residency and may touch terrain systems. Reintroducing sparse bridge sampling under the lock is rejected for the same reason. Full debris/terrain physics is rejected because debris is a visual pickup affordance, not gameplay terrain truth.
Scalability potential: Low gets cheap bounded visual sinking. Middle/High/Ultra get deeper/noisier visual embedding through continuous quality weight without adding terrain bridge calls.
Hardware Impact: On i3/MX350 this removes up to `debrisCount` terrain bridge calls from the batch lock window. Exact microseconds depend on terrain cache state and require profiler data.

## Decision 038 - Deferred Debris Failure Telemetry

Problem: Some debris construction failure paths could call `WriteBlackBoxTelemetry` while debris/spatial/cluster write locks were still held. Telemetry itself writes the unmanaged ring through the Vault wrapper, creating nested lock risk.
Solution: Store numeric failure code, values, and position in local scalars during the lock window. Release clusters, spatial keys, and debris records in `finally`, then write telemetry once outside the locked region.
Rejected Alternatives: Leaving telemetry inline is rejected because diagnostic writes must not extend or nest compaction-blocking locks. Dropping telemetry is rejected because fail-closed paths still need numeric proof.
Scalability potential: All tiers preserve the same black-box history and debris visuals. Low-tier benefits most from shorter worst-case lock residency during failed generation.
Hardware Impact: No steady-frame speedup claimed. The correction removes nested Vault lock risk and keeps failure diagnostics outside the construction critical section.

## Decision 039 - Unsigned Debris Cluster Modulo

Problem: `math.abs(clusterKey) % MaxDebrisClusters` is not safe for `int.MinValue`; the result can remain negative and index `clusters[clusterIndex]` out of bounds.
Solution: Use `(int)((uint)clusterKey % (uint)MaxDebrisClusters)` before reading the cluster view. Harden `debrisBuildBatchLock` scanner proof to reject `math.abs(clusterKey)` and require unsigned modulo.
Rejected Alternatives: Branching around `int.MinValue` is rejected because unsigned modulo is cheaper and branchless. Try/catch around index failure is rejected by no-throw fail-closed rules.
Scalability potential: All quality tiers retain deterministic cluster distribution without exceptional overflow cases.
Hardware Impact: CPU cost is neutral to lower than an abs/branch guard. The gain is native bounds safety under adversarial hash values.

## Decision 040 - Synchronous Proxy Mesh Vault Pin

Problem: `BuildProxyMesh()` filtered placements into the vault-backed `_filteredPlacements` buffer and then resolved a `NativeArray<WreckModulePlacement>` view without pinning `WreckGeneratorFilteredPlacementsBufferId`. The async proxy path already pinned the same buffer, so the sync navigation proxy route had a compaction-safety gap.
Solution: Lock `WreckGeneratorFilteredPlacementsBufferId` immediately before `_filteredPlacements.TryResolve`, execute the synchronous `BuildProxyMeshJob` loop while the view is pinned, unlock before `CalculateLocalBounds`, submesh setup, mesh creation, and `Mesh.ApplyAndDisposeWritableMeshData`, and keep a `finally` fallback unlock through `placementLockHeld`.
Rejected Alternatives: Copying placements into managed staging memory is rejected because it adds allocation and duplicate ownership. Holding the lock through bounds calculation and mesh apply is rejected because those operations do not need the vault view and would widen the compaction-blocking window.
Scalability potential: Low devices keep shorter and correct vault pin windows during cold navigation proxy generation. Middle/High/Ultra preserve the same proxy mesh fidelity and placement counts without changing `GlobalQualityWeight` semantics.
Hardware Impact: No profiler-backed frame-time gain is claimed. The correction removes stale native view risk in cold generation; expected microsecond impact is shorter lock residency, not a measurable hot-frame saving.

## Decision 041 - Runtime Definition Batch Copy Window

Problem: `RefreshRuntimeDefinitions()` rebuilt cold module DTOs and then wrote the vault-backed runtime definition buffer through repeated scalar write paths. That is cold, but it still multiplies DataVault write-lock transitions during refresh and makes the route look like a hot mutable list.
Solution: Add `_runtimeDefinitionBuildCache[MaxModuleDefinitions]` as a cold managed staging cache, clear it on vault release, populate it before acquiring the vault lock, and copy the complete runtime definition range through one `TryLockForWrite(out NativeArray<WreckModuleRuntimeDefinition>)` window with release in `finally`.
Rejected Alternatives: Keeping per-slot `TrySet` is mechanically safe but wastes lock traffic. Storing a persistent `NativeArray<WreckModuleRuntimeDefinition>` cache is rejected because it reintroduces stateful native ownership. Editing shared DataVault APIs for a bulk writer is rejected because this domain can solve the issue locally.
Scalability potential: Low devices avoid unnecessary cold refresh lock churn. Middle/High/Ultra preserve identical module truth and can scale visual module density through existing `GlobalQualityWeight` routes.
Hardware Impact: On i3/MX350, lock transitions for runtime definition refresh collapse from up to `MaxModuleDefinitions` writes to one bounded lock window. Exact microseconds require profiler data.

## Decision 042 - Generated Record Construction Batch Locks

Problem: Artifact, scorch decal, and burial cut construction are cold generation phases, but the generated records were still vulnerable to per-record vault write churn and inline failure telemetry near write-lock code.
Solution: Acquire one write lock per generated record buffer in `BuildArtifactFragmentHash`, `BuildScorchDecalRecords`, and `BuildBurialCutRecords`; write records through local `NativeArray<T>` views; release in `finally`; emit numeric failure telemetry only after the lock is released.
Rejected Alternatives: Per-record `TrySet` is simple but burns lock overhead at high visual density. Holding a lock while calling external voxel or scan systems is rejected; interactive mutation paths keep sparse `TrySet` after side effects instead.
Scalability potential: Low gets bounded cold generation cost. Middle/High/Ultra can spend the saved lock budget on more lore fragments, scorch decals, and burial visual cuts without changing gameplay authority.
Hardware Impact: On i3/MX350, construction lock transitions collapse from record-count dependent writes to three bounded buffer windows. Profiler evidence is still required for exact frame-time savings.

## Decision 043 - Interactive Mutation Boundary

Problem: Sparse artifact discovery and burial-cut mutations interact with external systems. If the external event or voxel cut runs before the Vault state commit is proven, a failed `TrySet` can duplicate discovery events or reapply a crater.
Solution: Commit the state first through sparse `TrySet`, release the Vault lock immediately, and only then call the external system. Artifact discovery rolls `State` back to 0 if `ScanEvents.TryRaiseEntryDiscovered` fails; burial cuts mark `Applied=1` before voxel surgery because the voxel API is void and must not be wrapped in catch-based recovery.
Rejected Alternatives: Holding a write lock across `ScanEvents` or voxel surgery is rejected. Keeping side effect before state commit is rejected because duplicate events/cuts are worse than a fail-closed missed event.
Scalability potential: Low devices avoid repeated side effects under contention. Middle/High/Ultra can scale artifact and burial counts without multiplying duplicate external work.
Hardware Impact: Runtime cost is neutral except one rollback `TrySet` on event failure. The gain is deterministic side-effect ordering, not raw microseconds.

## Decision 044 - Verification Lane After Batch Lock Pass

Problem: Static scanner green is insufficient unless independent audits and build-lane rules are checked after edits.
Solution: Rerun `.tmp/agent1328_static_scan.js`, targeted `JobCompletionAudit`, targeted `DataVaultSovereigntyAudit`, prompt extraction/task count, brace/paren balance, and `git diff --check`. Build was not launched because CPU sampled at 100%, even though no dotnet/csc/VBCSCompiler processes were active.
Rejected Alternatives: Claiming compile/runtime proof without a build is rejected. Launching `dotnet build` during 100% CPU violates the project guard and would interfere with other agents.
Scalability potential: No runtime tier change. This preserves evidence quality without consuming workstation headroom.
Hardware Impact: Runtime cost is 0. Workstation impact is limited to static scans and Python audits.

## Decision 045 - Read-Only Vault Route For Pure Consumers

Problem: `VaultArrayBuffer<T>.TryGet()` and the near-field debris scan used mutable `NativeArray<T>` aliases for pure reads. That was not a persistent field leak, but it weakened the read-accessor contract and made consumer code look like it could mutate vault-owned buffers without a write lock.
Solution: Add `TryResolveReadOnly(out NativeArray<T>.ReadOnly)` to `VaultArrayBuffer<T>` and `VaultListBuffer<T>`, route scalar `TryGet()` through `IDataVault.TryReadOnlyHandle`, and switch `ProcessNearFieldDebris()` to read-only debris/spatial views. Mutable `TryResolve()` remains only for locked job windows and direct write windows.
Rejected Alternatives: Removing mutable `TryResolve()` entirely is rejected because scheduled mesh/render jobs still require pinned `NativeArray<T>` views. Copying read data into managed staging arrays is rejected because it creates allocation and duplicate ownership.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The gain is ownership clarity and compaction safety under all quality weights.
Hardware Impact: No frame-time saving is claimed. The likely cost is neutral; read-only handle resolution follows the Vault's pure read path and avoids accidental write aliases.

## Decision 046 - Cold Allocation Comment Gate

Problem: Some cold managed staging arrays had comments that did not state exact type/capacity/owner, and one existing queue comment used `PendingWreckLootSpawn[8]` instead of the real allocation token `MaxPendingLootSpawns`.
Solution: Normalize cold allocation comments for runtime definition staging, loot staging, item/prefab caches, pending loot queue, and shared empty collider array. Harden the scanner so readonly array fields must include `COLD ALLOC: Type[Capacity]` and `owner: ProceduralWreckGenerator`.
Rejected Alternatives: Treating comments as cosmetic is rejected because HECTON-8 uses disk proof artifacts as long-term memory across compaction and multi-agent handoff. Moving these arrays to native memory is rejected because they are cold managed refs or DTO staging caches, not persistent simulation collections.
Scalability potential: No visual tier change. The proof now states which cold allocations are allowed and why, reducing future accidental hot-path growth.
Hardware Impact: Runtime cost is 0. The benefit is auditability, not microsecond savings.

## Decision 047 - No Same-Frame Forced Job Readback

Problem: `JobCompletionAudit` found four `DispatcherJobSwap.TryComplete(... forceComplete: true)` sites. They were cold generation paths, not Tick blockers, but the project doctrine rejects same-frame schedule/readback loops without profiler proof.
Solution: Replace immediate schedule/complete patterns with direct execution of the same deterministic job kernels: scatter payload, render payload, proxy mesh async, and merged mesh copy finalization. Remove `_copyHandles` and `CombineScheduledCopyHandles()` because no scheduled copy fan-in remains.
Rejected Alternatives: Keeping forced completes with comments is rejected because comments do not remove the synchronization point. Spreading these jobs across awaits is rejected because it would hold `MeshDataArray`, read-only mesh snapshots, or pinned Vault views across frame boundaries.
Scalability potential: Low avoids job scheduling overhead for small cold batches. Middle/High/Ultra keep the same visual output and still scale count/fidelity through continuous `GlobalQualityWeight`.
Hardware Impact: On i3/MX350, this removes four possible dispatcher sync points and one `JobHandle[maxPlacements]` cold cache. Exact microseconds require Unity profiler evidence; static evidence now shows zero forced-complete findings.

## Decision 048 - Merged Mesh Single Validated Acquire

Problem: Merged mesh construction validated source mesh layout and then reacquired read-only mesh data during copy, reopening a native snapshot after the first validation. That widened native lifetime reasoning and made readability failure handling inconsistent.
Solution: Add `CanReadMeshData`, `ValidateMeshDataLayout`, and `TryAcquireValidatedMeshData`. The copy pass now acquires a readable mesh snapshot once, validates it once, copies from that snapshot, and disposes in the same bounded method scope.
Rejected Alternatives: Keeping a separate validate-then-acquire path is rejected because it can observe different mesh state and doubles native snapshot traffic. Copying source mesh data into managed staging memory is rejected because it would allocate and duplicate ownership.
Scalability potential: Low devices avoid redundant native snapshot acquisition during cold wreck mesh assembly. Middle/High/Ultra keep the same visual density and use the saved budget only through existing continuous `GlobalQualityWeight` paths.
Hardware Impact: One redundant `Mesh.AcquireReadOnlyMeshData` per copied readable mesh is removed. Exact microseconds depend on Unity mesh backend and require profiler capture.

## Decision 049 - Copy Count Naming After Job Removal

Problem: After forced copy jobs were replaced by direct kernel execution, the local name `scheduledJobCount` still described a scheduler that no longer exists. That is a maintenance hazard in a domain where hidden `.Complete()` and same-frame schedule/readback loops are explicitly forbidden.
Solution: Rename the counters to `mergeableMeshCount` and `copiedMeshCount`, and harden the scanner so stale `scheduledJobCount` cannot return in `ProceduralWreckGenerator.cs`.
Rejected Alternatives: Leaving the name as harmless is rejected because future agents could reintroduce scheduling around the stale concept. Adding comments instead of renaming is rejected because the code should state the actual execution model.
Scalability potential: Runtime behavior is unchanged across Low/Middle/High/Ultra. The value is proof clarity for future optimization passes.
Hardware Impact: Runtime cost is 0 us. It removes ambiguity, not CPU work.

## Decision 050 - Near-Field Pickup State Before Loot Side Effect

Problem: `ProcessNearFieldDebris()` could enqueue a wreck loot spawn before `_debrisRecords.TrySet()` proved the `ActivePickup` flag was written. If the Vault write failed after the enqueue, the same debris could be processed again and duplicate loot.
Solution: Split the route into a pure read phase and a mutation phase. `TryResolveDebrisPickupSpawn()` only resolves cached prefab/item data and spawn transform. The method exits read-only view scope, writes `ActivePickup` to the Vault, then calls `QueueWreckLootSpawn()`. If queueing fails unexpectedly, it clears `ActivePickup`; if that rollback fails, numeric telemetry flag 55 is written.
Rejected Alternatives: Keeping enqueue inside the read scan is rejected because it mixes pure Vault reads, mutable Vault state, and scene side effects. Holding a write lock across loot queueing is rejected because queueing is outside the debris buffer authority route.
Scalability potential: Low devices keep sliced near-field scans with no duplicate pickup truth. Middle/High/Ultra can scale visible debris count without multiplying side-effect risk.
Hardware Impact: Expected runtime cost is neutral. The correction adds one rollback path only on queue failure and prevents duplicate spawn recovery work.

## Decision 051 - Render Payload Vault Staging

Problem: BRG render payload generation still used direct `new NativeArray<T>` staging constructors in `ProceduralWreckGenerator.cs`. They were cold, but still outside the DataVault route and invisible to compaction/handle proof.
Solution: Add vault-backed render payload buffers 132813-132815 for matrices, module IDs, and ages; lock each buffer only for direct kernel execution and copy into cold managed snapshots before renderer publication.
Rejected Alternatives: Temp native arrays are rejected because they duplicate native ownership outside the Vault. Passing Vault views into `WreckMaterialRegistry.Publish()` after releasing locks is rejected because those aliases can become stale after compaction.
Scalability potential: Low devices avoid cold native allocation spikes during wreck render publication. Middle/High/Ultra keep higher BRG instance counts through continuous quality budgets without unmanaged staging churn.
Hardware Impact: Direct native constructor count in `ProceduralWreckGenerator.cs` is now 0. Exact frame-time gain requires profiler capture; expected benefit is allocator-risk removal.

## Decision 052 - Mesh Metadata Validation And Artifact Slicing

Problem: Merged mesh count validation acquired native read-only mesh data just to inspect layout, and artifact discovery scanned the whole artifact buffer per slow tick.
Solution: Validate source mesh vertex layout via `Mesh.GetVertexAttribute*` metadata in the count pass, acquire read-only mesh data only in the copy pass, keep color optional, and process artifact discovery with a quality-scaled cursor slice.
Rejected Alternatives: Requiring vertex color on all meshes would reject valid structural assets. Full artifact scans are rejected because artifact discovery is a belief trigger, not a per-frame physics truth.
Scalability potential: Low uses small artifact slices and lower debris budgets; Middle/High/Ultra spend quality weight on denser fragments and discovery responsiveness without binary switches.
Hardware Impact: Removes one read-only mesh data acquisition per candidate mesh in the count pass and bounds artifact discovery work per SlowTick. Exact microseconds require Unity profiler evidence.

## Decision 053 - Scanner Gate For External Side-Effect Ordering

Problem: The scanner did not detect whether external side effects occurred before Vault state commits.
Solution: Add `externalSideEffectCommitOrder` gate to `.tmp/agent1328_static_scan.js`, checking artifact discovery and burial voxel cut ordering.
Rejected Alternatives: Manual review alone is rejected because this exact bug survived prior passes.
Scalability potential: No visual tier change. The gate prevents future duplicate side-effect regressions across all quality settings.
Hardware Impact: Runtime cost is 0. Workstation scan cost remains sub-second to low-second under Node.

## Decision 054 - Renderer Publish Outside Vault Write Locks

Problem: `PublishBrgScatterPayload()` and `PublishWreckRenderPayload()` built render payloads in Vault buffers but still called `WreckMaterialRegistry.Publish()` before releasing `_renderWorldMatrices`, `_renderModuleIds`, and `_renderAges` write locks. The registry path can configure BRG batches, allocate or resize managed staging arrays, lock `GraphicsBuffer`, set material buffers, and update `BatchRendererGroup` bounds, so it is not DataVault-owned work.
Solution: Copy render payload views into cold fixed managed snapshots while the Vault locks are held, release all render locks in `finally`, then call new managed-array `WreckMaterialRegistry.Publish()` overloads. Add `renderPayloadPublishLocks` scanner gate and include `WreckMaterialRegistry.cs` in the 1328 scan/hash scope.
Rejected Alternatives: Holding write locks across registry publish is rejected because it blocks compaction behind renderer work. Reacquiring read-only Vault views for publish is rejected because the registry is not the DataVault owner and the publish path can run graphics side effects. Keeping registry-side native list staging is rejected because it reintroduces persistent native ownership in a renderer coordinator.
Scalability potential: Low devices get shorter compaction-blocking windows around cold wreck publish. Middle/High/Ultra keep the same continuous BRG instance budgets and can spend saved lock residency on denser visible wreck scatter.
Hardware Impact: Expected gain is reduced worst-case Vault write-lock residency during wreck render publication. Exact microseconds require Unity profiler evidence; static proof now shows renderer publish occurs after all three render lock releases.

## Decision 055 - BRG Metadata Temp Native Allocation Purge

Problem: After adding `WreckMaterialRegistry.cs` to the 1328 scan scope, broad DataVault audit still identified a direct method-local `new NativeArray<MetadataValue>(..., Allocator.Temp)` in `ModuleBatch.EnsureResources()`. It was short-lived and disposed, but it still forced a native constructor in the renderer contract touched by this pass.
Solution: Add DataVault-backed BRG metadata buffer 132816 owned by `SystemID.WorldStreaming`; acquire it through `TryAcquireWriteLock`, fill the single `MetadataValue`, call `BatchRendererGroup.AddBatch`, and release the write lock in `finally`. Release the handle on batch disposal and DataVault hot-swap.
Rejected Alternatives: Stackalloc plus `NativeArrayUnsafeUtility.ConvertExistingDataToNativeArray` is rejected because it relies on undocumented BRG metadata lifetime and safety-handle plumbing. Keeping the Temp constructor is rejected because the direct dependency now participates in the agent scanner and hash scope.
Scalability potential: Low devices avoid one cold Temp native allocation during each wreck BRG batch registration. Middle/High/Ultra keep the same renderer capacity while moving metadata ownership onto the same Vault route as the payload.
Hardware Impact: Removes one direct native constructor from `WreckMaterialRegistry.cs`. DataVault broad audit dropped from 162 to 161 direct constructors and no longer reports `WreckMaterialRegistry.cs`; exact runtime cost requires profiler evidence.

## Decision 056 - Central BufferID Contract And Metadata Fence

Problem: The wreck generator buffers were collision-free numerically, but the runtime files still declared them as local numeric `(BufferID)1328xx` casts. That bypasses the central enum contract and makes future duplicate audits weaker. The BRG metadata lock route also relied on `GlobalDataVault.TryAcquireWriteLock` internals instead of a visible caller-side compaction-fence check before and after lock acquisition.
Solution: Add `WreckGenerator*` and `WreckBrgBatchMetadata` entries for 132800-132816 to the central `BufferID` enum, replace local casts with enum identifiers, and harden `WreckMaterialRegistry.TryAcquireBatchMetadata()` with explicit `vault.IsCompactionFenceActive` checks around `TryAcquireWriteLock`. `EnsureBatchMetadataBuffer()` now reuses an existing handle through `TryGetGenerationHandle` before calling `EnsureGenerationHandle`.
Rejected Alternatives: Keeping local numeric casts is rejected because it hides ownership in each runtime file. Editing unrelated World local casts is rejected because those are outside 1328 ownership and would collide with other agents. Trusting only the Vault implementation for the fence is rejected because this domain's lock evidence must be visible at the call site.
Scalability potential: Low devices keep the same visual budget with stronger fail-closed metadata setup. Middle/High/Ultra can keep larger BRG payloads without weakening buffer identity or compaction safety. No gameplay truth changes.
Hardware Impact: Runtime cost is neutral except two cheap fence reads in a cold BRG metadata path. The practical gain is contract safety: project-wide duplicate audit reports `duplicates=0`, and the 1328 scanner now blocks local numeric BufferID regressions.

## Decision 057 - Editor Validator Dependency Scope

Problem: The Unity editor memory-sovereignty validator still scanned only `ProceduralWreckGenerator.cs`, even though the 1328 implementation now depends on `WreckMaterialRegistry.cs` and central `BufferID` entries in `H8Memory.cs`. That created a proof gap where the Node scanner could fail while the Unity menu validator emitted a misleading green report.
Solution: Extend `ProceduralWreckGeneratorMemorySovereigntyValidator1328` to read `WreckMaterialRegistry.cs` and `H8Memory.cs`, include dependency SHA-256 values in the report, validate the registry metadata buffer ID route, and write report JSON with a no-BOM `UTF8Encoding`.
Rejected Alternatives: Keeping the editor validator narrow is rejected because it is a false proof artifact after direct-dependency edits. Replacing the validator with only the Node script is rejected because Unity-side layout offsets still need an editor validation entry point.
Scalability potential: Runtime behavior is unchanged across Low/Middle/High/Ultra. The value is preventing future proof drift when renderer and memory-contract files change.
Hardware Impact: Runtime cost is 0 us. Editor validation reads two more small source files; this is outside frame time.

## Decision 058 - Frustum Scratch Prewarm And Camera Fast Path

Problem: `WreckMaterialRegistry.TryPopulateFrustumPlanes()` could allocate `Plane[6]` and `float4[6]` the first time visibility upload ran, and `ResolveViewCamera()` could recurse through the player hierarchy even when a live `_viewCamera` was already cached.
Solution: Prewarm frustum scratch arrays in `Awake()` and `OnEnable()`, add a cached active camera fast path before `WorldRuntimeReferenceUtility.TryResolvePlayerTransform()` and `ComponentReferenceUtility.ResolveOwnedComponent<Camera>()`, and harden the scanner with `frustumScratchAndCameraFallback`.
Rejected Alternatives: Leaving allocation to first visibility upload is rejected because it hides a managed allocation in a renderer hot path. Removing the fallback entirely is rejected because startup order can still publish before the player runtime context has a camera.
Scalability potential: Low devices avoid a first-upload managed allocation and hierarchy walk. Middle/High/Ultra keep identical visual output and use the same continuous BRG instance budgets.
Hardware Impact: Removes two first-use managed array allocations from the visibility path and skips recursive camera lookup when cached. Exact microseconds require Unity profiler evidence; static impact is zero new hot-path allocations in that path.

## Decision 059 - Renderer Upload Prewarm Before Visibility Culling

Problem: `ModuleBatch.Publish()` sized GPU upload buffers from the current visible subset. If the initial cull saw zero or few visible instances, a later `LateFrameTick` visibility refresh could grow `GraphicsBuffer` objects in the visibility path. The registry also returned before publish when no camera/frustum existed, leaving first camera availability as a potential allocation trigger.
Solution: Add `ModuleBatch.PrepareUploadResources()` that creates BRG resources and matrix/age upload buffers using the full `_matrixCount` capacity. Call it before frustum/cull in single-batch and multi-batch publish paths, and harden the scanner with `rendererUploadPrewarm` so buffer sizing cannot regress to `visibleCount`.
Rejected Alternatives: Deferring buffer creation until first visible subset is rejected because it moves graphics allocation into a frame-visible path. Allocating max global capacity for every batch is rejected because `GlobalQualityWeight` and authored `maxInstancesPerWreckBatch` already bound the full actual batch count.
Scalability potential: Low devices avoid late `GraphicsBuffer` growth when visibility expands. Middle/High/Ultra keep the same visual density controls and pay allocation in the cold publish phase, not the upload refresh phase.
Hardware Impact: Removes a possible first-visible buffer allocation from `LateFrameTick`. Exact microseconds require Unity profiler evidence; static proof now shows buffers are prepared from `_matrixCount` before frustum culling.

## Decision 060 - Build Timeout Handling

Problem: The build lane was initially clear, but `dotnet build Hecton8.slnx -nologo -clp:ErrorsOnly -maxcpucount:1` did not return within 124 seconds and left a live `dotnet.exe` process consuming CPU. Leaving that process alive violates the shared workstation guard.
Solution: Waited additional intervals, confirmed the same command line remained active, stopped only the agent-launched build PID, and ran `dotnet build-server shutdown`. Recorded `.tmp/agent1328_build_timeout.log` and included it in the scanner hash scope.
Rejected Alternatives: Reporting build success is rejected because no result was produced. Leaving the orphan build running is rejected because it blocks other agents and violates the compiler-lane rule.
Scalability potential: No runtime behavior change. This protects shared development throughput.
Hardware Impact: Stops an orphaned compiler workload. Runtime microsecond impact is 0.

## Decision 061 - Registry Teardown Forced-Complete Purge

Problem: `WreckMaterialRegistry.ModuleBatch.Dispose()` still called a `Dispose(default)` overload, then conditionally invoked `DispatcherJobSwap.TryComplete(forceComplete:true)` even though the overload only returned the dependency. This was dead synchronization surface and kept a false forced-complete route in a direct dependency.
Solution: Remove the overload and perform teardown directly in `Dispose()`: unregister BRG handles, release graphics buffers, release owned runtime material, clear managed snapshots, and release the DataVault metadata handle.
Rejected Alternatives: Keeping the dead forced-complete path as "teardown only" is rejected because static job-completion proof must not contain fake synchronization surfaces. Scheduling disposal jobs is rejected because the current method disposes Unity graphics objects and managed references that are not Burst job data.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The gain is proof clarity and no false same-frame completion route in renderer teardown.
Hardware Impact: No frame-time saving claimed. Targeted `JobCompletionAudit` for `WreckMaterialRegistry.cs` now reports findingCount=0.

## Decision 062 - Terrain Snap Absolute-Y Correction

Problem: The terrain snap path performed double-precision `absolute - originAbsolute` before casting X/Z, but it wrote `terrainHeight + offset` directly into `AbsoluteUniversePosition` Y. `MapMagicRuntimeBridge.TryGetHeight(float x,z)` returns runtime-space terrain height, so any non-zero floating-origin Y would place wreck anchors at the wrong absolute altitude.
Solution: Keep the origin-relative X/Z query, compute `snappedAbsoluteY = originAbsolute.y + terrainHeight + terrainSnapVerticalOffsetMeters`, verify the new AUP and runtime float3 conversion, then commit both AUP and runtime origin.
Rejected Alternatives: `TryGetHeightAUP(Vector3 absoluteUniversePosition)` is rejected here because it accepts an already-float absolute vector before this domain can prove double-origin subtraction. Assuming origin Y is always zero is rejected because AUP code must not rely on scene setup.
Scalability potential: Low/Middle/High/Ultra behavior is visually stable at large-world offsets; quality settings do not change placement truth.
Hardware Impact: Runtime cost is neutral. The change prevents large-origin vertical placement error rather than saving CPU time.

## Decision 063 - Proof Hash Scope And Current Compile Wall

Problem: New job/DataVault audit files and the latest build output were touched proof artifacts but were not yet part of the scanner verification hash. A second guarded build also timed out after producing only plugin/editor compile errors, so the compile state needed a fresh disk record.
Solution: Add the audit reports and latest build logs to `.tmp/agent1328_static_scan.js` optional hash scope. Rerun the guarded build only after CPU/process guards passed; kill only the launched PID on timeout and shut down build servers. Filter the resulting log for 1328-owned symbols.
Rejected Alternatives: Hashing only source files is rejected because the final proof depends on generated audit artifacts. Editing AmplifyImpostors or MapMagic editor/plugin compile errors is rejected because those are outside 1328 domain ownership.
Scalability potential: No runtime behavior change. The proof chain now covers the evidence used for all device-tier claims.
Hardware Impact: Runtime cost is 0. Workstation impact is one single-worker build attempt that timed out after 240 seconds and was cleaned up.

## Decision 064 - False Async Proxy Mesh Wrapper Purge

Problem: `BuildProxyMeshAsync()` and `ResolveNavigationProxyMeshAsync()` survived as `async Awaitable<Mesh>` wrappers after proxy mesh readback was converted to direct bounded execution. They had no `await`, so they carried a false async contract, compile-warning surface, and unnecessary state-machine risk around cold navigation proxy generation.
Solution: Remove both wrappers and call `ResolveNavigationProxyMesh()` directly from `GenerateInternalAsync`. Add `asyncWithoutAwait` to `.tmp/agent1328_static_scan.js` by preserving method signatures during extraction and failing any `async` method whose body has no `await`.
Rejected Alternatives: Keeping the names for semantic symmetry is rejected because the signature lied about scheduling behavior. Replacing them with an `Awaitable.FromResult` style wrapper is rejected because no project-local contract requires it and it would preserve the false async surface.
Scalability potential: Low devices remove unnecessary cold generation overhead. Middle/High/Ultra visual output is unchanged; saved overhead remains available to the existing continuous `GlobalQualityWeight` paths.
Hardware Impact: No profiler-backed microsecond number claimed. Static gain is removal of two no-await async wrappers and one scanner blind spot.

## Decision 065 - Generation Vault Epoch Fence

Problem: `GenerateInternalAsync()` could yield, then continue after a GlobalDataVault hot-swap because `_initialized` and recreated wreck buffers made `CanContinueGeneration()` pass against a different physical allocation generation.
Solution: Add `_wreckVaultEpoch` and `_activeGenerationVaultEpoch`. `TryBeginGeneration()` captures the current epoch, `ReleaseWreckVaultBuffers()` increments the epoch before handles are released, `CanContinueGeneration()` requires exact epoch equality, and `EndGeneration()` clears the active epoch.
Rejected Alternatives: Holding a Vault lock across async generation is rejected because it blocks compaction across frames. Relying on `_initialized` is rejected because a hot-swap can rebuild valid buffers for a different generation. Throwing/canceling through managed exceptions is rejected in production simulation paths.
Scalability potential: Low/Middle/High/Ultra visual output is unchanged. The change protects all tiers from stale continuation after memory relocation or service replacement.
Hardware Impact: Runtime cost is two volatile reads in async continuation checks and one interlocked increment on buffer release. No profiler-backed microsecond number claimed; prevented failure is stale native-view continuation after hot-swap.

## Decision 066 - Generated Wreck Mesh Ownership Cleanup

Problem: Sync and async wreck generation could create local generated meshes, then fail before returning `WreckageData` due to disable, hot-swap, stale Vault state, or Unity API failure. Those meshes would not be owned by the caller and could leak Unity native mesh memory.
Solution: Track `combinedMesh`, `essentialMesh`, `detailMesh`, `clutterMesh`, and `proxyMesh` as local owned resources until `meshOwnershipTransferred=true`. Both `GenerateInternal()` and `GenerateInternalAsync()` release non-transferred generated meshes in `finally`; authored `wreckCollisionProxyMesh` is preserved by destroying the proxy only when it was generated locally.
Rejected Alternatives: Leaving cleanup to the caller is rejected because no `WreckageData` exists on aborted generation. Destroying any proxy mesh unconditionally is rejected because authored collision proxy assets are not owned by generation.
Scalability potential: Low devices avoid creeping mesh memory after cancellation or hot-swap. Middle/High/Ultra can keep higher visual wreck mesh budgets without leak amplification.
Hardware Impact: Steady-state frame cost is 0 outside failed generation. Exact microseconds saved are not claimed; the concrete gain is bounded Unity native mesh lifetime on aborted generation.

## Decision 067 - Mesh Apply Result Failure Cleanup

Problem: `BuildMergedMesh()`, `BuildMergedMeshAsync()`, and `BuildProxyMesh()` created `new Mesh()` immediately before `Mesh.ApplyAndDisposeWritableMeshData()`. If apply or post-apply bounds setup failed before returning, the local Mesh object was not transferred and was not destroyed.
Solution: Move local mesh result variables outside the try block, track `meshOwnershipTransferred`, dispose `writableMeshData` only when not applied, and call `ReleaseGeneratedMesh(result)` when the result was not transferred. Add `appliedMeshResultCleanup` to the static scanner.
Rejected Alternatives: Assuming Unity mesh apply never fails is rejected because the code already handles writable mesh data disposal defensively. Adding catch blocks is rejected; `try/finally` gives deterministic cleanup without managed exception handling policy in the hot/cold path.
Scalability potential: Low devices avoid native mesh leaks in rare apply failures. Middle/High/Ultra retain the same generated mesh fidelity and only improve failure hygiene.
Hardware Impact: Normal successful generation adds one boolean write per mesh result. No profiler-backed microsecond saving claimed; the benefit is prevented native mesh leak under failed apply.

## Decision 068 - Async Generation Side Effects Behind Final Continuation Gate

Problem: `GenerateInternalAsync()` and `GenerateBrgWreckOnlyAsync()` yielded after local generation work, then could still execute external side effects (`PrepareWreckWorldState`, renderer publish, collision proxy publish, loot enqueue). If the component was disabled or the DataVault epoch changed during that final yield, later code could return `default` while already-published external state survived.
Solution: Move the final `YieldAfterGenerationStageAsync()` and `CanContinueGeneration()` checks before all external publish calls. After BRG/collision/loot/world-state publication begins, the async methods no longer yield or return `default` before returning their generated data. Add `asyncGenerationSideEffectsAfterAwait` to the scanner.
Rejected Alternatives: Adding rollback code for renderer/collision/loot after side effects is rejected because those routes cross different owners and would create more failure windows. Holding Vault locks or object-pool leases across the final await is rejected because it blocks compaction and violates phase boundaries.
Scalability potential: Low devices avoid stale partial wreck publication after disable/hot-swap. Middle/High/Ultra retain the same continuous visual budgets; the change protects publication ordering independent of quality.
Hardware Impact: Normal path cost is unchanged except earlier continuation checks. No profiler-backed microsecond saving claimed; the prevented cost is stale external state after async cancellation.

## Decision 069 - Registry Player Runtime Context Route

Problem: `WreckMaterialRegistry.SlowTick()` and `ResolveViewCamera()` reached into `PlayerRuntimeContextService.TryGetActiveRuntimeContext()` during runtime phases. The method is cheap, but it bypasses the project authority rule that consumers cache runtime context interfaces from `GlobalRegistry` during cold setup or hot-swap.
Solution: Add cached `IPlayerRuntimeContext _playerRuntimeContext`, populate it from `GlobalRegistry.Player` in `Awake()`/`OnEnable()`, update it on `GlobalRegistryServiceSlot.Player` hot-swap, and route player AUP/camera reads through `PlayerMovement`, `TryGetMovementRuntimeState()`, `PlayerCamera`, and `PlayerTransform` on the cached interface. Add `registryPlayerContextRoute` to the scanner.
Rejected Alternatives: Leaving the static service lookup is rejected because it normalizes pull-based runtime ownership in a renderer/tick path. Removing the transform fallback entirely is rejected because startup order may still leave camera data unavailable before the player context publishes.
Scalability potential: Low devices avoid repeated runtime context service pulls and hierarchy fallback when cached context is valid. Middle/High/Ultra keep the same wreck signal and BRG visibility behavior with cleaner ownership boundaries.
Hardware Impact: Expected runtime gain is small and unmeasured; the concrete benefit is route correctness and one less direct static service lookup in `SlowTick`/visibility resolution.

## Decision 070 - Black-Box Dump Fail-Closed Writer

Problem: The fault-dump route still used `BinaryWriter`, resolved `_telemetryEntries.TryGet()` once per entry, and unconditionally marked the dump written after `DumpBlackBox()` returned. A denied path, locked file, or unsupported path could throw from `LateFrameTick()`/`OnDisable()` or suppress future dump attempts while no dump existed.
Solution: Convert `DumpBlackBox()` to a `bool` success/failure result, add `_blackBoxDumpFailed`, reset dump state during fresh wreck-state preparation, resolve one read-only telemetry view before writing, write the binary format with explicit little-endian stack-span helpers, and catch only specific `IOException`, `UnauthorizedAccessException`, `NotSupportedException`, and `System.Security.SecurityException` cases.
Rejected Alternatives: Keeping `BinaryWriter` is rejected because it allocates a managed helper in a fault path and hides byte order behind framework behavior. Catching `Exception` is rejected because the production policy forbids broad managed exception swallowing. Preallocating a permanent managed byte buffer is rejected because the dump path is rare and the existing stack-span primitive writer is simpler and bounded.
Scalability potential: Low devices avoid cascading crashes during telemetry dump after a NaN/fault. Middle/High/Ultra keep the same black-box evidence path with no extra per-frame simulation cost; the fix only changes the deferred dump writer.
Hardware Impact: Steady-state frame cost is 0. Fault flush removes `BinaryWriter` allocation and per-entry vault handle resolution; exact microseconds require profiler evidence, but the concrete gain is fail-closed dump behavior and no false "written" state on I/O failure.

## Decision 071 - Wreck Integrity Proxy Registry Latch

Problem: The collision proxy is spawned through `ObjectPoolManager`, which calls `SetActive(true)` and `OnSpawn()` before `ProceduralWreckGenerator.ConfigureIntegrityProxy()` sets the owner and wreck identity. `WreckIntegritySignalProxy.OnSpawn()` registered the collider tree once with incomplete owner state, then `Configure()` registered it again after collider setup. `RegisterCollider()` is keyed and idempotent, but `RegisterTree()` still performs an avoidable child-collider traversal and target resolution. The proxy also lacked an `OnDisable()` invalidation fallback if it is disabled outside normal pool despawn.
Solution: Add `_interactableRegistered` and route all registry mutations through `TryRegisterInteractableTree()` / `UnregisterInteractableTree()`. Registration now requires an owner, non-zero wreck id, active enabled state, and an unset latch. `Configure()` unregisters first when rebinding to a different owner/id, then tries a guarded register. `OnSpawn`, `OnEnable`, `OnDespawn`, `OnDisable`, and `OnDestroy` now share the same idempotent lifecycle path.
Rejected Alternatives: Relying on `InteractableRegistry.RegisterCollider()` idempotence is rejected because the tree traversal and target-info resolution still happen twice. Registering only in `Configure()` is rejected because pool lifecycle can reactivate an already configured object. Registering only in `OnSpawn()` is rejected because owner identity is not available at that point.
Scalability potential: Low devices avoid duplicate collider-tree scans when wreck proxies spawn. Middle/High/Ultra get the same interaction behavior with tighter registry lifetime and no visual-quality branch.
Hardware Impact: Static estimate saves one `GetComponentsInChildren` pass and one target-info resolution pass per collision proxy spawn, roughly 3-20 us depending collider count on i3/MX350-class hardware. Exact profiler proof remains pending; the guarded solution build timed out after 240 seconds and partial logs contained 93 external plugin/editor errors with zero 1328-owned symbol matches.

## Decision 072 - BRG Module Batch Payload-Sized Capacity

Problem: `WreckMaterialRegistry.EnsureBatches()` created 16 module batches and immediately allocated matrix/age/visible staging arrays at `maxInstancesPerWreckBatch` for every slot. The default was 2048 instances per slot even though the generator already clamps actual render payload by `GlobalQualityWeight` to roughly 50-250 placements/fragments. Multi-batch publish also configured empty slots before seeing any module ids.
Solution: Make module staging lazy and payload-sized. `EnsureBatches()` now creates only the 16 lightweight owners. `ModuleBatch.EnsureCapacity()` clamps requested payload capacity against the authored maximum and grows only when the existing arrays are too small. Both managed and native publish overloads configure a module batch only when the first real instance for that module id is routed, using a 16-bit integer mask instead of a managed set.
Rejected Alternatives: Keeping eager 16x2048 arrays is rejected because it burns low-end memory before a wreck is even visible. Shrinking arrays on every smaller payload is rejected because it creates managed allocation churn across generations. Allocating per-frame visible subsets is rejected because upload buffers must be ready before visibility refresh.
Scalability potential: Low uses staging near the actual reduced payload. Middle/High/Ultra can grow to their authored payload ceiling without startup memory waste. The capacity route follows the continuous generator payload, not a binary quality tier.
Hardware Impact: Static memory reduction for default startup is roughly 16 * (2 Matrix4x4 arrays + 2 float arrays) * (2048 - payload) entries avoided. At a 250-instance payload this avoids about 3.7 MB of managed staging arrays on startup before object overhead. Runtime microseconds require Unity profiler proof.

## Decision 073 - Interleaved Unity Mesh Attribute Support

Problem: The fallback merged mesh path validated source mesh attributes as if every attribute lived in a separate vertex stream with offset 0 and stride exactly `sizeof(T)`. Standard imported Unity meshes commonly interleave position, normal, uv, and color in one stream with non-zero offsets. The old validator could silently reject valid meshes and produce no fallback wreck geometry when BRG is unavailable.
Solution: Make the merge job offset/stride aware. `CombineMeshDataJob` now reads Position/Normal/UV/Color through `NativeArray<byte>` stream pointers and `UnsafeUtility.ReadArrayElementWithStride<T>()`. Mesh validators now prove format/dimension plus `offset + sizeof(T) <= stride`, and job initializers pass attribute offsets and strides explicitly.
Rejected Alternatives: Forcing artists to author separate streams is rejected because it is brittle and contrary to normal Unity import layouts. Copying source mesh vertices into managed arrays is rejected because it allocates and duplicates memory. Keeping only procedural fallback colors is insufficient when normals/uvs exist and are valid.
Scalability potential: Low can still use the cheap merged fallback without BRG-specific assets. Middle/High/Ultra keep BRG as the preferred visual path, but fallback correctness no longer depends on unusual mesh import settings.
Hardware Impact: Normal merge cost adds a few stride/offset reads in cold mesh construction. The concrete gain is avoiding silent fallback mesh loss. Runtime frame cost remains 0 because the merge path is generation-time, not Tick/SlowTick/LateFrameTick.

## Decision 074 - Placement Storage Clamped To Actual Wreck Cap

Problem: `GlobalQualityWeight` limited active wreck placements to a 50-250 range, but the serialized `maxPlacements` field only had a lower bound. A bad inspector value could allocate larger DataVault placement/artifact/scorch/burial buffers and a larger `Mesh.MeshDataArray[]` snapshot pool than the generator could ever use.
Solution: Add `MaxScalabilityPlacementCap = 250`, clamp `maxPlacements` in `OnValidate()` and `Initialize()`, and allocate all placement-derived Vault buffers and mesh snapshot storage from `placementStorageCapacity`. The default `maxPlacements` now uses the same cap instead of a nearby uncapped 256.
Rejected Alternatives: Keeping authoring capacity separate from the active cap is rejected because it spends memory on impossible output. Raising the quality cap is rejected without profiler/VRAM proof. Dynamically resizing storage per quality frame is rejected because it would reintroduce DataVault churn.
Scalability potential: Low allocates only enough placement storage for the continuous cap and then uses the lower active quality limit. Middle/High/Ultra still reach the full 250 placement visual envelope without letting serialized data inflate memory.
Hardware Impact: Prevents unbounded cold Vault/native storage and managed snapshot allocation from a bad `maxPlacements` value. Default runtime behavior is unchanged except 250 storage slots instead of 256, matching the already-active quality cap.

## Decision 075 - Merged Mesh Copy Completeness Gate

Problem: The merged fallback mesh path counted valid source meshes in one pass, then reacquired mesh data in a second pass and trusted that every counted mesh was copied. If an asset became unreadable, invalid, or otherwise failed validation between passes, the final mesh could apply buffers sized for more vertices/indices than were actually written.
Solution: Add explicit overflow checks during count accumulation and track `copiedMeshCount`, `vertexOffset`, and `indexOffset` through the copy pass. Both sync and async mesh builders now return `null` with numeric WFAI telemetry unless copied mesh count and final offsets exactly match the pre-counted geometry.
Rejected Alternatives: Applying partially filled buffers is rejected because it can emit default vertices/indices as corrupt geometry. Reacquiring/readback retries are rejected because source mesh readability is an asset contract and the generator should fail closed. Catching broad exceptions is rejected by the no-throw production rule.
Scalability potential: Low avoids wasting CPU/GPU upload on corrupt fallback mesh buffers. Middle/High/Ultra retain the same mesh fidelity when assets are valid and fail closed when the asset contract is broken.
Hardware Impact: Adds integer checks in cold mesh construction only. No frame-time saving is claimed; the gain is deterministic failure instead of undefined visual geometry or native mesh apply risk.

## Decision 076 - Renderer Publish Raw Native Surface Removed

Problem: `WreckMaterialRegistry` still exposed public `Publish` overloads accepting `NativeArray<Matrix4x4>`, `NativeArray<byte>`, and `NativeArray<float>` even though the generator now copies render payloads into managed snapshots after releasing Vault locks. The overloads were unused and preserved a raw native cross-domain API surface.
Solution: Remove the `NativeArray<T>` publish overloads and keep only the managed snapshot route. Add `nativeArrayPublishOverload` to the static scanner so this surface cannot reappear unnoticed.
Rejected Alternatives: Keeping the overloads as future extension points is rejected because future work must use an explicit route card, not dormant raw native APIs. Making them private is rejected because no internal caller remains and it still preserves dead code.
Scalability potential: Low/Middle/High/Ultra rendering behavior is unchanged. The benefit is tighter ownership: renderer upload receives a stable post-lock snapshot, not caller-owned native views.
Hardware Impact: Runtime cost is unchanged on the active path. The concrete gain is eliminating an API route that could hold or misuse native views across render publication.

## Decision 077 - Loot Prefab Prewarm After Vault Commit

Problem: `RefreshLootRecords()` queued `ItemCatalog.QueueWorldPrefabPrewarm()` and refreshed loaded prefab cache before `_lootRecords` was written under a Vault lock. If the Vault write failed, Addressables prewarm side effects could survive for loot records that were not committed.
Solution: Stage loot DTOs and item data first, commit the loot Vault buffer under one write lock, release the lock in `finally`, then call `RefreshCommittedLootPrefabCache()` to queue prewarm and refresh loaded prefab references only for committed loot slots.
Rejected Alternatives: Keeping prewarm before the lock is rejected because external side effects must not outrun authoritative state. Calling prewarm inside the write-lock window is rejected because catalog/Addressables work must not happen while a Vault buffer is pinned.
Scalability potential: Low devices avoid wasted prewarm tickets after failed generation. Middle/High/Ultra keep the same loot presentation path; the route is cleaner and still fail-closed when prefabs are not loaded.
Hardware Impact: No frame-time saving is claimed. The change prevents cold Addressables work from being triggered for uncommitted loot and keeps the Vault lock window free of catalog calls.
