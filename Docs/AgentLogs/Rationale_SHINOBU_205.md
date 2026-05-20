# Rationale_SHINOBU_205

Date: 2026-05-20
Agent: SHINOBU_205
Status: PENDING VERIFICATION

## Initial Boundary

Problem: AUP precision failures at 100 km boundaries can occur when large absolute coordinates are narrowed to float before observer-origin subtraction.
Solution: Enforce the DOD order `double3 delta = target - observer; float3 local = (float3)delta;` in shared kernels and validators. Use unmanaged DTOs, Burst jobs, editor/static scanners, and fixed-size telemetry.
Rejected Alternatives: Transform.position authority is rejected because it is presentation-space after floating origin shifts. Raw `Vector3.Distance` is rejected because it narrows and may allocate/string-log in surrounding debug paths. New DataVault/global routes are rejected unless existing owner surfaces are present, because global authority docs freeze surface growth.
Scalability potential: Low uses continuous distance gating to skip far entities while preserving precision near the route; Middle keeps normal cadence and probe density; High increases telemetry sample density; Ultra spends saved cycles on visual sync precision/debug overlays, not corrupting authority math.
Hardware Impact: Low-end i3/MX350 gains by avoiding far-entity double subtraction and zero-fill passes; expected static budget target is sub-0.1 ms for 4096 active localizations and load-shed for larger sets. Runtime profiler proof absent.
First 20 Minutes moment: swim / world load / save-load position continuity.
Route impact: route position, resource/node placement, collision, and save hash stay stable after origin shifts and far-from-origin travel.
Proof required: static scan plus compile where safe; Unity Console, Play Mode, GCMonitor, profiler, and player proof remain pending.

## Mandate Selection

Problem: Task spans AUP math, memory layout, Burst jobs, telemetry, and global authority.
Solution: Selected 8 mandates: MATH_Coordinate_Precision_AUP_FloatingOrigin, MATH_AUP_Determinism_Sync, DATA_Runtime_Struct_Layout_ARM64, OPT_Zero_GC_Policy_AllocFree_Mandate, OPT_Native_Memory_Collections_JobSystem_Protocol, DBG_Telemetry_Crash_Reporting_PostMortem, ARCH_Global_Registry_ServiceLocator_DI_Init, ARCH_Execution_Phases.
Rejected Alternatives: Reading rendering-only or AI-only mandates first would miss core authority and native layout rules.
Scalability potential: mandates enforce continuous quality weight and phase-owned load-shed rather than binary precision switches.
Hardware Impact: prevents hidden GC and ARM64 unaligned stalls that are disproportionately visible on cheap devices.

## Global Authority Boundary

Problem: XML asks for GlobalDataVault buffers and observer AUP injection, but adding global buffers without current owner proof violates the global authority boundary.
Solution: First scan existing DataVault/AUP owners. Only add owner-local kernels/static validators unless an existing vault route exists and can be used without new global slots.
Rejected Alternatives: inventing new `GlobalRegistry.AupPrecision` or DataVault numeric buffers without route card/GREEN review.
Scalability potential: keeps precision system stateless and reusable across Low/Middle/High/Ultra without widening global surface.
Hardware Impact: avoids central mutable heap contention and stale handle failures.

## Loop 1 Decisions

Problem: Direct `(float3)` AUP casts were spread across atmosphere sampling, voxel culling, shadow culling, gameplay ballistics, scanner VFX, inventory sector lookup, mod event projection, terminal interaction, and biolum sync.
Solution: Added `AupPrecisionMath` helpers in `Hecton8.Core.Contracts` and rewrote all static scan hits to use `LocalDeltaDouble`, `LocalDeltaFloat3`, `DowncastLocalDelta`, or `DowncastProceduralPhase`. Procedural noise phases now scale in double before downcast.
Rejected Alternatives: raw regex replacement was rejected because local deltas, procedural phases, and AUP authority conversions need different semantics; leaving `(float3)(AUP - origin)` was rejected because it lets future edits move subtraction after the cast without a scanner hit.
Scalability potential: Low and Middle keep cheap local float math after one double subtraction; High and Ultra can increase visible sampling/detail without changing precision order.
Hardware Impact: i3/MX350-class devices avoid false far-distance jitter and keep hot loops branch-light; estimate is 0.01-0.08 us per localized entity versus float-first correction work.

Problem: Task required Transform.position eradication, but broad codebase contains presentation, editor, gizmo, and legacy facade reads sharing the same syntax.
Solution: Added `AUP_Premature_Cast_Scanner` transform authority queue in `Docs/Reports/MATH_OPTIMIZATION_REPORT.json`; did not invent new DataVault routes. Existing Transform reads are treated as review findings unless owner AUP authority exists.
Rejected Alternatives: deleting every Transform read would break visual sync, editor tools, and legacy compatibility facades without supplying an authority source.
Scalability potential: continuous review gate keeps Low/Middle devices on presentation-only Transform usage while AUP authority stays stable for High/Ultra scene sizes.
Hardware Impact: no hot-path cost added; static scan reported 1034 broad Transform candidates for staged owner review.

Problem: AUP double3 DTO layout must remain ARM64-safe and raw-field only.
Solution: Added `AupDouble3AlignmentValidator` using `UnsafeUtility.SizeOf<T>()` plus Marshal offsets for core AUP DTOs and reflection checks for world AUP structs. New `AupToleranceProfileDTO` and `AupPrecisionTelemetryEntry` are explicit 64-byte raw-field DTOs.
Rejected Alternatives: relying on `LayoutKind.Sequential` and C# properties was rejected because Burst/native arrays require deterministic unmanaged layout and no defensive-copy property layer.
Scalability potential: Low devices avoid unaligned double loads; Ultra devices can push more AUP telemetry without layout ambiguity.
Hardware Impact: expected gain is avoided cache-line split stalls; per-record field read savings are small, but failure mode is severe on ARM64.

Problem: Extreme 100 km boundary precision needs repeatable proof without manual scene swimming.
Solution: Added `GenerateMockExtremeAupJob` and X-Ray edge mock path to synthesize +/-100 km AUP samples with millimeter jitter.
Rejected Alternatives: manual teleport testing alone is rejected because it is nondeterministic and does not isolate float-first subtraction jitter.
Scalability potential: Low uses small sample count; Middle/High/Ultra can increase sample count and gizmo density in editor only.
Hardware Impact: job writes fully initialized NativeArray rows; expected sub-100 us for 4096 samples on i3/MX350-class CPU, profiler proof pending.

## Loop 2 Decisions

Problem: Localization kernels must gate entity count by quality without changing precision rules.
Solution: Added `LocalizeAupCoordinatesJob` with double subtraction, double squared distance, continuous `GlobalQualityWeight` gate, clamped local downcast, and per-row result flags.
Rejected Alternatives: binary low/high precision modes were rejected because quality may reduce far work, not corrupt near authority math.
Scalability potential: Low gates near 1 km, Middle interpolates, High/Ultra expands toward 5 km while preserving identical subtract/downcast order.
Hardware Impact: low-end devices save far-entity work; expected active 4096 localization pass below 0.1 ms pending Burst profiler proof.

Problem: Legacy sector hashes are often one-way FNV mixes, but task requires deterministic sector-hash-to-AUP conversion.
Solution: Added a reversible packed 64-bit sector hash with signed 21-bit axes and explicit marker bit; `TryConvertSectorHashToAup` reconstructs sector centers deterministically. Existing one-way hashes are not reinterpreted.
Rejected Alternatives: guessing coordinates from existing FNV hashes was rejected because it is mathematically impossible and would corrupt rollback state.
Scalability potential: deterministic integer decode is identical across Low/Middle/High/Ultra; visual density can vary independently.
Hardware Impact: O(1) integer unpack plus double multiply; estimated 0.04 us per sector decode.

Problem: Distance calculations on large float locals can overflow precision and false-cull near the 100 km boundary.
Solution: Added and adopted `DistanceSqSafeDouble` / double `math.lengthsq` before float narrowing in shadow culling, voxel prioritization, and thermal hazard sampling.
Rejected Alternatives: `math.dot(float3,float3)` on large pre-localized values was rejected because it hides lost mantissa bits.
Scalability potential: Low can cull earlier by gate distance while High/Ultra spend saved cycles on more visible entities.
Hardware Impact: double square cost is acceptable for authority/culling boundaries; avoids expensive mis-cull debugging and visual instability.

## Loop 6 Polish Decisions

Problem: The first Vault lane candidate used `73053..73061`, colliding with SHINOBU_200 SignalWarden overflow buffers documented in the binary ledger and route card.
Solution: Rejected that range and moved SHINOBU_205 to `73200..73208`. The route card and ledger now state the collision and the selected range. Exact-number scan after migration shows `73200..73208` only in SHINOBU_205 code/docs.
Rejected Alternatives: Keeping a "local numeric cast" while another owner already owns the number is rejected because DataVault BufferID identity is global even when enum expansion is avoided.
Scalability potential: The lane can scale capacity from 1 to 262144 rows without aliasing another owner; Low uses narrow active counts, Middle/High/Ultra increase localized coverage.
Hardware Impact: Avoids catastrophic buffer type mismatch and memory aliasing. No microsecond saving is claimed; this is correctness and crash prevention.

Problem: Previous static kernel route did not satisfy the new Vault-law proof strongly enough because it owned only transient job inputs and did not document the memory route.
Solution: Added `AupPrecisionVault` as a handle-only static route using `VaultGenerationHandle<T>` rather than legacy persistent `VaultBufferHandle<T>` fields. It resolves `NativeArray<T>` views only in `EnsureBuffers`, parser, dump, editor mock, or job schedule boundaries and stores no private native arrays.
Rejected Alternatives: Private `NativeArray<T>` fields, long-lived pointer-bearing legacy handles, and hot `GlobalRegistry.DataVault` polling inside jobs.
Scalability potential: Low devices run fewer active rows via continuous distance gate; Middle/High/Ultra expand the same SoA lane without changing authority math.
Hardware Impact: Prevents stale-pointer and GC hazards; generation handle validation cost is cold/scheduling phase, not per-row in Burst.

Problem: Telemetry folding over full capacity could read uninitialized slack when requested capacity exceeds active entity count.
Solution: Added `ActiveCount` to `AupPrecisionTelemetryFoldJob` and pass the scheduled count from `TryScheduleLocalization`; the fold loop hashes only active rows.
Rejected Alternatives: Clearing every capacity row each frame or scanning full capacity. Clearing wastes memory bandwidth; full scan corrupts telemetry with slack.
Scalability potential: Low can allocate large future capacity but process only current active rows; Ultra can process more rows by raising active count, not by wasting bandwidth on slack.
Hardware Impact: On i3/MX350-class silicon, avoiding 4096-row slack at 0.018-0.036 us/row saves roughly 74-147 us per fold; at 125k slack the avoided range is millisecond-scale.

