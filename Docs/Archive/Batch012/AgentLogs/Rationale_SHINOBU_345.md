# Rationale_SHINOBU_345

Status: POLISH LOOP 15 IN-FLIGHT CELESTIAL SNAPSHOT FAIL-CLOSED HARDENING - BUILD BLOCKED BY EXTERNAL DEPENDENCIES

## Decision 000 - Batch State Initialization
Problem: SHINOBU_345 had no durable status or rationale files for the current batch.
Solution: Created fresh batch-local files before code edits so progress, DOD notes, rejected alternatives, and verification state survive context loss.
Rejected Alternatives: Chat-only progress; it violates the batch protocol and loses state after compression.
Scalability potential: Low/Middle/High/Ultra unaffected; this is process state, not runtime code.
Hardware Impact: 0 us runtime impact on i3/MX350; files are editor/agent-only.

## Decision 001 - Reuse Existing Celestial And Tide Owners
Problem: The batch asks for celestial orbit math, but the repo already contains `HectonCelestialEngine` for presentation/runtime snapshots and `HectonSeismicTideDirector` for Vault-backed tide/celestial mechanics.
Solution: Patch the existing owners. Use `HectonSeismicTideDirector` for unmanaged Vault DTOs, Burst orbital math, eclipse scalar, tide handoff, mock timeline, telemetry, CSV, and editor tuner surfaces. Use `HectonCelestialEngine` only to stop object-driven sun-light transform rotation and consume math direction for presentation.
Rejected Alternatives: Creating `HectonCelestialManager`; it would duplicate global authority and conflict with existing `GlobalRegistry.CelestialEngine`, `CelestialEvents`, and `EclipseGameplayEventPayload` routes.
Scalability potential: Low uses lower precision/cadence math and shader fakes; Middle keeps stable deterministic orbit state; High/Ultra spend saved transform churn on richer shader lighting, caustics, and biolum consumers without changing gameplay truth.
Hardware Impact: Removing transform mutation from sun mechanics avoids Unity transform hierarchy synchronization on MX350/i3; expected saving is small per tick, estimated 5-20 us during celestial updates, with stronger benefit from avoiding needless shadow/light invalidation churn.

## Decision 002 - Keep Existing Eclipse Gameplay Lane
Problem: Eclipse events must reach gameplay without fragmenting the signal corridor.
Solution: Reuse existing `SignalBus<EclipseGameplayEventPayload>` configured by `HectonSeismicTideDirector` and existing `CelestialEvents` for celestial start/end/sun-angle consumers.
Rejected Alternatives: New `SunBlockedSignal` or string event; a private one-off signal violates signal lane segregation and duplicates matrix-listed `EclipseGameplayEvents`.
Scalability potential: Low coalesces bounded payloads; Middle/High consume normal snapshots; Ultra may add visual-only consumers in `VISUAL_SYNC` without increasing authoritative solve cost.
Hardware Impact: Existing bounded SignalBus path avoids managed delegate fan-out; estimated 0 B GC and sub-5 us dispatch overhead at current low event count.

## Decision 003 - Remove Runtime Sun Transform Authority
Problem: `HectonCelestialEngine` still derived day/night direction by writing `sunLight.transform.forward` and had a low-tier `Quaternion.Euler` physical-light lock path.
Solution: Replaced runtime sun motion with `ApplyMathematicalSunDirection(angleDegrees)`, cached `_resolvedSunDirection`, disabled the binary low-tier transform lock, and moved sun visual placement to the cached vector. Editor-only preview transform reads remain isolated to editor synchronization.
Rejected Alternatives: Continuing to rotate the Directional Light and smoothing it visually; that keeps transform hierarchy synchronization and shadow/light invalidation in the runtime authority path.
Scalability potential: Low uses the same math vector with lower solve cadence; Middle/High/Ultra can spend saved transform churn on shader sky, water specular, caustics, and biolum fakes without changing gameplay truth.
Hardware Impact: Removes per-update transform write and Quaternion comparison in runtime sun motion; estimated 5-20 us saved during celestial presentation updates on i3/MX350 and avoids light transform dirty propagation.

