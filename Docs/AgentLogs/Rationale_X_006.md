# Rationale_X_006

Status: PHASE 1 STREAMING SCRATCH + CONTINUOUS GPU BANDWIDTH/UPLOAD BUDGET STATIC PASS / SAVE SNAPSHOT SCRATCH LIFETIME + PHYSX BAKE BACKPRESSURE PATCHED / DEFERRED COLLIDER QUALITY BUDGET PATCHED / RUNTIME COLLIDER NULL MUTATION PATCHED / SONAR CANCEL FORCE-COMPLETE PATCHED / VOXEL JOB CANCEL WAIT PATCHED / PAGING COMPONENT CACHE PATCHED / ACTIVE VOLUME REGISTRY HARD-CAP PATCHED / FIXED VOLUME REGISTRY PATCHED / CARVE QUEUE OVERFLOW COALESCING PATCHED / FIXED CARVE WRITE BUFFER PATCHED / DAMAGE STAMP OVERFLOW COALESCING PATCHED / VOXEL REBUILD GLOBAL LODBIAS FALLBACK REMOVED / COMPILE PROBE TIMED OUT AND CPU-GATED / BUDGETED UNITY MESH UPLOAD RESIDUAL
Scope: Phase 0 archaeology plus Phase 1 proof slice for Dear Lie clipping, stress validation, fixed dirty chunk registry, fixed live-volume registry, active engine volume registry hard-cap, fixed carve ingress overflow coalescing, fixed scheduled carve write buffer, cancellation-safe voxel job wait, GlobalDataVault dirty chunk pool, runtime PhysX publication removal, compaction scratch prewarm, mesh upload budgeting, streaming-scratch mesh rebuild pool migration, Dear Lie damage-volume bandwidth gating, same-frame damage stamp overflow coalescing, continuous mesh-upload catch-up budgeting, continuous deferred collider cleanup budgeting, runtime collider null-mutation cleanup, sonar cancellation force-complete cleanup, paging component cache cleanup, voxel rebuild global lodBias fallback removal, borrowed voxel save snapshot scratch, borrowed scratch lifetime lease, and PhysX bake admission backpressure.

## Decision 001: Phase Scope Lock

Problem: User ordered "Begin Phase 0" while the batch prompt contains 10 tasks across implementation and stress proof.
Solution: Execute tasks 01-03 only: repository scan, chunk pool/DataVault analysis, renderer/shader cohesion. No Phase 1 code until Phase 0 target list is factual.
Rejected Alternatives: Implementing ExecuteVoxelCarveJob or Dear Lie shader immediately was rejected because current source ownership, BufferIDs, and shader routes are not yet mapped.
Scalability potential: Low keeps evidence static and avoids destabilizing runtime. Middle/High/Ultra implementation planning will depend on measured chunk/mesh capacities found in Phase 0.
Hardware Impact: Estimated gain on i3/MX350 is risk avoidance, not runtime gain yet; prevents adding a synchronous path that could cost >1000 us/frame.

## Decision 002: Mandate Selection

Problem: Voxel work spans carving, meshing, streaming, AUP, native memory, DTO layout, and visual fakery.
Solution: Loaded 8 mandates: voxel SDF/MC, voxel carving persistence, world streaming residency, AUP determinism, ARM64 layout, zero-GC, native jobs, cinematic fake-first.
Rejected Alternatives: Loading all registry files was rejected as context noise. Loading only voxel mandates was rejected because AUP, jobs, and zero-GC are direct acceptance gates.
Scalability potential: Mandate set forces continuous quality and time-sliced budgets from weak devices through Ultra rather than fixed binary settings.
Hardware Impact: The selected rules target elimination of main-thread stalls and runtime allocation. Concrete microsecond savings remain pending source audit.

## Decision 003: Evidence First

Problem: The prompt demands proof artifacts and forbids fake optimization reports.
Solution: Create Status_X_006.md first, then generate a JSON target list and append final report to LOG_X_006.md.
Rejected Alternatives: Chat-only report was rejected by reporting protocol. Guessing names from prompt was rejected because source reality may differ.
Scalability potential: Static evidence identifies exact hot-path risks before scaling pool capacity or shader payload size.
Hardware Impact: Avoids speculative code churn. Estimated saved debugging cost: unmeasured; runtime microsecond gain pending implementation.

## Decision 004: Do Not Treat Existing Async As Complete Nonblocking

Problem: HectonVoxelEngine awaits job completion through frame-yielding helpers, but ExecuteVoxelPipelineAsync still performs per-build Persistent allocations and main-thread MeshData upload. VoxelDeltaProcessor schedules CarveSdfJob, then commits writes through main-thread dictionary state.
Solution: Mark the current pipeline as partially async, not fully nonblocking. Phase 1 must separate immediate visual carve response from slower authoritative mesh rebuild and chunk delta commit.
Rejected Alternatives: Calling the current job path "done" was rejected because main-thread allocation/upload/commit evidence exists. Rewriting immediately was rejected because Phase 0 scope is static archaeology.
Scalability potential: Low tier should use GPU clip/scar fake and deferred coarse mesh rebuild. Middle tier can increase commit/upload budget. High tier can run denser rebuild cadence. Ultra can add higher-density SDF/normal/AO polish after fake-first response.
Hardware Impact: Expected i3/MX350 gain after implementation is removal of burst mesh rebuild stalls larger than 1000 us; Phase 0 itself saves 0 us measured.

## Decision 005: DataVault Path Exists But Active Carve State Is Not Fully Native-Owned

Problem: SurfaceNets and pager systems expose DataVault-backed lanes, but live carve persistence still uses managed dictionaries and new Persistent NativeArrays on first dirty chunk touch.
Solution: Record active ownership gap in the target list. Future work must preallocate or DataVault-own chunk delta state by residency budget and keep managed maps out of the hot deformation path.
Rejected Alternatives: Reusing managed dictionaries with EnsureCapacity was rejected because it is not a hard Zero-GC proof under expanding world deformation. Moving everything to GlobalRegistry was rejected because GlobalRegistry is cold DI only.
Scalability potential: Low tier limits resident dirty chunks and uses compact RLE state. Middle tier raises dirty chunk capacity. High tier expands native state tables. Ultra keeps larger history/telemetry and higher-res deformation windows without changing gameplay truth ownership.
Hardware Impact: Expected i3/MX350 gain is avoidance of first-touch allocation spikes during combat/mining; exact microseconds require profiler capture.

## Decision 006: Dear Lie Must Reuse Existing Damage Volume Substrate, Not Invent New Authority

Problem: SargassumCutManager already publishes a damage volume and cut mask, and AbyssalVoxelRock already samples them. The shader currently uses them for scar/fresh-cut response, not geometric clipping. TerrainMaster lacks equivalent carve payload.
Solution: Phase 1 renderer design should add clip/depth/shadow parity on top of the existing GPU payload, while authoritative voxel truth remains in VoxelDeltaProcessor/HectonVoxelEngine.
Rejected Alternatives: Adding a second carve buffer was rejected because one fact needs one owner/route. Moving gameplay truth to shader was rejected because visual fake must not own authority or save identity.
Scalability potential: Low tier can sample a coarse damage volume at reduced cadence. Middle tier adds terrain-boundary clip. High tier adds better edge normals/scar blend. Ultra spends saved cycles on denser volume stamps and richer fresh-cut shading.
Hardware Impact: Expected i3/MX350 gain is immediate visual response without waiting for mesh rebuild; exact saving is the avoided main-thread rebuild wait, not measured in Phase 0.

## Decision 007: Phase 1 Dear Lie Clip Route

Problem: A 60 Hz laser drill cannot wait for Marching Cubes or Surface Nets geometry truth every frame without exposing stalls; the existing renderer only darkened/scarred cuts and did not hide stale geometry in depth/shadow passes.
Solution: Reused the existing damage-volume route and added clip parity to voxel rock forward/shadow/depth, terrain forward/shadow/depth/depth-normals, and voxel bake ghost forward. This keeps the visual cut immediate while mesh rebuild remains delayed.
Rejected Alternatives: A new per-cut GraphicsBuffer was rejected because SargassumCutManager already owns a bounded damage-volume route, and a second hot path would increase bandwidth and ownership ambiguity. CPU-side immediate mesh rebuild was rejected because it competes with the frame.
Scalability potential: Low uses coarse damage texture cadence and minimal clip. Middle keeps terrain/voxel parity. High raises stamp density and cut edge quality. Ultra spends saved frame time on richer fresh-cut normals, scorch, and post-cut material response.
Hardware Impact: Static proof only. Stress math in VOXEL_OPTIMIZATION_REPORT_X_006 shows a single 60 Hz laser consumes 1 of 16 stamp slots per frame for 7200 frames. Measured microseconds saved: 0 because profiler capture was not run; expected gain is removal of visible mesh rebuild wait from the visual path.

## Decision 008: PhysX Registration Fallback Removal

Problem: HectonVoxelEngine contained a fallback that assigned MeshCollider.sharedMesh immediately when deferred collider upload registration failed. That is a synchronous PhysX collider mutation on the deformation path.
Solution: Removed the immediate sharedMesh fallback and fail-closed the registration path by disabling/removing the pending proxy when the deferred uploader is unavailable.
Rejected Alternatives: Keeping the fallback for correctness was rejected because it violates the no-sync-deformation rule. Moving PhysX collider upload into a Burst job was rejected because Unity collider object assignment is main-thread API.
Scalability potential: Low avoids hitching by delaying collision truth rather than blocking the frame. Middle/High/Ultra can raise the late-frame collider budget or use higher-fidelity collider cadence, but the API swap still remains owner-phase work.
Hardware Impact: Estimated i3/MX350 gain is avoiding a collider cook/swap spike on failed registration; exact microseconds require Unity profiler. Deferred late-frame sharedMesh assignment remains and is still a residual hot-path risk if budget is exceeded.

## Decision 009: RLE Packet Proof Instead Of Duplicate Compressor

Problem: The user demanded a byte-level RLE packet proof and no unmanaged allocation inside regeneration jobs. The repo already had an aligned RLE architecture, so adding a parallel compressor would duplicate ownership.
Solution: Verified the existing DTO layout in the scanner: NativeSnapshotHeader 16 B, NativeSnapshotChunkHeaderDeltaRle 40 B, SaveVoxelDeltaRun8 8 B, VoxelDeltaHeaderDTO 32 B. Added VoxelCarvingTortureJob as a deterministic synthetic load generator without allocation or pointer calls.
Rejected Alternatives: Replacing the compressor wholesale was rejected because the existing 8-byte run packet and pager queue already provide a bounded route. Managed compression queues were rejected because they cannot prove Zero-GC under a two-minute drill stream.
Scalability potential: Low caps dirty chunks and write slots. Middle raises queue capacity. High increases resident RLE buffers. Ultra can keep longer forensic history and larger deferred write bursts without changing save identity.
Hardware Impact: Stress report bounds worst-case 32^3 chunk RLE at 40 + 32768 * 8 bytes. Measured microseconds saved: 0; proof is static. Remaining blocker: active carve commit still uses managed chunk dictionaries.

## Decision 010: Honest Validator Gate

Problem: A final claim of "monolithic and fast" would be false while managed chunk tracking, runtime NativeArray allocations, and main-thread mesh upload sites remain visible.
Solution: Added Tools/OOP_Voxel_Scanner.py and generated VOXEL_OPTIMIZATION_REPORT_X_006.json with pass/fail gates. The verdict intentionally fails on remaining hot paths instead of masking them.
Rejected Alternatives: Treating shader clip and RLE proof as full completion was rejected. Running dotnet build was rejected because CPU load was 100%, and the project rule forbids launching dotnet build above 50% CPU load.
Scalability potential: Low/Middle/High/Ultra can only be trusted after the failed gates are fixed: native resident chunk state, preallocated mesh/sdf buffers, and budgeted upload ownership.
Hardware Impact: The scanner itself saves 0 runtime microseconds. It prevents shipping a false Zero-GC claim; current residual risk is first-touch allocation and mesh upload spikes on i3/MX350.

## Decision 011: Stress Proof Must Separate Bounded Memory From Lossless Persistence

Problem: The user demanded proof that a 60 Hz laser for 120 seconds cannot overflow the visual GraphicsBuffer or create an unbounded RLE write backlog.
Solution: Expanded Tools/OOP_Voxel_Scanner.py to calculate exact pressure. Visual damage stamps are bounded at 16 same-frame entries; DamageVolumeStampCommand is 32 B, so the GraphicsBuffer upload ceiling is 512 B per dispatch. H8BinaryWorldPager is bounded at 32 write slots * 262080 B = 8386560 B of write payload arena. The queue rejects overflow instead of growing.
Rejected Alternatives: Claiming full success was rejected because bounded memory is not the same as lossless persistence. A pathological 32^3 one-cell-run RLE chunk is 262184 B in the native snapshot layout, exceeding the pager sector payload by 104 B.
Scalability potential: Low keeps stamp count and damage volume resolution low. Middle raises damage-volume cadence. High can tolerate the default 125829120 B/s ping-pong traffic. Ultra can use the 128x96x128 path at 1509949440 B/s only if GPU bandwidth telemetry says it is safe.
Hardware Impact: Measured microseconds saved: 0. Static memory proof only. On i3/MX350 the bounded visual route prevents unbounded CPU memory growth, but the max damage-volume path is too expensive without quality gating.

## Decision 012: DataVault Surface Pool Is Not The Dirty-Chunk Recycler

Problem: The current GlobalDataVault proof could be misread as a complete voxel chunk recycler. It is not. VoxelSurfaceNetsVault owns 3335708 B of preallocated meshing scratch/state, but VoxelDeltaProcessor active dirty chunks still allocate NativeArrays through managed dictionaries.
Solution: Report both ledgers explicitly. SurfaceNets Vault is accepted as a preallocated scratch pool. VoxelDeltaProcessor remains failed: 135168 B native per dirty chunk, initial dictionary capacity 256, no hard cap because GetOrCreateChunkState calls EnsureCapacity(_chunkStates.Count + 1) before allocating ChunkDeltaState arrays.
Rejected Alternatives: Treating InitialChunkRegistryCapacity as a maximum was rejected because Dictionary.EnsureCapacity proves growth, not a cap. Disabling colliders to hide this was rejected because the prompt forbids removing physical collision as a performance shortcut.
Scalability potential: Low must cap resident dirty chunks and spill/coalesce before pager write. Middle/High can raise fixed native pool counts. Ultra can keep larger dirty-history and forensic buffers, but all tiers need the same fixed lease/return route.
Hardware Impact: Current risk on i3/MX350 is first-touch allocation spikes and unmanaged memory growth during fast scooter traversal or sustained drilling. Exact spike cost requires Unity profiler; static proof says the recycler is not yet production-complete.

## Decision 013: Dense Snapshot Fallback For RLE Pathological Chunks

Problem: Native sparse RLE worst case for a 32^3 chunk is 40 B header + 32768 * 8 B runs = 262184 B, exceeding the H8 pager sector payload 262080 B by 104 B.
Solution: Changed VoxelDeltaProcessor native snapshot writing and measurement to choose dense delta snapshot when sparse RLE payload is larger than dense. Dense payload is dirty mask 4096 B + SDF 65536 B + material 32768 B + flags 32768 B = 135168 B, plus 40 B aligned header = 135208 B.
Rejected Alternatives: Increasing WorldPager sector size was rejected because it changes save layout and can break other payload budgets. Splitting a single chunk into multiple sectors was rejected for this slice because it changes chunk identity routing and needs a route-card-level save schema update. Dropping pathological runs was rejected because it is terrain corruption.
Scalability potential: Low benefits because dense fallback avoids pathological RLE expansion and disk queue rejection. Middle/High keep sparse RLE for normal drilling where it is smaller. Ultra can spend extra IO budget on denser forensic snapshots, but the writer still chooses the smaller bounded payload.
Hardware Impact: Measured microseconds saved: 0. Static sector proof improved: effective native snapshot worst case is 135208 B, leaving 126872 B headroom inside the 262080 B sector payload.

## Decision 014: Fixed Dirty Chunk State Lease Pool

Problem: VoxelDeltaProcessor still created ChunkDeltaState native arrays on first dirty chunk touch. Each dirty chunk required 135168 B native memory: dirty mask 4096 B, SDF bits 65536 B, material ids 32768 B, flags 32768 B.
Solution: Added a fixed 256-slot dirty chunk state pool prewarmed in OnEnable. Carve/load paths now lease from the pool and fail closed when capacity is exhausted. Compaction release returns dirty state to the pool. Scanner now records fixed_dirty_chunk_pool_present=true and fixed_dirty_chunk_pool_native_bytes=34603008.
Rejected Alternatives: Leaving first-touch allocation was rejected because a 60 Hz drill or scooter traversal can hit new chunks under frame pressure. Moving this slice directly into GlobalDataVault was rejected because the existing VoxelDeltaProcessor still owns save/load dictionaries and needs a wider route-card migration to remove managed lookup without breaking persistence identity.
Scalability potential: Low can keep the 256-slot pool and shed/merge excess dirty chunks before persistence. Middle/High can raise DirtyChunkStatePoolCapacity from quality/residency policy after profiler proof. Ultra can keep larger dirty history, but the DTO layout and save identity remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of first-touch ChunkDeltaState NativeArray allocation spikes from the carve/load path. Remaining risk is explicit: managed dictionaries, compaction transient NativeArrays, main-thread mesh upload, and late-frame MeshCollider.sharedMesh assignments are still visible in the validator.

## Decision 015: Remove Runtime PhysX Mesh Publication

Problem: Unity MeshCollider.sharedMesh assignment is a main-thread PhysX publication point. The deferred queue reduced timing pressure, but it was still a main-thread collider mesh swap after deformation.
Solution: Removed non-null MeshCollider.sharedMesh publication from HectonVoxelEngine deferred collider upload drain/flush and HectonVoxelVolume deferred chunk commit. The queue now drains without publishing a new runtime PhysX mesh. Existing live colliders remain if already present; if no live collider exists, the collider is disabled and the staged bake mesh is cleared for reuse.
Rejected Alternatives: Claiming this was fully async was rejected because Unity does not expose off-thread MeshCollider.sharedMesh publication. Forcing the swap in late-frame was rejected because the prompt explicitly required removing synchronous PhysX update during deformation. Destroying collider objects was rejected because it would add managed/object churn and destabilize pooled ownership.
Scalability potential: Low avoids collider cook/swap hitches entirely and relies on Dear Lie plus existing collision state. Middle/High can later add an owner-phase collider publication budget behind explicit gameplay acceptance tests. Ultra may restore high-fidelity collider refresh only if profiler proof shows the main-thread swap is under budget and not in the deformation path.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is elimination of MeshCollider.sharedMesh swap spikes from runtime deformation. Physics fidelity is intentionally stale until a separate non-deformation collider publication route is designed and tested.

## Decision 016: Prewarm Compaction Scratch

Problem: TrySchedulePendingCompaction allocated source/copy/output NativeArrays for every compaction schedule. This was not UnsafeUtility.Malloc inside the job, but it was still runtime Persistent churn under deformation pressure.
Solution: Added a single prewarmed compaction scratch set owned by VoxelDeltaProcessor. Capacity is 2412930 B: source SDF 2146689 B for a 129^3 sonar payload, plus fixed dirty/output scratch 266241 B. TrySchedulePendingCompaction now leases these buffers and has 0 NativeArray allocation sites. Only uniform compaction is persisted; non-uniform compaction releases scratch and keeps the dirty chunk state.
Rejected Alternatives: Keeping per-compaction allocations was rejected because sustained 60 Hz drilling can repeatedly cross the dirty threshold. Persisting non-uniform compaction output from scratch was rejected because reusable scratch cannot be stored in CompactedChunkState. Allocating a new compacted array set was rejected because it preserves the allocation spike being removed.
Scalability potential: Low uses the fixed scratch and avoids compaction churn. Middle/High can raise source capacity if grid limits change. Ultra can add a separate compacted-state native pool later, but this slice keeps persistence identity unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of eight Persistent NativeArray allocations from each compaction schedule. Remaining hot allocation risk is now dominated by mesh generation/upload allocations in HectonVoxelEngine and by managed chunk lookup.

