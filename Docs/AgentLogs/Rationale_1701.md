PROMPT IDENTIFIED: 1701
STATUS: SOURCE VERIFIED / BUILD NOT LAUNCHED BY APEX THROTTLE

## Decision Log

Problem: Task state files were absent at session start.
Solution: Created fresh `Docs/Tasks/Status_1701.md` and `Docs/AgentLogs/Rationale_1701.md` before implementation.
Rejected Alternatives: Reading archived batch status would import stale decisions from another run.
Scalability potential: Disk-only task memory has no runtime impact and prevents context-loss drift across all device lanes.
Hardware Impact: 0 runtime us; no i3/MX350 frame cost.

Problem: 1701 touches survival truth, UI presentation, DataVault mutation, SignalBus publication, and DTO layout.
Solution: Selected eight registry mandates covering oxygen/pressure survival, ARM64 DTO layout, zero-GC, native/DataVault ownership, execution phases, cold registry DI, typed SignalBus lanes, and zero-GC UI text streaming.
Rejected Alternatives: Reading generic UI/survival prose alone would miss DataVault lock and explicit SignalBus payload constraints; reading all 35 mandates would waste context without increasing task-specific proof.
Scalability potential: Low lane preserves deterministic oxygen truth and readable warning UI; middle keeps normal cadence; high/ultra spend only presentation budget on richer vignette/visor effects from the same scalar state.
Hardware Impact: Prevents hot registry polling, runtime mesh allocation, and string allocation on i3/MX350; estimated static risk reduction, no profiler-backed microsecond claim yet.

Problem: The prompt names `Assets/_Project/Scripts/Gameplay/Survival/HectonSurvivalSystem.cs`, but that folder is absent.
Solution: Used recursive file discovery under `Assets/_Project/Scripts` and found the live class at `Assets/_Project/Scripts/HectonSurvivalSystem.cs`; kept scope to prompt-named class plus HUD, with Core/Physiology contracts only for authority integration.
Rejected Alternatives: Creating a new file under the missing prompt path would duplicate survival authority and violate one-owner doctrine.
Scalability potential: Existing owner route remains stable across all hardware lanes; no extra dispatcher lane or duplicate data owner.
Hardware Impact: Avoids duplicate per-frame survival work; estimated avoided cost is one extra managed/MonoBehaviour dispatch per frame, static-only.

Problem: HectonSurvivalSystem had no unmanaged hypoxia bridge and instant oxygen failure could bypass the required 4 s agony window.
Solution: Extended MetabolicStateDTO with RealO2, AgonyTimeRemaining, and IsInHypoxia; HectonSurvivalSystem writes row 0 through one DataVault write lock with compaction-fence checks and finally ReleaseWriteLock.
Rejected Alternatives: Adding a new hypoxia MonoBehaviour, publishing a new death signal, or mutating physiology from HUD would split authority and add dispatch cost.
Scalability potential: Low uses the same scalar and timer; middle/high/ultra can spend visual budget on richer visor effects from _HypoxiaSignal without changing simulation truth.
Hardware Impact: One 48-byte DTO write on slow tick; no heap allocation or extra hot registry route.

Problem: SHINOBU_323 suit integrity pressure math was isolated from oxygen drain.
Solution: Survival reads SuitIntegrityDTO through the shared Core physiology contract and applies a quadratic barotrauma oxygen multiplier, capped to hard safety limits.
Rejected Alternatives: Re-implementing suit pressure damage in Survival would duplicate SuitIntegrityRuntime and risk divergent pressure truth.
Scalability potential: All device lanes consume the same cheap scalar; ultra can amplify presentation without changing O2 math.
Hardware Impact: One read-only DataVault sample on slow tick after cached handle; retry is bounded to 30 frames until the buffer appears.

Problem: SuitHUDV4CanvasOverlay generated chevron Mesh data at runtime and displayed oxygen linearly.
Solution: Replaced runtime Mesh build with serialized _threatChevronStaticMesh and mapped real oxygen through math.smoothstep below 15%.
Rejected Alternatives: Keeping scratch arrays and Mesh.SetVertices in runtime bootstrap would keep RB-004 alive; string/TMP formatting changes were unnecessary because existing SetCharArray path is already preallocated.
Scalability potential: Low devices skip mesh construction; high/ultra keep the same static mesh and can increase shader/flicker fidelity from quality policy.
Hardware Impact: Removes runtime Mesh allocation path and its vertex/index array usage; steady-state remains instanced draw with cached MaterialPropertyBlock.

