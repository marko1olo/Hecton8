# Rationale_KINETIC_IMPACT_ACOUSTICS

Status: PENDING VERIFICATION

## Initial Boundary Decision
Problem: Procedural collision audio must intercept high-speed impacts without adding a new singleton or parallel audio manager.
Solution: Use the existing GlobalRegistry/IAudioService and NativeQueue-backed procedural audio event lane if source confirms the contract.
Rejected Alternatives: Creating a standalone MonoBehaviour dispatcher or using `AudioSource.PlayOneShot` would violate the project audio contract and create hot-path managed routing.
Scalability potential: Low tier can route a cheap baked impact cue; Middle/High/Ultra can spend saved CPU on procedural tonal layers, clipping, echo taps, and stronger spatial cues.
Hardware Impact: Expected i3/MX350 gain is avoiding per-impact AudioSource allocation and mixer graph churn; target impact-event CPU remains under 0.1 ms main-thread admission.

## Mandate Selection
Problem: Audio prompt crosses DSP, spatialization, NativeQueue, telemetry, and AUP boundaries.
Solution: Selected 8 mandates: DSP SPSC, acoustic occlusion, binaural spatialization, zero-GC, frame budgets, native lifetime, crash telemetry, and AUP precision.
Rejected Alternatives: Reading the whole registry wastes context and invites cross-domain drift; fewer mandates would miss telemetry or AUP safety.
Scalability potential: Low/Middle/High/Ultra behavior must be explicit in the runtime code or documented fallback.
Hardware Impact: Mandate-driven constraints prevent unmanaged buffer leaks and audio-thread stalls on low-end silicon.

## Loop 1 Decisions
Problem: High-speed impact data exists in `HighSpeedImpactSignal`, but material/mass fields named in the prompt are not present in that 88-byte packet.
Solution: Consume the high-speed packet directly, derive mass from `LostKineticEnergy` and `ImpactSpeed`, then recompute `0.5 * mass * speedSq`; treat CCD player/vehicle/leviathan base impacts as metallic until a future material hash is added to the high-speed packet.
Rejected Alternatives: Changing `HighSpeedImpactSignal` layout would violate `ValidateSignalSize<HighSpeedImpactSignal>(88)` and break producers already publishing the packet.
Scalability potential: Low routes a volume-scaled baked clip; Middle/High/Ultra use the procedural event lane and native echo tap bridge. Toaster: one pool clip. $5000 machine: sine thud, clang, distortion, binaural echo, telemetry.
Hardware Impact: i3/MX350 keeps the hot path to a bounded 32-signal scan and skips procedural PCM in low-tier mode.

Problem: Service routing needed no new singleton but still had to expose a callable API.
Solution: Added `QueueHighSpeedImpactSignal` to `IAudioService`; `SpatialAudioManager` mirrors passive radar and forwards to `GlobalRegistry.PlayerCriticalAudio`.
Rejected Alternatives: Adding `KineticImpactAudioManager.Instance` would violate the GlobalRegistry mandate and create another audio authority.
Scalability potential: Cheap devices use the existing AudioSource pool; high-tier devices spend DSP cycles inside the existing player-critical renderer.
Hardware Impact: No extra scene search or allocation; estimated main-thread admission stays below 0.02 ms for a 32-signal cap.

Problem: Task 1-5 compile proof is blocked by environment/project dependency state.
Solution: Ran Unity MCP validation twice and `dotnet build Assembly-CSharp.csproj --no-restore`; recorded the MCP `no_unity_session` and unrelated Hecton8.Core missing namespace failures as dependency blockers, then continued.
Rejected Alternatives: Reverting unrelated code or editing other agents' asmdef dependencies would violate parallel-agent ownership.
Scalability potential: No runtime effect.
Hardware Impact: No runtime effect.

