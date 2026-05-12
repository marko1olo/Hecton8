# LOG_RENDER_FILLRATE

## 2026-05-11 - Kill Transparent Overdraw

Status: PENDING VERIFICATION

What was wrong:
- Visor/HUD/fluid/smoke paths were capable of stacked alpha blending or transparent-queue overdraw.
- Blood/fluid aftermath used mesh decals instead of screen-space projection.
- Dense VFX filtering missed new AlphaTest/Cutout queues.
- Low-tier refraction and flora biolum paths still spent bandwidth/math where a visual fake was sufficient.
- Build hygiene had no 02_HECTON_WORLD transparent-overlap gate.

What was done:
- Converted visor/radar/smoke/plume/fluid fallback paths to cutout/dither with opaque alpha output.
- Added visor stencil write ref 1 and radar HUD stencil compare equal ref 1.
- Added RenderGraph depth prepass for Water/Terrain/VoxelCave with a hidden depth-only shader.
- Ensured half-res VFX captures cutout queues and composites via bilateral depth upsample.
- Added black-crush, dithered shadow, fog jitter, 3-sine caustics, depth-faded cutout, far flat-noir vegetation, vertex SH fauna, point-light variant stripping audit, screen-space fluid decals, refraction Math LOD, procedural biolum, BRG presentation packing, and a transparent-overdraw build gate.

Cinematic cheats used:
- 3-sine ALU caustics instead of texture/projector caustics.
- IGN/TAA dither for shadows/fog/coverage instead of extra taps.
- Screen-space deferred fluid decals instead of transparent blood geometry.
- Low-tier static visor UV offset instead of full refraction.
- Triangle-wave biolum from `_Time.y` and world/UV position instead of emissive texture masks.
- BRG color/biolum/damage packed into one Vector4 lane instead of a wider payload.
- `rcp` polish in repeated shader paths instead of divides.

Exact microseconds saved, estimated until GPU capture:
- Dithered visor/radar alpha removal: 250 us.
- Water/opaque depth prepass: 110 us.
- Half-res VFX with bilateral composite: 220 us.
- Dithered low-tier shadows: 95 us.
- Fog jitter preserving low taps: 45 us.
- ALU caustics avoiding texture bandwidth: 60 us.
- Depth-faded cutout smoke/plumes: 80 us.
- Vegetation TAA motion stability: 35 us.
- 20m flat-noir vegetation LOD: 140 us.
- Vertex SH fauna and point-light stripping: 65 us.
- Screen-space fluid decals: 120 us.
- Low-tier visor refraction LOD: 55 us.
- Zero-texture biolum pulse masks: 35 us.
- BRG packing: 25 us.
- Build gate: 0 us runtime.
- Total estimate: 1,335 us in worst fill-rate scenes, PENDING RenderDoc/MX350 verification.

Stencil visor mask evidence:

```hlsl
Stencil
{
    Ref 1
    Comp Always
    Pass Replace
    WriteMask 255
}
```

HUD stencil evidence:

```hlsl
Stencil
{
    Ref [_StencilRef]
    Comp Equal
    Pass Keep
    ReadMask 255
}
```

Bilateral half-res upscale evidence:

```hlsl
float HectonBilateralDepthWeight(float centerDepth, float tapDepth)
{
    float depthScale = max(_HectonHalfResParticlesBilateralDepthScale, 0.001);
    return exp2(-abs(tapDepth - centerDepth) * depthScale);
}

HectonAccumulateParticleTap(uv + tapOffset * float2(-1.0, -1.0), centerDepth, colorAccum, weightAccum);
HectonAccumulateParticleTap(uv + tapOffset * float2( 1.0, -1.0), centerDepth, colorAccum, weightAccum);
HectonAccumulateParticleTap(uv + tapOffset * float2(-1.0,  1.0), centerDepth, colorAccum, weightAccum);
HectonAccumulateParticleTap(uv + tapOffset * float2( 1.0,  1.0), centerDepth, colorAccum, weightAccum);

return (half4)(colorAccum * rcp(max(weightAccum, 0.0001)));
```

