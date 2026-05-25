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

Superseded by Decision 50. Current SHINOBU occupancy truth is committed through `SocketStateDTO.ConnectionStatus` and `SocketConnectionPairDTO` rows in Vault; the authoring-component transfer described below is historical only.

Problem: Historical target socket rebuilds wrote DTO rows from `BaseModuleTemplate.SocketDefinition` data, but occupied state lived on cold `ModuleSocket` components after placement. That could erase `IsOccupied` truth and let the Burst evaluator consider an already consumed socket.

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

## Decision 34 - ModuleTemplate Preview Is Data-Only

Problem: `PlayerBuilder.SpawnGhost()` still used `ObjectPoolManager.Spawn(activeBuildable.ghostPrefab)` whenever an authored ghost prefab existed. For buildables with `ModuleTemplate`, that preserved a preview-prefab hierarchy path even though SHINOBU socket truth already comes from template socket definitions and Vault `GhostPreviewDTO`.

Solution: Make `SpawnGhost()` data-only for the builder preview. It now releases any legacy ghost object, sets `_builderGhostPreviewActive`, stores pose/rotation/scale fields, and leaves `_currentGhostObj` null. Active socket matching continues to read `BaseModuleTemplate.SocketDefinitions`, builder preview pose fields, Vault `GhostPreviewDTO`, and CSR lanes, not preview-prefab `ModuleSocket` components.

Rejected Alternatives: Keeping authored ghost prefabs for nicer previews was rejected because Task 02 explicitly asks for data-driven preview authority during active snapping. Falling back to `ConstructionRuntimeProxyFactory.TryAcquireGhostProxy()` was rejected for the active preview because it still creates/owns a GameObject shell; that factory remains only for legacy/no-prefab placed module paths.

Scalability potential: Low uses only pose/scale fields, Vault preview rows, and minimal shader feedback. Middle/High/Ultra can still spend quality budget on Dear Lie shader response and batched preview rendering without instantiating a preview prefab for socket modules.

Hardware Impact: Avoids one preview prefab pool spawn/despawn cycle plus ghost hierarchy setup per armed `ModuleTemplate` buildable and removes ghost-prefab hierarchy traversal from the socket-module preview route. No Burst candidate cost changes.

## Decision 35 - Builder Ghost Validation Uses Dispatcher Fence

Problem: The builder holography/SDF validation bridge scheduled Burst jobs but previously forced completion inside the active validation call. That kept a hidden main-thread stall next to the socket snap route and made pending validation results weakly owned by pose only.

Solution: `TryRunBuilderGhostBurstValidation()` now schedules `BuildBuilderGhostStateJob`, chains `ValidateBuilderGhostPlacementJob`, registers the final handle with `H8Memory`, and returns immediately. The next builder validation tick calls `DispatcherJobFence.TryFinalizeCompleted` and consumes the `BuilderGhostStateDTO` only if the query hash still matches. That hash includes module hash, preview pose, rotation, proxy bounds center/size, and snap/DearLie flags. `SetActiveBuildable()`, `OnDestroy()`, and `ResetBuilderState()` complete the validation handle only on lifecycle teardown boundaries.

Rejected Alternatives: Keeping `TryComplete(forceComplete:true)` in the active validation route was rejected because it can block the builder tick on SDF/bounds proof work. Dropping stale results by pose only was rejected because `_isSnapped` can change the presentation flags without changing pose. Creating a second private NativeArray for validation output was rejected because the owner-local Vault `BuilderGhostStateDTO` lane already exists.

Scalability potential: Low quality can skip one-frame-late validation without blocking placement preview; Middle/High/Ultra keep the same scheduled path and can spend saved main-thread time on richer Dear Lie holography. No binary quality switch was introduced.

Hardware Impact: On i3/MX350-class hardware, the worst visible cost removed is a synchronous fence wait during active preview validation. Added cost is a small FNV fold sequence and one query-hash comparison per validation tick.

## Decision 36 - Builder Validation Uses Cached Vault Only

Problem: The builder ghost validation bridge still contained a live `GlobalRegistry.DataVault` fallback inside `TryRunBuilderGhostBurstValidation()`. That is not inside a Burst loop, but it is still an active preview route and weakens the Global Authority boundary already enforced by the socket snap bridge.

Solution: Route builder validation through `TryResolveShinobuSocketVault(out IDataVault vault)`. The only SHINOBU DataVault binding for `PlayerBuilder` remains in the cold `BindRuntimeReferences()` path, where the Vault is also initialized for construction validation.

Rejected Alternatives: Keeping the active fallback was rejected because a missing cached vault should fail closed instead of silently polling the registry in the frame path. Re-resolving the registry every preview tick was rejected because it hides boot-order defects and adds service-locator traffic to a path that already has an owner-local cache.

Scalability potential: Low/Middle/High/Ultra all use the same cached-vault gate. Quality weight continues to affect candidate budgets and shader response, not service lookup behavior.

Hardware Impact: Removes one possible service-locator property read per builder validation attempt when `_shinobuSocketVault` is missing. The larger impact is architectural: no active preview fallback can mask a cold boot/cache failure.

## Decision 37 - Preview Alpha Uses Current Validation Flags

Problem: `HectonBlueprintPreviewBatch.WriteStateRow()` selected `BuilderGhostVisualDTO.Alpha` from `_lastPreviewAllowed`, but that field is updated after the current `ConstructionPreviewSignal` row is written. The current frame could therefore upload the previous signal's valid/invalid alpha even when SDF or bounds truth changed.

Solution: Add `IsBuilderGhostValid(uint flags)` and derive alpha directly from the current `BuilderGhostValidationFlags` row after finite sanitization. Valid requires `Valid` and no `SdfBlocked`, `BoundsBlocked`, or `NonFinite`. After `WriteStateRow()`, `ConsumeConstructionPreviewSignals()` now reads the written `BuilderGhostStateDTO` for telemetry SDF sign and `_lastPreviewAllowed`, so non-finite corrections made inside the writer become the material/black-box truth.

Rejected Alternatives: Updating `_lastPreviewAllowed` before `WriteStateRow()` was rejected because `SetPreview()` and other writers also need current-row truth without relying on external mutable state. Leaving a one-frame visual mismatch was rejected because the Dear Lie fake must never contradict the current validation payload.

Scalability potential: Low/Middle/High/Ultra all use the same bitmask predicate. Quality still changes material response and candidate budgets, not validity truth.

Hardware Impact: Adds one small bitmask predicate plus one written state row read per preview row. It prevents one-frame invalid/valid alpha drift and stale telemetry sign without touching Burst candidate work.

## Decision 38 - Preview Scale Must Be Positive On Every Axis

Problem: `HectonBlueprintPreviewBatch.WriteStateRow()` treated scale as valid when any axis was positive. A row with one positive axis and one zero or negative axis could be clamped to `0.001` for upload while still retaining valid flags.

Solution: Change the finite/validity gate to `math.all(scale > 0f)`. Invalid axes now force the writer to clear `Valid`, set `NonFinite`, and upload the tiny safe fallback matrix only as an invalid visual.

Rejected Alternatives: Keeping silent clamp behavior was rejected because it hides malformed preview geometry. Adding branchy per-axis repair was rejected because a malformed payload should fail closed and become visible as invalid, not be normalized into truth.

Scalability potential: Low/Middle/High/Ultra all use the same validity predicate. Quality still controls visual intensity, not whether malformed geometry is accepted.

Hardware Impact: Same SIMD comparison width as the prior check; stricter predicate only. No extra memory traffic.

## Decision 39 - Validated Builder Visual Mirrors Final Flags

Problem: `BuildBuilderGhostStateJob` wrote `BuilderGhostVisualDTO.Flags` before `ValidateBuilderGhostPlacementJob` performed SDF and bounds checks. The state row could become `SdfBlocked`, `BoundsBlocked`, or `NonFinite` while the GPU-facing visual row still carried the pre-validation flag set.

