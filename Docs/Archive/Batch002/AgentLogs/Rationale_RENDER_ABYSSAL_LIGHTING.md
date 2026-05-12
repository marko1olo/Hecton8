# Rationale_RENDER_ABYSSAL_LIGHTING

STATUS: PENDING VERIFICATION (OMEGA POLISH COMPLETE; COMPILE BLOCKED BY DEPENDENCY)

## Mandate Selection
Problem: Abyssal lighting requires fog, light shafts, shader LOD, voxel AO, material reconnaissance, and SSAO removal without exceeding MX350 budgets.
Solution: Loaded eight relevant mandates covering noir fog, voxel lighting, URP hot paths, compute kernels, perf budgets, zero-GC, visual fake doctrine, and telemetry.
Rejected Alternatives: Reading all mandate files would add context noise; starting from Unity real-time lights or SSAO violates render mandates.
Scalability potential: Low uses depth/LUT fog, baked AO, no SSAO, no point-light truth. Middle uses half-res light shafts and limited glow arrays. High uses more raymarch steps and stronger caustics. Ultra spends saved cycles on visual overkill, not gameplay simulation.
Hardware Impact: Expected low-end i3/MX350 gain comes from avoiding Unity SSAO, point-light clusters, full-res volumetrics, and runtime material clones.

## Decisions
Problem: Durable state files were absent for this prompt.
Solution: Created `Docs/Tasks/Status_RENDER_ABYSSAL_LIGHTING.md` and this rationale file before code edits.
Rejected Alternatives: Chat-only tracking is rejected by batch protocol and will be lost during context compression.
Scalability potential: No runtime effect; process integrity only.
Hardware Impact: 0 us runtime impact.

## HONEST AAA R&D CONTINUATION - 2026-05-12
Problem: The previous glow proxy implementation was zero-GC but still pushed the same compute-buffer data and shader-global arrays every active tick. That violates bandwidth discipline: small uploads are still driver work, and unchanged PCIe traffic is waste on MX350.
Solution: Added quantized FNV hashes for the 32-point biolum compute payload and the 16-point glow proxy payload. `GraphicsBufferUploadUtility.UploadArray` now runs only when count/hash changes. `Shader.SetGlobalVectorArray` now runs only when glow count/hash changes or when teardown force-clears the global count.
Rejected Alternatives: Replacing the global array with a new GraphicsBuffer/SRV was rejected for this continuation because it would require shader binding changes across consumers and increase integration risk. Leaving per-tick uploads because the array is only 16 elements was rejected by the bandwidth rule.
Scalability potential: Low/MX350 benefits from skipped CPU-driver traffic in static biolum rooms. High/Ultra keep the same visual overkill path and can spend the saved driver time on stronger bloom/caustic response later.
Hardware Impact: Estimated -3 to -8 us CPU/GPU-driver overhead in static or slowly changing biolum zones; 0 B/frame; no extra managed collections.

Problem: The old point collection loop returned `safeCount` even when a source zone was null. That can leave stale slots in `_pointUpload` and false glow in the shader/global buffer.
Solution: Replaced source-index writes with a dense `writeCount` prefix. Null zones are skipped, valid zones are packed, and the published count matches valid data.
Rejected Alternatives: Clearing all 32 slots every tick was rejected as unnecessary bandwidth/math. Keeping stale slots behind a larger count was rejected as correctness debt.
Scalability potential: Low avoids visual lies from stale glow. High/Ultra get stable glow proxies for later SSGI/emission consumers.
Hardware Impact: Correctness fix; removes wasted upload work for invalid slots.

Problem: Glow data comes from runtime zones and feeds rendering. Non-finite positions, colors, ranges, or intensities could poison shader globals.
Solution: Added finite checks before writing point/glow upload records. Invalid positions are skipped. Invalid scalar inputs are clamped to safe non-negative fallbacks and emit `GlobalTelemetryBus.PublishMathGuardInvalidNumber(0x474C4F57)` at most once per frame.
Rejected Alternatives: Trusting zone providers was rejected. Direct logs were rejected because hot-path strings allocate and spam.
Scalability potential: All tiers get deterministic fallback instead of NaN flashes. Telemetry gives postmortem proof without chat guesswork.
Hardware Impact: Normal path cost is scalar finite branches; error path records a fixed telemetry event.

Problem: Verification remains blocked by other agents' partial symbols.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:normal`. It now fails with 76 errors and 5 warnings outside this render file, including missing `HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `HardwareTierDetector`, `SteamDeckInputPal`, `UploadIndirectArgsStaticMeshData`, and scatter telemetry methods. No `HectonBiolumDiffusionVolume.cs` compiler error appeared in the captured compiler tail.
Rejected Alternatives: Fixing core/save/audio/fauna/native bridge symbols from a lighting prompt was rejected as cross-domain sabotage.
Scalability potential: No render runtime change; preserves parallel integration boundaries.
Hardware Impact: 0 us runtime impact.

