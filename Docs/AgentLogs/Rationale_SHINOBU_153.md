# Rationale SHINOBU_153

Date: 2026-05-20
Status: PENDING VERIFICATION

## Current Prompt Authority

Problem: `Docs/Tasks/CURRENT_BATCH.md` rotated after the original SHINOBU_153 extraction; a fresh scan no longer finds `<AGENT_PROMPT id="SHINOBU_153"...>`.
Solution: Ignore neighboring active prompts and continue from the user-supplied SHINOBU_153 mandate plus `Status_SHINOBU_153.md`, this rationale file, and `LOG_SHINOBU_153.md`.
Rejected Alternatives: Inferring from `SHINOBU_200+` prompts; changing domain because CURRENT_BATCH rotated; deleting existing SHINOBU_153 work.
Scalability potential: No runtime effect; prevents architectural bleed from unrelated core-threading batch prompts.
Hardware Impact: No code-path impact. This is assignment integrity only.

## Decision 01 - Remove Stored Ore Coordinates

Problem: A 100x100x10 km world cannot persist every ore vein coordinate without bloating save/runtime memory and making rollback snapshots coordinate-heavy.
Solution: Regenerate unmined ore from `worldSeed ^ AUP sector hash ^ deterministic slot` through pure integer mixing plus LCG-style avalanche steps. Persist only candidate-slot depletion masks.
Rejected Alternatives: Storing per-vein `double3`; hydrating ore prefabs at sector load; using Unity random.
Scalability potential: Low uses core nodes only; middle/high/ultra add visual cluster matrices around the same gameplay node.
Hardware Impact: MX350/i3 avoids world-coordinate corpus scans; expected win is memory bandwidth and load-time stability, not a measured runtime claim.

## Decision 02 - Vault Sovereignty

Problem: Private persistent NativeArrays are invisible to Vault relocation, rollback, and blackbox tooling.
Solution: Request runtime lanes `71530..71550` through `GlobalDataVault`; persistent class state is now pointer-free descriptor-only (`VaultGenerationHandle<T>` plus scalar counters). `NativeArray<T>` views are transient locals resolved at read-model, mutation, render-upload, dump, or job-schedule boundaries.
Rejected Alternatives: `new NativeArray(... Allocator.Persistent)` in `ProceduralOreSpawner`; per-sector managed collections.
Scalability potential: Same memory topology from weak devices to ultra; only visual density changes.
Hardware Impact: Stable arena ownership and generation-checked pointer-free descriptors reduce fragmentation and stale-pointer risk on low memory.

## Decision 03 - Explicit DTO Layout

Problem: Runtime DTOs entering NativeArray/GPU/telemetry must be ARM64-aligned and memcpy-stable.
Solution: `ResourceNodeDTO` is explicit 128 B; telemetry/tuning/rule/self-audit DTOs are 64 B; indirect args are 16 B. Editor validator checks offsets.
Rejected Alternatives: `Pack=1`, properties, bool fields, managed references.
Scalability potential: Gameplay truth stays compact; visual overkill uses matrix children, not bigger truth records.
Hardware Impact: 128 B node rows align to two 64 B cache lines; avoids unaligned ARM64 loads.

## Decision 04 - Dear Lie Visual Clusters

Problem: Simulating geological veins or per-crystal colliders would waste CPU on presentation.
Solution: One authoritative ore node spawns optional deterministic visual-only matrix children. Children carry `OreTypes=0` and clear with the parent deterministic slot.
Rejected Alternatives: Navier/erosion-style vein simulation; `GameObject` crystals; `MeshCollider` harvesting.
Scalability potential: q=0 collapses clusters to 0; q=1 allows up to five visual-only nodes per core.
Hardware Impact: Saved CPU is spent as GPU-instanced visual richness.

## Decision 05 - GPU Submission Route

Problem: Mesh-based CPU loops and indexed indirect args still rely on mesh asset shape and CPU-owned instance counts.
Solution: `GeologyIndirectArgsDTO` is written by Burst to Vault, copied to a `GraphicsBuffer`, and submitted with `Graphics.DrawProceduralIndirect`; shader expands 36 vertices per instance from `SV_VertexID`.
Rejected Alternatives: `RenderMeshIndirect`, `DrawMeshInstancedIndirect`, scene renderers, material property blocks.
Scalability potential: Same draw route scales from few matrices to dense visual clusters.
Hardware Impact: CPU does not instantiate or enumerate ore meshes; GPU receives raw matrices.

## Decision 06 - CSV Without Managed Byte Staging

Problem: `File.ReadAllBytes` created a managed `byte[]` despite a span parser.
Solution: Read CSV through `FileStream.Read(Span<byte>)` into Vault `CsvScratch` and parse `ReadOnlySpan<byte>`.
Rejected Alternatives: managed arrays, `string.Split`, LINQ, dictionaries.
Scalability potential: Designers can tune distribution/tuning without C# rebuild; runtime keeps fixed scratch memory.
Hardware Impact: Prevents cold heap spikes during editor/play bootstrap on low memory devices.

## Decision 07 - Deterministic Frame Metadata

Problem: `Time.frameCount` remained in signals/telemetry and is not rollback authority.
Solution: Use `TimeSliceScheduler.CurrentFrameId`; if dispatcher frame is zero, advance owner-local deterministic fallback.
Rejected Alternatives: Unity frame count, wall clock, `Time.deltaTime`.
Scalability potential: No visual/quality split here; state identity remains deterministic across tiers.
Hardware Impact: Eliminates a desync source.

## Decision 08 - HZB Boundary

Problem: The broad mandate requires HZB exclusion, but renderer-owned HZB implementations cannot be imported into the geology runtime without violating compile-wall ownership.
Solution: Add owner-local HZB readback lanes `71549` `GeologyHzbTileDTO[4096]` and `71550` `GeologyHzbMetaDTO[1]`. `GenerateResourceNodesJob` reads them through `[NoAlias, ReadOnly]` fields. If `HzbActiveFlag` is set, visual-only matrices are culled before they enter `ResourceMatrices`; authoritative-node cull requires explicit `HzbCullAuthoritativeFlag` so gameplay truth is not silently camera-dependent.
Rejected Alternatives: importing `HectonIndirectVegetationRenderer`; referencing procedural coral HZB DTOs across asmdef boundaries; faking HZB with no producer-owned depth basis.
Scalability potential: Low devices can publish a coarse depth pyramid and shed cosmetic cluster matrices; high/ultra can retain rich clusters only where visible.
Hardware Impact: Avoids vertex work for hidden visual-only crystals when HZB is resident; static source only, no profiler number.

## Decision 09 - Grounding Refinement Curve

Problem: Task 07 required gradient descent, while the previous implementation stopped at one height sample plus finite-difference normal.
Solution: Add `SampleGrounding()`: below q=0.3 it collapses to the original nearest height sample; higher quality executes up to two bounded gradient refinement steps using `math.step` and `math.lerp`.
Rejected Alternatives: `Physics.Raycast`, `MeshCollider`, unbounded descent loop, or 64-step SDF raymarch.
Scalability potential: Toaster path keeps one sample; middle/high/ultra buy better slope seating without changing sector seed truth.
Hardware Impact: Low tier pays no extra terrain iterations; high tier pays bounded ALU for visibly flush ore matrices.

## Decision 10 - Dump Alias And Hot Signal Assignment

Problem: XML Task 16 names `Dump_GEOLOGY_ARCHITECT.bin`, while global blackbox law names `Dump_SHINOBU_153.bin`. Hot depletion code also used value-type object initializers for signals.
Solution: Dump the same telemetry ring to both paths. Replace `new ItemAcquiredSignal {}` and `new ResourceDepletionDeltaSignal {}` with `default` plus field assignment.
Rejected Alternatives: choosing only one dump filename; keeping object-initializer syntax in gameplay interaction flow.
Scalability potential: No quality split; this is forensic compatibility and allocation-hygiene polish.
Hardware Impact: Crash-path IO only. Depletion signal assignment remains stack value writes.

## Decision 11 - Native View Eviction

Problem: Even Vault-backed `NativeArray<T>` aliases in the manager looked like persistent owner memory and weakened the H-Phi audit evidence.
Solution: Remove all manager-level `NativeArray<T>` fields from `ProceduralOreSpawner`. Introduce a transient `ProceduralGeologyVaultViews` struct resolved from cached handles for full mutation/job paths, and use narrow single-buffer handle resolves for per-frame telemetry, sector hashes, biome heatmap, args, and matrix upload.
Rejected Alternatives: Keeping aliases and arguing they were harmless; resolving `GlobalRegistry.DataVault` in hot loops; storing per-sector managed snapshots.
Scalability potential: Low/middle/high/ultra tiers all use the same Vault memory topology; quality only changes math work and matrix count, not ownership.
Hardware Impact: No extra persistent memory. Hot frame helpers avoid resolving all 21 buffers; the gain is relocation-safe Vault authority and cleaner rollback/blackbox ownership without turning H-Phi compliance into a dispatch tax.

## Decision 12 - Handle-Only Tick Guard And Private Pads

Problem: `EnsureNativeState()` still resolved every Vault lane during `SlowTick` and `LateFrameTick` just to prove state existed, and explicit padding fields were public only so editor validators could use `nameof`.
Solution: Change `EnsureNativeState()` to a metadata-only descriptor-created check and leave full handle resolution to mutation/job/upload paths. Make all DTO padding fields private explicit-offset fields; the editor validator checks those offsets by string name through non-public reflection.
Rejected Alternatives: Resolving all 21 buffers every tick; keeping public padding as pseudo-API; adding a core Vault validation hook outside this domain.
Scalability potential: Low/middle/high/ultra tiers keep the same Vault topology. The saved tick work is constant overhead removed from every quality tier, while visual density still scales continuously through `GlobalQualityWeight`.
Hardware Impact: Avoids touching every Vault lane in routine frame guards and removes public padding writes from external code. ARM64 layout remains explicit and cache-line aligned.

## Decision 13 - AUP-Safe Heightmap Payload Sampling

