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
