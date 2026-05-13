# Rationale - ORBITAL_MECHANICS_DIRECTOR

Status: PENDING VERIFICATION

## Decision 1 - Registry and Contract Boundary

Problem: The task requires `IOrbitalDirector` and `GlobalRegistry.CurrentDomain`, but the existing contract assembly is intentionally small and the live `GlobalRegistry`/`GlobalSignals` implementation is in `Hecton8.Core`.

Solution: Add a public `IOrbitalDirector` contract and a registry slot inside the existing Core registry surface, then put the prologue runtime in its own asmdef that references Core and contracts needed for AUP payloads. This obeys the project service-locator pattern and avoids any SpaceManager singleton.

Rejected Alternatives: A local singleton would violate the prompt. A pure `Hecton8.Core.Contracts` dependency is impossible without moving `GlobalRegistry`/`GlobalSignals`, which would be a wide architectural rewrite and higher compile risk.

Scalability potential: Low = 2D impostor and low precision shader branch; Middle = 3D planet under proximity; High = full shader fake and turbulence; Ultra = overkill plasma, camera, haptics, audio modulation.

Hardware Impact: On i3/MX350 this removes N-body/orbit simulation and keeps work below noise floor by using one double3 integration and shader fakes; expected hot cost under 20 us excluding renderer cost.

## Decision 2 - Signal Handoff Instead of World Runtime Calls

Problem: The batch asks to load ocean chunks at handoff, but the assigned domain is space prologue and direct world runtime dependencies create cross-domain coupling.

Solution: Emit unmanaged `AtmosphericReentrySignal` during approach and `PrologueCompleteSignal` at whiteout. World residency should consume the signal in its own domain owner.

Rejected Alternatives: Calling `WorldChunkResidencyManager` directly would violate the domain boundary and asmdef isolation.

Scalability potential: Low hardware receives the same one signal path; high hardware can attach richer consumers without changing orbital code.

Hardware Impact: NativeQueue/SignalBus handoff cost is bounded and allocation-free after queue initialization; expected cost under 5 us per emitted event.

## Decision 3 - Relativity Fake Kinematics

Problem: A physical 10,000km orbital descent in float3 would collapse precision and push Physics/Rigidbody work into the hot path.

Solution: Keep the capsule locked at runtime/AUP origin and integrate a single unmanaged `double3 UniverseVelocity`; the planet presentation moves from `-distance` toward zero. Player thrust applies negative Y acceleration to the universe, not force to the capsule.

Rejected Alternatives: Rigidbody capsule descent, gravity wells, N-body orbit, or large world-coordinate Transform travel. All are slower, less deterministic, and precision-hostile.

Scalability potential: Low = passive approach plus 2D planet impostor; Middle = 3D mesh under 2000m; High = 3D mesh plus turbulence/audio/haptics; Ultra = stronger shader fake and higher visual intensity with same math cost.

Hardware Impact: On i3/MX350 this is one double3 length, one double integrate, and a few throttled signal writes; expected CPU cost 45 us or less excluding renderer.

## Decision 4 - Visual Fakes Before Simulation

Problem: Plasma, planet curvature, and cloud whiteout can consume frame time if built as particles, volumetric raymarch, or high-scale geometry.

Solution: Use three shader fakes: logarithmic planet horizon deformation, transparent capsule leading-edge plasma, and screen/mesh whiteout noise keyed by global orbital scalars.

Rejected Alternatives: Volumetric atmosphere, simulated plasma particles, or real planet mesh scale. Those trade predictability for cost.

Scalability potential: Low = shader impostor with minimal mesh; Middle = normal shader fake; High = turbulence and stronger plasma; Ultra = overkill shader intensity without changing CPU behavior.

Hardware Impact: Low-end CPU impact is near zero because the extra work is shader-side and LOD-swapped; saved CPU buys richer visual output on high-end devices.

## Decision 5 - Blackbox and Failure Handling