Problem: Late-created DataVault buffers could leave HUD and survival stuck on fallback because handles were only resolved during cold bind.
Solution: Added bounded frame-gated handle retries, then cached generation handles after success.
Rejected Alternatives: Per-frame GlobalRegistry polling or scene searches in LateFrameTick would violate hot-path rules.
Scalability potential: Weak devices pay at most a sparse handle query while buffers are absent; high-end devices get identical truth with no extra fidelity divergence.
Hardware Impact: Retry every 30 dispatcher frames until available; after bind, no recurring lookup.

Problem: A temporary separate suit-integrity Core contract file would have expanded assembly topology and left a new .meta surface.
Solution: Merged SuitIntegrityDTO authority into the existing Core physiology contract file and made the Physiology layout guard validate the shared contract size and 8-byte multiple explicitly.
Rejected Alternatives: Keeping a parallel local DTO or adding a new helper file would violate one fact/one owner and increase merge surface for other agents.
Scalability potential: Low/middle/high/ultra lanes all read the same 32-byte ABI; presentation fidelity can scale without changing save/runtime DTO truth.
Hardware Impact: 32-byte cache-local read remains unchanged; no additional hot allocation or lookup.

Problem: Survival-side direct writes to `_HypoxiaSignal` created a second shader-global writer beside the existing rendering bridge/dispatcher route.
Solution: Removed the survival hypoxia shader writer; survival now writes only MetabolicStateDTO truth, and ShinobuSensoryImpairmentRuntime merges that hypoxia state into its existing `PublishPhysiologyGasToxicity` VISUAL_SYNC route.
Rejected Alternatives: Calling `Shader.SetGlobalFloat` from survival LateFrameTick or adding a second bridge publisher would violate one-owner shader presentation.
Scalability potential: Weak devices get one scalar through the existing shader slot; high/ultra can amplify post effects from the same gas-toxicity vector without duplicating simulation.
Hardware Impact: Removes one potential global shader write from survival LateFrameTick; new sensory read is a cached read-only handle and one DTO copy.

Problem: Metabolic integration preserved `FlagHypoxia` but did not force-clear it when `IsInHypoxia` was false.
Solution: MetabolicIntegrationJob now derives the flag from the byte state every pass, eliminating stale visual hypoxia after recovery/disable.
Rejected Alternatives: Clearing only in HUD or shader code would hide stale simulation state instead of fixing the DTO contract.
Scalability potential: All lanes consume a coherent byte/flag pair; no extra presentation branching on device tier.
Hardware Impact: One branchless flag select-equivalent operation in the existing Burst job; no allocation.

Problem: The SHINOBU_323 barotrauma oxygen multiplier used `math.pow(damage01, 2f)` for a fixed quadratic curve.
Solution: Replaced the exponent helper with `damage01 * damage01`; the curve is mathematically identical after the existing saturate clamp.
Rejected Alternatives: Keeping `math.pow` would spend unnecessary scalar math in the survival slow tick; lookup tables would be overkill for one quadratic scalar.
Scalability potential: Low/middle devices get cheaper scalar math; high/ultra keep the same pressure-to-breathing drama and can spend saved budget on presentation.
Hardware Impact: One multiply replaces exponent helper per oxygen drain solve; no allocation, no extra branch.

Problem: The MetabolicStateDTO write lock still performed finite sanitization and lazy source-ID resolution inside the critical section.
Solution: Hoisted scalar sanitization and source-ID resolution before `TryAcquireWriteLock`; the locked block now validates the buffer and performs direct DTO field assignment only.
Rejected Alternatives: Leaving tiny math inside the lock would be acceptable functionally but weaker against compaction-fence stalls; adding a second staging buffer would duplicate state.
Scalability potential: Weak devices get shorter vault lock occupancy; middle/high/ultra retain identical hypoxia and oxygen truth.
Hardware Impact: Removes finite checks and lazy ID call from the write-lock window; no heap allocation or extra handle.

Problem: The requested in-memory Roslyn AST pass could not run cleanly in the current host.
Solution: Tried local `Assets/Plugins/Roslyn` parser path with explicit dependency loading; recorded the binding failure and kept validation to source gates instead of launching `dotnet build` while CPU was 100 and external Unity/dotnet processes were active.
Rejected Alternatives: Reporting a false AST pass, invoking MSBuild/project build under throttle violation, or writing generated parser artifacts to disk.
Scalability potential: No runtime effect; protects the shared workstation from parser/build contention while preserving honest verification state.
Hardware Impact: No game runtime cost; parser attempt ended without orphan parser process.

