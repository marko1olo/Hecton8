# SHINOBU_217 Rationale

Date: 2026-05-20
Status: STATIC VERIFIED - COMPILE WALL IN CORE MEMORY ASMDEF SURFACE

## Decision 00 - Authority and Domain Scope

Problem: The task touches construction snapping, native buffers, Burst jobs, shader-facing preview state, and logistics graph handoff. That can become cross-domain global surface if implemented as direct references or live registry polling.

Solution: Keep the implementation inside `Assets/_Project/Scripts/Construction/` plus editor-only validators, with unmanaged DTOs and jobs as stateless kernels. Cross-domain state is represented as handles/flags/jobs, not concrete references. No hot-loop `GlobalRegistry` polling.

Rejected Alternatives: A MonoBehaviour manager polling scene snap points was rejected because it violates Zero-GC, trigger eradication, and Global Authority boundaries. A new global registry service was rejected because no route-card/green review exists and owner-local construction domain can own the kernel.

Scalability potential: Low uses reduced candidate count and small snap radius through continuous quality weight; Middle keeps broader sector candidates; High increases visual Dear Lie shader fidelity; Ultra spends saved CPU on denser hologram distortion and debug visibility without changing simulation truth.

Hardware Impact: On i3/MX350, replacing trigger broadphase and prefab previews is expected to remove main-thread PhysX overlap pressure and managed instantiation stalls. Static source only; profiler proof absent.

## Decision 01 - Visual Fake First

Problem: Physical door prefab instantiation and trigger-based docking create CPU/GC cost for an effect that can be represented by connection flags and shader presentation.

Solution: Use socket flags and a shader scalar for the Dear Lie snap vibration. Rendering can procedurally grow hatch/bulkhead states from flags. Gameplay truth stays in AUP/socket DTOs.

Rejected Alternatives: Instantiating hatch/door prefabs on snap was rejected because it creates hierarchy churn, memory fragmentation, and cross-agent dependency on prefab authoring. Smooth physical interpolation was rejected because it introduces authority lag and float jitter.

Scalability potential: Low = simple red/green hologram and short decay; Middle = edge indicators; High = vertex ripple and normal response; Ultra = richer procedural seam/bulkhead growth. All driven by continuous `GlobalQualityWeight`, not binary tiers.

Hardware Impact: On i3/MX350, shifting work to a scalar shader fake should keep CPU snap cost in microseconds and avoid prefab spikes. GPU cost must remain gated by quality weight; Frame Debugger proof absent.

## Decision 02 - Vault Buffer IDs

Problem: Socket CSR data needs persistent NativeArray lanes for states, AUPs, ghost sockets, results, telemetry, tuning, bounds, counters, connection pairs, preview state, and CSR indices. The central `BufferID` enum already owns `70358..70369`; extending a core memory enum from this domain would widen the compile surface.

Solution: Use the existing `BufferID.ConstructionSocket*` enum values for `70358..70369`, and document owner-local construction casts `70370..70372` in the binary payload ledger for `GhostPreviewDTO`, CSR ranges, and CSR target indices. Owner is `SystemID.Construction`; all hot jobs receive NativeArrays, not `GlobalRegistry` references.

Rejected Alternatives: Mutating the core memory enum for the owner-local preview/CSR lanes was rejected because this pass is confined to socket construction and the stale Core.Memory asmdef is already the compile wall. Reusing logistics buffer IDs was rejected because Agent 114 owns those lanes.

Scalability potential: Low allocates fixed small candidate/result windows and clamps evaluated sockets to 16; Middle increases sector range; High/Ultra can use 256 candidates and richer Dear Lie presentation without changing topology truth.

Hardware Impact: On i3/MX350, explicit vault lanes avoid per-frame array allocation and reduce cache misses. Memory cost for mock grid is fixed: 500 module records plus 3000 socket records.

## Decision 03 - Active Builder Snap Path

Problem: `PlayerBuilder` used PhysX `OverlapSphereNonAlloc` against trigger socket colliders. That still forces broadphase work and ties snapping truth to scene hierarchy.

Solution: Replaced the active socket query with template-driven AUP math over registered construction modules. Target and ghost socket deltas are computed with `double3` AUP subtraction before final runtime float conversion. Runtime proxy socket colliders were removed; `ModuleSocket` components remain only as cold compatibility/occupancy markers.

Rejected Alternatives: Keeping trigger colliders and merely increasing buffer size was rejected because it preserves the PhysX dependency. Full GameObject deletion of all socket components was rejected because existing graph rebuild and authoring validators still consume `ModuleSocket` during migration.

Scalability potential: Low scans a continuous quality-limited candidate count; Middle expands search radius; High/Ultra retain deterministic snap while spending saved CPU on shader fake and debug visibility.

Hardware Impact: Expected low-end gain is removal of per-preview PhysX socket broadphase. Active path still reads registered module transforms until the dispatcher feeds pure Vault socket AUP arrays into the Burst kernel; profiler proof pending.

## Decision 04 - Mock Grid And Layout Gate

Problem: Socket solver must be testable while voxel/SDF buffers or authored module data are unavailable.

Solution: Added `GenerateMockBaseConstructionGrid()` to fill Vault-owned module/socket/AUP/counter buffers with 500 modules and 3000 sockets using `NativeArrayOptions.UninitializedMemory`. Added editor layout validation for the 64-byte `SocketStateDTO` offsets.

Rejected Alternatives: Scene-spawned mock modules were rejected because they allocate GameObjects and do not stress the Burst data path. Zero-filled arrays were rejected because every active element is overwritten.