Solution: Add the `BuilderGhostVisualDTO` Vault lane to `ValidateBuilderGhostPlacementJob` and update the matching visual row's `Flags` and `Alpha` after final validation. Alpha is resolved from the final flag predicate and clamped against the existing valid/invalid color alpha values.

Rejected Alternatives: A separate visual-sync job was rejected because it would add another dispatcher edge and duplicate the validation predicate. Leaving the pre-validation visual row was rejected because it lets the shader-facing proof artifact diverge from the black-box state row.

Scalability potential: Low/Middle/High/Ultra all use the same final flag predicate. Quality still scales Dear Lie amplitude and candidate budgets, not validation truth.

Hardware Impact: Adds one 64-byte visual row read/write and one bitmask predicate per builder validation output row. It avoids a stale-valid shader payload without adding PhysX, prefab work, or a main-thread fence.

## Decision 40 - Holography Dump Uses SHINOBU_217 Ownership

Problem: `HolographyDumpPath` pointed to a foreign-agent dump target, so a SHINOBU_217 holography fault could produce a postmortem artifact under the wrong agent ID.

Solution: Route holography black-box dumps to `Docs/AgentLogs/Dump_SHINOBU_217_Holography.bin`. The main construction socket telemetry still uses `Dump_SHINOBU_217.bin`.

Rejected Alternatives: Reusing `Dump_SHINOBU_217.bin` for both telemetry rings was rejected because `ConstructionSocketTelemetryEntry` and `HolographyTelemetryEntry` have different fixed binary layouts. Keeping the foreign-agent path was rejected because it breaks ownership and forensic traceability.

Scalability potential: Low/Middle/High/Ultra are unaffected; this is exceptional-path crash evidence only.

Hardware Impact: No hot-path cost. The only runtime effect is the target path used if the existing non-finite dump branch fires.

## Decision 41 - Reused ModuleSocket Lists Do Not Grow From 8

Superseded by Decision 50. SHINOBU snap occupancy no longer uses the `ModuleSocket` authoring bridge; the current route writes `SocketStateDTO.ConnectionStatus` and `SocketConnectionPairDTO` rows directly in Vault.

Problem: During the migration period, SHINOBU still reads cold `ModuleSocket` authoring components to transfer occupied-socket truth into `SocketStateDTO`. The reusable list buffers were created with capacity 8, so dense modules could trigger `List<T>` growth during target-cache rebuild or post-place occupancy marking.

Solution: Pre-size `_ghostSocketBuffer` and `_shinobuTargetSocketBuffer` to `ShinobuSocketConstructionRuntime.GhostSocketCapacity` so their managed backing arrays match the fixed SHINOBU ghost socket lane.

Rejected Alternatives: Using the array-returning `GetComponentsInChildren<T>()` overload was rejected because it allocates every call. Removing the authoring component scan entirely was rejected until the habitat graph rebuild feeds occupied truth directly from Vault.

Scalability potential: Low/Middle/High/Ultra all keep the same cold authoring bridge. Quality weight still controls candidate work and Dear Lie presentation, not component-buffer capacity.

Hardware Impact: Avoids one possible managed list resize allocation on dense modules in the cold cache-refresh path. No Burst candidate cost changes.

## Decision 42 - Builder SDF Validation Uses Continuous Math LOD (Superseded)

Superseded by Decision 43. The later Global Systems Doctrine and binary ledger clarify that `GlobalQualityWeight` must not change builder placement truth, so quality-scaled SDF corner reduction is rejected for validation authority.

Problem: Builder holography SDF hydration always sampled all eight bounds corners even when `GlobalQualityWeight` was low. That contradicted the continuous scalability rule and made low-end preview validation pay full corner-sampling cost before the scheduled Burst validation job.

Solution: Add `ResolveBuilderGhostSdfSampleCount()` and `ResolveBuilderGhostCornerIndex()` to the SHINOBU runtime. CPU hydration and `ValidateBuilderGhostPlacementJob` now share a deterministic opposite-corner sample order. The count scales smoothly from 2 to 8 corners through `GlobalQualityWeight`, and unsampled bytes are explicitly reset to clear before hydration so stale data cannot leak into Burst validation. Holography telemetry now records the actual sampled corner count.

Rejected Alternatives: A binary low/high switch was rejected because HECTON-8 requires continuous quality curves. Sampling only the first N raw corner indices was rejected because low quality would inspect one side of the bounds volume instead of opposite spatial extremes. Leaving the fixed eight-corner path was rejected because it wasted CPU work on weak devices while the terrain validator already provides separate placement authority.

Scalability potential: Low quality samples two opposing corners as a cheap presentation proof. Middle quality adds paired corners smoothly. High and Ultra sample all eight corners while the Dear Lie visual fake remains shader-driven and instant.

Hardware Impact: On i3/MX350-class hardware, low-quality builder holography SDF hydration drops from eight SDF calls to two. No layout, Vault ID, or shader payload changed.

## Decision 43 - Builder SDF Validation Uses All-Eight Truth

Problem: The previous SDF Math LOD rationale treated builder holography SDF validation as presentation-only, but the result feeds `BuilderGhostValidationFlags.SdfBlocked`, `BoundsBlocked`, `NonFinite`, and placement UI validity. Reducing corner checks by `GlobalQualityWeight` would therefore let quality change placement truth, violating the Global Systems Doctrine.

Solution: Remove the quality-scaled SDF sample-count route from the validate job API. CPU hydration and `ValidateBuilderGhostPlacementJob` now always use `BuilderGhostSdfCornerCount` with the shared `ResolveBuilderGhostCornerIndex()` deterministic order. Holography telemetry records the constant all-eight corner proof; quality stays limited to shader/material presentation and socket search/candidate budgets.

Rejected Alternatives: Keeping a 2-corner low-quality path was rejected because it could miss blocked corners and approve placement on weak hardware. Adding a separate "presentation-only" SDF lane was rejected because the existing result is already consumed as placement evidence. Removing the corner-order helper was rejected because CPU hydration and Burst validation still need identical deterministic ordering.

Scalability potential: Low/Middle/High/Ultra all share identical placement validation truth. Low devices still shed work through CSR candidate budget, search radius, and shader envelope cost. High and Ultra spend saved socket-search headroom on stronger Dear Lie presentation without changing SDF acceptance.

Hardware Impact: Restores up to six SDF sample calls on low quality compared with the superseded idea, but prevents hardware-dependent build legality. No BufferID, DTO layout, shader payload, or Vault descriptor changed.

## Decision 44 - Read Accessor Purity For Socket Bridge

Problem: The active SHINOBU socket bridge used read-looking names while doing mutating work. `TryResolveShinobuSocketAlignment()` could hydrate Vault rows, schedule jobs, finalize prior results, and update cached pose state. `TryResolveVaultViews()` also called `InitializeVault()`, which can request or grow Vault descriptors. That violated the Global Systems Doctrine for `Get*`, `TryGet*`, `Resolve*`, and `Read*` APIs.

Solution: Rename the mutating active bridge to `TryUpdateShinobuSocketAlignment()` and `TryUpdateShinobuSocketAlignmentFromVault()`. Rename the lifecycle binder to `BindRuntimeReferences()` and move SHINOBU `InitializeVault()` there beside the cached DataVault binding. Make `TryResolveVaultViews()` a pure phase-local handle resolution method with no descriptor requests. Rename descriptor acquisition helper `ResolveHandle<T>()` to `EnsureVaultHandle<T>()`. `GetCachedConstructionManager()` now only returns the cached field and does not lazily poll the registry.

Rejected Alternatives: Leaving the names unchanged was rejected because future callers would treat scheduling and Vault hydration as a read. Keeping `InitializeVault()` inside `TryResolveVaultViews()` was rejected because active validation and snapping call that method. Keeping lazy construction-manager registry fallback was rejected because active routes must fail closed if cold binding failed.

Scalability potential: Low/Middle/High/Ultra all use the same cold dependency binding and pure active read gates. Quality still scales candidate/search budgets and Dear Lie visuals, not dependency resolution or Vault allocation behavior.

Hardware Impact: Removes possible active-route registry fallback and descriptor request work. Main gain is architectural: no read-looking accessor hides scheduling, Vault allocation/growth, descriptor acquisition, or service lookup.

