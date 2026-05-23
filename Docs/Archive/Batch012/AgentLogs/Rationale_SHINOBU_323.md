PROMPT IDENTIFIED: SHINOBU_323
STATUS: PENDING VERIFICATION

Problem: Depth crush is assigned to SHINOBU_323 as Echelon 5 survival truth and must not depend on Unity trigger volumes or Physics.OverlapBox.
Solution: Use a stateless data-oriented pressure solver over AUP and unmanaged suit integrity/profile DTOs, executed in SIMULATION/POST_SIMULATION/VISUAL_SYNC boundaries.
Rejected Alternatives: BoxCollider death-zone, OnTriggerStay, Physics.OverlapBox, player-script depth literals, and direct health mutation are rejected because they are broadphase/managed routes for scalar hydrostatic math.
Scalability potential: Low uses slow cadence and cheap scalar pressure/fracture math; Middle raises cadence; High increases telemetry/visual feed density; Ultra spends saved CPU on shader/HUD/audio overkill, not more gameplay authority.
Hardware Impact: Expected low-end i3/MX350 gain is removal of PhysX broadphase/delegate callback overhead for depth damage. Exact measured gain absent until Unity profiler proof.

Problem: AUP depth can jitter at map edges if absolute positions are cast to float before sea-level subtraction.
Solution: Use double3 subtraction first: depthMeters = max(0, seaLevelAup.y - playerAup.y) after relative delta is computed, then cast relative Y to float.
Rejected Alternatives: Transform.position.y and absolute float AUP conversions are rejected because 100km map precision loss can synthesize false pressure spikes.
Scalability potential: Same authority math on all tiers; quality only changes cadence and visual presentation.
Hardware Impact: Double subtraction is O(1) per entity on SlowTick, cheaper than any collision query and stable on ARM64/Steam Deck/PC.

Problem: Runtime suit DTO must be ARM64-safe and mutable in NativeArray without CS1612 defensive copies.
Solution: Define SuitIntegrityDTO with explicit 32-byte layout and raw public fields at required offsets; mutate by ref in Burst jobs.
Rejected Alternatives: Properties, managed classes, bool fields, and Pack=1 are rejected as alignment/copy/GC hazards.
Scalability potential: 32-byte stride keeps predictable traversal from weak devices through high-end.
Hardware Impact: 32-byte stride fits two entries per 64-byte cache line; estimated sub-microsecond traversal for player-scale counts, pending profiler proof.

Problem: Core gameplay still contained a direct pressure damage mutation inside HectonSurvivalSystem, but Physiology cannot be referenced from Core without an assembly cycle.
Solution: Disable only ApplyPressureDamage integrity mutation and keep legacy pressure readouts as warning/UI scalars. New authority is ShinobuSuitIntegrityRuntime -> SuitIntegrityDTO -> CombatDamageSignal.
Rejected Alternatives: Adding a Core dependency on Hecton8.Physiology was rejected because Hecton8.Physiology already references Core; deleting broader survival code was rejected because the class also owns O2, thermal, save, and UI bridge surfaces.
Scalability potential: Low/Middle/High/Ultra all keep the same truth route; quality changes cadence and presentation only.
Hardware Impact: Removes old scalar integrity mutation and any future broadphase depth trigger route from the player survival tick. Estimate: 0.4 us scalar route saved, 8 us if trigger/OverlapBox fallback would have been active; profiler proof pending.

Problem: Visual crush must feel physical without CPU mesh deformation or post-volume mutations.
Solution: Publish a four-float Dear Lie payload to ShaderGlobalState slot 21: buckling, overpressure, integrity loss, and GlobalQualityWeight. The shader/HUD path can spend quality-weighted GPU work while gameplay truth remains in SuitIntegrityDTO.
Rejected Alternatives: Runtime mesh dents, material clone edits, and post-processing volume mutation were rejected as managed/object-oriented and unstable under many agents.
Scalability potential: Low uses slow cadence and small shader amplitude; Middle increases cadence; High and Ultra can spend the same scalar on more crack/noise samples without changing authority.
Hardware Impact: CPU cost is one Vector4 publish in LateFrameTick after job completion; visual cost is shifted to already-owned shader global dispatch.