Scalability potential: Low uses the mock grid for cheap deterministic tests; Middle/High/Ultra can increase search budgets without changing buffer layout.

Hardware Impact: Fixed vault memory improves repeatability on i3/MX350. Compile proof pending because CPU gate was above 50 percent at the first verification point.

## Decision 05 - Burst CSR Kernel Shape

Problem: A single main-thread best-socket search cannot meet deterministic co-op, AUP precision, and low-end frame budget requirements.

Solution: Added deterministic Burst jobs: `EvaluateSocketSnappingJob`, `SelectBestSocketSnapJob`, `AdaptConnectedSocketsJob`, `VerifyModuleBoundsJob`, `CommitPlacedModuleJob`, and `RecordConstructionSocketTelemetryJob`. Jobs operate on unmanaged socket, AUP, bounds, result, counter, and telemetry arrays.

Rejected Alternatives: A managed reduction over `ModuleSocket` objects was rejected because it keeps hierarchy dependency. A job that writes directly into scene objects was rejected because it would cross the main-thread authority boundary.

Scalability potential: Low clamps candidates to 16 and near-sector search; Middle increases range; High/Ultra use up to 256 candidates and richer visual flags. The math path is continuous through `GlobalQualityWeight`.

Hardware Impact: On i3/MX350, the expected win is removal of PhysX broadphase and cache-friendly 64-byte socket records. CPU profiler proof pending; compile was blocked by 100 percent CPU gate on second verification attempt.

## Decision 06 - Commit Without Solver Stall

Problem: Placement must update topology without forcing power/fluid/logistics readers to stall on a mid-frame rebuild.

Solution: `CommitPlacedModuleJob` appends module and socket DTOs into fixed Vault arrays and sets `TopologyDirty | RollbackFence` counters. Existing graph owners can swap on their dispatcher phase; no `.Complete()` or direct Agent 114 dependency is introduced.

Rejected Alternatives: Calling `HabitatGraphManager.Rebuild()` inside placement click was rejected because it can stall the frame and conflict with other graph readers. Direct calls into Agent 114 logistics code were rejected because they introduce a cross-domain compile dependency.

Scalability potential: Low keeps old CSR snapshot for one frame while pending buffers settle; Middle/High/Ultra can rebuild denser topology during dispatcher-controlled windows.

Hardware Impact: Low-end devices avoid a placement-frame spike. Top-tier devices can consume the dirty flag to schedule heavier visual/logistics recomputation off the click path.

## Decision 07 - Precision, Rollback, And Black Box

Problem: Socket placement becomes authoritative topology. Float drift, non-deterministic Burst math, or missing crash telemetry would make co-op and fluid leak diagnosis unreliable.

Solution: Snap deltas use `double3 TargetSocketAUP - GhostSocketAUP`; Burst jobs use `FloatMode.Deterministic`; DTO arrays are explicit-layout unmanaged records suitable for blind memcopy snapshots; telemetry writes a 300-entry ring and dumps `Dump_SHINOBU_217.bin` on non-finite state.

Rejected Alternatives: Float world-space subtraction was rejected because it tears at far AUP coordinates. Fast Burst float mode was rejected for rollback. Logging only to console was rejected because post-crash state must survive.

Scalability potential: Low records minimal high-level state; Middle/High/Ultra keep the same ring layout while adding richer visual/candidate data through flags and counters.

Hardware Impact: Deterministic mode may cost a small ALU premium, but low-end hardware gains more by eliminating PhysX socket broadphase and managed hierarchy scans. Dump write is exceptional-path only.

## Decision 08 - Human Control And Static Audit

Problem: Designers need socket visibility and tuning without touching hot runtime code, while the project needs a repeatable scan for legacy physics residues.

Solution: Added a UI Toolkit tuner, byte-span CSV importer, DTO-backed scene gizmo, and `ConstructionPhysicsStaticScanner` report writer. The scanner strips comments and records active forbidden patterns to `CONSTRUCTION_OPTIMIZATION_REPORT.json`.

Rejected Alternatives: Inspector-only serialized fields were rejected because tuning should reach Vault DTOs. GameObject socket gizmos were rejected because the debug view must read DTO/AUP lanes. Manual grep-only audit was rejected because it leaves no machine-readable evidence.

Scalability potential: Low uses sparse gizmo/candidate display; Middle/High/Ultra can show denser DTO sockets and use stronger Dear Lie visual scalars.

Hardware Impact: Editor-only controls do not affect runtime frame time. Static scanner currently flags adjacent non-snap construction physics that remain outside this socket route.

## Decision 09 - Shared Report Collision

Problem: `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT.json` currently contains a concurrent `SHINOBU_220` report. Overwriting it would destroy another agent's evidence and violate simultaneous execution discipline.

Solution: Preserve the shared file and write `Docs/Reports/CONSTRUCTION_OPTIMIZATION_REPORT_SHINOBU_217.json` as the agent-scoped mirror for socket adaptor evidence. Status and log now explicitly name the collision.

Rejected Alternatives: Blind overwrite was rejected because it would erase valid concurrent work. Merging unlike report schemas into one JSON root was rejected because it would make both reports weaker for machine consumption.

Scalability potential: Report isolation keeps construction metrics composable when multiple agents scan adjacent domains in parallel.

Hardware Impact: No runtime impact. Editor/static reporting only.

## Decision 10 - DTO Name Collision And Generation Handles

Problem: The first module-row name duplicated an existing construction catalog DTO name and would create a compile wall. The first pass also used legacy pointer-bearing Vault handle language after Core introduced `VaultGenerationHandle<T>`.