## Decision 45 - Cold Service Binders Are Explicit Ensure Calls

Problem: `ResolvePlayerContext()`, `ResolveEnvironmentContext()`, `ResolveConstructionManager()`, and `ResolveModuleCatalog()` were cold bind helpers, but the first two can call `EnsureRuntimeInstance()` and `InitializeService()`. That made `Resolve*` names semantically false under the new read-accessor purity doctrine.

Solution: Rename them to `EnsurePlayerRuntimeContext()`, `EnsureEnvironmentRuntimeContext()`, `EnsureConstructionManager()`, and `EnsureModuleCatalog()`. `BindRuntimeReferences()` remains the only SHINOBU cold binder that calls them; active socket alignment continues to use cached fields and pure Vault view resolution.

Rejected Alternatives: Leaving the names as `Resolve*` was rejected because a future caller could move them into an active frame path and think they were pure reads. Splitting service creation into a new global bootstrap was rejected because that crosses core ownership and is not required for the socket adaptor patch.

Scalability potential: Low/Middle/High/Ultra all keep dependency boot cost in the same cold phase. Quality curves remain limited to candidate budget, search radius, and Dear Lie presentation.

Hardware Impact: No direct runtime microsecond claim. The value is preventing hidden service initialization from entering preview/snap hot paths.

## Decision 46 - Construction Root AUP Comes From Socket Vault First

Problem: `ResolveConstructionRootAup()` used a read-looking name while scanning `ConstructionManager.SpawnedModules` and module transforms to select a base root for construction validation payloads. That repeated object-world reads despite SHINOBU already hydrating `ConstructionSocketModuleDTO.RootAup` into Vault.

Solution: Delete `ResolveConstructionRootAup()`. Vault target preparation now captures the first finite module root AUP into a local fallback, and construction validation asks `TryUpdateConstructionRootAupFromSocketVault()` for the root. That helper reads the Vault module lane first, updates the local fallback only from Vault data, and uses `BuildFallbackConstructionRootAup(previewPosition)` only when no Vault module root exists.

Rejected Alternatives: Keeping the spawned-module scan was rejected because it contradicts the Vault-owned AUP route and read-accessor search prohibition. Cache-first root lookup was rejected because a stale cache could hide topology churn before the Vault lane is reread.

Scalability potential: Low/Middle/High/Ultra all use the same root authority route. Quality continues to scale socket candidate count, search radius, and Dear Lie presentation, not base-root selection or placement truth.

Hardware Impact: Avoids a spawned-module transform scan per validation payload when Vault module rows exist. The remaining scan is over contiguous `ConstructionSocketModuleDTO` NativeArray rows and allocates no managed memory.

## Decision 47 - Preview Batch Vault Handles Are Cold-Bound

Problem: `HectonBlueprintPreviewBatch` still had `TryEnsureAndResolveBuffers()` in active preview paths. That method could reach `GlobalRegistry.DataVault` and request Vault handles through `GetBufferHandle`, contradicting the claim that active preview upload consumes cached Vault state only.

Solution: Rename the allocation path to `EnsureBuffersCold()` and call it from `Awake()` and play-mode `OnEnable()`. Active `SetPreview()`, pending-upload finalization, gizmo read, and `ConsumeConstructionPreviewSignals()` now use `TryReadCachedBuffers()` only. If the cold owner phase did not bind the Vault handles, the active path fails closed instead of polling the registry or requesting descriptors.

Rejected Alternatives: Keeping active lazy allocation was rejected because it hides registry and Vault descriptor work in the frame path. Moving the buffer acquisition into a global bootstrap was rejected because this component already owns the builder-holography lanes and can bind them during lifecycle cold setup.

Scalability potential: Low/Middle/High/Ultra all use the same cached Vault lanes. `GlobalQualityWeight` continues to scale presentation/search cost, not buffer ownership or authority routing.

Hardware Impact: Removes possible active-frame registry and descriptor acquisition work. No profiler number claimed; the patch is a route-purity fix.

## Decision 48 - Placement SDF Truth Uses Fixed Probe Count

Problem: `TryFindVoxelSdfIntersection()` and `ModularBaseConstructionValidator.ValidatePlacement()` still used `ResolveTerrainProbeCount(settings.GlobalQualityWeight)` / `ResolveProbeBudget()`. That allowed terrain intersection legality to change from 1 to 9 probes by hardware quality.

Solution: Add `TerrainProbeTruthCount = 9` and use it for both the PlayerBuilder terrain probe route and the validator job route. Remove the private quality-scaled probe budget helper so there is no stale quality-dependent terrain legality path.

Rejected Alternatives: Keeping a low-quality one-probe path was rejected because it can approve a module that intersects terrain on another machine. Adding a second presentation-only probe route was rejected until a concrete visual consumer exists; current probes feed placement truth.

Scalability potential: Low/Middle/High/Ultra share the same terrain placement truth. Quality still scales socket CSR budget, search radius, and Dear Lie shader/material envelope; it does not change DTO layout, save identity, or placement authority.

Hardware Impact: Low quality may pay up to eight more SDF sample calls than the rejected path. The cost is bounded to nine AABB probes and prevents hardware-dependent build legality.

## Decision 49 - Scene Hash Stops Sampling Runtime Transforms

Superseded by Decision 53. Active SHINOBU topology hashing now derives from Vault counters and `ConstructionSocketModuleDTO` rows only; the object-identity hash described below was an intermediate step.

Problem: The socket target scene hash previously included module transform position and rotation. Origin shifts and presentation-only transform adjustments can change those values even though the authoritative socket topology and AUP rows in Vault did not change.

Solution: The intermediate solution computed scene hash from module count and stable scene object identity only. Decision 53 removes that remaining scene identity input; current source computes topology hash from Vault counters and module rows.

Rejected Alternatives: Keeping transform hashing was rejected because it made runtime object transforms compete with Vault AUP authority. Hashing only module count was rejected because object replacement with the same count should still invalidate target hydration.

Scalability potential: Low/Middle/High/Ultra all avoid false topology rebuilds during origin-shift or visual-only transform updates. Quality curves remain limited to candidate/search/presentation cost.

Hardware Impact: Removes transform vector/quaternion hash reads during cache validation and avoids cold target-Vault rebuilds caused only by runtime transform drift.

## Decision 50 - SHINOBU Occupancy Commits Through Vault Pairs

Problem: The prior SHINOBU snapped-placement path still marked occupied sockets through `ModuleSocket` authoring components after placement. That meant the active snap route depended on scene component scans and local component flags instead of one unmanaged authority route. It also left target rebuilds dependent on authoring occupancy state rather than durable Vault connection records.

Solution: Remove the SHINOBU `GetComponentsInChildren<ModuleSocket>` occupancy bridge and replace it with `TryCommitShinobuSnapOccupancy()`. The current commit validates connection-pair capacity, socket capacity, and placed socket count before mutating rows; Decision 53 removed the scene-index lookup and writes `SceneModuleListIndex = -1`. It appends `ConstructionSocketModuleDTO` and `SocketStateDTO` rows for the placed module, marks the target socket and consumed ghost socket `Connected`, writes a `SocketConnectionPairDTO`, updates `Counters[4]`, replays connection pairs into socket state, and rebuilds CSR. If commit fails after the module has already been spawned, cached topology hash/count/root AUP are invalidated so the next snap pass cannot reuse stale rows.

Rejected Alternatives: Keeping component marking was rejected because `ModuleSocket` state is not the SHINOBU authority route and requires managed scene traversal. Marking only the current target/ghost rows without a connection-pair DTO was rejected because a later target rebuild would lose durable occupancy. Returning success after partial capacity failure was rejected because it leaves `SocketStateDTO.ConnectionStatus`, `ConstructionSocketConnections`, and CSR disagreeing.

Scalability potential: Low, Middle, High, and Ultra all share identical occupancy truth and DTO layout. `GlobalQualityWeight` still scales snap candidate/search budgets and Dear Lie presentation only; it does not change connection identity, socket count, or authority route.