Problem: The first out-of-bounds sentinel used infinity. That is mathematically poisonous because skipped far rows would be recorded as non-finite telemetry and could trigger false fault dumps.
Solution: Changed the sentinel to finite `DefaultMaxLocalCastMeters` on each axis. Skip state remains explicit through `ResultFlagSkippedByGate`.
Rejected Alternatives: Infinity or NaN sentinels were rejected because black-box telemetry, spatial hashes, and matrix consumers must never ingest non-finite values.
Scalability potential: Low-quality far-row shedding remains cheap without poisoning fault counters; High/Ultra still get larger active range by quality weight.
Hardware Impact: Avoids false dump/diagnostic cascades and prevents non-finite values from entering downstream vector math.

Problem: The editor edge mock proved jitter numerically but did not inject its samples into the owner Vault route.
Solution: X-Ray now writes +/-100 km mock samples to Vault `73200` and mirrors them to `73207` when DataVault exists; the TempJob fallback remains editor-only for no-vault sessions.
Rejected Alternatives: Debug GameObject spawning and Transform teleports were rejected because they test presentation drift, not AUP authority math.
Scalability potential: Editor sample count remains 32 by default; route capacity can scale to stress larger edge sets without runtime scene churn.
Hardware Impact: Cold editor only; no gameplay frame cost.

