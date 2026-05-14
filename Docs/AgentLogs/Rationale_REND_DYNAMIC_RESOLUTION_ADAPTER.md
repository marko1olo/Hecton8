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

## Decision 004 - Unity 6 DRS API
Problem: URP 17.4 exposes both asset renderScale and DynamicResolutionHandler system scalers; using only renderScale leaves hardware DRS and ScalableBufferManager disconnected.
Solution: Install a system scaler delegate with DynamicResolutionHandler.SetSystemDynamicResScaler(...ReturnsPercentage), switch to DynamicResScalerSlot.System, and mirror the same scale into UniversalRenderPipelineAsset.renderScale/ScalableBufferManager for URP cameras that do not request hardware DRS.
Rejected Alternatives: Directly resizing RTHandles or camera descriptors was rejected because URP owns those resources and manual mutation is brittle under RenderGraph.
Scalability potential: Low runs 50-70 percent render target with STP/FSR; Middle floats 70-95 percent; High/Ultra remains 100 percent unless health signals prove overload.
Hardware Impact: Quest 3/Steam Deck/MX350 expected GPU savings are 2500-8000 microseconds when fill-rate bound; CPU cost is one signal scan and one scalar write, target below 20 microseconds.

## Decision 005 - Verification wall
Problem: Unity MCP reports no active session, the project is open in another Unity instance, and batchmode aborts because the project lock is held.
Solution: Performed static re-read and a narrow MSBuild attempt; recorded that MSBuild currently fails on pre-existing generated-project reference gaps before reaching this adapter as a reliable Unity compile.
Rejected Alternatives: Killing the user's active Unity process or editing generated csproj files was rejected as destructive/irrelevant to the DRS implementation.
Scalability potential: No runtime behavior impact; this is verification infrastructure only.
Hardware Impact: 0 microseconds. Verification remains PENDING until Unity Editor console is reachable.

## OMEGA POLISH CHANGES
Problem: Adapter and scaler both wrote URP renderScale/ScalableBufferManager on the same scale-change path when the registry runtime existed.
Solution: Adapter now delegates renderScale writes to IDynamicResolutionRuntime when present and only uses direct URP writes as a fallback. Existing scaler fallback divisions were converted to reciprocal/multiply or fixed 0.01 factors.
Rejected Alternatives: Leaving duplicate writes was functional but spent redundant property calls and made ownership ambiguous.
Scalability potential: Low devices avoid duplicate render-scale commits; High/Ultra stay deterministic because one writer owns URP state.
Hardware Impact: Saves an estimated 2-15 microseconds on scale-change frames; 0 B/frame.
Cinematic Cheats used: EWMA frame-time scalar instead of per-camera timing, hard 0.7 thermal cap instead of thermal simulation, STP/FSR upscaling instead of native resolution.

## Decision 006 - Recheck hardening pass
Problem: Static re-read found three non-fatal runtime hygiene gaps: disabling the adapter could leave the Unity system DRS slot installed, recovery-scale telemetry could publish every 0.01 upscale step, and disabled save-load restored current scale without restoring the target scale.
Solution: Added idempotent system-scaler release, fallback render-scale restore for no-runtime cases, 30-frame recovery telemetry throttling with immediate drop telemetry, notification flags before runtime snapshot commit, and target-scale restoration on disabled save-load.
Rejected Alternatives: Leaving the system scaler installed after disable was rejected because it creates hidden render-scale policy outside the active adapter. Publishing every recovery step was rejected because the 300-frame NativeArray blackbox already records exact per-frame state.
Scalability potential: Low devices keep immediate downscale telemetry while avoiding warning-lane spam during recovery; Middle/High/Ultra retain fast quality recovery without extra allocations or duplicate render-scale writers.
Hardware Impact: Expected gain is 0 B/frame and 1-8 microseconds saved during recovery frames by suppressing non-critical telemetry publishes; disable/re-enable path now restores native/default DRS state deterministically.