Hardware Impact: Removes two managed component scans/list clears from each SHINOBU snapped placement. Adds one 32-byte `SocketConnectionPairDTO` write and bounded CSR rebuild on placement commit; no active per-frame scan is added.

## Decision 51 - Black-Box Dumps Avoid Managed Mirror Buffers

Problem: SHINOBU socket, builder-holography, and construction-validation telemetry dump paths copied full NativeArray telemetry rings into managed `byte[]` buffers before writing to disk. The paths are fault-only, but they still allocated dump-sized managed mirror buffers at exactly the moment the system is trying to preserve forensic state. Construction validation also still pointed at the foreign `Dump_SHINOBU_67.bin` dump name.

Solution: Route socket and holography dump APIs through `DumpNativeRingToFile<T>()`, and make `ModularBaseConstructionValidator.DumpTelemetry()` use the same `ReadOnlySpan<byte>` pointer-to-`FileStream` write shape. Construction validation now writes `Dump_SHINOBU_217_ConstructionValidation.bin`; socket and holography dump paths remain `Dump_SHINOBU_217.bin` and `Dump_SHINOBU_217_Holography.bin`.

Rejected Alternatives: Keeping `File.WriteAllBytes()` was rejected because it requires a managed byte buffer. Allocating a persistent private staging array was rejected because SHINOBU does not own persistent private arrays outside Vault. Merging socket, holography, and construction-validation dumps was rejected because their 64-byte rows have different schemas.

Scalability potential: Low, Middle, High, and Ultra share the same fixed 300-row telemetry rings and dump schemas. `GlobalQualityWeight` may scale optional telemetry cadence elsewhere, but it does not change dump row size, file identity, or black-box ownership.

Hardware Impact: Avoids one 19.2 KB managed allocation for each 300-row 64-byte dump. The remaining file handle allocation and disk write are fault-path only, not frame-lane work.

## Decision 52 - Construction Validator Jobs Are Deterministic

Problem: `BurstGridValidationJob`, `LogisticsGraphSpliceJob`, and `DeconstructionConnectivityJob` used `FloatMode.Fast`. Those jobs feed placement validity, construction graph splice decisions, and deconstruction connectivity truth. That is rollback-visible state, not a presentation-only approximation.

Solution: Switch all three `BurstCompile` attributes to `FloatMode.Deterministic` while retaining `CompileSynchronously = true` and `FloatPrecision.Standard`.

Rejected Alternatives: Keeping `FloatMode.Fast` was rejected because ARM64/x86 drift in placement or connectivity decisions can desync co-op rollback. Adding a separate low-quality validator was rejected because `GlobalQualityWeight` must not change gameplay truth or authority route.

Scalability potential: Low, Middle, High, and Ultra share the same validator truth. Quality curves remain on snap candidate budgets and Dear Lie visuals only.

Hardware Impact: Fast-math ALU wins are intentionally given up on validator truth. The cost is bounded to placement/connectivity jobs, not every frame of presentation.

## Decision 53 - Read Facades And Vault-Only Active Snap Source

Problem: The construction validator exposed read-looking methods that could request Vault descriptors, and the active `PlayerBuilder` SHINOBU snap bridge still used `ConstructionManager.SpawnedModules` to hydrate target socket rows from scene objects. The placement path also retained a legacy `_snappedSocket.SetOccupied(true)` component-authority branch.

Solution: Split construction-validation access into cold `Ensure*` methods and active `TryRead*` methods, cache object pool/deconstruction/audio services during `BindRuntimeReferences()`, and remove the unused public `AllocateRequestScratch()` NativeArray allocator. The SHINOBU snap bridge now derives topology hash from Vault counters, module rows, and connection-pair rows, then prepares CSR from pre-published `SocketStateDTO`/AUP rows; if those rows are absent, snapping fails closed. Snapped placement now always writes Vault occupancy through `TryCommitShinobuSnapOccupancy()` and uses `SceneModuleListIndex = -1` for new rows because scene-list identity is not snap truth.

Rejected Alternatives: Keeping lazy `GetBufferHandle` behind `TryResolve*` was rejected because read accessors must be pure. Keeping active `SpawnedModules` hydration was rejected because it reads managed scene authority inside the snap route. Keeping `ModuleSocket.SetOccupied` as a fallback was rejected because it creates a second occupancy owner.

Scalability potential: Low, Middle, High, and Ultra all consume the same Vault-owned socket truth and DTO layout. `GlobalQualityWeight` continues to scale candidate budget, search radius, and Dear Lie presentation only; it does not alter socket ownership, save identity, or placement validity.

Hardware Impact: Removes active scene-list traversal, `ModuleMarker` lookup, runtime transform reads, and component occupancy mutation from the SHINOBU snap/placement route. No profiler microseconds are claimed; static evidence only until the Core.Memory compile wall is cleared.

## Decision 54 - Occupied Cell Truth Reads Vault Modules Only

Problem: `TryFindOccupiedConstructionGridCell()` still pulled `ConstructionManager.SpawnedModules`, walked `GameObject`/`Transform` state, and used PlayerBuilder to hydrate `ConstructionBuilderOccupancy` scratch rows before checking a candidate cell. That contradicted the SHINOBU claim that active placement truth was Vault-owned and scene-list free. `ConstructionBuilderOccupancy` was not independent authority because the only active publisher was the same PlayerBuilder method.

Solution: Replace the method with `TryFindOccupiedConstructionGridCellInSocketVault()`. It reads cached SHINOBU Vault views, clamps `Counters[0]`, iterates `ConstructionSocketModuleDTO` rows, converts each finite `RootAup` into a `ConstructionRequestDTO` using the same root AUP and grid size as the candidate, and compares `GridPos`. Snapped placement commit now receives the placement command pose (`placePos`, `placeRot`) instead of reading `placedModule.transform`, normalizes the quaternion with finite guards, and writes module/socket/connection rows from that data. Construction acoustic/flora commit signals also derive center AUP from command pose plus template center instead of sampling the spawned transform.

Rejected Alternatives: Treating `ConstructionBuilderOccupancy` as pre-published truth was rejected because `EnsureOccupancyHashTable()` only allocates the buffer and PlayerBuilder was the writer. Keeping a fallback scene scan was rejected because it creates a second authority route and hides legacy publisher gaps. Reading the spawned transform after placement was rejected because the command pose already contains the placement fact and the Vault row is the proof artifact.

Scalability potential: Low, Middle, High, and Ultra all use the same module-row occupancy truth and placement command pose. `GlobalQualityWeight` continues to scale snap search budget, search radius, and Dear Lie visuals only; it does not change occupancy authority, DTO layout, or save identity.

Hardware Impact: Removes managed scene-list traversal and scratch hash-table writes from occupied-cell validation, one spawned-transform position/rotation read from snapped Vault commit, and one `TransformPoint`-based signal sample from post-place proof. No profiler microseconds are claimed; static evidence only until compile/runtime proof is available.

## Decision 55 - SHINOBU Frame Identity Uses Dispatcher Frame

Problem: `PlayerBuilder` and `HectonBlueprintPreviewBatch` still stamped SHINOBU-owned preview, validation, holography telemetry, deconstruction, and flora payloads with Unity `Time.frameCount`; builder holography also derived `AnimationPhase` from `Time.unscaledTime`. These are not placement layout fields, but they feed black-box evidence and `BuilderGhostStateDTO.ValidationStateHash`, so direct Unity time created a determinism and authority residue.

Solution: Add `CaptureShinobuFrameId()` in `PlayerBuilder` and `CapturePreviewFrameId()` in `HectonBlueprintPreviewBatch`. Both consume `TimeSliceScheduler.CurrentFrameId` and use an owner-local monotonic fallback only when the dispatcher has not published a nonzero frame. Replace direct frame stamps in construction validation telemetry, validator settings, builder ghost jobs, preview signals, deconstruction requests, flora exclusion signals, and holography telemetry. Replace `Time.unscaledTime * 0.5f` with frame-derived `frame / 120` animation phase.