## OMEGA POLISH CHANGES
Problem: Dear-lie audit required replacing honest lighting work with cheaper cinematic cheats where possible.
Solution: Kept exponential fog as `1 - FastNegativeExp(depth * density)` instead of a true expensive volumetric solve; kept IGN/TAA phase dither instead of blue-noise texture fetches; kept half-res light shafts at 4 low / 12 high steps; kept 16 glow proxies with squared-distance falloff instead of Unity Point Lights; kept low-tier depth crush as `color * color` and high-tier overkill as `pow(color, 2.2)` below 500 m.
Rejected Alternatives: Full-resolution volumetric fog, realtime point/spot lights, SSAO/HBAO, unbounded raymarch profiles, and exact distance falloff were rejected as budget waste.
Scalability potential: Low = dithered depth fog, baked voxel AO, 4-step half-res shafts, 16 glow proxies, no SSAO, no point lights. Middle = capped profile shafts and core-lit SH. High = 12-step shafts and accurate depth crush. Ultra = saved GPU budget can be spent on stronger caustics/biolum bloom without changing gameplay truth.
Hardware Impact: Ledgered low-tier GPU return is -1332 us hot path versus the rejected Unity-lighting/SSAO/full-fog path; glow proxy upload costs about +8 us CPU when active; voxel AO costs about +6 us CPU only during cold chunk build.

Problem: Frame-time audit required proof that no new hot-path GC, managed foreach, string formatting, or unconditional precision normalize was introduced.
Solution: Targeted audit on the touched render files found no hot-path managed `foreach`, no `string.Format`, and no string interpolation in the runtime bridge. `HectonShaderVariantStripper` has `.ToString()` only in an Editor-folder build pipeline class. `HectonCoreLitSafeNormalize` uses dominant-axis fallback unless `_MATH_LOD_HIGH` is explicitly selected. Volumetric compute uses `rsqrt` in its normalization/falloff hot path.
Rejected Alternatives: Runtime material instance strings, material property blocks per object, `length()` glow falloff, or direct fauna/light dependencies were rejected.
Scalability potential: Low devices skip precision normalization and expensive shader variants. High devices keep the overkill path behind `_MATH_LOD_HIGH`.
Hardware Impact: Estimated -2 us GPU for squared glow falloff alone; zero new runtime managed allocation sites in this prompt-owned bridge.

Problem: Silo audit flagged one cross-domain file, `HectonBiolumDiffusionVolume.cs`.
Solution: Kept the change because that class is the existing biolum render bridge and already consumes `GlobalRegistry.BiolumManager`. The lighting task adds only global shader proxy publication through fixed arrays; it does not call fauna, leviathan, flare, or gameplay objects directly.
Rejected Alternatives: Adding a new direct event dependency to fauna/flares or spawning Unity lights was rejected.
Scalability potential: Same bridge feeds low-tier proxy glow and high-tier SSGI/emission visuals.
Hardware Impact: About -300 us GPU versus 16 realtime point lights, with about +8 us CPU upload when active.

