# Rationale_REND_DYNAMIC_RESOLUTION_ADAPTER

## Decision 001 - DRS ownership boundary
Problem: Thermal and platform code currently poke the concrete DynamicResolutionScaler directly, making GPU resolution policy a cross-domain side effect.
Solution: Introduce a contract-facing dynamic-resolution runtime and a dedicated graphics adapter that consumes health/time signals and owns scale policy.
Rejected Alternatives: Leaving HardwareThermalService as the direct caller was faster to type but keeps graphics policy inside hardware thermal code and blocks signal-based scaling.
Scalability potential: Low uses 0.5-0.7 scale with foveation; Middle recovers slowly toward native; High/Ultra spend recovered frame time on cleaner STP/FSR presentation.
Hardware Impact: Expected low-end i3/MX350 gain is 1500-6000 microseconds when GPU-bound at 1.0 render scale and forced down to 0.7 or lower.

## Decision 002 - Signal source
Problem: The prompt requires SystemHealthSignal and FrameTimeSignal, but the project has SystemHealthSignal and no FrameTimeSignal type.
Solution: Add a compact FrameTimeSignal emitted by HomeostasisBrain during pre-simulation, then cache the latest signal in the adapter.
Rejected Alternatives: Polling Time.deltaTime inside a MonoBehaviour Update violates the prompt and adds another gameplay update path; polling GlobalRegistry every tick violates the hot-path mandate.
Scalability potential: Low/Middle devices receive immediate EWMA pressure without per-system polling; High/Ultra remain uncapped unless actual frame pressure appears.
Hardware Impact: NativeQueue publish/consume is below 10 microseconds target cost; render-scale drops buy milliseconds on thermal devices.

## Decision 003 - Resolution recovery behavior
Problem: Instant recovery from low scale causes presentation jitter and oscillation after transient GPU spikes.
Solution: Snap down on overload or thermal pressure, recover upward by a fixed small per-tick step.
Rejected Alternatives: Smooth damp/animation curves are visually nicer but add state and math not needed for a 0.1ms-suspicious hot path.
Scalability potential: Toaster hardware stays locked low under heat; high-end hardware climbs back to 1.0 without visible shimmer.
Hardware Impact: Prevents repeated render-target realloc/resize churn; expected stability gain is frame pacing, not raw microseconds.