Solution: Renamed this lane's module row to `ConstructionSocketModuleDTO` and migrated SHINOBU socket handles to pointer-free `VaultGenerationHandle<T>` descriptors resolved into phase-local `NativeArray<T>` views.

Rejected Alternatives: Keeping the old name was rejected because it collides with Agent 216 catalog ownership. Keeping legacy pointer-bearing handles was rejected because stale pointer retention violates the active Core memory addendum.

Scalability potential: Low/Middle/High/Ultra use the same 96-byte module row; quality changes candidate work, not layout or ownership.

Hardware Impact: Prevents compile failure and stale pointer hazards. Low-end impact is indirect: no extra owner-local persistent arrays or pointer aliases.

## Decision 11 - Active Vault/Burst Snap Route

Problem: The rough active path still contained a managed fallback nested scan after the Vault attempt, so PhysX was gone but object-oriented snapping logic remained as a hidden escape hatch.

Solution: Removed the fallback block. `PlayerBuilder` now caches `ConstructionManager` and `IDataVault` cold, hydrates target sockets into Vault only when topology/module count changes, writes `GhostPreviewDTO` to owner-local buffer `70370`, schedules `EvaluateSocketSnappingJob`, chains `SelectBestSocketSnapJob`, and writes solver telemetry when the dispatcher fence finalizes the result.

Rejected Alternatives: A permanent managed fallback was rejected because it would hide regressions and keep transform hierarchy scanning alive in the active route. A new global service was rejected because construction already owns the local route and Vault buffers.

Scalability potential: Low evaluates up to 16 near-radius candidates inside the 5m search. Middle expands range and budget smoothly. High/Ultra use 256 near-radius candidates and spend the saved CPU on Dear Lie shader response.

Hardware Impact: On i3/MX350, the snap route avoids PhysX broadphase and per-frame registry lookup. Full CPU proof is pending behind the build/profiler gate.

## Decision 12 - Race And Non-Finite Hardening

Problem: `AdaptConnectedSocketsJob` as `IJobParallelFor` could race when multiple connection pairs wrote the same socket row. `CommitPlacedModuleJob` accepted non-finite module/socket input. The snap-result buffer also risked aliasing the final ghost row with the best-result sink.

Solution: Converted adaptation to a single `IJob` over bounded connection count, added finite guards to commit, reserved `64 + 1` snap-result rows, and clamped parent-module lookup to the active counter to avoid reading uninitialized module slack.

Rejected Alternatives: Relying on caller uniqueness was rejected because topology edits are shared authority. Clearing full buffers was rejected because the prompt explicitly requires `UninitializedMemory` and active-count discipline.

Scalability potential: The same correctness fences apply at every quality weight; higher quality only increases accepted near-radius candidates.

Hardware Impact: Prevents false-sharing/data-race corruption and avoids extra clears. Low-end devices keep deterministic bounded writes; high-end devices can consume richer valid telemetry without changing memory layout.

## Decision 13 - Compile Surface And Core Memory Wall

Problem: The first allowed `dotnet build Assembly-CSharp.csproj --no-restore --nologo` showed a SHINOBU visibility fault: `PlayerBuilder` compiled inside `Hecton8.Core.csproj`, while `ShinobuSocketConstructionData.cs` and `ShinobuSocketConstructionJobs.cs` were not in that project file. After that was fixed, the build failed on `VaultGenerationHandle<T>` across SHINOBU and many unrelated Core systems because `Hecton8.Core.csproj` references stale `Library/ScriptAssemblies/Hecton8.Core.Memory.dll` while the updated `GlobalDataVault.cs` source defining the generation-handle API is not represented by a fresh CLI project build.

Solution: Add only SHINOBU-owned runtime/editor files to the existing project files so the socket adaptor is visible to the compiler. Stop at the Core.Memory asmdef wall and record it as a dependency boundary: the memory assembly must be regenerated/imported or supplied as a fresh project reference by the Core.Memory owner.

Rejected Alternatives: Copying `GlobalDataVault.cs` and `H8Memory.cs` into `Hecton8.Core.csproj` was rejected because it risks duplicate definitions against the referenced `Hecton8.Core.Memory.dll` and crosses a core-memory ownership boundary. Downgrading SHINOBU to pointer-bearing `VaultBufferHandle<T>` was rejected because it would preserve stale pointer semantics and violate the pointer-safety addendum that triggered this polish pass.

Scalability potential: Keeping generation handles preserves phase-local NativeArray resolution for Low/Middle/High/Ultra quality curves. A legacy pointer downgrade would not affect visual quality directly, but it would increase crash risk during vault defrag and AUP shifts across all quality weights.

Hardware Impact: No runtime gain from the csproj patch. The avoided downgrade protects low-end ARM64 from stale pointer/unaligned relocation failures and protects high-end heavy-load sessions from vault compaction aliasing during dense construction.

## Decision 14 - Direction CSR And Burst Wrapper

Problem: The active preview bridge still invoked job `Execute()` methods directly and the first CSR field was effectively unused by `PlayerBuilder`. That preserved a linear target scan shape and weakened the Burst/job-system proof even though the kernel itself was valid.

Solution: Add owner-local CSR buffers `70371` and `70372`. Target sockets are bucketed into six direction ranges, ghost sockets map to the inverse target-direction range, and `EvaluateSocketSnappingJob` resolves target indices through the CSR indirection lane. `PlayerBuilder` schedules `EvaluateSocketSnappingJob` as an `IJobParallelFor`, chains `SelectBestSocketSnapJob` behind its handle, registers the active construction handle, and finalizes only when `DispatcherJobFence.TryFinalizeCompleted` reports completion. Cached target sockets require both module-count and scene-hash agreement, then rebuild the CSR before reuse.

