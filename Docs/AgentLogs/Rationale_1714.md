# Rationale 1714 - Fauna Rigging & Material Clone Purifier

## Initialization

Problem: RB-007 material clone route exists in `FaunaBrain.cs`, with runtime `new Material(sourceMaterial)` and shared-material list mutation on fauna spawn.
Solution: Replace cloned-material ownership with shared authored material references plus one cold `MaterialPropertyBlock` per fauna renderer. Per-creature damage and biolum presentation becomes scalar/color shader parameters written in visual sync only.
Rejected Alternatives: Keeping per-creature material clones is rejected because it breaks batching and scales draw-state churn with population. Moving material clones to a pool is rejected because pooled clones still create unique material identities and do not restore shared-material batching.
Scalability potential: Low uses shared base material and sparse MPB cadence; middle updates damage/glow at reduced cadence; high updates full presentation; ultra spends saved CPU/GPU state churn on richer shader response while keeping material identity shared.
Hardware Impact: On i3/MX350, removing per-creature material clones avoids material object pressure, SetPass expansion, and spawn-time GC/native churn. Runtime estimate target is sub-10 microseconds per active fauna visual sync for MPB scalar refresh when cadence allows.

Problem: The prompt referenced a retired standalone domain map.
Solution: Use the domain explicitly granted by `AGENT_PROMPT id="1714"` plus the active domain index and coverage matrix.
Rejected Alternatives: Searching unrelated lore domain files is rejected because it does not define code ownership for this batch.
Scalability potential: Scope control prevents cross-domain churn while keeping fauna/rendering fixes direct.
Hardware Impact: No runtime impact; avoids architectural churn.

## RB-007 Material Clone Purge

Problem: `FaunaBrain.cs` owned per-creature runtime materials through clone lists, mutated them for biolum/death/corpse/decay/mutation state, restored shared slots on destroy, and destroyed clone instances. This created one material identity per creature.
Solution: Deleted `EnsureFaunaPresentationMaterials()` and the clone/restore lists. Added `_authoredFaunaMaterial` as an optional shared material reference and one cold `MaterialPropertyBlock` initialized in `Awake()`. Existing shader state now flows through `Renderer.GetPropertyBlock()` / `SetPropertyBlock()` during visual sync.
Rejected Alternatives: Pooling cloned materials was rejected because pooled material instances still fracture batching. Mutating `renderer.material` was rejected because it creates material instances. Mutating `sharedMaterial` state was rejected because it would leak one creature's damage/glow state to the whole species.
Scalability potential: Low quality gets base shared material with damage/glow intensity suppressed by smooth `GlobalQualityWeight`; middle uses reduced scalar amplitude; high gets full damage and aggression glow; ultra can spend the saved material-state churn on richer shader branches using the same shared material identity.
Hardware Impact: i3/MX350 avoids spawn-time material clone allocation and native material lifetime churn. Static token proof: no `new Material(sourceMaterial)`, `GetSharedMaterials`, or `SetSharedMaterials` remain in `FaunaBrain.cs`.

## Offline Rig Builder

Problem: Manual fauna rigging and swarm CPU skinning do not scale to dense creature populations, and runtime ricochet tests cannot afford complex visual mesh raycasts.
Solution: Extended the existing `AbyssalAnatomyStudio1610.cs` / `FaunaOfflineRigger1610` owner instead of keeping a duplicate 1714 editor window. The owner route keeps 96-bone clamping, Burst skinning, VAT baking, metadata, and now bakes armor rigidity to vertex alpha while preserving source RGB lanes.
Rejected Alternatives: Runtime mesh deformation was rejected because gameplay builds must load finished assets. A standalone `FaunaRigBuilder1714` was rejected after code archaeology because the 1610 owner already owned fauna rig/VAT authoring.
Scalability potential: Low uses VAT swarms and coarse bone counts; middle uses moderate rigged predators; high uses full 96-bone large fauna with richer shader damage; ultra increases VAT species density and shader response without CPU skinning per fish.
Hardware Impact: i3/MX350 avoids 5000-fish CPU skinning. The offline bake spends editor CPU once, then runtime uses MeshRenderer/VAT assets and baked vertex alpha armor masks.

## Compaction Fence Audit

