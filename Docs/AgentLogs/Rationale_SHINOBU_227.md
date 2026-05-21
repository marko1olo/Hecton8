# Rationale_SHINOBU_227

Status: STATIC SCAN ONLY / SEAGLIDE SOURCES NOT IN GENERATED PROJECT / BUILD UNPROVEN
Agent: SHINOBU_227
Domain: Echelon 4 Player, Kinematics & Tools / Scooter (Seaglide) Kinematics

## Initial Architecture Decision

Problem: The assignment targets Seaglide propulsion paths that may still manipulate Rigidbody or run FixedUpdate locally.
Solution: Source-first archaeology, then replace or add a data-only Burst pipeline that emits force packets for the central physics owner.
Rejected Alternatives: Direct `Rigidbody.AddForce`, local `FixedUpdate`, object-instantiated cavitation, and per-frame registry polling. These violate PhysicsApplySystem ownership, Zero-GC, execution phase law, and global authority boundaries.
Scalability potential: Low uses coarse drag/current cadence and presentation fakes; Middle uses full thrust/drag cadence with cached flow; High increases telemetry/audio/VFX signal richness; Ultra spends saved cycles on visual cavitation/audio detail, not extra gameplay truth.
Hardware Impact: i3/MX350 target avoids main-thread PhysX sync and per-object component traversal. Estimated benefit cannot be claimed without profiler proof; static expected saving is removal of per-frame managed/component physics touchpoints.

## First-20-Minutes Route

Problem: Player handheld underwater travel must not produce control hitches in the opening route.
Solution: Treat Seaglide propulsion as a route blocker removal for player traversal responsiveness during Copper Wire exploration.
Rejected Alternatives: Delaying vehicle physics behind future input/physics agents; mock job isolates math under load without waiting for full player spawn path.
Scalability potential: Same movement truth across tiers, variable cadence and presentation richness.
Hardware Impact: Keeps MX350 hot path under suspicion threshold until profiler proof exists.

## Runtime Boundary

Problem: `MantaScooter` was the real Seaglide-equivalent owner; `Assets/_Project/Scripts/Equipment` is absent in this branch.
Solution: Remove `Rigidbody` storage/velocity reads from `MantaScooter`; it now derives propulsion input from cached `HectonPlayerMovement` AUP/runtime fields and writes `SeaglidePropulsionRequestDTO` to `SeaglideHydrodynamicsRuntime`.
Rejected Alternatives: Leaving `GetTransportPropulsionForce()` active in `HectonPlayerMovement` or reading `_playerRigidbody.linearVelocity` for movement/presentation. Both preserve the legacy component physics path.
Scalability potential: Low keeps authored motion with coarser hydrodynamic cadence; Middle/High/Ultra increase drag precision, flow fidelity, cavitation/audio richness through continuous quality.
Hardware Impact: i3/MX350 removes per-tool Rigidbody velocity polling and legacy force contribution from the player movement branch. Exact microseconds require Unity profiler; static expected saving is one managed component physics read plus one legacy force path per active tool tick.

## Burst Force Pipeline

Problem: Handheld propulsion needs water drag/current/strain without direct body mutation.
Solution: Added `SeaglideHydrodynamicsRuntime`, explicit-layout DTOs, Burst jobs for thrust, drag, current advection, metabolism, audio parameters, telemetry, and a `PhysicsApplySystem.SeaglideQueue` bridge.
Rejected Alternatives: Direct `Rigidbody.AddForce`, `FixedUpdate`, managed particle spawning, and binary quality tiers. Central `PhysicsApplySystem` remains the only body application point.
Scalability potential: Low uses dominant-axis speed and triangle-wave current fake; Middle blends toward quadratic drag; High samples Vault flow records; Ultra spends saved cycles on visual/audio signal detail, not extra gameplay truth.
Hardware Impact: i3/MX350 gets cache-aligned 64/128-byte DTO streaming and no hot managed allocations. The 1000-record mock generator exists to measure worst-case request pressure once build CPU is clear.

## Black Box And Editor Gates

Problem: A NaN force or layout drift must be diagnosable without chat history.
Solution: Added 300-entry `SeaglideTelemetryEntry` Vault ring, fault dump path `Docs/AgentLogs/Dump_SHINOBU_227.bin`, editor layout trap, x-ray window, scanner report, and debug force gizmo.
Rejected Alternatives: String logs after crash, runtime debug UI, and unchecked sequential layout.
Scalability potential: Low devices keep telemetry cheap; high devices use the same physical truth and richer editor/presentation diagnostics.
Hardware Impact: Hot path remains unmanaged. Editor allocations are isolated behind `#if UNITY_EDITOR`.

## Verification Gate