Problem: The KCC environment mock creates `MetabolicStateDTO` rows and would inherit zero-valued newly added oxygen fields.
Solution: Added explicit normoxic bridge defaults (`RealO2=1`, `AgonyTimeRemaining=0`, `IsInHypoxia=0`) to the existing object initializer.
Rejected Alternatives: Relying on current consumers to ignore the mock oxygen fields would make the ABI brittle for future systems.
Scalability potential: Low/middle/high/ultra lanes receive coherent mock metabolism state without creating a separate mock DTO.
Hardware Impact: Three direct stores in an existing Burst mock generation path; no allocation and no extra dispatch.

Problem: Survival reads/writes physiology bridge buffers only if their generation handles are owned by `SystemID.GameplayPlayer`.
Solution: Rechecked the producer runtimes: `ShinobuMetabolismRuntime` and `ShinobuSuitIntegrityRuntime` both use `OwnerSystem = SystemID.GameplayPlayer`; no filter weakening is required.
Rejected Alternatives: Accepting any owner would hide topology mistakes and allow cross-domain mutation drift.
Scalability potential: Stable one-owner route on every device lane.
Hardware Impact: No runtime change; preserves cached handle validation.

Problem: Post-KCC verification could not rely on the earlier source gate because a DTO initializer changed afterward.
Solution: Re-ran targeted `git diff --check`, HUD runtime mesh/physiology token scan, modified-file hot-token scan, MetabolicStateDTO initializer scan, orphan `.meta` scan, and build-throttle process/CPU checks.
Rejected Alternatives: Declaring completion from stale verification or launching `dotnet build` while CPU measured 100 and Unity `VBCSCompiler.dll` was already active.
Scalability potential: No runtime feature change; prevents false validation state and protects shared workstation scheduling on low-end developer hardware.
Hardware Impact: No game runtime cost; build remains intentionally unlaunched under throttle.

Problem: Global missing-`.meta` scan found `FaunaRigBuilder1714.cs`, `RockSculptorEngine1713.cs`, and `EquipmentMetadata.cs` without matching Unity meta files.
Solution: Recorded them as out-of-domain residuals and left them untouched because 1701 did not create, delete, or modify those assets; orphan `.meta` scan itself returned no output.
Rejected Alternatives: Creating or deleting meta/assets in fauna, geology, or interaction domains would violate the current physiology/HUD domain boundary without a critical dependency.
Scalability potential: No runtime effect; preserves domain ownership while surfacing integration hygiene debt.
Hardware Impact: 0 runtime us.

Problem: HUD target still contains many `TryGetComponent` tokens, which can look like a hot-path violation in grep output.
Solution: Mapped the hits to runtime bootstrap, queued cold refresh, hierarchy construction/repair, save-independent canvas normalization, or static helper entry points; `LateFrameTick` steady solve exits unless `IsRuntimeHierarchyReady()` is true and does not call `AutoResolve`, `NormalizeCanvas`, or `EnsureHierarchy`.
Rejected Alternatives: Large HUD bootstrap refactor outside RB-004 would increase merge conflict surface and risk Canvas lifecycle regressions.
Scalability potential: Low devices avoid per-frame scene/component search; middle/high/ultra retain richer HUD visuals through existing continuous quality cadence.
Hardware Impact: No added lookup cost; existing cold paths remain outside steady visual solve.

Problem: The first Dear Lie oxygen curve used `0.15 * smoothstep(0, 0.15, real)`, which drops below the real value in the lowest part of the band and visually accelerates the last few percent.
Solution: Changed the low-band mapping to `real * (2 - smoothstep(0, 0.15, real))`; it keeps the exact endpoints at 0 and 0.15, stays above real oxygen inside the low band, and uses only the existing smoothstep scalar plus multiply/subtract.
Rejected Alternatives: `sqrt`, `pow`, lookup tables, or adding a HUD-side MetabolicStateDTO DataVault read. Those either add cost, add state ownership, or bypass the existing UIStateStore presentation bridge.
Scalability potential: Low devices pay the same VISUAL_SYNC scalar cadence with no allocation; middle/high/ultra can spend the stretched low-band tension on richer vignette/proxy-light presentation without changing simulation truth.
Hardware Impact: Replaces one multiply after smoothstep with one subtract and one multiply; no heap allocation, no registry lookup, no DataVault lock.