Rejected Alternatives: Keeping Unity frame/time reads was rejected because the source now has a dispatcher frame lane. Registering `PlayerBuilder` as an `IDispatcherSystem` was rejected because this MonoBehaviour is an active player tool path and widening its lifecycle would cross more ownership boundaries than the residue requires. Making frame identity a Vault lane was rejected because no SHINOBU-owned frame truth buffer exists and creating one would add a second clock owner.

Scalability potential: Low, Middle, High, and Ultra all get the same frame identity and DTO layout. `GlobalQualityWeight` still scales snap search and Dear Lie presentation; it does not change frame ownership, save identity, or validation authority.

Hardware Impact: No profiler microseconds claimed. Static cost is one public static frame read and rare fallback increment per payload. The gain is deterministic provenance: validation hashes and black-box rows no longer depend on direct Unity wall-clock/frame calls in the SHINOBU-owned route.

## Decision 56 - Placement Rule Cache Drops Managed List Buffer

Problem: `PlayerBuilder` owned `_placementRuleBuffer`, a persistent `List<MonoBehaviour>` with initial capacity two. The route cleared and refilled it with `finalPrefab.GetComponents(_placementRuleBuffer)` when active buildables changed. That path is cold, but a prefab with more behaviours than capacity can grow the list and allocate managed memory.

Solution: Remove the list field and `System.Collections.Generic` import. `CacheActivePlacementRule()` now performs one cold `activeBuildable.finalPrefab.GetComponent<IBuildPlacementRule>()` lookup and caches the returned rule reference. Active semantic validation still calls the cached rule only when a builder preview exists.

Rejected Alternatives: Increasing the list capacity was rejected because it only moves the growth threshold. Keeping `GetComponents()` and scanning all `MonoBehaviour` entries was rejected because the target contract is a single optional placement rule. Rewriting all authored placement rules into Vault rows was rejected because that is a wider design migration outside this SHINOBU socket polish pass.

Scalability potential: Low, Middle, High, and Ultra all use the same cold rule cache behavior. Quality curves remain limited to snap search, shader Dear Lie presentation, and optional telemetry; they do not alter semantic placement truth.

Hardware Impact: Removes one possible managed list capacity allocation on active buildable changes and removes the component-array scan loop. No active-frame profiler number is claimed.

## Decision 57 - Semantic Placement Rule Dispatch Closure

Problem: The active builder preview route still cached `IBuildPlacementRule` and called `ValidatePlacement()` through an interface every validation tick. The two current implementers also carried route-specific residue: `DeepDrillModule.ValidatePlacement()` polled `GlobalRegistry.InteractionSignals`, constructed an `InteractionPacket` with `Time.frameCount`, and cast absolute AUP coordinates down to `float3`; `AutonomousExtractorModule.ValidatePlacement()` could call `EnsureRuntimeInstance()` and allocate a runtime owner if none was registered, and its candidate distance fallback used `candidate.transform.position`.

Solution: Remove `IBuildPlacementRule.cs` and replace the active semantic rule lane in `PlayerBuilder` with a byte-tagged sealed dispatch cached from the active prefab. `PlayerBuilder.BindRuntimeReferences()` caches `IInteractionSignalService` and `AutonomousExtractorSystem`; active semantic validation passes those cached dependencies into `DeepDrillModule.ValidatePlacementWithService()` or `AutonomousExtractorModule.ValidatePlacementWithRuntime()`. Deep-drill validation now uses the interaction service runtime-position raycast overload with finite guards and no `InteractionPacket` or Unity time stamp. Extractor validation fails closed when the cached runtime is missing, and candidate distance returns valid scores only from persistent AUP pairs.

Rejected Alternatives: Keeping the cached interface was rejected because the implementer set is known in source and the route runs during active preview validation. Creating a new cross-domain semantic rule registry was rejected because it would widen ownership and add another route for the same placement fact. Keeping extractor runtime creation in validation was rejected because `EnsureRuntimeInstance()` can allocate a `GameObject`. Keeping transform fallback was rejected because it makes semantic placement truth depend on presentation transforms.

Scalability potential: Low, Middle, High, and Ultra use the same semantic truth and fail-closed missing-owner behavior. `GlobalQualityWeight` remains confined to snap candidate/search budgets and Dear Lie visuals; it does not alter semantic placement ownership or rule identity.

Hardware Impact: Removes one virtual/interface dispatch per semantic validation tick, one active registry poll from drill validation, one packet construction plus absolute-to-runtime conversion path per drill validation attempt, and one possible runtime-owner allocation branch from extractor validation. The remaining extractor `Physics.OverlapSphereNonAlloc` is documented as extractor-domain residue until a resource-node owner publishes an unmanaged spatial snapshot; no profiler microseconds are claimed.

## Decision 58 - Active Buildable Selection Does Not Force-Complete Jobs

Problem: `CycleBuildable()` was an active input path that called `BindRuntimeReferences()`, then `SetActiveBuildable()`. `SetActiveBuildable()` force-completed pending SHINOBU socket and builder-ghost validation jobs before switching the selection. Post-placement ghost refresh also called `DespawnGhost()` with the default structural-validation reset, and `HabitatConstructionManager.ResetValidation()` force-completes its pending job internally. `CacheActivePlacementRule()` still contained a direct `GlobalRegistry.InteractionSignals` fallback for deep-drill semantic rules.

Solution: Make active selection consume only cold-cached references. `CycleBuildable()` and `DebugDeployActiveBuildable()` no longer call `BindRuntimeReferences()`. `SetActiveBuildable()` no longer calls the force-complete teardown helpers; it despawns the preview with `forceValidationReset: false`, assigns the new buildable through `AssignActiveBuildable()`, and lets existing jobs finish naturally. `_activeBuildableGeneration` increments on buildable assignment; snap and builder-ghost validation jobs store that generation at schedule time and reject stale results after `TryFinalizeCompleted()` returns. Cached snap pose reuse also checks the generation. Active post-placement ghost refresh uses the same nonblocking despawn path, and `SpawnGhost()` marks integrity as pending if the old structural validation job is still running. `CacheActivePlacementRule()` now relies exclusively on the cold cached interaction service.

Rejected Alternatives: Calling `.Complete()` or force-complete helpers from input was rejected because module cycling is active-frame work. Clearing pending handles without completion was rejected because jobs could still write shared Vault rows. Rebinding `GlobalRegistry` when cycling was rejected because dependency identity belongs to cold owner phases. Creating a second job buffer per active selection was rejected because SHINOBU already owns fixed Vault lanes and can discard stale results with generation stamps.

Scalability potential: Low, Middle, High, and Ultra all use the same nonblocking active selection path. `GlobalQualityWeight` still scales snap candidate/search budget and Dear Lie presentation; it does not change selection identity, DTO layout, or job authority.

Hardware Impact: Removes one active registry binding sweep per catalog cycle and avoids worst-case input/placement stalls caused by forced completion of socket snap, builder ghost, or integrity validation jobs. Added cost is one uint generation compare on finalize/cache reads. No profiler microseconds are claimed.

## Decision 59 - Tuner Vault Read Is Strict

Problem: `ModularBaseConstructionValidator.TryReadTunerSettingsFromVault()` returned `false` on missing/invalid Vault data but still wrote `s_TunerSettings` into the out parameter before failing. `PlayerBuilder.TryBuildConstructionValidationPayload()` ignored the bool, so the route worked only because a read-looking API concealed a static fallback.

Solution: Make `TryReadTunerSettingsFromVault()` strict: it writes `default` before attempting the Vault read and only writes a real candidate after all finite checks pass. `PlayerBuilder` now checks the bool return and explicitly calls `GetTunerSettings()` as the local fallback when the Vault lane is unavailable.

Rejected Alternatives: Keeping the implicit out-parameter fallback was rejected because it makes the origin of tuning data invisible. Renaming the method to an explicit fallback API was rejected because the existing cold editor and initializer paths already expect strict `Try*` semantics and can choose their own fallback.

Scalability potential: Low, Middle, High, and Ultra use the same tuner layout and authority route. `GlobalQualityWeight` in the settings remains a scalar field; this patch changes only read provenance, not quality behavior.

