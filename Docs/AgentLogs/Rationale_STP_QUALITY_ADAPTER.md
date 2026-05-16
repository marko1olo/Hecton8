# STP_QUALITY_ADAPTER Rationale

Status: CORE COMPLETE - FINAL VALIDATION BLOCKED BY DEPENDENCY

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

## Compile Gate 1

Problem: `dotnet build Hecton8.Core.csproj --no-restore` failed before reporting STP adapter errors.
Solution: Logged compiler output to `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt1.txt` and classified the wall as unrelated to the current graphics scalability edits.
Rejected Alternatives: Editing AI sensory, tether, or visor fluid blackbox code from this domain; that would exceed the assigned boundary.
Scalability potential: None until the shared compile wall is repaired by its owning agents/integrator.
Hardware Impact: No runtime impact; build validation blocked.

## Loop 2 Decisions - Tasks 6-10

Problem: RenderGraph and future rendering consumers need state without managed policy reads.
Solution: Kept render scale, target scale, stress, EWMA, tier, STP flag, sharpen, and AUP lock in a persistent native `ResolutionScaleState`.
Rejected Alternatives: managed properties only; cheaper to write but unusable by native/render consumers.
Scalability potential: Low/Mid/High/Ultra can branch from the same 64-byte lane.
Hardware Impact: One native element; estimated less than 1 us/frame.

Problem: Resolution changes need to inform texture/runtime systems without event noise.
Solution: Added render-scale reasons/flags to `ResolutionChangedSignal` and emit only when scale delta exceeds 5 percent.
Rejected Alternatives: publishing every tick; signal lane noise and useless churn.
Scalability potential: Low can shed aggressively, High/Ultra can stay at 1.0 without spam.
Hardware Impact: Zero signal allocation; estimated 2 us/frame only on threshold crossings.

Problem: MX350/Quest-class hardware needs visible stability, not physical correctness.
Solution: Low/MX350/Unknown base scale is 0.5 and stress emergency scale is 0.35.
Rejected Alternatives: native 1080p/4K or simulation-heavy reconstruction; too expensive for target low silicon.
Scalability potential: Low uses cheap pixels plus STP; Mid uses 0.82 base; High/Ultra use 1.0.
Hardware Impact: 0.5 scale is 25 percent pixel area; 0.35 is roughly 12 percent pixel area before STP.

Problem: High-end must avoid a bland middle-ground policy.
Solution: High/Ultra base remains 1.0 with STP intent active for anti-aliasing rather than pixel saving.
Rejected Alternatives: clamping all tiers to 0.8; wastes top-tier headroom and softens presentation.
Scalability potential: Cheap devices fake resolution; expensive devices buy temporal quality.
Hardware Impact: No low-end cost; high-end spends full-res pixels intentionally.

Problem: Low internal resolution softens the image.
Solution: Drive global `_SharpenIntensity` from the active render-scale deficit instead of adding another pass.
Rejected Alternatives: extra full-screen post compensation; bandwidth-heavy and outside the 0.1 ms suspicion threshold.
Scalability potential: Low increases sharpening at 0.35-0.5; High/Ultra remain clean at 1.0.
Hardware Impact: One global shader scalar update only when value changes.

## Compile Gate 2

Problem: Second `dotnet build Hecton8.Core.csproj --no-restore` failed on duplicate `TetherFiredSignal` definitions and duplicate `StructLayout`.
Solution: Logged compiler output to `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt2.txt`; no STP adapter errors were visible before this wall.
Rejected Alternatives: Removing physics tether contracts from a graphics scalability task; that would violate domain ownership.
Scalability potential: None until the tether duplicate is resolved.
Hardware Impact: No runtime impact; validation remains blocked.

## Loop 3 Decisions - Tasks 11-15

Problem: HUD and diegetic RT scale was coupled to world dynamic resolution.
Solution: Removed the 3D dynamic-resolution multiplier from `VisorHUDController.ResolveEffectiveRuntimeRenderScale`.
Rejected Alternatives: deleting targetTexture UI paths; they are legitimate offscreen UI surfaces.
Scalability potential: Low can drop 3D pixels while HUD stays crisp; Ultra can keep richer UI RTs.
Hardware Impact: No added work; avoids STP text blur.

