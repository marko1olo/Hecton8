# LOG - ORBITAL_MECHANICS_DIRECTOR

## 2026-05-13 Relativity Fake Prologue

What was wrong: The orbital prologue had no isolated orbital director contract, no domain-gated space runtime, no unmanaged re-entry signal lane, and no proof path for the mandated capsule-at-origin fake. A physical 10,000km descent would destroy float precision and make the capsule/physics stack authoritative over something that should be presentation-only.

What was done: Added `IOrbitalDirector`, `Domain.Space`, `OrbitalDirectorSnapshot`, and a `GlobalRegistry` orbital runtime slot. Added `AtmosphericReentrySignal` and `PrologueCompleteSignal` to `GlobalSignals`. Added `Hecton8.Prologue.Space` asmdef and `OrbitalRelativityDirector`, which locks the capsule at origin, integrates unmanaged `double3 UniverseVelocity`, moves the planet/cloud/star presentation toward the pod, emits re-entry/handoff/audio/haptic/camera signals, and writes a 300-frame NativeArray blackbox dump to `Docs/AgentLogs/Dump_ORBITAL_MECHANICS_DIRECTOR.bin` on failure. Added planet, capsule plasma, and cloud whiteout shaders.

Cinematic Cheats used: locked-capsule relativity fake; logarithmic planet shader bulge; low-tier 2D planet impostor; transparent leading-edge plasma; whiteout noise seam; signal-only handoff; throttled camera/audio/haptic turbulence.

Exact Microseconds saved: estimated 3000+ us saved versus real orbital/physics descent; estimated 3 us saved in Omega polish by replacing repeated `math.length()` with squared-length/`rsqrt` paths. Final hot-path estimate: 38 us for authority/domain/input/capsule lock, 45 us for kinematics/presentation/camera, 25 us for whiteout/handoff/audio, 12 us for haptics/telemetry, excluding renderer cost.