## Decision 017: Budget Main-Thread Mesh Uploads

Problem: Mesh.ApplyAndDisposeWritableMeshData and collider mesh upload remain Unity main-thread APIs. Removing them completely is not possible inside the current engine surface without replacing Unity mesh publication, but allowing multiple uploads in one frame can still create visible stalls under sustained carving.
Solution: Added a global voxel mesh upload budget in HectonVoxelEngine. Direct surface/collider upload paths now await AwaitVoxelMeshUploadBudgetAsync, allowing one upload per frame and delaying additional uploads behind Dear Lie shader clipping.
Rejected Alternatives: Claiming the upload is async was rejected because Unity mesh publication is still a main-thread call. Removing geometry upload entirely was rejected because authoritative mesh truth must eventually catch up. Per-chunk unbounded uploads were rejected because a 60 Hz drill can dirty adjacent chunks faster than weak hardware can publish meshes.
Scalability potential: Low keeps one upload per frame. Middle/High can raise the budget only with profiler proof. Ultra can spend extra frame time on more frequent collider/surface refresh, while gameplay truth and save identity stay unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is smoothing upload spikes by distributing mesh publication across frames. Residual risk remains: the upload itself is still main-thread work, so scanner correctly keeps mesh_upload_main_thread_absent=false.

## Decision 018: Do Not Patch Power Domain Compile Blocker

Problem: A compile check against Assembly-CSharp.csproj failed before proving X_006 files because Hecton8.Core currently has 13 CS0103 errors for missing MathLodApproximation in Power-domain files.
Solution: Record the blocker and stop compile attempts while CPU/dotnet are hot. Do not edit Assets/_Project/Scripts/Power from the voxel agent without an explicit cross-domain route card.
Rejected Alternatives: Patching the Power files was rejected because X_006 domain is voxel SDF/paging and the AGENTS boundary treats unrelated edits as architectural sabotage. Re-running builds while CPU is 100% and dotnet processes remain active was rejected by the project build-launch rule.
Scalability potential: Low/Middle/High/Ultra unaffected by this decision. It preserves domain ownership and prevents a voxel patch from becoming a hidden power-system repair.
Hardware Impact: Measured microseconds saved: 0. Compile proof remains blocked by external dependency state, not by a measured X_006 runtime path.

## Decision 019: Vault-Back Dirty Chunk State

Problem: The dirty chunk state pool was fixed-size, but it still used local Persistent NativeArrays per slot and managed dictionaries for chunk lookup. That did not satisfy the GlobalDataVault recycler requirement and left a managed growth route in the validator.
Solution: Replaced ChunkAddress dictionaries with FixedChunkRegistry slots and moved dirty chunk storage into four GlobalDataVault generation handles: DirtyMaskPool, SdfBitsPool, MaterialPool, and CellFlagsPool. The pool remains 256 slots, 135168 B per slot, 34603008 B total, but the underlying storage now belongs to Vault lanes and each slot is a slice into those lanes.
Rejected Alternatives: Leaving the fixed local pool was rejected because the prompt specifically demanded GlobalDataVault ownership. Adding more managed dictionary preallocation was rejected because capacity growth would remain possible. Rewriting world paging identity in the same patch was rejected because save identity and chunk residency ownership need a separate route card.
Scalability potential: Low keeps 256 dirty slots and fails closed on exhaustion. Middle/High can raise the lane lengths from quality/residency policy after profiling. Ultra can add a larger compacted-state Vault pool, but save packet layout and chunk identity remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of first-touch dirty-state NativeArray allocation and managed dictionary growth from deformation pressure. Remaining hot risks are mesh generation NativeArray allocations and Unity main-thread MeshData upload.

## Decision 020: Streaming Scratch Mesh Rebuild Pool

Problem: HectonVoxelEngine still allocated exact-size NativeArrays during rebuild: MC raw/weld/index/edge buffers, welded counter, surface attribute buffers, projection buffer, spatial bucket buffers, collider chunk split buffers, smooth pillar collider buffers, and modified-cell map.
Solution: Moved those transient rebuild buffers into the existing streaming scratch lease. The lease now owns reusable MC extraction buffers, attribute buffers, projection buffer, spatial node/tunnel bucket buffers, collider split/remap buffers, smooth pillar collider buffers, and a reusable NativeParallelHashMap for modified SDF cells. Runtime code clears/reuses buffers in place and fails closed when scratch cannot be leased.
Rejected Alternatives: Leaving exact-size Persistent allocations was rejected because sustained laser carving can trigger repeated mesh rebuilds. Allocating worst-case arrays at process start was rejected because 128^3 MC worst-case scratch would explode memory on low devices. Moving Unity Mesh upload off-thread was rejected because Mesh.ApplyAndDisposeWritableMeshData is a Unity main-thread API.
Scalability potential: Low keeps a small streaming scratch slot count and delays uploads behind Dear Lie clipping. Middle/High can increase slot count and upload budget after profiler proof. Ultra can keep larger scratch capacities and richer surface attributes without changing save identity or voxel truth ownership.
Hardware Impact: Measured microseconds saved: 0. Static scanner residual native allocation evidence dropped from 32 to 10 and direct hot_rebuild allocation evidence dropped to 1. Expected i3/MX350 impact is removal of Persistent/TempJob allocation spikes from deformation mesh rebuilds; remaining hard stall is Unity mesh publication.

## Decision 021: DataVault Fail-Closed Dirty State

Problem: The dirty chunk pool was Vault-backed when GlobalDataVault resolved correctly, but the fallback ChunkDeltaState constructor still contained four Persistent NativeArray allocations. Static proof could not call the carve path Zero-GC while that fallback existed.
Solution: Removed the local NativeArray fallback. If GlobalDataVault dirty lanes are unavailable, EnsureChunkStatePool creates no leaseable dirty slots and TryLeaseChunkState fails closed through the existing capacity warning path.
Rejected Alternatives: Keeping fallback allocations for resilience was rejected because it violates the user's GlobalDataVault recycler requirement. Silently allocating local dirty state was rejected because it would hide bootstrap misconfiguration under drilling pressure.
Scalability potential: Low/Middle/High/Ultra all use the same ownership route. Tiers may change dirty slot count later, but not the authority path or DTO layout.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is avoiding emergency native allocations if DataVault bootstrap is broken; the visible result is failed carve commit rather than frame hitch and memory growth.

## Decision 022: Scratch-Owned Cave Graph Snapshots

Problem: RebuildVolumeAsync still allocated NativeArrays for cave nodes, tunnels, entrances, structures, and crater stamps before every deformation rebuild. Initial GenerateVolumeAsync also allocated CaveGraphGenerator output arrays before the pipeline scratch lease.
Solution: Pre-acquire the existing voxel streaming scratch lease before cave graph copy/fill, then lease scratch-owned NativeArray subranges for graph snapshots and crater stamp replay. ExecuteVoxelPipelineAsync now reuses a valid pre-acquired lease instead of taking a second slot. The scratch lease owns disposal; pipeline-local subarrays are never individually disposed.
Rejected Alternatives: Keeping per-rebuild Persistent arrays was rejected because sustained drilling can repeatedly rebuild the same volume. Creating a separate graph pool was rejected because the existing streaming scratch lease already owns the rebuild lifetime and slot backpressure. Passing managed arrays into Burst jobs was rejected because the density job requires NativeArray inputs.
Scalability potential: Low keeps one or few scratch slots and waits rather than allocating. Middle/High can raise streamingScratchSlotCount after profiler proof. Ultra can keep larger graph snapshots in scratch while preserving the same save and authority route.
Hardware Impact: Measured microseconds saved: 0. Static scanner residual hot native allocations dropped to 0 after this plus spawn scratch migration. Expected i3/MX350 impact is removal of graph snapshot allocation spikes from deformation rebuild and initial async generation.

## Decision 023: Scratch-Owned Spawn Point NativeList

Problem: VoxelSpawnPointJob allocated a fresh NativeList<CaveSpawnData> after vertex welding whenever spawn extraction was enabled. The list is generation-only, but it remained a validator hot-rebuild allocation token and could grow under streaming pressure.
Solution: Added SpawnPointListScratch to VoxelStreamingScratchSlot and leased it through TryPrepareSpawnPointScratch. The job still uses NativeList.ParallelWriter/AddNoResize, but capacity is owned by the reusable scratch slot and cleared between uses.
Rejected Alternatives: Removing spawn extraction was rejected because it is gameplay content. Replacing the job with a managed List was rejected by Zero-GC. Switching to NativeStream was rejected because the fixed NativeList route already provides deterministic bounded capacity with less schema churn.
Scalability potential: Low uses the existing max(welded/20,64) capacity and waits on scratch slot pressure. Middle/High can grow capacity through slot reuse. Ultra can keep richer spawn candidate density without new per-generation allocation sites.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of NativeList allocation churn when cave volumes are generated under traversal.

## Decision 024: Validator Treats Unity Mesh Publication As Residual, Not Hidden Success

Problem: Mesh.ApplyAndDisposeWritableMeshData is a Unity main-thread API and cannot be moved into Burst or a worker thread in this code surface. Failing the whole static validator on that fact hid the more important proof: all direct upload calls are now budget-gated and residual hot native allocations are zero.
Solution: OOP_Voxel_Scanner now reports PASS_STATIC_WITH_BUDGETED_UNITY_MESH_UPLOAD_RESIDUAL when all hard gates pass and mesh upload is budgeted. It keeps mesh_upload_main_thread_absent=false and records the residual risk explicitly.
Rejected Alternatives: Claiming mesh publication is off-thread was rejected as false. Removing mesh publication was rejected because authoritative geometry must catch up after Dear Lie clipping. Keeping a hard fail after every direct upload was budgeted was rejected because it prevents the validator from distinguishing impossible Unity API residue from unbounded hot-path allocation.
Scalability potential: Low keeps VoxelMeshUploadBudgetPerFrame=1. Middle/High/Ultra may raise the budget only after Unity profiler proof; the validator still exposes the main-thread API residue.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact comes from smoothing publication cadence, not from eliminating the Unity API call.

## Decision 025: Damage-Volume Bandwidth Gate

Problem: The Dear Lie shader clip made stale geometry visually disappear, but SargassumCutManager could still run the 3D damage-volume ping-pong dispatch under recovery even when no carve energy remained. Default pressure is 2097152 B per dispatch and 125829120 B/s at 60 Hz; max authored pressure is 25165824 B per dispatch and 1509949440 B/s.
Solution: Damage-volume runtime resolution now derives from finite continuous HomeostasisBrain.GlobalQualityWeight with hysteresis: minimum survival 32x16x32, authored default 64x32x64, authored max 128x96x128. QueueDamageVolumeVisualSync now requires queued stamps or tracked _damageVolumeEnergy > 0.0001, so idle recovery does not dispatch forever.
Rejected Alternatives: Leaving QualitySettings.GetQualityLevel was rejected because the project rejects binary quality switches. Always running recovery was rejected because it pays GPU bandwidth after the visual fake has no active damage energy. Dropping the damage volume entirely was rejected because the shader clip needs immediate visual coverage while mesh truth catches up.
Scalability potential: Low uses the 32x16x32 route and skips idle dispatch. Middle resolves toward authored defaults. High/Ultra can spend bandwidth on 128x96x128 only when GlobalQualityWeight and profiler proof justify it; gameplay truth, save identity, and RLE packets remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Static bandwidth floor improves from default 125829120 B/s to 15728640 B/s when quality collapses to minimum survival, and idle cost drops to zero dispatches after damage energy decays.

## Decision 026: Continuous Mesh Upload Catch-Up Budget

Problem: The prior mesh upload budget was a fixed 1 upload per frame. It was conservative, but it did not consume GlobalQualityWeight and forced high-end machines to catch up at toaster cadence even when Dear Lie had hidden the visual gap.
Solution: Replaced the fixed integer gate with a bounded fractional token bucket driven by finite HomeostasisBrain.GlobalQualityWeight. Low remains 1 upload/frame. Visual-overkill cap is 3 uploads/frame. Middle weights accumulate fractional tokens and never exceed the per-frame ceiling.
Rejected Alternatives: Unbounded catch-up was rejected because Unity Mesh.ApplyAndDisposeWritableMeshData remains a main-thread API. A binary low/high budget was rejected by the continuous scalability rule. Keeping exactly one upload everywhere was rejected because it wastes high-end headroom and prolongs stale mesh truth unnecessarily.
Scalability potential: Low protects frame time with 1 upload/frame. Middle gets fractional acceleration. High/Ultra can spend extra frame budget on faster mesh truth catch-up while shader clipping continues to hide latency. Gameplay truth, DTO layout, save identity, and RLE persistence remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact remains stall smoothing at 1 upload/frame. High-tier impact is lower visual-stale duration after sustained drilling because the mesh publication queue can drain up to 3 uploads/frame when quality permits.

## Decision 027: Compile Wall Remains Outside Voxel Domain

Problem: After X_006 patches, CPU and process gates opened, so a compile check was required. The build still fails before proving X_006 files because Hecton8.Core has 57 errors in non-voxel domains: SomaticKinematicsRuntime, GameBootstrapper, InputBindingServiceContracts, and ConstructionManager.
Solution: Record the compile wall and do not patch those owners from the voxel agent. Continue relying on static X_006 scanner proof until the external dependency wall is cleared.
Rejected Alternatives: Editing Gameplay, Bootstrap, Core input contracts, or Construction from X_006 was rejected because the domain boundary treats unrelated edits as architectural sabotage. Ignoring the compile attempt was rejected because CPU/process gates were legal and runtime C# changed.
Scalability potential: Low/Middle/High/Ultra unaffected by this decision. It preserves one-owner discipline and avoids burying voxel changes under cross-domain repairs.
Hardware Impact: Measured microseconds saved: 0. Compile proof for X_006 remains unproven because upstream Hecton8.Core errors stop the build first; no voxel file appeared in the reported compiler errors.

## Decision 028: Correct X_006 Black-Box Dump Identity

Problem: VoxelDeltaProcessor wrote crash forensic dumps to Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin. That violates the active X_006 reporting protocol and makes ownership ambiguous during multi-agent crashes.
Solution: Changed the dump path to Docs/AgentLogs/Dump_X_006.bin and updated the static scanner gate to require that exact path.
Rejected Alternatives: Keeping the stale SHINOBU_308 suffix was rejected because crash artifacts must have one owner and one route. Adding a second dump path was rejected because it duplicates forensic output and complicates crash triage.
Scalability potential: Low/Middle/High/Ultra unaffected at runtime; this is forensic ownership. The fixed 300-entry ring and binary dump layout remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Debugging impact is deterministic ownership of the crash dump; no runtime hot-path cost changes.

## Decision 029: Mesh Upload Burst Bias

Problem: The continuous mesh-upload token bucket allowed fractional quality to accumulate, but a raw ceiling on frameBudget could let a near-low quality weight stockpile a second same-frame Unity mesh upload after idle. That weakens the low-tier frame-time guard.
Solution: Added VoxelMeshUploadBurstCapBias=0.5 before Mathf.Ceil(frameBudget - bias), then clamped the cap from 1 to 3 uploads/frame. Minimum and near-low tiers stay at one upload/frame; higher quality must earn a clear budget before same-frame catch-up expands.
Rejected Alternatives: Flooring all middle budgets was rejected because it would discard continuous scaling. Keeping raw ceiling was rejected because low-tier devices can accumulate extra publication bursts without profiler proof. Unbounded catch-up remains rejected because Mesh.ApplyAndDisposeWritableMeshData is still a main-thread Unity API.
Scalability potential: Low keeps one upload/frame even after idle. Middle ramps only when GlobalQualityWeight is clearly above low. High/Ultra can reach two or three uploads/frame when the scalar justifies it, while Dear Lie hides delayed geometry truth.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is tighter protection against idle-then-drill upload bursts; high-tier visual stale duration remains reduced by the 1..3 continuous budget.

## Decision 030: Off-Thread Compaction Source Copy

Problem: TrySchedulePendingCompaction still copied the published sonar SDF into compaction scratch with a main-thread per-byte loop. A default 64x32x64 payload is 131072 B and the scratch capacity supports up to 2146689 B, so sustained drilling could still pay a visible synchronous copy before the actual compaction jobs run.
Solution: Added VoxelDeltaCopyEncodedSdfJob as a Burst IJobParallelFor. The copy job writes the prewarmed source scratch first, then VoxelDeltaCompactionJob is scheduled with copyHandle as its dependency. ScheduledCompactionRequest records SourceSonarVersion and discards completed output if the volume publishes a newer sonar SDF before commit.
Rejected Alternatives: Keeping the loop was rejected because it leaves synchronous memory bandwidth in the deformation/persistence path. NativeArray.Copy was rejected for this slice because it is still an immediate main-thread copy. Allocating a second snapshot to avoid copy was rejected because it would violate the fixed scratch ownership route.
Scalability potential: Low keeps compaction deferred and copy work spread through the Job System. Middle/High/Ultra can process larger sonar payloads without moving copy bandwidth back to the main thread. Save identity and RLE packet layout are unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of up to 2146689 B of synchronous copy pressure from compaction scheduling under sustained carving.

## Decision 031: Compile Wall Still External After X_006 Copy Guard

Problem: After the compaction copy job and version guard, runtime C# changed again and required a compile probe when CPU/process rules allowed it.
Solution: Ran dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal after CPU averaged 20% and no dotnet/csc/VBCSCompiler processes were active. Build still fails inside Hecton8.Core before X_006 files are compiled.
Rejected Alternatives: Editing UI/Core/Input/UserOptions/Discovery owners from X_006 was rejected because those are outside the voxel domain. Ignoring the build wall was rejected because the probe produced actionable ownership evidence.
Scalability potential: Low/Middle/High/Ultra unaffected by this decision. It preserves domain boundaries and keeps voxel validation separated from unrelated core/input dependency breakage.
Hardware Impact: Measured microseconds saved: 0. Build blocker count is now 44 Hecton8.Core errors dominated by missing INativeInputManagerRuntime, missing UserOptionsPersistence, and missing Span<> in HectonDiscoveryManager; no X_006 voxel file appeared in the compiler error list.

## Decision 032: Async Published Sonar Snapshot Encode