Problem: The prologue must not fail with unknown orbital state after a NaN or crash.

Solution: Keep a fixed 300-entry `NativeArray<OrbitalTelemetryEntry>` circular buffer and dump it to `Docs/AgentLogs/Dump_ORBITAL_MECHANICS_DIRECTOR.bin` on NaN or forced abort.

Rejected Alternatives: Debug.Log streams, managed Lists, or last-frame-only state. Those either allocate or lack postmortem history.

Scalability potential: Same fixed buffer on all tiers; high-end machines can attach additional consumers to the signal lanes without changing the blackbox.

Hardware Impact: About 4 us hot-path write cost on low-end silicon, no managed allocation; dump cost occurs only on failure.

## Decision 6 - Compile Verification Boundary

Problem: Full project compile is currently blocked by unrelated SaveSystem errors and stale generated csproj dependencies.

Solution: Use Unity MCP `validate_script` for touched scripts and Unity console filtering for Orbital/GlobalRegistry errors. Record full compile as PENDING VERIFICATION until the unrelated SaveSystem wall is removed.

Rejected Alternatives: Fixing SaveSystem from the orbital domain would violate domain ownership; reporting full compile success would be false.

Scalability potential: No runtime impact; this is evidence handling.

Hardware Impact: None.

## OMEGA POLISH CHANGES

Problem: The first implementation used honest `math.length()` calculations for visual-only intensity and clamp work.

Solution: Replaced orbital speed reads with squared-length plus `math.rsqrt` helper paths, replaced hot divisions with `math.rcp` multiplication, removed shader normal re-normalization where upstream data is already normalized, and reimported the three shaders.

Rejected Alternatives: Keeping exact magnitude everywhere was unnecessary. A LUT for the single speed scalar was rejected because the current cost is already one rsqrt path and the clamp still needs continuous response.

Scalability potential: Low = fewer scalar square roots and impostor LOD; Middle = 3D mesh under 2000m; High = visual turbulence/audio/haptic layering; Ultra = same CPU path with stronger shader intensity.

Hardware Impact: Estimated 3 us saved on i3/MX350 hot frame path by removing repeated `math.length()` calls and replacing shader divide/normalize waste.

Exact cinematic cheats used: moving universe with locked capsule; logarithmic planet curvature fake; 2D planet impostor on low tier; shader plasma leading-edge fake; whiteout noise seam; signal-only ocean handoff; speed-driven camera/audio/haptic intensity without physical plasma.

Final Git Diff: owned additions under `Assets/_Project/Scripts/Prologue/Space/`, `Assets/_Project/Art/Shaders/Prologue/`, `Docs/Tasks/Status_ORBITAL_MECHANICS_DIRECTOR.md`, and `Docs/AgentLogs/Rationale_ORBITAL_MECHANICS_DIRECTOR.md`. Shared-file edits are limited to `GlobalRegistryContracts.cs` (`Domain`, `OrbitalDirectorSnapshot`, `IOrbitalDirector`, service slot), `GlobalRegistry.cs` (domain state, orbital registry slot/register/unregister/resolve), and `GlobalSignals.cs` (`AtmosphericReentrySignal`, `PrologueCompleteSignal`, publish/dequeue/configure/validation). Existing unrelated dirty diffs are present in the same Core files from other active agents and are not claimed.

## SECOND-PASS AAA AUDIT

Problem: The first pass still allowed two integration-quality failures: a denied Space domain claim could leave the director partially registered, and the plasma leading-edge term depended on authored `Transform.forward`, which can be wrong for a capsule whose nose is modeled on another local axis. Cold `GetComponent` calls were also less explicit than the local zero-GC standard expects.

Solution: Make domain ownership fail closed before service/update registration, publish a telemetry anomaly for the denial, retry dispatcher registration through a tiny no-allocation guard, replace cold `GetComponent` calls with `TryGetComponent`, preserve authored capsule rotation while locking AUP position, and add a serialized local leading-edge vector normalized once during cold binding.