Problem: Microfracture and implosion require deterministic survival authority but must not allocate or call combat code directly.
Solution: CalculateStructuralYieldJob integrates overpressure^2 * YieldConstant * dt under deterministic Burst, mutates SuitIntegrityDTO via UnsafeUtility.AsRef, and emits unmanaged CombatDamageSignal/MovementAcousticSignal.
Rejected Alternatives: math.pow for exponent 2, managed DamageInfo, AudioSource calls, and direct health mutation were rejected for determinism/GC/ownership reasons.
Scalability potential: Continuous GlobalQualityWeight affects cadence and acoustic interval; damage integration preserves total stress through dt.
Hardware Impact: Player-row target is under 1 us; no build/profiler proof yet because active dotnet/csc and CPU 100% blocked verification.

Problem: Cold suit pressure profiles need data-driven limits without ScriptableObject/runtime parsing debt.
Solution: suit_pressure_profiles.csv is parsed only during cold boot via FileStream into Vault-backed byte scratch, ReadOnlySpan<byte> token slices, FNV-1a suit-name hashes, and manual ASCII float parsing.
Rejected Alternatives: float.Parse, LINQ, ScriptableObject lookups, and runtime string keys were rejected as allocation/culture/hot-path hazards.
Scalability potential: All tiers read the same unmanaged profile DTOs; quality never changes suit identity or safe pressure truth.
Hardware Impact: Zero hot-path cost; cold boot reads at most 8192 bytes into Vault scratch.

Problem: Black-box crash proof is mandatory for pressure NaNs, budget breaches, and implosion.
Solution: SuitIntegrityTelemetryEntry[300] in Vault 72513 records frame, depth, ATM pressure, overpressure, fracture, integrity, shader buckling, flags, state hash, and execution microseconds. Fault path writes raw ReadOnlySpan<byte> rows to Dump_SHINOBU_323.bin.
Rejected Alternatives: Debug.Log-only fault reports and variable-length managed telemetry were rejected.
Scalability potential: Fixed 300 rows across all tiers; quality only changes cadence, not ring layout.
Hardware Impact: 64-byte telemetry rows total 19.2 KB; write is once per completed slow tick for player row.

Problem: Implosion damage must route through existing combat classification, not as an opaque readable hash.
Solution: Use a BarotraumaImplosion mask equal to Pressure | MicroFracture bits, then enqueue unmanaged CombatDamageSignal magnitude 9999f.
Rejected Alternatives: A four-character BIMP hash was rejected because CombatDamageRuntime resolves damage class from low bit masks; direct player health mutation remains rejected.
Scalability potential: Low/Middle/High/Ultra all share identical authority; presentation scale stays in Dear Lie shader/acoustic paths.
Hardware Impact: Same single queue write; avoids fallback/unknown damage-class branching downstream.

Problem: A missing player pose snapshot could accidentally reuse stale `_lastPlayerAup` and synthesize false hydrostatic pressure after player service swaps or startup gaps.
Solution: Add an explicit player AUP override path to `EvaluateHydrostaticPressureJob`. Valid snapshots use cached player AUP. Missing snapshots use mock AUP samples when emergency mock is enabled, otherwise sea-level AUP override so pressure resolves to 1 ATM and cannot implode the player from stale data.
Rejected Alternatives: Reusing default `AbsoluteUniversePosition`, reusing stale `_lastPlayerAup`, or blocking until player context appears were rejected because they create false pressure, bad audio AUPs, or hidden phase stalls.
Scalability potential: Low/Middle/High/Ultra keep the same authority behavior; quality only scales cadence and presentation.
Hardware Impact: One extra `double3` field and byte flag in the scheduled job; avoids fault recovery/dump churn and false combat signal routing.

Problem: Player combat target hash resolution was still doing a managed `PlayerObject.GetEntityId()` read every scheduled slow tick.
Solution: Return the cached hash once resolved and reset it only on cold rebind or player-service hot swap.
Rejected Alternatives: Querying the scene or GlobalRegistry inside the job is forbidden; resolving the GameObject id every slow tick is unnecessary cold-object work.
Scalability potential: All tiers benefit equally; cadence scaling reduces calls further, but the route now does zero repeated target-object reads after warm cache.
Hardware Impact: Saves a small managed object/property path per pressure tick on low-end CPUs; exact microseconds pending profiler.