Rejected Alternatives: Direct dependency on a future logistics/graph CSR owner was rejected because it would invent a sibling dependency. Keeping `Execute()` calls was rejected because it bypasses the job wrapper. Rebuilding the full target socket vault every preview tick was rejected because the scene hash is enough to invalidate transform/topology changes.

Scalability potential: Low quality still clamps candidate budget near 16, but CSR removes incompatible direction buckets before the distance/alignment work. Middle/High/Ultra can spend the larger 256 candidate budget on viable inverse-facing sockets and Dear Lie visual response instead of doomed direction pairs.

Hardware Impact: For six-way sockets, the direction CSR removes roughly five incompatible buckets before compatibility math. On i3/MX350 class CPUs this should reduce candidate memory reads and branch misses in dense bases; scheduled chaining also keeps the main thread from waiting on the solver when the cached snap is still usable. Exact microseconds remain profiler-gated behind the Core.Memory compile wall.

## Decision 15 - Occupancy Truth Transfer

Problem: Target socket hydration rebuilt DTO rows from `BaseModuleTemplate.SocketDefinition` data, but occupied state lives on cold `ModuleSocket` components after placement. That could erase `IsOccupied` truth and let the Burst evaluator consider an already consumed socket.

Solution: During target-vault rebuild only, scan each module's authored `ModuleSocket` components into the existing `_shinobuTargetSocketBuffer` list and mark matching occupied sockets as `ConstructionSocketFlags.Connected` in `SocketStateDTO.ConnectionStatus`. The active SHINOBU placement path also records the consumed ghost socket index from the Burst result and marks that socket occupied on the newly placed module. The Burst evaluator rejects `Connected` rows before distance/alignment work.

Rejected Alternatives: Reading `ModuleSocket` components per candidate inside the solver was rejected because the hot path must stay DTO-only. Recreating trigger/socket GameObjects was rejected because it restores the old hierarchy route. A separate persistent managed occupancy map was rejected because it creates shadow authority beside the DTO row.

Scalability potential: Low/Middle/High/Ultra all share the same one-fact DTO flag. Quality only changes how many open candidates are evaluated; occupied sockets are rejected before budget is spent.

Hardware Impact: Adds one cold `GetComponentsInChildren<T>(List<T>)` pass per rebuilt module using the existing list buffer, plus one placement-time scan of the newly placed module. Hot path cost is a single bit test already present in `EvaluateSocketSnappingJob`; dense bases avoid wasted distance math against occupied sockets.

## Decision 16 - Select Reducer Alias Safety

Problem: A best-result reducer that reads a `NativeArray<SocketSnappingResultDTO>` through one safety handle while writing the same Vault lane through another can be rejected by Unity's job safety system even when the write targets a reserved row.

Solution: Keep `SelectBestSocketSnapJob` on a single writable `Results` field. Candidate rows are read by index, `ResultCount` is clamped to the reserved sink row, and the selected best result is written only to `ResultSinkIndex`. `PlayerBuilder` reserves `views.SnapResults.Length - 1` as that sink and schedules the reducer after the evaluate handle.

Rejected Alternatives: A separate persistent best-result `NativeArray` was rejected because it adds a Vault lane for one 128-byte record. A main-thread reduction was rejected because it would reintroduce per-frame solver work and force completion. Passing the same array as both `[ReadOnly] Results` and writable `ResultSink` was rejected because it creates a safety-handle alias.

Scalability potential: Low quality evaluates fewer ghost rows and writes the same single sink row. Middle/High/Ultra increase candidate work through `GlobalQualityWeight`, but the reduction shape stays one bounded pass and one sink write.

Hardware Impact: No allocation and no new memory owner. Low-end hardware avoids main-thread reduction and scheduler safety faults; high-end hardware keeps the same handle chain while spending larger candidate budgets on valid inverse-direction rows.

## Decision 17 - Telemetry Best-Row Contract

Problem: `RecordConstructionSocketTelemetryJob` read `BestResult[0]`, but the active reducer writes the selected snap into a reserved sink row at `ResultSinkIndex`. If that optional job path is wired later with the full `SnapResults` lane, telemetry would record ghost candidate row 0 instead of the selected snap.

Solution: Add `BestResultIndex` to the telemetry job and clamp it before reading `BestResult`. Default index 0 keeps legacy caller behavior; scheduled SHINOBU callers can pass the reserved sink row.

Rejected Alternatives: Copying the best row into index 0 was rejected because it would overwrite a real ghost candidate row and corrupt debugging. Allocating a dedicated telemetry best-result lane was rejected because it adds a persistent buffer for one row.

Scalability potential: Low/Middle/High/Ultra all write the same telemetry row shape; quality only changes evaluated candidate counts and Dear Lie scalar values.

Hardware Impact: Adds one integer clamp on the optional telemetry job path and avoids a future black-box false positive. No allocation, no new Vault owner.

## Decision 18 - Candidate Budget Measures Memory Reads

Problem: `EvaluateSocketSnappingJob` previously advanced `EvaluatedCandidates` only after the radius test. Under low `GlobalQualityWeight`, a far base with no sockets inside the snap radius could still scan the entire inverse-direction CSR bucket while reporting a low candidate count.