Problem: Full repository `.meta` scans are noisy because non-Unity tools/docs and other agents' test files lack Unity meta sidecars.
Solution: Separated proof into three scopes: orphan meta count is zero, every 1701-touched Unity asset has its `.meta`, and the remaining Assets-wide missing meta is `Assets/_Project/Tests/Editor/ColliderOptimizer1716EditTests.cs`, outside the physiology/HUD domain.
Rejected Alternatives: Creating meta files for another agent's editor test or reporting a clean global scan after filtering nothing.
Scalability potential: No runtime effect; keeps asset hygiene evidence accurate without crossing domain ownership.
Hardware Impact: 0 runtime us.

Problem: Threat chevron matrix generation recalculated the camera AUP inside every visible chevron slot.
Solution: Resolve camera runtime AUP once in `BuildThreatChevronMatrices` and pass the immutable AUP plus camera position into `TryBuildThreatChevronMatrix`.
Rejected Alternatives: Caching the camera AUP as a field across frames would risk stale origin-shift state; leaving per-slot reconstruction wastes VISUAL_SYNC scalar work.
Scalability potential: Low devices avoid repeated double/AUP conversion when multiple threats are visible; middle/high/ultra can keep the same four-slot presentation without extra truth state.
Hardware Impact: Up to three redundant `TryResolveRuntimeAup` calls removed per frame when four chevrons are active; no allocation, no lookup, no DataVault access.

Problem: The Dear Lie display scalar was also feeding HUD haptic coupling and the status-key resolver.
Solution: Keep `oxygen` as presentation-only for gauge/color/fog, but pass `realOxygen` to `EvaluateCriticalHapticCoupling` and `ResolveStatusKeyHash`.
Rejected Alternatives: Letting the low-oxygen display lie delay haptic/status warnings, or duplicating a second oxygen read from the physiology vault inside HUD.
Scalability potential: Low devices get the same cheap scalar path with accurate warning semantics; middle/high/ultra can intensify visuals from the displayed scalar while safety feedback remains tied to truth.
Hardware Impact: Argument-source swap only; no allocation, lookup, DataVault access, or extra math.

Problem: The sensory VISUAL_SYNC hypoxia bridge could retry `TryGetGenerationHandle` every frame while the metabolism buffer was absent or stale.
Solution: Added a 30-frame bounded retry gate around the metabolic state handle refresh while keeping steady-state reads on `IDataVault.TryReadOnlyHandle`, which is documented as a pure current-phase read accessor.
Rejected Alternatives: Adding a DataVault read lock to a pure read-only API, polling generation metadata every VISUAL_SYNC frame, or routing hypoxia presentation back through HUD.
Scalability potential: Low devices avoid repeated missing-buffer metadata probes; middle/high/ultra keep the same shader gas-toxicity presentation route from one scalar.
Hardware Impact: Removes up to 29 redundant failed metadata lookups per missing/stale handle interval; no allocation, no lock, no new owner.

Problem: Surface recovery and public oxygen refill/drain APIs could mutate local oxygen before the metabolism vault state was staged.
Solution: Stage `MetabolicStateDTO` through the existing single write-lock route before local oxygen mutation in `UpdateOxygen`, `ApplyOxygenRefill`, and `DrainOxygen`; return without mutation when the vault lock is denied.
Rejected Alternatives: Relying on `UpdateOxygenGraceState` fallback to repair drift later, or adding a second public oxygen bridge helper.
Scalability potential: Low devices get deterministic fail-closed oxygen truth during defrag contention; middle/high/ultra keep identical gameplay truth and can scale only presentation.
Hardware Impact: One existing DTO write route, no new collection, no allocation, no registry lookup.

Problem: Runtime oxygen-capacity multiplier changes could clamp effective oxygen without updating `MetabolicStateDTO.RealO2`.
Solution: `SetRuntimeOxygenCapacityMultiplier` now stages clamped oxygen and hypoxia state through the same DataVault writer, rolls back the multiplier on lock denial, and uses Unity.Mathematics `math.clamp`.
Rejected Alternatives: Letting capacity/equipment changes drift until the next slow oxygen tick, or introducing a duplicate physiology owner.
Scalability potential: Weak devices avoid inconsistent HUD/simulation bridge state during equipment changes; high/ultra lanes keep richer visual response from the same scalar.
Hardware Impact: Public API path only; no hot-loop cost, no new managed allocation, one existing DTO write when called.