Verification: Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs`, `GlobalRegistryContracts.cs`, `GlobalRegistry.cs`, and `GlobalSignals.cs`. Shader reimports completed and Unity console filters show zero `Orbital`, `Prologue`, or `Hecton_` errors. Full Unity refresh recovered after timeout. `dotnet build Hecton8.Core.csproj` remains red from unrelated generated assembly/reference and SaveSystem errors; status remains PENDING VERIFICATION as required.

## 2026-05-13 Second-Pass AAA Audit

What was wrong: The prologue could silently continue when `GlobalRegistry.TryClaimCurrentDomain(Domain.Space, this)` failed, the capsule lock forced identity rotation and could break authored capsule orientation, and plasma leading-edge heat was tied to `Transform.forward` even when the authored capsule nose is on a different local axis. Cold reference binding also used `GetComponent` instead of the cheaper explicit `TryGetComponent` path.

What was done: Added fail-closed Space-domain ownership handling with telemetry anomaly, preserved authored capsule rotation while still locking AUP position and Rigidbody motion at origin, added serialized `capsuleLeadingEdgeLocalDirection` normalized once during cold binding, replaced cold `GetComponent` calls with `TryGetComponent`, and added a no-allocation dispatcher registration retry guard for late bootstrap ordering.

Cinematic Cheats used: same locked-capsule relativity fake, but plasma alignment is now a controllable local-axis fake instead of relying on model forward. This keeps the scene art-directable without adding atmosphere/fluid simulation.

Exact Microseconds saved: estimated 0-1 us saved cold by using `TryGetComponent`; hot-path cost increases by an estimated 1-2 us for a branch and transform-direction read, buying reliable leading-edge plasma and stronger integration safety. Net cost remains below the 0.1 ms suspicion threshold.

Verification: Unity `validate_script` on `OrbitalRelativityDirector.cs` returned zero errors after the audit. It reported two heuristic warnings: Rigidbody operations outside FixedUpdate, which is intentional for a kinematic authority lock, and a false-positive Update string-allocation warning even though `Update()` only calls the registration guard. Unity refresh timed out twice after requesting script compile, then MCP console access reported no active Unity session. `dotnet build Hecton8.Core.csproj --no-restore` still fails with 154 unrelated missing-assembly/dependency errors across SaveSystem, fluids, CCD, audio propagation, terrain, ecology, radar/resource spawner, and other non-orbital domains.

## 2026-05-13 Third-Pass AAA Audit

What was wrong: Reset still applied orbital presentation before Space domain ownership was proven, so a denied domain claim could still move local orbital art and write global orbital shader scalars. Late dispatcher registration used a permanent `Update()` retry branch. NaN abort published and dumped telemetry before calling the manual abort path, duplicating failure side effects.

What was done: Moved presentation application behind the successful Space-domain claim, removed the MonoBehaviour `Update()` retry, registered `OrbitalRelativityDirector` as an `IGlobalRegistryHotSwapListener` for dispatcher/input rebinding, added listener unregister on disable, replaced the literal control drain cap with `ControlDrainLimit`, and unified manual/NaN abort handling through one `AbortReentry()` method.

Cinematic Cheats used: Same relativity fake. The improvement is architectural: the fake now stays visually inert until domain authority is valid, and bootstrap resilience uses the registry event path instead of hidden per-frame polling.

Exact Microseconds saved: estimated sub-1 us per frame by removing the idle `Update()` retry branch after enable; 0 managed allocation. Bigger gain is correctness: no pre-claim shader/global presentation churn and no duplicated dump path on NaN.

Verification: Prompt re-extracted before the pass. `git diff --check` is clean for owned files. Unity `validate_script` timed out once, then disconnected while awaiting the retry result. There is no generated `Hecton8.Prologue.Space.csproj`, so isolated dotnet proof is unavailable. `dotnet build Hecton8.Core.csproj --no-restore` still fails with 153 unrelated missing-assembly/dependency errors outside orbital scope.

## 2026-05-13 Fourth-Pass AAA Audit

What was wrong: Cold binding still mixed reference discovery with scene authority side effects. A denied Space claim or a non-Space external domain could still allocate telemetry, freeze the capsule Rigidbody, scale the planet sphere, or leave disable-time capsule locking reachable without previous authority.

What was done: Re-extracted the orbital prompt, split cold reference caching from authority-bound scene configuration, delayed `EnsureTelemetry()` until after valid Space domain proof, made non-claim mode require `GlobalRegistry.CurrentDomain == Domain.Space`, removed the denied-claim telemetry record remnant, and gated disable-time capsule locking to instances that actually claimed domain or registered the service.

Cinematic Cheats used: unchanged locked-capsule relativity fake, shader planet scale lie, plasma leading-edge fake, whiteout seam, and signal-only handoff. The polish here makes those cheats authority-scoped, so they never bleed into non-Space scenes.

Exact Microseconds saved: hot-path cost unchanged. Denied-domain path now avoids NativeArray allocation, planet scale writes, Rigidbody configuration, shader/global presentation writes, and a stale telemetry record call. Estimated low-end impact is cold-path only but removes avoidable setup churn and cross-scene state damage.

Verification: Unity `validate_script` is clean for `OrbitalRelativityDirector.cs` and `GlobalRegistryContracts.cs`. The large-file validator times out on `GlobalRegistry.cs` and `GlobalSignals.cs`; Unity console filters show no Orbital/Prologue errors, and the only GlobalRegistry text hit is an unrelated duplicate `OnGlobalRegistryServiceReplaced` in `SuitHUDV4CanvasOverlay.cs`. `git diff --check` reports no whitespace errors, only existing line-ending warnings across the dirty worktree. `Hecton8.Prologue.Space.csproj` is not generated. Full `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by 93 unrelated dependency errors outside orbital ownership.

## 2026-05-13 Fifth-Pass AAA Audit

What was wrong: Speed was still recalculated by multiple visual/signal consumers after integration, abort sanitized velocity before the blackbox wrote the failing frame, shader globals were not explicitly cleared on authority teardown, and the prologue shaders accepted a math LOD scalar without using it to remove low-tier fragment ALU.