Solution: Count a candidate as soon as its CSR target row resolves to a valid socket/AUP index. Connected, blocked, distant, incompatible, and poorly aligned rows all consume the same quality budget because the solver has already paid the memory-read and branch cost.

Rejected Alternatives: Counting only within-radius rows was rejected because it constrains successful snaps but not worst-case memory bandwidth. Counting only valid-compatible rows was rejected for the same reason. A binary far/near branch was rejected because budget must remain continuous through `GlobalQualityWeight`.

Scalability potential: At quality 0.0..0.3, each ghost socket now inspects the low budget rows of its inverse-direction bucket and stops, even when every row is far or occupied. Middle/High/Ultra smoothly expand that row budget up to 256 without changing the algorithm.

Hardware Impact: On low-end i3/MX350 or Quest-class CPUs, worst-case target reads are bounded by the budget instead of inverse-bucket length. In a 3000-socket mock grid with six roughly even buckets, low quality now reads about 16 rows per ghost rather than up to roughly 500 far rows.

## Decision 19 - Reducer Preserves Failed Solver Evidence

Problem: `SelectBestSocketSnapJob` only copied a valid snap candidate into the sink row. If all ghost rows failed, or if a non-finite target was detected without a valid snap, the sink row lost `EvaluatedCandidates` and fault flags. That made black-box telemetry under-report solver work and could suppress a non-finite dump request.

Solution: Aggregate `EvaluatedCandidates` with a saturating uint add across all inspected ghost rows and OR `NonFinite | CollisionBlocked | CapacityExceeded` into the sink row whether or not a valid snap is selected. Valid snap distance selection still uses the lowest distance row.

Rejected Alternatives: Reading all per-ghost rows again on the main thread was rejected because it forces solver completion and reintroduces CPU work outside the job. Keeping valid-snap-only telemetry was rejected because failed placement is still critical forensic state.

Scalability potential: Low quality aggregates at most low-budget candidate counts per ghost; higher quality increases those counts continuously. The reducer shape remains one bounded pass over ghost rows.

Hardware Impact: Adds one integer add and one OR per ghost row. It prevents false-clean telemetry and preserves dump triggers for non-finite solver states.

## Decision 20 - Reducer Rejects Non-Finite Valid Rows

Problem: A corrupted or future-modified evaluator could mark a snap as valid while carrying a non-finite distance, snapped AUP, alignment dot, or matrix column. The reducer would then select that row and hand a poisoned pose to the active builder path.

Solution: Add `IsFiniteResult()` inside `SelectBestSocketSnapJob` and require finite `DistanceSq`, `AlignmentDot`, `SnappedRootAup`, and all four `float4x4` columns before a valid row can become best. Non-finite valid rows are skipped and OR `NonFinite` into the sink flags.

Rejected Alternatives: Trusting `EvaluateSocketSnappingJob` was rejected because reducer is the authority sink. Main-thread validation after finalization was rejected because the bad row should never become selected authoritative state.

Scalability potential: Low/Middle/High/Ultra all run the same finite gate. Higher quality only increases candidate rows; it does not loosen safety.

Hardware Impact: Adds bounded SIMD finite checks only for rows already marked `ValidSnap`. This is cheaper than allowing NaN propagation into transform, telemetry, and rollback state.

## Decision 21 - CSR Index Faults Are Counted, Not Silent

Problem: An invalid CSR target index previously caused `EvaluateSocketSnappingJob` to `break` out of the bucket. A single stale slot could skip later valid rows and leave telemetry without a fault marker.

Solution: Consume one candidate budget slot as soon as a CSR target row is read. If the resolved target index is outside the socket or AUP arrays, set `NonFinite` and continue within the same bounded budget.

Rejected Alternatives: Keeping `break` was rejected because corrupt sparse rows should be observable. Throwing or completing on main thread was rejected because Burst hot paths must stay fail-closed and bounded.

Scalability potential: Low quality still inspects only low-budget rows, including invalid rows. Higher quality allows more recovery opportunities inside the same bucket without changing the algorithm.

Hardware Impact: Invalid CSR rows cost one branch and one flag OR. The important effect is containment: no silent early abort and no unbounded recovery scan.

## Decision 22 - Snap Query Hash Guards Pending Results

Problem: The active bridge keyed pending and cached snap results only by module scene hash. If the player moved the ghost root, rotated yaw, or changed the active blueprint while a scheduled snap job was still pending, a completed result from the previous query could be accepted for the current preview.

Solution: Add `_shinobuSocketSnapQueryHash` and compute it from scene hash, raw target point, yaw step, active module hash, ghost socket directions, local offsets, and compatibility hashes. `TryFinalizeShinobuSocketSnap()` and `TryUseCachedShinobuSocketSnap()` now require both scene hash and query hash to match before returning a pose.

Rejected Alternatives: Completing the old job immediately when the query changes was rejected because it can block the main thread. Reusing only `_shinobuSocketSnapFrame` was rejected because frame identity does not prove the input ghost pose or blueprint. A managed cancellation token was rejected because Unity jobs cannot consume it safely in this Burst path.

Scalability potential: Low/Middle/High/Ultra all use the same input hash. The hash cost scales with ghost socket count, not target base size or quality budget, so low-end devices avoid stale snap authority without forcing a synchronous solve.

Hardware Impact: Adds a small FNV fold loop over the ghost socket definitions on the managed bridge. It prevents visible snap pops and wrong occupied-socket marking without increasing Burst candidate bandwidth.

## Decision 23 - Ghost Socket Indices Stay Source-Stable