Problem: New SHINOBU_323 Vault/Signal/shader route lacked a standalone architecture route card and top-of-ledger payload boundary.
Solution: Added `Docs/ARCHITECTURE/SHINOBU_323_SUIT_INTEGRITY_DEPTH_CRUSH_ROUTE_CARD.md` and inserted the buffer/ABI/fault/scalability route into `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`.
Rejected Alternatives: Leaving proof only in chat or the final log was rejected because integrators read the architecture files and route cards during cross-agent merge.
Scalability potential: Route card explicitly documents Low/Middle/High/Ultra behavior without changing gameplay truth, DTO identity, or damage route.
Hardware Impact: Documentation change only; reduces integration risk and prevents duplicate BufferID/authority ownership.

Problem: Public `TryGet*` read accessors used the same Vault resolve path as write-phase code, and disable/DataVault swap only cleared cached handles without releasing the Vault references.
Solution: Added `ReadVaultArray`/`ReadVaultBuffer` backed by `IDataVault.TryReadHandle` for `TryGetIntegrity`, `TryGetVisual`, `TryGetLatestTelemetry`, and `TryGetTuning`. Added `ReleaseVaultHandles` using `IDataVault.ReleaseBuffer` before handle clear on `OnDisable` and DataVault hot swap.
Rejected Alternatives: Leaving read accessors on `TryResolveHandle` was rejected because project doctrine names `TryGet*` as pure read accessors. Clearing handles without `ReleaseBuffer` was rejected because Vault lifecycle requires explicit reference release.
Scalability potential: Low/Middle/High/Ultra unchanged; this is memory lifecycle and authority hygiene, not gameplay fidelity.
Hardware Impact: Prevents reference leaks and stale generation descriptors during long sessions and service replacement; exact runtime microseconds unchanged.

Problem: `ResolveContinuousYieldScale` used ternary piecewise branches to select Low/Middle/High/Ultra presentation yield scales.
Solution: Replace the branch chain with a `math.smoothstep` + `math.lerp` cascade so GlobalQualityWeight continuously blends visual buckling presentation without CPU-side quality switches.
Rejected Alternatives: Keeping ternary quality bands was rejected because the mandate requires continuous scalability proof and the Burst hot math should avoid avoidable branch selection.
Scalability potential: Low remains cheap and conservative, Middle/High blend smoothly, Ultra reaches visual-overkill buckling presentation; truth layout, fracture accumulation, and damage route do not change.
Hardware Impact: Removes branch predictor pressure from the quality scaling path; exact microseconds pending Burst profiler.

Problem: Read-only subagent audit found a compile risk from `[WriteOnly]` on `NativeQueue<T>.ParallelWriter`, a possible cold SignalBus initialization route in `SlowTick`, and residual managed target-hash resolution inside the runtime tick.
Solution: Removed `[WriteOnly]` from both queue writer fields while preserving `[NoAlias, NativeDisableContainerSafetyRestriction]`; added `SignalBus<T>.HasNativeStorage` gates before opening writers; replaced slow-tick target hash refresh with cold `RefreshPlayerCombatTargetHashCold` during service bind/hot-swap.
Rejected Alternatives: Adding a new Core SignalBus API was rejected because it would touch a core header for a local domain polish pass. Leaving `ParallelWriter` unguarded was rejected because the property calls `EnsureInitialized`. Keeping `GetEntityId()` in `SlowTick` was rejected as a managed object route in simulation cadence.
Scalability potential: All tiers share the same signal and target identity route; quality cadence still changes only evaluation frequency and presentation.
Hardware Impact: Removes one compile-risk attribute combination and prevents cold lane allocation from simulation; target hash object access is now cold only.

Problem: Fault dump wrote raw telemetry directly to `FileStream` and left the predeclared dump scratch lane unused.
Solution: Activated `BufferID.ShinobuSuitIntegrityDumpScratch` as a 19232-byte Vault lane. Fault dump now stages the 32-byte little-endian header plus 300 x 64-byte telemetry rows into Vault scratch, then writes that fixed span to disk.
Rejected Alternatives: Direct telemetry pointer write was rejected after audit because it bypassed the reserved scratch lane and weakened forensic ownership proof. Removing disk dump was rejected because the task mandates `Dump_SHINOBU_323.bin`.
Scalability potential: Fault-only route; no gameplay cadence or presentation change.
Hardware Impact: No hot-path cost. Fault path performs one bounded `MemCpy` into Vault scratch before managed disk IO.

