# Rationale 1427

Date: 2026-05-28
Agent: 1427
Domain: ARCHITECTURAL_PURITY_AND_SCALABILITY_ENFORCER

## Decision 00 - Initial State Discipline

Problem: Agent state files were absent, while the active protocol requires disk-backed state before source edits.
Solution: Created fresh status and rationale files before scanning source. Loaded task-relevant mandates for visual fake first, zero GC, registry DI, and execution phases.
Rejected Alternatives: Proceeding from chat memory would violate anti-amnesia and produce unverifiable work.
Scalability potential: Low/Middle/High/Ultra unaffected directly; this preserves auditability of later code changes.
Hardware Impact: 0 us runtime gain; prevents uncontrolled source edits under multi-agent concurrency.

## Decision 01 - Phase 0 Target Selection

Problem: Repository scan found many expensive math and quality-tier sites, but the worktree is heavily modified by other agents and several hot C# systems own gameplay truth.
Solution: Select the clean kelp shaders as the first surgical target. They contain binary `_QUALITY_MX350/_QUALITY_HIGH` variants and sine-driven flora sway while remaining visual-only and already consuming `_H8GlobalQualityWeight`.
Rejected Alternatives: Editing AI/pathfinding/physics broad systems before ownership proof would risk authority corruption; adding a C# quality broadcast route would violate cold DI because the shader scalar already exists.
Scalability potential: Low uses reduced scalar amplitude and cheap arithmetic wave; Middle increases sway and parallax smoothly; High extends rich motion without a keyword branch; Ultra spends saved variant/trig cost on denser visible flora/material richness through the same shader path.
Hardware Impact: Pending static diff; expected GPU instruction/variant reduction on i3/MX350 with no gameplay truth change.

## Decision 02 - Kelp Visual Fake Implementation

Problem: Kelp shaders used `_QUALITY_MX350/_QUALITY_HIGH` keyword branches and `sin` for ambient sway. High quality also re-sampled the mask map after parallax, creating a binary expensive path.
Solution: Removed the shader feature keyword, replaced sine with `HectonKelpDearLieWave` cubic triangle arithmetic, changed MX350 amplitude suppression into a continuous `lerp(0.58h, 1.0h, SmoothRange(GlobalQualityWeight))`, and made parallax offset continuous without the extra mask re-sample.
Rejected Alternatives: Keeping high/low variants would preserve binary quality; a texture LUT would add asset/streaming dependency; CPU-side animation would add a hot C# route and material update risk.
Scalability potential: Low gets small cheap sway and zero parallax offset; Middle gains proportional sway and starts parallax; High gets full amplitude with the same shader path; Ultra can afford denser kelp/material detail because variant and trig waste were removed.
Hardware Impact: Estimated 20-60 us saved in dense kelp views from removed high-path mask resample plus SFU trig removal; measured proof absent until Unity/RenderDoc.

## Decision 03 - Verification Scope

Problem: The prompt demanded proof while forbidding cluster-heavy builds and rejecting JSON reports.
Solution: Added an Editor test that reads shader sources, bans binary quality keywords/trig/reintroduced mask reassign, and sweeps the continuous scalar formula for monotonic finite output. Ran `rg` static gates and `git diff --check`.
Rejected Alternatives: `dotnet build`/Unity import would violate the user's CPU constraint; Burst IL reflection is irrelevant because no Burst job was modified.
Scalability potential: Test prevents regressions back to low/high dichotomy; same source path remains valid from weak devices through ultra hardware.
Hardware Impact: 0 B/frame added; editor-only test cost outside runtime.

## Decision 04 - Retina Runtime Keyword Removal

Problem: Retina distortion used a runtime MX350 shader keyword, which made chromatic/distortion behavior a binary hardware branch.
Solution: Packed a continuous retina quality scalar into the existing globals buffer and multiplied distortion/chroma by smooth scalar windows inside the shader.
Rejected Alternatives: Keeping `_QUALITY_MX350` or creating separate materials would preserve binary variants and keyword churn.
Scalability potential: Low keeps minimal retina instability; Middle restores controlled chroma; High/Ultra reaches full distortion without changing shader variant.
Hardware Impact: 0 B/frame added; avoids global keyword mutation and variant divergence.