Problem: Ghost hydration packed only finite socket definitions into the Vault ghost socket lane. `SocketSnappingResultDTO.GhostSocketIndex` then referenced the packed row, but `TryApplyShinobuVaultSnapResult()` and `TryMarkShinobuPlacedGhostSocketOccupied()` use that index against the original `BaseModuleTemplate.SocketDefinition[]`. If an earlier ghost definition was skipped, the wrong authored socket could be aligned or marked occupied.

Solution: Preserve source-stable ghost indices by writing row `i` for definition `i`. Non-finite ghost rows are written with `NonFinite | CollisionBlocked`, a safe normal, and a zero CSR range. `EvaluateSocketSnappingJob` now rejects flagged ghost rows before normal/AUP scoring.

Rejected Alternatives: Adding a persistent `GhostSourceIndex` Vault lane was rejected because it adds memory ownership and another alias surface for a problem solvable with stable indices. Continuing to pack valid rows was rejected because it makes later authoring-state mutation unsafe.

Scalability potential: The same row-stability rule applies at every quality weight. Low quality does not waste target-row budget on flagged ghost rows because the evaluator returns before CSR scanning.

Hardware Impact: Invalid ghost definitions cost one DTO write and one early flag test. Valid rows keep the same cost. The fix prevents wrong ghost-socket occupancy without increasing target candidate bandwidth.

## Decision 24 - Target CSR Contains Open Finite Sockets Only

Problem: Target direction CSR previously counted every socket row, including rows already marked `Connected`, `CollisionBlocked`, or non-finite. Low-quality candidate budgets could be exhausted on unavailable target rows before the evaluator reached an open socket in the same direction bucket.

Solution: Add `IsOpenFiniteSocket()` inside `ShinobuSocketConstructionRuntime.BuildSocketDirectionCsr()` and apply it in both the prefix-count and fill passes. Direction buckets now contain only open finite target rows. The evaluator still keeps its runtime guards as a second containment layer.

Rejected Alternatives: Keeping all rows in CSR and filtering only in `EvaluateSocketSnappingJob` was rejected because it preserves memory bandwidth waste and undermines low-quality candidate budgets. Building a second "open socket" CSR lane was rejected because the existing owner-local CSR can represent the valid target subset directly.

Scalability potential: Low quality now spends its 16-row budget on viable open sockets instead of unavailable rows. Middle/High/Ultra still expand the same continuous budget up to 256 without a binary quality switch.

Hardware Impact: On dense bases with many occupied sockets, low-end devices avoid repeated occupied-row reads and branch skips. The cold CSR rebuild pays one finite/flag test per target socket, which is cheaper than paying it every preview query.

## Decision 25 - Socket Direction And Module Hash Fail-Closed

Problem: Authored socket directions outside the canonical 0..5 range could be coerced into legal North-facing sockets by `direction & 7`, `ExtractDirection()` fallback, or `DirectionToNormal()` default. Separately, the active route hashed and emitted `ModuleHashId` directly in several places, so blueprints with `ModuleHashId == 0` could share the same ghost query/job hash even when `TemplateHashId` was distinct.

Solution: Add explicit unmanaged direction validators. `PackAllowedConnectionBitmask()` now emits no direction bit for invalid input; `ExtractDirection()` returns `byte.MaxValue` for zero or multi-bit masks; `AreCompatible()` and `BuildSocketDirectionCsr()` require a single valid direction. Target and ghost hydration mark invalid authored sockets as `NonFinite | CollisionBlocked`; ghost rows remain source-stable but receive zero CSR range. The active query hash, `GhostModuleHash`, and `GhostPreviewDTO.ModuleHash` now use `ResolveShinobuModuleHash()` with `TemplateHashId` fallback.

Rejected Alternatives: Silently quantizing invalid directions to North was rejected because it makes authoring corruption look like valid topology. Skipping invalid ghost rows was rejected because it reintroduces `GhostSocketIndex` drift. Adding another persistent direction-validation buffer was rejected because the existing bitmask and fault flags carry the proof without more Vault ownership.

Scalability potential: Low quality avoids spending its small CSR row budget on invalid authored sockets. Middle/High/Ultra retain the same fail-closed validation while using larger continuous candidate budgets and richer Dear Lie presentation.

Hardware Impact: On i3/MX350-class hardware, invalid rows now stop at a bit test and zero CSR range instead of entering radius/alignment math. The added cold hydration checks are smaller than one doomed socket candidate scan.

## Decision 26 - Dear Lie Pose Cache Invalidates With Query Truth

Problem: `_shinobuHasSnappedPose` could remain true after the ghost root, yaw, scene hash, or active blueprint query changed. The solver cache would reject the stale pose by hash, but `GhostPreviewDTO.Flags` still derived from `_shinobuHasSnappedPose`, so the Dear Lie shader could display snap feedback for a pose that was no longer the current solver truth.

Solution: Add `InvalidateShinobuCachedSnapPose()` and call it when scene/query hash diverges from the cached pose, when the reducer returns no valid snap, when applying the selected snap fails, on unsnap, on placement reset, and on builder reset. Cached distance now uses `float.MaxValue` as an invalid sentinel and `TryUseCachedShinobuSocketSnap()` rejects that sentinel explicitly.

Rejected Alternatives: Letting the old pose visually persist until the next job finishes was rejected because the cinematic fake would lie about authoritative topology. Completing pending jobs on every ghost movement was rejected because it can block the main thread and violates dispatcher ownership.

Scalability potential: Low quality avoids visible fake snap carryover while the smaller candidate budget is still solving. Middle/High/Ultra keep the same hash gate while spending larger budgets on valid current-query snaps.