Problem: The prompt required `FaunaPresentationStateDTO` and `_compactionFence` handling, but no such DTO exists in the target runtime code.
Solution: Audited existing fauna/rendering DataVault routes. Corpse/kinematics code checks `vault == null || vault.IsCompactionFenceActive` before handle resolution and output reads. No new presentation DataVault pointer route was introduced.
Rejected Alternatives: Inventing `TryAcquireReadLock` was rejected because `IDataVault` exposes `IsCompactionFenceActive` and write-lock release in this codebase, not the requested read-lock API. Adding a fake DTO would create unowned global surface.
Scalability potential: Low through ultra all fail closed during compaction and reuse last visual state instead of dereferencing stale native memory.
Hardware Impact: No added runtime lock contention on i3/MX350; presentation skips instead of blocking during compaction.

## Build Gate

Problem: Compilation verification is required, but host CPU load was above the allowed threshold and an existing `dotnet` process was active.
Solution: Did not launch `dotnet build`. Static scans continued. The latest gate showed CPU 69 percent and no active `dotnet`/`csc`; CPU remains above the 50 percent gate. Build gate remains pending until CPU drops below 50 percent and no compiler process is active.
Rejected Alternatives: Launching a build under host load was rejected by batch rule and would contaminate proof.
Scalability potential: No runtime effect.
Hardware Impact: Protects local machine from agent-induced compile contention.

## Proof Artifact

Problem: The CTO-facing proof cannot live only in chat.
Solution: Keep final proof in source code, static scans, and `Docs/AgentLogs/LOG_1714.md`. The earlier generated JSON proof was removed after the latest no-JSON directive because it became stale after additional source edits.
Rejected Alternatives: A prose-only final answer was rejected because batch protocol requires disk proof.
Scalability potential: No runtime effect; proof artifact supports integration triage.
Hardware Impact: No runtime impact.

## APEX Verification Patch

Problem: The first MPB rewrite queued shader updates into `LateFrameTick`, but `FaunaBrain` did not lifecycle-register `ILateFrameTickable`; the existing late-frame registration route was named and triggered by corpse sink. This created a visual-sync dependency on an unrelated corpse path.
Solution: Added lifecycle-owned fauna late-frame registration in `OnEnable` and removal in `OnDisable`/`OnDestroy`. The corpse sink wrapper now resolves through the same lifecycle-owned route. Presentation setters remain pure scalar queues; only `LateFrameTick` flushes MPB to the renderer.
Rejected Alternatives: Registering late-frame from `ApplyFaunaPresentationShaderState()` was rejected because that method can be called from high-frequency simulation code and would introduce a hot `GlobalRegistry` registration path. Immediate renderer writes from `Tick` were rejected because presentation must remain post-simulation.
Scalability potential: Low, middle, high, and ultra tiers now share one deterministic phase route. Low quality still suppresses subtle shader amplitude through continuous `GlobalQualityWeight`; high/ultra get richer MPB response without moving authority out of AI/physics.
Hardware Impact: On i3/MX350 this replaces possible missed visual flushes with one cold registry registration per fauna lifecycle. No `GlobalRegistry.Get<T>()`, `GetComponent()`, material clone, list creation, dictionary creation, or string formatting token exists in the scanned `Tick`, `FixedTick`, `LateFrameTick`, or fauna MPB flush methods.

Problem: The fauna generator owner still used legacy mesh vertex extraction and `new Material(sourceMaterial)` in its VAT material asset route.
Solution: Replaced skinned/VAT source vertex reads with `Mesh.AcquireReadOnlyMeshData` plus temporary `NativeArray<Vector3>` copies into `NativeArray<float3>`. VAT material generation now uses `Object.Instantiate(sourceMaterial)`, `FaunaRigMetrics1610` has explicit 96-byte sequential layout, and the owner rigger validates DTO alignment with `UnsafeUtility.SizeOf<T>()`.
Rejected Alternatives: Keeping Mesh legacy arrays was rejected because the prompt explicitly called for MeshData/NativeArray discipline. Keeping editor `new Material(sourceMaterial)` was rejected because the fauna generator domain needs grep-clean proof for the material-clone blocker.
Scalability potential: Low devices consume baked rig/VAT assets only. Middle/high/ultra can author denser rigs and VAT frames offline without adding runtime mesh deformation or per-creature material identity.
Hardware Impact: Runtime impact is zero because the builder is Editor-only. Editor bake memory pressure is lower and more deterministic for large meshes; generated assets remain shared-material driven.

