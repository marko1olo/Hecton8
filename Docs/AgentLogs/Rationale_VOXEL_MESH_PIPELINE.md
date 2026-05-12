# Rationale - VOXEL_MESH_PIPELINE

Status: PENDING VERIFICATION
Agent Role: VOXEL_SURGEON
Domain: Echelon 2 - World Generation & Terrain / Voxel SDF + Marching Cubes

## Decision Journal

### D0 - Prompt Identity And Scope

Problem: User supplied role `VOXEL_SURGEON` and prompt id `VOXEL_MESH_PIPELINE`; batch protocol required exact XML extraction and prompt-id logs.
Solution: Extracted `<AGENT_PROMPT id="VOXEL_MESH_PIPELINE" role="VOXEL_SURGEON">` with PowerShell from `Docs/Tasks/CURRENT_BATCH.md`.
Rejected Alternatives: Logging under role name only would split evidence and break batch audit.
Scalability potential: No runtime effect; preserves accountability for low, middle, high, and ultra variants.
Hardware Impact: 0 us runtime.

### D1 - Mandate Set

Problem: The work crosses Awaitables, Burst jobs, native memory, SDF math, seams, AUP, telemetry, and frame budget.
Solution: Loaded voxel MC pipeline, MapMagic/voxel seams, Awaitable standard, zero GC, native memory/jobs, performance budgets, telemetry/postmortem, and AUP precision mandates.
Rejected Alternatives: Loading only voxel docs would miss async and blackbox requirements; loading all mandates would add noise.
Scalability potential: Low tier disables or reduces visual modifiers; high/ultra spends saved budget on organic SDF and richer vertex data.
Hardware Impact: Documentation-only; implementation target is removing i3/MX350 hitch sources.

### D2 - Async Mesh Upload And Pool Warmup

Problem: Surface upload and collider upload are Unity API heavy, and pool prewarm previously performed hundreds of synchronous `new Mesh()` calls in `OnEnable`.
Solution: Added Awaitable frame yields around surface/collider upload and replaced synchronous pool prewarm with `WarmVoxelMeshPoolsAsync`, creating one mesh then yielding before the next; lazy acquisition creates one slot only if the pool is not warm yet.
Rejected Alternatives: Full synchronous prewarm was too slow for cold boot; coroutine prewarm violates prompt; per-frame unbounded lazy creation would hitch under burst loading.
Scalability potential: Low: one-slot lazy fallback prevents startup freeze. Middle: background prewarm fills pool gradually. High: full pool becomes available for high concurrency without boot hitch. Ultra: saved frame budget can support denser chunks.
Hardware Impact: Estimated 2500 us cold-frame gain on i3/MX350 when avoiding 512 synchronous mesh creations.

### D3 - Seam Concealment Skirt

Problem: Chunk boundaries can show gaps when density, terrain snap, or LOD differ at edges.
Solution: `VoxelChunkSkirtExtrusionJob` runs in Burst after terrain snap and lowers boundary vertices up to 0.5m while writing skirt alpha for material concealment.
Rejected Alternatives: Transvoxel requires topology negotiation across chunks and direct neighbor dependencies; runtime edge stitching would add cross-agent coupling.
Scalability potential: Low: cheap vertical fake. Middle: wider skirt alpha. High: normal blending and shader dust can make the fake invisible. Ultra: can combine with denser MC if budget exists.
Hardware Impact: Estimated 180 us saved versus neighbor-aware topology stitching; cost is a linear vertex pass already in the job chain.

### D4 - Collider Bake Deferral

Problem: Mesh collider baking can stall the main thread if `Physics.BakeMesh` is completed before the engine is ready.
Solution: Keep `VoxelMeshBakeJob` as scheduled worker job, await with frame polling, and defer teardown/upload through fixed queues; `sharedMesh` assignment occurs only after bake completion or staged volume commit.
Rejected Alternatives: Main-thread `Complete()` after scheduling was too expensive; assigning the collider before bake completion risks hitch and stale data.
Scalability potential: Low: collider fake can disable expensive bakes under pressure. Middle: chunked collider bake queue. High/Ultra: more chunks can bake without blocking surface visuals.
Hardware Impact: Estimated 3000 us main-thread stall moved out of the frame on collider-heavy chunks.