Hardware Impact: Adds one query-hash equality branch and a few scalar clears on query changes or failed results. It avoids a visual re-snap correction without adding target candidate reads or synchronizing the job chain.

## Decision 27 - One Compatibility Law For Hot And Cold Sockets

Problem: Burst socket matching used `UniversalCompatibilityHash24` semantics inside `AreCompatible()`, while cold authoring occupancy transfer and post-placement marking duplicated sentinel comparisons in `PlayerBuilder`. Even though the current wildcard sentinel is `0u`, duplicating the rule creates a future split-brain risk between authoritative DTO matching and authoring component occupancy.

Solution: Add `AreCompatibilityHashesCompatible(uint lhsHash, uint rhsHash)` in `ShinobuSocketConstructionRuntime` and route both Burst matching and PlayerBuilder cold occupancy scans through it.

Rejected Alternatives: Leaving duplicate checks in `PlayerBuilder` was rejected because compatibility semantics belong to the socket runtime, not the bridge. Adding string comparisons back into the cold path was rejected because all matching should operate on hashed DTO-compatible values.

Scalability potential: Low/Middle/High/Ultra use the same compatibility predicate; quality only changes candidate budget and shader response, not socket truth.

Hardware Impact: The helper is inlined. Cold occupancy scans pay the same comparison count; the value is preventing mismatch between hot solver acceptance and post-place occupied marking.

## Decision 28 - Compatibility Hash Zero Is Reserved

Problem: `UniversalCompatibilityHash24` uses `0u` as the wildcard sentinel. A non-empty compatibility string hashed through 24-bit FNV can theoretically fold to `0`, making a specific authored socket type behave as universal compatibility.

Solution: Remap non-empty string hash result `0` to `1` in `HashCompatibility()`. Empty/null strings still intentionally return `UniversalCompatibilityHash24`.

Rejected Alternatives: Increasing the compatibility field width was rejected because `AllowedConnectionBitmask` is already packed into the 64-byte socket DTO contract. Accepting the collision was rejected because it silently widens authority and could dock incompatible module faces.

Scalability potential: The rule is independent of quality weight. Low/Middle/High/Ultra all keep identical compatibility semantics; quality only affects candidate budget and visual fake intensity.

Hardware Impact: One extra compare on cold string hashing and editor/import/hydration paths. Burst hot loops operate on already-packed hashes and pay no extra per-candidate cost.

## Decision 29 - Builder Signals Use The Same Module Hash Fallback

Problem: Query hash, `GhostPreviewDTO`, and Burst `GhostModuleHash` used `ResolveShinobuModuleHash()`, but `ConstructionPreviewSignal.ModuleHash`, construction validation payloads, and commit-side source/module signals still emitted direct `ModuleHashId`. If that field is zero, render, validation, acoustic, or flora consumers could see a generic zero module identity while the socket solver used the template fallback.

Solution: Route preview signal, construction validation payload, acoustic source fallback, and flora exclusion module hash through `ResolveShinobuModuleHash()`.

Rejected Alternatives: Leaving these signals as direct `ModuleHashId` was rejected because they are proof artifacts for preview/placement state. Adding second template hash fields was rejected because DTO/signal layouts are fixed and the fallback gives one canonical identity.

Scalability potential: Low/Middle/High/Ultra all publish one canonical module identity. Quality weight still affects Dear Lie activity and candidate budget, not module identity.

Hardware Impact: One cold fallback branch when publishing preview, validation, and commit signals. No Burst candidate cost.

## Decision 30 - Dear Lie Signal Reaches Active Preview Shader

Problem: The socket solver wrote `GhostPreviewDTO.DearLieDampen`, and `PlayerBuilder` raised `FlagDearLieActive`, but the active `HectonBlueprintPreviewBatch` ignored that scalar and the main `Hecton8/Fabrication/BlueprintWireInstanced` shader had no snap dampen properties. The fake existed in a secondary material path, but the common batched preview could render with no mechanical lock response.

Solution: Reuse padding inside the 128-byte `ConstructionPreviewSignal` for `DearLieDampen`, `GlobalQualityWeight`, and `DearLieWiggleSpeed` at aligned offsets 96, 100, and 104, with `ModularBaseConstructionValidator.ValidateStructLayout()` now gating those offsets. `PlayerBuilder` fills those values from the current socket tuning and solved dampen. `HectonBlueprintPreviewBatch` consumes the signal, tracks the snap envelope by result/module hash, decays dampen continuously over 0.08..0.22 seconds using the quality curve, and writes `_H8SnapDampen`, `_H8SnapWiggleSpeed`, and `_H8GlobalQualityWeight` into the preview material. The instanced blueprint wire shader now applies the same normal-offset sine displacement as the Dear Lie hologram shader, with guarded normal normalization in both shader paths. Cold fallback proxy materials initialize `_H8SnapDampen` to `0`, so the effect is event-driven rather than permanently active.

Rejected Alternatives: Expanding `BlueprintPreviewInstance` was rejected because the existing draw path only consumes matrices and would bloat the 64-byte preview DTO without a per-instance shader buffer. Instantiating a special snapped preview prefab was rejected because it is exactly the object-hierarchy churn the task forbids. Leaving material values at creation time was rejected because runtime `GlobalQualityWeight` and snap dampen would not be event-correct.

Scalability potential: Low quality shortens the visual envelope and scales amplitude through the same smooth quality polynomial, so weak devices see a small tactile pulse. Middle quality keeps the pulse readable without adding candidate work. High and Ultra allow the full dampen amplitude and wiggle speed from tuning while the mathematical socket snap remains instant.