Problem: Full `dotnet build` remains forbidden by local machine state, and standalone Roslyn parse failed because PowerShell could not resolve Roslyn's `System.Memory, Version=4.0.1.2` dependency.
Solution: Performed static source proof instead: targeted token scans, method-body hot path scans, `git diff --check`, corrected `.meta` orphan scan, and CPU/compiler process gates. The Roslyn attempt is explicitly not counted as passing proof.
Rejected Alternatives: Launching another build or compiler process under 100 percent CPU and active external `dotnet` processes was rejected by compilation throttling rules.
Scalability potential: No runtime effect.
Hardware Impact: Avoided agent-induced compiler contention on the workstation.

## Deep Polish And Lock Flattening

Problem: `LateFrameTick()` still did frame-side bookkeeping before checking whether any visual/audio/despawn payload was pending.
Solution: Added `HasQueuedFaunaLateFrameWork()` and made it the first branch in `LateFrameTick()`. The predicate reads existing booleans and references only; it does not query the scene, registry, renderer, or allocate memory.
Rejected Alternatives: Unregistering and re-registering late-frame per dirty flag was rejected because that would move dispatcher mutations toward hot presentation paths. Keeping unconditional visual-sync work was rejected because thousands of data-only fauna should not pay a no-op late-frame tax.
Scalability potential: Low-tier devices skip idle fauna presentation entirely. Middle-tier keeps deterministic visual phase. High and ultra retain full MPB/audio/haptic payloads when dirty without changing simulation authority.
Hardware Impact: On i3/MX350, idle fauna avoid late-frame interpolation and flush calls. Static proof: scanned method contains no `GlobalRegistry.Get<T>()`, `GetComponent`, material clone, collection allocation, formatting, or wait token.

Problem: `StressDrivenSpawnDirector.RefreshColdInputs()` held the fauna director input write lock while resolving global quality, thermal pressure, weather, and macro ecosystem inputs. This created avoidable DataVault lock expansion.
Solution: Rebuilt the method into snapshot-read, compute, then one-slot write. `TrySetTuning()` now resolves quality before acquiring the write lock. Every write lock block remains wrapped in `try/finally` and writes only one native DTO slot.
Rejected Alternatives: Keeping helper calls inside the lock was rejected because helper reads may refresh borrowed handles and grow lock scope. Splitting to multiple write locks was rejected because one owner fact should publish through one route.
Scalability potential: Low through ultra share the same deterministic input route; high-end extra presentation or ecosystem detail does not expand DataVault write-lock time.
Hardware Impact: On i3/MX350, write-lock occupancy is reduced to a direct native assignment. Static proof: four write-lock sites in the audited file have matching `ReleaseWriteLock` in `finally`; no nested write lock was introduced.

Problem: The offline rigger had two quality risks: tail vertices could underweight the terminal bone, and armor alpha baking discarded authored vertex RGB. VAT texture dimensions also needed a hard allocation guard.
Solution: Inserted the terminal bone as an explicit skinning candidate, preserved source RGB while overwriting only `Color32.a`, and rejected VAT payloads above `SystemInfo.maxTextureSize` or `int.MaxValue` pixel count before allocating `NativeArray<float4>` or `Texture2D`.
Rejected Alternatives: Accepting segment-only weights was rejected because the last bone would be a bind-pose passenger. Overwriting all vertex colors was rejected because it destroys artist masks. Letting Texture2D construction fail late was rejected because editor tools should fail before heavy memory allocation.
Scalability potential: Low devices consume smaller rigs and VATs with stable memory. Middle/high/ultra can bake larger assets within GPU limits and retain richer vertex-color authoring.
Hardware Impact: Runtime impact is zero because the path is Editor-only. Generated meshes are more stable for tail deformation and combat mask lookup without adding runtime cost.

Problem: Keeping `FaunaRigBuilder1714` after discovering the first-party `FaunaOfflineRigger1610` owner would violate the duplication mandate.
Solution: Deleted the standalone 1714 `.cs` and `.meta` together, then moved the 1714 behavior into `AbyssalAnatomyStudio1610.cs`.
Rejected Alternatives: Leaving both tools was rejected because two authoring routes would drift on bone limits, VAT material contracts, and armor channel semantics.
Scalability potential: One fauna authoring owner means low/middle/high/ultra assets share the same validation gates and output paths.
Hardware Impact: Runtime impact is zero. Editor impact is lower maintenance cost and no duplicate menu/window path.

## Rendering Stall And Editor Leak Polish