What was done: Cached universe speed once per integration/reset and reused it for shader globals, camera turbulence, plasma audio, and re-entry signal payloads. Reordered abort so blackbox telemetry records the failing state before sanitizing public snapshot data. Added authority-owned shader global clearing on disable/abort. Added low-tier and ultra-tier shader branches to the planet, capsule plasma, and cloud whiteout fakes.

Cinematic Cheats used: locked-capsule relativity fake; cached speed scalar as the single visual truth; low-tier shader impostor paths with constant noise/dot rim; ultra-tier atmosphere/plasma/whiteout boost; blackbox-first abort capture; global-shader cleanup on teardown.

Exact Microseconds saved: estimated 3-6 us CPU saved in hot orbital frames by removing repeated visual `rsqrt` consumers. Low-tier GPU path skips fragment pow/sine continent/cloud/plasma/whiteout detail during the heaviest re-entry frames; exact GPU microseconds require RenderDoc/Unity Profiler capture.

Verification: Prompt re-extracted before the pass. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs`. Unity console filters returned zero errors for `Orbital`, `Prologue`, `Hecton_Orbital`, and `Hecton_Capsule`. Focused `git diff --check` returned no whitespace errors. Unity refresh request timed out after 60 seconds waiting for readiness. Full `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by 92 unrelated dependency errors outside orbital ownership.

## 2026-05-13 Sixth-Pass AAA Audit

What was wrong: A stale orbital registry service could cause service registration to throw after the director already claimed `Domain.Space`. Domain changes while the director stayed enabled could also leave the last orbital shader globals alive until disable.

What was done: Added a cold `GlobalRegistry.OrbitalDirector` preflight before Space-domain claim, added service authority guards to update/hot-swap registration, and added one-shot domain-exit handling. Pre-handoff domain loss now records blackbox telemetry and aborts; post-handoff domain exit clears shader globals, records telemetry, and stops input without false failure.

Cinematic Cheats used: unchanged relativity fake. The new cheat control is teardown discipline: visual-only shader state is killed as soon as Space authority exits, while legitimate whiteout handoff remains non-abort.

Exact Microseconds saved: no meaningful steady-state CPU win; this is correctness and state-containment work. Estimated hot cost stays below 1 us. It prevents duplicate director work and stale global shader state after domain transitions.

Verification: Prompt re-extracted before the pass. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs`. Focused `git diff --check` returned no whitespace errors. Unity console filter for `OrbitalRelativityDirector` returned zero errors; wider console filter retries stopped responding to ping. Full `dotnet build Hecton8.Core.csproj --no-restore` remains blocked by 96 unrelated dependency/duplicate-member errors outside orbital ownership.

## 2026-05-13 Seventh-Pass AAA Audit

What was wrong: Domain-exit cleanup was visually correct but still too passive. After a legitimate whiteout handoff, the director could remain registered as an updater, hot-swap listener, service owner, and domain owner until Unity disabled the object.

What was done: Added a shared `ReleaseRuntimeAuthority()` teardown path and wired it through `OnDisable()` and domain-exit handling. Pre-handoff domain loss still records blackbox telemetry and aborts; post-handoff domain exit clears orbital globals, writes final telemetry, and immediately unregisters update lane, hot-swap listener, service slot, and domain ownership. Adjacent VFX signal consumers were checked against the live signal payload.

Cinematic Cheats used: unchanged locked-capsule relativity fake, planet shader scale lie, leading-edge plasma fake, cloud whiteout seam, and signal-only ocean handoff. The pass tightens authority lifetime so visual cheats cannot keep owning runtime lanes after their scene job is done.

Exact Microseconds saved: estimated less than 1 us per frame after handoff by removing idle non-Space tick/listener/service work. No managed allocation added. Main gain is deterministic domain release and no stale orbital service contention.

Verification: Prompt re-extracted before the pass. Unity `validate_script` returned zero diagnostics for `OrbitalRelativityDirector.cs` after one MCP disconnect/retry. Focused `git diff --check` returned no whitespace errors. Full `dotnet build Hecton8.Core.csproj --no-restore` first timed out at 120 seconds, then completed with 152 unrelated dependency errors outside orbital ownership.