## Decision 004 - Split Celestial Optics From Environment Scalars
Problem: The old `CelestialStateDTO` mixed tide, eclipse, tremor, and time scalars in 32 bytes and could not satisfy the required 64-byte ARM64 optics layout.
Solution: Replaced `CelestialStateDTO` with the mandated 64-byte `double3 SunDirection`, `double3 MoonDirection`, `float EclipseShadowScalar01`, `float TimeOfDay01` layout, then moved tide/seismic/time truth into a separate 64-byte `EnvironmentStateDTO` Vault lane.
Rejected Alternatives: Extending the old DTO with extra fields; it would break the exact field offsets required by the batch and hide non-optical authority in the optics struct.
Scalability potential: Low/Middle read one cache-line optics truth and one cache-line environment truth; High/Ultra can add more shader consumers without expanding authoritative DTO width.
Hardware Impact: Explicit 64-byte rows preserve 8-byte double3 alignment and avoid ARM64 misaligned read penalties; estimated sub-10 us saved under multi-consumer cache pressure compared with scattered mixed scalar reads.

## Decision 005 - Polynomial Burst Orbit Kernel
Problem: Day/night and eclipse math must not depend on `Mathf.Sin(Time.time)` or full transcendental calls over float time.
Solution: Added `GenerateMockOrbitalTimeJob` and `EvaluateCelestialOrbitsJob` with double time, early modulo wrapping, polynomial sine/cosine, deterministic Burst attributes, quality-weight harmonic scaling, and dot-product eclipse scalar evaluation.
Rejected Alternatives: `math.sincos` over float phase; it is cheaper to write, but it violates the double-time precision requirement and gives less control over Low/Middle/High/Ultra math LOD.
Scalability potential: Low admits one harmonic and low-order polynomial blend; Middle admits more harmonics; High/Ultra lerp toward higher-order Taylor terms and higher cadence through `GlobalQualityWeight`.
Hardware Impact: Expected Burst job cost stays below 0.1 ms because it runs 1-4 harmonics on FrostTick cadence, not per frame; MX350/i3 estimate is 3-25 us per solve depending on quality.

## Decision 006 - Dear Lie Shader And Tide Link
Problem: The GPU and tide system need sun/moon/eclipse/tide truth without a Directional Light transform route.
Solution: Published `_HectonCelestialSunDirection`, `_HectonCelestialMoonDirection`, and `_HectonCelestialEclipseShadowScalar01` shader globals from the owner publish phase, and wrote the combined gravitational `double3 TideVector` into `EnvironmentStateDTO`.
Rejected Alternatives: Raycasts for eclipse or physical light rotation for shader alignment; both are slower and violate predictable math ownership.
Scalability potential: Low draws cheaper shader fakes from the same scalars; Middle/High/Ultra can increase shader-only sky/water/caustic richness.
Hardware Impact: Shader global upload is 2 vectors + 1 float per celestial publish; estimated below 5 us. Replaces ray/transform work with one dot product and three globals.

## Decision 007 - Proof And Tuning Facades
Problem: Designers need live orbit tuning and architecture needs static proof that OOP sun rotation is eradicated.
Solution: Added an `Orbital Mechanics Tuner` UI Toolkit entry backed by Vault orbital parameters, Scene View sun/moon/eclipse gizmos, `celestial_orbit_profiles.csv` polling, `OOP_Sun_Scanner`, and a non-destructive `PHYSICS_OPTIMIZATION_REPORT.json` section.
Rejected Alternatives: Separate runtime manager or inspector-only serialized fields; both bypass the Vault and create duplicate ownership.
Scalability potential: Low/Middle tune the same period/inclination rows; High/Ultra can push more visual shader overkill from unchanged Vault truth.
Hardware Impact: Editor-only cost is irrelevant to player runtime. Scanner report shows 0 forbidden rotation/time-sine hits in the 26-file CLI mirror; Unity Roslyn menu execution remains pending behind compile wall.

