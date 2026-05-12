# AUDIO_VWS_SYSTEM Rationale

Status: PENDING VERIFICATION

## Initial Decision Set

Problem: Existing warning audio may overlap and may rely on direct Unity audio calls.
Solution: Audit audio contracts and runtime before implementation; use `GlobalRegistry` + fixed native queue if ownership gap exists.
Rejected Alternatives: Direct `AudioSource.PlayClipAtPoint`, singleton access, string event names, managed list queues.
Scalability potential: Low uses priority clips without radio degradation; Middle keeps queue and ducking; High adds radio degradation; Ultra can spend saved CPU on richer speaker failure filtering once measured.
Hardware Impact: Expected low-end i3/MX350 gain is reduced GC and fewer overlapping clips; exact microseconds pending measurement.

Problem: VWS queue must remain deterministic under simultaneous warnings.
Solution: Fixed 16-slot queue with byte warning IDs and Burst-compatible priority sort.
Rejected Alternatives: `List<AudioClip>`, `Dictionary<string, AudioClip>`, coroutine-based cooldowns.
Scalability potential: Low uses flat priority and cooldowns; Middle/High/Ultra can enable richer DSP degradation while preserving the same queue state.
Hardware Impact: Fixed native memory avoids managed allocations and bounds queue scan cost; exact microseconds pending profiling.