Problem: The new mandate requires hard forensic proof, not chat-only claims.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_205_AUP_PRECISION_ROUTE_CARD.md`, updated the binary ledger, and will append a new XML self-audit to `LOG_SHINOBU_205.md`.
Rejected Alternatives: Relying on previous self-audit after changing Vault ownership and DTOs.
Scalability potential: Documents Low/Middle/High/Ultra behavior through continuous quality gate rather than binary tiers.
Hardware Impact: Documentation cost only; protects future maintainers from reintroducing BufferID collisions and float-first authority.

## Loop 7 Precision Gate Decisions

Problem: The scanner blocked `(float3)AUP` syntax but explicit component casts such as `new float3((float)deltaAup.x, ...)` could still reintroduce float-first coordinate narrowing.
Solution: Added `ComponentAupFloatCast` to `AUP_Premature_Cast_Scanner`, splitting runtime blockers from editor presentation review. Runtime component cast scan is now 0; editor-only visual/debug component casts remain 5 review findings.
Rejected Alternatives: Treating component casts as acceptable because several were already-local deltas. That makes the static gate syntax-dependent and allows future edits to move subtraction after cast.
Scalability potential: Low/Middle/High/Ultra all keep the same AUP precision law; only evaluated entity count scales.
Hardware Impact: Cold scan only. Runtime edits replace hand component casts with inlined helper calls; expected cost is equivalent after Burst inlining, with 0.01-0.08 us per entity precision safety value.

Problem: Multiple runtime domains still used manual component downcasts after double subtraction or absolute midpoint conversion, creating inconsistent review semantics.
Solution: Patched SignalWarden macro collisions, interior GI probe localization, player motor runtime sample resolution, fauna deafened wander vector, vehicle damage root-relative mapping, acoustic occlusion SDF midpoint, world spatial hash thermal gradient, and bulkhead editor gizmo to route through approved double/local helpers or double SDF overloads.
Rejected Alternatives: Mass editing every `Transform.position` consumer in one sweep. The strict scan shows 116 runtime authority blockers, but each needs owner AUP source ownership; inventing a shared route from SHINOBU_205 would violate global authority law.
Scalability potential: Low devices skip more far rows via continuous gating; high devices spend the preserved precision on richer GI/acoustic/vehicle feedback rather than unstable authority math.
Hardware Impact: Prevents 100 km mantissa loss. Microsecond savings are not the claim; fault prevention is the claim. Acoustic midpoint now uses existing double SDF overload and avoids a pointless float conversion.

Problem: `MockPredatorStimulusJob` wrote mock target AUP from a float position derived from `dto.CurrentAUP`, which could bake precision loss into mock gameplay state at the map edge.
Solution: Changed the job to deterministic Burst compile flags and preserves `dto.TargetAUP` by adding the mock acoustic offset in double. The float signal remains presentation/mock payload; double target authority no longer round-trips through float.
Rejected Alternatives: Marking the whole job as editor-only or leaving `FloatMode.Fast`; this job mutates DTO state and therefore needs deterministic treatment.
Scalability potential: Same deterministic mock behavior across Low/Middle/High/Ultra; quality can adjust stimulus rates elsewhere without changing coordinate truth.
Hardware Impact: Negligible ALU cost; removes a rollback/desync precision trap near 100 km.

Problem: Reports could be clobbered by multiple math agents.
Solution: Preserved the existing Jacobi scanner JSON and inserted an `aup_precision_inspector` object plus a dedicated static preflight report at `Docs/Reports/AUP_PRECISION_SCAN_SHINOBU_205.json`.
Rejected Alternatives: Overwriting `MATH_OPTIMIZATION_REPORT.json` with a SHINOBU-only report.
Scalability potential: Documentation-only, but preserves multi-agent proof channels.
Hardware Impact: 0 runtime us.

## Loop 8 CLI Gate Decisions

Problem: The AUP precision scanner existed as a Unity Editor menu action. That does not protect CI or local workspaces when Unity import is blocked by foreign compile debt or CPU build guard.
Solution: Added `Tools/AupPrecisionGate_SHINOBU_205.py`, an editorless Python gate that scans `Assets/_Project/Scripts` for direct AUP/double3 `(float3)` casts, runtime component AUP float casts, and strict `Transform.position` authority reads. It writes the full SHINOBU report and upserts only the `aup_precision_inspector` object in the shared math report.
Rejected Alternatives: Warning-only grep scripts and chat-only reports. A warning-only path would let coordinate regressions pass CI; a chat-only path would lose evidence on context compaction.
Scalability potential: This is tooling, not runtime. It protects the same Low/Middle/High/Ultra precision law by preventing low-quality paths from introducing float-first authority math while high-quality paths expand only evaluated entity count.
Hardware Impact: 0 runtime us. Cold static scan cost measured in this workspace was about 29-32 s over 1982 C# files; it replaces manual grep sweeps and avoids Unity Editor startup.

Problem: The first CLI count included four self-diagnostic editor lines from `AUP_Premature_Cast_Scanner.cs`, where the X-Ray tool intentionally constructs the early-float visual lie.
Solution: Excluded the scanner file from component-cast review in the CLI gate, matching the existing Editor scanner's `scannerSelfDiagnostic` behavior. The report now shows 5 real editor presentation reviews, 0 runtime component casts, and 116 strict transform authority blockers.
Rejected Alternatives: Counting the X-Ray self-demo as debt. That would obscure actual owner-domain findings and make the gate disagree with the Editor facade.
Scalability potential: Documentation/tooling only. The Dear Lie stays editor-only and does not affect runtime precision.
Hardware Impact: 0 runtime us.

Problem: Regex gates are brittle unless fixture-tested.
Solution: Added `Tools/TestAupPrecisionGate_SHINOBU_205.py`, which creates a temporary source tree and verifies exact detection of direct cast, runtime component cast, editor component review, strict transform authority, approved helper count, and self-diagnostic exclusion.
Rejected Alternatives: Trusting visual inspection of regexes. That fails silently when future edits change pattern scope.
Scalability potential: Tooling only. It keeps CI precision enforcement stable as the project grows.
Hardware Impact: 0 runtime us; cold test run was about 6 s on this workspace.

## Loop 9 Transform Authority Fallback Decisions

Problem: The CLI gate proved that direct AUP casts were blocked but player/camera fallback paths still reconstructed AUP from `Transform.position`, which turns the floating-origin presentation lie back into authority after origin shifts.
Solution: Rewired only provable owner-local fallbacks to `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot`, `HectonPlayerMovement.CurrentAup`, or existing movement-state AUP. Patched residency, biome transition, persistent registry, Manta player distance checks, item catalog, chemical grid player focus, PDA intrusion, impostor viewer, subtitles, celestial/ocean/light/decal camera references, LOD viewer, AR projection, drone render references, player acoustic signals, radiation, flora sway, resource distribution, ore spawning, world interest, world slice, and scatter sampling.
Rejected Alternatives: Mass rewriting object-self `transform.position` in modules, anchors, beacons, probes, wrecks, resource nodes, docking sockets, and live prefab records. Those objects need their owning domain to publish AUP or a Vault route; SHINOBU_205 cannot invent truth by converting the same presentation transform under another name.
Scalability potential: Low devices keep the same exact precision rule while evaluating fewer rows; mid/high/ultra expand visible coverage through existing quality gates. Camera/player observer math now scales by entity count only, not by weakening AUP precision.
Hardware Impact: Static blocker count dropped from 116 to 79. Runtime microseconds are not claimed because these are authority-source fixes; the gain is removing edge-of-map jitter, rollback drift, and false culling near 100 km.

## Loop 10 Residual Proven-Owner Decisions

Problem: `AmbientWaterMotionManager` still converted `lodObserver.position` to AUP for distance LOD. That serialized Transform is presentation-space and can be stale after a floating-origin shift.
Solution: Resolve the observer from `IPlayerRuntimeContext.TryGetPlayerPoseSnapshot` and then `PlayerMovement.CurrentAup`, one time per tick, and pass an explicit `hasObserverAup` flag into the distance-gate function.
Rejected Alternatives: Keeping `lodObserver.position` as a fallback or inventing AUP for arbitrary observer Transforms. Without a proven owner, the correct behavior is to skip distance LOD and keep the visual motion alive.
Scalability potential: Low/Middle/High/Ultra still scale only update cadence through existing distance gates; coordinate precision does not degrade.
Hardware Impact: Removes one authority conversion. Cost is a single cold player-context read per manager tick; no per-object GlobalRegistry polling was added.

Problem: `HectonWorldGenerator` derived streaming chunks and absolute XZ from `viewer.position`, silently re-authorizing a presentation Transform as terrain streaming truth.
Solution: Rewire chunk coordinate and absolute XZ resolution through the player AUP snapshot/current AUP. If no AUP owner is present, streaming does not advance rather than guessing from a Transform.
Rejected Alternatives: `AbsoluteUniversePosition.FromRuntimePosition(viewer.position)` and keeping a hidden `Vector3 runtimePosition` helper that was always fed from `viewer.position`.
Scalability potential: Low devices stream fewer chunks via existing radii; Ultra can expand radii without weakening AUP precision.
Hardware Impact: Removes one strict blocker and one hidden runtime-position path. No microsecond saving is claimed; this prevents far-origin chunk drift.

Problem: `ItemHighlight` used `_cachedTransform.position - _playerTransform.position` for activation range, which breaks when resource and player presentation roots are shifted independently.
Solution: Use `ResourceNode.TryGetPersistentAup` as the item owner route and player AUP snapshot/current AUP as observer; compute `AbsoluteUniversePosition.DistanceSq` before float saturation.
Rejected Alternatives: Reconstructing item AUP from its Transform, keeping a player Transform cache, or adding a new highlight-specific global route.
Scalability potential: Low can disable the visual highlight when no resource AUP exists; High/Ultra get stable stencil activation at 100 km with no new physics work.
Hardware Impact: Removes one strict blocker. AUP distance cost is acceptable for a visual tick; avoids incorrect highlight flicker after origin shifts.

Problem: `CaveGraphGenerator` was counted by the strict scanner because `math.length(rooms[i].position - rooms[j].position)` syntactically resembled a Transform-position distance.
Solution: Split the local generator-space delta into `float3 roomDelta` before length. The code remains local procedural math and no scanner weakening was required.
Rejected Alternatives: Lowering the scanner strictness globally or marking all lowercase `.position` access safe. That would hide actual owner debt elsewhere.
Scalability potential: No runtime behavior change; this preserves static gate precision for future Low/Middle/High/Ultra content generation.
Hardware Impact: No measurable runtime change. Static blocker count dropped from 79 to 74 after the full Loop 10 gate run.

## Loop 11 Presentation Fake Decisions

Problem: `HectonCelestialEngine` converted observer-relative Aegir/sun presentation transforms into AUP for cinematic distance and direction helpers. These bodies are Dear Lie visuals, not simulation-scale entities.
Solution: Keep the delta in presentation space using local visual `Vector3` subtraction and normalize the visual delta directly. No AUP is produced from celestial fake transforms.
Rejected Alternatives: Leaving `AbsoluteUniversePosition.FromRuntimePosition(fromTransform.position)` / `toTransform.position` in the helper. That recasts a render fake as world authority and confuses the static gate.
Scalability potential: Low/Middle keep cheap visual math; High/Ultra can add richer eclipse/ring visuals without coupling celestial presentation to AUP authority.
Hardware Impact: Removes four strict blockers. Runtime cost remains O(1) vector math; no microsecond saving is claimed.

Problem: HUD/SDF visual distance in `CameraJuiceSystem` and `WorldSpaceTMPSharpnessController` looked like authority distance because the subtraction occurred directly on Transform positions inside a distance expression.
Solution: Split canvas/camera and target/camera positions into explicitly named visual deltas and keep them out of AUP conversion paths.
Rejected Alternatives: Converting UI transforms to AUP, which would be false authority, or weakening the scanner for whole UI folders.
Scalability potential: Low devices keep sparse HUD update cadence; High/Ultra get sharper SDF/HUD focus without touching simulation coordinates.
Hardware Impact: Removes two strict blockers. The math remains float presentation-space work only.

Problem: `HectonNarrativeDirector.GetNearestUndiscoveredPOI` ignored the cached AUP already owned by `NarrativeDiscovery` and re-derived POI AUP from `poi.transform.position`.
Solution: Consume `NarrativeDiscovery.CachedAup` for nearest-POI query.
Rejected Alternatives: Re-reading Transform or requiring a new narrative Vault route.
Scalability potential: Same POI query semantics across Low/Middle/High/Ultra; scan interval scaling remains separate.
Hardware Impact: Removes one strict blocker and one duplicate Transform bridge.

Problem: `HectonFabricatorUI` selected hologram anchor was pure UI matrix placement but cached an AUP from `anchor.position`.
Solution: Return the anchor visual position as `float3` and remove the unused AUP cache fields/import.
Rejected Alternatives: Caching a fake AUP for a hologram anchor that is never simulation authority.
Scalability potential: Hologram visual overkill can scale independently from AUP precision.
Hardware Impact: Removes one strict blocker and dead cache state.

Problem: `PlayerExplorationTracker` editor gizmo used its own component Transform as a fallback cartography AUP when player AUP was unavailable.
Solution: Fail closed in `OnDrawGizmos`; no player AUP means no debug voxel draw.
Rejected Alternatives: Converting the tracker Transform to AUP for visualization. That creates an authority-looking value from presentation space.
Scalability potential: Editor-only; runtime cartography still uses player AUP and quality-scaled scan cadence.
Hardware Impact: Removes one strict blocker. No runtime cost.

## Loop 12 Visual-Lie Owner Debt Decisions

Problem: `AmbientWaterMotion` stored `_restAup` by converting its decorative Transform rest pose into AUP. This is a visual bob/sway component, not an owner of world-scale simulation coordinates.
Solution: Default rest AUP to absent and expose `HasRestAup`. The manager now applies distance LOD only when a real rest AUP exists; otherwise it uses the parent-relative rest pose as presentation-space input for current sampling and visual offsets.
Rejected Alternatives: Keeping `AbsoluteUniversePosition.FromRuntimePosition(_cachedTransform.position)` or inventing a global AUP route for decorative props. Both would turn a visual fake into coordinate authority.
Scalability potential: Low devices still benefit from cadence/quality controls where true AUP exists; otherwise decorative motion stays cheap and local. High/Ultra can spend cycles on richer bob/sway visuals without touching simulation precision.
Hardware Impact: Removes one strict blocker. No runtime microsecond saving claimed; cost remains O(n) visual float math with no new allocations.

Problem: `LODSystemManager` cached LODGroup AUP from `lodTransform.position`, making pure rendering LOD anchors look like simulation-scale facts.
Solution: Replaced LODGroup AUP cache with presentation `Vector3` cache and explicit camera-relative visual distance. This is a Dear Lie rendering decision: LOD crossfade is based on what the camera sees after floating origin, not on gameplay authority.
Rejected Alternatives: Fabricating AUP per LODGroup or weakening the scanner globally. LODGroups need owner-published AUP before they can participate in authority distance; absent that, presentation math is honest.
Scalability potential: Low/Middle/High/Ultra still scale via existing LOD bias and capped hot-path batch. Precision law remains untouched because no AUP cast occurs.
Hardware Impact: Removes two strict blockers. The hot path stays O(64) capped float squared-distance; no frame-time saving is claimed.

## Loop 13 Proven Local/Presentation Decisions

Problem: `FaunaBrain.Ecosystem.RefreshCorpseDiseaseState` still converted its own Transform into AUP even though the main FaunaBrain partial already has logic AUP resolution.
Solution: Use `TryResolveSelfLogicAup` and fail closed by clearing disease state when self AUP cannot be resolved.
Rejected Alternatives: Keeping a second self Transform bridge or manufacturing a new ecosystem route.
Scalability potential: Fauna logical LOD remains independent from coordinate precision; Low can evaluate fewer brains while High/Ultra keep richer ecosystem overlays.
Hardware Impact: Removes one strict blocker. No runtime saving claimed.

Problem: `FaunaSimplifiedRagdollHandoff` used Transform-derived sector AUP only to vary visual ragdoll angular seed.
Solution: Seed from stable EntityId and a salt. The handoff remains deterministic without any coordinate authority.
Rejected Alternatives: Transform-to-AUP sector hashing for a visual ragdoll. It is not worth making a false world fact for cosmetic angular variance.
Scalability potential: Same deterministic seed on all tiers; visual complexity can scale elsewhere.
Hardware Impact: Removes one strict blocker and a few cold integer ops; no frame-time saving claimed.

Problem: `TraumaDispatcher` converted a runtime-only EMP event and target Transforms into AUP. The event payload exposes no AUP, so the conversion was a false authority mask.
Solution: Keep the relevance test in presentation space using local `Vector3` deltas against the runtime pulse position.
Rejected Alternatives: Pretending a runtime event had authoritative AUP or adding a new signal field from SHINOBU_205.
Scalability potential: Low/High behavior stays consistent; this is a local effect relevance check, not route-state math.
Hardware Impact: Removes one strict blocker. O(1) float distance remains.

Problem: `PhysicalHandController` converted opposing hand and body bounds to AUP for sub-meter two-hand stabilization.
Solution: Use local presentation physics delta between opposing hand and body bounds center.
Rejected Alternatives: AUP conversion for controller/body distances inside the same floating-origin frame.
Scalability potential: VR hand stabilization stays cheap on mobile and can retain richer feedback on high tier without changing spatial authority.
Hardware Impact: Removes one strict blocker and avoids two AUP constructions per check.

Problem: `HullDentShaderController` localized shader dents by creating hit/root AUPs from visual points.
Solution: Use root-relative presentation subtraction and existing local rotation/scale normalization. The dent buffer is shader-only; no gameplay collision authority changes.
Rejected Alternatives: AUP fabrication for a shader-only dent presenter.
Scalability potential: Low keeps small dent buffer; High/Ultra can push more visual dent fidelity without touching simulation coordinates.
Hardware Impact: Removes one strict blocker and two double3 conversions per dent registration.

## Loop 14 Final Strict Gate Decisions

Problem: The static gate still reported 18 strict authority blockers where cold/bootstrap or save-sync code converted `Transform.position` directly with `AbsoluteUniversePosition.FromRuntimePosition` or `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3`.
Solution: Replaced those direct conversions in `PlayerBuilder`, `ConstructionManager`, `HabitatConstructionManager`, `DroneFleetManager`, `HabitatFluidIncursionDirector`, `SubmarineStructuralGrid`, `ScannableTarget`, `ThermalGeyser`, `AbyssalThermodynamicsSolver`, `BaseIntegrityHUD`, `ChemicalInfluenceGrid`, `EmergencyServiceRelay`, and `PersistentWorldRegistry` with explicit runtime-origin AUP helpers: `originAup = GlobalSignals.CurrentRuntimeOriginAup(); resolved = AbsoluteUniversePosition.OffsetMeters(in originAup, double3(runtimePosition))`. The conversion now makes the origin AUP authority explicit and keeps the local runtime delta small before any later float use.
Rejected Alternatives: Keeping direct `FromRuntimePosition(transform.position)` wrappers, reducing the gate threshold, adding new sibling-domain AUP services, or creating new DataVault lanes for authored scene objects. Direct wrappers hide authority debt; new global lanes violate owner-local routing without a defined owner.
Scalability potential: Low-tier devices keep identical coordinate correctness while evaluating fewer entities through existing quality gates. Middle/high/ultra can increase construction, thermal, scanner, and world-residency visual density without changing the AUP rule. No binary quality switch was added.
Hardware Impact: Static blockers dropped from 18 to 0; direct AUP float casts remain 0 and runtime component AUP float casts remain 0. Runtime microsecond savings are not claimed; this is precision and rollback stability. The avoided failure is 100 km mantissa loss and save/streaming hash drift after origin shifts.

Problem: Some remaining bridge sites had existing owner data while others were cold-authored scene handoffs.
Solution: Preferred existing owner AUP when present (`drone.TargetAup`, player movement AUP, persisted record AUP, grid origin, integrity center) and used the runtime-origin helper only at cold/bootstrap or authored-scene boundaries where no owner route exists. Non-finite helper inputs fail closed or fall back to current runtime origin only for non-authoritative signal defaults.
Rejected Alternatives: Using `Transform.position` as a universal fallback or failing all authored scene objects closed. The former is false authority; the latter would break existing authored relays, vents, scanner entries, and construction save sync before those domains expose owner AUP.
Scalability potential: Low/Middle/High/Ultra all share one origin-plus-local-delta rule. More expensive visuals can be added above this layer without reintroducing float-first math.
Hardware Impact: Prevents hidden route-state corruption during origin shifts without adding per-frame allocations or hot `GlobalRegistry` polling inside jobs.

Problem: Verification had to avoid a premature rebuild.
Solution: Ran `python Tools\AupPrecisionGate_SHINOBU_205.py` and targeted `git diff --check` only. The gate returned `PASS_STATIC_GATE` over 1994 C# files: direct casts 0, runtime component casts 0, editor component reviews 5, strict Transform authority reads 0.
Rejected Alternatives: Launching `dotnet build` after regex-only edits despite explicit rebuild discipline.
Scalability potential: Tooling-only proof; CI can enforce the same gate without Unity Editor startup.
Hardware Impact: 24.6 s cold static scan cost; 0 runtime us.

## Loop 15 Hidden Runtime Bridge Decisions

Problem: The strict regex gate was green, but hidden bridge calls still existed where `Transform.position` or runtime `Vector3` had already been stored into a local variable before `FromRuntimePosition` / `ToAbsoluteUniversePositionDouble3`. The expanded scan set also exposed one new direct door bridge and two component AUP casts in Burst/tooling code.
Solution: PlayerBuilder, HabitatConstructionManager, DroneFleetManager, BaseIntegrityHUD, HabitatFluidIncursionDirector, ChemicalInfluenceGrid, RepairDroneHub, and SealedDoor now use either existing owner AUP (`playerMovement.CurrentAup`, `drone.PositionAup`, `drone.TargetAup`, hub `DockAup`) or an explicit runtime-origin bridge: `GlobalSignals.CurrentRuntimeOriginAup()` plus finite local runtime delta in double through `AbsoluteUniversePosition.OffsetMeters`. Seaglide hydrodynamics and UpgradeMatrixCompiler use `AupPrecisionMath.LocalDeltaDouble` / `DowncastLocalDelta` for component downcasts.
Rejected Alternatives: Keeping hidden HFO conversions because they no longer matched `transform.position` text, or weakening the component-cast scanner for code that already subtracted in double. Both would leave regression holes.
Scalability potential: Low, Middle, High, and Ultra tiers all keep the same coordinate truth; quality affects evaluated row count and visual richness only. No binary quality switch was added.
Hardware Impact: Static gate now passes over 2013 files with direct casts 0, runtime component casts 0, and strict Transform authority 0. No frame-time saving is claimed; this removes edge-of-map jitter and rollback/hash drift risk.

## Loop 16 Compile-Risk / Auxiliary AUP Residue Decisions

Problem: A compile-risk audit after Loop 15 found one residual component cast in `UpgradeMatrixCompiler`, one runtime direct AUP cast in `AuxiliaryEquipmentJobs`, one editor gizmo direct AUP cast, and one strict `transform.position -> AbsoluteUniversePosition.FromRuntimePosition` blocker in `AuxiliaryEquipmentRouterRuntime.GenerateMockDeployments`. The affected files compile under `Hecton8.Core.asmdef`, which already references `Hecton8.Core.Contracts`.
Solution: Routed all direct/component downcasts through `AupPrecisionMath.LocalDeltaDouble` then `DowncastLocalDelta`. Mock auxiliary deployment seeding now uses `GlobalSignals.CurrentRuntimeOriginAup().ToAbsoluteDouble3()` instead of the component Transform. No asmdef file was edited; no sibling runtime dependency was added.
Rejected Alternatives: Leaving handwritten casts because the current line happened to subtract before cast, or converting the Transform through a different wrapper. Both preserve a syntax hole for future float-first regressions and keep presentation state as authority.
Scalability potential: Low/Middle/High/Ultra all preserve the same AUP law; quality can only change deployment counts, VFX matrix scale, or visual density. The mock path is cold, and runtime VFX localization remains one double subtraction plus local float matrix assembly.
Hardware Impact: Static gate now passes over 2023 files with direct AUP float3 casts 0, runtime component casts 0, and strict Transform authority 0. Targeted diff check reports no whitespace errors, LF/CRLF warning only. Unity/Burst compile and profiler proof remain pending because rebuild was not required and is explicitly gated.

## Loop 17 Transform Distance Review Channel Decisions

Problem: The hard gate blocked direct Transform-to-AUP calls, but direct `.position` distance property syntax such as `(candidate.transform.position - player.position).sqrMagnitude` was only visible in the broad Transform queue. That queue is too noisy to be a precision review instrument.
Solution: Added `TRANSFORM_DISTANCE_REVIEW` to `Tools/AupPrecisionGate_SHINOBU_205.py` and updated the fixture test to cover both function-call distance and `.sqrMagnitude` property-distance syntax. The channel is report-only because the 17 current findings mix editor/presentation/local-space checks with owner-route debt; hard-failing all of them would invite false AUP fabrication.
Rejected Alternatives: Making every `.position` distance a hard blocker immediately, or ignoring the syntax because the current strict threshold is green. The first would break presentation/editor code without owner proof; the second leaves a scanner blind spot.
Scalability potential: Low/Middle/High/Ultra all keep the AUP law unchanged. The review queue helps future passes pick proven owner AUP routes while avoiding binary quality switches or fake global dependencies.
Hardware Impact: Static gate remains PASS over 2023 files: direct casts 0, runtime component casts 0, strict Transform authority 0, transform distance reviews 17. This adds 0 runtime cost and improves CI review visibility.

## Loop 18 AUP Distance Review Debt Decisions

Problem: `AutonomousExtractorModule.TryResolveNearestValidNode` still ranked resource nodes with direct visual Transform deltas. `ResourceNode` already exposes persistent AUP, but module refresh was also passing its own `transform.position` into the first AUP resolver draft.
Solution: Added an AUP distance resolver that uses `GlobalSignals.CurrentRuntimeOriginAup()` plus double local offset only for explicit runtime placement queries. The module refresh path passes `hasQueryAup=false`, so its `transform.position` remains a presentation fallback and is never promoted into AUP authority. Candidates rank by `AbsoluteUniversePosition.DistanceSq` only when both sides have owner/proven AUP.
Rejected Alternatives: Treating every extractor caller's `Vector3 position` as absolute truth, converting the module Transform into authority, or adding a new extractor-specific global owner route. The first two lose precision or invent a fact; the third violates owner-local routing without a declared owner.
Scalability potential: Low devices keep the same node selection but evaluate fewer candidates through existing domain limits; Middle/High/Ultra can increase extractor visual feedback without changing coordinate authority.
Hardware Impact: Removes one transform-distance review finding. No frame-time saving is claimed; the avoided failure is wrong nearest-node selection at 50-100 km.

Problem: `WorldGenerativeGeologyIntegrationDirector` used player runtime presentation position for plan-refresh distance and residency checks. The first AUP patch also allowed serialized `playerTransform` fallback to become runtime-origin AUP when the player context was absent.
Solution: Resolved player pose through `IPlayerRuntimeContext` and player movement, and stored the last refresh sample as AUP only when those owner routes expose AUP. Plan refresh, missing-plan restore, and plan build use double AUP deltas before local float math only under `hasPlayerAup=true`; serialized `playerTransform` fallback stays visual-only. Origin shift only mutates the presentation fallback sample when no AUP sample exists.
Rejected Alternatives: Continuing to shift `Vector3` refresh samples after every origin change, promoting serialized `playerTransform` into authority, or inventing a geology-specific player authority. The player context/player movement route already owns the fact.
Scalability potential: Low can keep longer refresh intervals while preserving exact AUP comparisons; High/Ultra can use tighter refresh thresholds for richer geology/voxel presentation without jitter.
Hardware Impact: Removes one transform-distance review finding. AUP distance is cold planning work; stability gain outweighs ALU cost.

Problem: `UpgradeMatrixCompiler` retained one runtime component AUP cast through `new float3((float)deltaAup.x, ...)`, which kept the component-cast gate red after other edits.
Solution: Replaced the manual cast with `AupPrecisionMath.DowncastLocalDelta(deltaAup, float3.zero)`, preserving the double-subtract-before-float contract in one approved helper call.
Rejected Alternatives: Allowing handwritten component casts because this local line happened to subtract first. That leaves an easy regression when the expression is refactored.
Scalability potential: The compiler path does not implement quality tiers, but it protects runtime output consumed across all tiers.
Hardware Impact: Gate blocker removed; helper should inline to equivalent local float writes. Correctness, not microseconds, is the proof.

Problem: `WorldFaunaSpawnRegistry` selected ordinary and large-threat anchors with local `Vector3` deltas even when `FaunaDirector` already had player AUP. Procedural scatter anchors also lacked an AUP payload in the registry handoff.
Solution: Added optional AUP fields to `WorldFaunaSpawnRegistry.Anchor`, AUP-aware query overloads, player AUP calls from `FaunaDirector`, and procedural anchor AUP hydration from scatter placement absolute coordinates. Distance sorting uses `AbsoluteUniversePosition.DistanceSq` when both sides expose AUP and returns `float.MaxValue` for non-finite distances so invalid anchors cannot win.
Rejected Alternatives: Converting every anchor Transform at query time, adding a fauna-specific singleton, or leaving all anchor ranking in local floats. Query-time Transform authority and new singleton routes are both false authority; local float ranking jitters at 100 km.
Scalability potential: Low can spawn fewer fauna while retaining stable anchor ordering; Middle/High/Ultra can increase fauna density and threat-zone richness without changing the coordinate proof.
Hardware Impact: Removes three transform-distance review findings. The added branch is per candidate and cold relative to spawn cadence; it prevents wrong anchor selection after origin shifts.

Problem: `FaunaDirector` still contained hidden `AbsoluteUniversePosition.FromRuntimePosition` bridges in touched spawn identity, thermal migration, and player fallback paths. Those were not direct Transform reads, but they hid the origin authority route and could be fed by presentation state later.
Solution: Replaced those calls with `TryResolveRuntimePositionAup`, which explicitly reads `GlobalSignals.CurrentRuntimeOriginAup()`, adds the finite runtime local offset in double, and fails closed on invalid input. `TryResolvePlayerLogicPose` no longer converts `_playerTransform.position` into AUP; without `PlayerRuntimeContext`/movement AUP, it returns false.
Rejected Alternatives: Leaving hidden bridges because the hard Transform scanner was green, or treating player Transform fallback as acceptable logic AUP. Both keep a precision regression channel outside the direct scanner.
Scalability potential: Low/Middle/High/Ultra retain identical identity hashes and migration targets when owner AUP exists; spawn density and visual richness can scale separately.
Hardware Impact: Six hidden bridge calls were removed from the touched fauna path. No runtime savings claimed; failure mode avoided is persistent fauna identity hash drift near origin-shift boundaries.

Problem: Verification still had to respect rebuild discipline.
Solution: Ran the Python AUP gate, fixture self-test, Python bytecode compile, and targeted `git diff --check` only. The latest report scans 2027 files with direct AUP casts 0, runtime component AUP casts 0, strict Transform authority 0, and transform-distance review debt reduced from 17 to 12.
Rejected Alternatives: Launching `dotnet build` or Unity compile after static-only edits despite explicit user instruction and CPU/build-wall discipline.
Scalability potential: Tooling proof only; it protects the same continuous-quality coordinate law across device tiers.
Hardware Impact: Latest gate took 11.1 s and adds 0 runtime cost. Unity/Burst/profiler proof remains pending.

## Loop 19 Transform Distance Review Zeroing Decisions

Problem: The transform-distance review queue still contained twelve inline `.position` distance expressions. Ten were presentation/editor/local-space checks, two were voxel-local crater stamp DTO checks, and one was a real duplicate owner-route problem in `NoiseSystem`.
Solution: Presentation/editor/local-space sites now split the Transform read from the distance calculation into explicitly named visual/local deltas. Voxel crater clustering/merging now uses named local stamp deltas. `NoiseSystem.EvaluatePlayerNoise01` fails closed when `PlayerNoiseSignal` is unavailable, removing the duplicate Transform/Rigidbody fallback route.
Rejected Alternatives: Converting every visual/local distance to AUP, weakening the scanner, or keeping the noise fallback as a convenience path. AUP conversion would create false authority in editor/gizmo/local physics code; scanner weakening would hide debt; the noise fallback was a second player-state route.
Scalability potential: Low devices keep cheap presentation/local math and avoid extra owner lookups. Middle/high/ultra retain the same route law while richer visual systems can scale separately through existing quality weights. No binary quality switch was introduced.
Hardware Impact: Static gate now reports transform-distance reviews 0, direct AUP float3 casts 0, runtime component AUP casts 0, and strict Transform authority blockers 0 across 2027 scanned files. Runtime saving is not claimed; the concrete win is removal of shadow-state and false-authority review debt with 0 rebuild cost.

Problem: Rebuild discipline still applies after scanner cleanup.
Solution: Ran the SHINOBU Python gate, gate fixture self-test, Python bytecode compile, and targeted source `git diff --check`. No `dotnet build`, Unity compile, or editor rebuild was launched.
Rejected Alternatives: Running a rebuild to cosmetically increase confidence after static-only syntax changes. The user explicitly prohibited premature rebuilds, and the affected proof surface is the static gate.
Scalability potential: CI can enforce the static precision contract without entering the Unity compile wall; runtime tiers are unchanged.
Hardware Impact: Latest gate took 4.7 s. Targeted whitespace check returned no errors, only LF/CRLF warnings. Unity/Burst/profiler proof remains pending.

## Loop 20 Editor Component AUP Cast Review Decisions

Problem: The hard gate was green, but five editor-only review findings still downcasted absolute `double3` AUP components directly into `Vector3` for SceneView gizmos and tuner overlays. That is not runtime authority corruption, but it preserves the exact float-first pattern the scanner is meant to make extinct.
Solution: `ResidencyStreamingTunerWindow`, `VolcanicUpdraftTunerWindow`, `ProceduralCoralDebugGizmo`, and `ProceduralWreckageDebugGizmo` now call `HectonFloatingOrigin.ToRuntimePosition(aup, HectonFloatingOrigin.CurrentTotalOffsetDouble)`. The conversion subtracts the committed double offset first, then draws local runtime coordinates.
Rejected Alternatives: Suppressing editor review findings, keeping direct casts because they are editor-only, or adding new runtime owner/Vault routes for gizmos. Suppression leaves a regression pattern; new routes for debug drawing would violate owner-local boundaries.
Scalability potential: Low devices are unaffected because this is editor tooling. Middle/high/ultra retain accurate debug overlays after origin shifts without changing runtime simulation or visual quality paths.
Hardware Impact: Static gate now reports editor component AUP cast reviews 0, transform-distance reviews 0, direct AUP casts 0, runtime component casts 0, strict Transform authority blockers 0 across 2028 scanned files. Runtime cost is 0; editor gizmo cost remains O(n drawn rows).

Problem: Verification still had to avoid the compile wall.
Solution: Ran SHINOBU gate, gate self-test, Python bytecode compile, targeted diff hygiene, and a targeted editor-cast grep. No dotnet/Unity build was launched.
Rejected Alternatives: Running a rebuild for editor-only presentation changes. Static proof covers the changed failure mode.
Scalability potential: CI/static gate can now enforce zero direct AUP component casts across runtime and editor review channels.
Hardware Impact: Latest SHINOBU gate took 11.3 s. Targeted `git diff --check` produced no errors, only LF/CRLF warnings.

## Loop 21 Float Distance Review Decisions

Problem: The precision gate still reported 29 `floatDistanceReviewCount` findings. Most were not AUP authority failures; they were local-space hull dents, bot/editor helpers, GUI line drawing, flora wake points, fungal node runtime buffers, ore clump spacing, and procedural scatter buckets. Leaving raw `math.distancesq` made the scanner unable to distinguish local math from forbidden authority distance.
Solution: Replaced local findings with named deltas and `math.lengthsq` or existing `Vector3.sqrMagnitude`. This preserves the local-space semantics and removes the scanner blind spot without inventing AUP ownership where the data is explicitly local/presentation/procedural.
Rejected Alternatives: Converting every local distance to `AbsoluteUniversePosition` or globally suppressing the review pattern. AUP conversion would fabricate false authority for shader dents, GUI overlays, and procedural cell-local DTOs; suppressing the pattern would hide future float-first authority distance regressions.
Scalability potential: Low devices keep cheap local squared-distance checks. Middle/high/ultra can raise flora/scatter/wake density through existing quality weights without changing coordinate authority. No binary quality switch was introduced.
Hardware Impact: Static report debt dropped from 29 to 0. Runtime microsecond savings are not claimed; `math.lengthsq(delta)` is equivalent ALU to squared distance and makes memory/authority intent explicit for later Burst vectorization review.

Problem: Narrative POI and world chunk residency used true AUP/universe-space distance checks but still called raw `math.distancesq` on absolute coordinates.
Solution: Routed those comparisons through `AupPrecisionMath.DistanceSqSafeDouble`, which subtracts in double first, squares the local delta, and returns `double.MaxValue` on non-finite results. The sort score then clamps to `float.MaxValue` only after the double-safe calculation.
Rejected Alternatives: Keeping `math.distancesq(double3,double3)` because it already uses doubles. The helper is the approved proof channel and prevents future edits from sliding a float cast before the subtract.
Scalability potential: Low quality can narrow chunk/narrative scan windows elsewhere, while high/ultra can expand loaded chunks or POI richness without changing distance truth.
Hardware Impact: Removes 5 AUP distance review hits. Cost is one helper inline around existing double math; correctness and gate proof are the objective.

Problem: Some edited Burst jobs had incomplete directive flags or low precision settings.
Solution: Normalized touched mathematical jobs in predator cognition, submarine structural grid, world chunk residency, and procedural scatter candidate acceptance to `[BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]`. Existing deterministic mock predator state job stays deterministic from the prior rollback pass.
Rejected Alternatives: Leaving `FloatPrecision.Low` in breach repair or missing `CompileSynchronously` on edited kernels. That violates the current mandate and can hide Burst configuration drift.
Scalability potential: Quality affects active counts and visual density, not float precision mode of coordinate-critical kernels.
Hardware Impact: Static configuration proof only. Unity/Burst compile and profiler proof remain pending by rebuild discipline.

Problem: Verification had to prove the edit without triggering the compile wall.
Solution: Ran `python Tools\AupPrecisionGate_SHINOBU_205.py`, `python Tools\TestAupPrecisionGate_SHINOBU_205.py`, and Python bytecode compile. The gate now reports 2028 files scanned, direct AUP casts 0, runtime component casts 0, editor component reviews 0, strict Transform authority 0, float distance reviews 0, transform distance reviews 0.
Rejected Alternatives: Launching dotnet or Unity rebuild after static-only syntax edits despite explicit user command.
Scalability potential: CI can enforce the scanner proof without Unity Editor startup. Runtime scalability remains continuous through existing quality-weight gates.
Hardware Impact: Latest gate took 9.3 s and adds 0 runtime cost. Broad Transform presentation review remains 937, which is review debt, not a hard blocker.

## Loop 22 Hidden Runtime AUP Bridge Decisions

Problem: The strict scanner only caught direct `Transform.position` authority on the same line. Hidden bridges such as `AbsoluteUniversePosition.FromRuntimePosition(localRuntimePosition)` remained invisible when the runtime value had already been copied into a local variable or DTO field.
Solution: Added `runtimeAupBridgeReviewCount` to `Tools/AupPrecisionGate_SHINOBU_205.py` plus a fixture test. It reports direct `AbsoluteUniversePosition.FromRuntimePosition` and `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3` runtime calls as review debt without failing the hard gate.
Rejected Alternatives: Promoting all 542 remaining hidden bridges to hard blockers immediately. That would create a compile-wall crisis across unrelated domains and invite false rewrites where runtime DTOs are currently the only exposed owner route.
Scalability potential: The new channel gives Low/Middle/High/Ultra paths the same authority review rule while allowing staged owner-proof work. It does not add runtime branches.
Hardware Impact: Static scan only. It exposed 542 remaining runtime AUP bridge reviews after the current pass; hard blockers remain zero.

Problem: `BaseModule.cs` contained hidden `FromRuntimePosition` bridges for deconstruction requests, EMP radius checks, external depth sampling, player base transition signals, and repair snap points.
Solution: Added a local `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` helper that reads `GlobalSignals.CurrentRuntimeOriginAup()`, adds the finite runtime offset in double via `AbsoluteUniversePosition.OffsetMeters`, and validates the resulting double coordinates. All nine direct `FromRuntimePosition` calls in `BaseModule.cs` were removed.
Rejected Alternatives: Leaving `FromRuntimePosition` because the values were runtime positions, or adding a new BaseModule-specific authority service. The first hides the origin route; the second violates owner-local/global authority boundaries without a declared owner.
Scalability potential: Low devices keep the same habitat behavior but avoid precision ambiguity. Middle/high/ultra can scale habitat VFX/repair richness without changing coordinate truth.
Hardware Impact: Nine hidden bridge calls removed. No frame-time saving claimed; the prevented failure is wrong module/EMP/depth/repair AUP after origin shifts.

Problem: `VRPipeBlueprintPreview.cs` still used `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3(authoredPoint.position)` in a preview build path, which reintroduced one strict Transform authority blocker when the new scan report was regenerated.
Solution: Added an explicit current-origin helper in the preview and routed `SetPreviewPoint`, authored control point AUPs, fallback point AUPs, and runtime build origin through it. The hard gate returned to PASS.
Rejected Alternatives: Waiving the preview as "only visual" while it writes AUP-stable control points into vault-backed preview DTOs. Visual preview is a Dear Lie, but its coordinate handoff still needs an explicit origin route.
Scalability potential: Low devices can keep short/simple preview segments; high/ultra can raise preview density using the existing quality weight, with stable control-point AUPs.
Hardware Impact: Three hidden/strict bridge calls removed from the preview. Latest gate: 2028 files scanned, hard blockers 0, runtime bridge review debt 542.

Problem: Verification had to restore the hard gate after adding a new scanner channel.
Solution: Ran the gate, fixture test, and Python bytecode compile. The final gate result is `PASS_STATIC_GATE` with direct AUP casts 0, runtime component casts 0, editor component reviews 0, strict Transform authority 0, float distance reviews 0, transform distance reviews 0, runtime AUP bridge reviews 542, broad Transform presentation reviews 936.
Rejected Alternatives: Leaving the temporary fail report in place or running dotnet/Unity build for static scanner/tool changes.
Scalability potential: CI now has a narrower staged debt metric without entering Unity Editor.
Hardware Impact: Latest hard gate took 8.6 s; fixture/py_compile passed. Runtime cost is 0.

## Loop 23 Beacon Runtime Bridge Decisions

Problem: Beacon runtime/network/deployer code contained five direct `AbsoluteUniversePosition.FromRuntimePosition` bridges. These are small, high-confidence sites because beacons already own `PositionAup` once registered and runtime-origin conversion is only needed at deploy/query/cache boundaries.
Solution: Added explicit current-origin AUP helpers to `BeaconRuntime`, `BeaconNetworkSystem`, and `BeaconDeployerTool`. Query origins now fail closed on non-finite input; snapshot/cache constructors fall back to current runtime origin only when an AUP value is mandatory and the runtime vector is invalid.
Rejected Alternatives: Leaving `FromRuntimePosition` in constructors for convenience, or adding a new beacon-specific global AUP owner. The existing beacon runtime already owns cached AUP; a new service would widen the route surface.
Scalability potential: Low devices keep cheap beacon queries. Middle/high/ultra can increase beacon visual range or UI density without changing coordinate authority. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped from 542 to 537. No runtime us claimed; route clarity and origin-shift correctness are the proof.

Problem: Verification still had to avoid the compile wall.
Solution: Ran SHINOBU static gate, fixture test, and Python bytecode compile. The gate reports hard blockers 0, float distance reviews 0, transform distance reviews 0, and runtime AUP bridge reviews 537.
Rejected Alternatives: dotnet/Unity rebuild for three small helper rewrites.
Scalability potential: Static-only proof keeps CI cheap and avoids Unity startup.
Hardware Impact: Latest gate took 6.9 s. Runtime cost is a finite check plus current-origin offset at beacon deploy/query/cache boundaries.

## Loop 24 Auxiliary Runtime Bridge Decisions

Problem: `AuxiliaryEquipmentRouterRuntime` public runtime-position overloads used six direct `AbsoluteUniversePosition.FromRuntimePosition(...).ToAbsoluteDouble3()` bridges for flare, sensor ping, and gravity tether deployment/cancel flows.
Solution: Added `TryResolveAupDoubleFromRuntimeOrigin(Vector3, out double3)` inside the router. It validates finite runtime inputs, reads `GlobalSignals.CurrentRuntimeOriginAup()`, offsets in double, and fails closed before queue mutation if projectile, anchor, or cancel origin is invalid.
Rejected Alternatives: Keeping direct bridge calls because the AUP overloads already exist downstream. That leaves the unsafe route in the most convenient public overloads and allows invalid runtime vectors into queues.
Scalability potential: Low devices can reject invalid auxiliary requests cheaply; high/ultra can increase auxiliary visual richness through existing profiles without changing coordinate truth. No binary quality switch was added.
Hardware Impact: Runtime AUP bridge review count dropped from 537 to 531. Runtime cost is a finite check and double offset at call boundaries, not inside Burst loops.

Problem: Verification still had to stay static.
Solution: Ran SHINOBU gate, fixture self-test, and Python bytecode compile. The hard gate remains pass with 0 direct AUP casts, 0 runtime component casts, 0 strict Transform authority reads, 0 float distance reviews, 0 transform distance reviews, and 531 runtime AUP bridge reviews.
Rejected Alternatives: dotnet/Unity rebuild for a small overload helper rewrite.
Scalability potential: CI continues to quantify hidden bridge debt without entering Unity.
Hardware Impact: Latest gate took 7.3 s. No runtime frame-time saving claimed.

## Loop 25 FaunaBrain Runtime Bridge Decisions

Problem: `FaunaBrain.cs` still had forty direct `AbsoluteUniversePosition.FromRuntimePosition` calls after the hidden bridge scanner landed. Several were self AUP, player predicted AUP, corpse, lunge, impact, and hibernation paths where the file already had owner-local resolution helpers.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` as an explicit finite current-origin boundary helper, rewired self-authored calls through `TryResolveSelfLogicAup`, rewired player lead math through `TryResolvePlayerPredictedAup`, added `TryResolveAttackTargetLogicAup` for lunge target owner routing, and fail-closed invalid boundary conversions before publishing signals or mutating AUP state.
Rejected Alternatives: A blanket replacement of all forty calls was rejected. The remaining twelve calls depend on player eye AUP, voxel route waypoints, ecosystem/organic targets, director targets, prey fallback positions, or migration targets that do not expose a proven owner AUP in this file. Inventing those routes inside `FaunaBrain` would create false authority and cross-domain coupling.
Scalability potential: Low devices keep the same cheap fauna cadence and use explicit fail-closed boundaries; middle/high/ultra can scale fauna presentation, lunge VFX, and ecology richness through existing quality weights without changing coordinate truth. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 531 -> 503, and `FaunaBrain.cs` direct bridge hits dropped 40 -> 12. Runtime microsecond savings are not claimed; the gain is origin-shift correctness and hidden bridge debt reduction without entering the compile wall.