Problem: Final compile is mandatory but project rule forbids `dotnet build` when CPU load is above 50 percent or any `dotnet`/`csc` is active.
Solution: Checked CPU/process gate repeatedly. Latest check returned CPU_AVG=71 with no visible `dotnet`/`csc`/Unity process, still above the allowed CPU threshold. Static grep and `git diff --check` were used instead. Build remains blocked by protocol, not by an observed compiler error.
Rejected Alternatives: Launching `dotnet build` above the 50 percent CPU gate to claim compliance. That violates the explicit batch rule and risks starving the shared multi-agent workspace.
Scalability potential: No runtime code changed for this decision. Static gate confirms low/middle/high/ultra paths remain continuous `GlobalQualityWeight`, not binary switches.
Hardware Impact: Certified exact microseconds saved remain 0 until profiler/build evidence exists. Static expectation is removal of one Manta Rigidbody velocity poll and one legacy transport force path per active tool tick; not reported as measured.

## Ultra Polish Pass

Problem: The first static pass still had avoidable hot-path debt: per-solve force packet buffer clearing, fixed-rate hydrodynamic solve cadence under low quality, editor graph repaint allocation, and black-box telemetry that did not record enough final packet state.
Solution: Force packets now trust `ForcePackets` as the authoritative length and do not clear stale rows. Hydrodynamic solve cadence continuously lerps from the fixed tick toward 20 Hz and scales the emitted force by accumulated solver dt. Telemetry records last flow force, battery, compute micros, and budget faults. The editor graph scratch buffer is allocated once in the editor cold path.
Rejected Alternatives: A new `NativeQueue` route was rejected because the existing PhysicsApplySystem/Vault packet bridge is the active authority route and adding another global route without a route card would split ownership. A binary low/high physics switch was rejected because the quality law requires continuous degradation. Per-repaint editor arrays were rejected because they hide GC in diagnostics.
Scalability potential: Low quality sheds solver frequency and uses dominant-axis/triangle-current approximations; middle quality blends drag and cadence; high/ultra returns to fixed-tick solve cadence and spends saved cycles on richer visual/audio signals instead of heavier gameplay truth.
Hardware Impact: i3/MX350 expected static gain is removal of up to 131072 bytes of packet buffer writes per scheduled solve plus fewer low-quality hydrodynamic solves. Exact microseconds remain unmeasured because compile/profiler are blocked by CPU gate.

## SignalBus Closure Pass

Problem: The previous pass computed audio and cavitation DTOs but left the final DSP/VFX route as Vault data only. That satisfied data separation but not the literal signal-lane requirement for Task 02 and Task 11.
Solution: `SeaglideAudioSignalDTO` now carries `TargetEntityHash` and `FrameIndex` without changing its 64-byte size. After the Burst jobs complete, `SeaglideHydrodynamicsRuntime` publishes bounded `ToolAcousticSignal` and `BubbleSpawnSignal` packets through the existing typed `SignalBus` lanes. Signal lanes are warmed during cold boot.
Rejected Alternatives: Instantiating particle prefabs, creating a new bespoke VFX queue, or publishing all 1024 mock rows every frame. Existing global lanes already own audio/VFX transport and include load shedding; duplicating them would split authority.
Scalability potential: Low quality publishes one presentation signal packet with reduced bubble intensity; higher weights smoothly increase the publish budget up to four packets and restore full intensity. Physics truth stays unchanged.
Hardware Impact: i3/MX350 avoids prefab churn and DSP Rigidbody polling. Added work is a bounded post-solver signal publish over 1-4 packets, not over the entire 1024-row mock buffer.

## Compile Gate Recheck

Problem: A compile pass is required, but the build gate is meaningful only if the project files include the edited Seaglide sources and the CPU is below the mandated threshold.
Solution: Checked generated csproj coverage and process load. Current generated csproj files list `MantaScooter.cs` but not the newly added Seaglide source files. CPU then returned to 100 percent before any safe compile launch.
Rejected Alternatives: Running dotnet against stale csproj files to report a false pass, or running a build under the 100 percent CPU gate.
Scalability potential: No runtime behavior changed by this gate.
Hardware Impact: Certified measured savings remain 0 us until Unity regenerates project files and compile/profiler can run under the CPU rule.

## Global Systems Doctrine Pass

Problem: The previous pass still had two doctrine hazards: editor read accessors called allocation-capable buffer preparation, and runtime dependency refresh used `GlobalDataVault.TryGetLatestCreated()` as a fallback outside a route-carded core path.
Solution: `TryResolveEditorViews` and `TryResolveForcePacketEditorView` now only read cached handles and resolve existing Vault buffers. `RefreshColdDependencies` reads only `GlobalRegistry.DataVault`. `TrySubmitPlayerRequest` no longer installs the runtime; runtime installation moved to `RuntimeInitializeOnLoadMethod(AfterSceneLoad)` as a cold owner-phase step.
Rejected Alternatives: Keeping `TryGetLatestCreated()` as convenience recovery, hiding allocation in read-looking APIs, or creating runtime components from the player submit path.
Scalability potential: No gameplay truth change. Low-to-ultra scalability remains governed by `GlobalQualityWeight`; this pass reduces authority ambiguity.
Hardware Impact: i3/MX350 avoids accidental editor/read-path Vault growth and submit-time component installation. Exact microseconds remain unmeasured.