Problem: Oxygen critical signals and localized oxygen pockets still contained direct local oxygen mutation paths.
Solution: Staged critical signal clamps and air-pocket refills through `TryWriteMetabolicOxygenStateToVault` before local mutation; lock denial returns without changing local truth.
Rejected Alternatives: Waiting for a later pass to repair stale signal/pocket DTO state, or adding a second oxygen mutation owner.
Scalability potential: Low devices get coherent oxygen truth under defrag contention; middle/high/ultra keep identical simulation and can scale only visual response.
Hardware Impact: Same existing DTO write route; no allocation, no new handle lookup cadence, no extra owner.

Problem: Save-load restore could leave `MetabolicStateDTO` stale until the first slow simulation pass.
Solution: `LoadFromSaveData` now writes normoxic or hypoxia DTO state immediately after restoring oxygen and grace state.
Rejected Alternatives: Depending on first post-load SlowTick or HUD fallback state.
Scalability potential: All lanes avoid one-frame stale HUD/physiology bridge after load.
Hardware Impact: Cold load only; no steady-state frame cost.

Problem: The active agony branch briefly staged `AgonyTimeRemaining` before decrement, allowing a stale pre-decrement timer write in the same slow tick.
Solution: Removed the `_oxygenGraceActive` pre-write from `UpdateOxygen`; `UpdateOxygenGraceState` owns the post-decrement DTO publication and keeps the single write-lock route.
Rejected Alternatives: Writing twice per active agony tick or adding a separate timer shadow field.
Scalability potential: Low/middle/high/ultra lanes all consume the same post-decrement agony timer without presentation drift.
Hardware Impact: Removes one redundant DTO write attempt during active agony; no allocation, no extra lock owner.

Problem: RB-004 code-side fix still depended on a serialized static mesh reference that was absent from the HUD prefab.
Solution: Added offline Unity Mesh asset `M_HUD_ThreatChevron.asset` with 8 vertices, 12 uint16 indices, position+UV stride 20, and bound `Suit_HUD_Canvas.prefab` to the asset GUID.
Rejected Alternatives: Reintroducing runtime mesh generation, binding the existing curved panel mesh, or waiting on a compiling editor to author the asset.
Scalability potential: Low devices skip all mesh construction; middle/high/ultra keep the same instanced chevron draw and can spend saved budget on material/shader flicker.
Hardware Impact: Removes runtime mesh allocation/construction path; static validation shows 24 index bytes and 160 vertex bytes.

Problem: The first hand-authored chevron `.asset` was syntactically present but Unity reported `assetType=Unknown`.
Solution: Removed the invalid `.asset`, preserved the GUID `.meta`, and re-authored `M_HUD_ThreatChevron.asset` through Unity `AssetDatabase.CreateAsset`/`Mesh` API; Unity now reports `assetType=UnityEngine.Mesh`, 8 vertices, 12 indices, one submesh.
Rejected Alternatives: Keeping a dangling prefab reference, reintroducing runtime mesh construction, or binding the unrelated curved HUD panel mesh.
Scalability potential: Low devices get no runtime mesh build; middle/high/ultra keep stable instanced chevrons with shader flicker only.
Hardware Impact: Runtime unchanged from static mesh path; editor-only authoring allocations do not enter player steady state.

Problem: Unity console is red after import, but errors are not in 1701 files.
Solution: Recorded the blocker as out-of-domain `Assets/_Project/Scripts/Quest/MissionMarkerSystem.cs` syntax errors and did not cross the physiology/HUD domain boundary.
Rejected Alternatives: Patching quest code without assignment authority or falsely claiming build-green.
Scalability potential: No runtime change in this domain; keeps ownership clean for the quest agent/integrator.
Hardware Impact: 0 runtime us for 1701.

Problem: `TryReadMetabolicStateBuffer` could spend the 30-frame retry token before its fallback refresh branch, causing stale generation handles to stay stale after a failed read.
Solution: On failed read, check the compaction fence, clear `_metabolicStateHandle`, reset `_nextMetabolicStateHandleRetryFrame`, and perform one immediate generation-handle refresh/read attempt.
Rejected Alternatives: Adding a read lock in VISUAL_SYNC, polling generation metadata every frame, or accepting a 30-frame stale hypoxia presentation delay.
Scalability potential: Low devices keep bounded metadata retry in steady state; middle/high/ultra avoid delayed hypoxia vignette recovery after buffer generation changes.
Hardware Impact: No steady-state cost; failure path adds one immediate handle refresh instead of a delayed retry window.