## Loop 2 Decisions
Problem: Collision audio needs to feel physical without a physical acoustic simulation.
Solution: Used a cinematic cheat: one pitch-descending sine thud plus existing metallic clang/granular bed. The thud is armed by a fixed event payload and rendered in the already-async hull block.
Rejected Alternatives: Simulating surface deformation or spawning PCM clips per impact would cost more CPU/GC and be less controllable.
Scalability potential: Low disables this path; Middle gets thud+clang; High adds native echo; Ultra can tolerate denser echo and stronger distortion. Toaster: authored clip. $5000 machine: procedural thud, clang, low-pass, and echo-only tap.
Hardware Impact: MX350 avoids procedural samples entirely on low tier; mid/high cost is bounded to active 0.2 s impact windows.

Problem: Echo routing had to use the project’s native tap lane without turning every impact into a sonar ping.
Solution: Added `SonarTriggerFlagKineticImpactEcho`; the existing `NativeQueue<SonarEchoTap>` carries a single impact tap while `RenderSonarBlock` switches to a 150->40 Hz dry source for flagged impact echoes.
Rejected Alternatives: Adding another `NativeQueue<ImpactEchoTap>` would duplicate existing binaural delay/filter state and create new ownership.
Scalability potential: Low skips tap publication; higher tiers reuse the established binaural/ITD and low-pass code.
Hardware Impact: One tap upload and one short echo state instead of corridor ray simulation; estimated admission is <10 us, DSP work only while active.

Problem: Underwater impact muffling needed to be deterministic and AUP-safe.
Solution: Compare impact runtime Y resolved from AUP against current player waterline and clamp cutoff to 800 Hz for underwater impacts.
Rejected Alternatives: Querying ocean/volume systems per impact risks cross-domain dependencies and synchronous search.
Scalability potential: Same scalar rule across all tiers; high tiers spend saved work on echo and distortion.
Hardware Impact: One float compare; no physics query or allocation.

## Loop 3 Decisions
Problem: MX350 must not pay per-sample procedural impact synthesis for non-critical spectacle.
Solution: Low/MX350/Unknown/low-memory branch exits before procedural enqueue and plays `lowTierKineticImpactClip` through the existing pooled world audio route with volume/pitch scaled by kinetic energy.
Rejected Alternatives: Running a reduced oscillator on low tier still burns DSP thread time and contradicts the prompt.
Scalability potential: Low = baked thud; Middle = synth thud; High = synth + echo tap; Ultra = stronger distortion/portal polish if later budgets allow.
Hardware Impact: On i3/MX350, avoids the 0.2 s oscillator/LPF window entirely and spends only an existing pool source setup.

Problem: Black-box telemetry needed impact energy without managed logs.
Solution: Extended the existing 300-entry granular DSP telemetry ring with `PeakImpactEnergyJoules` and added `Dump_KINETIC_IMPACT_ACOUSTICS.bin`.
Rejected Alternatives: `Debug.Log` or text CSV in DSP path would allocate and break the crash-forensics mandate.
Scalability potential: Same data on every tier; high tiers can diagnose distortion/energy clamps under heavier scenes.
Hardware Impact: One float in a fixed telemetry entry; no extra allocation.

Problem: Burst proof needed a dedicated sine oscillator compile surface.
Solution: Added `KineticImpactSineOscillatorJob` in `Hecton8.Audio.Synthesis`, synchronous Burst annotation, NativeArray output/state, 150 Hz -> 40 Hz default sweep, low-pass, and clipping.
Rejected Alternatives: Relying only on the renderer method would not prove Burst compilation.
Scalability potential: The job can replace the inline renderer on higher-tier/batched generation later without changing contracts.
Hardware Impact: Current runtime impact is zero unless scheduled; compile-time coverage protects future DSP offload.

## Loop 4 Recursive Verification
Problem: Recursive audit could expose prompt drift after multiple patches.
Solution: Re-extracted `KINETIC_IMPACT_ACOUSTICS`, checked each source feature with static scans, and confirmed the core tasks map to exact symbols/files.
Rejected Alternatives: Marking complete from memory would violate anti-amnesia rules and miss possible neighboring-agent contamination.
Scalability potential: Audit confirmed Low/Middle/High/Ultra behavior exists in code/rationale.
Hardware Impact: No runtime effect.