## Subagent Audit Response Pass

Problem: Static audit found four remaining route/doctrine defects: emergency mock generation could be triggered from live `FixedTick`, `TryResolveSeaglideBody` mutated a body-binding cache under a read-looking name, audio speed used raw AUP subtraction, and Seaglide BufferIDs `71660..71672` lacked binary ledger coverage. It also flagged Manta headlight presentation still entering through `GlobalSignals.Publish`, and the new Seaglide scripts lacked stable Unity `.meta` GUIDs.
Solution: Removed the mock seed branch from live `FixedTick` and restricted serialized emergency mock seeding to editor/development cold `OnEnable`. Renamed the mutating physics bridge helper to `BindSeaglideBodyForPacket`. Changed Doppler delta to `AupPrecisionMath.LocalDeltaDouble` plus `DowncastLocalDelta`. Replaced Manta headlight `GlobalSignals.Publish` calls with direct typed `SignalBus<SubmarineLightsChangedSignal>.TryPush` and cold lane warmup. Added the Seaglide Vault lane row to `BINARY_PAYLOAD_INTEGRATION_LEDGER.md`. Added stable `.meta` files for the Seaglide folder, editor folder, and six new C# scripts.
Rejected Alternatives: Leaving a hidden cold-job `Complete` reachable from the live fixed tick, claiming `GlobalSignals.Publish` was acceptable because it internally pushes SignalBus, reporting route GREEN without ledger coverage, or letting Unity mint local GUIDs on import. These would hide authority debt behind wrapper convenience.
Scalability potential: No authority truth changes. Low tier still sheds hydrodynamic solve cadence and presentation budget continuously; middle/high/ultra restore precision and richer signal publication without changing DTO layout or ownership.
Hardware Impact: i3/MX350 avoids a worst-case 1000-record mock generation and forced completion during a live physics tick. Measured microseconds remain 0 because build/profiler are blocked; the removed live path was a correctness risk, not a measured optimization.

## Boole Audit Response Pass

Problem: Follow-up audit found report overclaims and a black-box cadence gap. `PHYSICS_OPTIMIZATION_REPORT.json` said eradicated/no findings despite uncompiled Seaglide files, and solver-only telemetry could skip idle or cadence-shed fixed ticks.
Solution: Changed report/status language to static-scan-only with explicit blocked findings for CPU gate, stale generated project files, and missing runtime profiler proof. Added idle/cadence heartbeat writes to the 300-entry telemetry ring, with solver rows still carrying force totals. Corrected the route-card producer/consumer phase wording for post-finalization SignalBus publication.
Rejected Alternatives: Claiming GREEN because static grep was clean, or downscoping the black-box requirement to solver-only rows. Both would hide missing proof.
Scalability potential: Heartbeat writes are one 64-byte row per fixed tick when the solver is idle or cadence-shed; low quality gains black-box continuity while still shedding hydrodynamic solve work. High/ultra behavior is unchanged except for clearer proof rows when no active request exists.
Hardware Impact: i3/MX350 cost is one bounded 64-byte telemetry row on frames where the expensive solver is skipped. Certified measured savings remain 0 because compile/profiler are still blocked.

## Cicero Beauvoir Audit Response Pass

Problem: The next static audit found remaining production risks: Manta could retry `TryGetComponent` every resolve when the optional upgrade module was absent, hull-stress audio still polled `GlobalRegistry.AcousticZone` from the live path, Seaglide presentation lanes used `EnsureInitialized` without local `Configure` proof, the physics force bridge could fall back into `PlayerRuntimeContextService`, the scanner still trusted the absent Equipment path more than the actual Manta owner path, black-box early failure rows did not cover force-ready/invalid-delta/Vault/lock/prepare exits, the CSV parser existed without cold boot ingestion, and `SeaglideHydrodynamicsRuntime` still imported `Hecton8.World`.
Solution: Added a cold sentinel for the optional Manta upgrade lookup with a per-spawn reset, cached one movement snapshot per `UsePrimary`/`Tick` entry, cached the acoustic-zone controller during dependency refresh, configured `SubmarineLightsChangedSignal`, `ToolAcousticSignal`, and `BubbleSpawnSignal` lanes before `EnsureInitialized`, removed the `PlayerRuntimeContextService` fallback from `PhysicsApplySystem.SeaglideQueue`, expanded heartbeat writes for force-ready/invalid-delta/Vault/lock/prepare exits, wired `Data/Physics/seaglide_vehicle_profiles.csv` through Vault CSV scratch at cold boot, made CSV float parsing reject empty/sign-only/trailing-junk tokens, expanded the editor scanner to include `Assets/_Project/Scripts/Gameplay/MantaScooter.cs`, documented `NativeDisableParallelForRestriction` index-local invariants, and removed the unused `Hecton8.World` import from Seaglide runtime.
Rejected Alternatives: Hot `TryGetComponent`, hot `GlobalRegistry` polling, raw `EnsureInitialized`, a player-runtime fallback inside the central physics drain, scanner-only proof over a nonexistent Equipment path, managed `File.ReadAllBytes`, tolerant CSV partial-token parsing, and sibling-domain using residue. These would preserve hidden traversal, authority ambiguity, or compile-wall debt.
Scalability potential: Low keeps the same physical truth but sheds solver cadence and limits presentation packets while still recording heartbeat rows. Middle restores more drag/current precision. High and Ultra restore fixed-tick solve cadence and larger bounded presentation budgets without changing DTO layout, route ownership, or save identity.
Hardware Impact: i3/MX350 avoids repeated optional component searches, duplicate movement-context syncs inside the same Manta action/tick, a live registry lookup, false player-runtime body fallback, and unmanaged row omissions during skipped/failing solver frames. Measured microseconds remain 0 because Unity import, Burst compile, Play Mode, and profiler proof are still blocked by CPU/stale-project-file gates.