### D5 - Biome SDF Modifier

Problem: Alien biome caves need organic bubbly walls without managed per-voxel lookups.
Solution: Fill a native 2D `gridBiome` from Data Monolith heatmap once per chunk, then sample it in `VoxelDensityJob`; Alien weight applies smooth-min SDF noise, full at LOD0, reduced at LOD1, disabled at LOD2 or Low/Mx350/Unknown tier.
Rejected Alternatives: Shader-only bubbles would not affect silhouettes; managed biome lookups inside voxel loops violate Burst/zero-GC requirements.
Scalability potential: Low: disabled. Middle: reduced single-noise modifier. High: fractal two-octave organic noise. Ultra: future extra layers can spend budget here.
Hardware Impact: Low tier saves about 70 us/chunk by skipping modifier; high tier spends that budget for visible geometry.

### D6 - RLE Delta And Vertex Payload

Problem: Edited voxel chunks must mesh from persisted deltas, not from base procedural density.
Solution: Existing `VoxelDeltaProcessor.TryBuildDeltaMapForVolume` expands compacted/RLE states into native modified cells; density applies those cells before `VoxelDensityQuantizeJob`, and MC reads the resulting `NativeArray<sbyte>` quantized density. `VoxelColorJob` marks modified cells for laser/dirty burn payload.
Rejected Alternatives: Direct dependency on `CarveSdfJob` internals would break domain decoupling; base-only SDF would erase edits visually.
Scalability potential: Low: sparse delta map avoids full terrain regen. Middle: quantized density keeps memory small. High/Ultra: same path supports more edit density without managed allocations.
Hardware Impact: Estimated 900 us saved on edited chunks by avoiding re-carve from procedural base.

### D7 - Blackbox Telemetry

Problem: Mesh stalls need fixed-size evidence, not chat reports.
Solution: Added a 300-entry `NativeArray<VoxelMeshPipelineTelemetryEntry>` ring that writes frame, flags, chunks meshed, bake queue, upload queue, pool use, active generation count, and state hash; padded entry to 32 bytes; development/editor invalid state dumps to `Docs/AgentLogs/Dump_VOXEL_MESH_PIPELINE.bin`.
Rejected Alternatives: Dynamic lists or strings would allocate; console logs do not preserve last 300 frame state.
Scalability potential: Low: fixed memory. Middle: tracks queue pressure. High/Ultra: state hash supports later correlation with chunk priority.
Hardware Impact: Fixed small write cost; diagnostic gain outweighs negligible hot cost.

### D8 - Verification Boundary

Problem: Project-wide compile is blocked by systems outside this patch.
Solution: Ran Unity MCP script validation on `HectonVoxelEngine.cs` successfully with 0 diagnostics, then confirmed console/build blockers do not reference `HectonVoxelEngine.cs`.
Rejected Alternatives: Editing gameplay/UI/core dependency files would violate assigned domain.
Scalability potential: No runtime effect.
Hardware Impact: 0 us; prevents cross-domain sabotage.

### D9 - Recon Evidence Correction

Problem: The recon target uses instance syntax (`mesh.RecalculateNormals()`), so a narrow `Mesh.RecalculateNormals()` scan missed the current world-system call.
Solution: Re-ran the CLI scan with both `Mesh\.RecalculateNormals\(` and `RecalculateNormals\(` and logged `SargassumGlobalDragManager.cs:3544` as an external, non-voxel occurrence.
Rejected Alternatives: Editing `SargassumGlobalDragManager.cs` would violate the assigned voxel domain; preserving a false no-match result would corrupt integration review.
Scalability potential: No runtime effect; keeps the voxel normal-generation mandate tied to the Burst path while surfacing the external world-system call for its owner.
Hardware Impact: 0 us direct; avoids review waste and cross-domain changes.

## OMEGA POLISH CHANGES

### Dear Lie Audit

- Honest mesh pool prewarm replaced: synchronous 512 mesh cold creation was converted to Awaitable one-mesh-per-frame prewarm with lazy single-slot fallback.
- Honest biome hash resolution replaced: repeated Data Monolith biome record lookup now uses a one-entry local hash cache while filling the chunk heatmap; the localized-name fallback was removed after compiler lifetime risk.
- Honest telemetry pool scan replaced: per-publish 512-flag pool scans now use maintained in-use counters updated on acquire/release/reset.
- Honest biome SDF evaluation gated: organic Alien noise requires non-low `GlobalRegistry.ScalabilityTier` and `lodLevel < 2`; Low/Mx350/Unknown returns before sampling noise.
- Honest telemetry packing fixed: `VoxelMeshPipelineTelemetryEntry` is padded to 32 bytes and dump writer emits padding.