## Decision 05 - Scatter Material Path Flattening

Problem: GPU scatter selected high/low material variants and validated `_QUALITY_MX350/_QUALITY_HIGH` keywords at render time.
Solution: Prefer the primary material, keep legacy fallback fields only as null fallback, validate only `HECTON_GPU_INDIRECT`, and keep visual properties on continuous `_cachedQualityWeight01` lerps.
Rejected Alternatives: Renaming serialized fields would break scene/prefab data; preserving quality keyword validation would keep binary scalability.
Scalability potential: Low/Middle/High/Ultra share one material authority while SSS/bloom/caustic/cull distances scale continuously.
Hardware Impact: Estimated 3-12 us/frame avoided in scatter-heavy views from material path simplification and no quality keyword validation; static estimate.

## Decision 06 - Project Shader Math LOD Keyword Purge

Problem: `_MATH_LOD_LOW/_MATH_LOD_HIGH` remained as compile-time shader variants in CoreLit, Terrain, IndirectVegetation, AbyssalVoxelRock, Coral, and ProceduralBio.
Solution: Removed visual math LOD keywords and replaced them with `_HectonMathLodWeight`, `_H8GlobalQualityWeight`, or existing indirect vegetation quality scalar. Cheap paths now use scalar early-out or scalar amplitude, not compile-time branches.
Rejected Alternatives: Removing only C# keyword toggles would leave default shader variants wrong; computing all high and low paths then lerping would waste low-tier GPU time.
Scalability potential: Low uses dominant-axis, matcap, cheap flow/dither/wake suppression; Middle ramps detail; High/Ultra enables richer POM, flow normals, additional lights, sonar grids, and exact normalize weight.
Hardware Impact: Estimated 10-80 us GPU/frame scene-dependent; larger build/import variant reduction from deleted shader keyword variants.

## Decision 07 - Core Phase-Safe Continuous Broadcast

Problem: DistanceMath and GlobalRegistry flushed math precision by global shader keywords even though SystemDispatcher already has a visual sync flush point.
Solution: DistanceMath now pushes `_HectonMathLodWeight`/mode floats only; GlobalRegistry math precision flush writes only `_H8MathLodLowBlend`; SystemDispatcher visual sync remains the only route.
Rejected Alternatives: Direct writes from pressure detection phases would violate phase ownership; keeping keywords would reintroduce binary shader state.
Scalability potential: All devices receive one scalar route; no variant flip while quality changes under thermal pressure.
Hardware Impact: Removes keyword churn; 0 B/frame added.

## Decision 08 - Verification Without Build

Problem: A full build is forbidden and a dotnet process was already active on the host.
Solution: Used targeted source scans, modified-file diff check, and editor source gates. No `dotnet build`, MSBuild, or Unity test runner was launched.
Rejected Alternatives: Running build under active cluster load would violate user constraint and risk blocking other agents.
Scalability potential: Verification now catches reintroduction of project shader quality/math keywords.
Hardware Impact: Avoided heavy CPU compile load; restricted `git diff --check` passed for touched files, global check remains blocked by unrelated existing whitespace in task docs.

## Decision 09 - Unity QualitySettings Preset Removal

Problem: Project scripts still read `QualitySettings.GetQualityLevel()`, creating a binary Unity preset ceiling outside HomeostasisBrain.
Solution: URP shadow budget now resolves solely from `HomeostasisBrain.GlobalQualityWeight` and platform recommendation; flora wake trail resources use quantized continuous global quality; graphics-state collection quality labels no longer block warmup compatibility.
Rejected Alternatives: Mapping Unity quality names to tiers would preserve preset authority and duplicate the homeostasis scalar.
Scalability potential: Low/Middle/High/Ultra resource pressure now follows the same continuous owner instead of a Unity settings index.
Hardware Impact: 0 B/frame added; avoids binary resource refresh and shadow budget jumps under preset changes.