Rejected Alternatives: Ignoring domain claim failure was rejected because it permits two space directors to fight registry authority. Forcing `Quaternion.identity` was rejected because it destroys authored cockpit/capsule forward setup. Real aerodynamic/plasma normal computation was rejected because this is a shader fake, not fluid simulation.

Scalability potential: Low = no extra simulation, one normalized local axis and impostor LOD; Middle = stable mesh swap under 2000m; High = stronger shader/audio/camera layers; Ultra = same CPU path with higher visual intensity and no new gameplay dependency.

Hardware Impact: On i3/MX350 the hot addition is a single branch for dispatcher retry until registered and one transform-direction read for leading-edge intensity; expected cost 1-2 us and 0 managed allocation. The visual gain is reliable plasma alignment instead of occasional zero-heat re-entry.

Verification: Unity `validate_script` on `OrbitalRelativityDirector.cs` returned zero errors after the second pass. Full dotnet build still fails with 154 unrelated missing dependency errors across non-orbital domains. Unity refresh timed out twice and the MCP console endpoint then reported no active Unity session.

## THIRD-PASS AAA AUDIT

Problem: `ResetRuntimeState()` still called `ApplyPresentation()` before proving ownership of `Domain.Space`, so a failed domain claim could still move orbital presentation objects and write orbital shader globals. The late-dispatcher fallback also used a permanent `Update()` branch, which removed the analyzer warning only if ignored rather than fixed. The NaN path published/dumped, then called `ForceAbortReentry()` which published/dumped again.

Solution: Split reset from presentation by passing `applyPresentation: false` during enable, apply presentation only after Space domain ownership is claimed, replace the permanent `Update()` retry with `IGlobalRegistryHotSwapListener` callbacks for `Dispatcher` and `Input`, unregister the listener on disable, and route NaN/manual abort through one `AbortReentry()` path that publishes one anomaly, snapshots, records blackbox state, and dumps once.

Rejected Alternatives: Keeping pre-claim presentation was rejected because domain failure must be visually inert. Keeping `Update()` as a retry loop was rejected because registry hot-swap already exists and avoids a per-frame idle branch. Separate NaN and manual abort paths were rejected because they produced duplicated failure side effects.

Scalability potential: Low = no idle Update retry, no pre-claim shader churn; Middle = deterministic dispatcher rebind; High = hot-swap input/dispatcher resilience; Ultra = same visual-overkill shader stack with cleaner domain ownership.

Hardware Impact: On i3/MX350 this removes one idle branch per frame after enable and prevents unnecessary shader/presentation writes on failed Space ownership. Estimated hot saving is sub-microsecond per frame but improves correctness and analyzer cleanliness with 0 allocation.

Verification: Re-extracted the prompt before the pass. `git diff --check` is clean for owned files. Unity validate timed out, then the Unity plugin session disconnected while awaiting result. `Hecton8.Prologue.Space.csproj` does not exist yet, so isolated dotnet build is unavailable. Full `dotnet build Hecton8.Core.csproj --no-restore` still fails with 153 unrelated missing dependency errors outside orbital ownership.

## FOURTH-PASS AAA AUDIT

Problem: Cold reference binding still had side effects that could run before authority was established. A denied or externally managed non-Space domain could allocate telemetry, scale the planet, freeze the capsule Rigidbody, and write presentation state despite the director not owning `Domain.Space`.

Solution: Split `CacheColdReferences()` from `ApplyColdSceneConfiguration()`, move telemetry allocation behind the domain gate, require `GlobalRegistry.CurrentDomain == Domain.Space` when external domain ownership is used, and gate `OnDisable()` capsule locking to instances that previously held domain or service authority.