Problem: `GpuScatterLodManager.CompletePendingVisibleCountReadbackForRelease()` still used `AsyncGPUReadbackRequest.WaitForCompletion()` when optional visible-count diagnostics were pending during release.
Solution: Replaced the synchronous wait with a cached `Action<AsyncGPUReadbackRequest>` callback. Release sets `_visibleCountReadbackReleaseRequested`; the persistent readback `NativeArray<uint>` is disposed only after the callback confirms GPU completion.
Rejected Alternatives: Disposing the NativeArray immediately was rejected because `RequestIntoNativeArray` may still own it. Creating a new global release helper was rejected because the existing manager owns the readback lifecycle. Keeping `WaitForCompletion()` was rejected because release-path stalls still violate the no synchronous wait rule.
Scalability potential: Low and middle devices avoid a disable/destroy hitch if diagnostics were enabled. High and ultra can keep optional readback diagnostics without compromising frame release behavior.
Hardware Impact: On i3/MX350, release no longer blocks on GPU readback completion. The callback delegate is allocated once in `Awake()`, not per readback.

Problem: `AbyssalAnatomyStudio1610` instantiated temporary rig/VAT meshes before all metadata and prefab save gates had passed.
Solution: Tracked `riggedMesh` and `vatMesh` across the bake `try/finally` blocks and destroy them if no AssetDatabase path owns the instance.
Rejected Alternatives: Letting Unity clean editor temp meshes later was rejected because failed batch bakes can accumulate native mesh objects. Wrapping this in a new utility was rejected because the existing rigger owner can handle its own temp ownership.
Scalability potential: Low through ultra content bakes now fail without editor native-object buildup.
Hardware Impact: Runtime impact is zero. Editor memory pressure is lower during repeated rejected bake attempts.

Problem: The fauna editor owner still had direct `JobHandle.Complete()` calls in offline fuzzer/skinning/VAT jobs.
Solution: Routed those cold editor completions through `DispatcherJobFence.TryComplete(forceComplete: true)` via `CompleteEditorBakeJobCold()`.
Rejected Alternatives: Leaving direct `.Complete()` calls was rejected because the proof scanner should not need to infer editor-only intent from raw call sites. Making these jobs asynchronous was rejected because the editor bake must produce deterministic assets before continuing to AssetDatabase writes.
Scalability potential: Runtime tiers are unaffected; editor bake flow now follows the same fence abstraction as runtime systems.
Hardware Impact: No runtime impact. Editor completion behavior is unchanged, but source-level sync policy is now explicit and first-party.

## Shader Global Write-Lock Narrowing

Problem: `GlobalShaderDispatcher.LateFrameTick()` computed sector phase, AUP offset, resolution state, and hazard pulse while holding `ShaderGlobalStateMutationGuardMask`.
Solution: Hoisted those calculations before the mutation guard and kept the guarded block to slot validation, inline fixed-slot kernel write, slot copy, DTO readback, and `finally` release.
Rejected Alternatives: Splitting the shader global publish into several locks was rejected because one visual-sync owner should publish one coherent slot snapshot. Moving all shader upload work under the guard was rejected because GPU/global shader calls are outside DataVault ownership.
Scalability potential: Low through ultra keep identical shader truth; the lock window no longer grows with quality/resolution/hazard resolver work.
Hardware Impact: On i3/MX350 the write guard now excludes floating-origin, scaler, and radiation-signal math. No runtime behavior change; lower lock occupancy only.

## MeshData Completion And Crab MPB Binding

Problem: `AbyssalAnatomyStudio1610` still used managed `List<>` mesh extraction in armor-mask baking and mesh hashing.
Solution: Converted both paths to `Mesh.AcquireReadOnlyMeshData` plus bounded `NativeArray<float3>` / `NativeArray<Color32>` scratch. Source vertex RGB remains preserved while armor rigidity owns alpha.
Rejected Alternatives: Keeping `List<>` was rejected because large imported leviathan meshes should not pay managed resize/copy risk in the offline rigger. Unsafe raw stream reads were rejected here because Unity's `MeshData.GetVertices/GetColors` handles imported vertex format conversion.
Scalability potential: Low through ultra content bakes use one deterministic mesh read route; bigger high-tier assets do not add managed list pressure.
Hardware Impact: Runtime impact is zero. Editor bake memory moves from managed list copies to short-lived native scratch.