Hardware Impact: Adds one explicit branch in PlayerBuilder validation-payload construction. No profiler microseconds are claimed; the gain is route clarity and removal of hidden static state from a read facade.

## Decision 60 - PlayerBuilder Consumes Interaction-Owned Surface Hits

Problem: `PlayerBuilder.TryGetBuildHit()` directly called `UnityEngine.Physics.RaycastNonAlloc` with its own one-element buffer. That made the builder tool the owner of the scene query for preview targeting and deconstruction targeting, contradicting the Global Systems Doctrine route split after the interaction service already exists.

Solution: Remove `_buildHits` and route `TryGetBuildHit()` through the cold-cached `IInteractionSignalService.TryRaycastPrimary()` runtime-position overload. The builder validates finite origin/direction/range, normalizes direction with `math.rsqrt`, and uses a stable requester id derived from the builder entity id. If the interaction service is missing or uninitialized, the builder fails closed instead of doing a private PhysX fallback.

Rejected Alternatives: Keeping `RaycastNonAlloc` as fallback was rejected because it preserves two owners for the same surface-hit fact. Reintroducing `InteractionPacket` was rejected because the runtime-position overload avoids absolute-to-runtime conversion in this route. Creating a new construction surface-hit signal was rejected for this pass because the interaction service already owns asynchronous raycast queuing and cached completion.

Scalability potential: Low, Middle, High, and Ultra use the same surface-hit authority route. `GlobalQualityWeight` still scales snap candidate/search budget and visual presentation, not hit ownership.

Hardware Impact: Removes one direct builder PhysX call site for active preview/deconstruction target queries and one cold `RaycastHit[1]` buffer field. The actual raycast cost remains owned by the interaction service and may return completed queued results; no profiler microseconds are claimed.

## Decision 61 - Extractor Runtime Registry And Job ABI Fence

Problem: `AutonomousExtractorSystem` still owned a growable `List<AutonomousExtractorModule>` and its `AdvanceExtractionJob` used implicit struct layout, `FloatMode.Fast`, and unqualified NativeArray lanes. `DeepDrillModule` also kept a static growable `List<DeepDrillModule>` active-provider registry. These runtimes are adjacent to SHINOBU semantic placement because active validation dispatches directly to drill/extractor providers, and the current world resource route still lacks an unmanaged extractor-host semantic contract.

Solution: Replace the extractor module registry with a fixed `AutonomousExtractorModule[256]` plus `_moduleCount`, bounded registration, and explicit compaction without `List<T>.Add/RemoveAt`. Replace the deep-drill static active registry with a fixed `DeepDrillModule[128]` plus `s_ActiveModuleCount` and swap-with-tail removal. Define `ExtractorJobInput`/`ExtractorJobResult` as explicit 32-byte rows. Change `AdvanceExtractionJob` to `BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)` and add `[NoAlias]` to the input/result lanes. Delete the unreferenced `AutonomousExtractorJobs.cs` duplicate instead of keeping a second internal advance-job ABI.

Rejected Alternatives: Keeping either `List<T>` was rejected because registration pressure could trigger managed growth and `RemoveAt` shifts managed references. Moving extractor host resolution to `ResourceNodeDTO` was rejected for this pass because the only public contracts route exposes ore positions/types; extraction-support flag, host diameter, yield item hash, depletion semantics, and stable host claim identity are not available through `Hecton8.World.Contracts`. Referencing `Hecton8.World.Economy` directly was rejected because it would create a sibling runtime dependency/cycle risk. Mirroring resource host facts inside construction was rejected because one fact needs one owner. Moving extractor private NativeArray SOA lanes to ad hoc BufferIDs was rejected because those lanes need an extractor-owned route card; SHINOBU socket truth is already Vault-owned separately.

Scalability potential: Low, Middle, High, and Ultra share the same fixed registry capacity, deterministic job ABI, and fail-closed host-contract boundary. `GlobalQualityWeight` must not change extractor host truth; once the world owner exposes extractor-capable host snapshots, SHINOBU should use quality only to scale optional preview/search cadence, not yield identity or claim state.

Hardware Impact: Removes possible managed list capacity allocations and list tail-shift paths from extractor runtime registration/compaction and deep-drill active-provider registration. Deterministic Burst may cost ALU latitude versus fast math, but cycle completion affects gameplay-visible inventory/power state. No profiler microseconds are claimed.

## Decision 62 - Provider Registry Proof Surfaces Must Match Source

Problem: The DeepDrill source had already moved from a static growable list to fixed active-provider storage, but several proof surfaces still described only the extractor registry. That mismatch creates a false forensic trail: a future reviewer could believe DeepDrill still owns a managed list or that the extractor evidence also covers DeepDrill without naming it.

Solution: Update the Rationale, LOG, construction architecture note, binary payload ledger, JSON report, and XML self-audit to explicitly name fixed `DeepDrillModule[128]` storage plus `s_ActiveModuleCount` and swap-with-tail removal. Keep the unresolved resource-host contract and extractor private NativeArray SOA risks intact rather than laundering them through the proof update.

Rejected Alternatives: Leaving the proof surfaces stale was rejected because the project treats disk logs as long-term memory. Adding a new BufferID or world-resource mirror during documentation cleanup was rejected because no extractor-owned route card exists and resource-host truth belongs to the world owner.

Scalability potential: Low, Middle, High, and Ultra share the same provider registry mechanics. Quality curves remain presentation/cadence controls only; they do not change semantic rule identity, resource host truth, or DTO layout.

Hardware Impact: No runtime microseconds claimed. The correction prevents future managed-container regression work and keeps the static verification predicate literal; the first PowerShell `-like` check used wildcard semantics for `[]`, so `.Contains()` is the valid proof check for `DeepDrillModule[128]`.

## Decision 63 - Integrity Validation Is Rollback Truth

Problem: `HabitatConstructionManager.IntegrityValidationJob` still used `FloatMode.Fast`. The job writes placement support validity, integrity score, candidate depth, and failure reason, which affects whether the player can place a module. That is gameplay-visible state and can enter rollback-relevant construction truth.

Solution: Change the job attribute to `BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)`. Keep the existing `[NoAlias]` lanes and scheduling path unchanged.

Rejected Alternatives: Keeping fast math was rejected because ARM64/x86 drift can change threshold-side structural decisions. Rewriting the whole `HabitatConstructionManager` scene-list graph path in this loop was rejected because replacing `ConstructionManager.SpawnedModules` with a pure socket-Vault graph requires a separate ownership route and must not be hidden inside a Burst flag fix.

Scalability potential: Low, Middle, High, and Ultra use the same structural truth. `GlobalQualityWeight` may scale preview/search cadence and visuals, but it must not change integrity pass/fail semantics.

Hardware Impact: No microseconds claimed. Deterministic mode may reduce compiler fast-math latitude, but it prevents co-op divergence for placement validity.

## Decision 64 - Build-Cost Scratch Buffers Are Fixed Capacity

Problem: `HabitatConstructionManager.HasBuildResources()` could grow `_inventoryPlacementBuffer` to `inventory.Grid.TotalCells`, and `PrepareCostBuffers()` could grow four managed cost arrays to the blueprint cost count. These paths run during builder resource validation and construction commit, so an oversized inventory or blueprint could allocate on the active placement route.

Solution: Allocate fixed cold buffers in the manager constructor: `PlayerInventory.ItemPlacement[1024]`, `int[32]` hash/remaining/removed buffers, and `ItemData[32]` rollback references. `PrepareCostBuffers()` returns `-1` if authored build-cost rows exceed the fixed capacity. `HasBuildResources()` fails closed if the inventory grid exceeds the fixed placement snapshot capacity, preventing `PlayerInventory.GetPlacements()` truncation from becoming a false resource proof.

Rejected Alternatives: Retaining `NextPowerOfTwo()` growth was rejected because it moves managed allocation into active build validation. Truncating the inventory placement scan was rejected because construction could be approved with incomplete inventory evidence. Moving build-cost validation into a new Vault lane was rejected for this loop because inventory ownership belongs to the inventory domain and would require a route card.