Problem: Verification had to prove the patch without triggering Unity/dotnet compilation.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct `FaunaBrain` bridge grep, JSON parse, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, or Play Mode run was launched.
Rejected Alternatives: Running a rebuild for one C# source edit despite explicit rebuild discipline. Static gate is the accepted proof surface for this loop.
Scalability potential: CI can continue tracking residual hidden bridge debt by file; the top remaining files now start with SargassumMicroFaunaBoids, GlobalPhysicsStateManager, PersistentWorldRegistry, EcosystemDirector, WorldSpatialHashGrid, and then FaunaBrain.
Hardware Impact: Latest gate took 5.1 s. Hard blockers remain 0; runtime bridge review debt is 503. Unity/Burst/profiler proof remains pending.

## Loop 26 WorldSpatialHashGrid Runtime Bridge Decisions

Problem: `WorldSpatialHashGrid.cs` still contained thirteen direct hidden runtime-to-AUP bridge calls. These sat in query facades, transient event registration, registration/update maintenance, validation, and far-unload maintenance. The file already stores `Entry.AbsolutePosition`, so direct bridge calls obscured whether a coordinate came from a tracked AUP entry, a player-owned AUP, or a runtime boundary value.
Solution: Added a single finite `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` helper using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Public runtime-vector query overloads now resolve through that helper before native AUP collection. Register/update/validation/far-unload entry refresh routes use the same helper. Far-unload player motion now uses `IPlayerRuntimeContext.PlayerMovement.CurrentAup` instead of reconstructing player AUP from `PlayerTransform.position`.
Rejected Alternatives: Adding a new world-spatial AUP owner service or leaving `FromRuntimePosition` as a convenience wrapper. A new service would widen global surface without route-card proof. Leaving the wrapper keeps the exact hidden bridge pattern the scanner was created to expose. Converting every caller to an AUP overload was rejected in this loop because it would change a broad public API surface across other agents' domains.
Scalability potential: Low devices keep the existing broadphase cadence and quality-weighted caller budgets; the grid simply rejects invalid runtime boundary values earlier. Middle/high/ultra can increase spatial query density, acoustic density, and fauna/resource richness through existing quality weights while preserving the same AUP truth. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 503 -> 490, and `WorldSpatialHashGrid.cs` direct bridge hits dropped 13 -> 0. Runtime microsecond savings are not claimed; the gain is removal of hidden origin-shift ambiguity in a central broadphase facade without touching Unity rebuild or global ownership.