Problem: Infinite or extreme energy can translate into speaker-hostile gain and telemetry garbage.
Solution: Finite-check `LostKineticEnergy`/`ImpactSpeed`, derive finite mass, recompute kinetic energy, and clamp to `KineticImpactMaximumSafeEnergyJoules` before amplitude, distortion, echo, or telemetry.
Rejected Alternatives: Raw producer energy was cheaper but unsafe under NaN/infinite or bad mass-speed inputs.
Scalability potential: Same safety clamp on all tiers; high-end distortion intensity comes from a bounded range.
Hardware Impact: Negligible ALU; prevents audio limiter abuse and black-box corruption.

## OMEGA POLISH CHANGES
Problem: The Burst kinetic oscillator compile surface used exact exponential one-pole decay while the runtime renderer already used the cheaper cinematic approximation.
Solution: Added `DepthStressGranularMath.ApproximateExpNegPositive` and replaced `math.exp(-x)` with a reciprocal polynomial approximation for the Burst oscillator low-pass coefficient.
Rejected Alternatives: Keeping exact exponential decay is more physically honest than needed for a 0.2 s collision thud; adding a LUT would require static data ownership and asset/version policy for no audible gain.
Scalability potential: Low tier still exits to baked clip; Middle/High/Ultra keep the same thud shape with cheaper filter setup. Low = no oscillator. Middle = thud + clang. High = thud + binaural echo tap. Ultra = bounded stronger distortion/echo without changing the contract.
Hardware Impact: Removes one transcendental from oscillator setup; expected i3/MX350 impact is micro-level but deterministic, with no GC and no extra memory.

Problem: Final audit needed proof that no owned hot path reintroduced forbidden managed work.
Solution: Ran fixed-string scans over owned files: no `PlayClipAtPoint`, no managed `foreach`, no `math.exp` in synthesis after the patch, no unconditional `math.normalize`; `.ToString()` hits are editor/cold bootstrap reporting only, and `math.sqrt` hit is pre-existing non-kinetic acoustic volume shaping in `SpatialAudioManager`.
Rejected Alternatives: Editing unrelated pre-existing bootstrap/editor diagnostics would violate domain ownership and risk other agents' changes.
Scalability potential: No runtime tier change; preserves the zero-GC kinetic path.
Hardware Impact: 0 B/frame in the kinetic path; no additional CPU beyond bounded scalar admission.

Problem: Final compile proof is still blocked by project dependency state.
Solution: Re-ran Unity MCP validation and `dotnet build Hecton8.Core.csproj --no-restore -v:minimal -p:UseSharedCompilation=false -m:1`; recorded `no_unity_session` and the 132 unrelated missing namespace/type errors.
Rejected Alternatives: Patching other domains or reverting parallel-agent edits would be architectural sabotage under the batch protocol.
Scalability potential: No runtime effect.
Hardware Impact: No runtime effect.

Final Git Diff:
- `Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs`: adds `IAudioService.QueueHighSpeedImpactSignal(in HighSpeedImpactSignal signal)`.
- `Assets/_Project/Scripts/SpatialAudioManager.cs`: routes high-speed impact packets through passive radar plus `GlobalRegistry.PlayerCriticalAudio` instead of a singleton.
- `Assets/_Project/Scripts/Audio/Synthesis/DepthStressGranularSynthesisKernel.cs`: adds the reciprocal decay approximation and uses it in the Burst kinetic oscillator.
- `Docs/Tasks/Status_KINETIC_IMPACT_ACOUSTICS.md`: records all loops and blocked compile checkpoints.
- `Docs/AgentLogs/Rationale_KINETIC_IMPACT_ACOUSTICS.md`: records decisions and Omega polish.

Diff Stat At Omega:
`5 files changed, 138 insertions(+), 35 deletions(-)` before the final LOG file append.
