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

## 2026-05-15 03:19:35 +04:00 - Follow-Up No-Rebuild Rendering/H-Phi Pass
What was wrong:
- `_MATH_LOD_LOW` still paid for normal-map sampling and unused specular/shadow setup.
- Dithered transparency evaluated blue-noise even when the dither feature was disabled.
- Clean materials sampled `_RustDetailMap` before proving rust was active.
- Optional caustic texture sampling was compiled into every non-low UberNoir variant.
- `Hecton8.Graphics.Materials.asmdef` carried an unused `Hecton8.World.Contracts` reference.

What was done:
- Low-tier UberNoir now returns from base+packed ORM surface sampling and skips normal/rust/POM/biolum sampling.
- Low-tier lighting uses `GetMainLight()` without `TransformWorldToShadowCoord`, specular half-vector, caustics, or discarded view math.
- Blue-noise dither is skipped under `_MATH_LOD_LOW` and only sampled when the dither feature flag is enabled.
- Rust detail sampling now returns early when resolved rust is effectively zero.
- Caustic map sampling is now behind `H8_UBERNOIR_CAUSTICS_TEXTURED`.
- Removed the unused World contracts dependency from `Hecton8.Graphics.Materials.asmdef`.

Cinematic Cheats used:
- Low-tier normals degrade to dominant-axis safe normals instead of exact normalization.
- Low-tier lighting keeps ambient + main diffuse only; visual belief is preserved by fog/ORM while expensive depth/specular detail is shed.
- Procedural caustics remain the default; texture caustics are opt-in visual overkill.

Exact Microseconds saved:
- Measured: 0 us. User forbade rebuilds, and Unity/runtime capture remains blocked by World/GPR compile errors.
- Estimated low-tier surface-sample savings: 20-120 us GPU in dense material views.
- Estimated low-tier lighting savings: 10-80 us GPU in forward-lit batches.
- Estimated clean-material rust gate savings: 10-90 us GPU when rust is zero.
- Estimated caustic texture variant/sample savings: 5-40 us GPU plus lower variant pressure when procedural caustics are enough.
- Asmdef cleanup runtime gain: 0 us; static architecture debt reduced.

Verification:
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json`: `RuntimeHPhiNarrow=0.010534799`, `RuntimeHPhiRisk=0.000573240`.
- Scoped HLSL scan: braces `40/40`, one `UnityPerMaterial` CBUFFER, one `_MaskMap` sample, caustic texture sample guarded by `H8_UBERNOIR_CAUSTICS_TEXTURED`.
- Scoped asmdef scan: no `Hecton8.World` / `World.Contracts` reference remains in `Assets/_Project/Scripts/Graphics/Materials`.
- `git diff --check` on touched files: no whitespace errors; LF-to-CRLF warnings only.
- No `dotnet build`, no `dotnet rebuild`, no Unity rebuild.

## 2026-05-15 03:34:52 +04:00 - Follow-Up No-Rebuild Rendering/H-Phi Pass 2
What was wrong:
- Low-tier caustic compute shutdown did not fully guarantee global caustic consumers were dark; `_HectonProjectedCausticsParams.x` could remain nonzero.
- Caustic GPU upload data, caustic black-box telemetry, and AUP culling job payloads relied on implicit layout in code crossing GPU/Burst/native boundaries.
- Disposed NativeArray scratch fields were released but not default-reset, making long-session state inspection less deterministic.

What was done:
- `AnalyticalCausticsService` now passes `lowTier` into `PublishShaderGlobals` and forces caustic intensity to zero for low-tier/depth-disabled modes.
- `CausticsWaveGpuData` and `CausticTelemetryEntry` now declare explicit sequential pack/size layout.
- `ApplyAupShiftJob` now declares explicit sequential layout.
- Disposed caustic black-box and wave-upload scratch NativeArrays are reset to default after release.

Cinematic Cheats used:
- Caustics remain fake-first analytical light contribution, and low-tier now kills the entire global contribution instead of paying for invisible ocean optics.
- Rust, POM, biolum, caustics, and bending stay tier-gated: toaster path keeps material identity; high-end path keeps overkill.

Exact Microseconds saved:
- 15-80 us estimated GPU saved on low-tier caustic receiver views by forcing global intensity to zero. Pending real Profiler/GPU capture.
- 0 us claimed for layout/default-reset changes; these are binary safety and black-box determinism improvements, not runtime speed claims.

Verification:
- No dotnet rebuild was executed.
- `git diff --check` reported no whitespace errors for owned files.
- Static brace scan passed for `Hecton8_UberNoir.hlsl`, `AnalyticalCausticsService.cs`, `InstanceCullingService.cs`, and `Hecton8.Graphics.Materials.asmdef`.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` completed with `RuntimeHPhiNarrow=0.010496041`, `RuntimeHPhiRisk=0.000571225`, `ArchitecturalPurity=0.996460177`, `MemoryAlignment=0.503703704`, `UnityUpdateMethods=2`, `AupPrecisionRisk=0`.

## 2026-05-15 03:48:00 +04:00 - Follow-Up No-Rebuild Shader Safety Pass
What was wrong:
- `H8UberNoirLoadInstance` could index `_H8UberNoirInstanceData[bufferOffset]` when the instance-buffer keyword was compiled but the runtime count was zero or the use flag was disabled.

What was done:
- Added `H8UberNoirBuildDefaultInstance`.
- Changed `H8UberNoirLoadInstance` to use Unity object/world matrices by default and only read the `StructuredBuffer` when `_UberNoirInstanceParams.z >= 0.5` and `_UberNoirInstanceParams.y > 0`.

Cinematic Cheats used:
- None. This was a deterministic safety fix for Resident Drawer fallback behavior.

Exact Microseconds saved:
- 0 us measured. Estimated 0-2 us vertex branch cost in fallback cases; undefined GPU buffer reads removed.

Verification:
- No dotnet rebuild was executed.
- Static HLSL review confirms the buffer read is now count/use gated before indexing.
- First H-Phi static audit attempt timed out at 120 seconds; second no-rebuild static audit completed at 300-second timeout.
- `Tools/Architecture/HectonPhiAudit.ps1 -Summary -Json` completed with `RuntimeHPhiNarrow=0.010497120`, `RuntimeHPhiRisk=0.000573792`, `ArchitecturalPurity=0.996460177`, `MemoryAlignment=0.503966155`, `UnityUpdateMethods=2`, `StructLayoutAttributes=953`, `AupPrecisionRisk=0`.