Problem: HectonVoxelVolume.PublishSonarSdfSnapshot encoded every sonar SDF sample on the main thread and then copied the active SDF to DataVault through a per-byte loop. At the supported 129^3 payload ceiling, the encode/copy side can touch 2146689 samples plus the audio material byte array before world generation hands off the volume.
Solution: Replaced the encode loop with PublishedSonarSdfEncodeJob, writing into scene-owned staging arrays. The active published SDF/audio buffers swap only after the job completes. The DataVault SDF copy now uses PublishedSonarSdfCopyJob while only the SDF payload write-lock is held; descriptor write-lock is acquired after SDF release and never spans await. HectonVoxelEngine awaits PublishSonarSdfSnapshotAsync before releasing the pipeline scratch that owns the input density field.
Rejected Alternatives: Writing the encode job directly into active published buffers was rejected because readers could observe half-updated sonar data. Completing the jobs immediately was rejected because it would preserve the same-frame stall. Leaving NativeArray.Copy as "good enough" was rejected because it is still an immediate memory-bus spike.
Scalability potential: Low yields frames while the encode job runs and keeps sonar publication behind generation work. Middle/High/Ultra can publish larger sonar payloads without moving per-sample encode back to the main thread. The payload descriptor, save identity, and AUP origin route are unchanged.
Hardware Impact: Measured microseconds saved: 0. Static expected gain on i3/MX350 is removal of up to 2146689 sample encode iterations and 2146689 SDF-byte copy writes from the main thread for max-size sonar snapshots. Residual risk is one SDF payload write-lock held during the copy job; descriptor lock is short and post-copy.

## Decision 033: Off-Thread Dirty-State Compaction Snapshot

Problem: TrySchedulePendingCompaction still executed four main-thread NativeArray.Copy calls before scheduling compaction: dirty mask 4096 B, SDF bits 65536 B, material ids 32768 B, and cell flags 32768 B. Total synchronous dirty-state copy pressure per compaction was 135168 B.
Solution: Added VoxelDeltaCopyChunkStateJob and made VoxelDeltaCompactionJob depend on JobHandle.CombineDependencies(chunkStateCopyHandle, sourceCopyHandle). Dirty-state copy and published-sonar SDF copy now both run through Burst before the compaction job reads scratch.
Rejected Alternatives: Keeping NativeArray.Copy was rejected because it still spends memory-bus bandwidth on the owner thread under sustained drilling. Copying directly from live chunk state inside VoxelDeltaCompactionJob was rejected because new carves can mutate the dirty chunk while compaction is in flight; the scratch snapshot is the isolation boundary.
Scalability potential: Low removes a 135168 B synchronous copy from each compaction attempt. Middle/High/Ultra keep the same snapshot route and can increase compaction cadence without reintroducing main-thread copy spikes. DTO layout and save identity are unchanged.
Hardware Impact: Measured microseconds saved: 0. Static expected gain on i3/MX350 is removal of 135168 B of synchronous dirty-state memory traffic per compaction schedule; combined with Decision 030, compaction scheduling no longer has main-thread SDF/source dirty-array bulk copies.

## Decision 034: Dirty-Pool Pressure Compaction Scheduler

Problem: TrySchedulePendingCompactionFrostTick waited 300 frames between compaction attempts when no compaction was already running. Under sustained drilling, the pending compaction ring and 256-slot dirty chunk pool can hit pressure long before a five-second frost tick.
Solution: Added IsCompactionPressureHigh. Pending compaction count >= 8 or free dirty chunk slots <= 32 bypasses the frost interval and schedules compaction immediately when the scheduler is idle.
Rejected Alternatives: Running compaction every frame unconditionally was rejected because non-uniform chunks may not compact and would burn job bandwidth. Raising pool capacity was rejected as the first response because it hides pressure by spending more memory instead of draining work. Writing dirty chunks straight to the pager was rejected because save sector routing needs stable snapshots and the existing dense/RLE selection.
Scalability potential: Low protects the 256-slot dirty-pool by draining uniform/high-dirty chunks before exhaustion. Middle/High/Ultra can raise thresholds or pool size later, but pressure scheduling stays continuous rather than a binary quality switch.
Hardware Impact: Measured microseconds saved: 0. Static behavior change: worst idle wait under pressure drops from up to 300 frames to the next scheduler tick, reducing dirty-slot residency risk during 60 Hz laser drilling or fast scooter traversal.

## Decision 035: Borrowed Native Save Snapshot Scratch

Problem: SaveManager still allocated a fresh Persistent NativeArray<byte> for the voxel native snapshot before serializing `VoxelDeltaProcessor` state. This is not the 60 Hz carve hot path, but under a dirty voxel world it can allocate up to the bounded native snapshot payload during a save and hide memory churn in the persistence route.
Solution: Added a VoxelDeltaProcessor-owned `_nativeSnapshotScratch` prewarmed from dirty-pool and compacted-uniform worst-case math, capped by `SaveBinaryStorage.RawPayloadCapacityBytes`. SaveManager now calls `TryCopyNativeSnapshotToBorrowedScratch`, receives an exact `GetSubArray` slice, and treats that slice as borrowed memory. The processor owns disposal at shutdown; SaveManager does not dispose the borrowed snapshot.
Rejected Alternatives: Keeping the per-save NativeArray allocation was rejected because it violates the zero-allocation persistence goal under stress saves. Aliasing the save raw/compressed buffer was rejected because the voxel snapshot may be needed while the save path builds payloads. Enlarging pager sectors was rejected because that changes save layout instead of fixing ownership. Lazy growth was rejected because the first large save would still hitch.
Scalability potential: Low uses the same fixed scratch and gets bounded save memory without extra churn. Middle/High can raise dirty-pool capacity only through the same prewarm math. Ultra can keep larger dirty history later, but snapshot ownership remains one route and DTO/save identity stay unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of a per-save native allocation spike when voxel deltas are dirty; memory ceiling is fixed by dirty-pool worst case plus compacted uniform chunks and then capped by the raw save payload capacity.

## Decision 036: PhysX Bake Admission Backpressure

Problem: `ForceReleaseDeferredVoxelPhysicsBakeTeardown` can force-complete a pending bake handle if the deferred teardown lane is full or cannot register. Removing the force path blindly would risk releasing or clearing a mesh while `Physics.BakeMesh` still owns it, but letting normal deformation keep scheduling bakes into an unavailable teardown lane preserves a possible sync wait.
Solution: Added `CanScheduleVoxelPhysicsBake` and made `TryScheduleVoxelPhysicsBake` refuse new bake jobs in play mode when the late-frame dispatcher cannot register or `_deferredVoxelPhysicsBakeTeardowns.Count` has reached `DeferredVoxelPhysicsBakeBackpressureThreshold`. Under pressure the system sheds collider refresh before it admits new PhysX bake work; Dear Lie and existing collision state carry the visual/gameplay gap until the lane drains.
Rejected Alternatives: Deleting `ForceReleaseDeferredVoxelPhysicsBakeTeardown` was rejected because shutdown/overflow without ownership tracking can leak meshes or race a live bake job. Increasing the teardown list capacity was rejected because it hides pressure by spending memory. Forcing collider publication on the main thread was already rejected because it violates the deformation path rule.
Scalability potential: Low stops new collider bake work early under pressure. Middle/High can drain the lane faster through existing late-frame budgets. Ultra can sustain more bakes only if profiler proof says the dispatcher and upload budget can carry them, but the admission guard remains continuous pressure control rather than a binary quality switch.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is avoidance of rare forced bake completion stalls during sustained drilling or scooter traversal when collider-bake teardown backlog is already high.

## Decision 037: Save Snapshot Fail-Closed On Borrowed Copy Failure

Problem: After moving voxel native save snapshots into processor-owned borrowed scratch, a scratch/copy failure could be misread as "no voxel delta data" and let SaveManager continue without persistence for dirty voxel state.
Solution: `TryCopyNativeSnapshotToBorrowedScratch` now returns the required byte count on scratch-capacity or copy failure. SaveManager treats a failed copy with positive byte count as a hard save failure via InvalidOperationException. Empty/no-dirty voxel state still returns zero bytes and is skipped.
Rejected Alternatives: Silently dropping voxel delta data was rejected as world corruption. Falling back to a per-save NativeArray allocation was rejected because it reintroduces the allocation spike just removed. Truncating the snapshot was rejected because it breaks RLE/dense snapshot integrity and hash proof.
Scalability potential: Low/Middle/High/Ultra all preserve the same rule: dirty voxel truth is either fully saved through the bounded borrowed scratch route or the save fails. Device tier changes capacity policy later, not correctness.
Hardware Impact: Measured microseconds saved: 0. Expected hardware impact is correctness under stress saves: no hidden memory growth fallback, no silent voxel deformation loss.

## Decision 038: Borrowed Snapshot Lease Lifetime

Problem: The borrowed voxel snapshot slice is handed from SaveManager's main-thread snapshot phase into the background save pipeline. Without an explicit lease, `VoxelDeltaProcessor.OnDisable` could dispose `_nativeSnapshotScratch` while the save pipeline still reads the borrowed subarray during scene teardown or domain shutdown.
Solution: Added `_nativeSnapshotScratchLeaseCount`, deferred-dispose flag, and `ReleaseBorrowedNativeSnapshotScratch`. Successful borrowed snapshot copies increment the lease; SaveManager releases the lease in its finally block after the verified save pipeline exits. `DisposeNativeSnapshotScratchBuffer` now defers disposal while a lease is active and completes disposal when the last borrower releases.
Rejected Alternatives: Returning to an owned per-save NativeArray was rejected because it reintroduces the allocation spike. Letting SaveManager keep a raw borrowed slice without a release contract was rejected because lifetime correctness would depend on scene order. Pinning the processor forever was rejected because it leaks persistent native memory after failed saves.
Scalability potential: Low/Middle/High/Ultra all share the same ownership: borrowed scratch is zero-allocation while active and cannot be disposed under a background save. Higher tiers can alter capacity later without changing the lease protocol.
Hardware Impact: Measured microseconds saved: 0. Expected impact is correctness under teardown/save overlap: no use-after-dispose and no fallback allocation.

## Decision 039: Legacy Voxel Load Fallback

Problem: SaveManager skipped VoxelDeltaProcessor during the normal ISaveable load loop, then called `TryLoadNativeSnapshot` even when no native voxel blob was present. That path returns success for an empty snapshot, so old saves that only contain `voxelDeltaPersistence` DTO data could silently lose voxel deformation state.
Solution: SaveManager now uses the native snapshot route only when `loadedVoxelDeltaSnapshot` is created and non-empty. If no native blob exists, it calls `VoxelDeltaProcessor.LoadFromSaveData(data)` as a legacy DTO fallback. The normal save path still bypasses DTO population and writes the borrowed native snapshot.
Rejected Alternatives: Dropping legacy DTO saves was rejected because it corrupts existing player worlds. Always loading both native and DTO was rejected because it could double-apply deformation. Re-enabling DTO save population was rejected because it reintroduces main-thread dense copy pressure.
Scalability potential: Low/Middle/High/Ultra all prefer native snapshot load. Legacy DTO path is compatibility-only and does not change the current save authority route.
Hardware Impact: Measured microseconds saved: 0. Correctness gain: old DTO-only saves keep voxel deltas instead of silently loading a clean world.

## Decision 040: Borrowed Snapshot Growth Guard

Problem: The snapshot scratch capacity is fixed by current dirty-pool math, but the helper still had an unsafe future edge case: if capacity ever needed to grow while a borrowed slice was active, disposing was deferred yet the field could be overwritten with a new NativeArray, losing the old owner reference.
Solution: `EnsureNativeSnapshotScratchBuffer` now refuses to replace `_nativeSnapshotScratch` while `_nativeSnapshotScratchLeaseCount > 0`. It marks disposal deferred and returns; the caller then hits the existing capacity check, reports rejected copy, and SaveManager fails closed with the required byte count.
Rejected Alternatives: Growing the scratch under an active lease was rejected because it risks use-after-dispose or leaked native memory. Allocating a temporary overflow buffer was rejected because it reintroduces the save allocation spike. Blocking the main thread until release was rejected because it would create a hidden save/deformation stall.
Scalability potential: Low/Middle/High/Ultra keep the same rule: capacity changes occur only when no borrowed slice exists. If a future tier raises dirty-pool capacity, it must prewarm before save borrowing starts.
Hardware Impact: Measured microseconds saved: 0. Correctness gain: no owner-reference loss and no emergency allocation during overlapping saves.

## Decision 041: Dirty-Pool DataVault Hot-Swap Rebind

Problem: `VoxelDeltaProcessor` cached `GlobalRegistry.DataVault` and built the dirty chunk pool in `OnEnable`. If the component enabled before DataVault registration, `_chunkStatePoolCreated` became true with zero free slots and never retried; if DataVault was replaced later, dirty-pool handles could remain tied to the old vault.
Solution: Added `GlobalRegistryServiceSlot.DataVault` handling. The processor now performs a cold rebind: live dirty/compacted state is serialized into the existing borrowed native snapshot scratch, pending compaction metadata is cleared, old dirty-pool handles are released through the old `IDataVault`, the new vault-backed pool is ensured, and the snapshot is loaded onto the new pool. If carve/compaction jobs or DataVault write locks are live, the rebind is deferred and `Tick` stops before draining/scheduling new voxel work until the pending rebind can apply. If restore to the new vault fails, the code rolls back to the old vault using the same snapshot.
Rejected Alternatives: Ignoring late DataVault registration was rejected because it makes the 34,603,008 B preallocated dirty-pool proof false in bootstrap-order variants. Immediate `.Complete()` during hot-swap was rejected because it would hide a sync point in a registry callback. Dropping dirty voxel state on vault replacement was rejected as terrain corruption.
Scalability potential: Low keeps a strict 256-slot pool and fails closed instead of allocating local fallback arrays. Middle/High/Ultra can raise dirty-pool capacity only through the same vault-backed prewarm and cold-rebind route; gameplay truth, DTO layout, and save identity remain unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is correctness and stall avoidance: no main-thread job completion on service replacement, no permanent zero-free dirty pool after late bootstrap, and no local NativeArray fallback growth during fast traversal or sustained drilling.

## Decision 042: Borrowed Snapshot Single-Writer Guard

Problem: The borrowed voxel snapshot scratch was protected from dispose and growth while leased, but not from a second copy into the same buffer. A concurrent save and DataVault rebind, or two save attempts, could receive two borrowed slices that alias the same VoxelDeltaProcessor-owned NativeArray; the later copy could overwrite the earlier background writer.
Solution: `TryCopyNativeSnapshotToBorrowedScratch` now rejects copy attempts while `_nativeSnapshotScratchLeaseCount > 0`, preserving the required byte count so SaveManager fails closed instead of allocating a temporary buffer. DataVault rebind treats an active snapshot lease as a busy condition and defers. If DataVault is unregistered while live dirty/compacted state exists, pending rebind remains frozen until a replacement vault exists; authoritative carve event enqueue rejects while the unresolved rebind is pending.
Rejected Alternatives: Allowing multiple borrowed aliases was rejected because it is silent save corruption. Allocating a second emergency snapshot was rejected because it reintroduces unmanaged persistence spikes. Blocking until the save finishes was rejected because it creates a hidden main-thread stall. Continuing to carve against an unregistered DataVault was rejected because old vault buffers may be invalid or about to be disposed.
Scalability potential: Low/Middle/High/Ultra all keep one scratch writer and one authoritative dirty-state route. Higher tiers can increase scratch capacity only through prewarm; they still cannot create overlapping borrowed writers or local fallback arrays.
Hardware Impact: Measured microseconds saved: 0. Correctness and memory impact: prevents save/rebind scratch overwrite without adding memory, and prevents post-unregister carve work from touching stale vault-backed SDF arrays during sustained drilling or fast traversal.

## Decision 043: Continuous Deferred Collider Cleanup Budget

Problem: The deformation collider publication path no longer writes a new runtime `MeshCollider.sharedMesh`, but the cleanup lane still drained a fixed 2 entries per frame and several deformation fake branches still wrote `sharedMesh = null`. That is a binary budget and a remaining PhysX mutation hint on the deformation frame.
Solution: Replaced the fixed deferred collider upload drain with a continuous `HomeostasisBrain.GlobalQualityWeight` token bucket: 1 drain/frame at minimum survival, up to 4 drains/frame at visual overkill, with low-tier burst bias. Added `HectonVoxelVolume.DisableColliderChunksForCinematicFake` and routed no-collider, smooth pillar fallback, empty chunk, and scratch-failure deformation branches through collider disable/proxy cleanup without mutating `MeshCollider.sharedMesh`. `DisableDeferredVoxelBakePresentation` now also disables collider presentation without clearing `sharedMesh`.
Rejected Alternatives: Keeping the fixed 2/frame drain was rejected because it violates the continuous quality mandate. Clearing `sharedMesh = null` in the deformation path was rejected because even a null assignment is still a PhysX object mutation on the main thread. Re-enabling runtime collider publication was rejected because Unity exposes that as a main-thread API, not a Burst/job path.
Scalability potential: Low drains 1 staged/proxy cleanup entry per frame and relies on Dear Lie plus stale collision truth. Middle increases cleanup cadence through fractional token accumulation. High/Ultra spend extra frame budget on faster collider cleanup without changing gameplay truth ownership or save identity.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is reduced main-thread PhysX mutation risk during 60 Hz drilling and bounded cleanup latency without a binary quality switch.

## Decision 044: Remove Voxel Rebuild Global lodBias Fallback

Problem: `RecordVoxelRebuildBudget` fell back to mutating `QualitySettings.lodBias` when `LODSystemManager` was missing. That is a global project quality mutation from voxel domain code and a binary decrementing response to a local rebuild spike.
Solution: Removed the direct `QualitySettings.lodBias` fallback. The accepted route is `LODSystemManager.ApplyEmergencyLODBiasStrike()` when the owner exists, plus `CrashTelemetryBuffer.ReportCriticalPerformanceSpike` in all cases.
Rejected Alternatives: Keeping the fallback was rejected because it violates continuous quality ownership and can degrade unrelated render systems from a voxel-local overbudget event. Writing a new voxel-owned LOD manager was rejected because LODSystemManager already owns that authority. Silently ignoring overbudget was rejected because the black-box/performance spike route must remain visible.
Scalability potential: Low reports voxel rebuild pressure without mutating global project quality from the wrong owner. Middle/High/Ultra keep visual-overkill policy under the existing LOD authority instead of a voxel-local binary setting.
Hardware Impact: Measured microseconds saved: 0. Correctness impact: avoids unpredictable global LOD quality drops on i3/MX350 during voxel rebuild spikes while preserving telemetry for the real owner to react.

## Decision 045: Bounded Damage Stamp Overflow Coalescing

Problem: The Dear Lie visual route had fixed 16-entry cut-mask and damage-volume GraphicsBuffers. Capacity was bounded, but overflow stamps were silently dropped, so multi-tool or burst damage in one frame could lose the newest visual cut while still claiming the buffer was safe.
Solution: Added overflow coalescing for both stamp queues. When the fixed queue is full, the newest visual stamp overwrites/coalesces into the final command slot using max radius and max strength. The queue size never grows, no managed allocation is added, and `_damageVolumeEnergy` still receives the strongest overflow stamp.
Rejected Alternatives: Growing the GraphicsBuffer was rejected because MX350 bandwidth and memory ceilings require a hard fixed buffer. Dropping overflow silently was rejected because it makes stress behavior visually dishonest. Adding a managed pending list was rejected because it violates Zero-GC hot path rules.
Scalability potential: Low keeps 16 commands/frame and merges bursts into a believable larger mark. Middle/High/Ultra can later increase fixed capacity through the same preallocated route if profiler proof says the GPU upload budget can carry it.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is stable 512 B damage-stamp upload ceiling with less visual loss under same-frame burst drilling.

## Decision 046: Runtime Collider Null-Mutation Cleanup