Problem: Verification still had to stay outside the compile wall.
Solution: Ran SHINOBU static gate, gate fixture self-test, Python bytecode compile, direct bridge grep on `WorldSpatialHashGrid.cs`, report count inspection, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Running a rebuild after a static bridge rewrite despite explicit user instruction and known external dependency wall history.
Scalability potential: CI now sees `WorldSpatialHashGrid` as explicit-helper routed, so remaining hidden bridge debt is concentrated in owner files that still need separate proof: Sargassum, PersistentWorldRegistry, GlobalPhysicsStateManager, EcosystemDirector, FaunaBrain, SpatialAudioManager, and thermal/player runtime slices.
Hardware Impact: Latest gate took 5.1 s with hard blockers 0. Broad Transform presentation reviews remain 936 and runtime bridge review debt is 490. Unity/Burst/profiler proof remains pending.

## Loop 27 GlobalPhysicsStateManager Runtime Bridge Decisions

Problem: `GlobalPhysicsStateManager.cs` still contained fifteen direct hidden runtime-to-AUP bridge calls in impact signal payloads, tracked rigidbody state refresh, origin-shift recovery, NaN recovery, sleep signal publication, and acoustic wake origins. Physics already has tracked `LastValidAup` and a current runtime origin signal, so direct `FromRuntimePosition` hid whether the coordinate was preserved authority or a runtime boundary value.
Solution: Routed impact signal construction/fallback, tracked body registration/update/recovery, queued impact points, acoustic wake origins, sleep signal fallback, and `TryResolveTrackedBodyAup` through finite `TryResolveAupFromRuntimeOrigin` helpers using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Existing `LastValidAup` is preserved when it is the known authority, and invalid runtime inputs fail closed before publishing AUP-backed events.
Rejected Alternatives: Keeping `AbsoluteUniversePosition.FromRuntimePosition` as a convenience bridge in physics, adding a new physics AUP owner service, or mass-converting physics public APIs to AUP overloads in one pass. The first keeps hidden origin ambiguity, the second widens global authority, and the third crosses other agents' call surfaces without route proof.
Scalability potential: Low devices retain the existing physics culling/sleep cadence and reject invalid runtime boundary values early. Middle/high/ultra can spend physics budget on richer collision feedback and acoustic wake presentation while the same AUP truth route remains fixed. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 490 -> 475, and direct bridge grep on `GlobalPhysicsStateManager.cs` returned zero raw `FromRuntimePosition`/`ToAbsoluteUniversePositionDouble3` hits outside helper routes. Runtime microsecond savings are not claimed; the gain is origin-shift correctness and reduced hidden authority debt in a central physics manager.

