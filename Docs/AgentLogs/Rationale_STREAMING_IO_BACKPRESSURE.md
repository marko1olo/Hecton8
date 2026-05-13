# STREAMING_IO_BACKPRESSURE Rationale

STATUS: PENDING VERIFICATION

## Session Setup

Problem: Steam Deck MicroSD / slow disk latency can allow the player to outrun chunk residency, exposing unloaded world holes.
Solution: Track Addressables latency in preallocated native timestamp storage, derive `storageDebt01`, route it through registry/signals, and clamp locomotion as a diegetic "thick current" pressure.
Rejected Alternatives: Per-load Stopwatch objects, coroutines, WaitUntil, or per-frame polling every handle; all create GC or frame jitter and hide the actual IO bottleneck.
Scalability potential: Low uses clamp plus proxies; Middle loads LOD1 early; High keeps clamp smooth with richer cover-up VFX; Ultra spends saved cycles on visual turbulence while preserving deterministic residency.
Hardware Impact: Expected low-end i3/MX350 benefit is reduced void exposure and lower IO polling cost; exact microsecond savings are PENDING VERIFICATION.