Problem: After removing non-null runtime collider publication, several runtime cleanup paths still cleared `MeshCollider.sharedMesh = null`: paging despawn/reuse, deferred physics bake teardown, and staged collider bake detach. Even null assignment is still a main-thread PhysX object mutation and can appear during scooter chunk paging or deferred deformation cleanup.
Solution: Runtime paths now disable colliders and bake proxies without mutating `sharedMesh`. `ResetColliderChunks(false)` no longer clears sharedMesh; only cold destroy cleanup (`destroyMeshes == true`) clears the reference. The scanner now rejects runtime collider null mutations while allowing cold destroy reference release.
Rejected Alternatives: Keeping null clears for convenience was rejected because scooter paging and deferred bake cleanup can run under frame pressure. Removing all cold destroy clears was rejected because it risks retaining references after object teardown. Re-enabling collider publication was rejected because Unity `MeshCollider.sharedMesh` is still a main-thread API.
Scalability potential: Low avoids PhysX object churn during fast chunk paging and sustained drilling. Middle/High/Ultra can still clean stale references during cold destruction while higher quality tiers spend budget on mesh upload/cleanup cadence, not synchronous collider mutation.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is fewer main-thread PhysX mutation spikes during chunk eviction/reuse and deferred bake cleanup.

## Decision 047: Sonar Publish Cancel Without Forced Complete

Problem: Published sonar SDF encode/copy jobs used cancellable frame waits. If cancellation fired while a job was live, the `finally` path could call `JobHandle.Complete()` on the main thread, converting a cancellation into a hidden sync wait.
Solution: The encode and vault-copy wait loops now ignore cancellation for the frame wait itself, record `CancellationToken.IsCancellationRequested`, and return false only after the scheduled job completes naturally over frames. This keeps NativeArray/DataVault ownership safe without forcing a same-frame complete on cancellation.
Rejected Alternatives: Disposing or releasing buffers immediately on cancellation was rejected because jobs may still own them. Keeping cancellable awaits was rejected because it preserves the sync-complete trap. Dropping the DataVault write lock early was rejected because it would expose a partially written SDF payload.
Scalability potential: Low avoids cancellation spikes during scene churn or rapid volume eviction. Middle/High/Ultra keep the same job ownership rule; higher tiers only change SDF resolution/cadence, not cancellation semantics.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is removal of cancellation-induced main-thread wait spikes in sonar SDF publish/copy.

## Decision 048: Paging Component Cache Cleanup

Problem: Fast chunk paging still called `GetComponent<T>()` in DespawnVolume, ClearAllVolumes, RegisterActiveVolume, and PrepareVolumeForBuild. It is not a managed allocation proof by itself, but it is avoidable scene-object lookup work on scooter traversal and volume reuse.
Solution: HectonVoxelVolume now caches root MeshFilter, MeshRenderer, and MeshCollider in its runtime component cache. HectonVoxelEngine uses the active-volume component registry and cached root components on paging cleanup, with `TryGetComponent` only as a cold fallback.
Rejected Alternatives: Adding a new global component registry was rejected because GlobalRegistry is cold DI only and the volume already owns its root component facts. Blindly removing fallback lookup was rejected because legacy/partially initialized pooled objects still need a safe cold path.
Scalability potential: Low reduces per-volume paging lookup work during scooter traversal. Middle/High/Ultra keep the same cache route; higher quality tiers spend saved CPU on upload/cleanup cadence, not repeated scene component discovery.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is reduced scene-object lookup cost when evicting or reusing many voxel volumes in one streaming sweep.

## Decision 049: Fixed Live-Volume Registry

Problem: VoxelDeltaProcessor used managed `List<HectonVoxelVolume>` for live volume registration and pending rebuild dispatch with initial capacity 16. Fast scooter traversal can register more volumes and force managed List growth in the streaming/rebuild control path.
Solution: Replaced both lists with a fixed 64-slot `FixedVolumeRegistry` backed by arrays. Registration deduplicates, removal uses swap-back, and pending rebuild overflow falls back to direct `RequestDeltaRebuild` instead of growing managed storage.
Rejected Alternatives: Raising List capacity only was rejected because it still permits hidden growth. Using GlobalRegistry as a hot volume registry was rejected because GlobalRegistry is cold DI. Dropping pending rebuilds silently was rejected because loaded voxel deltas need a visible rebuild request.
Scalability potential: Low keeps 64 live/pending volume slots without managed growth. Middle/High/Ultra can raise the fixed capacity through one constant after profiler proof, but the route remains fixed-array and fail-closed.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is removal of managed List expansion risk during chunk streaming and bulk delta rebuild dispatch.

## Decision 050: Carve Ingress Overflow Coalescing

Problem: The carve ingress memory was bounded, but overflow behavior was not good enough. `TryQueueCarveEvent` dropped the oldest event when the 64-slot NativeQueue was full, and a full pending carve ring could bounce events without reducing authority pressure. That proved bounded memory, not honest overloaded drilling behavior.
Solution: Added fixed-slot overflow coalescing. When the queued carve lane is full, the dequeued oldest compatible event and newest event merge into one capsule/radius-expanded `VoxelCarveEvent`; incompatible overload still reports the black-box overflow flag and keeps the newest command. When the 32-slot pending ring is full, compatible pending requests merge into an existing slot with accumulated damage, max radius/blend, ORed source flags, and capsule segment end. The scanner now records 64 * 128 B = 8192 B event payload and 32 pending slots.
Rejected Alternatives: Growing the NativeQueue was rejected because it breaks the bounded stress proof. Dropping oldest events silently was rejected because sustained 60 Hz drilling can turn into missing terrain truth. Blocking until the carve job drains was rejected because it creates a main-thread stall. Adding a managed backlog was rejected because it violates Zero-GC hot-path rules.
Scalability potential: Low keeps a fixed 64/32 queue shape and compresses overload into coarse capsule cuts. Middle/High/Ultra can raise constants only after profiler proof, but the overflow policy remains fixed-slot coalescing instead of memory growth.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is bounded CPU memory under 7200-event laser drilling with less authority loss than blind oldest-drop; queue payload stays 8192 B and the black-box still records overflow pressure.

## Decision 051: Fixed Scheduled Carve Write Buffer

Problem: The carve job output buffer lived in GlobalDataVault, but `TryResolveScheduledCarveWriteBuffer` still requested `EnsureGenerationHandle<CarveCellWrite>(..., requiredCount, ...)` on the schedule path. A large crater or box carve could grow the buffer exactly when the player is already causing deformation pressure.
Solution: Added `ScheduledCarveWriteCapacity = ChunkCellCount * 4`, cold-prewarmed the buffer on enable and DataVault rebind/rollback, and removed `requiredCount` growth from the schedule/commit resolve path. Over-capacity carve requests now fail closed with black-box overflow telemetry. Static ledger: 131072 CarveCellWrite packets * 32 B = 4194304 B.
Rejected Alternatives: Resizing by `candidateCount` was rejected because it is runtime memory growth on the carve hot path. Allocating a temporary overflow write array was rejected because it hides unmanaged allocation under stress. Blocking or splitting the carve synchronously was rejected because it risks frame stalls and route ambiguity. Dropping the GlobalDataVault route was rejected because the vault already owns cross-domain native memory.
Scalability potential: Low keeps the 4 MB fixed output buffer and rejects pathological oversized cuts. Middle/High/Ultra can raise the constant after profiler proof, but still only through cold prewarm and fixed capacity.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is removal of DataVault buffer growth during large deformation scheduling; worst-case carve write memory is now a visible 4194304 B instead of candidate-driven runtime expansion.

## Decision 052: Cancellation-Safe Voxel Job Wait

Problem: `AwaitForJobCompletionAsync` recorded cancellation while waiting for a voxel job, but still awaited `AwaitableDebtMonitor.NextFrameAsync(ct)`. A cancellation could throw before the JobHandle reached completion/finalization, leaving callers to handle a live job under teardown pressure.
Solution: The job wait loop now uses a non-cancellable frame yield while the handle is live, records cancellation, finalizes the completed handle, then propagates cancellation. This matches the sonar publish fix and keeps job ownership deterministic.
Rejected Alternatives: Keeping cancellable frame waits was rejected because it can turn cancellation into a live-job escape path. Force-completing on cancellation was rejected because it creates a hidden main-thread sync point. Ignoring cancellation entirely was rejected because callers still need teardown semantics after job ownership is safe.
Scalability potential: Low avoids cancellation hitches and use-after-buffer risks during streaming churn. Middle/High/Ultra keep the same ordering; quality only changes job size/cadence, not cancellation ownership.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is fewer cancellation-induced stalls and fewer live-job teardown hazards during chunk streaming or scene changes.

## Decision 053: Active Volume Registry Hard Cap

Problem: `HectonVoxelEngine` used three active-volume `List<>` registries with capacity 64 but no hard cap in `RegisterActiveVolume`. Fast scooter traversal could register the 65th volume and force managed List growth in the streaming control path.
Solution: `RegisterActiveVolume` now deduplicates first, evicts an existing active volume when the 64-slot cap is full, and fails closed if no slot is freed. The eviction selector prefers invalid/null entries and otherwise chooses the farthest active volume in AUP XZ space from the incoming volume.
Rejected Alternatives: Raising List capacity was rejected because it still permits hidden growth. Replacing the lists wholesale in this slice was rejected because several editor/runtime APIs iterate those lists; the safer patch is a hard cap at the existing storage boundary. Dropping the new volume without eviction was rejected because scooter traversal should keep the newest nearby terrain when possible.
Scalability potential: Low keeps 64 active volume records and evicts far terrain under pressure. Middle/High/Ultra can raise `ActiveVolumeRegistryCapacity` after streaming profiler proof, but registration still cannot grow dynamically.
Hardware Impact: Measured microseconds saved: 0. Expected low-end impact is removal of managed List expansion risk during fast chunk traversal; active registry memory is bounded by the existing 64-slot lists.

## Decision 054: Runtime Mesh Pool Lazy Allocation Removal

Problem: Voxel surface and PhysX bake mesh pools were documented as fixed 256-slot preallocated pools, but the async acquire fallback still created a new `Mesh` when it found a cold free slot. Under early streaming or pool warmup lag, terrain generation could allocate Unity mesh objects on the runtime path.
Solution: `AcquireVoxelSurfaceMeshAsync` and `AcquireVoxelPhysicsBakeMeshAsync` now only retry against slots created by the cold prewarm route. They wait up to 4 frames while warmup is running, then fail closed instead of calling `CreateVoxelPoolMesh` from acquire. The scanner records both pool sizes and rejects `CreateVoxelPoolMesh` inside the acquire block.
Rejected Alternatives: Keeping lazy creation was rejected because it makes the preallocation proof false. Blocking until all 512 meshes are warmed was rejected because it can delay startup and hide a large cold stall. Creating emergency unpooled meshes was rejected because it reintroduces managed/Unity object churn exactly when streaming is under pressure.
Scalability potential: Low gets deterministic fixed mesh-object ownership and can shed a surface/collider refresh if prewarm is not ready. Middle/High/Ultra can raise fixed pool constants after profiler proof, but runtime acquire remains allocation-free and fail-closed.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of early streaming `new Mesh` spikes from surface upload and PhysX bake mesh acquisition; memory is bounded by 256 surface Mesh slots and 256 PhysX bake Mesh slots.

## Decision 055: Published Sonar High-Water Buffer Reuse

Problem: `HectonVoxelVolume.EnsurePublishedSonarCapacity` required exact buffer length. A volume whose sonar grid changed from 129^3 to a smaller product, or oscillated across nearby products, disposed and reallocated four Persistent byte arrays even though existing capacity was already sufficient.
Solution: Converted published sonar local SDF/audio buffers to grow-only high-water reuse with an explicit 129^3 sample cap. `TryGetPublishedSonarSdfPayload` now accepts capacity >= current grid product, and `VoxelDeltaProcessor` leases/copies compaction source SDF by `gridDimensions.x * gridDimensions.y * gridDimensions.z` instead of the backing buffer capacity.
Rejected Alternatives: Preallocating 129^3 * 4 bytes for every volume at creation was rejected because many resident volumes would waste memory. Keeping exact-size churn was rejected because streaming/deformation can vary published sonar dimensions. Returning a sliced managed wrapper was rejected because the route must remain NativeArray/Burst-compatible.
Scalability potential: Low keeps only the high-water capacity actually reached by a volume and avoids shrink/grow churn. Middle/High/Ultra can publish larger sonar grids up to 129^3, but compaction still copies only the current grid product and does not treat capacity as truth.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of repeated Persistent byte-array dispose/reallocate churn during voxel rebuild/sonar publish dimension changes; max supported local payload per buffer is 2146689 bytes.

## Decision 056: Fixed Collider Chunk Registry Arrays

Problem: `EnsureColliderChunkCapacity` resized four managed arrays (`MeshCollider[]`, `BoxCollider[]`, live `Mesh[]`, staged bake `Mesh[]`) up to `MaxColliderChunkCount` during runtime collider splitting. The cap was 8, but the resize was still managed growth on the chunked collider path.
Solution: The volume now allocates fixed 8-slot collider/proxy/live-mesh/bake-mesh registries at construction, and `EnsureColliderChunkCapacity` only fills missing child objects inside those fixed slots. The scanner rejects `new *[clampedCount]` and `_colliderChunkColliders.Length < clampedCount` in the ensure block.
Rejected Alternatives: Leaving the resize was rejected because a scooter/deformation collider split can be the first call path. Precreating all child collider GameObjects for every volume was rejected in this slice because it would increase object count for volumes that never need chunked collision. Growing past 8 was rejected because the domain already defines `MaxColliderChunkCount`.
Scalability potential: Low keeps a fixed 8 registry slots and may skip collider refresh if child object fill cannot keep up. Middle/High/Ultra can change `MaxColliderChunkCount` after physics profiler proof, but the registry remains fixed-capacity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of managed array allocation/copy when chunked collider generation first expands beyond one chunk.

## Decision 057: Mesh Publication Component Cache Route

Problem: Mesh upload and collider setup still called `GetComponent<T>()` inside `BuildWeldedMeshNative` and `ApplyVolumeMeshAsync`. Component lookup is not the dominant stall, but it is avoidable scene traversal in the same window as mesh upload and collider bake scheduling.
Solution: The mesh publication path now takes `VoxelPipelineData.SourceVolume` and reads cached MeshFilter, MeshRenderer, and root MeshCollider from `HectonVoxelVolume`. `TryGetComponent` remains only as cold fallback for legacy objects or null-volume fallback paths. The isolated bake proxy lookup also uses `TryGetComponent`.
Rejected Alternatives: Adding a global hot component registry was rejected because GlobalRegistry is cold DI only. Removing fallback lookup entirely was rejected because old pooled objects or non-volume fallback generation can still exist. Caching through static dictionaries was rejected because it reintroduces managed hot state.
Scalability potential: Low removes repeated scene lookup from mesh publication. Middle/High/Ultra keep the same cached route while spending budget on higher upload/collider cadence, not component discovery.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is small but deterministic: fewer scene-object lookups in the mesh upload/collider setup phase.

## Decision 058: Published SDF Registry Hard Cap And Pure Read

Problem: `s_activePublishedVolumes` was initialized with capacity 32 and grew without enforcing the existing domain ceiling. Worse, `TryRaymarchAnyPublishedSdf` removed null/stale entries while reading, violating the pure read-accessor rule.
Solution: Restored an explicit `MaxRegisteredPublishedVolumes = 256` constant, initialized the list to that capacity, and made registration evict stale or farthest entries before Add. `TryRaymarchAnyPublishedSdf` now skips stale entries without mutation; owner-phase unregister/register paths use swap-back removal.
Rejected Alternatives: Letting `List<>` grow was rejected because active sonar/SDF readers can be hit during scooter traversal. Cleaning stale entries from read methods was rejected because read accessors must not mutate global state. Allocating a new fixed registry type was rejected for this slice because the existing list has external iteration assumptions; the hard cap and swap-back removal close the growth/purity defect without wider churn.
Scalability potential: Low keeps 256 published SDF candidates and evicts far/stale volumes. Middle/High/Ultra can raise the constant after sonar profiler proof, but read methods remain pure and registration remains bounded.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is bounded published-volume registry memory and no hidden list mutation cost during SDF raymarch reads.

## Decision 059: Published SDF Density Reads Must Be Pure

Problem: `TrySampleRuntimeSdfDensity` was still a public read accessor, reached by `GetSDFDensity`, but it removed null/stale entries from `s_activePublishedVolumes` while reading. The earlier scanner only checked the raymarch read path, so this mutation escaped the previous proof.
Solution: Routed `TrySampleRuntimeSdfDensity` through `TryReadRuntimeSdfDensity`, which skips stale volumes without mutating the registry. Strengthened `Tools/OOP_Voxel_Scanner.py` so `published_sdf_read_accessors_pure` checks raymarch, sample, and density read blocks together.
Rejected Alternatives: Keeping opportunistic cleanup in reads was rejected by the Global Systems Doctrine: read accessors must not mutate global state. Adding a second cleanup pass in the read method was rejected for the same reason. Removing stale entries only in register/unregister remains the owner-phase route.
Scalability potential: Low avoids hidden list mutation during sonar/SDF sampling under scooter traversal. Middle/High/Ultra can raise the published-volume cap later, but read methods stay pure regardless of tier.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of hidden swap-back list mutation during density sampling; the scanner now catches this class of accessor impurity.

## Decision 060: Collider Chunk Hot Path Requires Prewarmed Hierarchy

Problem: The collider chunk registries were fixed arrays, but `ApplyChunkedColliderMeshesAsync` and smooth pillar collider setup could still reach `EnsureColliderChunkCapacity`, which creates child `GameObject`/`MeshCollider`/`BoxCollider` objects when a pooled volume first needs chunked collision. That is object churn in the collider split window.
Solution: `PrepareForReuse` now calls `PrewarmColliderChunkHierarchy`, filling the fixed 8-slot collider/proxy hierarchy and parking it disabled. Hot collider paths call `TryUsePrewarmedColliderChunkCapacity`; if the hierarchy is missing, they disable collider chunks and fall back to the cinematic fake instead of creating Unity objects during split/bake.
Rejected Alternatives: Creating child colliders lazily in the split path was rejected because it hides Unity object allocation under terrain deformation pressure. Precreating beyond `MaxColliderChunkCount` was rejected because the domain cap is 8. Blocking the frame until hierarchy creation finishes was rejected because collider truth is already allowed to lag behind Dear Lie visual deformation.
Scalability potential: Low keeps collision refresh optional and uses stale/fake presentation when hierarchy proof fails. Middle/High/Ultra can raise `MaxColliderChunkCount` only after physics profiler proof, but the hot path remains prewarm-or-fake.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of `new GameObject`/`AddComponent` spikes from chunked collider generation; fixed hierarchy cost is paid during pool preparation, not collider splitting.

## Decision 061: PhysX Bake Wait Without Cancellable Live-Job Await

Problem: `AwaitForPhysicsBakeCompletionOrDeferAsync` still awaited `AwaitableDebtMonitor.NextFrameAsync(ct)` while a PhysX bake `JobHandle` was live. The catch branch deferred teardown, but the cancellable await still made cancellation control flow exception-driven inside a live-job wait.
Solution: Removed the cancellation token from the frame await. The loop now checks `ct.IsCancellationRequested` explicitly before yielding and enqueues deferred teardown without relying on an `OperationCanceledException` path.
Rejected Alternatives: Force-completing the bake on cancellation was rejected because it would introduce a hidden main-thread sync point. Ignoring cancellation until completion was rejected because volume teardown needs to detach ownership promptly. Keeping the catch path was rejected because the generic voxel job wait had already established the safer non-cancellable live-handle pattern.
Scalability potential: Low avoids exception-driven cancellation during collider bake pressure. Middle/High/Ultra keep the same ownership rule; quality can change collider cadence but not live-job wait semantics.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is fewer cancellation-path spikes and less risk of live bake handles escaping through exception flow during streaming churn.