## Decision 008 - Compile Gate Obeyed
Problem: The batch requires compilation verification, but the workstation guard forbids launching `dotnet build` while CPU is above 50% or any `dotnet`/`csc` process is active.
Solution: Ran static validation only: forbidden pattern scan, JSON parse, and `git diff --check`. Rechecked after waits; CPU sampled 100%/72.46% and an active `dotnet` process remained, so no build was launched.
Rejected Alternatives: Forcing `dotnet build` to satisfy a checklist; this would violate the explicit hardware-protection rule and collide with another running compiler/tool process.
Scalability potential: Runtime code unaffected; process safety prevents wasted developer cycles on compile contention.
Hardware Impact: Avoided adding another build workload during active CPU pressure; runtime estimate unchanged.

## Decision 009 - Named Vault IDs And Owner ID Repair
Problem: Subagent audit found SHINOBU_345 raw `BufferID` casts colliding with HullIntegrity and Somatic buffers, and `(SystemID)74` aliasing GameplayCombat.
Solution: Reserved named `BufferID.Shinobu345*` values 73350..73372 in `H8Memory` and changed the seismic/celestial owner to `SystemID.HabitatAtmosphere`.
Rejected Alternatives: Leaving raw casts; it can silently corrupt unrelated Vault buffers and violates one fact/one owner routing.
Scalability potential: Low/Middle/High/Ultra all read the same correctly-owned Vault lanes; quality only changes cadence/math fidelity.
Hardware Impact: No direct microsecond gain; prevents cross-owner memory collision failures on low-end and high-end devices alike.

## Decision 010 - Vault Pointer Relocation Fence
Problem: Celestial Burst jobs exported raw Vault pointers without a lock window, allowing relocation/compaction risk while a scheduled job still owns those pointers.
Solution: Added owner-tagged `TryLockBuffer` calls for celestial write/read state, environment state, flow, tuning, mock timeline, and orbital parameter buffers before pointer export; release happens after commit, nonfinite failure, or pointer failure.
Rejected Alternatives: Same-frame pointer optimism; it is invalid under GlobalDataVault compaction rules.
Scalability potential: Continuous quality still controls solve cadence; locks are per FrostTick solve, not per frame visual work.
Hardware Impact: Estimated below 5 us per solve on i3/MX350; buys correctness against relocation faults.

## Decision 011 - Presentation Engine Stops Owning Celestial Truth
Problem: `HectonCelestialEngine` retained a second analytical solver and transform-based sun/planet-shine presentation writes.
Solution: Defaulted the legacy analytical solver off, removed runtime sun visual position/rotation plus planet-shine light rotation in favor of shader scalar/vector globals, and fenced the presentation class as a consumer rather than a celestial truth owner. Decision 017 supersedes the temporary global-snapshot read path with cached Vault reads from `Shinobu345CelestialStateRead` and `Shinobu345EnvironmentState`.
Rejected Alternatives: Keeping presentation as a second truth owner; this duplicates the SHINOBU_345 Vault solver and risks rollback drift.
Scalability potential: Low skips object movement and reads one immutable snapshot; Middle/High/Ultra can spend GPU shader work on sky/planet shine with the same route.
Hardware Impact: Removes residual transform sync writes; estimated 5-20 us per celestial presentation update on i3/MX350.

## Decision 012 - Merge-Safe Scanner And Report
Problem: `OOP_Sun_Scanner` wrote a whole JSON object and erased other agents' report sections.
Solution: Reworked the editor scanner to upsert only `shinobu345CelestialOrbitScanner`, preserve existing top-level JSON, and include additional transform assignment checks for LookRotation and sun visual position writes.
Rejected Alternatives: Whole-file replacement or chat-only scanner proof; both fail concurrent agent safety.
Scalability potential: Editor-only proof path; no runtime quality impact.
Hardware Impact: 0 us runtime impact.

## Decision 013 - Double CSV And Celestial Graph Correction
Problem: The celestial CSV route parsed into `float` immediately, and the Orbital Mechanics Tuner graph was plotting seismic telemetry instead of celestial orbit telemetry.
Solution: Added double CSV parsing for celestial tuning/orbital rows before final clamped float DTO assignment, and switched the graph to `CelestialTelemetryEntry` sun-angle/eclipse series.
Rejected Alternatives: `float.Parse`, string splitting, and misleading seismic graph reuse.
Scalability potential: Designers can tune Low/Middle/High/Ultra continuous curves without recompilation.
Hardware Impact: Editor/cold path only; 0 us player hot-path cost.