Rejected Alternatives: Keeping side effects in cold binding was rejected because a failed Space claim must be inert. Relying only on `Tick()` domain checks was rejected because object/Rigidbody/shader state can already be mutated before the first tick. Registering in non-Space scenes for later handoff was rejected because ownership must be explicit through the registry and signal lanes.

Scalability potential: Low = denied domain exits with no telemetry allocation, no Rigidbody mutation, no shader churn; Middle = Space-owned scene still gets deterministic locked-capsule setup; High = service/hot-swap path remains available after authority; Ultra = visual-overkill shader stack activates only when the Space prologue actually owns the scene.

Hardware Impact: On i3/MX350 this avoids cold-path allocation and setup churn in non-Space scenes and removes accidental Rigidbody state writes from failed ownership attempts. Runtime hot cost is unchanged; denied-domain cost is reduced to a domain branch and anomaly scalar.

Verification: Re-extracted the prompt before the pass. Unity `validate_script` is clean for `OrbitalRelativityDirector.cs` and `GlobalRegistryContracts.cs`. The Unity validator times out on the very large `GlobalRegistry.cs` and `GlobalSignals.cs`, so console filters were used there: no Orbital/Prologue errors, and the only GlobalRegistry text hit is an unrelated duplicate `OnGlobalRegistryServiceReplaced` in `SuitHUDV4CanvasOverlay.cs`. `git diff --check` reports no whitespace errors, only existing line-ending warnings. `Hecton8.Prologue.Space.csproj` does not exist yet. Full `dotnet build Hecton8.Core.csproj --no-restore` now fails with 93 unrelated missing dependency errors outside orbital ownership.

## FIFTH-PASS AAA AUDIT

Problem: The hot path recalculated universe speed for multiple visual consumers after integration, abort handling zeroed velocity before the blackbox could capture the failing state, orbital shader globals could survive teardown, and the shader LOD scalar was underused so low-tier hardware still paid for animated sine/pow/log fake detail.

Solution: Cache `_universeSpeedMetersPerSecond` and `_universeSpeed01` once after integration/reset and route presentation, camera, audio, and signal payloads through that cache. Change abort order so telemetry records the failing state before public state is sanitized. Clear all orbital shader globals on authority teardown and after authority-owned abort. Add `_H8OrbitalMathLod` branches to planet, plasma, and whiteout shaders: Low uses cheap constant/dot paths, High keeps detailed fake layers, Ultra boosts atmosphere/plasma/whiteout intensity.

Rejected Alternatives: Recomputing `LengthFast()` in every visual consumer was rejected because speed is a presentation scalar, not a physics proof. Zeroing velocity before telemetry was rejected because it destroys the postmortem evidence mandated by the blackbox rule. Leaving globals dirty after disable was rejected because prologue materials use global shader state. A single middle-ground shader path was rejected because the scalability pillar requires a toaster path and a visual-overkill path.

Scalability potential: Low = cached CPU speed, planet avoids fragment pow/sine continent/cloud bands, plasma avoids flicker sine, whiteout avoids dual sine noise; Middle = existing detailed fake shaders; High = detailed fake layers with normal signal stack; Ultra = boosted atmosphere/plasma/whiteout without additional CPU cost.

Hardware Impact: On i3/MX350 this removes repeated hot `rsqrt` consumers and trims low-tier fragment ALU during the most expensive whiteout/re-entry frames. Estimated CPU saving is 3-6 us in hot orbital frames, with larger GPU savings on low tier from skipping sine/pow/log-heavy fake detail.