## Decision 062: Malformed Volume Components Fail Closed

Problem: `BuildWeldedMeshNative` and `ApplyVolumeMeshAsync` used cached components for normal volumes, but if a `HectonVoxelVolume` was malformed or missing a MeshFilter/MeshRenderer/MeshCollider, the hot publication path could still call `AddComponent`. That is Unity object/component churn in the mesh/collider publication window.
Solution: For real `HectonVoxelVolume` instances, missing MeshFilter or MeshRenderer now fails the mesh publication instead of adding components. Missing root MeshCollider falls back to disabled/cinematic chunk collider fake and keeps the visual mesh path alive. `AddComponent` remains only in non-volume fallback creation.
Rejected Alternatives: Auto-repairing malformed runtime volumes in the hot path was rejected because object creation is not a valid deformation/streaming response. Destroying the volume was rejected because it would be a broader ownership decision outside this slice. Keeping AddComponent as a safety net was rejected because the scanner must prove the volume path is allocation-free.
Scalability potential: Low avoids object churn on malformed pooled volumes and uses visual fake/stale collider. Middle/High/Ultra keep the same fail-closed policy; quality only changes how much mesh/collider work is admitted, not whether hot publication repairs object graphs.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of rare but expensive `AddComponent` spikes from voxel mesh/collider publication.

## Decision 063: Register PhysX Teardown Driver Before Bake Admission

Problem: `TryScheduleVoxelPhysicsBake` checked whether late-frame work could be registered, but it did not actually register the deferred teardown driver before admitting the bake job. If cancellation/watchdog later deferred a live bake, `EnqueueDeferredVoxelPhysicsBakeTeardown` still had to register after the job was already in flight.
Solution: `CanScheduleVoxelPhysicsBake` now calls `EnsureDeferredVoxelPhysicsBakeTeardownRegistered()` before admitting a bake. This keeps normal admission coupled to a known teardown lane; if the dispatcher cannot accept the lane, the bake is rejected before scheduling.
Rejected Alternatives: Leaving post-schedule registration as the common path was rejected because it preserves an avoidable failure window. Force-completing on registration failure was rejected as a hidden sync point. Scheduling without teardown ownership was rejected because bake meshes must not outlive their owner route.
Scalability potential: Low sheds collider bake work before scheduling if the teardown lane is unavailable. Middle/High/Ultra can admit more collider work through quality budgets, but every admitted bake still has a teardown route first.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is lower risk of late-frame teardown saturation turning into a synchronous or leaked bake cleanup path.

## Decision 064: Published Sonar Vault Payload Must Not Resize During Publish

Problem: Local published sonar SDF buffers were already high-water, but `TryPublishSonarSdfVaultPayloadAsync` still called `EnsureGenerationHandle<byte>(BufferID.VoxelSdfTexture3D, totalPointCount, ...)` on the publish path. A larger sonar grid could resize GlobalDataVault while voxel rebuild/publish pressure was active.
Solution: Added `PublishedSonarVaultPayloadCapacity = PublishedSonarMaxPointCount` and a fixed-capacity prewarm route. `HectonVoxelEngine.OnEnable` and `HectonVoxelVolume.OnEnable` prewarm `VoxelSdfPayloadDescriptor` and `VoxelSdfTexture3D` to 2146689 bytes. Runtime publish now only resolves existing handles and fails closed if the SDF lane is not already at max capacity.
Rejected Alternatives: Keeping current-size `EnsureGenerationHandle` in publish was rejected because it hides DataVault growth under sonar/voxel streaming pressure. Allocating per-volume vault SDF buffers was rejected because the descriptor route is a single shared cross-domain SDF fact. Copying only into a local buffer and skipping vault publication was rejected because fauna, physics, construction, and audio consumers already depend on the shared `VoxelSdfTexture3D` lane.
Scalability potential: Low pays one fixed vault SDF lane of 2146689 B and publishes smaller grids by descriptor `ByteCount`. Middle/High/Ultra can increase local sonar quality up to 129^3 without changing vault capacity during publish; raising the ceiling requires a cold constant/schema decision, not runtime growth.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of GlobalDataVault block reallocation/defrag risk from published sonar SDF updates when grid dimensions change.

## Decision 065: Published Sonar Descriptor Clear Must Prove Ownership

Problem: `ClearPublishedSonarSdf` and failed descriptor-origin resolution could clear `VoxelSdfPayloadDescriptor[0]` unconditionally. A volume leaving or failing its AUP rebase after another volume published a newer descriptor could erase the shared SDF fact for fauna, physics, radar, or audio consumers.
Solution: Descriptor clear now captures this volume's published version and runtime origin before local reset, rebases that origin through the existing AUP resolver, and clears the shared descriptor only if `Flags` is valid, `SdfVersion` equals the expected version, and `VolumeOrigin` matches the expected rebased origin within a small float tolerance.
Rejected Alternatives: Leaving unconditional clear was rejected because it violates one fact -> one owner -> one proof artifact. Clearing by version only was rejected because two volumes can share a monotonically similar version under scene churn. Adding a managed descriptor-owner registry was rejected because the descriptor already carries the version/origin proof and the hot path must not grow managed state.
Scalability potential: Low avoids cross-volume sonar/SDF invalidation during streaming churn. Middle/High/Ultra can publish higher-quality sonar grids, but descriptor ownership remains version+origin based and independent of quality.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability, not frame time: fewer false SDF descriptor invalidations during fast paging or volume teardown.

## Decision 066: Surface Nets GPU Upload Finalize Must Complete Safety Handles

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.TryFinalizeUpload` checked `JobHandle.IsCompleted` and then unlocked `GraphicsBuffer` write ranges without calling `JobHandle.Complete`. In Unity Jobs, `IsCompleted` proves the work is done but `Complete` is still needed to release safety handles before accessing job-written native views.
Solution: Added `uploadDependency.Complete()` after the `IsCompleted` guard and before every `UnlockBufferAfterWrite` call. The scanner proves this order and also proves there is no wait loop in finalize.
Rejected Alternatives: Calling `Complete` without the `IsCompleted` guard was rejected because it can become a hidden main-thread sync point. Leaving finalize as-is was rejected because it risks safety-handle leaks or invalid buffer unlock ordering. Removing the GPU upload dispatcher was rejected because it is the correct nonblocking lock-buffer route.
Scalability potential: Low uses the same double-buffered GPU upload route with strict finalize ordering. Middle/High/Ultra can raise mesh upload cadence through existing quality budgets, but finalize must still never wait for unfinished jobs.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is fewer safety-handle stalls/leaks during Surface Nets upload finalization; no new blocking wait is introduced.

## Decision 067: Surface Nets GPU Upload Must Fail Closed On Capacity

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.TryBeginUpload` used `math.min` to clamp `VertexCount` and `IndexCount` to buffer capacity. That can upload a partial mesh while indices still reference vertices that were not copied.
Solution: Upload begin now requires `IndirectArgs`, source vertices/indices, and GPU buffer capacity to cover the exact state counts. Oversized states are marked `Fault | CapacityClamped` and no buffer lock occurs.
Rejected Alternatives: Silent truncation was rejected because it corrupts mesh truth and can produce invalid index ranges. Blocking to resize GraphicsBuffers was rejected because upload capacity must be cold-owned, not grown under deformation or streaming pressure. Dropping indices only was rejected because it hides geometry corruption instead of surfacing the capacity fault.
Scalability potential: Low sheds the over-capacity chunk and keeps the Dear Lie visual mask until a later rebuild fits the fixed capacity. Middle/High/Ultra can raise fixed GPU capacities after profiler and VRAM proof, but upload never mutates capacity at runtime.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no partial GPU mesh upload or invalid index fetch caused by capacity overflow.

## Decision 068: Dear Lie Texture Resize Must Not Hit Active Drilling

Problem: `SargassumCutManager.SlowTick` could release and recreate cut-mask or damage-volume render textures after a `GlobalQualityWeight` change while stamps or damage recovery were still active. That is a render-resource churn point inside the visual carve route.
Solution: `RefreshQualityDependentResourcesIfNeeded` now exits while cut stamps, damage stamps, pending damage sync, mask energy, damage energy, or texture clears are active. Quality-driven resize is admitted only when the Dear Lie texture pipeline is idle.
Rejected Alternatives: Resizing immediately was rejected because it can hitch the same route that hides delayed mesh rebuild. Preallocating all tiers at max resolution was rejected because low devices would pay VRAM for inactive quality levels. Ignoring quality changes permanently was rejected because low/middle/high/ultra scaling must still converge once active cutting stops.
Scalability potential: Low avoids mid-cut RT churn and can settle to 32x16x32 damage volume after work drains. Middle/High/Ultra keep higher dimensions only while useful visual energy is active, then resize on the next idle slow tick.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of `RenderTexture.Release/Create` spikes during continuous laser or scooter cutting.

## Decision 069: Dear Lie Overflow Coalescing Must Preserve Coverage

Problem: The overflow stamp coalescer kept the buffer bounded, but it overwrote the final command center with the newest stamp and only kept max radius. The previous stamp could disappear on the 17th same-frame cut.
Solution: Overflow now keeps the existing final command center and expands its radius by distance to the new stamp plus the new stamp radius for both cut-mask UV stamps and 3D damage-volume stamps.
Rejected Alternatives: Growing the GraphicsBuffer was rejected because capacity must stay fixed. Dropping the newest stamp was rejected because continuous drilling would lose visible cuts. Overwriting the old center was rejected because it loses prior coverage and makes the proof dishonest.
Scalability potential: Low gets conservative overcut rather than missing cuts under stamp saturation. Middle/High/Ultra can raise fixed stamp capacity later, but overflow remains bounded and coverage-preserving.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is visual stability under >16 same-frame stamps without buffer growth or allocation.

## Decision 070: Surface Nets GPU Release Must Drain Completed Uploads Without Waiting

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.TryRelease` returned false for any in-flight upload. If teardown happened after the upload job had already completed, locked GraphicsBuffer ranges could remain locked until an owner called finalize.
Solution: The dispatcher now stores the scheduled upload JobHandle. `TryRelease` checks `IsCompleted`; if true, it calls `Complete`, unlocks pending buffers through the same helper used by finalize, clears state, and then releases buffers. If not completed, it still returns false without blocking.
Rejected Alternatives: Blocking release on unfinished upload was rejected because teardown must not hide a sync wait. Releasing GraphicsBuffers without unlocking was rejected because it violates Unity buffer ownership. Ignoring completed in-flight uploads was rejected because it leaks locked ranges and leaves stale upload state.
Scalability potential: Low avoids teardown stalls and leaks. Middle/High/Ultra keep the same nonblocking ownership rule while upload cadence scales through existing budgets.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is fewer locked-buffer leaks and no hidden upload-completion wait during terrain streaming teardown.

## Decision 071: Surface Nets GPU Reinit Must Respect In-Flight Uploads

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.Initialize` called `Release()` and ignored failure. Reinitializing with different capacity during an unfinished upload could overwrite buffer fields and lose references to locked GraphicsBuffer ranges.
Solution: `Initialize` now calls `TryRelease()` and returns false if release cannot complete nonblocking. New buffers are created only after old buffers are actually released.
Rejected Alternatives: Blocking reinit until upload completion was rejected because it hides a main-thread wait. Proceeding with new buffers was rejected because it leaks GPU memory and invalidates upload ownership. Force-unlocking unfinished buffers was rejected because the copy job may still be writing.
Scalability potential: Low sheds reinit under upload pressure. Middle/High/Ultra can retry later through owner cadence, but reinit never violates lock ownership.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is prevention of GPU buffer leaks and invalid upload states during capacity or quality changes.

## Decision 072: Surface Nets Uploading State Must Be Written After Ownership Is Real

Problem: `TryBeginUpload` marked a chunk `Uploading` before `LockBufferForWrite` and job scheduling. If a buffer lock failed, the chunk state could remain stuck in `Uploading` without an active upload owner.
Solution: The stage transition now happens only after vertex/index/args buffers are locked and the copy job has been scheduled. Capacity faults still mark `Fault | CapacityClamped` before any lock.
Rejected Alternatives: Leaving the early stage write was rejected because state must reflect real ownership. Catching lock exceptions and repairing state was rejected as wider exception-flow complexity. Marking `Uploading` before schedule was rejected because the job owner does not exist yet.
Scalability potential: Low avoids stuck upload states under buffer pressure. Middle/High/Ultra keep the same ownership order while capacities and upload cadence scale.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: fewer chunks stranded in `Uploading` after GPU buffer pressure.

## Decision 073: Surface Nets Finalize Must Trust Stored Upload Handle

Problem: `TryFinalizeUpload` completed the caller-supplied handle. A wrong or default completed handle could satisfy the guard while the dispatcher's real pending upload job was still writing into locked buffers.
Solution: `TryFinalizeUpload` now requires both the caller handle and `_pendingUploadDependency` to be completed, then completes `_pendingUploadDependency` before unlocking. The stored handle is the dispatcher's ownership proof.
Rejected Alternatives: Trusting only the caller handle was rejected because API misuse can unlock early. Ignoring the caller handle entirely was rejected because callers may use it as a higher-level dependency token. Blocking on the stored handle was rejected because finalize must stay nonblocking.
Scalability potential: Low avoids early unlock corruption. Middle/High/Ultra keep the same handle ownership while upload cadence scales.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no premature GraphicsBuffer unlock under caller handle mismatch.

## Decision 074: Published Sonar Descriptor Must Be Invalid During SDF Rewrite

Problem: `TryPublishSonarSdfVaultPayloadAsync` copied new bytes into the shared `VoxelSdfTexture3D` buffer before writing the new descriptor. During that copy, the previous valid descriptor could still point consumers at a partially overwritten SDF buffer.
Solution: Shared vault SDF publication is now serialized with `Interlocked`. The descriptor is invalidated before acquiring the SDF write lock, then the final valid descriptor is written only after the SDF copy completes and releases its write lock.
Rejected Alternatives: Leaving the old descriptor valid during copy was rejected because it breaks one fact -> one route. Holding the descriptor write lock across the async copy was rejected because it would span awaits and block descriptor ownership for frames. Allocating per-volume shared SDF buffers was rejected because the project already defines a single shared SDF payload lane.
Scalability potential: Low consumers see no descriptor while the buffer is mutating instead of reading corruption. Middle/High/Ultra keep the same publication order while sonar grid quality scales inside the fixed vault capacity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no fauna/audio/physics consumer reads an old valid descriptor against a half-copied SDF payload.

## Decision 075: Local Published SDF Buffer Needs Reader Lease

Problem: VoxelDeltaProcessor copied the active published sonar SDF into compaction scratch through a scheduled job. The shared vault descriptor was protected, but the local two-buffer staging pair could reuse the old active buffer as the next build buffer before that source-copy job completed.
Solution: Added a local two-buffer read lease to HectonVoxelVolume. Snapshot publish is serialized, readers acquire a lease against the current physical active buffer, and publish refuses to encode into the build buffer if that physical buffer still has a read lease. VoxelDeltaProcessor now acquires the lease before scheduling `VoxelDeltaCopyEncodedSdfJob` and releases it through `ReleaseCompactionScratchBuffers` after the scheduled chain is drained or aborted.
Rejected Alternatives: Copying the 2 MB SDF on the main thread was rejected because it would reintroduce a drill-frame memcpy spike. Adding more local published buffers was rejected because it hides the ownership bug with memory instead of proving the reader fence. Holding the shared DataVault descriptor lock was rejected because the race was local-buffer reuse, not shared descriptor publication.
Scalability potential: Low delays the next local sonar publish under compaction pressure instead of corrupting the source copy; Dear Lie hides the delayed mesh/SDF truth. Middle/High/Ultra can publish richer SDF grids, but the same lease rule prevents reuse of any buffer still read by a compaction copy job.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability under sustained drilling: no background compaction job reads a physical SDF buffer while publish overwrites it.

## Decision 076: External Flora Compile Wall Minimal Fix

Problem: `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal` failed outside X_006 after the voxel patch: `FloraInteractionManager.cs` used `if (!ReleaseCascadePhaseSeedChannel(...))`, but the method returned `void`.
Solution: Changed only `ReleaseCascadePhaseSeedChannel` to return `bool`, matching the existing caller contract. It returns false when the pending cascade phase seed job cannot complete and true after the release path finishes.
Rejected Alternatives: Ignoring the external compile wall was rejected because it blocks verification of the voxel patch. Broad Flora refactoring was rejected because it is outside Echelon 2 ownership. Removing the caller checks was rejected because they intentionally defer visual-sync release while jobs are pending.
Scalability potential: Low avoids a compile-stop in the visual flora lane without changing cadence. Middle/High/Ultra retain the existing queued visual-sync behavior; only the success/fail value is restored.
Hardware Impact: Measured microseconds saved: 0. This is a compile-wall correction, not a runtime optimization.

## Decision 077: Published SDF Clear Must Not Free Reader Buffers

Problem: `ClearPublishedSonarSdf` disposed local published SDF/audio NativeArrays. Most read accessors consume immediately, but the API can hand out `NativeArray.ReadOnly` views for same-frame scalar consumers; disposal during pooled-volume reset or failed publish could invalidate a view outside the local compaction lease protocol.
Solution: Clear now invalidates metadata and the shared vault descriptor only. Local SDF/audio arrays are max-capacity high-water buffers allocated to 129^3 samples and reused across grid-size changes. Physical disposal is isolated in `TryDisposePublishedSonarSdfBuffers`, which refuses to run while local read leases or snapshot publish are active.
Rejected Alternatives: Keeping disposal inside Clear was rejected because it mixes logical invalidation with memory lifetime. Adding leases to every scalar read accessor was rejected because those reads are pure/cold snapshot accessors and would mutate state on reads. Allocating per-read copies was rejected because it would reintroduce managed/native churn.
Scalability potential: Low keeps one bounded high-water local payload and avoids reallocating while drilling/paging. Middle/High/Ultra can publish richer grids up to the fixed 129^3 ceiling without changing local buffer lifetime semantics.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of local SDF NativeArray dispose/reallocate spikes when volumes are reused or sonar grid size changes.

## Decision 078: Surface Nets Upload Must Unlock Partial GPU Locks

Problem: `TryBeginUpload` could acquire one or two GraphicsBuffer write locks and then fail while locking a later buffer or scheduling the copy job. Before this patch, that path had no partial-unlock cleanup because `_uploadInFlight` was not set yet.
Solution: Added explicit lock-acquired flags around vertex, index, and indirect-args `LockBufferForWrite` calls. The catch path unlocks only ranges actually acquired, resets locked NativeArray views, marks the chunk `Fault`, and returns false without blocking.
Rejected Alternatives: Relying on `TryRelease` was rejected because the dispatcher has no in-flight state until all locks and schedule succeed. Blocking or retrying inside catch was rejected because upload begin must stay nonblocking. Growing/recreating buffers on failure was rejected because GPU capacity ownership is cold.
Scalability potential: Low sheds the upload and keeps fixed buffers usable after the failure. Middle/High/Ultra can retry through the owner cadence, but every failed begin now leaves GPU lock ownership clean.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no locked-range leak after transient GraphicsBuffer lock pressure.

## Decision 079: Logical SDF Clear Must Abort Active Publish