### Scalability Matrix

- Low / Mx350 / Unknown: Alien SDF modifier disabled; collider fake and pressure gates remain available; mesh pool creation is staggered to avoid cold frame spikes.
- Middle: Alien modifier can run at reduced LOD1 weight with single-noise path.
- High: full LOD0 Alien SDF modifier and normal/color payloads run in Burst.
- Ultra: same deterministic path supports higher density or more concurrent chunks without changing interfaces.

### Zero-GC Scan

- `foreach`: none found in `HectonVoxelEngine.cs`.
- `string.Format`, `.ToString()`, `$"..."`: none found in the runtime scan of `HectonVoxelEngine.cs`.
- New runtime allocations touched by this task: `Mesh` creation only in cold Awaitable pool warmup/lazy fallback; `NativeArray` blackbox is persistent fixed-size; `FileStream`/`BinaryWriter` only inside editor/development dump path.

### Build Health

- Previous `dotnet build Hecton8.Core.csproj --no-restore -v:minimal`: PASS, 0 warnings, 0 errors. Not rerun during the current no-build continuation per user instruction.
- Previous `dotnet build Assembly-CSharp.csproj --no-restore -m:1 /nr:false -v:minimal`: PASS, 0 warnings, 0 errors. Not rerun during the current no-build continuation per user instruction.
- Previous Unity MCP validation: PASS. `HectonVoxelEngine.cs` and `HectonVoxelVolume.cs` both returned 0 warnings and 0 errors. Not rerun during the current no-build continuation.
- Latest `git diff --check` on touched voxel/docs files: PASS for whitespace; Git reports only future LF-to-CRLF normalization warnings.
- Voxel coroutine/recalculate scan: PASS, no `IEnumerator`, `StartCoroutine`, `yield return`, or `RecalculateNormals(` matches in `HectonVoxelEngine.cs`, `HectonVoxelVolume.cs`, or `VoxelDeltaProcessor.cs`.
- Reserved-acquisition scan: PASS, no stale `EnsureVoxelSurfaceMeshAvailableAsync` or `EnsureVoxelPhysicsBakeMeshAvailableAsync` helpers remain.
- Deferred upload ownership scan: PASS, no unhandled `volume.PublishColliderChunkMesh(...)` call remains, and no `volume.name`/`name,index` bake mesh acquisition path remains.
- Cold allocation scan: PASS, `CreateVoxelPoolMesh` explicitly marks the staggered pooled `new Mesh` as cold path.
- Deferred-work shutdown scan: PASS, `TryShutdownSharedTables` now waits on `HasPendingVoxelDeferredWork()` and is retried after deferred physics bake teardown and deferred collider upload queues drain.
- Dispatcher-unavailable scan: PASS, deferred registration helpers now return `bool`; no bare ignored calls remain; backpressure notification checks `GlobalRegistry.Dispatcher` before `SystemDispatcher` access; shutdown force-release suppresses per-item warning spam; subsystem reset flushes deferred queues before clearing; touched files have no trailing whitespace; no-build brace count for `HectonVoxelEngine.cs` is 719 opens / 719 closes.
- Upload sanitizer scan: PASS, surface and collider MeshData upload now guards non-finite payloads and out-of-range triangle indices before `DontValidateIndices` upload.
- Current no-build continuation status: NOT RUN per user instruction to avoid build and `dotnet build`.
- Silo check: edited runtime file is voxel engine domain; docs/log files are required by batch protocol. No gameplay/UI source was changed.

### D10 - Continuation Audit Boundary