Verification: Re-extracted the prompt before the pass. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs`. Unity console filters returned zero errors for `Orbital`, `Prologue`, `Hecton_Orbital`, and `Hecton_Capsule`. Focused `git diff --check` returned no whitespace errors for owned orbital/shader/log files. Unity refresh was requested but timed out after 60 seconds waiting for readiness. Full `dotnet build Hecton8.Core.csproj --no-restore` now fails with 92 unrelated dependency errors outside orbital ownership.

## SIXTH-PASS AAA AUDIT

Problem: A stale `GlobalRegistry.OrbitalDirector` service could make `RegisterOrbitalDirectorRuntime()` throw after this director already claimed `Domain.Space`, leaving domain ownership dirty. Also, if the Space domain changes while the director remains enabled, the hot tick returned immediately and could leave orbital shader globals alive until `OnDisable()`.

Solution: Add a cold service preflight before claiming the Space domain. If another orbital director is already registered, publish a service-claim anomaly and stay inert. Require `_serviceRegistered` before update-lane and hot-swap listener registration. Add one-shot `HandleDomainExit()`: pre-handoff domain loss triggers blackbox-first abort and global cleanup; post-handoff domain exit clears globals, records telemetry, and stops input without marking a false abort.

Rejected Alternatives: Letting the registry throw was rejected because it can leave domain ownership half-claimed after an exception. Clearing shader globals every non-Space tick was rejected because it burns work repeatedly after handoff. Treating all domain exits as aborts was rejected because Ocean handoff can legitimately change domain after `PrologueCompleteSignal`.

Scalability potential: Low = no stale shader state or duplicate director work after domain exit; Middle = deterministic handoff cleanup; High = clean service authority before update registration; Ultra = same visual-overkill path, with safer scene/domain teardown.

Hardware Impact: On i3/MX350 this is mostly cold-path safety. Hot cost is one existing domain branch plus a boolean reset in Space and a one-shot cleanup when leaving Space; estimated steady-state cost is below 1 us.

Verification: Re-extracted the prompt before the pass. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs`. Focused `git diff --check` returned no whitespace errors for owned orbital/shader/log files. Unity console filter for `OrbitalRelativityDirector` returned zero errors; wider `Orbital` console filters then stopped responding to ping and are not claimed as passed. Full `dotnet build Hecton8.Core.csproj --no-restore` now fails with 96 unrelated dependency/duplicate-member errors outside orbital ownership.

## SEVENTH-PASS AAA AUDIT

Problem: Post-handoff domain exit cleared shader globals but left the director registered on the update lane, hot-swap listener list, orbital service slot, and possibly domain ownership until `OnDisable()`. That meant an already-finished space prologue could still receive registry callbacks or a non-Space tick branch for no useful work.

Solution: Add `ReleaseRuntimeAuthority()` as the single teardown primitive and call it from `OnDisable()` and one-shot domain exit handling. Pre-handoff domain loss still blackbox-aborts first; post-handoff domain exit now clears shader globals, publishes one final snapshot/telemetry frame, then unregisters update, hot-swap, service, and domain ownership immediately.

Rejected Alternatives: Waiting for Unity disable was rejected because additive scene/domain transitions can keep the component enabled past the logical handoff. Clearing visuals only was rejected because registry authority is also state. Re-registering later from the same component was rejected because the prologue is one-shot and should be restarted by scene/bootstrap ownership, not hidden hot-loop recovery.

Scalability potential: Low = no idle non-Space branch, no registry callbacks after handoff, no service contention on cheap devices. Middle = deterministic authority release for additive scene transitions. High = downstream ocean/world consumers can claim their domains without stale orbital ownership. Ultra = visual-overkill orbital shaders remain unchanged but die immediately when authority exits.

Hardware Impact: On i3/MX350 this removes post-handoff update/listener/service overhead and prevents unnecessary branch work after Space exits. Estimated steady-state saving after handoff is less than 1 us per frame with 0 managed allocation; correctness gain is higher than CPU gain.

Verification: Re-extracted the prompt before the pass and checked adjacent `OrbitalDropReentryVfxController`/`PrologueReentrySignals` payload usage. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs` after one MCP disconnect/retry. Focused `git diff --check` returned no whitespace errors. Full `dotnet build Hecton8.Core.csproj --no-restore` first timed out, then completed with 152 unrelated dependency errors outside orbital ownership.