Problem: `CalculateStructuralYieldJob` writes telemetry at `_telemetryCursor`, which intentionally differs from the `IJobParallelFor.Execute(index)` lane after the first frame.
Solution: Added `NativeDisableParallelForRestriction` to Integrity, Visuals, and Telemetry in `CalculateStructuralYieldJob`; Integrity/Visuals still write index-local rows, while Telemetry is explicitly a ring-buffer writer fenced by `index == 0`.
Rejected Alternatives: Forcing telemetry to write only `Telemetry[index]` was rejected because it would destroy the 300-frame circular black-box contract. Splitting a tiny telemetry-only job was rejected because same-frame micro-jobs violate dispatcher amortization guidance.
Scalability potential: Low/Middle/High/Ultra unchanged; this is a safety annotation for an existing data-local write route.
Hardware Impact: Prevents Job Safety range exceptions in Editor/Development builds without adding memory traffic or runtime allocations.

Problem: Suit crush shader payload was written into shader global slot 21, but `GlobalShaderDispatcher` did not upload slot 21 when dispatcher-owned visual sync was active.
Solution: Add dispatcher-side `_HectonSuitCrushDearLieParams` and `_HectonSuitCrushBuckling` uploads sourced from `HectonShaderGlobalDataVaultBridge.SuitCrushDearLieSlot`.
Rejected Alternatives: Forcing the physiology runtime to call `Shader.SetGlobal*` directly was rejected because dispatcher-active mode intentionally centralizes CBuffer/global state writes. Adding a separate material or renderer mutation was rejected as a Dear Lie violation.
Scalability potential: Low uses the same scalar with minimal shader work; Middle/High/Ultra can increase shader crack/noise taps from the same payload without changing gameplay truth.
Hardware Impact: One Vector4 and one float command-buffer upload in the existing dispatcher pass; avoids CPU mesh deformation and keeps the visual cost on the GPU.

Problem: `GlobalQualityWeight` was blended into `fractureDelta`, causing suit survival truth to vary by hardware quality.
Solution: Remove quality from authority damage integration. Fracture truth is now strictly `overpressure^2 * YieldConstant * dt`; the continuous quality blend is retained only for visual buckling presentation and acoustic interval.
Rejected Alternatives: Keeping quality-weighted failure was rejected because Task 11 requires dt-scaled accuracy and doctrine states GlobalQualityWeight must not alter gameplay truth. A separate high-tier gameplay curve was rejected as rollback-hostile.
Scalability potential: Low sheds CPU through cadence and cheaper shader/audio presentation; Middle/High/Ultra buy visual overkill from the same scalar without changing implosion timing.
Hardware Impact: No extra work; removes cross-hardware desync risk and keeps the Burst path O(1).

Problem: The dedicated SHINOBU_323 physics scan report existed, but the shared `PHYSICS_OPTIMIZATION_REPORT.json` did not contain the SHINOBU_323 key required by Task 19.
Solution: Added `shinobu323DepthCrushScanner` with dedicated report path, finding count, forbidden runtime pattern count, and runtime route proof.
Rejected Alternatives: Leaving the proof only in the dedicated report was rejected because the task explicitly requests the shared physics optimization report summary.
Scalability potential: Documentation only; prevents integrator duplication and stale trigger routes across Low/Middle/High/Ultra builds.
Hardware Impact: No runtime cost; integration proof only.

Problem: Gated build verification failed before SHINOBU_323 could be isolated.
Solution: Recorded external compile blocker and did not edit `Gameplay/VRSomaticProvider.Comfort.cs` or `Gameplay/PlayerKinematicsRuntime_HandIK.cs` because they are outside the assigned domain.
Rejected Alternatives: Patching missing `VRSomaticKinematicStateMirrorDTO`, `VRSomaticComfortDTO`, or `PlayerHandIkConfigFlags` in Gameplay was rejected as cross-domain sabotage without ownership. Running repeated builds was rejected by the compile-wall protocol.
Scalability potential: SHINOBU_323 static proofs remain valid; final compile proof waits for external Gameplay dependency repair.
Hardware Impact: One single-core build attempt consumed 58.89s and failed externally; no further build pressure added.