## Decision 014 - Seismic Evaluation Vault Fence
Problem: `SeismicEvaluationJob` still exported six raw Vault pointers without holding an owner lock across the dispatcher-owned job window.
Solution: Added `TryLockSeismicEvaluationVaultBuffers` and reverse-order unlock for events, seismic states, shake offset, turbidity spike, seismic telemetry, and mock silt buffers; unlock executes on pointer-open failure or after the completed job publishes outputs.
Rejected Alternatives: Trusting the Vault not to relocate during a scheduled job; that violates the same pointer lifetime rule already fixed for celestial mechanics.
Scalability potential: Low/Middle/High/Ultra all use identical ownership safety; `GlobalQualityWeight` still controls cadence continuously.
Hardware Impact: Expected below 5 us per scheduled seismic evaluation on i3/MX350; prevents relocation/corruption faults rather than chasing raw speed.

## Decision 015 - Compile Wall Boundary
Problem: Build verification was required after SHINOBU code changes, and the hardware guard eventually allowed one build attempt.
Solution: Ran `dotnet build Hecton8.Core.csproj --no-restore` once after confirming no `dotnet`/`csc` process and CPU samples 39.96/19.04/24.94. The build failed before SHINOBU diagnostics on unrelated missing DTO/interface errors in AirlockPressurization, HectonNarrativeDirector, and SolarPanel.
Rejected Alternatives: Fixing airlock, narrative, or solar code from this celestial domain; that would violate domain boundary and risk overwriting other agents.
Scalability potential: Runtime code unaffected; compile wall is external dependency state.
Hardware Impact: One build cost paid under allowed CPU conditions; no additional rebuild loop launched.

## Decision 016 - Default-Off Legacy Orbit Buffer
Problem: `HectonCelestialEngine` still allocated the legacy `_orbitJobOutput` persistent NativeArray at play boot even though SHINOBU_345 now owns the primary Vault-backed celestial truth and the analytical solver defaults off.
Solution: First gate the legacy allocation behind `enableAnalyticalOrbitSolver`; Loop 8 then removes the private array entirely by routing legacy fallback output through `BufferID.Shinobu345CelestialLegacyOrbitOutput` as a generation handle resolved only into method-local `NativeArray<CelestialOrbitJobOutput>` views.
Rejected Alternatives: Removing the fallback job entirely; that would be a broader presentation/editor compatibility change outside the current evidence window.
Scalability potential: Low/Middle/High/Ultra use the same SHINOBU Vault snapshot path by default; optional legacy fallback remains opt-in only.
Hardware Impact: Saves one private persistent NativeArray row allocation and sentinel registration on default startup; fallback async schedule now locks the Vault row during the dispatcher job window.

## Decision 017 - Presentation Vault Read Route
Problem: `HectonCelestialEngine` still consumed celestial truth through `GlobalRegistry.CelestialRuntimeSnapshot` inside the celestial cadence, making a read-looking presentation helper a hot registry poll.
Solution: Cache `IDataVault`, `Shinobu345CelestialStateRead`, and `Shinobu345EnvironmentState` generation handles during cold lifecycle, then read the two owner rows with `TryReadOnlyHandle` from `TryApplyPublishedCelestialSnapshot`. The method now fails closed to the legacy fallback if the cached descriptors are absent or stale.
Rejected Alternatives: Continuing to poll `GlobalRegistry.CelestialRuntimeSnapshot`; it violates the cold identity/hot data boundary and makes presentation a second global truth reader instead of a Vault consumer.
Scalability potential: Low reads two cache-line rows at reduced cadence; Middle/High/Ultra can spend the same truth on richer shader/GI responses without changing BufferIDs or DTO layout.
Hardware Impact: Removes one hot registry snapshot dependency per celestial cadence; expected microsecond gain is small, below 5 us, but the authority route is now deterministic and cache-local.