Problem: Verification had to prove the patch without triggering a compile wall.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `GlobalPhysicsStateManager.cs`, JSON parse checks, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Running a rebuild for static bridge cleanup despite explicit user instruction and known multi-agent dirty worktree.
Scalability potential: CI can now isolate remaining hidden bridge debt outside this central physics manager. Top remaining files are world/fauna/audio/thermal owners that need separate owner-route proof, not a global physics rewrite.
Hardware Impact: Latest gate took 5.2 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 475. Unity/Burst/profiler proof remains pending.

## Loop 28 SargassumMicroFaunaBoids Runtime Bridge Decisions

Problem: `SargassumMicroFaunaBoids.cs` contained sixteen direct hidden runtime-to-AUP bridge calls across statistical population dematerialization, migration registration, formation beacon/obstacle distance checks, sensory threat slots, panic inference, predator kill/acoustic/swarm signals, harvester anchor lookup, and camera distance gating. This mixed visual GPU boid data with AUP-backed world state without exposing the current-origin boundary.
Solution: Added a finite `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` helper using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. AUP-backed population, formation, sensory, signal, anchor, and camera-distance paths now resolve through that helper or fail closed. Predator rupture fluid decals remain visual/presentation-local even if AUP debris publication is unavailable.
Rejected Alternatives: Adding a new Sargassum-specific authority service, promoting GPU boid runtime positions into permanent AUP owners, or converting every boid/GPU buffer to AUP storage. The boid field is a Dear Lie visual swarm; only the population/signals/query boundaries need AUP authority. Full AUP storage would increase memory bandwidth and pollute a compute-driven visual system.
Scalability potential: Low devices keep cheap GPU boid presentation and can reduce boid count/cadence through existing quality decisions. Middle/high/ultra can increase boid density, acoustic feedback, and formation richness while using the same explicit AUP boundary route. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 475 -> 459, and direct bridge grep on `SargassumMicroFaunaBoids.cs` returned zero raw `FromRuntimePosition`/`ToAbsoluteUniversePositionDouble3` hits outside the helper. Runtime microsecond savings are not claimed; the win is origin-shift correctness without expanding the GPU boid payload.