## XML CSV Contract And AUP Signal Conversion Pass

Problem: Task 16 names `seaglide_performance_profiles.csv`, while the previous cold ingest and proof files still named `seaglide_vehicle_profiles.csv`. Manta headlight AUP conversion also retained a direct `GlobalSignals.CurrentRuntimeOriginAup` helper call, which is a stale global bridge dependency even though the actual publish path was already typed `SignalBus<T>`.
Solution: Changed the primary Seaglide CSV contract to `Data/Physics/seaglide_performance_profiles.csv`, kept `Data/Physics/seaglide_vehicle_profiles.csv` as legacy fallback only, added the primary file, and updated scanner/report/status/architecture proof text. Manta headlight AUP now uses `AbsoluteUniversePosition.FromRuntimePosition` directly after finite local-position validation. The Seaglide runtime keeps one fully-qualified `Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition` payload conversion because `BubbleSpawnSignal.PositionAup` is that contract type; no World service import or polling was added.
Rejected Alternatives: Leaving the XML-mismatched CSV path, deleting the legacy file before integrator import, or routing Manta presentation through the old GlobalSignals origin helper. Those options preserve avoidable proof drift or global bridge residue.
Scalability potential: Low/Middle/High/Ultra physics truth remains unchanged. Tuning still hydrates once in cold boot; GlobalQualityWeight remains the only runtime quality continuum.
Hardware Impact: i3/MX350 hot path savings are structural only: no runtime file IO, no GlobalSignals origin helper call in Manta headlight presentation, and no extra solver work. Certified measured savings remain 0 us until Unity import/profiler proof exists.

## Fermat Audit Response Pass

Problem: Audit found that `PlayerRuntimeContextService.TryGetActiveRuntimeContext` was hidden behind `TryResolveSeaglideMovementState`, optional `TryGetComponent` could still be reached through battery/integrity resolver calls if cold prewarm failed, previous-AUP fallback used raw `currentAup - new double3` literals in Manta and the mock generator, and the scanner did not detect the new hot-upgrade or raw-AUP patterns.
Solution: Moved movement context access into explicit `RefreshSeaglideMovementStateSnapshot` calls at `UsePrimary` and `Tick` entry; downstream `TryResolveSeaglideMovementState` is now a pure cache read. Removed `CacheVehicleUpgradeModuleCold` from hot battery drain and max-integrity resolvers; optional upgrade discovery stays in `Awake`/`OnSpawn` cold hooks. Replaced previous-AUP literals with finite-gated `RewindAupByLocalVelocity` helpers in Manta and the Burst mock job. Expanded the editor scanner to catch stale hot-upgrade and raw-AUP literal regressions.
Rejected Alternatives: Pretending the PlayerRuntimeContextService dependency was fully eliminated without an upstream pushed movement DTO, leaving a first-use `TryGetComponent` in active propulsion accounting, or using raw AUP-minus literals because they were only fallback/mock paths.
Scalability potential: Low/Middle/High/Ultra math is unchanged. The pass reduces hidden hot-path traversal and keeps previous-AUP reconstruction deterministic across tiers.
Hardware Impact: i3/MX350 avoids first-use optional component traversal from active propulsion accounting and removes hidden service read from read-looking Manta accessors. Exact microseconds remain unmeasured; build/profiler are still blocked.

## Galileo Audit Response Pass