Problem: The MapMagic heightmap route still built a `Vector3` by casting absolute `double3` player coordinates to float before payload lookup, and the Burst sampler subtracted runtime terrain origins from absolute sector XZ values. After an origin shift this can clamp every ore candidate to a wrong heightmap edge.
Solution: Convert player AUP to runtime coordinates through `HectonFloatingOrigin.ToRuntimePosition(double3)` before calling the runtime payload lookup. Pass a `double2 TerrainOriginAbsoluteXZ` into `GenerateResourceNodesJob` and compute heightmap UVs in double space as `(absoluteSampleXZ - absoluteTerrainOriginXZ) / terrainSize`.
Rejected Alternatives: Keeping `TryGetQuantizedHeightmapPayloadAUP(Vector3)` and accepting float truncation; converting sector coordinates down to runtime floats before generation; storing per-ore absolute coordinates to compensate.
Scalability potential: Low tier still falls back to mock SDF/nearest sampling; high/ultra keep richer cluster generation on correctly grounded payload samples instead of spending GPU overkill on misplaced ore.
Hardware Impact: Adds two double subtracts/multiplies only on heightmap sample path and removes a precision failure mode that would cause bad culling, bad draw bounds, and wasted matrix upload.

## Decision 14 - Tangent Basis NaN Vaccination

Problem: `BuildTangent()` could return a non-finite tangent if the supplied normal or cross-product basis degenerated, poisoning ore matrices sent directly to the GPU.
Solution: Normalize with finite fallback for normal, tangent, bitangent, and final spun tangent. Matrix generation now falls back to a stable orthonormal-enough basis instead of emitting NaNs.
Rejected Alternatives: Trusting terrain normals; filtering only after matrix write; catching NaNs in shader clip.
Scalability potential: All quality tiers share the same matrix safety. High/ultra visual-only clusters no longer amplify one bad normal into multiple bad GPU rows.
Hardware Impact: A few finite checks in generation prevent corrupt `float4x4` rows from entering the Vault/GPU upload path.

## Decision 15 - Matrix-Inclusive Draw Bounds And Blackbox Validation

Problem: `ResolveDrawBounds()` bounded only authoritative ore positions, while the high-quality Dear Lie adds visual-only matrix rows with `OreTypes=0`. Those cosmetic rows could be outside the authoritative point bounds and disappear under Unity's procedural draw culling. `ValidateOreState()` also skipped those rows, leaving a NaN path into `_OreMatrices`.
Solution: Derive draw bounds from every uploaded active `float4x4` row using the same diagonal-activity predicate as the shader, and accumulate matrix extents from the absolute basis vectors. Validate all uploaded matrix rows for finiteness before the blackbox dump path, while retaining authoritative `OrePositions` validation for gameplay rows.
Rejected Alternatives: Sector-wide draw bounds; it hides the bug but wastes culling precision on weak GPUs. Shader-only NaN clipping; it still uploads poisoned matrices and loses forensic evidence.
Scalability potential: Low quality keeps fewer active matrices and gets tighter bounds; middle/high/ultra can render visual-only clusters without losing them to CPU-side bounds.
Hardware Impact: Adds a bounded O(rendered matrices) post-job bounds scan already required for `DrawProceduralIndirect`, and avoids wasted redraw/flicker from overbroad or missing bounds.

## Decision 16 - Shader-Matched Procedural Bounds Extents

Problem: Loop 11 fixed matrix-inclusive bounds but used a generic `0.5 * basis` cube assumption. The procedural shader's generated ore primitive is not a unit cube: local vertices reach X `0.34`, Y `0.34`, and Z `0.82`. The old CPU bounds could still clip the forward spike of every procedural ore instance.
Solution: Replace the generic half-basis bound with conservative constants matching shader-local maximum magnitudes: `OreProceduralLocalExtentX=0.34`, `Y=0.34`, `Z=0.82`. Bounds now multiply each matrix basis vector by the correct local extent.
Rejected Alternatives: Sector-wide bounds; it avoids clipping but wastes culling. Mesh-driven bounds; it reintroduces mesh dependency for a shader-expanded primitive.
Scalability potential: Low tier keeps tight bounds for fewer matrices; high/ultra visual clusters keep their full shader geometry visible without forcing overbroad sector culling.
Hardware Impact: Same O(rendered matrices) scan, but no underbound flicker and no GPU work hidden behind a too-small procedural draw AABB.

## Decision 17 - Transient Terrain Payload Handoff

Problem: `MapMagicBridge.QuantizedHeightmapPayload` contains NativeArray-backed terrain views. Keeping it as a private manager field left a persistent non-Vault alias that weakened the H-Phi proof even though geology did not allocate that memory.
Solution: Remove the manager `_heightPayload` field. `RefreshSectorAndTerrain()` now creates a local payload, `RefreshMapMagicPayload(..., out payload)` fills it, and `ScheduleSpawnJob(..., payload)` consumes it immediately when a sector/anchor regeneration is scheduled.
Rejected Alternatives: Keeping the field and arguing MapMagic owns the memory; copying the heightmap to a private geology NativeArray; expanding core terrain contracts during this polish pass.
Scalability potential: All tiers keep the same Vault-owned geology memory route. Low tier can still fall back to mock SDF when the transient payload is invalid; high/ultra still use the payload for better grounding without retaining foreign terrain state.
Hardware Impact: Removes one persistent NativeArray-view alias from the manager. Runtime cost is neutral; the gain is ownership clarity, relocation safety, and cleaner rollback/blackbox evidence.

## Decision 18 - Canonical Cold Allocation Evidence

Problem: The source comments for cold allocations used ASCII hyphen separators while AGENTS requires the exact canonical `COLD ALLOC` format with em-dash separators. The runtime behavior was unchanged, but the proof trail was technically false.
Solution: Update the owned runtime/editor `COLD ALLOC` comments to `// COLD ALLOC: Type[capacity] — reason — owner: ClassName`.
Rejected Alternatives: Leaving non-canonical comments because they are cosmetic; deleting cold-allocation comments; hiding editor `StringBuilder` allocation from audit.
Scalability potential: No runtime tier effect. It improves audit determinism so future static gates can distinguish approved cold allocations from hot-path drift.
Hardware Impact: No frame-path change. The benefit is evidence integrity for zero-GC and memory-lifetime reviews.

## Decision 19 - Dispatcher-Owned Job Fence

Problem: `ProceduralOreSpawner.Dispose()` and finished-job retirement called `_spawnJob.Complete()` directly. Completion after `IsCompleted` is non-blocking, but raw calls bypass the project's `DispatcherJobFence` policy and the scheduled job was not registered with H8Memory owner telemetry.
Solution: Replace raw completion with `DispatcherJobFence.TryFinalizeCompleted(ref _spawnJob)` for non-blocking retirement and `DispatcherJobFence.TryComplete(ref _spawnJob, forceComplete: true)` only for forced teardown. Register every scheduled geology generation job with `H8Memory.RegisterActiveJob(OwnerSystemId, _spawnJob)`.
Rejected Alternatives: Unlocking Vault buffers before job completion; pretending teardown can be non-blocking while locked Vault buffers are still being written; adding a new cross-domain shutdown queue.
Scalability potential: No visual-tier effect. The benefit is deterministic owner-fence evidence and centralized job completion policy.
Hardware Impact: Gameplay retirement remains gated by `IsCompleted`. Forced teardown can still block because the current Vault lock API has no async unlock callback; the blocking point is now explicit and centralized instead of raw local completion.

## Decision 20 - Depletion Compaction Before Indirect Draw

Problem: Depletion cleared the authoritative slot and its visual children to zero matrices but left `_renderInstanceCount` unchanged. The shader clipped inactive rows, but the vertex stage still had to process those dead instances.
Solution: After deterministic slot clearing, compact active `ResourceNodes`, `OrePositions`, `OreTypes`, `ResourceMatrices`, and `CandidateSlots` rows to the front of the Vault buffers, zero the tail rows, recompute authoritative/visual/titanium counters, refresh draw bounds, and rewrite indirect args to the compacted instance count.
Rejected Alternatives: Relying on shader `clip()` for harvested rows; regenerating the whole sector after every harvest; storing a separate managed free-list.
Scalability potential: Low tier sheds harvested vertex work immediately. High/ultra keep dense visual-only clusters only while they are alive, then reclaim the indirect instance budget without changing deterministic depletion truth.
Hardware Impact: Adds bounded O(active rendered rows) CPU compaction on depletion events only. It removes repeated per-frame vertex processing for dead zero-matrix rows after harvest.

## Decision 21 - Candidate Slot Sentinel Discipline

Problem: Loop 16 compacted rows, but cleared tail rows still wrote `CandidateSlots[index] = 0`. Slot `0` is a valid deterministic geology slot, so a damaged or partially compacted active prefix could make cleanup, telemetry, or ore hash code treat a cleared row as live slot zero.
Solution: Introduce `ClearedCandidateSlot = -1`, write it to cleared rows, and reject negative deterministic slots before ore hash, depletion-mask, or first-live telemetry derivation. `RefreshFirstLiveOreTelemetry` also clamps its scan to available `OreTypes` and `OrePositions` lengths before reading rows.
Rejected Alternatives: Keeping zero as an implicit sentinel; using a separate managed free-list; changing the depletion command API to a stable ore hash during this batch.
Scalability potential: No visual-tier split. It protects the same deterministic slot law across low/middle/high/ultra density because every tier can legitimately produce slot zero.
Hardware Impact: One scalar branch on harvest/read-model paths. It prevents false slot-zero work and blackbox hash corruption without adding memory, GC, or per-frame GPU cost.

## Decision 22 - Runtime Shift Row Clamp

Problem: `ApplyRuntimeShift` iterated `_renderInstanceCount` and trusted Vault row lengths for `OreMatrices`, `OreTypes`, and `OrePositions`. Normal generation keeps those lengths aligned, but Vault generation changes or a damaged buffer could turn an origin-shift signal into an out-of-range read/write.
Solution: Gate the matrix row loop on `views.OreMatrices.IsCreated`, clamp the loop to actual matrix/type lengths, and bounds-check `OrePositions` before authoritative position writes. Drop-pod anchor and first-live telemetry still shift even when zero matrix rows are live.
Rejected Alternatives: Trusting `_renderInstanceCount`; returning early from the whole shift path when no matrices exist; forcing a full sector regeneration on every AUP shift.
Scalability potential: No visual-tier split. Low tiers have fewer rows and pay less shift work; high/ultra keep dense matrix rows but now with bounded reads if Vault state changes under pressure.
Hardware Impact: Adds only scalar length clamps on origin-shift events. It prevents out-of-range safety faults and avoids unnecessary sector regeneration or main-thread stalls.

## Decision 23 - Pointer-Free Vault Generation Descriptors