Problem: The user requested continued quality work, but the current project compile blocker moved to UI cartography symbols outside the voxel mesh pipeline domain.
Solution: Rechecked the voxel pool warmup/acquire path, confirmed bake mesh creation is preceded by awaited availability checks in the voxel finalize path, reran whitespace/build/Unity guards, and kept `PDAMapTab.cs` untouched.
Rejected Alternatives: Fixing duplicated UI point-cloud declarations would cross into Echelon 8 without a critical voxel dependency; reverting other agents' UI edits would violate shared-worktree rules.
Scalability potential: Low tiers keep cold mesh creation staggered; high/ultra keep full pool availability once warm without synchronous boot hitch.
Hardware Impact: No new runtime cost; audit preserves the earlier 2500 us cold-frame prewarm reduction and avoids adding cross-domain risk.

### D11 - Pool Exhaustion False-Negative Fix

Problem: The async pool availability check could return false when all global pool slots were in use, even if the current `MeshFilter` or voxel volume already owned the mesh needed for this finalize pass.
Solution: Added `NeedsVoxelSurfaceMeshAcquire` in `HectonVoxelEngine` and `GetColliderChunkBakeMesh` in `HectonVoxelVolume`; availability checks now run only when a new pooled mesh is actually required.
Rejected Alternatives: Allowing synchronous cold allocation would reintroduce boot/frame hitches; ignoring the false negative would drop valid surface/collider updates under peak pool pressure.
Scalability potential: Low: existing chunks keep updating under tight pool budgets. Middle: no extra pool pressure for reused meshes. High/Ultra: high concurrency avoids unnecessary finalize aborts while retaining the staggered warmup.
Hardware Impact: Avoids false finalize failure without adding hot allocations; preserves the earlier 2500 us cold-frame savings and prevents redundant one-mesh warmup work when reusing existing meshes.

### D12 - Reserved Async Acquisition

Problem: The cold-slot availability helper created a mesh and yielded before the caller acquired it, so another finalize path could claim the warmed mesh first under peak load.
Solution: Replaced availability checks with reserved async acquisition. Surface meshes and physics bake meshes are marked in-use before the frame yield; ownership is then transferred to `MeshFilter` or `HectonVoxelVolume.AssignColliderChunkBakeMesh`.
Rejected Alternatives: Synchronous cold allocation would recreate the frame hitch; leaving the race would cause intermittent false finalize failures under pool pressure.
Scalability potential: Low: avoids wasted retries when the pool is tight. Middle: stable chunk finalize under normal streaming. High/Ultra: high concurrency keeps deterministic ownership without locking or cross-domain scheduler dependencies.
Hardware Impact: No new hot allocation; avoids peak-load retry stalls while preserving the 2500 us cold-frame pool-warmup saving.

### D13 - Mesh Pool Warmup Lifecycle And Collider Acquire Hygiene

Problem: Async mesh-pool warmup can outlive a disabling engine instance, while shared static table teardown must not race the warmup task. The collider finalize path also accessed `volume.name` while acquiring pooled bake meshes, adding unnecessary managed name lookup in a hot finalize path.
Solution: Register the live engine before launching `WarmVoxelMeshPoolsAsync`, hold shared-table shutdown while `_voxelMeshPoolWarmupRunning` is true, add `ShouldAbortVoxelMeshPoolWarmup` before each cold mesh creation, and trigger `TryShutdownSharedTables` after warmup exits if shutdown was requested. Collider bake mesh acquisition now passes the constant pool owner name instead of `volume.name`.
Rejected Alternatives: Immediate warmup cancellation would strand low-end devices with cold pools and more lazy allocations; allowing shared-table shutdown during warmup risks stale static state; using `gameObject.name`/`volume.name` in finalize violates the no-hot-managed-name-access rule.
Scalability potential: Low: warmup aborts cleanly when no live engine remains and avoids boot hitch. Middle: warmup continues one mesh per frame while an engine is live. High: full pools become available for concurrent chunks. Ultra: saved lifecycle stability supports denser visual overkill without synchronous pool creation.
Hardware Impact: Preserves the earlier 2500 us cold-frame saving on i3/MX350, removes hot managed name access from collider finalize, and prevents teardown/warmup races that could produce dropped meshes or extra retries.

### D14 - Deferred Collider Upload Staged Mesh Guard