Problem: Moving local SDF disposal out of `ClearPublishedSonarSdf` fixed dangling local buffers, but it exposed a publication race: if clear happens while `PublishSonarSdfSnapshotAsync` is awaiting encode or vault copy, the async method could resume and publish a valid descriptor for a volume that was already cleared or reused.
Solution: Added `_publishedSonarPublishAbortRequested`. Publish resets it only after acquiring the publish gate. Clear sets it. Publish checks it after encode before local buffer swap and again after vault SDF copy before final descriptor write.
Rejected Alternatives: Cancelling the await with a token was rejected because cancellation while a live job is running is exactly the hidden-sync pattern this pass removes. Holding a descriptor lock through the whole publish was rejected because it would span awaits. Disposing buffers in Clear was already rejected because it invalidates local readers.
Scalability potential: Low returns no descriptor during teardown/reuse races and relies on Dear Lie/stale visual geometry for continuity. Middle/High/Ultra keep the same abort semantics; quality affects payload size/cadence, not truth ownership after clear.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no destroyed/reused voxel volume republishes a valid shared SDF descriptor after clear.

## Decision 080: Cave Graph Generation Must Not Allocate Temp Native Containers

Problem: `CaveGraphGenerator.TryMeasure` and `TryFill` both generated cave topology through `Allocator.Temp` `NativeList`/`NativeArray` scratch. During paging or generation this is a native allocation route in the voxel terrain pipeline, and it also generated the same cave twice before the caller-owned scratch path.
Solution: Replaced the internal generator with bounded stackalloc `Span` scratch: 64 rooms, 128 tunnels, 8 entrances, and 128 structures. `TryMeasure` now counts from stack scratch; `TryFill` fills caller NativeArrays from stack scratch without creating Temp containers.
Rejected Alternatives: Keeping Temp allocations was rejected because it violates the Zero-GC/native hot-path mandate. Moving scratch to managed arrays was rejected because it would trade native churn for GC pressure. Expanding cave capacity dynamically was rejected because custom presets must fail closed to fixed terrain-pipeline ceilings.
Scalability potential: Low uses the same fixed topology caps without allocations. Middle/High/Ultra can increase visual richness through preset density inside the caps; raising caps requires an explicit memory ledger and scanner gate.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is fewer native Temp allocation spikes during cave paging/generation and one less duplicate native topology generation pass per measured cave.

## Decision 081: Cave Spawn Extraction Must Be Capacity-Bounded

Problem: `VoxelSpawnPointJob` used `NativeList<CaveSpawnData>.ParallelWriter.AddNoResize`. The scratch list was preallocated, but `AddNoResize` in a parallel writer cannot enforce a deterministic per-frame capacity clamp; a pathological hash distribution could overflow the list and fault the worker job.
Solution: Converted spawn extraction to a single owner `IJob` that scans welded vertices after normals are ready and calls `AddNoResize` only while `spawnPoints.Length < spawnPoints.Capacity`. This makes the write lane bounded and removes the parallel writer from cave spawn extraction.
Rejected Alternatives: Growing the NativeList was rejected because cave generation must not allocate under streaming pressure. Keeping the parallel writer was rejected because overflow is still possible. Allocating a second atomic counter buffer was rejected because the existing owner-list length already provides the needed bounded write state.
Scalability potential: Low keeps bounded spawn extraction and may emit fewer spawn points if the scratch capacity saturates. Middle/High/Ultra can raise the fixed scratch capacity with a memory ledger, but the job still fails closed instead of growing buffers.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: no cave generation worker fault from spawn-point list overflow during high-density mesh extraction.

## Decision 082: Modified-Cell Delta Fill Must Be Time-Sliced

Problem: `TryPrepareModifiedCellsForPipeline` built the authoritative `NativeParallelHashMap<int3, VoxelModifiedCell>` from dirty and compacted chunks in one uninterrupted pre-job loop. A two-minute 60 Hz drilling history can turn that loop into a visible main-thread slice before density jobs even start.
Solution: Added `VoxelDeltaProcessor.TryFillDeltaMapForVolumeAsync` and converted the engine caller to `TryPrepareModifiedCellsForPipelineAsync`. The fill keeps the same truth path but yields through `AwaitableDebtMonitor.NextFrameAsync(ct)` on the chunk-generation frame budget every 512 dirty/compacted probes.
Rejected Alternatives: Dropping modified cells was rejected because the rebuilt mesh must eventually match carved truth. Moving the whole source registry into a Burst job was rejected in this pass because the current chunk registries are owner-managed runtime state, not job-readable flat snapshots. Increasing hash-map capacity alone was rejected because it does not remove the uninterrupted loop.
Scalability potential: Low spreads dense delta replay across frames while Dear Lie GPU clipping hides delayed mesh truth. Middle/High/Ultra can consume the same fill faster by changing budget cadence, not by changing DTO layout or save authority.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch reduction: dense dirty-cell replay no longer monopolizes one frame before voxel density jobs are scheduled.

## Decision 083: Stopwatch Import Must Not Shadow Unity Debug

Problem: The first modified-cell time-slicing patch added `using System.Diagnostics` to `VoxelDeltaProcessor.cs`. That made existing `Debug.LogError` calls ambiguous between `UnityEngine.Debug` and `System.Diagnostics.Debug`, breaking compilation.
Solution: Removed the diagnostics using and fully qualified the new stopwatch calls as `global::System.Diagnostics.Stopwatch`.
Rejected Alternatives: Rewriting existing `Debug` calls was rejected because it touches unrelated diagnostics. Adding aliases was rejected because the file already uses fully qualified Stopwatch elsewhere and the narrowest fix is to follow that pattern.
Scalability potential: None; this is a compile correctness fix.
Hardware Impact: Measured microseconds saved: 0. This only restores compile after the time-slicing change.

## Decision 084: External Lore Compile Wall Must Use Stable Exploration Key Route

Problem: After X_006 sources compiled past the Stopwatch/Debug fix, the guarded build failed outside the voxel domain in `ProceduralLoreDirector.cs`. Its `IPlayerExplorationChunkReadModel` calls expected `CopyExploredChunks`, `IsChunkExplored`, and `ChunkWorldSize`, while the neighboring ecosystem domain already uses the stable `CopyExploredChunkKeys` route.
Solution: Minimal compile-wall fix: `ProceduralLoreDirector` now copies packed exploration chunk keys into a cold `long[]`, populates a cold `HashSet<long>` membership cache, unpacks scan seeds with `PDAKeyUtility.UnpackChunkKey`, and uses `ExplorationMapDTO.DenseChunkSizeMeters` for placement scale.
Rejected Alternatives: Expanding the interface was rejected because it risks cross-assembly route churn. Casting to `PlayerExplorationTracker` was rejected because it would bind narrative to a concrete PDA owner. Ignoring the compile wall was rejected because it blocks verification of the X_006 voxel patch.
Scalability potential: Low/Middle/High/Ultra all use the same bounded key-buffer scan; cadence and max active drops remain unchanged.
Hardware Impact: Measured microseconds saved: 0. This is a compile-wall correction outside X_006, not a voxel runtime optimization.

## Decision 085: Dirty-Mask Word Expansion Must Share The Delta Fill Budget

Problem: The first modified-cell fill time-slice yielded every 512 expanded dirty/compacted cell probes, but the dirty-mask word loop could still scan many words before reaching a set bit. On a dense or sparse pathological chunk this is smaller than cell replay, but it is still an uninterrupted pre-job loop inside the terrain rebuild.
Solution: Added the same `YieldIfDeltaMapFillBudgetExpiredAsync` probe to the dirty-mask word loop before expanding each word. The authoritative modified-cell map remains unchanged; only the owner-frame budget cadence changed.
Rejected Alternatives: Ignoring the word scan was rejected because the prompt demands no hidden pre-job spikes. Yielding on every word was rejected because it would add excessive scheduler overhead on normal chunks. Moving the registry scan into a Burst job remains rejected until the chunk registries are flattened into job-owned snapshots.
Scalability potential: Low spreads both sparse and dense dirty replay across frames while Dear Lie clipping covers delayed mesh truth. Middle/High/Ultra consume the same queue faster through budget cadence and token buckets, not by changing save authority.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is tighter worst-frame bound during long 60 Hz drilling histories because dirty-mask scan and dirty-cell replay now share the same frame-debt gate.

## Decision 086: Quest Compile Wall Is Currently Stale/Concurrent Evidence

Problem: After the dirty-mask word probe, the guarded build failed outside X_006 in `QuestStateManager.cs`, reporting five missing helper methods. Immediate source inspection of the current working tree shows all five methods are present inside `QuestStateManager` and not hidden by preprocessor guards.
Solution: Do not patch Quest from X_006 on stale evidence. Record the compile wall and hold the next build until the project build-launch rule allows a clean retry without active dotnet/csc/VBCSCompiler processes.
Rejected Alternatives: Editing QuestStateManager immediately was rejected because the current file does not match the compiler error and this is outside Echelon 2. Relaunching dotnet while seven dotnet processes are active was rejected by project rule. Reverting Quest edits was rejected because they are not X_006-owned changes.
Scalability potential: None for voxel runtime. This preserves concurrent-agent safety and keeps X_006 evidence isolated to voxel/SDF ownership.
Hardware Impact: Measured microseconds saved: 0. This is verification hygiene, not a runtime optimization.

## Decision 087: Streaming Scratch Must Prewarm Modified-Cell And Spawn Containers

Problem: `TryPrepareModifiedCellsScratch` and `TryPrepareSpawnPointScratch` reused streaming scratch containers, but they could still allocate or grow after mesh generation or delta measurement if the slot had not already seen that capacity. That is a post-lease native container growth route in the voxel rebuild path.
Solution: Prewarm `ModifiedCellsScratch` to `totalCellCount` and `SpawnPointListScratch` to `max(64, totalCellCount / 10)` inside `EnsureStreamingScratchSlotCapacity`. Cap measured modified-cell capacity to `data.TotalCells` because the authoritative hash map is scoped to the current rebuild volume; boundary chunk over-measure no longer forces larger scratch growth.
Rejected Alternatives: Leaving growth as "pooled" was rejected because the user asked for no hidden hot allocation. Prewarming to full dirty-chunk registry worst case was rejected because it would reserve memory for cells outside the current rebuild volume. Dropping modified cells on capacity overflow was rejected because it breaks eventual mesh truth.
Scalability potential: Low keeps scratch bounded to the active rebuild cell budget. Middle/High/Ultra can increase gridDim and therefore prewarm larger per-lease scratch, but allocation happens at lease capacity setup rather than inside the post-mesh/delta replay step.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of a late NativeList/NativeParallelHashMap growth spike when a drilled volume first emits spawn points or dense modified-cell replay.

## Decision 088: Rebuild Graph Scratch Must Match Fixed Cave Caps

Problem: `CaveGraphGenerator` now uses fixed stackalloc caps, but `TryPrepareRebuildGraphScratch` could still grow the matching NativeArray snapshots after measuring the cave graph or crater list. That moved allocation out of the generator but not fully out of the rebuild pipeline.
Solution: Prewarm rebuild graph scratch arrays during streaming scratch slot capacity setup: nodes 64, tunnels 128, entrances 8, structures 128, crater stamps 16. `TryPrepareRebuildGraphScratch` now rejects counts above those caps instead of growing arrays.
Rejected Alternatives: Leaving graph arrays to grow after `TryMeasure` was rejected because it is still a post-lease allocation route. Duplicating dynamic graph arrays per volume was rejected because `HectonVoxelVolume` already owns cold runtime snapshots and the rebuild path needs scratch, not new ownership. Raising caps without a ledger was rejected.
Scalability potential: Low uses fixed topology scratch without allocation. Middle/High/Ultra can increase these caps only with an explicit memory ledger and matching scanner gate.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is removal of first complex-cave rebuild graph NativeArray growth during streaming or crater replay.

## Decision 089: Modified-Cell Hash Map Overflow Must Not Produce Partial Truth

Problem: `NativeParallelHashMap.TryAdd` returns false on capacity failure. The delta replay path ignored that return value, so a capacity defect could produce a partial modified-cell truth map and still feed the mesher.
Solution: All four compacted/dirty replay write sites now fail closed when `TryAdd` returns false. The async engine caller clears the scratch and disables the modified-cell map on false, so stale geometry remains visually covered by Dear Lie instead of publishing partial truth.
Rejected Alternatives: Ignoring `TryAdd` was rejected as silent terrain corruption. Growing the hash map on failure was rejected because it would reintroduce hot native allocation. Dropping only the failed cell was rejected because mesh truth must be authoritative.
Scalability potential: Low sheds the rebuild map under impossible capacity defects and keeps visual continuity through GPU clipping. Middle/High/Ultra keep the same fail-closed contract while scratch capacity scales from the rebuild volume cell count.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability, not speed: no partial mesh rebuild is emitted when the modified-cell map capacity contract is violated.

## Decision 090: External Quest Compile Wall Missing Localization Namespace

Problem: A guarded build after the X_006 hash-map fix failed outside the voxel domain in `QuestStateManager.cs`: `LocalizationManager` was unresolved at line 580. Source evidence showed `LocalizationManager` exists under `Hecton.Localization`, while `QuestStateManager` lacked that using directive.
Solution: Added only `using Hecton.Localization;` to `QuestStateManager.cs`. This restores the existing dependency route without changing quest state, localization ownership, save DTOs, or runtime cadence.
Rejected Alternatives: Rewriting the quest text cache was rejected because the missing symbol was a namespace/import defect. Fully qualifying the one line was rejected because neighboring quest files already use the namespace import. Ignoring the wall was rejected because it blocks verification of the voxel/SDF work.
Scalability potential: None for voxel runtime. Low/Middle/High/Ultra quest presentation behavior is unchanged; this only restores compilation.
Hardware Impact: Measured microseconds saved: 0. This is a cross-domain compile-wall correction, not a runtime optimization.

## Decision 091: Streaming Scratch Must Not Grow After Lease Admission

Problem: `HectonVoxelEngine` still called `EnsureNativeArrayCapacity` after a streaming scratch lease was already issued for mesh extraction, mesh attributes, projected positions, cave spatial buckets, rebuild graph snapshots, and collider split scratch. That made first dense Marching Cubes or collider work capable of reallocating native buffers in the middle of the terrain rebuild path.
Solution: Streaming scratch lease admission now receives `gridDimension` and prewarms all dependent mesh, edge-registry, attribute, projection, spatial-bucket, rebuild-graph, and collider arrays. Post-lease `TryEnsure*` methods only verify `IsCreated` and `Length`; over-budget work fails closed, writes `VoxelMeshPipelineScratchCapacityOverflowFlag` into the prewarmed 300-frame voxel mesh black box, and leaves Dear Lie/stale geometry in charge instead of growing buffers.
Rejected Alternatives: Prewarming worst-case 15 vertices per cell was rejected because it would reserve hundreds of MB per slot on high grid dimensions. Keeping exact post-count growth was rejected because it violates Zero-GC/native hot-path requirements. Silently truncating MC output was rejected because terrain truth must fail closed, not publish partial geometry.
Scalability potential: Low uses the 524288 raw-vertex scratch cap and sheds over-budget mesh truth behind GPU clipping. Middle/High/Ultra can increase the fixed cap with a memory ledger; the route still scales by capacity/cadence, not by hot allocation.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch removal: no first-use `NativeArray` dispose/reallocate occurs after MC count, vertex weld, spatial partition, or collider split admission.

## Decision 092: Streaming Scratch Lease Admission Must Be Atomic And Quality-Scaled

Problem: The streaming scratch slot array could be disposed/recreated when `streamingScratchSlotCount` changed while another slot was still leased by a live voxel job. Separately, the raw MC scratch cap was a fixed number, creating a hidden binary quality switch instead of the project-mandated continuous `GlobalQualityWeight` scaling.
Solution: `EnsureStreamingScratchSlots` now refuses slot-array resize while any slot is `InUse`, and `TryAcquireStreamingScratchLease` marks a slot in use only after `EnsureStreamingScratchSlotCapacity` completes. Raw mesh scratch capacity now lerps continuously from 262144 to 786432 vertices using `HomeostasisBrain.GlobalQualityWeight`, with 524288 preserved as the mid-tier cap.
Rejected Alternatives: Disposing slots during runtime count changes was rejected because it can invalidate NativeArrays still referenced by scheduled jobs. Locking the pool for the full job lifetime was rejected because leases already carry ownership and release state. Keeping a fixed cap was rejected because it violates the no-binary-quality-switch rule; increasing every tier to the high cap was rejected because low-end VRAM pays for unused MC headroom.
Scalability potential: Low keeps smaller raw mesh scratch and relies on Dear Lie if a dense MC pass exceeds capacity. Middle preserves the previous 524288 budget. High/Ultra gain extra visual headroom without changing authority, DTO layout, save identity, or the post-lease no-growth rule.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability plus lower scratch pressure at low quality: no live-slot disposal during runtime pool resize and less raw MC scratch reserved when `GlobalQualityWeight` is near minimum.

## Decision 093: Scratch Prewarm Failure Must Fail Closed

Problem: Even after moving `InUse` assignment after prewarm, `EnsureStreamingScratchSlotCapacity` still threw directly if a NativeArray allocation/prewarm failed. That could abort the async generation path with an exception instead of producing a black-boxed, fail-closed lease denial.
Solution: Added `TryEnsureStreamingScratchSlotCapacity`. Lease admission uses it before setting `InUse`; on exception it records `VoxelMeshPipelineScratchCapacityOverflowFlag`, logs the exception in editor, returns false, and continues scanning other free slots.
Rejected Alternatives: Letting exceptions bubble was rejected because the terrain pager needs bounded denial, not a broken coroutine. Swallowing exceptions without black-box telemetry was rejected because post-mortem proof would be missing. Retrying growth inside the same slot was rejected because the failed prewarm is already an admission-time capacity/memory fault.
Scalability potential: Low fails closed under native memory pressure and leaves Dear Lie/stale geometry active. Middle/High/Ultra keep the same failure route; higher quality increases allowed scratch capacity but does not change the lease contract.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: native scratch admission failure no longer creates a half-admitted lease or uncaught terrain generation exception.

## Decision 094: GPU Stamp Dispatch Must Not Replay Stale Buffers

Problem: `SargassumCutManager` bounded cut-mask and damage-volume stamp queues, but `ProcessQueuedMaskUpdate` and `ProcessQueuedDamageVolumeUpdate` could still dispatch with `_queuedStampCount` or `_queuedDamageVolumeStampCount` when `GlobalDataVault` failed to provide the CPU staging buffer for upload. That route can replay stale `GraphicsBuffer` contents with a fresh nonzero count under vault pressure.
Solution: Added local uploaded-count variables. A nonzero stamp count now requires a same-frame successful vault acquisition and `GraphicsBufferUploadUtility.UploadNativeArray` before compute dispatch receives the count. If acquisition fails, the method returns with the queued CPU-side stamps intact for retry and performs no stale GPU dispatch.
Rejected Alternatives: Dispatching count zero after failed upload was rejected because it would still advance mask/damage recovery and clear queued stamps in the same frame path. Clearing the queue on failed upload was rejected because it drops the player's fresh drilling visual. Growing or duplicating the staging buffer was rejected because the lane must stay bounded inside `GlobalDataVault`.
Scalability potential: Low retries the bounded 16-command visual upload next frame and leaves the prior visual state visible. Middle/High/Ultra keep the same 16-command buffer; higher quality changes texture dimensions and recovery cadence, not command-buffer authority or queue capacity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is correctness under pressure: no stale visual carving/damage stamps are replayed when the vault staging lane is temporarily unavailable.

## Decision 095: Direct Pager Reads Must Not Publish A Slice On Non-Ready Status