Problem: SHINOBU_202 established that runtime managers may persist only pointer-free `VaultGenerationHandle<T>` descriptors. `ProceduralOreSpawner` still persisted obsolete `VaultBufferHandle<T>` fields and resolved through `.Resolve()`, leaving stale pointer metadata in manager state even though the data lived in Vault.
Solution: Replace all 21 geology Vault fields with 16-byte `VaultGenerationHandle<T>` descriptors, allocate through `IDataVault.GetGenerationHandle`, resolve method-local views through `IDataVault.TryResolveHandle`, and acquire CSV/job writer fences through `TryAcquireWriteLock`/`ReleaseWriteLock` on the descriptors. Resolve/acquire helpers reacquire the descriptor if the generation is stale, missing, or shorter than the required lane length. Persistent manager state now contains no Vault pointer, no cached `NativeArray<T>`, and no legacy bridge handle.
Rejected Alternatives: Keeping `VaultBufferHandle<T>` because the obsolete bridge still generation-checks; editing Core Vault APIs during a geology polish pass; using raw `BufferID` writer locks while claiming descriptor-only ownership.
Scalability potential: Low, middle, high, and ultra tiers keep identical Vault ownership. Quality still changes generated visual matrix count; descriptor width and resolve policy remain constant across hardware.
Hardware Impact: Persistent descriptor footprint drops from the legacy 24-byte pointer-bearing handle shape to the 16-byte explicit descriptor. More importantly, defrag/relocation cannot leave stale pointers in geology manager fields; every execution phase pays a deliberate local resolve before touching memory.

## Decision 24 - DataVault Hot-Swap Without Tick-Time Registry Polling

Problem: A DataVault replacement could leave `_dataVault` and all geology generation descriptors aimed at a stale Vault instance. The first Loop 20 patch detected this by reading `GlobalRegistry.DataVault` inside `EnsureNativeState()`, but that violated the cold-registry rule for tick paths.
Solution: Make `ProceduralOreSpawner` an `IGlobalRegistryHotSwapListener`/`IGlobalRegistryHotSwapRefListener`. DataVault replacement events queue a pending Vault pointer; `EnsureNativeState()` consumes only that cached event state and never polls `GlobalRegistry.DataVault` in ticks. Rebind marks any scheduled spawn job output for discard, waits until `DispatcherJobFence.TryFinalizeCompleted` can retire it, clears presentation without writing through the old Vault, releases stale descriptors, reacquires all 21 `VaultGenerationHandle<T>` descriptors from the new Vault, writes the 16-byte indirect args row back to Vault, and zeros the GPU args buffer if the Vault was cleared.
Rejected Alternatives: Hot polling `GlobalRegistry.DataVault` each `SlowTick`/`LateFrameTick`; force-completing the spawn job during a service swap; accepting stale descriptors until scene reload; editing core registry/DataVault APIs during a geology-owned polish pass.
Scalability potential: Low, middle, high, and ultra tiers keep the same descriptor route. Weak devices avoid registry churn and main-thread stalls; high/ultra visual density is preserved after rebind because only the presentation prefix and indirect args are reset, not the deterministic sector rules.
Hardware Impact: Removes one registry read from every geology tick and prevents stale descriptor writes after Vault defrag/replacement. No profiler number is claimed; the safety gain is avoiding invalid NativeArray views and stale indirect draws without adding hot-path allocation.

## Decision 25 - Disable Cleanup Without Vault Writer Races

Problem: `OnDisable()` could clear presentation while `_spawnJobScheduled` was still true, and the old clear path rewrote `GeologyIndirectArgsDTO` through the Vault. The spawn job also owns the indirect-args writer lock, so that cleanup path was a potential owner-local write race. A queued DataVault rebind could also make disable/discard cleanup write through the old Vault after replacement was already known.
Solution: Route disable cleanup through `ClearDisabledPresentationState()`. It rewrites the Vault indirect-args row only when no spawn job is scheduled and no DataVault rebind is pending; otherwise it clears scalar presentation state and zeroes only the GPU args buffer through `WriteIndirectArgsGpu(0u)`. `DiscardSpawnJobOutput()` now avoids old-Vault indirect-args rewrites while a pending rebind exists. `Dispose()` explicitly clears any queued rebind reference after releasing descriptors.
Rejected Alternatives: Force-completing the spawn job from `OnDisable()`; leaving shader-side zero-instance clipping to hide stale args; clearing pending rebind state in normal `OnDisable()` and losing service-replacement evidence before re-enable.
Scalability potential: No visual-tier split. Low tier avoids an avoidable disabled-object Vault write; high/ultra keep dense matrix presentation only after a valid job commit on a current Vault.
Hardware Impact: Removes one possible disabled-path Vault write during active job ownership and prevents stale service references from surviving object destruction. No profiler number is claimed; this is correctness and lock-discipline hardening.

## Decision 26 - Editor Gizmo Descriptor Discipline

Problem: The editor-only geology gizmo still read `ResourceNodes` with `IDataVault.TryGetBuffer`, bypassing the pointer-free `VaultGenerationHandle<T>` discipline used by runtime phases. It was not a gameplay hot path, but it preserved an obsolete access pattern in SHINOBU-owned source.
Solution: Route `OnDrawGizmosSelected()` through `TryResolveBuffer(ref _resourceNodesHandle, ProceduralGeologyVaultBufferIds.ResourceNodes, ...)`. The gizmo now resolves the same descriptor-owned `ResourceNodes` lane as runtime, keeps the `NativeArray<ResourceNodeDTO>` local to the draw call, and fails closed if the descriptor is absent or stale.
Rejected Alternatives: Leaving editor code as an exception; using `TryGetBuffer` because it is convenient; adding a new editor-only Vault API wrapper.
Scalability potential: No runtime quality split. The benefit is evidence consistency: low/middle/high/ultra runtime routes and editor inspection all prove the same descriptor ownership model.
Hardware Impact: No frame-path gain is claimed. This removes one stale API route and prevents future agents from copying direct buffer access back into runtime code.

## Decision 27 - Depletion Writer Fence And Tuner Descriptor Discipline

Problem: `TryMarkOreDepleted()` could mutate `DepletionMasks`, `ResourceNodes`, `OrePositions`, `OreTypes`, `ResourceMatrices`, `CandidateSlots`, and `IndirectArgs` while a generation job still owned writer locks for the same Vault lanes. The UI Toolkit tuner also still used direct `GetBuffer`/`TryGetBuffer`, so the Loop 22 editor-surface proof was overstated.
Solution: `TryMarkOreDepleted()` now fails closed when `_spawnJobScheduled` is true and calls `EnsureNativeState()` before resolving mutation views, allowing queued DataVault rebinds to apply before any depletion write. `ProceduralResourceTunerWindow` now reads tuning/telemetry through existing `VaultGenerationHandle<T>` descriptors and writes tuning through a method-local descriptor resolved immediately after `GetGenerationHandle`.
Rejected Alternatives: Force-completing the generation job from the interaction/depletion path; writing through stale Vault descriptors during a pending rebind; leaving direct editor buffer APIs as "harmless" because they are cold; broadening the fix into unrelated world resource files.
Scalability potential: No binary tier split. Low density devices avoid mutation races during sector regeneration; high/ultra visual density keeps deterministic compaction only after the current job commits or is discarded through the dispatcher fence.
Hardware Impact: One scalar branch and one descriptor-state guard on depletion commands. It prevents contested cache-line writes and stale descriptor mutation without adding allocations or per-frame GPU bandwidth.

## Decision 28 - Terrain Adapter Boundary And Provider Fallback Height

Problem: `ProceduralOreSpawner` carried the concrete `MapMagicBridge.QuantizedHeightmapPayload` type across refresh and spawn scheduling, and `SlowTick` repeatedly called the MapMagic resolver. When no quantized payload was available, the mock SDF base height fell back to player AUP Y instead of the registered terrain provider height, so fallback ore grounding could drift toward camera altitude.
Solution: Add a phase-local `GeologyHeightPayloadView` with raw fields and no properties. `RefreshTerrainPayload()` is the only cold adapter that names `MapMagicBridge.QuantizedHeightmapPayload`; it copies height samples, terrain size, absolute XZ origin, and absolute base Y into the SHINOBU-owned view. `ScheduleSpawnJob()` consumes only that view. `ITerrainProvider` is cached on enable and through `TerrainProviderRuntime` hot-swap events; `MapMagicRuntime` hot-swap updates the bridge and clears/replaces the provider reference when the bridge had been the provider. If no quantized payload exists, the mock SDF job now uses the cached provider terrain height converted back to AUP Y.
Rejected Alternatives: Editing Core `ITerrainProvider` to expose a quantized payload during a geology polish pass; keeping the foreign nested payload in the scheduling signature; querying `GlobalRegistry.MapMagic` every slow tick; using player altitude as fallback terrain truth.
Scalability potential: Low tier and missing-MapMagic paths still use the cheap 32x32 mock SDF, but anchored to terrain-provider seafloor height when available. Middle/high/ultra retain the quantized heightmap route and Dear Lie visual density without carrying MapMagic types beyond the adapter boundary.
Hardware Impact: Removes one slow-tick MapMagic registry resolve and avoids bad fallback matrices that would waste draw bounds, HZB culling, and indirect instance budget. The new provider height sample is one scalar cold query per sector refresh, not a per-candidate loop.

## Decision 29 - Player Runtime Context Service Cache

Problem: `SlowTick()` still used `WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform)`, and `TryResolvePlayerAup()` read `GlobalRegistry.Player` from the sector refresh path. That kept a registry/helper lookup in the recurring geology update path after terrain services had already been moved to hot-swap cached dependencies.
Solution: Add a cached `_playerContext` field initialized in cold `CacheRuntimeServices()` and maintained through `GlobalRegistryServiceSlot.Player` hot-swap events. `SlowTick()` now refreshes `playerTransform` from the cached context only, and `TryResolvePlayerAup()` resolves pose/AUP through `_playerContext` without touching the registry.
Rejected Alternatives: Keeping `WorldRuntimeReferenceUtility` in slow tick because the cadence is low; converting AUP from the Unity `Transform`; editing core player APIs during a geology-owned polish pass. The selected route keeps player authority inside the existing `IPlayerRuntimeContext` contract.
Scalability potential: Low, middle, high, and ultra tiers share the same cached player route. Weak devices avoid recurring service discovery. High/ultra still spend saved budget on quality-weighted height refinement and Dear Lie matrix clusters, not object lookup.
Hardware Impact: Removes one recurring helper lookup and one recurring player-registry read from the sector refresh path. No profiler number is claimed; this is compile-wall and hot-path service-route hardening without adding allocations.