Scalability potential: Low, Middle, High, and Ultra share the same build-cost truth and fixed capacity. Quality weight must not change resource affordability or construction authority.

Hardware Impact: Removes possible managed array allocation and copy churn from active resource validation/commit. Added cost is one capacity predicate per resource check; no profiler microseconds are claimed.

## Decision 65 - Integrity Graph Cache Uses Socket Vault Signature When Available

Problem: `HabitatConstructionManager` still keyed its existing integrity graph cache with `GameObject.GetInstanceID()`. That cache key is nondeterministic Unity object identity, while SHINOBU already publishes module AUP/socket/connection topology into Vault. A full rewrite of the integrity graph source is not safe yet because `ConstructionSocketModuleDTO` does not carry the support-root/family or resource-mass facts consumed by `IntegrityValidationJob`.

Solution: Add `TryComputeSocketVaultGraphSignature()` and route `ComputeExistingGraphSignature()` through it when the socket Vault module count exactly matches the construction scene registry count. The signature hashes module count, socket count, connection count, topology counters, `ConstructionSocketModuleDTO` module hash/socket range/flags/topology/root AUP/rotation, and `SocketConnectionPairDTO` target/ghost indices and flags. If the Vault route is absent or count-mismatched, the fallback scene signature now folds `ModuleHashId`, family, AUP-quantized root, and rotation bits instead of Unity instance IDs.

Rejected Alternatives: Replacing the full existing graph with Vault rows was rejected because it would guess support roots and mass from incomplete socket DTOs. Using Vault signature unconditionally was rejected because mock or stale Vault rows could mask scene graph changes. Removing the fallback scene-built graph path was rejected until the construction owner publishes support-root and resource-mass facts in an unmanaged route card; only Unity instance identity was removed from that fallback.

Scalability potential: Low, Middle, High, and Ultra share the same topology truth. `GlobalQualityWeight` still controls snap search/presentation only; it does not alter integrity graph identity, support semantics, or DTO layout.

Hardware Impact: Removes nondeterministic Unity instance-id cache keys from SHINOBU-published topology cases and invalidates the cached graph when Vault module/connection topology changes. No profiler microseconds are claimed.

## Decision 66 - Integrity Adjacency Fails Closed On Corrupted Connection Rows

Problem: `HabitatConstructionManager.BuildAdjacency()` counted and wrote adjacency entries by indexing `AdjacencyCounts[connection.x]` and `AdjacencyCounts[connection.y]` without validating each `int2` connection row against the current node count. Normal generation should only write valid module indices, but a stale/corrupted Vault connection lane or partial failed cache state could turn a bad row into an unchecked NativeArray index before the deterministic `IntegrityValidationJob` runs.

Solution: Add `IsValidConnectionIndex()` and validate every connection row before degree counting and before adjacency writes. `BuildAdjacency()` now also checks `AdjacencyRanges` creation/length, fences `_connectionCount` against `_connectionCapacity`, and rejects adjacency-count integer overflow by invalidating the existing graph cache and returning false. `AddConnection()` rejects negative endpoints and self-loops before writing a pair into the connection buffer.

Rejected Alternatives: Trusting the generated rows was rejected because black-box crash forensics require fault isolation before a bad unmanaged row reaches a Burst job. Clamping invalid endpoints was rejected because it would fabricate graph topology and possibly approve unsupported placement. Filtering invalid rows was rejected because losing an edge silently changes structural support truth; fail-closed validation is the only deterministic route.

Scalability potential: Low, Middle, High, and Ultra share the same integrity topology truth. `GlobalQualityWeight` does not alter adjacency validity, support graph identity, DTO layout, or placement authority.

Hardware Impact: Adds two unsigned endpoint bounds checks per connection row in the CPU adjacency build and three scalar checks in the writer. The cost is bounded by connection count and only occurs before scheduling structural validation; it prevents out-of-bounds NativeArray writes and downstream cache-corruption cascades. No profiler microseconds are claimed.

## Decision 67 - Builder Deconstruction Target Uses Collider Registry

Problem: After routing builder raycasts through the interaction service, `PlayerBuilder.TryDeconstructTargetModule()` and `GetTargetedModule()` still converted the returned collider to a module through `GetComponentInParent<BaseModule>()`. That is a scene hierarchy search on an active target path. `BaseModule.OnEnable()` already registers its collider tree into the fixed-array `LaserCutterTargetRegistry`, and `LaserCutterTargetRegistry.TryResolveModule()` performs an open-addressed collider-id lookup without component traversal.

Solution: Add `PlayerBuilder.TryResolveTargetModule(Collider, out BaseModule)` and route both deconstruction target call sites through `LaserCutterTargetRegistry.TryResolveModule()`. If the collider is missing from the registry, the builder fails closed with no target.

Rejected Alternatives: Keeping `GetComponentInParent<BaseModule>()` as fallback was rejected because it preserves a second authority route and component traversal in the active builder target path. Creating a new builder-owned collider registry was rejected because the same collider-to-module fact is already owned by a lifecycle-populated fixed registry. Moving this into `ConstructionManager` was rejected for this loop because it would widen the route and touch a larger owner surface without need.

Scalability potential: Low, Middle, High, and Ultra use the same collider-to-module identity route. `GlobalQualityWeight` does not change target ownership, deconstruction authority, or DTO layout.

Hardware Impact: Replaces two active component-parent searches with a fixed-capacity open-address collider-id lookup. No profiler microseconds are claimed.

## Decision 68 - Snap Candidate Budget And Radius Use Continuous Quality

Problem: `ShinobuSocketConstructionRuntime.ResolveCandidateBudget()` accepted `quality` but discarded it and returned `safeMax`. `ResolveSearchRadius()` also discarded `quality` and returned the ultra radius. `EvaluateSocketSnappingJob` compounded this by using `safeCount` as the budget and the max radius directly. The reports claimed continuous scalability, but the source still ran the ultra snap search width on low-quality devices.

Solution: `ResolveCandidateBudget()` now uses `SmoothQuality(quality)` and `math.lerp(safeMin, safeMax, q)` with a ceil to keep at least one row. `ResolveSearchRadius()` uses the same smoothed quality scalar to lerp from low radius to ultra radius. `EvaluateSocketSnappingJob` calls both helpers and clamps inspected CSR rows to the resolved candidate budget.

Rejected Alternatives: A binary low/high branch was rejected because `GlobalQualityWeight` must be continuous. Returning the old max budget for correctness was rejected because target eligibility is still checked deterministically inside the bounded candidate window and missing far rows are a fidelity/search-cadence tradeoff, not a DTO or authority layout change. Scaling placement truth checks was rejected; SDF corners and terrain probes remain fixed truth.

Scalability potential: Low uses the minimum inspected CSR rows and low search radius; Middle smoothly expands both values; High and Ultra approach the full 256-row and ultra-radius path. The same socket DTOs, compatibility predicate, and connection authority remain unchanged across the curve.

Hardware Impact: Restores the intended memory-bandwidth and distance-check reduction on low-quality devices. At default 16..256 budgets, quality 0 resolves to 16 inspected target rows and quality 1 resolves to 256 rows. No profiler microseconds are claimed.

## Decision 69 - Mock Grid Clears All Counter Lanes

Problem: `GenerateMockBaseConstructionGrid()` writes module count, socket count, topology version, and flags into `Counters[0..3]`, but the counters buffer is allocated with `NativeArrayOptions.UninitializedMemory` and `Counters[4]` is the live connection-pair count consumed by topology hashing and placement commit logic. Leaving that lane stale can make a mock-only grid appear to have arbitrary connection pairs.

Solution: Clear the entire `counters` NativeArray at the start of explicit mock generation, then write the known module/socket/topology values. This sets connection count and spare lanes to zero without touching active read facades.

Rejected Alternatives: Clearing counters in `TryResolveVaultViews()` was rejected because read accessors must be pure. Clearing counters on every `InitializeVault()` was rejected because cold rebinding could erase live construction state. Leaving stale lanes was rejected because the fallback mock generator is a CI/profiling route and must be deterministic.