Problem: Final broad grep still reported cold `TryGetComponent` and CSV allocation tokens, which could be misread as hot-loop regressions.
Solution: Re-ran a declaration-bounded hot-method scan over the touched survival, sensory, and HUD files and separated cold bootstrap/save-load hits from simulation/update method bodies.
Rejected Alternatives: Reporting the broad grep as failure, deleting cold bootstrap lookups, or adding duplicate cached systems without evidence.
Scalability potential: Low devices keep cold setup costs outside steady-state frames; middle/high/ultra keep the same cached runtime path and can scale presentation only.
Hardware Impact: 18 hot method definitions scanned, 0 forbidden token hits; 0 runtime us.

Problem: The local 4-second agony timer could advance even when the metabolic vault writer path failed after lock acquisition or while a compaction fence denied mutation.
Solution: `UpdateOxygenGraceState` now computes the next timer/blur first, stages the `MetabolicStateDTO`, and commits local timer, active flag, vignette, and emergency movement multiplier only after the write route does not report lock/fence denial. Invalid/empty writer views now set `lockDenied=true`.
Rejected Alternatives: Letting local oxygen death timing drift from DataVault state, adding a second timer owner, or holding extra locks around movement/presentation calls.
Scalability potential: Low devices under vault defrag contention lose one agony decrement instead of risking deadlock or state drift; middle/high/ultra keep the same visual vignette scalar and spend no extra steady-state allocation.
Hardware Impact: No heap allocation; failure path returns earlier. Steady state adds no collection allocation and retains one write-lock route.

Problem: The O2 ring used the smoothstep Dear Lie scalar, but the same gauge's numeric reserve still displayed truthful `OxygenCurrent`, making the HUD contradict its own presentation deception.
Solution: Keep `UIStateStore.OxygenCurrent` truthful and derive a local `oxygenCurrentDisplay` in `RefreshVisuals` by scaling the current value with `displayOxygen01 / realOxygen01` via `ResolveDearLieOxygenCurrentDisplay`.
Rejected Alternatives: Mutating the headless truth slot, feeding Dear Lie oxygen into haptics/status, or adding a second physiology read in HUD.
Scalability potential: Low devices pay one scalar multiply path in VISUAL_SYNC; middle/high/ultra get coherent diegetic O2 presentation without changing simulation truth.
Hardware Impact: One rcp/multiply guarded by `math.select`; no heap allocation, no registry lookup, no DataVault lock.

Problem: Unity console still reported missing `HectonScanMarkerSystem` symbols that do not exist in the current file revision.
Solution: Validated the current scanner marker source with Unity MCP and confirmed zero diagnostics; treated the console entries as stale/out-of-date rather than patching absent identifiers.
Rejected Alternatives: Reintroducing removed runtime mesh/shader generation fields, editing unrelated systems, or claiming a full compile while CPU/dotnet throttle blocks build.
Scalability potential: Keeps scanner marker presentation on authored mesh/material assets and avoids runtime allocation regressions on weak devices.
Hardware Impact: 0 runtime us; validation only.

Problem: The Unity validator reported duplicate method signatures in large physiology files at line 0.
Solution: Cross-checked each reported symbol with `rg`; `HectonSurvivalSystem` has one real definition for each reported method, and `ShinobuSensoryImpairmentRuntime` has one `MutationGuardBit` definition with multiple call sites.
Rejected Alternatives: Removing valid call sites, splitting helpers, or adding wrappers to satisfy a false-positive validator pass.
Scalability potential: Preserves one-owner physiology/HUD routes without duplicate method churn.
Hardware Impact: 0 runtime us.

Problem: `Tool_Scanner_Held.prefab` routed scanner markers to the threat-chevron mesh after runtime mesh generation was removed, so the scanner marker shader would receive an incompatible two-bar chevron instead of a quad/diamond UV surface.
Solution: Authored `M_ScannerMarkerQuad.asset` through Unity `AssetDatabase.CreateAsset` with 4 vertices, 6 indices, UV0, and bound `ScannerTool.scannerMarkerMesh` to its GUID while reusing the existing instanced scanner marker material.
Rejected Alternatives: Reintroducing runtime `new Mesh`, keeping the threat chevron mesh binding, or duplicating the material without a tuning need.
Scalability potential: Low devices avoid runtime mesh construction and get a correct single-quad marker; middle/high/ultra can scale shader flicker/occlusion visuals through the existing material without changing code.
Hardware Impact: Removes runtime mesh build pressure; authored mesh is 80 vertex bytes and 12 index bytes.