## Decision 30 - Preemptive SafeNormalize In Burst Geometry Basis

Problem: The owned Burst geometry paths used `math.normalize()` and then checked the result for finiteness. That repaired the stored normal/tangent in most cases, but a zero or poisoned vector could still manufacture a transient NaN inside the kernel before fallback. The mock SDF job had the same pattern for terrain normals.
Solution: Add `SafeNormalize(float3 value, float3 fallback)` in the owned mock SDF job and the resource generation job. It rejects non-finite inputs, rejects `lengthsq <= 0.0001f`, and only calls `math.rsqrt(math.max(lengthSq, 0.0001f))` after those guards. Terrain normals, cluster bitangents, aligned matrix normals, tangents, bitangents, and spun tangents now use this helper.
Rejected Alternatives: Keep post-normalize finite checks; clamp only final matrices; push NaN rejection into shader clip. Those alternatives still allow transient NaNs or poisoned basis vectors in CPU-side generation before the blackbox path can explain the source.
Scalability potential: Low quality still uses nearest/sample-only terrain with no extra refinement. Middle/high/ultra keep quality-weighted refinement and Dear Lie clusters, but basis creation no longer gets less stable as visual density rises.
Hardware Impact: Adds a finite check and `lengthsq` branch around basis normalization. It prevents NaN matrix rows and dump-triggering bad geometry without allocating memory, touching Vault layout, or adding per-frame managed work.

## Decision 31 - Player Runtime Position Snapshot Discipline

Problem: Loop 25 removed recurring player-registry lookup, but generation, drop-pod fallback anchoring, draw-bound fallback, and telemetry state hashing still read `playerTransform.position` or `transform.position`. Those reads keep Unity `Transform` float state in SHINOBU-owned recurring paths and bypass the existing player pose snapshot contract.
Solution: Cache `_lastPlayerRuntimePosition` from `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot()` through `RefreshCachedPlayerRuntimeReference()` and `TryResolvePlayerPose()`. If the snapshot is unavailable but `PlayerMovement.CurrentAup` is valid, derive runtime position through AUP-to-runtime conversion. Feed `CameraRuntimePosition`, first drop-pod fallback, draw-bound fallback, and telemetry hash from the cached float3. AUP origin-shift signals now shift the cached player runtime position with the same delta as ore matrices and drop-pod presentation.
Rejected Alternatives: Keep reading `Transform.position` because it is convenient; convert absolute double AUP to float directly without the floating-origin helper; remove the serialized transform field and risk scene compatibility churn; edit the core player context during a geology-owned polish pass.
Scalability potential: Low, middle, high, and ultra tiers share the same cached pose route. Low tier avoids Unity transform reads in geology presentation and telemetry; high/ultra still spend quality-weighted budget on SDF refinement, HZB-gated visual-only clusters, and indirect draws.
Hardware Impact: Removes recurring SHINOBU-owned transform-position property reads from generation/telemetry fallback paths. No profiler number is claimed; this is authority and precision hardening without new allocations, Vault lanes, or asmdef references.

## Decision 32 - Continuous Grounding And Ore Weight Curves

Problem: `SampleGrounding()` used `math.step(0.3f, quality)` as a hard refinement gate, and `ResolveOreWeights()` used hard near/far distance branches around the drop pod. The first was a real GlobalQualityWeight threshold pop; the second was a deterministic gameplay distribution discontinuity that could reshuffle ore probabilities abruptly at band edges.
Solution: Replace the grounding gate with `math.smoothstep(0.25f, 1f, quality) * 2f`, then apply per-iteration weights through `math.saturate(refineBudget - i)`. Low quality still schedules zero extra refinement below the soft floor, while middle/high quality blend into one or two refinement passes. Replace near/far ore-weight branches with a finite-safe `math.smoothstep(0f, 1f, gradient01)` curve, lerping titanium/copper/silver weights and preserving an integer total of 100.
Rejected Alternatives: Keeping `math.step` because the prompt allowed `step`; running two refinement probes every time and multiplying by zero; retaining hard distance bands because ore type selection is not hardware quality. The selected route keeps low-tier work bounded and removes threshold discontinuities.
Scalability potential: Low tier remains nearest/sample-first with no extra refinement below the smooth floor. Middle tiers get fractional first-pass influence. High/ultra get the full two-pass refinement plus existing visual-only cluster density, without a binary quality branch.
Hardware Impact: Low-tier CPU cost is unchanged at the collapsed path. Mid/high tiers pay only bounded extra height samples, and the smoother ore curve prevents deterministic slot churn at exact distance thresholds without new memory, Vault rows, or shader payloads.

## Decision 33 - Read Accessor Purity And Vault Acquisition Naming

Problem: The new Global Systems Doctrine forbids read accessors named `Get*`, `TryGet*`, `Resolve*`, or `Read*` from allocating, growing buffers, mutating global state, completing jobs, publishing, or syncing scene state. SHINOBU-owned code still had side-effecting `TryResolveVaultViews`/`TryResolveBuffer` helpers that could acquire or reacquire Vault generation handles, public `TryGetOrePositions`/`TryGetOreTypes` that could trigger that acquisition path, a `TryResolvePlayerPose` method that updated cached runtime-position state, a cold `ReadCsvFileIntoScratch` loader, and several RNG-consuming `Resolve*` helpers inside the Burst generation job.
Solution: Rename allocation-capable Vault paths to `AcquireVaultViews` and `AcquireBuffer`. Add `TryReadExistingBuffer` for public read paths; it resolves only already-created `VaultGenerationHandle<T>` rows and does not call `GetGenerationHandle`. Rename the pose capture path to `CapturePlayerPose`, the cold CSV loader to `LoadCsvFileIntoScratch`, and RNG-consuming job helpers to `Select*`/`Sample*`. The editor tuner keeps pure existing-buffer reads separate from `AcquireOrCreateBuffer`.
Rejected Alternatives: Leaving the old names because they were private; documenting an exception; making public `TryGet*` methods allocate missing buffers for convenience; moving all Vault acquisition to a new core wrapper during a domain polish pass. The selected route preserves current ownership while making side effects explicit in SHINOBU-owned names and behavior.
Scalability potential: Low tier avoids hidden Vault acquisition from consumer reads. Middle/high/ultra keep the same visual-overkill generation budget, but dependency surfaces are clearer: consumers read existing snapshots, owner phases acquire fixed Vault lanes.
Hardware Impact: No profiler number is claimed. The practical gain is removal of hidden handle acquisition from public read accessors and clearer auditability under CPU load; no new memory, DTO, shader, asmdef, or route lane was introduced.

## Decision 34 - Public Read Snapshot Fence

Problem: Loop 29 made public `TryGetOrePositions` and `TryGetOreTypes` pure with respect to Vault acquisition, but they could still expose `OrePositions` or `OreTypes` while `_spawnJobScheduled` was true or while `_pendingDataVaultRebind` meant descriptors were about to be replaced. The ground radar consumer reads both arrays as immutable snapshots; exposing writer-owned rows violates the runtime-context snapshot doctrine.
Solution: Add `CanExposeReadSnapshot()` and fail both public read accessors closed when a generation job is scheduled or a DataVault rebind is pending. The existing depletion command already used the same writer-fence principle; this applies that discipline to the read model.
Rejected Alternatives: Force-completing `_spawnJob` from the read accessor; returning stale arrays while generation is in flight; adding a copied front buffer in this polish pass; editing the radar consumer outside SHINOBU ownership. The selected route preserves compile boundaries and avoids main-thread stalls.
Scalability potential: Low tier avoids same-frame read/write hazards during cheap collapsed generation. Middle/high/ultra keep higher visual cluster counts, but radar reads now wait for the owner commit window instead of racing richer write workloads.
Hardware Impact: Two scalar branch checks per public read call. It avoids contested read/write cache behavior and stale descriptor exposure without allocating memory, copying arrays, completing jobs, or changing DTO/Vault/shader layout.

## Decision 35 - Cold Allocation Evidence Labels

Problem: SHINOBU-owned runtime/editor source had intentional cold reference allocations that were not all labeled at the allocation site: CSV `FileStream`, blackbox dump `FileStream`/`BinaryWriter`, UI Toolkit `Label`/`Slider`, and the structured `GraphicsBuffer` factory. The allocations were not hot-path gameplay work, but unlabeled `new` sites make zero-GC audits noisy and invite future misuse.
Solution: Add canonical `COLD ALLOC` comments with owner and purpose to the file CSV stream, telemetry dump stream/writer, editor status label, editor sliders, and structured GraphicsBuffer factory. Existing matrix/args buffer labels remain unchanged.
Rejected Alternatives: Removing FileStream/BinaryWriter and rewriting file I/O during a polish pass; leaving comments only in the log; treating editor allocations as self-evident; broadening into unrelated systems.
Scalability potential: No runtime quality tier changes. Low/middle/high/ultra all retain the same hot path; this only tightens static evidence that allocation happens in cold CSV load, crash dump, editor facade, or boot GPU buffer creation.
Hardware Impact: No microsecond gain claimed. The benefit is auditability: hot-path zero-GC scans can distinguish intentional cold setup/dump/editor allocations from accidental gameplay allocations without adding memory or changing behavior.

## Decision 36 - Recurring Telemetry Existing-Handle Discipline

Problem: `WriteTelemetrySample()` ran from slow/late-frame paths and still called acquisition-capable `AcquireBuffer` for `TelemetryRing` and `DepletionMasks`. `DumpTelemetry()` and the editor gizmo also used `AcquireBuffer`, which could create/reacquire rows instead of proving they were already owner-created. That weakened the "owner setup acquires, recurring paths consume existing handles" doctrine.
Solution: Rename the pure existing-handle helper to `TryOpenExistingBuffer` and route public ore reads, telemetry writes, telemetry dumps, and editor gizmo inspection through it. These paths now fail closed if the generation handle is absent, stale, too short, or not created, and they do not call `GetGenerationHandle`.
Rejected Alternatives: Keeping telemetry acquisition because it is cheap; force-creating telemetry rings from the late-frame path; copying telemetry into a private local ring; editing radar/gizmo consumers outside SHINOBU ownership.
Scalability potential: Low tier avoids hidden descriptor work during recurring telemetry. Middle/high/ultra still generate richer visual matrices, but telemetry and gizmo inspection no longer grow or reacquire Vault rows while observing them.
Hardware Impact: Removes acquisition-capable calls from recurring telemetry and editor inspection. No profiler number is claimed; the measurable intent is avoiding hidden Vault descriptor churn and preserving predictable owner-phase memory behavior.