Problem: Verification had to stay static and avoid shader/Unity import churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `SargassumMicroFaunaBoids.cs`, JSON parse checks, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, shader import, or profiler run was launched.
Rejected Alternatives: Running a rebuild or touching compute shader payloads while the patch only rewired C# boundary AUP conversion.
Scalability potential: Remaining bridge debt is now concentrated in `PersistentWorldRegistry`, `EcosystemDirector`, fauna/audio/player/thermal/world-resource owner files; each needs separate owner-route proof.
Hardware Impact: Latest gate took 5.1 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 459. Unity/Burst/profiler proof remains pending.

## Loop 29 PersistentWorldRegistry Runtime Bridge Decisions

Problem: `PersistentWorldRegistry.cs` contained fifteen direct hidden runtime-to-AUP bridge hits, fourteen of which were persistence/query boundary calls for mod protection, thermal vents, dropped items, flora/resource tombstones, chunk IDs, whale fall influence, cached fauna hibernation, and apex migration. These paths write or query persistent facts, so a hidden runtime bridge can silently bake the wrong origin into save-facing state after an origin shift.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` near the existing live-instance helper and rewired runtime persistence/query sites through it. Invalid runtime inputs or missing current-origin AUP now fail closed for mutations and return neutral values for queries. The existing `TryResolveLiveInstanceAup` now delegates to the same helper, removing duplicate conversion logic.
Rejected Alternatives: A blanket rewrite of the public `AbsoluteUniversePosition.FromRuntimePosition(Vector3)` wrapper was rejected. The remaining direct bridge in this file is the core public wrapper definition at line 86; changing that API during a staged cleanup would cross the compile wall and alter call semantics for other agents. Adding a new persistence AUP owner service was also rejected because the registry already owns persisted AUP records and only needs explicit runtime-boundary conversion.
Scalability potential: Low devices keep the same cheap persistent lookup/mutation cadence and reject invalid boundary values earlier. Middle/high/ultra can increase persistence richness, flora/resource density, and ecology queries through existing quality budgets while keeping one AUP authority route. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 459 -> 445, and direct bridge grep on `PersistentWorldRegistry.cs` now returns only the intentionally preserved public wrapper definition. Runtime microsecond savings are not claimed; the value is save/load origin-shift correctness and reduced hidden bridge debt in a central persistence owner.

Problem: Verification had to prove the registry edit without triggering a rebuild or touching the public AUP wrapper.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `PersistentWorldRegistry.cs`, JSON parse checks, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Running a rebuild for static helper rewrites despite explicit rebuild discipline, or marking the line 86 wrapper as debt to force an unsafe public API change.
Scalability potential: CI now isolates the remaining bridge debt outside this persistence pass; top remaining owner files start with `EcosystemDirector`, `FaunaBrain`, `SpatialAudioManager`, player kinematics, and thermal/world slices.
Hardware Impact: Latest gate took about 5.1 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 445. Unity/Burst/profiler proof remains pending.

## Loop 30 EcosystemDirector Runtime Bridge Decisions

Problem: `EcosystemDirector.cs` contained thirteen hidden runtime-to-AUP bridge calls in ecology LOD, organic mass, whale fall POI/acoustic, fauna mutation signals, apex territory fallback hits, player eye fallback, biomass impacts, sector quantization, biomass macro-cell quantization, and runtime AUP distance checks. This file owns ecology facts and black-box rings, so implicit origin conversion can corrupt sector/ecology state after an origin shift.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` beside the existing finite runtime-position check. Runtime boundary entry points now fail closed or return neutral values when current-origin AUP is unavailable. Existing owner AUP routes remain preferred: `hit.AbsolutePosition`, predicted/player `CurrentAup`, and AUP overloads are used before any helper fallback. Runtime sector/biomass quantizers became `TryQuantize*` helpers so invalid positions do not default into sector zero.
Rejected Alternatives: Keeping direct `FromRuntimePosition` in private quantizers was rejected because it hides the same origin route behind a smaller method. Blanket conversion of external organic/fauna/spatial APIs was rejected because those are neighboring ownership surfaces and would widen compile-wall churn. Adding a new ecology AUP service was rejected because this file already has contract signals and owned AUP overloads.
Scalability potential: Low devices keep the same ecology/biomass cadence and reject invalid boundary values cheaply. Middle/high/ultra can increase macro swarm, apex territory, fauna mutation, and biomass presentation richness through existing quality/cadence fields while coordinate truth stays fixed. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 445 -> 432, and direct bridge grep on `EcosystemDirector.cs` returned zero raw `FromRuntimePosition`/`ToAbsoluteUniversePositionDouble3` hits. Runtime microsecond savings are not claimed; the gain is preventing origin-shift corruption in ecology sector and signal state.

Problem: Verification had to stay static and avoid Unity/Burst import.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `EcosystemDirector.cs`, JSON parse checks, report count inspection, and targeted `git diff --check`. A PowerShell-incompatible Python heredoc command failed during report inspection; it was corrected with a native PowerShell JSON query and did not change source or reports.
Rejected Alternatives: Running dotnet/Unity rebuild for static helper rewrites, or changing neighboring APIs so every caller must pass AUP in one broad sweep.
Scalability potential: Remaining bridge debt is now concentrated in fauna/audio/player/thermal/world-resource owner files, each needing separate route proof.
Hardware Impact: Latest gate took 5.5 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 432. Unity/Burst/profiler proof remains pending.

## Loop 31 SpatialAudioManager Runtime Bridge Decisions

Problem: `SpatialAudioManager.cs` contained hidden runtime-to-AUP bridges in PlayAtPoint source frames, listener fallback, radar impact emitters, delayed fatal-pressure/inventory events, base interior muffle centers, voxel acoustic portal waypoints, habitat acoustic graph nodes, active world source cache fallback, and caption request fallback. Spatial audio uses several Dear Lie acoustic paths, but every AUP-backed delayed event, caption, portal, or cache still needs explicit origin proof.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` and changed `ResolveSourceAupFrame` to `TryResolveSourceAupFrame`. Source/listener/event paths now fail closed when the current-origin route is invalid. Interior muffle and portal graph cache writes skip or abort instead of storing hidden bridge AUPs. `AudioCaptionRequest` now marks `HasWorldAup=false` when the constructor cannot resolve a current-origin AUP, and its fallback resolver uses the same explicit helper.
Rejected Alternatives: Keeping audio bridge calls as harmless presentation logic was rejected because several paths enqueue delayed events, captions, acoustic portals, and cached AUPs. Converting all audio APIs to require AUP was rejected because it would cross broad gameplay/UI call surfaces and trigger compile-wall churn. Full physical acoustic propagation was rejected; the existing portal/muffle/virtual-voice Dear Lie remains the correct architecture.
Scalability potential: Low devices can skip invalid or distant audio work through existing LOD/virtualization limits. Middle/high/ultra can increase virtual voice richness, portal detail, and caption/audio feedback without changing coordinate authority. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 432 -> 421, and direct bridge grep on `SpatialAudioManager.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the gain is origin-shift correctness for delayed audio and acoustic portal state while preserving the cheap audio fake.

Problem: Verification had to stay static and avoid audio import/build churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `SpatialAudioManager.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, audio import, or profiler run was launched.
Rejected Alternatives: Running rebuild after audio source edits despite explicit command discipline, or broad API migration across every audio caller.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `PlayerKinematicsRuntime`, `AbyssalThermalManager`, `FloraInteractionManager`, `ResourceDistributionDirector`, and player/random-event slices.
Hardware Impact: Latest gate took 5.4 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 421. Unity/Burst/profiler proof remains pending.

## Loop 32 PlayerKinematicsRuntime Runtime Bridge Decisions

Problem: `PlayerKinematicsRuntime.cs` contained ten direct hidden runtime-to-AUP bridge calls in same-tick SDF squeeze sampling, movement acoustics, KCC velocity publication, SDF squeeze player state, glove scrape acoustic ping, staged sync writes, sync fence publication, current sync hash calculation, body fallback hash, and sync state rehash. These paths affect deterministic rollback and player authority, so a hidden runtime bridge is not acceptable.
Solution: Added `TryResolveAupFromRuntimeOrigin` overloads for `Vector3` and `float3`, using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Signal paths fail closed when current-origin AUP is unavailable. Sync hash paths return `0` or abort staged writes instead of hashing an unproven coordinate. SDF squeeze uses helper-resolved body AUP before writing the player kinematic vault.
Rejected Alternatives: Keeping direct bridges because this is the player owner was rejected; owner-local still needs explicit origin proof. A broad KCC API rewrite was rejected because it would cross physics/KCC ownership and compile-wall boundaries. Running a rebuild was rejected under explicit command discipline.
Scalability potential: Low devices keep the existing SDF squeeze cadence and player signal budgets. Middle/high/ultra can increase player feedback, SDF gradient fidelity, and acoustic richness through existing continuous quality controls while the same AUP truth route remains fixed. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 421 -> 411, and direct bridge grep on `PlayerKinematicsRuntime.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the value is rollback/hash/origin-shift correctness in player authority code.

