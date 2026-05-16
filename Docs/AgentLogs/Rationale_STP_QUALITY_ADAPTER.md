# STP_QUALITY_ADAPTER Rationale

Status: PENDING VERIFICATION

## Session Start

Problem: Native resolution pressure is currently split across `ThermalDynamicResolutionAdapter`, `DynamicResolutionScaler`, and low-tier platform pressure code. The existing path is source-backed but not yet a single STP quality adapter.
Solution: Collapse policy into the graphics-owned adapter while preserving existing registry service boundaries so dependent systems keep reading `GlobalRegistry.DynamicResolution` or `IDynamicResolutionRuntime`.
Rejected Alternatives: Adding a second scaler would create competing writes to URP render scale and `ScalableBufferManager`.
Scalability potential: Low uses cheap internal render scale plus STP reconstruction; Middle keeps 0.8-1.0; High/Ultra keep 1.0+ presentation quality and use saved cycles for stronger anti-aliasing/sharpening.
Hardware Impact: Estimated low-end gain is GPU-bound, roughly proportional to pixel-count reduction; source-only until profiler proof exists.

## Loop 1 Decisions - Tasks 1-5

Problem: Dynamic-resolution policy had no registry-facing STP service contract.
Solution: Added `IResolutionScalerService`, `ResolutionScaleState`, and `GlobalRegistry.ResolutionScaler`.
Rejected Alternatives: `ResolutionManager.Instance` or expanding `DynamicResolutionScaler.Instance`; both keep consumers bound to concrete runtime objects.
Scalability potential: Low/MX350 can read one native state lane; High/Ultra can keep STP active at 1.0 for temporal AA intent.
Hardware Impact: Interface lookup cost is cold or cached; estimated hot-path impact stays below 2 us/frame.

Problem: `Camera.targetTexture` hits included legitimate diegetic UI render targets.
Solution: Preserved UI/offscreen target textures and removed only the world dynamic-resolution multiplier from `VisorHUDController`.
Rejected Alternatives: deleting every targetTexture assignment; that would break visor panels and cockpit feeds.
Scalability potential: Low keeps UI pixel-stable while world resolution drops; Ultra can still run high-resolution diegetic RTs.
Hardware Impact: No added frame cost; prevents STP blur on text.

Problem: System stress and hardware tier needed a persistent native handoff.
Solution: Added `BufferID.ResolutionScaleState` and a DataVault-backed single-element `ResolutionScaleState`; hardware tier is cached from `GlobalRegistry.HardwareProfile`.
Rejected Alternatives: storing policy state only in managed fields; RenderGraph or later consumers would have no native state lane.
Scalability potential: Low reads the same state as High; policy values can drive Low/Mid/High/Ultra math LODs without new managed plumbing.
Hardware Impact: One 64-byte native record; fallback array exists only before DataVault is available.

Problem: Resolution yo-yo from raw stress changes would poison STP history.
Solution: Added a Burst `IJob` EWMA that writes `SystemStressEwma01` into the native scale state with one-frame latency.
Rejected Alternatives: scheduling and completing the job immediately; that would be fake Burst and a main-thread stall.
Scalability potential: Low uses stable scale decisions; Ultra can tolerate finer policy changes later without visible pumping.
Hardware Impact: One element job has negligible compute cost; actual measured time pending Unity profiler.

Problem: AUP is not owned by a screen-space scaler but can smear temporal history during rebases.
Solution: Treat AUP as N/A for ownership and lock scale changes for three frames on `AupShiftSignal`.
Rejected Alternatives: converting render-scale state into AUP-relative coordinates; irrelevant and slower.
Scalability potential: Same lock protects STP/TAA on all tiers.
Hardware Impact: No allocation; a byte counter in telemetry/state.