## Decision 37 - Evidence Log Chronology Repair

Problem: The Loop 29-32 report blocks were accidentally inserted above older Loop 26-28 report entries, violating the project log convention that old entries stay at the top and new evidence is appended at the bottom.
Solution: Append a bottom-tail correction block to `LOG_SHINOBU_153.md` that explicitly re-anchors Loop 29-32 evidence after Loop 28 and marks the earlier misplaced blocks as historical duplicates, not the newest audit anchor.
Rejected Alternatives: Rewriting or deleting previous log blocks and risking loss of audit history; ignoring the ordering defect because source code scans were clean.
Scalability potential: No runtime quality-tier impact. This preserves evidence integrity for future context recovery across low, middle, high, and ultra runtime paths.
Hardware Impact: No frame-path effect. The benefit is forensic reliability: future agents and reviewers read newest evidence from the actual file tail instead of a mid-file insertion.

## Decision 38 - GPU Matrix Upload Existing-Handle Discipline

Problem: `UploadRenderMatrices()` is a recurring presentation path and still called acquisition-capable `AcquireBuffer()` for `ResourceMatrices`. That allowed a late-frame GPU upload observer to create or reacquire a Vault row instead of proving the owner setup/generation phase already established the lane.
Solution: Route `UploadRenderMatrices()` through `TryOpenExistingBuffer(in _oreMatricesHandle, _oreCapacity, out NativeArray<float4x4>)`. The upload now fails closed when the descriptor is absent, stale, short, or not created, and it never calls `GetGenerationHandle` from the render-upload path.
Rejected Alternatives: Keeping acquisition in upload because the matrix lane is expected to exist; force-creating the lane during late-frame presentation; adding a private fallback matrix array; touching renderer/core Vault APIs outside the SHINOBU geology boundary.
Scalability potential: Low tier avoids hidden Vault descriptor churn while uploading fewer matrices. Middle/high/ultra still use the same double-buffered `GraphicsBuffer` route for denser Dear Lie clusters, but only after owner phases have provided the matrix lane.
Hardware Impact: Removes one acquisition-capable Vault path from late-frame upload. No profiler number is claimed; the gain is predictable owner-phase memory behavior and reduced risk of stale or unexpected row creation under GPU presentation pressure.

## Decision 39 - Depletion AUP Proof Before Mutation

Problem: `MarkDepleted()` wrote the depletion mask before proving it could publish a valid `ItemAcquiredSignal.PositionAup`. If `ResourceNodes` was absent or stale, the fallback converted runtime float position through `GlobalSignals.CurrentRuntimeOriginAup()`, adding a global-origin read and still returning after the mask mutation if conversion failed.
Solution: Require the owner `ResourceNodes` row before depletion, derive `PositionAup` from `ResourceNodeDTO.SectorAUP`, validate it before any mask write, and remove `TryResolveRuntimeAup()` plus the `CurrentRuntimeOriginAup()` fallback from SHINOBU-owned ore depletion.
Rejected Alternatives: Keeping the runtime-origin fallback for convenience; moving mask mutation below only the fallback call while retaining the global read; force-reading floating-origin state directly; adding a new cross-domain origin service during a geology polish pass.
Scalability potential: All tiers share the same deterministic authoritative node row. Low tier avoids a global-origin fallback in depletion; middle/high/ultra keep richer visual-only clusters without changing item-acquisition truth ownership.
Hardware Impact: Adds a few bounds/created checks before a depletion event and removes one global-origin conversion route. No profiler number is claimed; the value is transactional correctness and stricter one-owner proof for depletion signals.

## Decision 40 - Public Depletion Result Mirrors Mutation

Problem: `TryMarkOreDepleted()` computed `oreHash`, `itemHash`, and `depletedPosition` before calling a `void MarkDepleted()` helper. After Loop 35, the helper could still fail closed on invalid AUP or mask bounds while the public API returned `true`, exposing a false success to interaction/scanner callers.
Solution: Convert `MarkDepleted()` to return `bool`. `TryMarkOreDepleted()` now returns `true` only when mask mutation, signal publish, rendered-slot clear, compaction, indirect-args update, and telemetry write all run; otherwise it resets outputs and returns `false`.
Rejected Alternatives: Assuming Loop 35 preconditions always match the private helper; throwing exceptions; publishing a failure signal; broadening the fix into interaction consumers outside SHINOBU ownership.
Scalability potential: No quality-tier split. Low/middle/high/ultra all get one transactional depletion result that does not depend on visual cluster density.
Hardware Impact: One boolean return path on depletion events only. It avoids false caller-side success without allocations, scene search, job completion, or additional Vault lanes.

## Decision 41 - Regeneration-Window Terrain Writes

Problem: `SlowTick()` called `RefreshSectorAndTerrain()` every slow frame. Even when the player stayed in the same sector and no regeneration was scheduled, the method wrote the AUP sector hash grid and refreshed the biome heatmap through acquisition-capable Vault paths. That made observer cadence perform owner writes.
Solution: Move `WriteAupSectorHashGrid()` into the `sectorChanged` branch after `_currentSectorHash` is updated, and call `RefreshTerrainPayload()` only inside the `(sectorChanged || anchorRefresh) && !_spawnJobScheduled` scheduling window. Stable-sector slow ticks now only refresh player pose, drain signals, and write telemetry through existing-handle paths.
Rejected Alternatives: Keeping the recurring writes because the buffers are small; converting those two lanes to read-only existing-handle opens while still rewriting them every tick; adding a new cached managed terrain payload outside Vault.
Scalability potential: Low tier avoids pointless Vault descriptor acquisition and heatmap fill while idling in-sector. Middle/high/ultra still get the same payload, biome map, and Dear Lie density when regeneration actually runs.
Hardware Impact: Removes one 9-row sector grid write and one 256-byte heatmap fill plus their acquisition checks from stable-sector slow ticks. No profiler number is claimed; this is owner-phase discipline and deterministic bandwidth trimming.

## Decision 42 - Remove Dead Telemetry Acquisition Wrapper

Problem: A private zero-argument `RefreshFirstLiveOreTelemetry()` wrapper had no callers but still acquired the full geology Vault view before forwarding to the phase-local overload. Dead acquisition-capable helpers are future misuse surface.
Solution: Delete the unused wrapper and leave only `RefreshFirstLiveOreTelemetry(ProceduralGeologyVaultViews views)`, which is called from commit/depletion paths that already own a phase-local Vault view.
Rejected Alternatives: Keeping dead code because it is private; changing it to `TryOpenExistingBuffer` while no caller exists; adding another wrapper that would widen the read path.
Scalability potential: No quality-tier behavior change. Low/middle/high/ultra all keep telemetry first-live hashing inside owner mutation/commit phases only.
Hardware Impact: No measured frame saving because the wrapper was currently unused. The gain is route removal: no dormant full-view Vault acquisition helper remains for first-live telemetry.

## Decision 43 - Simulation Frame Mutation Naming

Problem: `ResolveSimulationFrameId()` mutated `_simulationFrameCounter` when the dispatcher frame was unavailable. A `Resolve*` method with mutation violates the read-accessor purity doctrine even though the behavior is deterministic.
Solution: Rename the method to `AdvanceSimulationFrameId()` and update self-audit, depletion signal, and telemetry call sites. The name now advertises the fallback counter mutation instead of presenting it as a read accessor.
Rejected Alternatives: Leaving the name because the mutation is small; making the frame accessor pure and losing deterministic fallback progression; reading Unity frame/time APIs.
Scalability potential: No quality-tier behavior change. Weak and high-end devices retain the same deterministic frame source; the route is now explicit for audit and rollback review.
Hardware Impact: No frame-time saving claimed. The gain is doctrine compliance: no side-effecting `Resolve*` accessor remains for simulation frame advancement.

## Decision 44 - Depletion Writer Fence And Narrow Views

Problem: `TryMarkOreDepleted()` failed closed while generation jobs owned writer locks, but the synchronous depletion transaction still mutated Vault lanes without acquiring explicit writer fences. It also resolved the full geology view, touching unrelated mock terrain, biome, distribution, self-audit, and HZB lanes.
Solution: Add `TryLockVaultDepletionBuffers()` for the exact mutation/read-write set: resource nodes, positions, types, depletion masks, matrices, candidate slots, indirect args, depletion cache keys/masks/count, and telemetry ring. Add `AcquireDepletionViews()` so the depletion transaction resolves only its bounded lane set before mutation and always releases writer fences through `UnlockVaultWriteBuffers()`.
Rejected Alternatives: Reusing the full generation job lock set; relying on main-thread sequencing without Vault writer proof; keeping `AcquireVaultViews()` for depletion convenience; expanding into unrelated consumer read models.
Scalability potential: Low tier avoids broad Vault lane acquisition during harvest. Middle/high/ultra keep richer visual-only matrix density, but depletion now fences only the rows it actually mutates or reads for proof.
Hardware Impact: Adds event-time Interlocked writer-fence operations on harvest, not per-frame work. It removes broad full-view acquisition from depletion and gives the Vault a concrete writer owner during compaction, mask writes, indirect args, cache update, and telemetry.

## Decision 45 - Generation Writer Lock Set Matches Writes

Problem: The generation lock path must not claim ownership of read-only inputs. `GenerateResourceNodesJob` writes resource nodes, ore positions, ore types, matrices, spawn counts, candidate slots, and indirect args; the preceding mock terrain job writes the mock SDF. Depletion masks, biome heatmap, distribution rules, HZB tiles, and HZB meta are read-only inputs at the resource job boundary and must not be writer-locked by SHINOBU generation.
Solution: Keep `TryLockVaultJobBuffers()` narrowed to the actual generation write set: `ResourceNodes`, `OrePositions`, `OreTypes`, `ResourceMatrices`, `SpawnCounts`, `MockTerrainSdf`, `CandidateSlots`, and `IndirectArgs`. Depletion masks are fenced by the separate depletion transaction; biome and distribution rows are prepared before scheduling; HZB rows remain producer-owned readback inputs.
Rejected Alternatives: Writer-locking every lane passed to the job for convenience; claiming HZB write ownership from geology; dropping HZB input to avoid the ownership question; force-completing producer jobs before geology scheduling.
Scalability potential: Low tier avoids unnecessary Vault lock contention on read-only rows. Middle/high/ultra keep HZB culling and richer Dear Lie matrix density without blocking the HZB producer or CSV/biome owner lanes.
Hardware Impact: Removes avoidable Interlocked writer-fence contention from generation scheduling. No profiler number is claimed; the gain is ownership correctness and less cross-domain lock pressure without changing DTO layout, save identity, or draw route.