Problem: `H8BinaryWorldPager.TryReadPageIntoVaultSlice` acquired the read staging slice before checking the page header and payload. Missing, corrupt, or IO-error returns could therefore leave a valid out slice handle pointing at staging memory even though `status` was not `Ready`. There are no current callers, but this is a future stale-read footgun in the `world_data.h8bin` route.
Solution: Changed the method to acquire into a local `stagingSlice`, resolve it locally, and assign the out `slice` only after payload hash verification and `status = H8WorldPageStatus.Ready`. All non-ready returns leave `slice = default`.
Rejected Alternatives: Relying on every future caller to inspect `status` was rejected because the method name returns a slice and the bool can be misread as success. Clearing the staging buffer on every non-ready result was rejected because it wastes bandwidth and still exposes a valid handle. Adding a managed wrapper result was rejected because this path must remain zero-GC and blittable-friendly.
Scalability potential: Low/Middle/High/Ultra all keep the same fixed 2-sector read staging slice. This changes publication correctness only; queue capacity, sector size, and RLE/raw storage behavior are unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: future direct pager reads cannot consume stale read-staging memory after a missing/corrupt world page.

## Decision 096: Direct Pager Read Staging Must Be Prewarmed, Not Acquired During Read

Problem: After the slice publication gate, `H8BinaryWorldPager.TryReadPageIntoVaultSlice` still used `GlobalDataVault.TryAcquireSliceHandle` inside the direct read path. If a future caller used the route before staging was created, the read accessor could force a `GlobalDataVault` buffer allocation/growth while reading `world_data.h8bin`.
Solution: Added `_readStagingHandle`, prewarmed `SaveWorldPagerReadStaging` at pager allocation with fixed `SectorPayloadBytes * 2` capacity, included it in readiness and release, and changed direct reads to `TryResolveDirectReadStaging`. The direct read path now only resolves an existing handle and publishes the slice after `Ready`.
Rejected Alternatives: Leaving first-use staging allocation in `TryReadPageIntoVaultSlice` was rejected because read accessors must not grow buffers. Reusing the queued read arena was rejected because it is slot-state owned by the worker ticket route. Adding managed per-call staging was rejected because it violates zero-GC and memory-bus constraints.
Scalability potential: Low/Middle/High/Ultra all use the same fixed 524160-byte direct-read staging buffer. It does not scale gameplay truth, DTO layout, save identity, or authority route; it removes a hidden runtime allocation path.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch avoidance: direct world-page reads can no longer allocate/grow the read staging buffer on the frame that needs the page.

## Decision 097: PhysX Bake Schedule Failure Must Return Pool Meshes

Problem: Collider chunk bake meshes are drawn from the fixed 256-slot `_voxelPhysicsBakeMeshPool`. When `TryScheduleVoxelPhysicsBake` failed twice, the chunk paths called `DetachColliderChunkBakeMesh`, which only drops the volume reference because it is meant for deferred-teardown ownership. In the schedule-fail branch no deferred owner exists, so the pool slot stayed marked in use forever.
Solution: Added `HectonVoxelVolume.ReleaseColliderChunkBakeMesh` for the no-deferred-owner case. The two schedule-fail branches now release the staged mesh back to the physics bake mesh pool, disabling the proxy/collider first. Existing `DetachColliderChunkBakeMesh` remains for cancellation/watchdog paths where deferred teardown owns the mesh.
Rejected Alternatives: Changing `DetachColliderChunkBakeMesh` to always release was rejected because cancellation/watchdog paths enqueue a deferred teardown that later releases the same mesh. Growing the physics mesh pool was rejected because it hides the leak and increases low-end memory pressure. Publishing the collider synchronously on schedule failure was rejected because PhysX sharedMesh mutation is not acceptable in the deformation frame.
Scalability potential: Low keeps the fixed 256-slot pool usable under repeated schedule failures. Middle/High/Ultra keep the same ownership contract; higher tiers may bake more chunks per frame, but failed schedules no longer permanently consume slots.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is long-session stability: repeated bake scheduling pressure cannot drain the fixed physics bake mesh pool during drilling or fast paging.

## Decision 098: External Docking Compile Wall Missing Physics Namespace

Problem: A guarded build after the X_006 PhysX pool release patch failed outside the voxel domain in `Construction/VehicleDockingModule.cs`: `SubmarineFluidDynamics` was unresolved. Source evidence shows `SubmarineFluidDynamics` is defined in `Hecton8.Physics`.
Solution: Added only `using Hecton8.Physics;` to `VehicleDockingModule.cs`. This restores the existing type reference without changing docking state, transport physics, save identity, or fluid authority.
Rejected Alternatives: Rewriting docking to avoid `SubmarineFluidDynamics` was rejected because the error is a namespace/import defect. Moving the fluid type or adding an alias wrapper was rejected because that changes ownership outside X_006. Ignoring the wall was rejected because it blocks compile proof for the voxel changes.
Scalability potential: None for voxel runtime. Low/Middle/High/Ultra docking and fluid behavior are unchanged; this only restores compilation.
Hardware Impact: Measured microseconds saved: 0. This is a cross-domain compile-wall correction, not a runtime optimization.

## Decision 099: External Crash Telemetry Compile Wall Missing Physics Namespace

Problem: A guarded build after the docking import fix failed outside the voxel domain in `CrashTelemetryBuffer.cs`: `PhysicsDeterminismSignals` was unresolved. Source evidence shows the deterministic KCC velocity signal owner is `Hecton8.Physics.PhysicsDeterminismSignals`.
Solution: Added only the missing `Hecton8.Physics` import to `CrashTelemetryBuffer.cs`, matching existing consumers of deterministic KCC velocity signals. The crash telemetry ring, binary dump layout, and export cadence are unchanged.
Rejected Alternatives: Rewriting telemetry to read `Rigidbody.linearVelocity` again was rejected because that reintroduces non-owner hot physics reads. Fully qualifying the call was rejected because the file already imports domain namespaces at the top and the deterministic physics signal is the intended owner route. Ignoring the wall was rejected because it blocks compile proof for X_006.
Scalability potential: None for voxel runtime. Low/Middle/High/Ultra crash telemetry behavior is unchanged; this only restores compilation after a route correction by another domain.
Hardware Impact: Measured microseconds saved: 0. This is a cross-domain compile-wall correction, not a runtime optimization.

## Decision 100: GlobalDataVault Pool Limits Must Be Machine-Visible

Problem: The static voxel proof already checked X_006 vault routes, but the report left `global_data_vault_pool` absent. That made the stress answer rely on prose for unmanaged pool limits instead of a reproducible scanner artifact.
Solution: Extended `Tools/OOP_Voxel_Scanner.py` to parse `GlobalDataVault` numeric constants and `GameBootstrapper` primary prewarm anchors. The generated report now includes the vault pool ledger: 64-byte block alignment, 128 MiB initial arena, 512 MiB minimum quality arena, 4 GiB maximum quality arena, 32768 buffer descriptors, 65536 block descriptors, 100000 generation handles, boot prewarm anchors, bounded growth guards, and X_006 fixed lane payload totals including both cut-mask and damage-volume stamp buffers.
Rejected Alternatives: Writing only a chat explanation was rejected because it is not a durable proof artifact. Hardcoding claims in a markdown report was rejected because scanner output must be regenerated from source. Editing `GlobalDataVault` was rejected because this pass found a missing proof surface, not a runtime allocation defect.
Scalability potential: Low uses the minimum 512 MiB arena cap and bounded X_006 lanes; Middle/High/Ultra can raise the arena to the continuous profile limit up to 4 GiB without changing DTO layout or authority routes.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is verification quality: the report now exposes whether fixed X_006 lanes fit the preallocated vault budget instead of hiding that calculation in chat.

## Decision 101: External ScannableFragment Compile Wall Missing Visual Reset Wrapper

Problem: A guarded build after the X_006 proof-ledger patch failed outside the voxel domain in `ScannableFragment.cs`: `StopScanning` and `ResetState` called `QueueScanVisualReset`, but the file's current late-frame renderer route only defined `ResetScanVisuals`.
Solution: Added a private `QueueScanVisualReset()` wrapper that delegates to `ResetScanVisuals()`. This keeps the reset in the existing pending late-frame path and does not touch scan state, lore state, renderer ownership, material property block layout, or interaction authority.
Rejected Alternatives: Replacing both call sites directly was rejected because the current file naming indicates queue semantics and the wrapper preserves that caller contract. Applying the reset synchronously was rejected because renderer/property-block mutation should remain in the existing late-frame lane. Reworking scannable interaction was rejected because the compile wall was a missing symbol, not a gameplay defect.
Scalability potential: None for voxel runtime. Low/Middle/High/Ultra scannable behavior is unchanged; this restores compilation while preserving deferred visual work.
Hardware Impact: Measured microseconds saved: 0. This is a cross-domain compile-wall correction, not a runtime optimization.

## Decision 102: PhysX Bake Registration Failure Must Stay Nonblocking

Problem: `EnqueueDeferredVoxelPhysicsBakeTeardown` still had an emergency route where late-frame driver registration failure removed the freshly queued teardown and called `ForceReleaseDeferredVoxelPhysicsBakeTeardown`, which uses `DispatcherJobSwap.TryComplete(... forceComplete: true)`. The normal schedule path already refuses new bake jobs when the driver cannot register, but this post-schedule failure case was still a synchronous completion risk.
Solution: Keep the disabled/proxy teardown entry in the fixed 2048-entry queue when registration fails after scheduling. The next successful `EnsureDeferredVoxelPhysicsBakeTeardownRegistered` call can attach the driver and drain only completed handles. Scanner proof now records `physics_bake_registration_failure_nonblocking`.
Rejected Alternatives: Forcing completion was rejected because it can stall the deformation path under a dispatcher race. Dropping the mesh immediately was rejected because the bake job may still own it. Growing or creating a managed fallback queue was rejected because the existing fixed deferred queue is already the correct bounded owner.
Scalability potential: Low holds the proxy fake and stale collider while the queued teardown waits for a valid late-frame lane. Middle/High/Ultra use the same route; quality can raise collider upload cadence, not change ownership or force synchronous completion.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch avoidance in dispatcher-race conditions: no forced bake completion on the active deformation path.

## Decision 103: Pager Prefetch Tickets Need Stable Monotonic IDs

Problem: `WorldChunkResidencyManager.RequestAsyncPagerRead` derived pager request ids from `chunkId xor Time.frameCount`. Dense prefetch churn can request the same chunk more than once in one frame, creating duplicate request ids inside the fixed ticket ring and risking wrong result retirement.
Solution: Added `_pagerReadRequestSequence` and `ResolveNextPagerReadRequestId()` to issue monotonic nonzero request ids. The existing 16-ticket native ring, payload type, `H8WorldPageReadTicket` layout, and async retire path are unchanged.
Rejected Alternatives: Hashing chunk id with more frame state was rejected because it still depends on time and can collide. Enlarging the ticket ring was rejected because the issue is identity correctness, not capacity. Adding managed GUID/string ids was rejected because pager tickets are blittable fixed-layout contracts.
Scalability potential: Low/Middle/High/Ultra keep the same fixed ticket capacity and bounded retire behavior. Higher tiers may issue prefetches more often, but id uniqueness no longer depends on frame cadence.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is correctness under streaming pressure: no false async pager result match from same-frame duplicate request ids.

## Decision 104: Pager Prefetch Retirement Must Scale By GlobalQualityWeight

Problem: The chunk residency async pager read ticket ring was fixed at 16 slots, but normal late-frame retirement always processed only one completed ticket. That is cheap on low-end hardware but unnecessarily slow on high/ultra tiers and can hold stale read results longer during fast traversal.
Solution: Replaced the fixed retire constant with `ResolvePagerReadRetireBudget()`, continuously scaling retirement from 1 to 4 tickets by `HomeostasisBrain.GlobalQualityWeight`. Full-ring admission still attempts a bounded 16-ticket drain before rejecting a new prefetch.
Rejected Alternatives: Enlarging the ticket ring was rejected because it increases fixed memory and does not solve stale result dwell time. Using discrete quality tier switches was rejected because project policy requires continuous `GlobalQualityWeight`. Retiring all tickets every frame was rejected because low-end devices should keep the cheapest cadence.
Scalability potential: Low retires one ticket per late frame. Middle increases cadence gradually. High/Ultra retire up to four completed tickets per late frame, freeing pager pressure faster during scooter traversal without changing save identity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is unchanged low-tier cost; high-tier reduces pager result dwell and ring saturation probability under fast movement.

## Decision 105: Hydration Apply Diagnostics Must Not Acquire Vault Slices During Activation

Problem: `WorldChunkResidencyManager.CopyHydrationApplyRecordToVault` wrote a 64-byte `ChunkHydrationApplyRecord` during chunk activation by acquiring a byte slice from `GlobalDataVault`. Under fast traversal this is inside the chunk hydration boundary and could force a vault buffer grow or slice metadata path while activation is already spending the frame budget.
Solution: Added a prewarmed `_hydrationApplyRecords` `NativeArray<ChunkHydrationApplyRecord>` using `HydrationApplyRecordVaultBufferId` during `AllocateNativeState`. Runtime activation now writes the explicit-layout 64-byte record directly by chunk index and returns if the ledger is unavailable or too small. Scanner proof records default capacity 512 records and 32768 bytes, sentinel registration, owner release, and no runtime `TryAcquireSlice*`, `EnsureGenerationHandle`, `UnsafeUtility.MemCpy`, or `UnsafeUtility.Malloc` in the copy route.
Rejected Alternatives: Keeping first-use slice acquisition was rejected because read/diagnostic accessors must not grow buffers in runtime owner phases. Storing managed activation logs was rejected because it violates zero-GC. Removing the diagnostic record was rejected because black-box-style evidence is useful under streaming spikes.
Scalability potential: Low gets a fixed 32768-byte default ledger and fails closed under pressure. Middle/High/Ultra can use a larger serialized `maxChunkCount`, but the route stays prewarmed and does not change chunk authority, save identity, or DTO layout.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch avoidance: chunk activation no longer has a hidden `GlobalDataVault` slice acquisition/growth path while the player crosses chunk boundaries quickly.

## Decision 106: Teleport Residency Reset Must Not Force-Complete Scan Jobs

Problem: `WorldChunkResidencyManager.HandleTeleport` called a teleport-specific force-complete helper before clearing streaming queues. A large AUP jump could therefore block the main thread on a live residency scan/sort job exactly when traversal pressure is already high.
Solution: Removed the teleport force-complete route. `HandleTeleport` now stores `_pendingTeleportAup` and `_teleportResetPending` if a residency job is live. `Tick` calls `CompleteResidencyJobIfFinished()` before `TryApplyPendingTeleportReset()`, so stale job output is finalized or ignored before queue clearing and immediate-radius loads run. Force-complete remains only in teardown/service-rebind paths.
Rejected Alternatives: Clearing queues while the job is still writing them was rejected because it races native lists. Keeping forced completion was rejected because teleport should be handled by deferring the reset one or more frames, not by blocking the frame. Enlarging load queues was rejected because the defect was a sync point, not capacity.
Scalability potential: Low shows stale/pending residency for at most the natural job completion window and then applies the immediate-radius load. Middle/High/Ultra get the same correctness route; faster devices naturally reduce the delay without changing chunk DTOs or paging identity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch avoidance under scooter/teleport jumps: no residency job is synchronously completed from the gameplay tick.

## Decision 107: Surface Nets GPU Upload Release Must Be Deferred, Not Forgotten

Problem: `VoxelSurfaceNetsGpuUploadDispatcher.Release()` delegated to `TryRelease()` and ignored the false result when a GPU upload copy job was still running. The path did not force-complete, which is correct, but it also had no durable release-request state to prevent new uploads or drain buffers once the job naturally completed.
Solution: Added `_releaseRequested`. `Release()` marks it and tries a nonblocking release. `TryBeginUpload` rejects new uploads while a release is pending. `TryRelease` returns false without completing the job if `_pendingUploadDependency` is not completed, and finalization drains the pending release after completed-job unlock.
Rejected Alternatives: Forcing `_pendingUploadDependency.Complete()` from `Release()` was rejected because it would reintroduce a sync point. Releasing locked `GraphicsBuffer` ranges before the job completes was rejected because the job still writes into locked memory. Ignoring the release request was rejected because it can leak GPU buffers on teardown edges.
Scalability potential: Low keeps the previous no-wait behavior and defers GPU buffer destruction. Middle/High/Ultra use the same route; higher quality can increase mesh capacity, but release authority remains nonblocking and explicit.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stability: Surface Nets teardown no longer loses a release request when a GPU upload job is live.

## Decision 108: PhysX Bake Overflow Must Not Force-Complete Live Jobs

Problem: `EnqueueDeferredVoxelPhysicsBakeTeardown` still had an overflow branch that could call `ForceReleaseDeferredVoxelPhysicsBakeTeardown`, which uses `DispatcherJobSwap.TryComplete(... forceComplete: true)`. Normal scheduling backpressure should make the branch unreachable, but a stress proof cannot rely on "unreachable" for a live deformation path.
Solution: Added a fixed 512-entry emergency teardown array for already-scheduled bake jobs when the normal 2048-entry deferred list is saturated. Overflow now disables presentation/proxy state, records the teardown in that fixed lane, and drains it only after the job handle is already completed. Backpressure telemetry now uses total normal+emergency pending count. The remaining forced completion helper is named `ForceReleaseDeferredVoxelPhysicsBakeTeardownForShutdownOnly` and is called only by dispatcherless reset/shutdown flushing.
Rejected Alternatives: Keeping the force-complete branch was rejected because it can create the exact frame hitch this pass is removing. Growing the managed list was rejected because it hides pressure and can allocate. Dropping the mesh without tracking was rejected because the bake job may still own it. Completing only on shutdown remains because dispatcherless subsystem reset must release Unity objects deterministically.
Scalability potential: Low gets fail-closed stale/proxy presentation under pathological bake teardown pressure. Middle/High/Ultra use the same ownership route; higher quality can drain/publish more collider work per frame but cannot change the no-force-complete deformation contract.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch avoidance under pathological drilling/backpressure: enqueue overflow no longer blocks on an in-flight PhysX bake job.

## Decision 109: Collider Chunk Object Creation Must Be Prewarm-Only

Problem: `HectonVoxelVolume.EnsureColliderChunkCapacity` creates child GameObjects plus MeshCollider/BoxCollider components. It was only called by `PrewarmColliderChunkHierarchy`, but the method remained public, which left a future hot-path allocation API exposed.
Solution: Made `EnsureColliderChunkCapacity` private and updated the scanner proof to require the private builder plus hot split usage of `TryUsePrewarmedColliderChunkCapacity`. Runtime collider split paths cannot call the allocator directly and continue to fail closed to the cinematic fake when prewarm data is missing.
Rejected Alternatives: Leaving the method public was rejected because public allocation APIs become accidental hot-path dependencies. Moving object creation into the split path was rejected because it violates zero-GC and Unity-object prewarm policy. Removing collider chunk prewarm was rejected because chunked collider bakes need stable proxy objects.
Scalability potential: Low avoids accidental object/component creation during deformation. Middle/High/Ultra keep the same fixed 8-chunk registry; quality changes drain cadence, not hierarchy ownership.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch prevention: no external caller can create collider chunk GameObjects from the deformation path by mistake.

## Decision 110: Voxel Carve Black Box Dump Path Must Match The Mandate