Problem: Static review found four remaining authority leaks: Manta still called `PlayerRuntimeContextService` inside its explicit movement snapshot, headlight shader/light mutation ran from player `Tick`, the Seaglide force drain resolved physics services from inside the drain method, and scheduled solver jobs were invisible to H8Memory active-job tracking.
Solution: `RefreshSeaglideMovementStateSnapshot` now reads only cached `HectonPlayerMovement` AUP/runtime/depth fields and computes velocity through `AupPrecisionMath.LocalDeltaDouble` before float downcast. Manta `Tick` now only queues headlight presentation; `LateFrameTick` performs shader/light mutation. `SeaglideHydrodynamicsRuntime` caches `PhysicsApplySystem` and `GlobalPhysicsStateManager` during cold dependency refresh/hotswap and passes them into `PhysicsApplySystem.SeaglideQueue`. The solver handle is registered with `H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, handle)` and cleared after deferred completion.
Rejected Alternatives: Keeping a bounded `PlayerRuntimeContextService` snapshot because it was explicit, resolving `PhysicsApplySystem` from the drain bridge for convenience, or letting presentation mutation remain in player tick because it was visual-only. Those choices preserve hidden route coupling or phase bleed.
Scalability potential: Low tier still sheds hydrodynamic cadence and publishes fewer presentation packets; Middle/High/Ultra restore cadence/precision without changing route ownership. Headlight presentation remains visually rich but is isolated to late-frame presentation rather than player gameplay tick.
Hardware Impact: i3/MX350 avoids one player-runtime context read per active Manta tick/action and two central physics service lookups per force drain. It also keeps scheduler visibility for deferred job ownership. Certified measured savings remain 0 us because Unity import/build/profiler proof is still blocked by CPU and stale generated project files.

## Boole Manta Lifecycle And Safety Proof Pass

Problem: Follow-up audit found a disabled-equipped Manta could remain registered as `IUpdatable`, `ResolveCurrentIntegrityNormalized` mutated lifecycle state under a read-looking name, emergency bailout could `AddComponent<MantaEmergencyWreck>` in a crash path, and Seaglide `NativeDisableParallelForRestriction` comments were too thin for the native collections mandate.
Solution: `OnDisable` now unregisters both tick and late-frame routes. `ResolveCurrentIntegrityNormalized` now reads either initialized integrity or max integrity without calling `EnsureTransportLifecycleInitialized`. `TrySpawnEmergencyBailoutWreck` now fails closed and despawns if the pooled prefab lacks `MantaEmergencyWreck`. Every Seaglide NativeDisable field group now has paragraph-level justification for index ownership, rejected alternatives, and dependency invariants.
Rejected Alternatives: Keeping `OnUnequip`/`OnDespawn` as the only unregister routes, allowing read accessors to initialize lifecycle state, adding missing components during bailout, or relying on one-line safety comments. Those options preserve dispatcher residue, accessor impurity, active crash-path component construction, or unverifiable Burst safety assumptions.
Scalability potential: Low tier avoids dead disabled tick targets and bailout-time component construction while keeping the same physical truth. Middle/High/Ultra retain continuous `GlobalQualityWeight` cadence, precision, and presentation budgets; the patch changes lifecycle discipline and proof quality, not DTO layout or authority routes.
Hardware Impact: i3/MX350 avoids stale dispatcher calls on disabled Manta instances and removes a crash-path `AddComponent` allocation/lifecycle spike. Exact microseconds remain unmeasured because Unity import/build/profiler proof is still blocked.

## Epicurus Route Boundary Pass

Problem: The remaining request route was still a concrete Gameplay -> `SeaglideHydrodynamicsRuntime.TrySubmitPlayerRequest` call, and `PhysicsApplySystem.SeaglideQueue` could perform a body-hash lookup plus body-binding mutation during force drain. Manta headlight AUP conversion also needed to stay independent of `GlobalSignals.CurrentRuntimeOriginAup` after the earlier proof text overclaimed that removal.
Solution: Added explicit-layout `SeaglidePropulsionRequestSignal` (192 bytes) and moved Manta request ingress to `SignalBus<SeaglidePropulsionRequestSignal>`. `SeaglideHydrodynamicsRuntime.FixedTick` now ingests the signal snapshot into Vault request/state rows before Burst scheduling. Removed the public direct submit API. Force drain now resolves only an already-bound body index; `TryFindTrackedBodyByFoldedEntityHash` is confined to `TryBindPlayerBodyCold` during cold dependency refresh, and unresolved drain packets set `FlagBodyBindingUnresolved` in counters/black-box rows before a fault dump. Manta headlight AUP conversion now subtracts cached predicted runtime position from the light runtime position, applies the local float delta to cached predicted AUP in double precision, and never reads GlobalSignals origin state.
Rejected Alternatives: Keeping direct runtime submit as "just a helper" was rejected because it is concrete cross-domain coupling. Keeping hash search in drain was rejected because first-miss force application would hide indexed dictionary or scan work inside the hot physics bridge. Moving body lookup into Manta was rejected because body identity belongs to central physics. Creating a new GlobalRegistry service was rejected because typed SignalBus plus existing Vault rows already form one route.
Scalability potential: Low keeps the same gameplay truth while SignalBus low-tier capacity sheds request bursts to four frame signals and hydrodynamic cadence trends to survival mode. Middle restores more signal capacity and solver cadence. High/Ultra keep fixed-tick solver cadence and richer presentation signal budget; the 192-byte request signal layout and Vault ownership do not change across tiers.
Hardware Impact: i3/MX350 avoids a concrete runtime call from Manta and removes hot miss-time body-hash lookup/mutation from the force drain. Exact microseconds remain unmeasured because Unity import/build/profiler proof is still blocked; static expected gain is bounded branch/index resolve in drain instead of hash lookup on unresolved packets.

