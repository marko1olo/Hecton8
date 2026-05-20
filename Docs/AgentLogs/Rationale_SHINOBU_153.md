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
Solution: Regenerate unmined ore from `worldSeed ^ AUP sector hash ^ deterministic slot` through `Unity.Mathematics.Random.CreateFromIndex` plus LCG. Persist only candidate-slot depletion masks.
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