Problem: The carve black-box ring existed, but `VoxelDeltaProcessor` wrote it to `Docs/AgentLogs/Dump_X_006.bin` while the X_006 mandate names `Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin`. That mismatch makes crash-forensics automation brittle even if the binary layout is valid.
Solution: Changed `VoxelBlackBoxDumpRelativePath` to `Docs/AgentLogs/Dump_SHINOBU_308_Voxel.bin` and updated the scanner proof token. The dump header, 300-frame ring payload, and write route are unchanged.
Rejected Alternatives: Keeping the old generic path was rejected because the prompt and downstream CTO read path are explicit. Writing both files was rejected because it doubles crash IO and creates two possible truths. Renaming the mesh-pipeline dump was rejected because it has a different binary format and reason domain.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged; this only makes the forensic artifact discoverable under the mandated path.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is diagnostic reliability: carve crash dumps land where automated review expects them.

## Decision 111: RLE Byte Proof Must Resolve Const-Sized Struct Layouts

Problem: The scanner's `struct_layout()` parser only accepted numeric `StructLayout(Size = N)` literals. `SaveVoxelDeltaRun8` uses `StructLayout(Size = SaveDeltaCompressionLayout.SaveVoxelDeltaRun8StrideBytes)`, so the report emitted a false 0-byte run layout and understated the native worst-case packet size.
Solution: Updated `Tools/OOP_Voxel_Scanner.py` to evaluate const expressions and qualified const names in `StructLayout(Size = ...)`. The regenerated report now records `SaveVoxelDeltaRun8` and `VoxelDeltaRleRunDTO` as identical 8-byte explicit layouts, native pathological sparse RLE as 262184 bytes, dense fallback total as 135208 bytes, and effective sector overage as 0.
Rejected Alternatives: Hardcoding `SaveVoxelDeltaRun8=8` in the report was rejected because the proof must be source-derived. Leaving the 0-byte artifact was rejected because it makes the memory-stress answer mathematically invalid. Changing the runtime DTO was rejected because the runtime layout was already correct; the proof parser was defective.
Scalability potential: Low/Middle/High/Ultra runtime behavior is unchanged. The proof now correctly shows why a worst-case sparse packet cannot grow the pager queue: pathological RLE converts to dense payload inside the fixed sector budget.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is verification integrity: the report now exposes the real 262184-byte pathological sparse case and the 135208-byte dense fallback bound.

## Decision 112: Chunk Residency Paging Must Use Double AUP Distance For Decisions

Problem: `RadiusBasedStreamingJob` subtracted chunk/player AUP in `double3` but then cast the delta to `float3` and used `math.lengthsq(localDelta)` for load/unload thresholds. That keeps values small after subtraction, but it still makes paging decisions depend on float precision instead of the project's authoritative AUP math.
Solution: Replaced the radius job's decision distance with `AupPrecisionMath.DistanceSqSafeDouble(chunk, player)`. The float distance is now a clamped DTO telemetry value only. Scanner proof now requires safe double distance in the radius job, sort job, and projected AUP route.
Rejected Alternatives: Keeping the float route was rejected because paging authority should not depend on presentation precision. Computing only chunk IDs from floats was rejected because scooter-speed traversal and large worlds need stable AUP comparisons. Changing DTO layout to store double distance was rejected because the DTO is a presentation/telemetry surface and the decision path is now double.
Scalability potential: Low/Middle/High/Ultra use the same authoritative load/unload decisions. Quality can alter radius/cadence, not the coordinate math or chunk identity.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is correctness under large-world traversal: no extra allocations or job count changes, but fewer edge-case false load/unload decisions from float distance rounding.

## Decision 113: Voxel Delta WAL Payloads Must Fail Before Pager Queue Admission

Problem: `VoxelDeltaCompressionArchitecture` could pack a compressed voxel-delta WAL payload larger than a single `H8BinaryWorldPager` sector payload if its staging buffer was larger than the pager sector. The pager rejected oversized writes, so memory did not grow, but the rejection happened after WAL packing and enqueue attempt.
Solution: Added `MaxVoxelDeltaWalPayloadBytes = 262080` to the voxel delta compression architecture and guarded both `VoxelWalPayloadPackJob` and `TryEnqueueVoxelDeltaWalWrite`. Oversized payloads now set failure/return false before they enter the pager write queue.
Rejected Alternatives: Relying only on `H8BinaryWorldPager.TryEnqueueWrite` was rejected because it proves queue safety but leaves the compressor's own contract loose. Growing the pager sector was rejected because it changes save storage geometry. Silently truncating payloads was rejected because it corrupts voxel deltas.
Scalability potential: Low/Middle/High/Ultra keep the same sector geometry. Quality can alter compression effort and write cadence, not WAL payload size authority.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is pressure containment: pathological sparse RLE fails in the compression pipeline instead of reaching pager admission, while the native dense fallback remains the primary bounded runtime snapshot route.

## Decision 114: Laser Carve Backlog Needs Continuous Time-Sliced Cadence

Problem: `VoxelDeltaProcessor` used thresholded queue-drain budgets and a fixed 64 scheduled-write commit budget. Under the mandated 60 Hz laser-drill stress case, memory stayed bounded by the 64-event ingress queue, 32 pending ring, and 131072-write arena, but the cadence was not continuously scaled by `GlobalQualityWeight` and high-tier hardware could not spend saved frame budget to drain backlog faster.
Solution: Added token-bucket cadence for queued carve drain and scheduled carve commit. Queue drain now scales continuously from 1 to 4 events per frame. Scheduled commit now scales continuously from 64 to 512 cell writes per frame, with bounded backlog pressure raising cadence without increasing queue or arena capacity. The black-box still records current drain budget, queue count, pending count, and scheduled write count.
Rejected Alternatives: Raising fixed budgets was rejected because it would punish MX350-class devices. Growing queues was rejected because the stress requirement is no unbounded memory, not larger buffers. Draining all writes in one frame was rejected because it risks a deformation-frame spike and violates the time-slice requirement.
Scalability potential: Low keeps the 64 writes/frame survival cadence and relies on coalescing plus Dear Lie clipping. Middle ramps gradually. High/Ultra can commit up to 512 cell writes/frame and reduce visual/mesh delay while preserving identical carve authority and save DTO layouts.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is no new hot allocation and no higher low-tier burst cost. High-tier impact is reduced backlog dwell under continuous drilling: an 8x8x8 localized laser candidate block can commit in one high-quality late frame instead of eight low-tier frames.

## Decision 115: Chunk Load Dispatch Must Scale Continuously Without Same-Frame Overdispatch

Problem: `WorldChunkResidencyManager` still dispatched chunk loads through a discrete tier mapping. That met the fixed low/high budget intent but violated the continuous `GlobalQualityWeight` rule and could not express middle-tier cadence. The first token-bucket pass also clamped the return value to at least one load, which could overdispatch if the resolver were called twice in one frame.
Solution: Replaced the tier switch with a continuous token bucket driven by `HomeostasisBrain.GlobalQualityWeight`, scaling from 1 to 4 load starts per frame. The resolver now returns zero when same-frame cadence is already spent, and load-dispatch tokens reset with streaming queue clears and activation runtime clears.
Rejected Alternatives: Keeping low/middle/high/ultra integer switches was rejected because quality control must be continuous. Enlarging the load queue was rejected because queue size is not the bottleneck. Forcing at least one load on every resolver call was rejected because it is a hidden same-frame burst path.
Scalability potential: Low starts one chunk load per frame. Middle ramps smoothly instead of jumping tiers. High/Ultra can start up to four loads per frame during scooter-speed traversal while preserving the same load queue, chunk DTOs, pager identity, and save data.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is no added low-tier burst cost. High-tier impact is lower fixed-ring dwell under traversal pressure without allocating or changing chunk truth ownership.

## Decision 116: Residency Radius And Concurrent Load Capacity Must Not Use Tier Branches

Problem: After the load-dispatch token bucket was fixed, `WorldChunkResidencyManager` still used discrete tier branches for max concurrent loads, predictive lookahead distance, and load/unload radii. That made scooter-speed traversal behavior jump by hardware bucket instead of scaling continuously by the project quality scalar.
Solution: Added `ResolveSmoothGlobalQualityWeight01()` and routed pager retire cadence, load dispatch cadence, concurrent load cap, predictive lookahead, and load/unload radii through the same continuous smoothstep quality scalar. Health/VRAM squeeze remains a clamp, not a quality switch.
Rejected Alternatives: Leaving tier branches was rejected because binary quality switches are explicitly forbidden. Expanding queue sizes was rejected because capacity growth does not fix cadence discontinuity. Changing chunk DTO layouts or save identity was rejected because quality may scale cadence/radius but cannot alter authority contracts.
Scalability potential: Low keeps survival radii and one-to-two concurrent load pressure. Middle ramps smoothly. High/Ultra expands residency radius and concurrent load starts without changing chunk IDs, pager requests, or save geometry.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is no higher low-tier pressure. High-tier impact is lower visible chunk dwell and fewer hard streaming cliffs during fast traversal because radius and concurrent-load cap no longer jump through coarse buckets.

## Decision 117: Async Upload Budget Must Follow Continuous Quality At Runtime

Problem: Unity async upload buffer size/time slice in `WorldChunkResidencyManager` was written by a tier switch during `Awake` only. That created coarse hardware-bucket jumps and ignored later `GlobalQualityWeight` changes caused by thermal or memory pressure.
Solution: Replaced the switch with a continuous quality-derived upload buffer/time-slice pair and a budget hash guard. `Tick` calls the guarded writer, so changing `GlobalQualityWeight` updates Unity upload pressure without repeated writes when the resolved budget is unchanged.
Rejected Alternatives: Keeping the `Low/Middle/default` switch was rejected because it is a binary quality control. Writing `QualitySettings` every frame without a hash was rejected because global setting churn is unnecessary. Changing mesh upload DTOs or pool sizes was rejected because this is an upload cadence issue, not a geometry ownership issue.
Scalability potential: Low resolves near 64 MB/1 ms upload settings. Middle ramps smoothly. High/Ultra can reach 256 MB/4 ms and reduce chunk/mesh upload dwell without changing chunk truth, save identity, or pager lanes.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stable low upload pressure. High-tier impact is lower delayed mesh visibility under traversal and deformation publication without adding managed allocations.

## Decision 118: Runtime Voxel Volume Spawn Must Be Pool-Only

Problem: `HectonVoxelEngine.SpawnVolume()` fell back to `new GameObject(RuntimeCaveVolumeName)` plus `AddComponent<MeshFilter/MeshRenderer/MeshCollider/HectonVoxelVolume>()` when the pool or prefab route missed. That is acceptable as an editor authoring fallback, but in play mode it creates managed objects and Unity components after the voxel pipeline has already spent rebuild work.
Solution: The runtime fallback now fails closed in play mode: it writes `VoxelMeshPipelineVolumeSpawnPoolMissFlag` into the mesh-pipeline black-box ring and returns null. Both generation call sites now null-check the result before naming/positioning/applying mesh data. The editor fallback remains after the play-mode guard.
Rejected Alternatives: Creating the object anyway was rejected because it hides a pool-prewarm failure behind a scene allocation spike. Growing or lazy-creating the pool from `SpawnVolume()` was rejected because the generation path is not the owner phase for object-pool capacity. Throwing an exception was rejected because the black-box route gives crash/NaN forensics without adding an uncontrolled failure mode.
Scalability potential: Low fails to stale/no volume when prewarm is broken instead of hitching. Middle/High/Ultra use the same object-pool contract; higher quality can publish more mesh work, but cannot create volume hierarchy from the hot path.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch prevention under pool exhaustion or prefab misconfiguration: no runtime GameObject/component allocation is performed by the voxel generation path.

## Decision 119: External Compile Wall Fixes Must Stay Mechanical

Problem: The guarded build after the volume-spawn patch failed outside X_006: `DemoDoor` used old late-frame registry method names, `PDAInventoryTab` referenced a loop-scoped `length`, and `AutonomousExtractorSystem` called the generic `IDataVault.EnsureGenerationHandle<T>` without specifying `T`.
Solution: Applied the smallest compile-only fixes: `DemoDoor` now calls `TryRegisterLateFrameTickable`/`UnregisterLateFrameTickable`, `PDAInventoryTab` predeclares `length`, and `AutonomousExtractorSystem` calls `EnsureGenerationHandle<T>`. No domain behavior, DTO layout, buffer capacity, or scheduling policy was changed.
Rejected Alternatives: Ignoring the build errors was rejected by the fail-fast protocol. Refactoring the affected systems was rejected because these are outside X_006 and the defects were mechanical API/scope mismatches. Adding compatibility wrapper methods to `GlobalRegistry` was rejected because it would reintroduce legacy API surface globally.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. These edits only restore compile compatibility with existing dispatcher and vault contracts.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is none at runtime; this removes compile blockers without touching performance-critical X_006 code.

## Decision 120: HLOD Impostor Residency Flags Must Use Continuous Quality

Problem: `WorldChunkResidencyManager.TryResolveChunkImpostorPayload` still selected `FlagSurvivalSnap` with `_resolvedTier == ChunkStreamingScalabilityTier.Low`. That is a presentation-only decision, but it was still a binary tier switch inside the paging/HLOD lane.
Solution: Added `ChunkImpostorSurvivalSnapQualityThreshold` and routed survival snap vs dither blend through `ResolveSmoothGlobalQualityWeight01()`. The async upload helper was renamed to `ApplyAsyncUploadBudgetForQuality()` so the code no longer implies that the upload budget is tier-driven.
Rejected Alternatives: Leaving the tier branch was rejected because the mandate forbids low/ultra dichotomy in quality decisions. Removing the snap mode entirely was rejected because low-quality survival presentation still needs the cheapest impostor transition. Adding more enum tiers was rejected because it keeps the same discontinuity.
Scalability potential: Low resolves to survival snap below the threshold. Middle transitions into dither blend without a hardware-bucket jump. High/Ultra keep dither blend while spending saved budget on larger radius/concurrency/upload cadence already controlled by continuous quality.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is stable cheap HLOD presentation below the threshold; higher devices avoid low-tier snap artifacts without changing chunk identity, DTO layout, or save data.

## Decision 121: World Residency Must Not Carry Dead Hardware Tier State

Problem: After radius, dispatch, upload, and HLOD presentation were moved to continuous quality, `WorldChunkResidencyManager` still carried `_resolvedTier`, `ResolveScalabilityTier()`, and tier parameters on radius/prediction helpers. The values were mostly dead, but they preserved a binary quality route for future regressions and fed `MacroDatabaseTier` directly from hardware buckets.
Solution: Removed `_resolvedTier` and `ResolveScalabilityTier()`. Radius and prediction helpers now have no tier parameter. `ResolveMacroDatabaseTier()` now adapts the continuous `ResolveSmoothGlobalQualityWeight01()` value into the external `MacroDatabaseTier` enum using explicit thresholds, because that downstream service still exposes an enum contract.
Rejected Alternatives: Keeping dead tier state was rejected because it invites later binary switches. Removing `MacroDatabaseTier` use was rejected because that is an external service contract outside X_006. Mapping hardware VRAM/RAM to macro tier was rejected because quality authority must be the global continuous scalar, not device buckets.
Scalability potential: Low maps the macro database to the cheapest enum only through the low end of `GlobalQualityWeight`. Middle/High/Ultra progress through thresholds without reintroducing device-tier state in residency logic. Chunk identity and pager DTOs are unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is predictability: streaming behavior follows the same global quality pressure source as the rest of the voxel/paging lane, rather than diverging by stale hardware classification.

## Decision 122: Shader Stamp Count Must Match The Clamped GraphicsBuffer Upload

Problem: `SargassumCutManager` used `GraphicsBufferUploadUtility.UploadNativeArray`, which internally clamps writes to source length and buffer count, but then passed the original queue count into the compute shader. Under normal queue discipline the count is capped at 16, but the stress proof should not rely on a hidden uploader clamp if the shader still receives an unclamped dispatch count.
Solution: Added explicit saturating counts for cut-mask stamps and 3D damage-volume stamps: each upload count is the minimum of the queued count, the borrowed vault slice length, and the fixed command capacity. The same safe count is used for the GPU upload and the shader `_StampCount` / `_DamageVolumeStampCount` value.
Rejected Alternatives: Relying on `GraphicsBufferUploadUtility.ResolveSafeWriteCount` was rejected because it protects memory copy size but does not publish the clamped count back to the shader. Increasing the GraphicsBuffer capacity was rejected because the queue is already fixed and overflow coalesces coverage. Dropping all stamps on any mismatch was rejected because a bounded clamp preserves visual continuity without changing gameplay authority.
Scalability potential: Low remains 16 same-frame visual stamps with overflow coalescing into the final slot. Middle/High/Ultra keep the same buffer size and can spend extra mesh/carve cadence elsewhere; shader stamp count never scales by growing the buffer.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is correctness under fault pressure: no out-of-bounds shader stamp iteration if queue metadata is ever desynced from the fixed command buffer capacity.

## Decision 123: Fresh Generated Volumes Must Bind Their Prewarmed Component Before Mesh Publication

Problem: Initial cave generation builds mesh data before `ConfigureRuntimeData`, so `VoxelPipelineData.SourceVolume` was null during `ApplyVolumeMeshAsync`. Even when the spawned pooled prefab already had `HectonVoxelVolume`, mesh publication could enter legacy null-volume fallback branches that use `TryGetComponent` and can call `AddComponent` if the prefab is malformed.
Solution: Added `TryBindGeneratedVolumeForMeshPublication` and call it in both fresh generation entry points immediately after `SpawnVolume()` and before `ApplyVolumeMeshAsync`. The helper requires an existing `HectonVoxelVolume`, writes it into `VoxelPipelineData.SourceVolume`, records the current runtime stamp, and fails closed by despawning the volume if the component is missing.
Rejected Alternatives: Waiting for `ConfigureRuntimeData` was rejected because mesh/collider publication already happens before that call. Letting `BuildWeldedMeshNative` use legacy fallback was rejected because runtime generation must use prewarmed components, not patch malformed prefabs with component creation. Moving `ConfigureRuntimeData` before mesh upload was rejected because it would publish runtime data before mesh application succeeds.
Scalability potential: Low fails closed when prefab prewarm is broken instead of creating components in the frame. Middle/High/Ultra use the same cached component route; quality can increase mesh upload cadence, but cannot change component ownership.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is hitch prevention under malformed pool prefabs: runtime generation no longer reaches null-volume `AddComponent` branches during mesh publication.

## Decision 124: Carve Cadence Proof Must Not Require Hardware Tier Inputs

Problem: The runtime carve drain had already moved to `GlobalQualityWeight`, but the debug proof method still accepted `HectonQualityTier` and mapped it through `ResolveQualityWeightFromTier`. `VoxelDeformationSmokeTester` also required `case HectonQualityTier.High` in source text, meaning the smoke test preserved an obsolete binary route.
Solution: Changed `DebugResolveQueuedCarveDrainBudget` to accept a continuous `float qualityWeight01`, removed `ResolveQualityWeightFromTier`, and updated the smoke tester to validate monotonic continuous budgets at 0.0, 0.24, 0.5, 0.78, and 1.0. The static source contract now rejects the tier adapter and tier-debug signature.
Rejected Alternatives: Keeping the tier adapter "for tests only" was rejected because tests become the next source of production regressions. Hardcoding exact middle-tier budget values was rejected because continuous curves can change while preserving monotonic low-to-overkill semantics. Removing the smoke coverage entirely was rejected because the carve queue still needs packet and cadence proof.
Scalability potential: Low/weak devices still resolve to the minimum drain cadence. Middle/High/Ultra progress continuously through the same curve without hardware-tier branches, while queue capacities and save DTOs stay unchanged.
Hardware Impact: Measured microseconds saved: 0. Expected i3/MX350 impact is regression prevention: future carve cadence edits will be tested against continuous quality behavior instead of reviving tier switches.