Problem: Dynamic resolution must never write NaN or out-of-contract scale values into Unity render state.
Solution: Clamp all render-scale writes to 0.25f..1.5f and recover non-finite state to 1.0 while dumping the blackbox.
Rejected Alternatives: trusting upstream stress/health values.
Scalability potential: All tiers fail closed to a stable visual state.
Hardware Impact: One finite/clamp guard per tick.

Problem: A crash without prior scale/STP state would be useless.
Solution: Extended the fixed 300-frame telemetry ring to record current scale, target scale, stress, sharpen, AUP lock, and `StpActive`.
Rejected Alternatives: `Debug.Log` or managed history lists.
Scalability potential: Same blackbox format covers toaster and top-tier cases.
Hardware Impact: Fixed NativeArray write; estimated 1 us/frame.

Problem: Unity 6000 RenderGraph churn can break legacy custom pass paths.
Solution: Kept the adapter on Unity's dynamic-resolution API and did not add a legacy `Execute`/`Blit` render pass.
Rejected Alternatives: inserting a manual blit/downscale pass; it would be fragile and likely more expensive.
Scalability potential: RenderGraph consumers can read the native state later without this adapter owning a pass.
Hardware Impact: No extra render pass.

Problem: AUP shifts can invalidate temporal reconstruction history.
Solution: Consume `AupShiftSignal` and freeze scale movement for three frames.
Rejected Alternatives: allowing scale decisions during rebase.
Scalability potential: Low and Ultra both protect STP/TAA history.
Hardware Impact: One counter branch per tick.

## Compile Gate 3

Problem: Third compile gate could not validate the project because `Hecton8.Core.csproj` references `Assets/_Project/Scripts/Physics/Tethers/Contracts/TetherSignalContracts.cs`, which is missing from disk.
Solution: Logged output to `Docs/AgentLogs/Dump_STP_QUALITY_ADAPTER_compile_attempt3_restore.txt` and marked final validation blocked by dependency.
Rejected Alternatives: Recreating or deleting tether contracts from the graphics scalability domain.
Scalability potential: None until physics/integration repairs the generated project file or tether contract ownership.
Hardware Impact: No runtime impact; build validation unavailable.

## Loop 4 Decisions - Tasks 16-18

Problem: STP quality is destroyed when transparent silt/bubble particles write bad motion vectors.
Solution: Static scan for motion-vector writes in project VFX/shader paths found no silt/bubble motion-vector writers; the only project VFX hit was debris using `MotionVectorGenerationMode.ForceNoMotion`.
Rejected Alternatives: sweeping material mutation at runtime; too risky and outside the adapter boundary.
Scalability potential: Low-tier STP keeps stable transparent history; high-tier avoids ghosted particles.
Hardware Impact: Static validation only; no frame cost.

Problem: Emergency scale below 0.4 needs a diegetic cue without chatty UI writes.
Solution: Register `OPTICS COMPENSATING` once and publish a HUD notification only when scale crosses below 0.4, rearming above 0.45.
Rejected Alternatives: writing text every frame from the scaler.
Scalability potential: Low-tier emergency has a faint diegetic explanation; high-tier never pays unless scale drops.
Hardware Impact: One signal on threshold crossing.

Problem: Final validation requires `dotnet build` but shared project compilation is broken externally.
Solution: Ran three compile gates and stored logs; marked task 18 as `[BLOCKED BY DEPENDENCY]`.
Rejected Alternatives: editing physics tether or AI files from the graphics adapter prompt.
Scalability potential: Adapter source is complete, but runtime proof waits for integrator build repair.
Hardware Impact: No measurable runtime data available until build is green.

## Loop 5 Polish

Problem: Omega polish required anti-bloat verification after core tasks were complete/blocked.
Solution: Ran static scans for `Update`, `ResolutionManager.Instance`, stale `Hecton8.Graphics.DRS`, direct finder APIs, and whitespace errors; patched stale DataVault handle reacquisition in the adapter.
Rejected Alternatives: Marking "VERIFIED MASTER GRADE" despite a known external compile wall.
Scalability potential: DataVault handle reacquisition prevents future native state loss after relocation/compaction.
Hardware Impact: Reacquire path is cold/error-path only; no new normal-frame cost.
