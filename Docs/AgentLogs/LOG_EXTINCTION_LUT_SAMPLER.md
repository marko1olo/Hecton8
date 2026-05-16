# LOG_EXTINCTION_LUT_SAMPLER

## Extinction LUT Sampler Report
What was wrong:
- Beer-Lambert LUT existed on disk, but there was no global runtime loader or shader sampling path.
- Underwater/fog color was still partly driven by RenderSettings.fogColor writes, which fights shader/post-process extinction.
- Low-end and high-end paths were not separated: uniform water color gave no red-light extinction depth cue.

What was done:
- Added `Assets/_Project/Art/Shaders/Hecton_WaterExtinction.hlsl` as the shared packed-LUT sampling include.
- Integrated extinction into `Hecton8_UberNoir.hlsl`: LOW vertex sample, non-LOW per-pixel sample, albedo extinction, fog tint, and IGN stabilization.
- Integrated post stack hooks in `Hecton_NoirDepthFog.shader` and `Hecton_ScooterVolumetricShafts.shader`.
- Added cold `LutArrayResolver` in `Assets/_Project/Scripts/Rendering/` to load `Data/Visuals/Water_Extinction_Matrix.bin` as packed 4096x4096 R16F, bind `_ExtinctionLUT` globally, and fall back to ARGB32 if half sampling is unavailable.
- Published `_ExtinctionLUTRuntime` from underwater visuals and `_ExtinctionLUTWeatherParams` from weather intensity without inventing a new signal type.
- Removed runtime `RenderSettings.fogColor` assignments from underwater/celestial visual color authority while keeping lifecycle restore/read paths.

Cinematic cheats used:
- Packed 2D LUT instead of physical spectral light transport.
- Vertex-only LUT sample on `_MATH_LOD_LOW`.
- Per-pixel extinction only where visual payoff is high.
- IGN fog dither without adding a noise texture dependency.
- Branchless emissive mask exemption via `lerp()`.

Verification:
- LUT byte count verified: 33,554,432 bytes.
- Static shader brace check passed for `Hecton_WaterExtinction.hlsl`, `Hecton8_UberNoir.hlsl`, `Hecton_NoirDepthFog.shader`, and `Hecton_ScooterVolumetricShafts.shader`.
- `_MATH_LOD_LOW` pragma exists in `Assets/_Project/Art/Shaders/Core/Hecton8_UberNoir.shader`; include uses compile-time LOW gates.
- Emissive exemption scan found mask + `lerp()` and no emissive `if` branch.
- Targeted Roslyn compile of `Assets/_Project/Scripts/Rendering/LutArrayResolver.cs` exits 0 after replacing obsolete `FormatUsage` calls with `GraphicsFormatUsage`.
- `git diff --check` returned exit 0; only CRLF normalization warnings.
- `dotnet build Hecton8.Core.csproj --no-restore` failed with 105 existing cross-domain errors. Full dump: `Docs/AgentLogs/Dump_EXTINCTION_LUT_SAMPLER_Build.txt`. No build-log hits for the extinction files or touched shader/C# integration files.

Exact microseconds saved:
- Exact measured profiler savings: unavailable; project compile is blocked, so no Unity profiler run was possible.
- Engineering estimates recorded for review: LOW vertex path saves 40-140 us/frame versus per-pixel material LUT at 1080p; packed LUT fake saves 80-250 us/frame versus an 8-step raymarch; global binding saves 5-40 us/frame in material-heavy scenes; IGN procedural dither saves 10-35 us/frame versus a new texture sample. These are estimates, not measured data.

Final state:
- Core EXTINCTION_LUT_SAMPLER tasks are implemented.
- Final validation is dependency-blocked by the existing compile wall, not by the extinction implementation.