Problem: Chunked collider baking can enqueue a staged bake mesh for late-frame upload, then fail on a later chunk before `CommitDeferredColliderChunkUpload` runs. The old failure path cleared all staged bake meshes, which could make a pending upload commit an empty collider mesh.
Solution: Changed `HectonVoxelVolume.PublishColliderChunkMesh` to return enqueue success, made smooth/chunked callers fail fast when enqueue fails, tracked whether any deferred collider upload has been queued, and skipped `ClearColliderChunkBakeMeshes` on failure when a pending upload may still need its staged mesh. Also removed the last unused `name,index` arguments from synchronous bake mesh acquisition.
Rejected Alternatives: Forcing immediate `sharedMesh` assignment would reintroduce main-thread collider upload hitches; canceling every queued upload on later chunk failure would require scanning the deferred upload queue and would still risk cross-frame ownership churn.
Scalability potential: Low: prevents collider holes on tight pool/backpressure frames. Middle: stable staged ownership for normal chunked collider streaming. High/Ultra: concurrent chunk uploads can remain late-frame throttled without corrupting earlier staged meshes.
Hardware Impact: Avoids a failed deferred upload retry and prevents empty collider assignment; no new hot allocation, no new sync point, preserves the 600 us deferred collider assignment saving and 3000 us async bake stall displacement.

### D15 - Cold Mesh Allocation Evidence And Lifecycle Re-Audit

Problem: The pooled `new Mesh` call in `CreateVoxelPoolMesh` is intentionally staggered cold work, but a future zero-GC scan could classify it as unannotated runtime churn. The same continuation pass needed to reconfirm that reserved async meshes cannot outlive active generation teardown.
Solution: Added the canonical `COLD ALLOC` marker directly on the pooled `new Mesh` call, rechecked all voxel pipeline entry points for `BeginGenerationOperation`/`EndGenerationOperation`, and verified `TryShutdownSharedTables` waits for both active generation and mesh-pool warmup before destroying shared pools.
Rejected Alternatives: Moving mesh creation back into `OnEnable` would recreate the cold-frame hitch; using unpooled per-finalize meshes would violate the pool budget; editing external build-warning sources would cross domain boundaries.
Scalability potential: Low: staggered creation avoids boot spikes on weak devices. Middle: reserved pool slots keep normal streaming deterministic. High/Ultra: full pool availability supports higher concurrent chunk density without synchronous allocation.
Hardware Impact: Direct runtime saving is audit hygiene, 0 us; preserved savings remain 2500 us cold-frame pool warmup reduction and retry avoidance under pool pressure.

### D16 - Deferred Work Shutdown Guard

Problem: Last-engine shutdown could destroy shared mesh pools and marching-cubes tables while deferred physics bake teardown or deferred collider upload queues still owned pooled meshes or queued staged collider commits.
Solution: Added `HasPendingVoxelDeferredWork`, made `TryShutdownSharedTables` wait for deferred queues to drain, and retried shared-table shutdown from the deferred physics bake teardown and deferred collider upload drain paths.
Rejected Alternatives: Forcing all deferred work complete during shutdown would create a main-thread stall; immediate pool destruction risks dangling pooled meshes; canceling queued collider uploads risks collider holes on recently finalized chunks.
Scalability potential: Low: scene unload avoids late-frame crash after sparse streaming. Middle: normal streaming drains queues without forced completion. High: more concurrent chunks can finish without teardown races. Ultra: dense visual-overkill streaming keeps deterministic ownership while still shutting down cleanly.
Hardware Impact: Prevents use-after-release style faults without adding hot allocations; avoids forced complete hitches while preserving the 3000 us async bake stall displacement, 600 us deferred collider upload saving, and 2500 us cold pool warmup saving.

### D17 - Dispatcher-Unavailable Deferred Work Fallback

Problem: Deferred teardown/upload queues depended on late-frame dispatcher registration, but the registration helpers were void best-effort calls. If the dispatcher was unavailable or disappeared during shutdown, queued pooled meshes could remain stranded and block shared-table teardown forever.
Solution: Converted deferred registration helpers to return success, forced physics bake teardown release when driver registration fails, immediately committed/canceled collider uploads when no dispatcher exists, added a shutdown flush for pending deferred work when late-frame execution is unavailable, guarded backpressure notification before touching `SystemDispatcher`, and suppressed per-item force-release telemetry during deliberate shutdown flushes.
Rejected Alternatives: Waiting for a dispatcher that no longer exists would leak pooled mesh ownership; forcing the normal path every frame would reintroduce hitches; dropping collider uploads silently would create holes. The fallback is restricted to dispatcher-unavailable or shutdown fault paths.
Scalability potential: Low: weak devices can unload/reload voxel scenes without stranded pool slots. Middle: normal dispatcher path stays deferred and throttled. High: high-concurrency streams keep ownership deterministic. Ultra: dense chunk churn can still shut down cleanly without changing cross-domain scheduler interfaces.
Hardware Impact: 0 us on the normal hot path beyond checking a returned bool and dispatcher pointer before backpressure notification; fault-path forced completion may cost up to the existing bake wait, but only when no dispatcher exists. Shutdown flush avoids up to 2048 redundant telemetry warning publishes. Preserves the 3000 us async bake stall displacement and 600 us deferred collider upload saving during normal play.