Problem: Second read-only audit found that bridge and dispatcher maintained separate ShaderGlobalState cache handles, allowing no-vault publishers to skip direct shader globals while failing to write slot 21.
Solution: Added `HectonShaderGlobalDataVaultBridge.BindPreparedShaderGlobalSlots(IDataVault)` and call it from every successful `GlobalShaderDispatcher.EnsureShaderGlobalSlots` path.
Rejected Alternatives: Hot-polling `GlobalRegistry` inside the no-vault publisher was rejected because visual sync publishers should use cached services and prepared Vault state.
Scalability potential: Low/Middle/High/Ultra all receive the same suit-crush scalar; shader complexity still scales continuously from the payload.
Hardware Impact: One cached handle bind on existing dispatcher ensure path; prevents stale crush visuals without adding per-frame search.

Problem: Second audit identified broad safety bypasses and undocumented SignalBus writer attributes.
Solution: Restored SignalBus writers to the project-local `[WriteOnly, NoAlias]` pattern, removed unnecessary parallel-for bypass from initialization and visual writes, and added explicit safety proof comments for the retained unsafe integrity mutation and telemetry ring cursor write.
Rejected Alternatives: Replacing unsafe integrity mutation with copy-modify-store was rejected because the SHINOBU_323 prompt explicitly requires raw-pointer `UnsafeUtility.AsRef` mutation to avoid CS1612/property copy traps. Removing telemetry ring bypass was rejected because the black-box cursor intentionally differs from `Execute(index)`.
Scalability potential: No fidelity change; this is job safety hygiene across all tiers.
Hardware Impact: No added hot-path work; safer editor/development job validation and no extra allocations.

Problem: The proof artifacts drifted: the shared/dedicated physics reports still used stale CPU-gate compile wording, the route card named the wrong shared JSON key, and the mandatory self-audit existed only inside LOG history instead of a standalone report file.
Solution: Extracted the SHINOBU_323 prompt with an attribute-tolerant XML regex, created `Docs/Reports/SHINOBU_323_SELF_AUDIT.xml`, updated shared/dedicated report compile proof to the actual external Gameplay errors, and corrected the route-card key to `shinobu323DepthCrushScanner`.
Rejected Alternatives: Leaving stale report wording was rejected because integrators read JSON artifacts before chat history. Re-running dotnet build was rejected because the compile blocker is already identified outside this domain and repeated builds violate compile-wall discipline.
Scalability potential: Documentation/proof only; runtime Low/Middle/High/Ultra behavior remains unchanged.
Hardware Impact: No runtime cost; prevents integration churn and false verification status.

Problem: The route card still said low-tier quality used a conservative profile yield scale, which could be read as hardware quality altering survival failure truth.
Solution: Reworded the Low tier bullet to "Dear Lie presentation amplitude" so the document matches code: quality affects cadence and visual/acoustic presentation only, never fracture authority.
Rejected Alternatives: Leaving the ambiguous phrase was rejected because GlobalQualityWeight must not change gameplay truth ownership, DTO layout, save identity, or authority route.
Scalability potential: Low/Middle/High/Ultra presentation remains continuous while implosion timing remains hardware-independent.
Hardware Impact: Documentation only; removes integration ambiguity.

Problem: The SHINOBU_323 prompt explicitly names `MetabolicStateDTO` and `KinematicStateDTO` Vault arrays, while the previous solver used the cached player service snapshot as its primary AUP route.
Solution: Added read-only borrowed descriptors for `BufferID.PlayerKinematicState` (`LockstepPlayerKinematicState[1]`) and `ShinobuMetabolismConstants.MetabolismStatesBuffer` (`MetabolicStateDTO[1]`). `SlowTick` now resolves pressure from the Kinematic Vault AUP first, reads `MetabolicStateDTO.EntityHashID` for damage target identity, and uses `IPlayerRuntimeContext` only as bootstrap/editor fallback when the owner fact is absent.
Rejected Alternatives: Adding a Physiology reference to Gameplay or mutating the player kinematic/metabolic buffers was rejected as cross-domain ownership violation. Reading the Physics/KCC `KinematicStateDTO` lane was rejected because the active player-owned Core route is `LockstepPlayerKinematicState`; crossing into KCC would add sibling coupling and likely read the wrong authority fact. Allocating or ensuring the foreign buffers from SHINOBU_323 was rejected; the new route only binds existing descriptors and reads via `TryReadHandle`. Passing optional uncreated foreign `NativeArray` fields into Burst jobs was rejected because it would add job safety risk without improving authority.
Scalability potential: Low/Middle/High/Ultra all use the same borrowed truth route; quality still changes cadence and Dear Lie presentation only. On weak devices the fallback avoids scene queries; on high-end hardware the same scalar feeds richer shader/HUD response.
Hardware Impact: One cached read-only `LockstepPlayerKinematicState` row and one optional `MetabolicStateDTO` row per scheduled slow tick. No allocation, no lock, no release of foreign buffers, and no PhysX route. Expected cost remains sub-microsecond; profiler proof pending external Gameplay compile fix.

