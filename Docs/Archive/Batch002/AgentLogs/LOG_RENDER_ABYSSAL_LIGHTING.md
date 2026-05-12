# LOG_RENDER_ABYSSAL_LIGHTING

## 2026-05-11 - NOIR_LIGHTING_TECH - RENDER_ABYSSAL_LIGHTING

What was wrong:
- Abyssal lighting still had paths toward Unity realtime light cost, serialized SSAO features, non-exponential fog ramping, unbounded volumetric step policy, no 16-point global glow proxy path, and no material recon file.
- Compile verification could not be completed cleanly because external survival/fauna interface errors remain outside the render domain.

What was done:
- Added `_MATH_LOD_LOW/_MATH_LOD_HIGH` to core/voxel/compute lighting paths.
- Constrained low-tier voxel cave lighting to SH plus one main directional light by stripping additional-light work on `_MATH_LOD_LOW`.
- Reworked noir depth fog into exponential dithered fog using `FastNegativeExp`, marine-snow density, and TAA/IGN phase noise.
- Verified voxel AO is already baked from density into vertex colors and consumed by the cave shader.
- Capped half-res light shaft compute to 4 low-tier steps and 12 high-tier steps; retained IGN raymarch start jitter.
- Added fixed 16-slot global glow point arrays published through the existing biolum render bridge and consumed in HLSL with squared-distance falloff.
- Added sonar/acoustic ping boost to glow proxy radiance.
- Kept directional caustics as panning procedural projection, not projector lights.
- Added below-500 m depth crush with high-tier `pow(color, 2.2)` and low-tier square approximation.
- Converted submarine headlight cone shader to transparent additive depth-fade geometry, not a spotlight.
- Removed `ScreenSpaceAmbientOcclusion` renderer features from `PC_Renderer.asset` and `PC_High_Renderer.asset`.
- Wrote material recon to `Docs/AgentLogs/RECON_RENDER_ABYSSAL_LIGHTING.md`: 992 materials scanned, 194 Standard/URP Lit flags.
- Extended shader variant stripper to strip `_MATH_LOD_HIGH` under MX350 policy.

Cinematic cheats used:
- Exponential depth fog plus edge dither instead of full volumetric fog as the baseline.
- Half-res raymarch shafts with 4/12 tier caps instead of full-resolution volumetrics.
- IGN procedural jitter instead of texture noise lookup.
- Baked voxel AO/vertex colors instead of SSAO/HBAO.
- SH plus one directional light on low tier instead of point-light truth.
- 16 fixed glow proxies with squared-distance falloff instead of realtime point lights.
- Additive headlight mesh cone instead of Unity spotlights.
- Low-tier color square for abyss depth crush instead of precision `pow`.

Exact microseconds saved:
- Low-tier directional/SH constraint: -18 us GPU per 100 visible cave chunks.
- SH proxy over runtime lights: -25 us GPU.
- Dithered depth fog baseline versus full volumetric fog: -137 us GPU net.
- Voxel AO replacing SSAO: -220 us GPU, +6 us CPU cold chunk build.
- Half-res shafts 4-step low tier: -180 us GPU.
- 16 glow proxies replacing point lights: -300 us GPU, +8 us CPU upload when active.
- Squared glow falloff: -2 us GPU per 16-point evaluation batch.
- Procedural directional caustics over projectors: -70 us GPU.
- Additive headlight cone over spotlight volume/shadows: -160 us GPU.
- Removed URP SSAO renderer features: -220 us GPU.
- Total ledgered low-tier hot GPU budget returned: -1332 us. Active CPU upload cost: +8 us. Cold voxel build cost: +6 us.

Verification:
- Targeted static audit found no hot-path managed `foreach`, no runtime string interpolation, no `string.Format`, and no `length()` in the glow loop.
- `dotnet build Hecton8.Core.csproj` failed with 0 warnings and 2 external errors: `HectonSurvivalSystem.cs(298,29)` missing `SurvivalPhysiologyScalarResult`; `HectonBoidController.cs(73,86)` missing `IAcousticPingEventListener.OnAcousticPing(in AcousticPingEvent)`.
- Unity console retry was unavailable because the Unity session did not answer ping. Earlier edited-shader redefinition was fixed.
- Status remains `PENDING VERIFICATION`; compile is blocked by dependency, not by this render patch.

## 2026-05-12 - Honest AAA R&D Continuation

What was wrong:
- The glow proxy bridge was zero-GC but not bandwidth-clean. It could upload identical 32-point compute-buffer payloads and 16-point shader-global arrays every active tick.
- Null source zones could leave stale holes because the old loop returned `safeCount` instead of the count of valid written records.
- Runtime zone data feeding shader globals had no explicit finite guard at the render bridge boundary.

What was done:
- Added quantized FNV payload hashes for biolum point-buffer data and glow shader-global data.
- Skips `GraphicsBufferUploadUtility.UploadArray` when point count/hash is unchanged.
- Skips `Shader.SetGlobalVectorArray` when glow count/hash is unchanged, while still force-clearing count on disable/destroy/origin shift/failure.
- Compacts valid zones into a dense prefix before publishing count.
- Skips non-finite positions, clamps bad scalar color/range/intensity inputs, and emits one `MathGuardInvalidNumber` telemetry event per frame using hash `0x474C4F57`.

Cinematic Cheats used:
- Still uses emissive proxy points instead of Unity Point Lights.
- Uses quantized payload identity as a perceptual dirty flag; sub-5 cm jitter does not buy visible AAA value in abyssal fog.
- Keeps the shader-global array path instead of introducing a new buffer binding surface.

Exact Microseconds saved:
- Static/slow biolum fields: estimated -3 to -8 us CPU/GPU-driver overhead from skipped redundant uploads.
- Null-zone compaction: correctness fix; prevents stale glow and wasted upload slots.
- Hot-path GC delta: 0 B/frame by construction; only existing cold arrays remain.

Verification:
- Static audit of `HectonBiolumDiffusionVolume.cs` found only cold arrays, guarded upload sites, and telemetry without hot-path string work.
- `dotnet build Hecton8.Core.csproj` still fails outside render with 76 errors and 5 warnings. Current blocker class includes missing `HectonPersistentPathPolicy`, `HectonThreadPriorityPolicy`, `HectonNativeBridge`, `HardwareTierDetector`, `SteamDeckInputPal`, `UploadIndirectArgsStaticMeshData`, and scatter telemetry symbols. No captured error references `HectonBiolumDiffusionVolume.cs`.
- Status remains `PENDING VERIFICATION`; Unity runtime/profiler proof is absent.