## Zeno Body Binding Row Coverage Pass

Problem: Cold body binding resolved the player body once but wrote only `bodyBindings[0]`. Any valid force packet produced from a sparse or burst request row with `StateIndex > 0` could fail in `PhysicsApplySystem.SeaglideQueue` as unresolved even though the central physics body existed.
Solution: `TryBindPlayerBodyCold` now pre-fills every Seaglide body-binding row with the resolved player `RigidbodyIndex` and the matching row-local `StateIndex`. The only body-hash search remains cold dependency refresh; PostFixed drain remains a pure indexed resolve against `GlobalPhysicsStateManager.TryResolveTrackedBodyByIndex`.
Rejected Alternatives: Reintroducing a hash lookup in the drain was rejected because it hides central physics search work in the hot force bridge. Binding only row 0 was rejected because the SignalBus ingress and mock profiler support multiple request rows. Moving body binding into Manta was rejected because the body index is central physics authority.
Scalability potential: Low tier can shed request signal capacity to four rows without false unresolved rows; Middle/High/Ultra can process larger bounded request bursts without changing DTO layout or authority route. Visual overkill remains presentation-signal budget, not extra gameplay truth.
Hardware Impact: i3/MX350 cost is a cold O(1024) row fill only when dependencies bind or hotswap; hot drain stays O(packet window) with indexed validation. Measured microseconds remain 0 because Unity import/build/profiler proof is still blocked.

## Zeno Layout Proof Pass

Problem: The editor layout trap validated request signal size and offsets but not its 8-byte alignment. Runtime `SeaglideHydrodynamicsLayout` validated request DTO size but did not map `SeaglidePropulsionRequestDTO` offsets, leaving request offset proof editor-only.
Solution: Added `UnsafeUtility.AlignOf<SeaglidePropulsionRequestSignal>() == 8` to the editor trap and included state/request/request-signal alignment checks in the runtime layout validator. Added `OffsetOfRequest` for every `SeaglidePropulsionRequestDTO` field and wired runtime checks for the critical AUP/vector/target/surface/padding offsets.
Rejected Alternatives: Deferring to `[FieldOffset]` attributes alone, or leaving request offsets as editor-only proof. Both reduce the validator to prose/source convention instead of executable source proof.
Scalability potential: No gameplay truth change. Low/Middle/High/Ultra all keep the same aligned DTO layouts; higher tiers can safely consume the 192-byte request signal without changing authority route or payload shape.
Hardware Impact: Hot-path cost is zero after static initialization. The gain is risk removal: ARM64 alignment and offset drift fail at source validation instead of becoming a platform-only fault.

## Stale Request Cadence Fence Pass

Problem: Live `SeaglidePropulsionRequestSignal` input could be accepted on a low-quality fixed tick, then skipped by the continuous cadence throttle. If the next fixed tick had an empty SignalBus snapshot, `IngestPropulsionRequestSignals` returned without clearing `_activeRequestCount`, allowing the previous player command to survive into a later solve.
Solution: Added `_mockRequestsActive` as a separate cold-profiler fence. `GenerateMockPropulsionRequests` sets it; completed solver, disable, and Vault release clear it. Empty SignalBus snapshots now reset live `_activeRequestCount` unless the only surviving data is the cold emergency mock window.
Rejected Alternatives: Clearing all empty snapshots was rejected because it would make the mandated 1000-row cold mock generator impossible to profile under cadence shedding. Preserving all previous live requests was rejected because no-signal is not a gameplay command and would replay stale thrust.
Scalability potential: Low quality still sheds solver cadence toward 20 Hz, but it no longer changes gameplay truth by replaying an old input frame. Middle/High/Ultra behavior is unchanged except for the same stricter no-input reset.
Hardware Impact: i3/MX350 cost is two boolean assignments on no-signal paths. The removed risk is stale force application after cadence-shed or failed-prepare frames; certified measured savings remain 0 us until Unity profiler proof exists.

## Hilbert Stale Route Response Pass