Problem: `ProceduralCrabLegIKRuntime` bound indirect draw buffers by mutating the shared crab material, and its visual upload path could lazy-create graphics buffers on first active render.
Solution: Added one cold `MaterialPropertyBlock`, bound body/joint buffers through `RenderParams.matProps`, and moved crab `GraphicsBuffer` creation into lifecycle cold setup.
Rejected Alternatives: Per-runtime material clones were rejected by RB-007. Leaving shared material mutation was rejected because multiple crab runtimes could overwrite each other's buffer binding. Keeping first-active-frame buffer creation in `UploadAndRenderIndirect()` was rejected because VISUAL_SYNC must not allocate.
Scalability potential: Multiple crab lanes can share one material asset while carrying per-runtime buffer bindings.
Hardware Impact: Removes material-state drift without adding hot allocations; MPB is allocated once in lifecycle setup.

## Leviathan Visual Cold Allocation And Readback Ownership

Problem: `FaunaKinematicsRuntime.UploadBonesToGpu()` and `PublishLeviathanIkGlobals()` could allocate/recreate `GraphicsBuffer` objects from visual upload routes reached by `LateFrameTick()`.
Solution: Moved bone and IK buffer creation into `EnsureVisualGpuBuffersCold()` called from `Awake()`/`OnEnable()`. Upload methods now only validate existing buffers, write mapped memory, and fail closed if buffers are unavailable.
Rejected Alternatives: Keeping lazy allocation in visual upload was rejected because VISUAL_SYNC must not allocate. Creating a separate GPU allocator was rejected because the runtime already owns these buffers.
Scalability potential: Low devices avoid first-visible-frame allocation spikes; middle/high/ultra keep the same double-buffered GPU path without changing gameplay authority.
Hardware Impact: On i3/MX350, leviathan visual upload no longer performs native buffer creation inside the late-frame path.

Problem: `TryCopyTerrainSdfLeaseToSnapshot()` held `TerrainSdfSnapshotMutationGuardMask` while copying SDF bytes through a managed per-element loop.
Solution: Replaced the loop with one bounds-checked `UnsafeUtility.MemCpy()` from the read-only lease pointer into the owned snapshot buffer. The guard still wraps only the native snapshot copy.
Rejected Alternatives: Guessing a new SDF owner or adding another DataVault route was rejected. Leaving the loop was rejected because lock time scaled with C# indexer overhead.
Scalability potential: Low through ultra share the same deterministic terrain-hugging snapshot; larger high-tier SDF payloads pay native copy cost instead of managed loop cost.
Hardware Impact: On i3/MX350, snapshot guard occupancy drops for dense SDF payloads without changing solver math.

Problem: `GpuScatterLodManager` no longer blocked on pending visible-count readback, but `ReleaseGpuBuffers()` could still release `_argsBuffer` while the async readback referenced it.
Solution: Added `_visibleCountReadbackArgsReleaseDeferred`; release now keeps `_argsBuffer` alive until the readback callback disposes native readback data, then releases the deferred source buffer.
Rejected Alternatives: Reintroducing `WaitForCompletion()` was rejected because it stalls the main thread. Deferring every GPU buffer was rejected because only `_argsBuffer` is the readback source.
Scalability potential: Low and middle devices avoid release hitches and readback/source-buffer races. High and ultra keep optional diagnostics without blocking release.
Hardware Impact: On i3/MX350, scatter diagnostics release no longer blocks and no longer frees the source buffer under an active GPU readback.

## Scatter Diagnostics Repair And Material Variant Cache

Problem: `GpuScatterLodManager.FlushVisibleCountReadbackRepairSlow()` cleared the repair flag when the readback NativeArray was missing, but did not recreate the buffer.
Solution: Routed the missing-buffer branch through `EnsureVisibleCountReadbackDataCold()`. If allocation cannot happen because a readback is pending, the repair request stays alive.
Rejected Alternatives: Allocating from `LateFrameTick()` was rejected because diagnostics repair must stay in slow/cold cadence. Disabling readback permanently was rejected because the manager already owns a repair path.
Scalability potential: Low through ultra diagnostics can recover after buffer loss without frame-path allocation.
Hardware Impact: On i3/MX350, optional diagnostics avoid a silent dead lane and still allocate only in slow repair.

Problem: The scatter material variant cache fields were written but hot render validation still queried the material keyword every frame and could record duplicate invalid-material black-box events.
Solution: `IsRenderMaterialVariantValid()` now returns cached validity for the same material instance id. `Render()` uses the cached check after the frame-level validator has already recorded failures.
Rejected Alternatives: Removing validation entirely was rejected because invalid indirect variants should still fail closed. Adding a material wrapper was rejected because the manager already has cache state.
Scalability potential: Low devices avoid repeated keyword queries; high and ultra keep identical material correctness gates.
Hardware Impact: Reduces hot render validation work to an integer id comparison for stable material assets.