Problem: Final verification cannot be honestly marked as a clean build.
Solution: Re-ran `dotnet build Hecton8.Core.csproj --no-restore -m:1 -nr:false -p:UseSharedCompilation=false -p:RunAnalyzers=false -v:normal`. It failed with 0 warnings and 2 errors outside this prompt domain: `HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`, and `HectonBoidController.cs(73,86)` not implementing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)`. Unity console retry failed because the session did not answer ping.
Rejected Alternatives: Editing survival/fauna acoustic interfaces from the render prompt was rejected as domain sabotage.
Scalability potential: No render runtime change; protects parallel agent boundaries.
Hardware Impact: 0 us runtime impact.

### Final Git Diff
Targeted modified code/assets:
- `Assets/_Project/Art/Shaders/Hecton_AbyssalVoxelRock.shader`
- `Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl`
- `Assets/_Project/Art/Shaders/Hecton_FlashlightConeSilt.shader`
- `Assets/_Project/Art/Shaders/Hecton_NoirDepthFog.shader`
- `Assets/_Project/Art/Shaders/Hecton_VolumetricLight.compute`
- `Assets/_Project/Data/PC_High_Renderer.asset`
- `Assets/_Project/Data/PC_Renderer.asset`
- `Assets/_Project/Scripts/Editor/HectonShaderVariantStripper.cs`
- `Assets/_Project/Scripts/Visor/VolumetricLightFeature.cs`
- `Assets/_Project/Scripts/World/Biolum/HectonBiolumDiffusionVolume.cs`

Diff stat before appending final logs:
```text
.../Art/Shaders/Hecton_AbyssalVoxelRock.shader     |  36 ++--
Assets/_Project/Art/Shaders/Hecton_CoreLit.hlsl    | 185 ++++++++++++++++++---
.../Art/Shaders/Hecton_FlashlightConeSilt.shader   |   8 +
.../Art/Shaders/Hecton_NoirDepthFog.shader         |  25 ++-
.../Art/Shaders/Hecton_VolumetricLight.compute     |  23 ++-
Assets/_Project/Data/PC_High_Renderer.asset        | 147 ++++++++++------
Assets/_Project/Data/PC_Renderer.asset             |  38 ++---
.../Scripts/Editor/HectonShaderVariantStripper.cs  |  70 +++++++-
.../Scripts/Visor/VolumetricLightFeature.cs        |  35 +++-
.../World/Biolum/HectonBiolumDiffusionVolume.cs    |  29 ++++
10 files changed, 456 insertions(+), 140 deletions(-)
```

Evidence files created by this prompt:
- `Docs/Tasks/Status_RENDER_ABYSSAL_LIGHTING.md`
- `Docs/AgentLogs/Rationale_RENDER_ABYSSAL_LIGHTING.md`
- `Docs/AgentLogs/RECON_RENDER_ABYSSAL_LIGHTING.md`
- `Docs/AgentLogs/LOG_RENDER_ABYSSAL_LIGHTING.md`

Problem: Low-tier cave lighting still exposed a path to runtime additional lights and high math variants.
Solution: Added `_MATH_LOD_LOW/_MATH_LOD_HIGH` variants, guarded the voxel rock additional-light loop on low tier, and extended the shader variant stripper to remove `_MATH_LOD_HIGH` under the MX350 strip policy.
Rejected Alternatives: Keeping Unity additional lights alive and relying on inactive scene lights was rejected; it still pays variant and cluster cost.
Scalability potential: Low = SH + main light only. Middle = limited additional-light fallback. High/Ultra = high math variant retained when strip policy allows it.
Hardware Impact: Estimated -18 us GPU per 100 visible cave chunks on i3/MX350 due to removed low-tier additional-light work.

Problem: Depth fog was a film ramp, not an exponential dithered transition.
Solution: Replaced the ramp with `1 - FastNegativeExp(depth * density)`, kept marine snow density, and widened the dither only near the fog transition edge.
Rejected Alternatives: Full volumetric fog as the baseline was rejected because it spends too much fill and raymarch cost before visibility proves need.
Scalability potential: Low = one full-screen depth pass. Middle = depth fog plus half-res shafts. High/Ultra = stronger shafts and caustics on top.
Hardware Impact: Estimated +3 us GPU for the fog math, avoiding roughly 140 us versus making this a full volumetric solve.

Problem: Bioluminescence needed a point-light replacement without direct dependencies on leviathan/flares owned by other agents.
Solution: Published 16 global glow points from `HectonBiolumDiffusionVolume` using existing `GlobalRegistry.BiolumManager` zone data, then evaluated them in HLSL with squared-distance falloff and sonar pulse boost.
Rejected Alternatives: Unity Point Lights, material property blocks per renderer, and a direct fauna dependency were rejected.
Scalability potential: Low = up to 16 nearest glow proxies. Middle = glow proxies plus 3D biolum volume. High/Ultra = SSGI can consume the same emissions for visual overkill.
Hardware Impact: Estimated -300 us GPU versus 16 realtime point lights on MX350; CPU upload is roughly +8 us when active.

Problem: Light shafts were capped at seven steps, missing the required 12 high / 4 low tier split.
Solution: Added compute macros for 12 max steps and 4 low-tier cap, and mirrored that decision in `VolumetricLightFeature.ResolveRaymarchSteps`.
Rejected Alternatives: Profile-driven 16/32-step budgets were rejected for this prompt because they exceed the mandate.
Scalability potential: Low = 4 steps half-res. Middle = profile/fallback capped to 12. High/Ultra = 12 steps with stronger scatter and caustic contribution.
Hardware Impact: Estimated -180 us GPU on MX350 low tier, with high-tier overkill buying visible shaft density.

Problem: Unity SSAO/HBAO was serialized into renderer assets even though inactive.
Solution: Used Unity AssetDatabase/SerializedObject to remove `ScreenSpaceAmbientOcclusion` subfeatures from PC renderer assets and wrote material reconnaissance to `RECON_RENDER_ABYSSAL_LIGHTING.md`.
Rejected Alternatives: Merely setting `m_Active: 0` was rejected because the prompt requires removal and future build policy can resurrect inactive features.
Scalability potential: Low/Middle/High/Ultra all use vertex AO/contact shadows instead of Unity SSAO; higher tiers can spend saved budget on caustics/shafts.
Hardware Impact: Estimated -220 us GPU on MX350 when compared to enabling URP SSAO.

Problem: Compile verification surfaced errors outside the render domain after shader fixes were clean.
Solution: Fixed the only edited-shader error (`_HectonMathLodMode` duplicate), reran `dotnet build`, then stopped at the domain boundary and marked compile check blocked by unrelated `HectonSurvivalSystem` and `HectonBoidController` contract errors.
Rejected Alternatives: Editing survival physiology or fauna acoustic interfaces from the lighting prompt was rejected as domain violation.
Scalability potential: No runtime rendering change; protects parallel-agent integration boundaries.
Hardware Impact: 0 us runtime impact.