Problem: Save-load and respawn reset paths discarded the result of the metabolic vault write, allowing `_metabolicOxygenStateSyncedThisTick` to remain stale from previous runtime phases.
Solution: Assign `_metabolicOxygenStateSyncedThisTick` from `TryWriteMetabolicOxygenStateToVault` in `LoadFromSaveData` and `ResetToMax`, so a denied cold write causes the next owner tick to retry.
Rejected Alternatives: Failing the whole load/respawn on a temporary compaction fence, or pretending local state and vault state are synchronized after a denied write.
Scalability potential: Low devices under memory pressure keep coherent retry behavior; middle/high/ultra get the same save/respawn semantics without extra hot cost.
Hardware Impact: Cold path only; no runtime allocation and no extra lock owner.

Problem: Scanner markers used the authored quad/material path, but `DrawMeshInstanced` still passed a null `MaterialPropertyBlock`, so `Hecton_ScannerMarkerInstanced.shader` could not receive per-instance fade data.
Solution: Extend the existing `HectonScanMarkerSystem` with one cold `MaterialPropertyBlock` and a fixed `Vector4[64]` mirror; `BuildMarkerMatrices` writes alpha/enable bits beside the matrix, and `RenderMarkers` uploads `_InstanceData` before the instanced draw.
Rejected Alternatives: Reintroducing runtime material mutation, creating a parallel marker renderer, or accepting shader default alpha while scale fade alone carries lifetime.
Scalability potential: Low devices keep one authored quad and one instanced draw; middle/high/ultra retain shader flicker/occlusion and can scale material quality without changing marker ownership.
Hardware Impact: One cold MPB allocation, fixed 64-vector mirror; steady-state adds no heap allocation, no registry lookup, no DataVault access, and no sync completion.

Problem: HUD chevron and scanner marker MPBs were created through lazy resource-ensure branches, leaving object allocation syntax near systems that are audited as presentation gates.
Solution: Move both MPBs to readonly cold field allocations and strip the lazy `??=`/null-reset branches from `EnsureThreatChevronRuntimeResources` and `EnsureRuntimeResources`.
Rejected Alternatives: Adding a new renderer/GraphicsBuffer path this late would be a broader shader/material contract change; leaving lazy allocation helpers keeps a false hot-path risk surface.
Scalability potential: Low devices keep authored mesh + instanced fake presentation; middle/high/ultra can spend shader quality on the same MPB-fed instance data without changing ownership.
Hardware Impact: 0 heap bytes in VISUAL_SYNC. One cold MPB object per component instance; hot scans over draw/resource methods report no allocation tokens.

Problem: A compaction fence could rise during metabolic handle refresh, or a generation-rotated handle could fail write-lock acquisition while remaining marked ready.
Solution: Treat fence-raced refresh failure as `lockDenied`, and clear the cached metabolic handle/retry gate when write-lock acquisition fails without an active fence.
Rejected Alternatives: Freezing all oxygen writes when the metabolism buffer is simply missing during bootstrap would break cold startup; keeping stale ready handles would cause repeated fail-closed writes.
Scalability potential: Low devices under vault defrag get deterministic no-mutation behavior; middle/high/ultra recover from generation swaps on the next owner tick without extra locks.
Hardware Impact: Failure path only. No heap allocation, no second write lock, no additional steady-state DataVault owner.

Problem: Survival physiology handle retry gates used only `SystemDispatcher.CurrentFrameId`, so a zero dispatcher frame during early bootstrap could bypass the intended 30-frame retry cadence.
Solution: Fall back to `Time.frameCount` only when the dispatcher frame id is zero.
Rejected Alternatives: Per-frame generation lookup until dispatcher startup, or moving handle resolution into a new bootstrap subsystem.
Scalability potential: Weak devices avoid repeated missing-buffer metadata probes during startup; high-end lanes keep identical cached-handle behavior once dispatcher frames are valid.
Hardware Impact: One frame-count read on missing/stale handle paths; no `deltaTime`, no allocation, no registry polling.