## Presentation Dirty Correctness

Problem: The fauna MPB queue-side early-out compared pending scalar inputs but not the derived current damage blend, emission strength, or genetic mutation mask. Health snapshots and genome overlays could therefore leave renderer property blocks stale until a later full presentation tick.
Solution: `ApplyFaunaPresentationShaderState()` now computes and compares the derived genetic mask, mutation hue/twitch, damage blend, emission strength, and smoothed quality before suppressing a queue update. `QueueCurrentFaunaPresentationShaderState()` routes hit flash, hibernation health snapshots, and runtime genome/ecosystem overlays into the same LateFrame visual-sync queue.
Rejected Alternatives: Writing MPB immediately from damage/genome setters was rejected because renderer writes belong in visual sync. Adding a new presentation service was rejected because `FaunaBrain` already owns these shader scalars and late-frame registration.
Scalability potential: Low devices still get one shared material and coalesced MPB writes; middle/high/ultra can show damage, mutation, and glow changes immediately after state transfer without material identity splits.
Hardware Impact: On i3/MX350 this adds only scalar comparisons to the existing queue path and prevents extra catch-up writes. No allocation, registry lookup, component lookup, or material clone is introduced.

## Pool Hazard Identity And Cold Infection Color Cache

Problem: Pooled fauna could retain the previous infection hazard source id after unregister, and infection visual restore still depended on material color reads near LateFrame. A spawn health reset could also happen after earlier presentation queue state, leaving damage blend stale on reused instances.
Solution: `ClearInfectionHazardRegistration()` now clears `_infectionHazardSourceId` after unregister. `InitializeFaunaPresentationPropertyBlock()` captures authored material colors once in the cold setup path, and `FlushEcosystemInfectionVisuals()` restores colors from cached fields. `OnSpawn()` queues current presentation after `_currentHealth = _maxHealth`.
Rejected Alternatives: Reading `renderer.sharedMaterial` during infection flush was rejected because visual sync should use cached state. Immediate renderer writes from spawn or infection setters were rejected because the existing LateFrame MPB queue already owns presentation transfer. Creating an infection presentation service was rejected because this is a `FaunaBrain` partial ownership issue.
Scalability potential: Low devices avoid per-frame material property queries and stale pooled hazards. Middle/high/ultra keep richer infection/damage shader response through the same shared-material MPB route.
Hardware Impact: On i3/MX350 this removes LateFrame material color queries and prevents hazard registry source-id drift without adding hot allocations or scene lookups.

## External Director Input Lock Flattening

Problem: `StressDrivenSpawnDirector.PublishDirectorInput()` still performed AUP packing, direction normalization, finite checks, saturates, and transition clamping while holding the director input write lock.
Solution: Pre-sanitize the external payload before `TryAcquireWriteLock()`. The locked block now validates the native slot, reads one DTO, applies already-packed values, writes one DTO, and releases in `finally`.
Rejected Alternatives: Splitting the input publish into separate lock/read/write phases was rejected because it would open a stale-read race. Adding another external input DTO was rejected because the existing director input slot already owns this fact.
Scalability potential: Low through ultra keep the same director input semantics; high-frequency external stress pushes no longer expand lock occupancy with math.
Hardware Impact: On i3/MX350 the write lock excludes floating-point normalization and finite checks. Static proof: `PUBLISH_LOCK_FINALLY_OK` and `PUBLISH_LOCK_HEAVY_TOKENS_CLEAR`.

## Scatter Vault Publish Remap Hoist

Problem: `AbyssalScatterBrgDataVaultBootstrap` held matrix and metadata DataVault write locks while applying the quality-index remap per element.
Solution: Apply the quality map once in `ApplyQualityMapCold()` before acquiring write locks. `TryWriteMatricesCold()` and `TryWriteMetadataCold()` now use contiguous `NativeArray<T>.Copy(...)` inside the guarded region and release in `finally`.
Rejected Alternatives: Keeping indexed writes under the lock was rejected because remap math and indirect source loads are not Vault ownership work. Adding a new scatter publisher was rejected because the bootstrapper already owns BRG payload import.
Scalability potential: Low devices get shorter load-time Vault lock windows; high and ultra scatter payloads can keep quality-sorted data without expanding guarded publish time.
Hardware Impact: On i3/MX350 this moves remap loops outside the write lock and leaves only native memory copy inside the guard. Static proof: both scatter write methods report `LOCK_FINALLY_OK` and `HEAVY_TOKENS_CLEAR`.

