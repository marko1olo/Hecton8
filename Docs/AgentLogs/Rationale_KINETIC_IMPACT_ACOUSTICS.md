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