## Decision 007 - Fault containment and runtime handoff
Problem: A corrupted render-scale field could be forwarded to the runtime before the blackbox dump saw it, and losing the registry runtime at play time could leave the direct URP fallback stale.
Solution: Added an invalid-state guard before target calculation, record-then-dump telemetry ordering, immediate native-scale commit after invalid recovery, direct URP fallback on null runtime rebind, and default-state restoration when DynamicResolutionScaler clears system override.
Rejected Alternatives: Letting WriteTelemetry repair after CommitRuntimeSnapshot was rejected because the runtime could receive a NaN first. Waiting for the next scale change after a runtime hot-unbind was rejected because cameras without hardware DRS could remain at stale renderScale.
Scalability potential: Low devices get deterministic recovery instead of a black-screen/stuck-low-resolution failure; Middle/High/Ultra keep the same hot path and only pay for these checks as scalar comparisons.
Hardware Impact: Normal path remains 0 B/frame with two finite checks and one saturate, estimated below 1 microsecond. Fault path trades cold file I/O for an immediate postmortem dump and native-scale visual recovery.

## Decision 008 - Same-frame pressure merge
Problem: FrameTimeSignal and SystemHealthSignal can arrive in the same frame; assigning pressure level in consume order allowed a lower later signal to clear a higher escalation before the DRS decision.
Solution: Merge same-frame pressure with a max-byte comparison and only replace cached pressure when at least one pressure-bearing signal arrives.
Rejected Alternatives: Trusting signal order was rejected because producer ordering is an integration detail and should not control emergency render-scale policy. Persisting the previous pressure in the max was rejected because it would block recovery when signals return to zero.
Scalability potential: Low devices keep emergency drops when either frame-time or system-health pressure spikes; High/Ultra recover normally once both signal lanes de-escalate.
Hardware Impact: Adds two byte comparisons per signal, below 1 microsecond, and prevents missed 2500-8000 GPU microsecond savings during same-frame escalation.

## Decision 009 - Ownership, reload, and telemetry hygiene
Problem: Duplicate adapter instances, dispatcher-not-ready enable order, hot-swap ref+compat callback duplication, domain-reload-disabled play mode, and startup default-scale telemetry could all create noisy or hidden DRS side effects.
Solution: Added active-owner guards, a Start registration retry, same-runtime rebind no-op, SubsystemRegistration DRS slot restore, and last-observed scale seeding/native-scale warning suppression.
Rejected Alternatives: Relying on Destroy timing for duplicates was rejected because OnEnable can still be risky during Unity lifecycle edge cases. Publishing a performance warning at default scale was rejected because it is false telemetry.
Scalability potential: Low devices keep deterministic DRS ownership even across scene reloads; Middle/High/Ultra avoid warning-lane noise when no resolution drop occurred.
Hardware Impact: Normal path adds one ReferenceEquals guard and one integer scale comparison, below 1 microsecond. Prevents duplicate registration/DRS-slot churn and removes false startup telemetry.

## Decision 010 - Conservative signal merge
Problem: Multiple producers can publish FrameTimeSignal and SystemHealthSignal in one frame; last-writer consumption could overwrite a worse EWMA frame time, health index, pressure level, or foveation tier before DRS target calculation.
Solution: Merge only the current-frame snapshots, taking maximum EWMA frame time, minimum health index, maximum pressure, and maximum foveation tier, then replace cached values once per signal family.
Rejected Alternatives: Trusting signal queue order was rejected because producer order is not a graphics policy contract. Persisting the previous frame in the max was rejected because it would make recovery sticky after pressure clears.
Scalability potential: Low devices keep immediate scale drops when any current-frame lane reports stress; Middle recovers when current signals cool; High/Ultra avoid unnecessary downscale unless a same-frame signal actually proves pressure.
Hardware Impact: Adds scalar comparisons only, estimated below 1 CPU microsecond per frame, and prevents missed 2500-8000 GPU microsecond savings during same-frame escalation.