## UberNoir Telemetry Lock Flattening

Problem: `HectonUberNoirRuntimeBridge.PushBlackBox()` held the telemetry DataVault write lock while computing quality byte, bucketed stress values, state hash, and saturated telemetry fields.
Solution: Build `UberNoirShaderTelemetryEntry` before `TryAcquireTelemetryWriteBuffer()`. The locked region now writes one ring entry, advances `_telemetryCursor`, and releases in `finally`.
Rejected Alternatives: Dropping telemetry was rejected because black-box visibility is required. Splitting cursor ownership out of the runtime was rejected because this bridge already owns the telemetry ring cursor.
Scalability potential: Low devices avoid extra math inside graphics telemetry locks; high and ultra keep identical black-box detail without increasing contention.
Hardware Impact: On i3/MX350 this removes hash/bucket/saturate work from a write lock. Static proof: `PUSHBLACKBOX_LOCK_FINALLY_OK` and `PUSHBLACKBOX_LOCK_HEAVY_TOKENS_CLEAR`.

## DRS And Shader Guard Flattening

Problem: `HectonBilateralDrsUpscalerRuntime.ScheduleOwnerSimulation()` held the parameters write lock while reading tuning/profile state and executing the upscaler parameter job inline. The job also used unsafe pointer writes and safety suppression to push one DTO into a `NativeArray`.
Solution: Compute all tuning/profile/mock/quality inputs before the lock, run the scalar job with value outputs, validate `HasLastParameters`, then acquire the parameters write lock only to assign one DTO and release in `finally`.
Rejected Alternatives: Keeping optional `NativeArray` output as the only job result was rejected because it forced either a lock around job execution or unsafe write plumbing. Scheduling a tiny same-frame job was rejected because the owner already computes one scalar DTO and a same-frame schedule/readback would add dispatcher overhead without parallel benefit.
Scalability potential: Low, middle, high, and ultra all keep the same DRS truth route. Quality can scale resolution and visual cadence, but write-lock time remains one native assignment.
Hardware Impact: On i3/MX350 the DRS parameters lock no longer includes profile reads, service queries, or scaler math. Static proof: `DRS_SIM_LOCK_AFTER_EXECUTE_OK`, `DRS_SIM_LOCK_HEAVY_TOKENS_CLEAR`, and `DRS_KERNEL_VALUE_OUTPUT_OK`.

Problem: `GlobalShaderDispatcher.LateFrameTick()` and thermal packing held DataVault-style mutation/read guards while building mock shader payloads and filtering thermal source math.
Solution: Build mock global slots before the shader mutation guard and copy fixed slots under guard. Thermal source guards now copy bounded center/temperature/lifetime values into stack spans only; finite checks, decay, intensity calculation, and fallback mock-slot writes happen after guard release.
Rejected Alternatives: Splitting shader global publication into multiple guard sections was rejected because one visual-sync owner should publish one coherent slot snapshot. Leaving math inside the guard was rejected because visual quality scaling and thermal filtering are not lock ownership work.
Scalability potential: Low devices get shorter visual-sync guard windows. Middle, high, and ultra can increase thermal/light richness without stretching the guarded copy section.
Hardware Impact: On i3/MX350 the shader mutation guard excludes mock payload construction, and the thermal read guard excludes temperature/lifetime math. Static proof: `GSD_SHADER_GUARD_HEAVY_TOKENS_CLEAR` and `GSD_THERMAL_GUARD_HEAVY_TOKENS_CLEAR`.

## Physiology Signal Guard Hoist