Verification:
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`: passed, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: passed, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: passed, 0 warnings, 0 errors.
- Unity shader import, Frame Debugger, RenderDoc, GC profiler, and MX350 GPU capture: PENDING.

Scoped diff:
- Docs: `Docs/Tasks/Status_RENDER_FILLRATE.md`, `Docs/AgentLogs/Rationale_RENDER_FILLRATE.md`, `Docs/AgentLogs/LOG_RENDER_FILLRATE.md`.
- C#: `DeferredDecalPass.cs`, `HectonHalfResParticlesFeature.cs`, `HectonFillrateDepthPrepassFeature.cs`, `AbyssalFluidDecalManager.cs`, `HectonIndirectVegetationContracts.cs`, `HectonIndirectVegetationRenderer.cs`, `HectonTransparentOverdrawBuildGuard.cs`.
- Shaders: `SuitVisor.shader`, `Hecton_HUD_AcousticRadarOverlay.shader`, `Hecton_HalfResParticleComposite.shader`, `AbyssalFluidDecal.shader`, `Hecton_DeferredDecal.shader`, `Hecton_FillrateDepthOnly.shader`, `Hecton_CoreLit.hlsl`, `AbyssalBlackSmoke.shader`, `Hecton_LeakPlume.shader`, `Hecton_NoirDepthFog.shader`, `Hecton_DryZoneLit.shader`, `Hecton_AbyssalVoxelRock.shader`, `Hecton_ScatterIndirectLit.shader`, `Hecton_WreckIndirectLit.shader`, `Hecton_LeviathanOrganic.shader`, `Hecton_VolumetricLight.compute`, `Hecton_ScooterVolumetricShafts.shader`, `Hecton_KelpMaster.shader`, `Hecton_KelpMaster_GPUI.shader`, `Hecton_CoralMaster.shader`, `Hecton_CoralMaster_GPUI.shader`, `Hecton_IndirectVegetation.shader`.

---

2026-05-11 Loop 6 Continuation - Broad Runtime Alpha Sweep

Status: PENDING VERIFICATION.

What was still wrong:
- Static scan after the first 20 tasks still found blended runtime presentation shaders outside the initial critical set: holograms, diegetic UI, scanner/PDA overlays, tether/pipe visuals, rain/laser/silt effects, phantom drones, seam-gap masking, and the sun pass.
- Some were additive instead of `SrcAlpha`, but they still used `Transparent` queues and stacked fill-rate.

What was done:
- Converted the remaining HECTON runtime presentation shaders to `AlphaTest`/`TransparentCutout`, `Blend Off`, stochastic screen-space dither `clip`, and opaque return alpha.
- Converted HECTON runtime additive presentation passes (`FlashlightConeSilt`, `LaserCutRadianceDecal`, `PhantomDrones`, `SeamGapDitherIndirect`, `Sun`) away from `Transparent` queues.
- Converted live overlay UI shaders under `Assets/_Project/Shaders/UI` (`RetinaStressPulse`, `IGNDitherDissolve`, `DiegeticPanelDepthFade`, `DataRecPulse`) away from `Blend SrcAlpha`.
- Converted archived `Assets/_Project/_Archive/HectonOcean.shader` away from `Transparent` tags and `Blend SrcAlpha` so static `_Project` alpha scans are clean.
- Hardened `HectonTransparentOverdrawBuildGuard` so it flags non-off blend states while allowing `Blend Off` and no-op `Blend One Zero`.

Cinematic cheats used:
- IGN/stochastic coverage replaced soft alpha and additive glow stacking.
- Hologram/visor/UI visibility remains temporal-dither fake instead of blended transparency.
- Silt cone, laser radiance, phantom drones, and seam-gap repairs now buy glow with sparse pixels instead of additive overdraw.

Exact microseconds saved:
- Additional broad-sweep estimate: 180-320 us in UI+hologram+silt stress frames on i3/MX350, PENDING GPU CAPTURE.
- Updated worst-case total estimate: 1,515-1,655 us saved, PENDING RenderDoc/MX350 verification.

Verification:
- `rg -n -F "Blend SrcAlpha" -- Assets/_Project`: no matches.
- `rg -n -P '"Queue"\s*=\s*"Transparent' -- Assets/_Project`: no matches.
- `rg -n -P '"RenderType"\s*=\s*"Transparent"' -- Assets/_Project`: no matches.
- `rg -n -P --glob '*.shader' "^\s*Blend\s+(?!(Off|One\s+Zero)\b)" Assets/_Project`: only `Crest_SargassumFoamDamping.shader`, `Crest_SargassumWaveDamping.shader`, and hidden editor `Hecton_OverdrawHeatmap.shader` remain.
- `dotnet build .\Hecton8.Core.csproj --no-restore -v:minimal`: PASSED, 0 warnings, 0 errors after Unity regenerated Temp assets.
- `dotnet build .\Assembly-CSharp.csproj --no-restore -v:minimal`: PASSED, 0 warnings, 0 errors.
- `dotnet build .\Hecton8.Editor.csproj --no-restore -v:minimal`: PASSED, 0 warnings, 0 errors.
- Unity batchmode import/compile: PARTIAL PASS / BLOCKED BY DEPENDENCY. `Hecton8.Optimization.Editor.dll` compiled, IL-postprocessed, and copied; Unity failed later in unrelated `Assets/_Project/Scripts/Editor/DataMonolith/H8DataMonolithCompiler.cs(745,34)` and `(746,34)` float-to-uint errors. Log saved at `Docs/AgentLogs/Unity_RENDER_FILLRATE.log`.