Scalability potential: Low, Middle, High, and Ultra receive the same deterministic mock topology. `GlobalQualityWeight` still scales snap search and presentation only; it does not alter counter layout or connection ownership.

Hardware Impact: Adds at most eight integer stores during explicit mock generation. No active-frame cost and no profiler microseconds are claimed.

## Decision 70 - Counter Lane Is Seeded Only On Cold Invalid State

Problem: The socket counters buffer is requested with `NativeArrayOptions.UninitializedMemory`. Before mock generation or any construction owner writes topology, `TryResolveVaultViews()` consumers can read `Counters[0]`, `Counters[1]`, and `Counters[4]`. Clearing in the read facade would violate purity, while clearing on every `InitializeVault()` could erase live topology after a cold rebind.

Solution: Add `ShouldResetCounterLane()` and `ClearCounterLane()`. `InitializeVault()` checks the existing `ConstructionSocketCounters` generation handle before requesting the handle. If the lane is absent, shorter than the used counters, or already outside known module/socket/connection capacities, the lane is cleared once after handle resolution. Valid existing counters are preserved. `GenerateMockBaseConstructionGrid()` reuses the same clear helper because mock generation is an explicit writer route.

Rejected Alternatives: Clearing counters in `TryResolveVaultViews()` was rejected because read accessors must not publish or mutate. Clearing on every `InitializeVault()` was rejected because service rebinding could destroy legitimate topology. Adding a new counter DTO was rejected because it would change the payload surface for a guard that can be handled inside the existing fixed lane.

Scalability potential: Low, Middle, High, and Ultra share the same counter identity and capacity semantics. `GlobalQualityWeight` has no effect on counter ownership or topology identity.

Hardware Impact: Cold path only: one existing-handle resolve plus a few integer range checks, with at most eight integer stores when the lane is absent/invalid. No active-frame profiler number is claimed.

## Decision 71 - Builder Holography Uses Generation Descriptors

Problem: `HectonBlueprintPreviewBatch` still stored obsolete `VaultBufferHandle<T>` descriptors and active reads called `.Resolve(vault)`. That contradicted the SHINOBU proof surfaces that describe generation-checked descriptor reads and kept legacy pointer-bearing handle semantics in the preview upload/telemetry heartbeat path.

Solution: Replace `_stateHandle`, `_visualHandle`, `_telemetryHandle`, and `_argsHandle` with `VaultGenerationHandle<T>`. `EnsureBuffersCold()` now checks existing handles with `IDataVault.TryResolveHandle(...)` and acquires/grows lanes with `GetGenerationHandle(...)`. `TryReadCachedBuffers()` resolves phase-local `NativeArray` views only through `TryResolveHandle(...)`.

Rejected Alternatives: Keeping `VaultBufferHandle<T>.Resolve(vault)` was rejected because the handle type is marked as a legacy pointer-bearing migration bridge. Converting active reads to `GlobalRegistry.DataVault` was rejected because the batch already cold-caches `_vault` and active paths must not poll the registry. Moving the holography lanes into a new owner was rejected because the existing BufferIDs already define the construction-owner route.

Scalability potential: Low, Middle, High, and Ultra share the same descriptor identity. `GlobalQualityWeight` still scales hologram presentation and snap search work only; it does not change Vault descriptor ownership or DTO layout.

Hardware Impact: Removes cached pointer handle resolution from active holography reads and keeps active upload/telemetry reads generation-checked. No profiler microseconds are claimed.

## Decision 72 - Runtime Origin Conversion Avoids GlobalSignals Bridge

Problem: `PlayerBuilder` and `HectonBlueprintPreviewBatch` still called `GlobalSignals.CurrentRuntimeOriginAup()` while scheduling SHINOBU snap/holography jobs and converting runtime positions to AUP. The method is a legacy wrapper around `HectonFloatingOrigin.CurrentTotalOffsetDouble`, so the active route was depending on a signal facade for data that can be resolved as a finite double3 origin.

Solution: Add local `TryResolveRuntimeOriginAup(out double3)` helpers in the two SHINOBU files. Active snap job scheduling passes that finite origin into `EvaluateSocketSnappingJob`. Snap result application subtracts the finite origin before casting to `Vector3`. Holography runtime-position conversion adds the finite origin in double precision and then hydrates `AbsoluteUniversePosition` from the resolved absolute double3.

Rejected Alternatives: Keeping `GlobalSignals.CurrentRuntimeOriginAup()` was rejected because direct GlobalSignals bridge reads are legacy lanes and obscure the owner-local origin dependency. Converting runtime positions through absolute floats was rejected because it violates the 100km AUP precision rule. Creating a new signal payload was rejected because no new fact is needed; the existing floating-origin owner already exposes the current double3 offset.

Scalability potential: Low, Middle, High, and Ultra share the same origin conversion. `GlobalQualityWeight` does not change origin authority, DTO layout, snap truth, or runtime/AUP conversion precision.

Hardware Impact: Removes four active legacy signal-origin reads from the SHINOBU builder/preview path. Added cost is finite double3 validation at each conversion boundary; no profiler microseconds are claimed.

## Decision 73 - Construction Validator Uses Generation Descriptors

Problem: `ModularBaseConstructionValidator` still stored `VaultBufferHandle<T>` descriptors for tuning, telemetry, bounds, and occupancy lanes and resolved them with `ResolveBuffer` / `.Resolve(vault)`. The validator is part of SHINOBU's construction placement proof, so keeping pointer-bearing migration handles contradicted the Vault descriptor discipline already applied to socket and holography lanes.

Solution: Replace the four static handles with `VaultGenerationHandle<T>`. Add `EnsureValidationBuffer()` for explicit writer/ensure routes and `TryResolveCachedValidationBuffer()` for read-only cached descriptor resolution. `TryReadTunerSettingsFromVault()` now reads an existing generation descriptor through `TryGetGenerationHandle<ConstructionValidationSettingsDTO>()` and `TryResolveHandle(...)`.

Rejected Alternatives: Keeping `VaultBufferHandle<T>` was rejected because the type carries obsolete cached-pointer semantics. Using `GetGenerationHandle` inside read facades was rejected because read accessors must not create or grow buffers. Adding new BufferIDs was rejected because the existing tuning/telemetry/bounds/occupancy identities remain valid.

Scalability potential: Low, Middle, High, and Ultra share the same validation DTO lanes and descriptor identity. `GlobalQualityWeight` scales preview/search presentation only; it does not change validator buffer identity or placement truth.

Hardware Impact: Removes legacy pointer refresh/resolve calls from validator lanes. Active read routes now resolve generation descriptors and check lengths. No profiler microseconds are claimed.

## Decision 74 - Habitat Socket AUP Conversion Avoids GlobalSignals Bridge

Problem: After removing the active builder and holography origin bridges, `HabitatConstructionManager.TryResolveAupFromRuntimeOrigin()` still called `GlobalSignals.CurrentRuntimeOriginAup()` before computing authored socket AUP rows. This kept the same legacy origin bridge inside SHINOBU's habitat socket adaptation route.

Solution: Resolve the current floating-origin offset directly as `HectonFloatingOrigin.CurrentTotalOffsetDouble`, guard it with `math.isfinite`, add the runtime position in double precision, and return the resulting finite `double3` AUP to the socket resolver.

Rejected Alternatives: Keeping the GlobalSignals wrapper was rejected because it adds no ownership proof beyond the floating-origin owner. Converting through runtime floats was rejected because AUP precision must stay double until local runtime projection. Creating a new signal or DTO was rejected because the existing floating-origin owner already provides the required origin fact.

Scalability potential: Low, Middle, High, and Ultra share the same AUP conversion. `GlobalQualityWeight` does not change origin authority, socket root identity, DTO layout, or placement truth.

Hardware Impact: Removes the last `CurrentRuntimeOriginAup()` read from the SHINOBU habitat/builder/preview files. Added cost is one finite double3 check per conversion call; no profiler microseconds are claimed.