## Decision 018 - Cold Dependency Cache Sweep
Problem: Celestial presentation helpers still read ocean, weather, GI relay, underwater visuals, random events, dynamic resolution, world seed, biome matrix, and player context through `GlobalRegistry.*` from methods called by `SlowTick`.
Solution: Added a cold `RefreshColdRuntimeDependencies` cache and replaced those helper reads with fields. Registration/unregistration still uses the registry in lifecycle code; cadence helpers use cached references.
Rejected Alternatives: Broad event-driven hot-swap system in this patch; that would exceed the celestial orbit boundary and create more integration surface. The current conservative step removes hot polling without inventing new cross-domain signals.
Scalability potential: Low avoids global lookup churn on constrained silicon; Middle/High/Ultra preserve the same presentation richness and only adjust shader/cadence fidelity.
Hardware Impact: Sub-us per call on PC, but removes repeated service-slot reads from the celestial cadence and lowers branch/coupling pressure on i3/MX350.

## Decision 019 - Seismic SlowTick Registry Cut
Problem: `HectonSeismicTideDirector.SlowTick` refreshed registry dependencies and imported `GlobalRegistry.CelestialRuntimeSnapshot`, even though this owner is already the source of the current celestial snapshot.
Solution: Removed the `RefreshCachedRuntimeState` call from `SlowTick` and changed the cold refresh fallback time to use owner-local `_celestialSnapshot` rather than the global snapshot slot.
Rejected Alternatives: Polling global state from the owner cadence; it obscures one fact -> one owner and risks circular snapshot authority.
Scalability potential: Quality scaling remains owned by existing continuous `ResolveGlobalQualityWeight`; no binary quality switch or DTO route changes.
Hardware Impact: Avoids 1-5 us of slow-tick service refresh work and removes one redundant global snapshot read.

## Decision 020 - Scanner And Ledger Evidence Repair
Problem: The proof artifacts were internally inconsistent: scanner source claimed Roslyn but the shared report had zero syntax-node evidence, older status/rationale kept a 22-file count, ledger contained stale SHINOBU_346 BufferIDs and a historical `CelestialStateDTO=32` statement.
Solution: Hardened `OOP_Sun_Scanner` to include direct/aliased Transform rotation APIs and assignment forms, marked the shared report as static CLI pass plus Unity Roslyn pending, and amended the ledger/route card to state current `73350..73372`, `CelestialStateDTO=64`, and cached DataVault presentation consumption.
Rejected Alternatives: Leaving contradictions for integrator interpretation; stale proof is worse than an explicit yellow proof state.
Scalability potential: Documentation/editor route only; runtime Low/Middle/High/Ultra behavior unchanged.
Hardware Impact: 0 us runtime; prevents wrong BufferID/ABI review decisions that could corrupt memory ownership.

## Decision 021 - Presentation NativeArray Eviction
Problem: `HectonCelestialEngine` still owned scene-lifetime private `NativeArray` fields for celestial presentation blackbox, three atmosphere gradient LUTs, and legacy orbit fallback output. These were not authoritative orbit truth, but they still violated the batch H-PHI instruction to avoid manager-owned persistent native arrays.
Solution: Added named SHINOBU_345 Vault lanes `73393..73397` after detecting and rejecting the occupied `73373..73377` range used by SHINOBU_354. Presentation now stores only `VaultGenerationHandle<T>` descriptors and resolves method-local views through cached `IDataVault`. Async legacy fallback locks `Shinobu345CelestialLegacyOrbitOutput` before scheduling and unlocks after dispatcher finalization.
Rejected Alternatives: Keeping private arrays as presentation exceptions; that would leave a scanner-visible H-PHI violation. Using raw casts was also rejected after the collision scan caught SHINOBU_354 ownership at `73373..73379`.
Scalability potential: Low/Middle/High/Ultra all keep the same DTOs and BufferIDs; quality still changes solve cadence and visual richness only. The gradient LUTs remain fixed 8-sample visual caches and can be sampled cheaply on low-end hardware while high-end visuals consume the same shader-facing scalar/vector route.
Hardware Impact: Removes five private persistent NativeArray allocations from `HectonCelestialEngine`; expected runtime savings are small but deterministic ownership improves Vault compaction safety and removes native lifetime leak risk on i3/MX350.