## Decision 46 - Depletion Mask Reload Narrow Writer Route

Problem: `LoadDepletionMasksForCurrentSector()` prepared the generation read snapshot by acquiring the full geology view and writing `DepletionMasks` plus depletion-cache rows without a dedicated writer fence. That made sector reload touch unrelated lanes and left mask/cache ownership implicit.
Solution: Add `TryLockVaultDepletionMaskBuffers()` for only `DepletionMasks`, `DepletionCacheKeys`, `DepletionCacheMasks`, and `DepletionCacheCount`. Add `AcquireDepletionMaskViews()` for the same bounded lane set and release through `UnlockVaultWriteBuffers()` in `finally`.
Rejected Alternatives: Reusing full `AcquireVaultViews()`; reusing the broader harvest depletion lock set; leaving reload unfenced because generation is not scheduled yet; copying masks into a private array outside the Vault.
Scalability potential: Low tier avoids broad Vault acquisition while crossing sectors. Middle/high/ultra keep the same deterministic mask snapshot before richer generation and Dear Lie matrix expansion.
Hardware Impact: Adds four event-time writer fences during sector reload and removes full-view acquisition from that route. No profiler number is claimed; the value is narrower cache pressure and explicit mask/cache ownership before the Burst generation job reads the snapshot.

## Decision 47 - Pre-Generation Payload Writer Fences

Problem: `WriteAupSectorHashGrid()` and `FillBiomeHeatmap()` wrote Vault rows through `AcquireBuffer()` without explicit writer fences. These rows are small, but they are still owner facts consumed later by generation and telemetry, so unfenced writes weaken the one-owner route proof.
Solution: Add `TryLockVaultSectorHashGridBuffer()` for `SectorHashGrid` and `TryLockVaultBiomeHeatmapBuffer()` for `BiomeHeatmap`. Both writers now acquire the single lane, mutate it inside `try`, and release through `UnlockVaultWriteBuffers()` in `finally`.
Rejected Alternatives: Leaving small rows unfenced; folding both writes into the broad generation job lock; creating private cached arrays for sector and biome payloads; moving the lanes to another domain.
Scalability potential: Low tier avoids broad locks while still proving row ownership. Middle/high/ultra retain biome-driven distribution and sector hash proof before denser SDF/refinement and Dear Lie matrix output.
Hardware Impact: Adds event-time single-lane writer fences at sector/payload refresh cadence. No profiler number is claimed; the gain is explicit Vault ownership and bounded lock scope without changing generation DTOs or draw buffers.

## Decision 48 - Runtime Shift Writer Fence

Problem: AUP runtime-shift application mutates ore positions, ore matrices, resource-node local matrices, presentation anchors, and telemetry after the generation job writer fence is released. The external shift path also acquired the full geology view even though it needed only runtime-shift rows.
Solution: Add `TryLockVaultRuntimeShiftBuffers()` for `ResourceNodes`, `OrePositions`, `ResourceMatrices`, and `TelemetryRing`, plus `AcquireRuntimeShiftViews()` for the same write rows and read-only `OreTypes`. `TryApplyRuntimeShiftWithFence()` now owns the lock/acquire/apply/release sequence. External AUP-shift application retains the pending shift without advancing `_lastAppliedAupShiftFrameId` if the lock/view cannot be acquired. Pending shifts are retried on later ticks even when no new AUP signal arrives.
Rejected Alternatives: Relying on main-thread sequencing after job finalization; keeping full-view acquisition for shift; folding runtime shifts into generation job output only; using global origin fallback instead of local AUP shift signals.
Scalability potential: Low tier shifts fewer rendered rows but now has the same explicit ownership proof. Middle/high/ultra can shift dense Dear Lie matrix rows without an unfenced mutation path.
Hardware Impact: Adds event-time writer fences during origin shifts only. No profiler number is claimed; the benefit is preventing unfenced matrix/position cache-line mutation and narrowing the external shift view.

## Decision 49 - Proof Row Writer Fences

Problem: `UpdateIndirectArgsBuffer()`, `WriteTelemetrySample(uint flags)`, and `RunSelfAudit()` could write Vault proof rows from commit, depletion, stable telemetry, disable, or diagnostic paths after the generation writer fence had been released. These rows are small, but they are still authoritative proof artifacts; leaving them unfenced weakens the Vault route evidence and can make self-audit report its own proof-row lock as an alias fault.
Solution: Add single-row writer fences `TryLockVaultIndirectArgsBuffer()`, `TryLockVaultTelemetryBuffer()`, and `TryLockVaultSelfAuditBuffer()`. Each call site acquires its own lock only when the caller does not already hold the relevant bit, refuses to nest into unrelated active locks, and releases through `UnlockVaultWriteBuffers()` in `finally`. `WriteSelfAudit()` now ignores bit 18 when computing `AliasFaults`, so the audit does not treat its own self-audit writer lock as evidence of an unrelated alias.
Rejected Alternatives: Relying on main-thread ordering; keeping proof rows under the broad generation fence only; resolving full geology views for telemetry and indirect args; allowing nested lock acquisition to overwrite `_lockedVaultBufferMask`; storing private telemetry or indirect-args mirrors outside the Vault.
Scalability potential: Low tier writes fewer matrices but still gets the same fenced indirect-count proof, blackbox sample, and self-audit row. Middle/high/ultra can push dense Dear Lie matrix counts and optional HZB-cull evidence without unfenced proof-row mutation.
Hardware Impact: Adds event-time single-row Interlocked fences only on proof writes, not per candidate. No profiler number is claimed; the gain is deterministic ownership evidence and lower race surface for indirect args, telemetry, and self-audit rows without changing DTO layout or gameplay truth.

## Decision 50 - Indirect Args Existing-Handle Write

Problem: Loop 45 fenced `UpdateIndirectArgsBuffer()`, but the method still used acquisition-capable `AcquireBuffer()` after taking the writer lock. A recurring proof-row write should not grow or reacquire the Vault row from disable, depletion, or commit paths; the writer fence should protect mutation of an already owner-created descriptor.
Solution: Replace the inner indirect-args resolution with `TryOpenExistingBuffer(in _indirectArgsHandle, IndirectArgsCount, out NativeArray<GeologyIndirectArgsDTO>)`. The row is still writer-fenced by bit 10, but the actual data view now fails closed if the descriptor is absent, stale, or short instead of reacquiring memory.
Rejected Alternatives: Keeping `AcquireBuffer()` because the lock helper already ensures the row; moving all indirect args writes back into full generation view acquisition; storing a private CPU-side indirect args mirror; touching RenderGraph or Vault core APIs outside SHINOBU ownership.
Scalability potential: Low tier still writes a tiny instance-count row for collapsed matrix counts. Middle/high/ultra keep dense Dear Lie indirect counts, but the proof row now follows the same existing-handle discipline as recurring telemetry and matrix upload.
Hardware Impact: Removes acquisition-capable descriptor work from the indirect args update path. No profiler number is claimed; the gain is deterministic memory route behavior and lower risk of hidden row growth under presentation/depletion events.

## Decision 51 - Existing-Handle Views After Writer Locks

Problem: Several routes acquired writer locks and then resolved the same rows with acquisition-capable helpers: depletion transactions, depletion-mask reloads, runtime shifts, sector-grid writes, biome-heatmap fills, generation scheduling, and generation commit. This mixed "owner setup" with "existing row mutation" and left dormant acquisition wrappers for future misuse.
Solution: Add `TryOpenExistingVaultViews()` for full post-setup view resolution, convert the narrow routes to `TryOpenExistingDepletionViews()`, `TryOpenExistingDepletionMaskViews()`, and `TryOpenExistingRuntimeShiftViews()`, and replace sector-grid/biome-heatmap row resolution with `TryOpenExistingBuffer()`. Remove the no-argument `AcquireVaultViews(out ...)` wrapper. Full `AcquireVaultViews(IDataVault, ...)` remains only for cold setup/rebind after the owner explicitly receives an `IDataVault`.
Rejected Alternatives: Leaving acquisition in post-lock routes; making every recurring path call the full cold acquisition helper; relying on writer locks while still allowing descriptor reacquisition; widening into Vault core APIs shared by other agents.
Scalability potential: Low tier avoids hidden descriptor churn in collapsed generation and event routes. Middle/high/ultra keep denser visual matrices and HZB culling, but schedule/commit/shift/harvest now consume existing Vault rows rather than growing or reacquiring memory under load.
Hardware Impact: Removes acquisition-capable descriptor work from recurring and event-time post-lock view resolution. No profiler number is claimed; the value is stricter row ownership, less lock-scope ambiguity, and fewer hidden cache/metadata touches.

## Decision 52 - Immutable Ore Read-Model Snapshots

Problem: `IWorldResourceSpawnerReadModel` still exposed writable `NativeArray<float3>` and `NativeArray<int>` lanes to consumers. The GPR consumer wrapped those lanes in read-only `NativeSlice` fields later, but the contract itself allowed downstream mutation of geology-owned Vault rows.
Solution: Replace the public methods with `TryGetOrePositionsReadOnly` and `TryGetOreTypesReadOnly`, returning `NativeArray<T>.ReadOnly` views produced from existing Vault handles. `GroundPenetratingRadarRuntime` now consumes those immutable snapshots directly, and `GroundRadarRaymarchJob` stores the ore inputs as `[ReadOnly, NoAlias] NativeArray<T>.ReadOnly`.
Rejected Alternatives: Leaving writable arrays because the current consumer behaved; copying ore rows into a private GPR buffer; adding a managed adapter object; keeping mutable arrays and relying only on job attributes.
Scalability potential: Low/middle/high/ultra tiers keep the same zero-copy Vault read path. Continuous quality still scales scan cadence, ray count, and visual geology density; the immutable contract does not change gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: No profiler number is claimed. The gain is alias-surface reduction: consumers cannot mutate ore SoA rows through the registry read model, and the GPR Burst job keeps non-overlapping read lanes explicit for vectorization without allocating or copying.

## Decision 53 - Ore Reader Job Lifetime Fence