Problem: Verification had to stay static and avoid KCC/physics compile churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `PlayerKinematicsRuntime.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Running build for source-only helper rewrites or changing KCC/physics public contracts in one pass.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `AbyssalThermalManager`, `FloraInteractionManager`, `ResourceDistributionDirector`, `HectonPlayerMotor`, and random-event/player-movement slices.
Hardware Impact: Latest gate took 5.7 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 411. Unity/Burst/profiler proof remains pending.

## Loop 33 AbyssalThermalManager Runtime Bridge Decisions

Problem: `AbyssalThermalManager.cs` contained ten hidden runtime-to-AUP bridge calls in vent attractor distance checks, thermal source/temperature/acoustic/impact signals, voxel insulation, voxel thermal melt handoff, cable/vent distance helpers, player-zone AUP, and cable visual player AUP. Thermal systems are mostly visual fakes, but their AUP-backed signals and voxel handoffs need explicit origin proof.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` and routed thermal signals, vent attractors, voxel insulation/melt, player-zone anchoring, cable visuals, and AUP distance helpers through it. Distance helpers return `double.MaxValue` or `float.MaxValue` when current-origin AUP is unavailable; signal paths skip AUP-backed publication instead of emitting poisoned coordinates.
Rejected Alternatives: Keeping `HectonFloatingOrigin.ToAbsoluteUniversePositionDouble3` inside voxel handoff was rejected because it hides the bridge the scanner tracks. Defaulting missing AUP to zero was rejected because it can activate cable/vent zones around sector zero. A full thermodynamics/voxel API migration was rejected as cross-domain churn.
Scalability potential: Low devices keep the cheap shader/thermal-map fake and reject invalid boundary values. Middle/high/ultra can increase smoke, thermal source, and voxel melt richness through existing budgets while using the same AUP truth route. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 411 -> 401, and direct bridge grep on `AbyssalThermalManager.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the value is origin-shift correctness for thermal signals and voxel handoffs.

Problem: Verification had to stay static and avoid shader/voxel import churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `AbyssalThermalManager.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, shader import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Running build for source-only helper rewrites or widening voxel/thermal public APIs.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `FloraInteractionManager`, `ResourceDistributionDirector`, `HectonPlayerMotor`, `RandomEventSystem`, `HectonPlayerMovement`, and voxel slices.
Hardware Impact: Latest gate took 7.3 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 401. Unity/Burst/profiler proof remains pending.

## Loop 34 FloraInteractionManager Runtime Bridge Decisions

Problem: `FloraInteractionManager.cs` contained nine hidden runtime-to-AUP bridge calls in kelp pushback, submarine wash shader AUP constants, player wake fallback, submarine propwash, sway-field center fallback, reactive flora spatial-hash registration, player cascade queries, cascade source propagation, and apex predator wake fallback. The file mixes GPU vegetation fakes with AUP-backed spatial hash and signal boundaries, so only the boundary facts should become AUP.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Kelp pushback, reactive flora registration, cascade spatial queries, player/submarine/apex wake publication, and submarine wash AUP shader constants now fail closed when current-origin AUP is unavailable. Cascade event centers, wake vectors, and vegetation matrices remain presentation/GPU data.
Rejected Alternatives: Promoting every vegetation matrix, cascade center, and wake shader vector into a persistent AUP owner was rejected because this would expand GPU payload bandwidth and confuse Dear Lie presentation data with simulation authority. Keeping `FromRuntimePosition` convenience calls was rejected because it hides origin proof at exactly the spatial-hash/signal boundaries that survive origin shifts.
Scalability potential: Low devices keep cheap shader/GPU vegetation fakes and can drop wake/cascade density through existing quality/cadence controls. Middle/high/ultra can increase vegetation density, cascade richness, wake feedback, and shader detail while the same explicit AUP truth route feeds spatial hash and signal facts. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 401 -> 392, and direct bridge grep on `FloraInteractionManager.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the gain is origin-shift correctness at flora spatial-hash and wake signal boundaries without expanding vegetation GPU state.

Problem: Verification had to account for an already dirty same-file worktree without reverting unrelated edits.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `FloraInteractionManager.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. Existing same-file edits unrelated to this loop were left intact. No dotnet build, Unity import, shader import, editor compile, Play Mode, or profiler run was launched.
Rejected Alternatives: Reverting unrelated flora job-finalization/math diffs, or running a rebuild to compensate for static-only bridge rewrites.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `ResourceDistributionDirector`, `HectonPlayerMotor`, `RandomEventSystem`, `HectonPlayerMovement`, voxel slices, tether/geology, and director AI.
Hardware Impact: Latest gate took 7.7 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 392. Unity/Burst/profiler proof remains pending.

## Loop 35 ResourceDistributionDirector Runtime Bridge Decisions

Problem: `ResourceDistributionDirector.cs` contained nine hidden runtime-to-AUP bridge calls in resource spawn sector keys, brine density/layer sampling, embedded-vein voxel handoff, and seismic shockwave seed generation. These paths decide persistence sector ownership, tombstone reinsertion, hazard sampling, or voxel absolute stamps; hidden runtime conversion can persist the wrong sector after origin shifts.
Solution: Added `TryResolveAupFromRuntimeOrigin(Vector3, out AbsoluteUniversePosition)` with `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Runtime spawn paths now fail closed before sector registration if the current-origin route is unavailable. Pillar-surface resources derive spawn AUP from the authoritative pillar AUP plus local surface offset. Brine layer sampling now uses the resolved AUP absolute coordinate instead of direct floating-origin offset reconstruction. Embedded-vein and shockwave seed paths require helper-resolved AUP before voxel absolute handoff or deterministic seed generation.
Rejected Alternatives: Extending `SpawnRequest` with a new AUP field was rejected in this pass because it would expand managed queue payloads and require a wider ghost-proxy contract review. Keeping `FromRuntimePosition` in resource spawn helpers was rejected because resource nodes are persistent facts, not visual-only objects. Seeding shockwaves from raw payload floats was rejected because it would lose sector authority and reduce deterministic spatial variance.
Scalability potential: Low devices keep the existing deterministic envelope spawner, pool warmup, and ghost-proxy raycast budget. Middle/high/ultra can increase resource richness, brine hazard fidelity, meteor events, and embedded-vein visuals while using the same explicit AUP truth route. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 392 -> 383, and direct bridge grep on `ResourceDistributionDirector.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the gain is persistence-sector correctness and explicit voxel/hazard AUP handoff.

Problem: Verification had to stay static and avoid resource/prefab/physics import churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `ResourceDistributionDirector.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, physics raycast test, or profiler run was launched.
Rejected Alternatives: Running a rebuild for a source-only AUP route cleanup or widening the spawn queue payload without a separate queue ABI review.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `HectonPlayerMotor`, `RandomEventSystem`, `HectonPlayerMovement`, voxel slices, tether/geology, director AI, and destructible organic owners.
Hardware Impact: Latest gate took 10.7 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 383. Unity/Burst/profiler proof remains pending.

## Loop 36 HectonPlayerMotor Runtime Bridge Decisions

Problem: `HectonPlayerMotor.cs` contained eight hidden runtime-to-AUP bridge calls in kinematic repair probe origins/snap points, wake silt decals, wall impact signals, KCC CCD consequences, SDF squeeze runtime sample localization, and SDF squeeze player state signals. These are player-authority and impact facts, so hidden origin conversion is not acceptable.
Solution: Added `TryResolveAupFromRuntimeOrigin` overloads using `GlobalSignals.CurrentRuntimeOriginAup()` plus `AbsoluteUniversePosition.OffsetMeters`. Repair probes and snap points now fail closed without an explicit AUP. Wake/impact/CCD/debris/squeeze signals publish only after helper-resolved AUP. SDF squeeze sampling subtracts helper-resolved origin AUP from helper-resolved sample AUP before local downcast, removing the direct floating-origin offset reconstruction.
Rejected Alternatives: Widening KCC, physics, or repair-target public contracts was rejected because this pass only needed boundary conversion and would otherwise cross compile-wall ownership. Keeping `FromRuntimePosition` because this is the player motor was rejected; owner-local still needs explicit current-origin proof. Using `CurrentTotalOffsetDouble` directly for SDF sample reconstruction was rejected because it bypasses the contract signal route.
Scalability potential: Low devices keep existing low-tier SDF sample mode, sweep cadence, and impact budgets. Middle/high/ultra can increase CCD feedback, decals, haptics, and SDF gradient detail while the AUP truth route remains fixed. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 383 -> 375, and direct bridge grep on `HectonPlayerMotor.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the value is player-authority origin-shift correctness and deterministic impact/squeeze state.

Problem: Verification had to stay static and avoid KCC/physics import churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `HectonPlayerMotor.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, editor compile, Play Mode, KCC scene test, or profiler run was launched.
Rejected Alternatives: Running a rebuild for source-only bridge cleanup or changing KCC job payloads without a separate ownership review.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `RandomEventSystem`, `HectonPlayerMovement`, voxel slices, tether/geology, director AI, destructible organic, and vehicle docking owners.
Hardware Impact: Latest gate took 18.3 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 375. Unity/Burst/profiler proof remains pending.

## Loop 37 RandomEventSystem Runtime Bridge Decisions

Problem: `RandomEventSystem.cs` contained eight hidden runtime-to-AUP bridge calls in meteor water splash feedback, delayed meteor thunder timing, seismic cave-collapse seed generation, seismic trench line AUP construction, target-volume range gating, and seismic impulse direction/distance. These paths create fluid feedback, delayed audio, voxel cave-collapse payloads, and physics impulses, so they cannot hide runtime-to-authority conversion behind convenience wrappers.
Solution: Meteor impacts now derive impact AUP from the already-owned player observer AUP plus a finite runtime delta from observer to impact. Seismic context now carries the player AUP, compares target volume range against `HectonVoxelVolume.GenerationAbsoluteUniversePositionDouble`, seeds the event from player AUP fields, builds the trench line from `playerAup.ToAbsoluteDouble3()`, and derives rigidbody impulse endpoints from epicenter AUP plus small runtime deltas before AUP/double subtraction.
Rejected Alternatives: Treating meteor splash/boom as pure presentation was rejected because the splash payload and delayed audio persist beyond the immediate visual fake. Calling `GlobalSignals.CurrentRuntimeOriginAup()` for every meteor or body endpoint was rejected where a stronger owner-local route existed. Rewriting voxel/physics APIs was rejected because it would cross domain ownership and compile-wall boundaries.
Scalability potential: Low devices keep the existing meteor shader globals, prewarmed splash fake, seismic overlap cap, and cave-collapse cadence. Middle/high/ultra can increase meteor flashes, splash polish, voxel stamp richness, and impulse feedback through existing continuous budgets while the same AUP truth route remains fixed. No binary quality switch was introduced.
Hardware Impact: Runtime AUP bridge review count dropped 375 -> 367, and direct bridge grep on `RandomEventSystem.cs` returned zero raw bridge calls. Runtime microsecond savings are not claimed; the gain is origin-shift correctness for random-event payloads while preserving the meteor/splash Dear Lie.

Problem: Verification had to stay static and avoid random-event, voxel, shader, and physics import churn.
Solution: Ran SHINOBU static gate, fixture self-test, Python bytecode compile, direct bridge grep on `RandomEventSystem.cs`, JSON parse checks, report top-file inspection, and targeted `git diff --check`. No dotnet build, Unity import, shader import, editor compile, Play Mode, physics scene test, or profiler run was launched.
Rejected Alternatives: Running a rebuild for source-only bridge cleanup or widening random-event payload structs without a separate ABI/layout review.
Scalability potential: Remaining bridge debt is now concentrated in `FaunaBrain`, `TetherInstance`, `WorldGenerativeGeologyTerrainSeamApplier`, `HectonVoxelVolume`, `HectonPlayerMovement`, `HectonVoxelEngine`, destructible organic, director AI, and vehicle/cable owner files.
Hardware Impact: Latest gate took 11.3 s with hard blockers 0, broad Transform presentation reviews 936, and runtime bridge review debt 367. Unity/Burst/profiler proof remains pending.