Problem: Follow-up audit found three remaining stale-truth risks: the serialized `OnEnable` emergency mock seed could activate mock rows in editor/development play with no explicit profiler action, early fixed-tick failures wrote heartbeat rows with a previous `_activeRequestCount`, and active Manta previous-AUP fallback still subtracted local velocity displacement directly from absolute double3 AUP.
Solution: Removed automatic mock seeding from `OnEnable`; mock rows now activate only through explicit `GenerateMockPropulsionRequests()` from the editor/profiler path. Invalid fixed delta, failed Vault prepare, and failed runtime-buffer resolve call `ClearActiveRequestWindow()` before heartbeat telemetry. Manta and mock `RewindAupByLocalVelocity` now anchor a local AUP frame with `AupPrecisionMath.LocalDeltaDouble`, subtract displacement locally, and rehydrate to absolute double3.
Rejected Alternatives: Keeping serialized mock auto-seed was rejected because profiler data generation must be deliberate, not live-play side effect. Preserving old request counts in failure telemetry was rejected because black-box rows must describe the failed frame, not the previous accepted frame. Keeping raw absolute AUP rewind was rejected because it weakens the active-path precision proof even if the displacement is small.
Scalability potential: Low quality still sheds solver cadence, but no-input and failure frames now have zero live request authority. Middle/High/Ultra keep identical request semantics; only telemetry truth and fallback AUP math are tightened.
Hardware Impact: i3/MX350 cost is a few scalar assignments and one local-frame helper call only on previous-AUP fallback. The gain is correctness: no auto mock contamination, no stale request telemetry on early failure, and no raw absolute rewind in the active Manta path. Measured savings remain 0 us because runtime proof is blocked.

## Scanner Evidence Preservation Pass

Problem: `SeaglideRigidbodyAddForceScanner` wrote directly to `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json`. In the current batch that file already contains preserved reports for other agents, so a SHINOBU_227 editor menu run would destroy shared evidence while claiming validation.
Solution: The scanner now writes `Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_227.json` as the domain sidecar and non-destructively inserts or replaces only a top-level `shinobu227SeaglideScanner` property in the shared physics report. The existing shared report body remains intact.
Rejected Alternatives: Keeping a destructive overwrite, moving evidence to chat only, or abandoning the shared report requirement. Those options either lose multi-agent proof or fail Task 18's report artifact.
Scalability potential: No runtime behavior changed. Low/Middle/High/Ultra Seaglide cadence, force, and presentation paths remain governed by `GlobalQualityWeight`.
Hardware Impact: Runtime cost is 0 us because this is editor-only file IO. Measured runtime savings remain 0 us until Unity import/profiler proof exists.

## Archimedes Audit Response Pass

Problem: Static review found three remaining proof/authority defects: a failed `SignalBus<SeaglidePropulsionRequestSignal>.TryPush` still advanced Manta's previous accepted AUP baseline, `SeaglideHydrodynamicsRuntime.EnsureRuntimeInstance` could hide scene composition by creating or auto-installing the runtime, and layout proof only covered the primary three DTOs instead of every Seaglide DTO consumed by NativeArray/SignalBus lanes. It also found report drift around the actual `PhysicsApplySystem.SeaglideQueue` file path.

Solution: Manta now writes `_lastSeaglideAup` only after the request signal is accepted. Seaglide runtime no longer has a `RuntimeInitializeOnLoadMethod` installer and no longer calls `AddComponent<SeaglideHydrodynamicsRuntime>`; `EnsureRuntimeInstance` only returns an existing runtime on the registered `PhysicsApplySystem`. `SeaglideTelemetryEntry` now overlays a `ulong FrameAndRequestCountPacked` lane at offset 0 so `UnsafeUtility.AlignOf<SeaglideTelemetryEntry>()` is 8 while `FrameIndex` and `EvaluatedRequests` keep their existing offsets. Editor and runtime layout validators now check all Seaglide DTO alignments used in native arrays or SignalBus payloads. Docs and reports identify the actual bridge path as `Assets/_Project/Scripts/Physics/Seaglide/PhysicsApplySystem.SeaglideQueue.cs`.

Rejected Alternatives: Updating AUP cache before signal acceptance was rejected because a saturated lane would corrupt the next Doppler/velocity delta. Hidden runtime auto-install was rejected because scene composition and physics ownership must be explicit. Adding a new root `PhysicsApplySystem.cs` partial or moving the queue file was rejected because the current Seaglide-domain partial avoids touching a large shared file under multi-agent contention. Size-only DTO checks were rejected because ARM64 trap risk is alignment-sensitive, not just byte-count-sensitive.

Scalability potential: Low tier still sheds hydrodynamic cadence and presentation packet count continuously; Middle restores quadratic drag/current influence; High/Ultra keep fixed-tick solve cadence and richer audio/bubble signaling. This pass changes authority/proof integrity only; `GlobalQualityWeight` still does not change gameplay truth ownership, DTO layout, save identity, or route.