Problem: Immutable `NativeArray<T>.ReadOnly` snapshots prevent consumer mutation, but they do not by themselves stop a later geology write from racing a scheduled GPR job still reading `OrePositions` and `OreTypes`.
Solution: Add `IWorldResourceSpawnerReadDependencySink.RegisterOreReadDependency(JobHandle)`. GPR registers its scheduled scan handle only when an ore scan is active and ore rows are present. `ProceduralOreSpawner` combines those reader handles, clears completed fences without blocking, refuses DataVault rebind while a reader is active, and fails closed before writer-lock routes that mutate ore positions/types: generation, depletion/compaction, and runtime shift. Structural teardown may force-complete the reader fence with an explicit blocking-sync comment.
Rejected Alternatives: Copying ore lanes into a private GPR buffer; completing the reader handle on every harvest/regeneration attempt; adding a new global scheduler lane; trusting frame order between `IUpdatable`, `ISlowTickable`, interaction commands, and `ILateFrameTickable`.
Scalability potential: Low tier still reads sparse ore rows zero-copy and simply delays ore-row mutation until the scanner job retires. Middle/high/ultra keep dense Dear Lie geology matrices and GPR scans without changing gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: No profiler number is claimed. The runtime cost is one `JobHandle.CombineDependencies` per active ore scan and `IsCompleted` checks before ore-row writer locks. The avoided failure is a NativeArray safety race and cache-line corruption between scanner reads and geology writes.

## Decision 54 - Reader Fence Finalization Discipline

Problem: A completed Unity `JobHandle` still has to be finalized with `Complete()` before main-thread NativeArray ownership is safe again. The Loop 49 draft cleared completed reader fences by `IsCompleted` alone and called that cleanup from ore read accessors, which was both unsafe and a read-accessor purity violation.
Solution: Replace the clear helper with `TryFinalizeCompletedOreReadDependency()`, backed by `DispatcherJobFence.TryFinalizeCompleted(ref _pendingOreReadDependency)`. Slow/Late owner phases and writer-lock guards call that side-effecting helper. `CanExposeReadSnapshot()` now only reads flags and fails closed while a pending reader fence exists. If GPR registers an already completed scan handle, the sink finalizes that local copy immediately without blocking.
Rejected Alternatives: Leaving `IsCompleted` as ownership proof; completing reader jobs inside `TryGetOrePositionsReadOnly` / `TryGetOreTypesReadOnly`; copying ore rows into a private GPR buffer; forcing every ore mutation to block on an active reader.
Scalability potential: Low tier keeps sparse zero-copy scans and waits at most until the owner phase finalizes a completed scan. Middle/high/ultra keep denser scan and Dear Lie geology output without adding copied buffers or changing the authority route.
Hardware Impact: No profiler number is claimed. The cost is a non-blocking completed-handle finalization in owner/mutation phases. The avoided failure is a safety-handle leak or unsafe main-thread write after `IsCompleted` but before `Complete`.

## Decision 55 - Ore Contract Compile-Wall And Teardown Closeout

Problem: The immutable ore-reader contract introduced explicit `JobHandle` surface area and exposed three declaration gaps: World.Contracts used `Unity.Jobs.JobHandle` without an asmdef reference, legacy root `Hecton8.Core` already consumed world contract types through `GlobalRegistry`/GPR without declaring `Hecton8.World.Contracts`, and geology registered active jobs against `SystemID.WorldResourceSpawnerRuntime` before that owner ID existed in `H8Memory.SystemID`. Goodall also found that structural teardown skipped finalization when the reader handle was already completed.
Solution: Add `Unity.Jobs` to `Hecton8.World.Contracts.asmdef`, add `Hecton8.World.Contracts` to `Hecton8.Core.asmdef`, add `SystemID.WorldResourceSpawnerRuntime = 157` to match the existing registry slot, keep the `Hecton8.Gameplay` namespace import in GPR because `ISubmarineState` currently lives in the legacy root assembly, and make `CompletePendingOreReadDependencyForTeardown()` finalize completed reader handles before clearing the pending flag.
Rejected Alternatives: Reverting the immutable reader contract; copying ore lanes into a GPR-owned buffer; leaving asmdef references implicit; using `SystemID.WorldStreaming` for geology job telemetry; clearing an already completed reader handle without `Complete()`.
Scalability potential: No gameplay-quality change. Low/middle/high/ultra all keep the same zero-copy ore read path and continuous quality scaling; this pass only makes declared assembly routes, owner IDs, and teardown ownership match the existing source contract.
Hardware Impact: No frame-path cost claimed. The avoided failures are Unity import/compile breakage, missing H8Memory owner telemetry, and unsafe NativeArray ownership release during structural teardown.

## Decision 56 - GPR Cache Ownership And Generation Handles

Problem: `GroundPenetratingRadarRuntime` still had several standard Unity/legacy patterns: persistent pointer-bearing Vault handles, public read properties that resolved native rows on demand, hot-adjacent registry polling for DataVault/submarine/player/voxel/ecosystem state, and a hot-swap path that would rebind twice because `GlobalRegistry` calls the ref hook before the compatibility hook.
Solution: Persist only `VaultGenerationHandle<T>` descriptors for GPR lanes, cache DataVault/player/submarine/voxel/ecosystem services at enable time and through `IGlobalRegistryHotSwapRefListener`, and return cached `NativeArray<T>.ReadOnly` snapshots from GPR read properties. DataVault rebind clears descriptors and reacquires GPR rows only after the scan job is not scheduled. The non-ref hot-swap callback is intentionally no-op in this class because the ref hook owns cache mutation.
Rejected Alternatives: Keeping legacy `VaultBufferHandle<T>` descriptors; resolving `GlobalRegistry.DataVault` inside every buffer read helper; copying GPR hits into private managed or native arrays; allowing both hot-swap callbacks to clear/reallocate Vault rows; removing submarine-origin support just to avoid the `Hecton8.Gameplay` namespace import while `ISubmarineState` still lives in the root assembly.
Scalability potential: Low tier still gets the cheapest single-buffer GPR ping path and avoids hidden rebind churn. Middle/high/ultra keep zero-copy Vault snapshots, macro-swarm visual pings, and procedural indirect drawing without changing gameplay truth, save identity, or authority route.
Hardware Impact: No profiler number is claimed. The gain is route discipline: no per-frame registry/DataVault polling in the GPR tick path, no stale pointer descriptor retained across Vault generations, and no duplicate hot-swap reallocation event. On i3/MX350-class hardware this removes avoidable branch/cache work from scanner frames; on high-end hardware it preserves the same dense scan/visual route.

## Decision 57 - Continuous GPR Quality And Deterministic Slot Seed

Problem: GPR ray layout still carried tier-shaped behavior and Unity frame/time reads, while geology's authoritative slot seed used `Unity.Mathematics.Random.CreateFromIndex(...).NextUInt()`. The local slot-machine mandate for SHINOBU requires pure deterministic hash/RNG state, and the global doctrine forbids binary quality switches for scalable work.
Solution: GPR now reads `HomeostasisBrain.GlobalQualityWeight`, sanitizes it, and uses `math.smoothstep` plus `math.lerp` to select 4..64 rays and 1..configured max raymarch steps continuously. `Time.frameCount` is replaced by `TimeSliceScheduler.CurrentFrameId` with an explicit deterministic fallback counter, and `Time.time` is replaced by accumulated render delta for visual pulse phase. `ResolveSlotSeed(int slot)` now uses pure sector/slot/seed integer mixing and never calls a mutable RNG object in the generation job.
Rejected Alternatives: Keeping a low/high ray-count branch; keeping `Time.time` because the pulse is visual; keeping `CreateFromIndex(...).NextUInt()` because `Unity.Mathematics.Random` is Burst-compatible; adding a copied RNG state array to the Vault; changing gameplay seed layout or depletion identity.
Scalability potential: Low quality collapses GPR to 4 rays and 1 SDF step while preserving the same ore truth route. Middle quality smoothly expands the grid and depth work. Ultra quality reaches 64 rays and configured max depth steps while macro-swarm pings and GPU procedural presentation continue to spend saved CPU time on visible output.
Hardware Impact: No profiler number is claimed. The cheap-device path bounds scanner ALU and SDF memory reads continuously; the high-end path buys denser presentation without changing DTO layout or save identity. The pure integer seed removes mutable RNG state from the authoritative geology job and preserves rollback replay.

## Decision 58 - Evidence Order And GPR Cold Allocation Hygiene

Problem: The on-disk LOG had stale evidence drift: Task 06 still described `Unity.Mathematics.Random`, older Loop 49/50/54 entries each called themselves a bottom/current tail, and GPR fallback/dump cold allocations were less explicit than the geology-owned allocation labels. GPR also retained a dead `LowTierRays` constant after continuous quality selection replaced the binary tier path.
Solution: Correct Task 06 evidence to pure integer mixing, demote historical LOG tail labels to evidence restatements, append a new Loop 55 tail entry, delete `GroundRadarConstants.LowTierRays`, label GPR fallback material/mesh/array and dump writer cold allocations, and replace the GPR blackbox failure string-concat log with `Debug.LogException`.
Rejected Alternatives: Leaving historical contradictions for later integrators; moving large LOG blocks and risking more report churn; keeping `LowTierRays` as a harmless constant; keeping string concatenation because blackbox failure is cold.
Scalability potential: Low tier keeps continuous 4-ray/1-step scanner collapse without any binary constant inviting regression. Middle/high/ultra still spend scan work through `GlobalQualityWeight` and keep GPR visual pings on the existing indirect path.
Hardware Impact: No profiler number is claimed. Runtime path impact is removal of a regression vector and one cold failure-path managed concatenation. The practical gain is audit correctness and preventing future binary-tier or unlabeled-allocation drift.

## Decision 59 - Dispatcher-Owned Memory Telemetry Frame And Core Contract Boundary