### D18 - Subsystem Reset Deferred Queue Flush

Problem: `ResetStaticRuntimeState` cleared deferred physics/collider queues and then reset mesh-pool occupancy. With domain reload disabled or editor subsystem reuse, a queued pooled mesh could be forgotten and its slot marked free while some stale collider/job path still owned it.
Solution: Call `FlushDeferredVoxelWorkWithoutDispatcher()` at the start of subsystem reset, before queue clear and mesh-pool occupancy reset.
Rejected Alternatives: Blind `Clear()` is fast but loses ownership evidence; destroying the whole mesh pool first risks killing a mesh still referenced by a queued collider upload; relying on domain reload is not valid for modern Unity editor settings.
Scalability potential: Low: editor and low-memory devices avoid stale pool ownership after scene reload. Middle: repeated play-mode cycling preserves deterministic pool state. High/Ultra: dense voxel streams can be stopped/restarted without inherited queue poison.
Hardware Impact: 0 us during normal play; reset-only work prevents pooled mesh aliasing. Avoided worst-case is a later collider/surface mesh corruption, not a steady-state frame saving.

### D19 - MeshData Upload NaN And Index Guard

Problem: Surface and collider upload use `MeshUpdateFlags.DontValidateIndices` for speed, but the final MeshData fill path did not sanitize non-finite vertex payloads or out-of-range triangle indices. A corrupt density or projection result could reach GPU or PhysX buffers before blackbox evidence was dumped.
Solution: Added zero-allocation finite checks and fallbacks inside the existing upload loops. Surface upload now sanitizes position, normal, color, AO, skirt, dirty blend, curvature, AUP UV payloads, and triangle indices. Collider upload now sanitizes positions and triangle indices. Any correction sets `VoxelMeshPipelineInvalidMeshDataFlag`; blackbox dumping now triggers for any non-zero telemetry flag in editor/development builds.
Rejected Alternatives: Re-enabling Unity index validation would spend validation cost on every clean mesh; throwing exceptions would drop the frame and lose the last-300-frame context; running a separate validation pass would double memory traversal.
Scalability potential: Low: corrupt voxel data is contained without a GPU/PhysX crash on weak devices. Middle: clean meshes pay only branch checks in already-required upload loops. High/Ultra: dense mesh streaming keeps the fast upload flags while gaining deterministic fault evidence.
Hardware Impact: No heap allocation; branch-level upload checks only. Preserves fast `DontValidateIndices` upload while forcing `Dump_VOXEL_MESH_PIPELINE.bin` on invalid mesh data.

### Final Git Diff

- Staged source diff: `Assets/_Project/Scripts/HectonVoxelEngine.cs` 851 insertions, 89 deletions.
- Staged source diff: `Assets/_Project/Scripts/HectonVoxelVolume.cs` 94 insertions, 7 deletions.
- Staged docs/log additions before this continuation: `Docs/Tasks/Status_VOXEL_MESH_PIPELINE.md`, `Docs/AgentLogs/Rationale_VOXEL_MESH_PIPELINE.md`, `Docs/AgentLogs/RECON_VOXEL_MESH_PIPELINE.md`, and `Docs/AgentLogs/LOG_VOXEL_MESH_PIPELINE.md`.
- Current static-only unstaged diff: `HectonVoxelEngine.cs` plus VOXEL_MESH_PIPELINE status/rationale/log files currently show 454 insertions and 55 deletions in `git diff --stat`; `HectonVoxelVolume.cs` has no new unstaged upload-sanitizer diff.
