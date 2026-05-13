# Rationale_ACOUSTIC_REFLECTION_MAPPER

Status: PENDING VERIFICATION

## Decision 1: Use Existing Player-Critical DSP Owner
Problem: Echolocation already intersects player helmet DSP, active sonar UI, cave reverb, and existing sonar echo tap buffers. A second runtime owner would duplicate hot audio paths and risk extra scheduling, thread wakeups, and concrete cross-domain coupling.
Solution: Extend `PlayerCriticalProceduralAudioRenderer` and isolate reusable Burst raymarch math in a new `Hecton8.Audio.Echolocation` assembly. Keep the DSP delay ring as the single output authority.
Rejected Alternatives: Spawning `AudioSource` echoes was rejected by prompt and would allocate/playback-manage many Unity objects. A new MonoBehaviour manager was rejected because it would compete with the existing audio producer thread and require extra registry wiring. Direct world-system dependency inside audio was rejected; the new job receives plain buffers/structs.
Scalability potential: Low uses 8 rays and existing psychoacoustic fake. Middle/High use more ray directions. Ultra can spend the saved object-spawn cost on denser virtual echo taps and richer filter material profiles.
Hardware Impact: Low-end i3/MX350 avoids hundreds of Unity audio voices; expected gain is bounded main-thread work with no new managed hot allocations. Exact microseconds remain PENDING VERIFICATION until Unity profiling.

## Decision 2: Treat Echolocation as Deterministic Acoustic Fake
Problem: Physically correct underwater acoustic reflection would require scene-wide propagation and many material/geometry interactions. That violates the 0.1ms suspicion threshold without profiler proof.
Solution: Use a capped ray fan over the existing SDF authority to generate virtual echo taps. The DSP delay line sells cave shape through delay, gain, pan, and low-pass instead of simulating full acoustics.
Rejected Alternatives: Full wave propagation, per-wall reflection bounces, and per-source `AudioReverbZone` edits were rejected as expensive and architecturally wrong for active ping feedback.
Scalability potential: Low = coarse cardinal/diagonal ray fan. Middle = 32 rays. High/Ultra = higher tap budget, flesh/material coloring, and richer low-pass profiles if budgets allow.
Hardware Impact: Converts object churn and Unity audio scheduling into tight numeric loops. Expected low-end benefit is lower CPU variance and 0 B GC hot path; measured data PENDING VERIFICATION.
