# STRUCTURAL_ACOUSTICS_LEAD Rationale

Status: PENDING VERIFICATION

## Initial Mandate Selection

Problem: Structural acoustics crosses audio DSP, habitat stress, pressure damage, AUP, haptics, and crash telemetry.

Solution: Scope ownership to the audio synthesis runtime and use contracts/signals for habitat, pressure, haptics, and portal propagation. Use fixed buffers, NativeArray/struct payloads, and Burst-compatible kernels.

Rejected Alternatives: Direct HabitatGraphManager references from DSP, scene searches for stressed rooms, AudioSource.PlayOneShot creaking, and multiple authored creak clips. Those patterns violate decoupling, zero-GC, or low-tier audio-thread budget.

Scalability potential: Low uses a pitched fallback clip/low grain density and no expensive routing. Middle uses bounded granular density. High adds richer routing and higher grain concurrency. Ultra can spend saved CPU on denser grains and stronger modulation.

Hardware Impact: On i3/MX350, disabling full granular on Low is estimated to save 40-120 us per 512-sample block versus 16-32 active grains. Main-thread update target stays below 0.01 ms with fixed queue/snapshot updates.

## Decision 0 - State Files Before Code

Problem: Batch protocol requires persistent checklist and rationale before marking progress.

Solution: Created Status_STRUCTURAL_ACOUSTICS_LEAD.md and this rationale log before code edits.

Rejected Alternatives: Chat-only status. It is non-durable under context compression and violates the task protocol.

Scalability potential: No runtime impact.

Hardware Impact: No runtime impact.