Hardware Impact: i3/MX350 avoids hidden scene-component construction and prevents dropped-signal AUP drift that could create force/audio spikes after thermal SignalBus shedding. Certified measured savings remain 0 us because Unity import/build/profiler proof is still blocked; structural risk removed is one hidden `AddComponent` path and one false accepted-AUP update path.

## DSP State And Explicit Denominator Guard Pass

Problem: Seaglide DSP publication reused `ToolAcousticSignal.StateLaserLoop`, so propeller strain could be classified as laser-loop audio by consumers. Cross-agent static math scanning also flagged two Seaglide reciprocal sites where the denominator was safe by previous local variables but not explicitly guarded at the `math.rcp` call site. The shared physics report was overwritten by another agent and again lacked the SHINOBU_227 scanner property.

Solution: Added the Seaglide-local `ToolAcousticStateSeaglidePropeller = 4` state and publish that state through the existing typed `ToolAcousticSignal` lane. Rewrote flow-cell and cavitation-range reciprocal denominators with explicit `math.max` guards at the call site. Expanded the editor scanner to cover `SeaglideHydrodynamicsJobs.cs`, stale laser-loop assignment, and stale unguarded reciprocal strings. Reinserted the SHINOBU_227 top-level report property while preserving the current SHINOBU_248 report body.

Rejected Alternatives: Editing `GlobalSignals.cs` to add a global enum was rejected because core contracts are shared and an existing typed lane already carries state bytes. Keeping laser-loop state was rejected as semantic signal drift. Leaving reciprocal guards implicit was rejected because static proof gates matter more than prose. Overwriting the shared report was rejected because it destroys other agents' evidence.

Scalability potential: Low/Middle/High/Ultra physical truth is unchanged. The dedicated state lets DSP/VFX consumers scale propeller presentation by `GlobalQualityWeight` without confusing it with laser-loop audio or changing the authority route.

Hardware Impact: i3/MX350 measured savings remain 0 us. The patch removes DSP misclassification and future NaN-proof regression risk without adding allocations, scene queries, extra force work, or a new physics route.

## Lorentz Audit Response Pass

Problem: Read-only audit found Manta still had three standard Unity fallback paths inside the Seaglide producer surface: `AudioSource.Play/Stop` motor fallback, `MaterialPropertyBlock` power-indicator mutation during active use, and ignored `SignalBus<SubmarineLightsChangedSignal>.TryPush` return values that could mark dropped light upserts/removes as published. The same audit found public player-runtime access to the blocking mock generator, grouped `NativeDisableParallelForRestriction` proof in Seaglide jobs, and a non-sequential `_pad1` tail name in `SeaglideAudioSignalDTO`.

Solution: Removed Manta `AudioSource`, motor clip/volume fields, mixer assignment, and all `.Play()`/`.Stop()` fallback calls; propeller presentation is DSP-only through the hydrodynamic `ToolAcousticSignal` route. Removed `MaterialPropertyBlock` and renderer property-block writes from the power indicator; Manta now caches only a compact power visual state byte. Changed headlight upsert/remove publication so masks advance only after accepted SignalBus pushes, with a bounded local drop counter/slot/operation for deterministic forensic state. Added hash gating for headlight global vector array uploads. Wrapped `GenerateMockPropulsionRequests` in `UNITY_EDITOR || DEVELOPMENT_BUILD`. Removed `NativeDisableParallelForRestriction`, unsafe pointer access, and `UnsafeUtility.AsRef` from Seaglide jobs, replacing them with index-local `NativeArray[index]` read/write. Renamed `SeaglideAudioSignalDTO` tail padding to `_pad0` and expanded scanner/report booleans for these gates.

Rejected Alternatives: Keeping AudioSource as a fallback was rejected because it reintroduces Unity object audio state outside DSP ownership. Keeping MPB writes with transition gating was rejected because the requirement was to delete the property-block route, not make it rarer. Ignoring failed SignalBus pushes was rejected because it corrupts the local published-mask proof. Keeping player-runtime mock completion reachable was rejected because blocking generation belongs to editor/development profiling only. Adding more `NativeDisable` comments was rejected because the code can keep Unity's parallel-for safety enabled.

Scalability potential: Low tier now fails silent on absent DSP rather than waking Unity audio playback, and late-frame headlight arrays upload only when payload hashes change. Middle/High/Ultra keep the same visual/audio routes but have cleaner DSP classification and fewer redundant managed shader-array uploads. No binary tier switch was introduced.

Hardware Impact: i3/MX350 measured savings remain 0 us. Static risk removed: Unity audio playback fallback, MPB renderer mutation, dropped SignalBus mask drift, player-runtime blocking mock completion, and disabled parallel-for safety. Hash-gated headlight upload reduces redundant late-frame `Shader.SetGlobalVectorArray` calls when payloads are unchanged; exact timing requires profiler proof.