## Decision 022 - Cold Allocation Purity For Presentation Scratch
Problem: The first Loop 8 Vault-backed presentation helper still mixed resolve semantics with `EnsureGenerationHandle`, so a method named like a read/resolve accessor could allocate or grow Vault rows if called from a cadence path. The cold helper also accepted any nonzero generation descriptor as valid without proving the row still resolved and met the required length.
Solution: Split presentation lifecycle from reads. `RefreshColdRuntimeDependencies` calls `EnsureColdCelestialPresentationVaultHandles`, and that cold helper validates BufferID, generation, `TryResolveHandle`, `IsCreated`, and minimum length before re-ensuring stale or short rows. Runtime/cadence helpers call only `TryResolveExistingCelestialPresentationBuffer`, which uses cached `IDataVault.TryResolveHandle` and fails closed without allocation, growth, job completion, global mutation, scene search, or registry polling. The old `EnsureCelestialRuntimeBuffers` probe name was removed so `OnEnable` now refreshes cold dependencies before `TryResolveCelestialRuntimeBuffers`.
Rejected Alternatives: Leaving `EnsureGenerationHandle` inside read-looking helpers; it violates accessor purity and can hide lifecycle work in presentation cadence. Polling `GlobalRegistry` again was also rejected because the presentation owner already has cold cached `IDataVault` and generation descriptors.
Scalability potential: Low/Middle/High/Ultra all keep the same BufferIDs and DTO layouts. Quality may change how often presentation samples/shader scalars are consumed, but missing scratch rows degrade to direct gradient evaluation or legacy fallback skip rather than changing gameplay truth.
Hardware Impact: No intended hot-path speed gain; this is route safety. Low-end i3/MX350 avoids surprise Vault ensure/grow work on presentation cadence, while high-end devices keep the same shader overkill path backed by cold-created rows.

## Decision 023 - Read Accessor Transitive Purity
Problem: Loop 9 removed hidden allocation from `TryResolve*`, but `TryResolveCelestialBlackBoxBuffer` still cleared/reset telemetry state and gradient sampling helpers refreshed dirty rows. Because `ResolveScriptSunsetCloudColor`, `ResolveScriptNightCloudColor`, and `ResolveScriptSunsetHorizonColor` call those samplers, a method with a read-style `Resolve*` name could still mutate Vault scratch.
Solution: Moved blackbox clearing to cold handle regeneration and disposal through `ResetCelestialBlackBoxState`. Kept `TryResolveAtmosphereGradientSamples`, `TryResolveCelestialBlackBoxBuffer`, and `TryResolveExistingCelestialPresentationBuffer` as pure existing-view probes. Moved dirty gradient rebuilds into explicit command paths: cold `EnsureColdCelestialPresentationVaultHandles`, runtime `MarkAtmosphereGradientSamplesDirty` after the cached Vault exists, and LUT evaluation refresh.
Rejected Alternatives: Leaving refresh/reset work in `TryResolve*` or in `SampleSunset/Night`; both hide O(N) memory writes inside read accessors and violate the Global Systems Doctrine. Rebuilding directly in each `ResolveScript*` color helper was also rejected because material binding calls must not become scratch owners.
Scalability potential: Low devices now avoid surprise 300-entry blackbox clears or 24-sample gradient rewrites from read paths; Middle/High/Ultra keep the same shader-facing vectors and colors, with richer visuals consuming already-prepared scratch rows.
Hardware Impact: Steady-state hot-path gain is small but measurable under cadence pressure: removes hidden O(300) blackbox clear risk and hidden 24-row gradient writes from read helpers. On i3/MX350 this is route safety plus avoidance of micro-stutter spikes; on high-tier hardware it preserves predictable presentation scheduling.

## Decision 024 - Command Names For Mutable Caches
Problem: After the Vault accessor fix, several remaining SHINOBU presentation helpers still used read-style names while mutating managed caches: firmament compute/kernels, sun direction cache, `UniversalAdditionalLightData`, and sun-disc renderer.
Solution: Renamed firmament and sun-direction mutable helpers to `EnsureFirmamentBakeCompute`, `EnsureFirmamentKernels`, and `EnsureSunDirectionCache`. Loop 14 superseded the component-cache portion by moving the `TryGetComponent` probes to cold lifecycle helpers and leaving cadence reads as pure cached reads.
Rejected Alternatives: Leaving component probes behind `Resolve*`, `TryResolve*`, or `Get*`; this would preserve misleading accessor surfaces and make future hot-path audits treat mutating code as read-only. Loop 14 permits `GetCachedSunDiscRenderer` only because it no longer probes and returns an already-cached field.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The change protects maintainability: quality can scale presentation richness while mutable cache prep remains explicit command work.
Hardware Impact: 0 us direct runtime speed change. Indirect low-end impact is audit safety: fewer chances that a future cadence path hides component lookup, asset load, or cache writes behind a read accessor.