Problem: Borrowed Kinematic/Metabolic descriptors were bound during `EnsureVaultState`; if GameplayPlayer or metabolism owners created their buffers after SHINOBU_323 boot, the solver could stay on the fallback AUP route until a full service replacement.
Solution: Renamed the mutating private helper to `BorrowVaultArray` and made it late-bind an existing generation descriptor with `IDataVault.TryGetGenerationHandle` before reading via `TryReadHandle`. It does not call `EnsureGenerationHandle`, `TryLockBuffer`, or `ReleaseBuffer` for foreign buffers.
Rejected Alternatives: Polling `GlobalRegistry`, allocating the foreign buffers from SHINOBU_323, or forcing a job dependency on GameplayPlayer initialization were rejected. Keeping the stale fallback-only state was rejected because it weakens the prompt's Vault-backed input route.
Scalability potential: Low through Ultra share the same late-bound fact route; cadence scaling still controls how often the borrowed rows are sampled.
Hardware Impact: At worst one failed descriptor lookup per scheduled slow tick until the owner buffer exists. No heap allocation, no buffer growth, no additional job, and no main-thread completion.

Problem: `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` still described the older player-service fallback wording and the former incorrect OOP depth shared report key.
Solution: Updated the SHINOBU_323 ledger row to name the borrowed read-only `PlayerKinematicState` and `MetabolicStateDTO` input route, the no-foreign-ownership rule, and the correct shared report key `shinobu323DepthCrushScanner`.
Rejected Alternatives: Leaving the ledger stale was rejected because it is the cross-agent ABI and route authority document. Moving the bridge into a new contract or changing BufferIDs was rejected because no ABI change is needed.
Scalability potential: Documentation only; confirms the same Low/Middle/High/Ultra route already implemented.
Hardware Impact: No runtime cost; removes integration ambiguity and prevents duplicate pressure/AUP routes.

Problem: `SlowTick` cadence already used `GlobalQualityWeight`, but the accumulator advanced by the nominal 0.1s local constant. If `SystemDispatcher` stretches slow ticks to 0.2s or 1.0s during thermal/homeostasis shedding, pressure stress would under-integrate.
Solution: Cache `ITickDispatcher` during cold rebind/service replacement and integrate `_simulationAccumulator` from `ITickDispatcher.TimeSnapshot.Time` delta. `SimulationPaused` returns 0. The fallback nominal value is used only when the dispatcher is absent or its timestamp is invalid.
Rejected Alternatives: `Time.deltaTime` was rejected by tick-system law. Keeping the fixed 0.1s accumulator was rejected because it breaks Task 11's dt-preserved stress contract. Polling `GlobalRegistry` from `SlowTick` was rejected because dispatcher identity must be cached cold.
Scalability potential: Low/Middle/High/Ultra cadence remains continuous through `math.lerp(0.1f, 1.0f, 1.0f - quality)`, while the integrated fracture truth uses actual dispatcher elapsed time and remains hardware-independent.
Hardware Impact: Adds one cached interface read and one double subtraction per `SlowTick`; no new job, no GC, no extra Vault lane, and no main-thread completion.