Hardware Impact: On i3/MX350, the change adds up to three material scalar writes in the presentation path and one sine-based vertex offset only when dampen is non-zero. It avoids CPU-side interpolation, door prefab instantiation, and snap-animation GameObject state.

## Decision 31 - Snap Result Sink Rejects Invalid Directions

Problem: Upstream hydration, CSR, and Burst matching already reject invalid authored directions, but `TryApplyShinobuVaultSnapResult()` still converted an unknown target direction byte to `ModuleSocketDirection.North` through the final enum helper. If stale/corrupt result data ever crossed the reducer boundary, the final pose could be calculated against a legal North rotation instead of failing closed.

Solution: Replace the defaulting helper with `TryToShinobuSocketDirection()`. The final snap application now rejects invalid target direction bytes and invalid ghost socket directions before calculating `ModuleSocketTopology.RotationFromDirection()` or mutating cached snap state.

Rejected Alternatives: Trusting the CSR invariant was rejected because the snap application method is the authority sink that touches scene pose and occupancy markers. Keeping the North fallback was rejected because it makes bad authoring data look like a valid docking face.

Scalability potential: Low/Middle/High/Ultra all keep the same fail-closed sink behavior. Quality only changes how many CSR rows are inspected before a valid row reaches this sink.

Hardware Impact: Two byte-range checks on accepted snap rows only. On low-end hardware this is below measurement noise and prevents wrong-pose correction work after a corrupt result.

## Decision 32 - CSR Missing-Data Does Not Fall Back To Linear Scan

Problem: `EvaluateSocketSnappingJob` still had defensive fallbacks that undermined the CSR contract. Missing ghost CSR ranges returned `0..TargetCount`, and missing/short `SocketCsrTargetIndices` treated the CSR slot as a direct target socket index. Under damaged or undersized CSR buffers, the solver could silently re-enter an O(N) scan shape.

Solution: Make the CSR range and target-index lanes mandatory for this job. Missing ghost ranges or missing target-index lanes write `CapacityExceeded` and return. Short target-index arrays consume the inspected budget slot, set `CapacityExceeded`, and continue without direct target reads.

Rejected Alternatives: Keeping the fallback was rejected because it hides buffer ownership faults and violates the task's CSR graph requirement. Completing on the main thread was rejected because the solver must stay scheduled and fail-closed.

Scalability potential: Low quality now remains bounded even when the CSR lane is malformed; it cannot accidentally scan all target sockets. Middle/High/Ultra still use the same CSR path and only increase inspected CSR rows when the lanes are valid.

Hardware Impact: Adds one target-index bounds check per inspected CSR row. In the failure case it saves up to `TargetCount` socket/AUP reads per ghost by refusing the direct scan.

## Decision 33 - Dear Lie Envelope Clears With Preview Lifetime

Problem: The active preview batch keyed the Dear Lie pulse by result hash and module hash. If the preview disappeared without an inactive signal, then later returned to the same socket/module, the stored hash could keep the old start time and suppress a fresh snap pulse.

Solution: Add `ResetDearLieEnvelope()` and call it when active preview count becomes zero and when previews are explicitly cleared. The reset clears dampen, quality, wiggle speed, result hash, module hash, and active state without changing the 64-byte `BlueprintPreviewInstance` or the 128-byte signal layout.

Rejected Alternatives: Letting time decay alone was rejected because the hash key would still suppress a new envelope for the same result. Adding per-instance Dear Lie fields was rejected because the single active builder preview only needs material scalars.

Scalability potential: Low quality still gets a short pulse after preview re-entry; Middle/High/Ultra can replay the full tuned envelope. No binary switch is introduced.

Hardware Impact: Seven scalar writes on preview clear only. No Burst candidate cost and no additional material updates until the preview draws again.

## Decision 34 - ModuleTemplate Ghost Prefab Spawn Is Bypassed

Problem: `PlayerBuilder.SpawnGhost()` still used `ObjectPoolManager.Spawn(activeBuildable.ghostPrefab)` whenever an authored ghost prefab existed. For buildables with `ModuleTemplate`, that preserved a preview-prefab hierarchy path even though SHINOBU socket truth already comes from template socket definitions and Vault `GhostPreviewDTO`.

Solution: Route every `ModuleTemplate` buildable through `ConstructionRuntimeProxyFactory.TryAcquireGhostProxy()` and keep the `ghostPrefab` pool branch only for non-template buildables. The reusable proxy is a cold singleton presentation shell; active socket matching continues to read `BaseModuleTemplate.SocketDefinitions`, `GhostPreviewDTO`, and CSR lanes, not preview-prefab `ModuleSocket` components.

Rejected Alternatives: Keeping authored ghost prefabs for nicer previews was rejected because Task 02 explicitly asks for data-driven preview authority during active snapping. Falling back to the prefab if proxy acquisition fails was rejected because it would hide a broken `ModuleTemplate` preview route and reintroduce object hierarchy dependence.

Scalability potential: Low uses the same cheap proxy shell while the Vault preview and shader signal carry the snap truth. Middle/High/Ultra can still spend quality budget on Dear Lie shader response and batched preview rendering without instantiating a preview prefab for socket modules.

Hardware Impact: Avoids one preview prefab pool spawn/despawn cycle per armed `ModuleTemplate` buildable and removes ghost-prefab hierarchy traversal from the socket-module preview route. No Burst candidate cost changes.