## Decision 10 - Flora Tick Lookup Flattening

Problem: `FloraInteractionManager.Tick()` called `ResolvePlayerState()`, and the player-root-change branch used `TryGetComponent` for movement/tool ownership. The lookup was rare but still lived under a high-frequency phase.
Solution: Player movement/tool references now refresh from `IPlayerRuntimeContext` in the Tick path, while direct scene override fallback resolves through `TryGetComponent` only in cold `OnEnable`/GlobalRegistry hot-swap cache refresh.
Rejected Alternatives: Keeping the identity-change lookup in Tick would violate hot-loop accessor policy; removing direct-scene fallback entirely would break isolated scene validation.
Scalability potential: Low/Middle/High/Ultra keep the same player interaction visuals, but root swaps no longer inject reflection-style component lookup into the frame phase.
Hardware Impact: 0 B/frame added; avoids one-off player-root swap spikes on weak CPUs and keeps steady-state Tick lookup-free.

## Decision 11 - Encounter Candidate Capacity Scalar

Problem: `EncounterDirector` selected 16 or 32 spawn candidates through a hard `SystemInfo` high-tier boolean.
Solution: Candidate capacity now lerps from base to high count by continuous hardware weight multiplied by `HomeostasisBrain.GlobalQualityWeight` and `PlatformAdaptiveBudgetGovernor.RecommendedQualityWeight`.
Rejected Alternatives: Keeping CPU/VRAM threshold branch would preserve binary scalability; always using 32 candidates wastes weak CPU time.
Scalability potential: Low keeps cheap candidate scans, Middle ramps gradually, High/Ultra spend extra checks on better cinematic spawn placement.
Hardware Impact: 0 B/frame added; candidate search cost now degrades continuously under i3/MX350 pressure.

## Decision 12 - Tether Taut-Line Visual Fake Blend

Problem: Tether visual upload collapsed to a straight-line fake through a boolean low-tier path, causing a potential visual step when quality or tension crossed threshold.
Solution: Visual points now blend continuously between straight-line fake and Verlet curve by quality and load weight.
Rejected Alternatives: Keeping the bool fake would save the same work but create pop; computing only full Verlet visual ignores low-tier presentation budget.
Scalability potential: Low leans into cheap taut silhouettes, Middle restores curvature gradually, High/Ultra show full simulated curve.
Hardware Impact: 0 B/frame added; same buffer writes, no allocations, fewer visible discontinuities.

## Decision 13 - Audio Virtualization Continuous Contract

Problem: `IAudioVirtualizationService.SetLowTierVirtualization(bool)` exposed binary quality authority even though the implementation already had a continuous voice-budget path.
Solution: Replaced the public API with `SetVirtualizationQualityWeight(float)` and routed it through `ApplyVirtualVoiceQualityWeight`.
Rejected Alternatives: Keeping the bool wrapper would preserve accidental 0/1 external load shedding.
Scalability potential: Low/Middle/High/Ultra can now request proportional physical voice budgets.
Hardware Impact: 0 B/frame added; no new allocations or lookup routes.

## Decision 14 - Misleading Tier Semantics

Problem: `HighTierSmoothSteering` and `highTierMaskDetail` encoded hardware-tier language in code paths that are actually apex-behavior or scalar mask-detail activity.
Solution: Renamed cognition to `ApexSmoothSteering`; renamed terrain seam mask detail to `maskDetailActive` while preserving scalar `maskDetailWeight` ownership.
Rejected Alternatives: Leaving misleading names makes future agents rebuild binary hardware forks on top of correct scalar code.
Scalability potential: No behavior change; keeps Low/Middle/High/Ultra decisions attached to scalar weights, not names.
Hardware Impact: No direct runtime gain; reduces architectural drift risk.