## Decision 025 - Owner Readbacks Use ReadOnly Vault Views
Problem: `HectonSeismicTideDirector` had read accessors that did not allocate or ensure, but still opened mutable `NativeArray<T>` views through `TryOpenVaultBuffer`. `ResolveGlobalQualityWeight` also mutated the quality filter behind a read-style name, and `ReadCelestialTuning` could call that fallback path.
Solution: Added `TryReadOnlyVaultBuffer<T>` over `IDataVault.TryReadOnlyHandle`. Moved `TryReadCelestialState`, `TryReadEnvironmentState`, `TryReadCelestialFlow`, `ReadCelestialTuning`, `ReadSeismicTuning`, and `ReadWaterSurfaceAupYOrTide` to immutable read-only views. Renamed the mutating quality filter to `UpdateGlobalQualityWeight`; `ReadCelestialTuning` now uses the cached quality scalar without advancing the filter. Editor helpers that allocate/grow tuning rows were renamed to `EnsureTuningBuffers` and `EnsureOrbitalParameters`.
Rejected Alternatives: Keeping mutable read views because the methods only copied index 0; this leaves accidental write capability in read contracts. Keeping `ResolveGlobalQualityWeight` was rejected because it writes `_globalQualityWeight`, `_lastQualityFilterFrame`, and `_qualityFilterPrimed`.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. Continuous quality still controls cadence and math richness, but readbacks no longer mutate the smoothing filter or expose writable Vault rows to consumers.
Hardware Impact: Direct runtime speed change is negligible. The safety gain is preventing accidental cache-line writes and hidden state mutation from read paths, which is more important on i3/MX350 where small stalls and invalidations are visible.

## Decision 026 - Fallback Orbit State ABI Explicitness
Problem: SHINOBU primary Vault DTOs were explicit and padded, but `HectonCelestialEngine.CinematicOrbitState` remained implicit sequential layout even though it participates in the opt-in Burst analytical fallback path.
Solution: Marked `CinematicOrbitState` as `[StructLayout(LayoutKind.Explicit, Size = 32)]` with `RegistryOffset` at 0, `Direction` at 12, `Phase01` at 24, and `Fullness01` at 28. Re-ran struct body scans for auto-properties, `Pack=` markers, and explicit size multiples of 8 in the SHINOBU runtime scope.
Rejected Alternatives: Leaving the struct implicit because it is a local fallback helper; fallback Burst structs still need stable ABI when they cross job codegen boundaries.
Scalability potential: Low/Middle/High/Ultra behavior is unchanged. The opt-in fallback remains a consumer-side compatibility route; primary scalable orbit truth stays in the 64-byte Vault DTOs and continuous quality controls cadence/math richness.
Hardware Impact: No direct microsecond gain claimed. This removes layout ambiguity and prevents future ARM64 alignment drift in the fallback orbit path, which matters most on Quest-class and i3/MX350-class devices.