Problem: The task memory claimed scanner marker `_InstanceData` was restored, but current source still passed `null` as the `MaterialPropertyBlock` to `DrawMeshInstanced`, so `Hecton_ScannerMarkerInstanced.shader` could not consume per-marker alpha.
Solution: Patch the existing `HectonScanMarkerSystem` directly: add `InstanceDataId`, a fixed `Vector4[64]` mirror, one cold `MaterialPropertyBlock`, per-visible-marker alpha/enable writes, and pass the block to `DrawMeshInstanced`.
Rejected Alternatives: Reintroducing runtime mesh/material generation, mutating the shared material per marker, or adding a parallel renderer outside the scanner marker owner.
Scalability potential: Low devices keep one authored quad and one instanced draw; middle/high/ultra keep shader flicker/fade/occlusion from the same data path without changing simulation or scanner ownership.
Hardware Impact: 0 steady-state heap bytes; one cold MPB object and one fixed 64-vector mirror, with a bounded 1024-byte vector payload upload only when scanner markers are visible.

Problem: Invalid or empty metabolic writer views were marked as lock-denied, but clearing the cached handle inside the locked region would make `finally ReleaseWriteLock` use the wrong handle if the field itself was passed to release.
Solution: Copy `_metabolicStateHandle` into a local `writeHandle` before acquisition; acquire and release the local handle, while stale cached handle recovery can safely clear the field before returning.
Rejected Alternatives: Leaving stale invalid handles ready, or clearing the field without protecting release identity.
Scalability potential: Low devices under DataVault generation churn recover on the next owner tick without local oxygen drift; middle/high/ultra keep the same one-lock writer route.
Hardware Impact: Failure path only; one unmanaged handle struct copy on write attempts, no heap allocation, no additional lock.

Problem: A terminal `OxygenCriticalSignal` could call `UpdateOxygenGraceState(0f)` inside `ConsumeOxygenCriticalSignals`, then the same `SlowTick` would call `UpdateOxygenGraceState(dt)`, causing local agony time to advance while the DTO still held the 4-second pre-decrement value from the signal write.
Solution: Remove the intra-consumer grace call and let the existing owner-phase `UpdateOxygenGraceState(dt)` run once after all oxygen mutation inputs have settled.
Rejected Alternatives: Adding a per-tick guard flag, writing the DTO twice in one slow tick, or keeping the local/DTO timer drift for one tick.
Scalability potential: Low devices avoid redundant survival helper work on signal-heavy frames; middle/high/ultra keep the same four-second hypoxia drama with tighter state truth.
Hardware Impact: Removes one zero-delta helper call on terminal signal frames; no allocation, no new signal, no extra lock.

Problem: After late scanner and survival patches, older proof artifacts were no longer sufficient.
Solution: Re-run scoped declaration-bounded hot scans and runtime mesh/material blocker scans over the touched survival/HUD/scanner/sensory routes.
Rejected Alternatives: Claiming green from stale scans or launching build while host CPU remains above the explicit throttle.
Scalability potential: No runtime feature change; protects the low-end proof lane from hidden hot allocations and runtime mesh/material regressions.
Hardware Impact: 0 runtime us; verification only.

Problem: Public oxygen mutation paths could restage an already-active hypoxia window as a fresh 4.0-second DTO timer.
Solution: Added `ResolveStagedAgonyTimeRemaining(byte)` inside `HectonSurvivalSystem`; hypoxia staging now uses the active `_oxygenGraceTimer` when `_oxygenGraceActive` is already true, otherwise it arms the initial 4.0-second window.
Rejected Alternatives: Adding separate branches to every public oxygen API, writing the DTO twice in the same call, or allowing equipment/critical-signal calls to extend the agony window.
Scalability potential: Low devices keep one scalar helper with no allocation; middle/high/ultra keep identical survival truth while presentation can still scale the vignette and HUD tension from the same DTO.
Hardware Impact: 0 heap bytes. Public/slow oxygen mutation paths add one `math.select` helper route; no DataVault lock count increase and no new owner.

Problem: `CheckLethalConditions` ignored the respawn signal enqueue result and reset survival locally even if the cross-domain respawn request was dropped.
Solution: Gate `ApplyRespawnReconciliationSurvival` on `PlayerDeathReconciliationBridge.RequestRespawn` returning true; if the lane rejects the signal, survival remains dead with its death record intact instead of silently diverging from respawn/inventory/collision consumers.
Rejected Alternatives: Retrying in a loop, adding a second death lane, or keeping local reset as a hidden fallback after signal failure.
Scalability potential: Low devices under signal pressure fail closed instead of corrupting cross-domain state; middle/high/ultra keep identical death semantics and one respawn signal route.
Hardware Impact: Death path only. One bool branch after the existing signal enqueue; 0 steady-state heap bytes and no extra hot route.
