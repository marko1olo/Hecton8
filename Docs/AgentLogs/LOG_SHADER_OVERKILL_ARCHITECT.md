# LOG_SHADER_OVERKILL_ARCHITECT

## 2026-05-15 02:40:07 +04:00 - SHADERS CRYSTALLIZED / VISUAL ORGASM READY
What was wrong:
- Material behavior was fragmented across separate caustics/rust/deformation concepts, which risks SetPass multiplication and SRP Batcher damage.
- The active dependency rationale files requested by the prompt were missing: `Docs/AgentLogs/Rationale_CAUSTICS_PROJECTION.md` and `Docs/AgentLogs/Rationale_MATERIAL_DECAY.md`.
- `Docs/Tasks/CURRENT_BATCH.md` does not contain this agent XML or a `<POLISH_MANDATE>` tag.
- Unity batchmode cannot complete because `Assets/_Project/Scripts/World/GroundPenetratingRadarRuntime.cs` references missing World/GPR symbols: `Hecton8.World.GPR`, `GroundRadarTelemetryEntry`, and `GroundRadarConstants`.

What was done:
- Created `Assets/_Project/Art/Shaders/Hecton8_UberNoir.hlsl` as the single UberNoir URP HLSL core.
- Enforced one `CBUFFER_START(UnityPerMaterial)` for per-material data.
- Applied `_TotalUniverseOffset.xyz` before world-position matrix multiplication for AUP precision.
- Added `StructuredBuffer<H8UberNoirInstanceData>` for GraphicsBuffer/Resident Drawer compatible matrices and seed/fade/flags.
- Integrated analytical caustics, dynamic pressure bending, 16-tap rust POM, spectral biolum emission, branchless attenuation, and blue-noise cutout.
- Added `_MATH_LOD_LOW` stripping for albedo/roughness-only low-tier output.
- Added NaN guards for all owned `pow()` and `rsqrt()` use.
- Created `Assets/_Project/Scripts/Graphics/Materials/H8ShaderIDs.cs` for zero-GC property ID caching.
- Ran Unity 6000.4.1f1 batchmode and static audits. Owned shader/C# names do not appear in the compiler error scan; the compile wall is outside this rendering domain.

Cinematic Cheats used:
- Caustics are analytical wave interference plus optional lookup texture, not physical photon simulation.
- Hull bending is shader vertex bowing from stress fields, not CPU mesh deformation.
- Rust depth is high-tier POM only, not geometry displacement or decal stacks.
- Bioluminescence is phase-driven spectral emission, not script-updated material state.
- Noir fog uses blue-noise cutout, not full transparent sorting.

Exact Microseconds saved:
- Measured: 0 us. No clean compile/runtime capture is available because Unity exits on the external World/GPR compile blocker.
- Estimated CPU SetPass/pass savings from unified shader path: 30-120 us.
- Estimated CPU savings from GraphicsBuffer/resident instance path: 20-80 us in dense draws.
- Estimated CPU savings from shader-side hull bending versus CPU mesh mutation: 60-300 us.
- Estimated CPU savings from static property IDs versus hot string lookup bursts: 5-40 us.
- Estimated GPU savings from low-tier stripping: 80-500 us in material-heavy low-end views.
- Estimated GPU texture savings from single packed ORM sample: 10-60 us.

Verification:
- Static audit: one `UnityPerMaterial` CBUFFER, one `_MaskMap` sample, guarded `pow()`/`rsqrt()`, balanced braces.
- `git diff --check`: no whitespace errors; PowerShell reports LF-to-CRLF warnings for updated markdown only.
- Unity batchmode: blocked by `GroundPenetratingRadarRuntime.cs` World/GPR missing references, not owned rendering files.
- Frame Debugger/RenderDoc/Profiler: not run because the project does not reach a clean compile.