## Decision 027 - Hot Cadence Allocation And Component Probe Cut
Problem: Subagent audit found three hot-cadence risks after the orbit solver hardening. `HectonSeismicTideDirector.SlowTick` still called `EnsureTelemetryRing` and `EnsureSeismicVaultBuffers`, so stale handles could trigger Vault ensure/grow work from SlowTick. `HectonCelestialEngine.ShouldCullCelestialForAbyss` still queried `PlayerRuntimeContextService.TryGetActiveRuntimeContext` instead of the cached player context. Two presentation helpers could still reach `TryGetComponent` from `RunCelestialTimeline`: sun cookie `UniversalAdditionalLightData` and sun-disc renderer resolution. A second audit found proof drift where the scanner source omitted `73393..73397` presentation scratch and older checklist text still described published-snapshot consumption.
Solution: Removed the SlowTick Vault ensure calls; the buffers are established by `InitializeService`, and cadence paths now fail closed through existing handle probes. Changed abyssal culling to read `PlayerMovementRuntimeState` from `_cachedPlayerContext.TryGetMovementRuntimeState`. Moved sun-light and sun-disc component probes to cold lifecycle helpers `CacheSunAdditionalLightDataCold` and `CacheSunDiscRendererCold`; cadence now uses `TryGetCachedSunAdditionalLightData` / `GetCachedSunDiscRenderer` only. Updated scanner-generated BufferID text and stale checklist route wording.
Rejected Alternatives: Retrying Vault ensure from SlowTick to recover missing rows; it hides allocation/grow work in cadence. Repeating `TryGetComponent` until success was rejected because a missing component would become a persistent hot probe. Editing broader player-runtime ownership was rejected because the existing cached interface already provides the pure read API.
Scalability potential: Low devices shed hidden cadence spikes and static lookup work; Middle/High/Ultra keep the same Vault truth, shader globals, and presentation richness. Quality still controls cadence/visual work continuously and does not change DTO layout or ownership route.
Hardware Impact: SlowTick now avoids possible Vault ensure/grow work under stale-handle conditions; component-probe fixes remove repeated Unity component lookup risk. Direct steady-state gain is sub-us on i3/MX350, but worst-case cadence spikes are removed from the celestial path.

## Decision 028 - In-Flight Celestial Solve Uses Last Valid Vault Snapshot
Problem: `ResolveCelestialSolve` treated an asynchronously scheduled but unfinished `EvaluateCelestialOrbitsJob` as a solve failure. On a `SlowTick` publish path this could emit the hardcoded emergency sun/moon vectors before the dispatcher finalized the real job, causing one-cadence sunlight, tide, and eclipse truth to snap to defaults.
Solution: Added `TryReadCachedCelestialSolve`, a pure read-only composition path over `TryReadCelestialState`, `TryReadEnvironmentState`, and `TryReadCelestialFlow`. When the job is in flight, a Vault lock is temporarily unavailable, or the cadence chooses not to reschedule, the owner now prefers the last finite `Shinobu345CelestialStateRead` / `Shinobu345EnvironmentState` snapshot and derived local tide value. The hardcoded fallback remains only for first-boot/no-valid-snapshot failure.
Rejected Alternatives: Blocking with `.Complete()` inside `ResolveCelestialSolve` was rejected because the dispatcher owns completion windows. Publishing the hardcoded fallback was rejected because it changes visible/gameplay truth during normal job latency. Mutating `_cachedTide` inside the read-style helper was rejected to preserve accessor purity.
Scalability potential: Low devices benefit most because lower quality runs longer solve intervals and is more likely to reuse cached celestial truth; Middle/High/Ultra still get higher cadence and richer shader visuals without DTO, BufferID, save, or authority route changes.
Hardware Impact: No direct microsecond speed claim. The gain is eliminating a default-vector churn path that could trigger unnecessary shader/global updates and downstream presentation reactions on i3/MX350-class hardware while preserving non-blocking job scheduling.

## Decision 029 - In-Flight Route Evidence Matches Runtime Behavior
Problem: Loop 15 changed the failure semantics, but the route card and binary payload ledger still only described non-finite fallback vectors. That left room for reviewers to read emergency vectors as an accepted normal in-flight solve fallback.
Solution: Updated `SHINOBU_345_CELESTIAL_ORBIT_ROUTE_CARD.md` and `BINARY_PAYLOAD_INTEGRATION_LEDGER.md` to state the exact order: pending or temporarily unavailable solve -> last finite read-side Vault snapshot -> hardcoded emergency vectors only when no valid snapshot exists.
Rejected Alternatives: Relying on code alone; this project treats route cards and ledger entries as authority proof, and stale proof can cause integrators to preserve the wrong behavior.
Scalability potential: Low devices benefit most because lower quality lengthens solve intervals and can reuse cached truth longer. Middle/High/Ultra keep higher cadence and richer shader consumers without altering DTO layout, BufferIDs, save identity, or authority route.
Hardware Impact: Documentation-only runtime impact is 0 us. The recorded behavior protects low-end hardware from avoidable shader/presentation churn and prevents future patches from reintroducing default-vector latency snaps.