Problem: Source audit found core memory telemetry using `Time.frameCount`, which makes blackbox evidence depend on Unity frame state instead of dispatcher-owned simulation cadence. The same audit flagged `Hecton8.Core.asmdef` referencing `Hecton8.World.Contracts`; root-assembly files needed the radar/read-model interfaces, so deleting the reference alone would break import.
Solution: Add a dispatcher-owned telemetry frame slot to `H8Memory`, publish `TimeSliceScheduler.CurrentFrameId` in `SystemDispatcher.RecordMemoryBlackBoxHeartbeat()`, and make both `H8Memory` and `GlobalDataVault` blackbox entries call the pure `ResolveTelemetryFrame(sequence)` fallback. Move `GroundRadarContracts.cs` into `Hecton8.Core.Contracts` while keeping the `Hecton8.World` namespace and preserving the file meta GUID, then remove `Hecton8.World.Contracts` from `Hecton8.Core.asmdef`.
Rejected Alternatives: Keeping `Time.frameCount`; adding a `Hecton8.Core.Memory` dependency on `SystemDispatcher`; deleting the asmdef reference without moving the radar/read-model contracts; moving runtime MonoBehaviours between folders; using object-typed registry slots that would erase the typed contract proof.
Scalability potential: Low/middle/high/ultra tiers now share one dispatcher-frame evidence route for memory and Vault blackboxes. The contract move changes compile routing only; it does not change quality behavior, DTO layout, save identity, or authority route.
Hardware Impact: No profiler number is claimed. The frame-source fix removes Unity frame API reads from memory forensic writes and avoids a new cross-assembly dependency from memory into dispatcher code. The contract move removes a Core-to-World contract dependency without moving runtime code or adding a managed adapter.

## Decision 60 - Log Tail Evidence Correction

Problem: The Loop 56 LOG block was inserted above older evidence restatements because the patch anchor matched an earlier audit close tag. The bottom of `LOG_SHINOBU_153.md` still showed Loop 51 evidence, violating the report protocol that the newest evidence must be at the bottom.
Solution: Append a Loop 57 correction at the actual file tail, restating the current memory-frame and contract-route evidence and preserving the compile gate result.
Rejected Alternatives: Leaving the current evidence buried near the top; moving historical blocks and creating large report churn; claiming chat output is enough.
Scalability potential: No runtime quality change. This is proof-artifact hygiene so weak/middle/high/ultra behavior evidence remains discoverable after context loss.
Hardware Impact: No frame-path cost. The gain is audit reliability: the disk log now carries the latest route proof at the actual tail without forcing a build or touching runtime systems again.

## Decision 61 - Generated Project Staleness Boundary

Problem: Source asmdefs now remove the Core-to-World.Contracts dependency, but the non-tracked Unity-generated `Hecton8.Core.csproj` still contains a stale `<ProjectReference Include="Hecton8.World.Contracts.csproj" />`. Unity project files are an import artifact; editing them directly would create false proof and be overwritten by regeneration.
Solution: Treat asmdef source as the authority, preserve the source-level contract move into `Hecton8.Core.Contracts`, and record the stale generated-project state as pending Unity regeneration/build proof once the CPU/compiler gate opens.
Rejected Alternatives: Manually editing generated `.csproj` files; moving runtime MonoBehaviours across domains; restoring the sibling contract reference in `Hecton8.Core.asmdef`; forcing a build while CPU load is 100%.
Scalability potential: Low/middle/high/ultra runtime behavior is unchanged. This is compile-wall proof hygiene only; the ore/GPR route still scales through continuous `GlobalQualityWeight` and immutable zero-copy read snapshots.
Hardware Impact: No frame-path cost. Avoiding generated-project edits prevents a fake compile-route fix and preserves the source-of-truth boundary until Unity import can regenerate projects without violating the active build gate.

## Decision 62 - Meitner Residual Hot-Path Cleanup

Problem: Meitner found residual non-DOD patterns that Loop 58 missed: GPR persisted native read-only aliases and a managed component-probe list inside a `MonoBehaviour`, memory/Vault telemetry still used `Time.frameCount` in sovereignty and pressure paths, dispatcher dependency retries still polled `GlobalRegistry` from the frame loop, and GlobalDataVault retained binary low/high memory thresholds.
Solution: Make GPR read accessors resolve immutable views directly from cached Vault generation handles and remove the persistent List probe. Add `ResolveMemoryTelemetryFrameId()` to SystemDispatcher and route Vault sovereignty, pressure, and massive-move telemetry through dispatcher frame IDs plus a monotonic fallback. Remove frame-loop retry polling for input determinism, job admission, and simulation bucketing; cold initialization and hot-swap events own those caches. Replace binary Vault capacity/fragmentation branches with smooth profile curves via `GlobalDataVault.DecodeScalabilityProfile01()`.
Rejected Alternatives: Keeping read-only native aliases because they are not allocations; keeping retry polling at an 8-frame cadence; manually editing generated `.csproj` files to mask source truth; preserving low/high memory thresholds for legacy semantics.
Scalability potential: Low profile now maps to low memory/fragmentation tolerance, middle profiles interpolate smoothly, and high/ultra profiles reach the previous high budget without a binary jump. GPR still scales rays and SDF steps continuously through `GlobalQualityWeight`; the cleanup does not change gameplay truth, DTO layout, save identity, or authority route.
Hardware Impact: No profiler number is claimed. The change removes persistent native alias state from GPR, eliminates managed List ownership in the owner MonoBehaviour, removes retry registry polls from dispatcher frames, and makes Vault memory pressure thresholds continuous for i3/MX350 through high-end hardware instead of branching on a binary tier.

## Decision 63 - Meitner Report Tail Repair

Problem: The detailed Loop 59 report block landed above older LOG evidence, repeating the earlier evidence-order failure pattern.
Solution: Append a concise Loop 60 block at the actual `LOG_SHINOBU_153.md` bottom with the current Meitner-cleanup source state and verification scans.
Rejected Alternatives: Moving large historical LOG blocks; relying on chat; leaving the latest source cleanup buried above older evidence.
Scalability potential: No runtime behavior change. This preserves proof-artifact order for the continuous GPR/Vault scalability work already in source.
Hardware Impact: No frame-path cost. The gain is audit recoverability after context loss.

## Decision 64 - Pure Read Accessors And GPR Procedural Indirect

Problem: Epicurus found that GPR and geology public read accessors still resolved Vault handles through `TryResolveHandle`, whose failure path mutates generation-fault telemetry and dumps. GPR also retained a cold `GetComponent` convenience probe and rendered pings through `RenderMeshIndirect` plus indexed mesh args, contradicting the procedural indirect presentation route.
Solution: Added `IDataVault.TryReadHandle()` as a no-fault pure read resolver. `GroundPenetratingRadarRuntime` public read/copy paths and `ProceduralOreSpawner.TryGetOre*ReadOnly()` now use pure `TryRead*` helpers, while owner mutation/job paths keep the fault-recording resolver. Removed the GPR component probe. Converted GPR ping rendering to `Graphics.DrawProceduralIndirect` with a 16-byte explicit `GroundRadarIndirectArgsDTO`; the shader now derives its quad from `SV_VertexID` and instance ID, so no fallback mesh or indexed indirect args remain. Dispatcher signal/timing/blackbox frame evidence now uses dispatcher-owned frame IDs for the flagged paths, and the Vault arena fallback clamp no longer uses a low/high ternary branch shape.
Rejected Alternatives: Caching persistent `NativeArray<T>.ReadOnly` fields again; allowing read accessors to mutate Vault telemetry; keeping `RenderMeshIndirect` because it was presentation-only; documenting the cold component probe instead of deleting it; forcing a build while compiler processes were active.
Scalability potential: Low quality keeps the same continuous 4-ray/1-step GPR collapse and submits only six procedural vertices per live ping. Middle/high/ultra scale scan density and ping count through `GlobalQualityWeight`, with the GPU deriving presentation quads directly instead of the CPU owning mesh/index setup.
Hardware Impact: No profiler number is claimed. The source removes mesh fallback allocation, indexed indirect argument dependence, a cold component search, read-accessor telemetry mutation, and binary clamp shape; on i3/MX350-class devices this reduces presentation setup surface and preserves pure zero-copy ore reads.

## Decision 65 - Voxel SDF Facade And Dispatcher Frame Proof

Problem: Tesla found GPR still crossed into the concrete voxel/cave surface for SDF lookup, and Curie found remaining dispatcher proof paths keyed by Unity `Time.frameCount`. Those patterns weaken compile-wall routing and make blackbox/job telemetry frame evidence depend on Unity frame state rather than the dispatcher frame sequence.
Solution: Add `IVoxelSonarSdfReadModel` to the core contract surface and expose it through `GlobalRegistry.VoxelSonarSdf`; `HectonVoxelEngine` implements the interface and owns the concrete `HectonVoxelVolume` call. GPR now stores only the interface and its pure SDF/ore helpers use `TryRead*` naming. `SystemDispatcher` now records the AUP pre-shift barrier against `_currentDispatcherFrameId`, publishes time-dilation/camera/job-dependency/mock-signal evidence with dispatcher frame IDs, and no longer uses the exact Curie-flagged `Time.frameCount` patterns in those paths. The dormant ore visual switch was verified as already converted to a continuous `dormantOreVisualWeight * smoothstep(GlobalQualityWeight)` scalar.
Rejected Alternatives: Reintroducing direct GPR references to `HectonVoxelEngine` or `HectonVoxelVolume`; copying SDF bytes into a GPR-owned buffer; keeping `Time.frameCount` as "close enough" proof; breaking the existing cockpit/tool GPR contract to hide `GraphicsBuffer` this loop; removing the cold serialized `MapMagicBridge` field without a terrain-owner route card.
Scalability potential: Low quality keeps GPR at the existing continuous 4-ray/1-step SDF path and can reduce dormant ore presentation to zero without changing ore truth. Middle/high/ultra keep denser GPR ray work, richer ping presentation, and dormant ore visual weight through the same scalar route. The SDF facade changes dependency ownership only; it does not change DTO layout, save identity, rollback identity, or authority route.
Hardware Impact: No profiler number is claimed. The expected low-end gain is compile-wall and branch-cache hygiene: GPR no longer imports concrete voxel/cave types, private read helpers avoid side-effect naming, and dispatcher telemetry uses one owner frame sequence instead of Unity frame reads in the corrected proof paths. CPU build verification was not launched because CPU sampled at 94% and `VBCSCompiler` was active.

## Decision 66 - Loop 62 Log Tail Repair

Problem: The Loop 62 report block was appended to the first matching audit close tag instead of the actual bottom of `LOG_SHINOBU_153.md`, repeating an earlier report-order defect.
Solution: Append a Loop 63 repair block at the file bottom that restates the current Loop 62 source state, static scan evidence, and build-gate reason.
Rejected Alternatives: Moving large historical log blocks; relying on chat output; leaving the newest evidence buried above older entries.
Scalability potential: No runtime behavior change. This is proof-artifact ordering so continuous GPR/geology scalability evidence survives context loss.
Hardware Impact: No frame-path cost. The gain is audit recoverability for the actual source state without launching a blocked build.