Problem: A single target hash cache let cold player service fallback block later `MetabolicStateDTO.EntityHashID` authority. That violated the documented Metabolic -> Kinematic -> cold fallback priority.
Solution: Split target identity into `_metabolicDamageTargetHash`, `_kinematicDamageTargetHash`, and `_coldDamageTargetHash`; resolve in priority order at signal scheduling. DataVault and player service replacement clear all three and repopulate only the cold fallback from the player service.
Rejected Alternatives: Mutating the borrowed metabolic row, making cold service identity authoritative, or reading Gameplay concrete classes were rejected. The borrowed metabolic fact must override fallback without ownership transfer.
Scalability potential: Same target route on Low/Middle/High/Ultra; quality changes cadence and presentation only, not damage target identity.
Hardware Impact: Two extra uint fields and branch checks in a scheduled slow tick. No allocation, no new Vault lane, and no scene search.

Problem: `NonFinitePressure` persisted after a recovered pressure sample because volatile fault flags were not cleared before current-frame OR operations.
Solution: Clear `NonFinitePressure` in both pressure evaluation and structural yield volatile flag masks, while preserving sticky flags such as `Initialized` and `Imploded`.
Rejected Alternatives: Keeping sticky non-finite state was rejected because the black box would keep dumping/reporting recovered frames as faulted. Clearing all flags was rejected because implosion must remain sticky.
Scalability potential: All tiers receive identical fault semantics; quality never changes fault truth.
Hardware Impact: One additional bitmask clear in each job. No measurable cost expected.

Problem: `NativeDisableParallelForRestriction` was present on `Integrity` even though each job writes the lane matching `Execute(index)`.
Solution: Remove the restriction from lane-local `Integrity` in both Burst jobs; retain it only on `Telemetry`, whose black-box ring cursor intentionally differs from the parallel-for index.
Rejected Alternatives: Keeping broad bypasses was rejected because it hides partition mistakes. Splitting telemetry into a tiny second job was rejected because same-frame micro-jobs violate dispatcher amortization guidance.
Scalability potential: No fidelity change; safety surface is narrower across all tiers.
Hardware Impact: No added hot-path work; safer job validation in Editor/Development builds.

Problem: A non-finite borrowed player AUP or sea-level AUP could be sanitized into depth `0m`, then pressure `1 ATM`, without setting `NonFinitePressure`.
Solution: `EvaluateHydrostaticPressureJob` now checks `math.all(math.isfinite(playerAup))` and `math.all(math.isfinite(seaLevelAup))` before pressure conversion. Faulty input writes surface pressure only as a fail-closed row and still ORs `SuitIntegrityFlags.NonFinitePressure` for telemetry/dump routing.
Rejected Alternatives: Letting `ResolveDepthMetersFromAup` silently return 0 was rejected because it hides the actual upstream AUP fault and weakens the black-box autopsy. Throwing or completing jobs on fault was rejected because gameplay jobs must stay deterministic and non-blocking.
Scalability potential: Low/Middle/High/Ultra all share identical fault semantics; quality never changes AUP truth or fault ownership.
Hardware Impact: Two `double3` finite checks per active row. For player-row pressure this is sub-microsecond-scale and replaces a silent forensic blind spot.

Problem: Running `OOP_Depth_Scanner` would overwrite the detailed SHINOBU_323 report with a weak minimal JSON and count editor scanner string literals as candidate depth authority.
Solution: The scanner now scopes to Environment/Physiology/Player, skips `/Editor/` and scanner files as runtime authority, records ignored literals separately, and emits route card, self-audit, compile blocker, runtime route proof, and forbidden runtime pattern count.
Rejected Alternatives: Writing a separate scanner-only report was rejected because Task 19 names the dedicated physics report route. Keeping editor string hits in `findings` was rejected because it makes the static proof noisy and misleading.
Scalability potential: Documentation/tooling only; runtime Low/Middle/High/Ultra path unchanged.
Hardware Impact: Editor-only scan cost only; no player-frame work.

Problem: The retained telemetry cursor `NativeDisableParallelForRestriction` had labels but not a sufficiently substantive safety proof near the unsafe annotation.
Solution: Expanded the comments into three paragraphs proving single-lane writer behavior, Vault lock/read-accessor fencing, and why a separate tiny telemetry job was rejected.
Rejected Alternatives: Splitting telemetry into a one-row owner job was rejected because the current single-chain write is bounded and avoids same-frame micro-job overhead. Removing the telemetry ring cursor was rejected because the black-box contract requires a 300-frame circular history.
Scalability potential: No fidelity change; this is safety-documentation rigor.
Hardware Impact: Comments only; no runtime cost.
