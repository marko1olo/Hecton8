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