Problem: After the mock/thermal cleanup, `GlobalShaderDispatcher.LateFrameTick()` still processed `SignalBus<PhysiologyStateSignal>.GetFrameSnapshot()` and looped over physiology signals while holding `ShaderGlobalStateMutationGuardMask`.
Solution: Added a prepared physiology payload built before the guard. Under the guard, the dispatcher only reads the existing decompression/gas slots, applies prepared values, writes two slots, and releases. The prepared payload has an explicit 56-byte sequential layout and is checked through `UnsafeUtility.SizeOf<T>()`.
Rejected Alternatives: A second global physiology manager was rejected because `GlobalShaderDispatcher` already owns visual-sync shader globals. Processing signals under the guard was rejected because signal iteration is not DataVault ownership work.
Scalability potential: Low devices get a shorter shader-global guard window. Middle, high, and ultra keep richer physiology visual response without increasing guarded lock work.
Hardware Impact: On i3/MX350 the shader mutation guard no longer includes signal snapshot access or per-signal iteration. Static proof: `GSD_MAIN_GUARD_SIGNAL_AND_LOOP_CLEAR` and `GSD_PREPARED_PHYSIOLOGY_LAYOUT_GATE_OK`.

## Shader Telemetry Guard Copy-Only Pass

Problem: `GlobalShaderDispatcher.RecordTelemetry()` still computed telemetry cursor wrap, frame increment, slot index, and `float4` payload while holding `ShaderGlobalStateMutationGuardMask`.
Solution: Precompute cursor, next cursor, frame, slot, and telemetry entry before acquiring the guard. The guarded section now validates the native slot array, writes one `float4`, assigns the prepared scalar state, and releases in `finally`.
Rejected Alternatives: Removing telemetry was rejected because the 300-frame shader black-box is required. Keeping cursor math under guard was rejected because it is not native slot ownership work.
Scalability potential: Low devices get shorter shader telemetry guard time. Middle, high, and ultra keep identical black-box detail without expanding the guarded section.
Hardware Impact: On i3/MX350 the shader telemetry guard excludes wrap/frame/payload construction. Static proof: `GSD_RECORD_TELEMETRY_GUARD_COPY_ONLY_OK`.

## Shader Slot Bridge Handle-Only Prep

Problem: `HectonShaderGlobalDataVaultBridge.TryPrepareSlotsVault()` and `GlobalShaderDispatcher.TryResolvePreparedShaderGlobalSlots()` resolved `ShaderGlobalState` buffers while preparing cache state, outside the mutation guard that owns shader slot access.
Solution: Prep paths now cache only owned `VaultGenerationHandle<float4>` values. Buffer resolution and length validation stay in the guarded writer/locked resolver routes.
Rejected Alternatives: Allocating a new shader-slot service was rejected because the bridge and dispatcher already own this route. Keeping pre-lock buffer resolve was rejected because it violates the DataVault read/write lock discipline.
Scalability potential: Low through ultra keep the same shader-global route. The guard window is not widened; prep is now handle-only and deterministic.
Hardware Impact: On i3/MX350 this removes unguarded DataVault buffer resolution from shader global prep. Static proof: `BRIDGE_PREP_HANDLE_ONLY_OK` and `GSD_PREP_HANDLE_ONLY_OK`.

## Shader Slot Guarded Capacity And Read-Copy Discipline

Problem: Handle-only shader-slot prep removed unguarded buffer resolution, but it also allowed a cached or existing `ShaderGlobalState` handle to be accepted before proving the native buffer still had the full shared slot capacity. The editor/dump reader also returned a `ReadOnly NativeArray` and let callers read DataVault-backed slots after the helper had no lock ownership.
Solution: `GlobalShaderDispatcher` and `HectonShaderGlobalDataVaultBridge` now validate each newly acquired shader-slot handle under `ShaderGlobalStateMutationGuardMask`, cache a validated flag, and invalidate cache state if a guarded resolve fails later. `TryReadCachedShaderGlobalSlots()` was removed; editor tuning/flow and telemetry dump now copy required slots into stack spans inside a strict guard/finally block.
Rejected Alternatives: Validating the buffer every frame was rejected because it adds redundant guard traffic in `LateFrameTick()`. Returning `NativeArray.ReadOnly` to callers was rejected because it extends DataVault reads outside the guarded ownership window. Adding a new shader-slot owner was rejected because the dispatcher/bridge already own this route.
Scalability potential: Low devices avoid repeated validation locks after the handle is proven. Middle, high, and ultra keep the same shader global layout and can increase visual richness without stale slot-capacity failures.
Hardware Impact: On i3/MX350 this keeps hot prep handle-only after one guarded validation, while diagnostic/editor reads copy only 1, 7, or 300 `float4` slots under guard. Static proof: `GSD_COPY_READ_LOCK_DISCIPLINE_OK`, `GSD_GUARDED_SLOT_VALIDATOR_OK`, and `BRIDGE_GUARDED_SLOT_VALIDATOR_OK`.
