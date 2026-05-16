# LOG_UBER_NOIR_INTEGRATOR

## 2026-05-16 Phase 1-4 Source Pass

What was wrong:
- UberNoir had a core include but the material-facing shader, screen refraction hook, displaced motion vectors, and runtime feature telemetry were incomplete.
- Shader globals for biolum/AUP were partially scattered before the DataVault bridge pass.
- The first DataVault bridge version still cached a direct native array handle, which violated the new sovereignty demand.
- Unity material consolidation cannot execute while unrelated assemblies fail compilation.

What was done:
- Added/extended `Hecton8/Rendering/UberNoir` with one ForwardLit pass, MotionVectors pass, SRP Batcher CBUFFER, DOTS instancing keyword, and Snell refraction properties.
- Wired Beer-Lambert noir extinction, blue-noise/Bayer dither cutouts, low-tier salt crust, analytical/textured caustics, 16-tap rust POM, hull dents, crush/habitat bends, wake/silt offsets, and normal-bias deformation into the Uber path.
- Added `HectonUberNoirRuntimeBridge` with Pack=1 48-byte telemetry entries, 300-frame DataVault ring, `_HectonActiveShaderFeatureMask`, homeostasis shed gate, and fault dump path.
- Converted `HectonShaderGlobalDataVaultBridge` from direct native array caching to `VaultBufferHandle<float4>`.
- Reduced fragment helper branches in normalize, dither selection, rust corrosion, and blood overlay. POM early-outs remain deliberately because removing them would still execute 16 taps while claiming POM is disabled.
- Reran Unity batch compile. It still fails outside this domain: IK local shadowing, Core Bucketing missing `GlobalRegistry`, Audio Virtualization assembly/reference errors. No log entry references UberNoir source files.

Cinematic cheats used:
- Beer-Lambert curve instead of physical volumetric water simulation.
- Triangle/Bayer/blue-noise dither instead of alpha blending and expensive HLOD fades.
- Salt crust scalar overlay on low tier instead of rust POM texture traversal.
- Snell screen-space opaque-texture offset instead of GrabPass or ray-traced refraction.
- Vertex-only hull dents/crush/wake displacement instead of CPU mesh deformation or collider rebuilds.
- Normal bias toward camera instead of TBN reconstruction.

Microseconds saved:
- Exact measured microseconds: not available because Unity compile blocks player/profile validation.
- Static target: low tier removes 16 rust POM taps plus caustic texture sampling and refraction taps when `_MATH_LOD_LOW` or homeostasis shed is active.
- Static target: material consolidation is expected to save roughly 40-120 us render-thread state overhead for the identified DryZone hard-surface set after compile permits Editor API material migration.

Verification:
- Static scans found no `GrabPass`, `Update()`, `string.Format`, non-Pack=1 struct, or DirectX-only syntax in the touched UberNoir/runtime files.
- HLSL and shader brace counts are balanced.
- Metal thread-group audit: no new compute kernel added; relevant existing compute constants in scanned shader set are 64 or 8x8, below 1024 threads.
- Status remains `PENDING VERIFICATION`, not Master Grade, until unrelated compile blockers are cleared and Vulkan/DX12 builds run.
- Omega status is not claimed. `_TotalUniverseOffset` is used in vertex/stable noise paths; remaining shader `if` branches are work-shed/culling paths retained for low-tier and homeostasis correctness.

## 2026-05-16 Loop 7 - AUP Transform Correction

What was wrong:
- The active Uber shader path inherited a helper that subtracted `_TotalUniverseOffset` from object-to-world translation before clip-space projection.
- `HectonFloatingOrigin` already keeps scene transforms in runtime space by applying `runtime = absolute - TotalOffset`; subtracting `_TotalUniverseOffset` again risks double-shifting visible geometry and displaced motion vectors.
- `Hecton_CoreLit.hlsl` treats `_TotalUniverseOffset` as runtime-to-absolute phase input for stable procedural noise, not as a pre-projection geometry offset.

What was done:
- Renamed `H8UberNoirObjectToAupWorld` to `H8UberNoirObjectToRuntimeWorld`.
- Kept finite translation sanitation but removed the geometry subtraction of `_TotalUniverseOffset`.
- Updated ForwardLit and MotionVectors paths to use runtime-space object transforms.
- Kept `_TotalUniverseOffset` on procedural phase math for buckling, salt crust, caustics, and salt-crystal highlights.
- Changed `H8UberNoirSafeRcp` to preserve denominator sign while still clamping by epsilon.

Cinematic cheats used:
- Runtime-space geometry for stable STP and clip projection.
- AUP-space phase fakes for visual continuity across floating-origin shifts.
- Sign-safe POM reciprocal so high-tier rust depth does not invert under negative UV scale/view edge cases.

Exact microseconds saved:
- None claimed. This pass is correctness/stability work, not a measured optimization. Compile/profiler validation is still blocked by unrelated project errors.

Validation:
- Static scans after the AUP correction found no stale `H8UberNoirObjectToAupWorld` references.
- HLSL brace count remains balanced.
- Forbidden-pattern scan over UberNoir-owned shader/runtime files found no `GrabPass`, legacy `sampler2D`, `tex2D`, DirectX-only marker, compute `numthreads`, `NativeArray<`, `Update()`, or `string.Format`.
- Unity batch validation was rerun. The editor reached AssetDatabase script compilation and emitted no UberNoir errors in the partial log, but no `UberNoirMaterialConsolidationReport.md` was produced and the process stalled with no log growth; it was terminated rather than left running.

## 2026-05-16 Loop 9 - Texture Gate Honesty

What was wrong:
- The branchless textured-caustic `lerp` still sampled `_HectonCausticsMap` when high-cost caustics were disabled.
- The branchless screen-refraction `lerp` still sampled `_CameraOpaqueTexture` one to three times when refraction params or homeostasis disabled the effect.
- That was a false economy: visually disabled, but still paying bandwidth.

What was done:
- Added a `[branch]` guard around the textured caustic sample.
- Added a `[branch]` early return around the screen-refraction sample block.
- Kept POM early-outs for the same reason: disabled high-cost effects must actually skip texture work.

Cinematic cheats used:
- Low/Middle stay on procedural caustics and no refraction taps.
- High/Ultra keep Snell refraction and textured caustics only when the runtime gate allows the spend.

Exact microseconds saved:
- None measured. Static effect: under homeostasis shed, affected fragments skip one caustic map sample and one to three opaque-texture samples. Profiler proof is still blocked.

## 2026-05-16 Loop 10 - Dither Texture Gate Honesty

What was wrong:
- `H8UberNoirClipDitheredTransparency` sampled `_BlueNoiseTex` through a branchless `lerp`, so disabled/stress-shed dither could still pay a texture fetch.
- `HectonUberNoirRuntimeBridge` reported `FeatureBlueNoiseDither` even on low-tier/stress frames where the shader should not spend texture bandwidth on blue noise.

What was done:
- Added `H8UberNoirCheapDither` as an ALU interleaved-gradient fallback.
- Gated `_BlueNoiseTex` behind dither-active and high-cost runtime state.
- Updated runtime feature-mask generation so blue-noise telemetry is emitted only when non-low high-cost work is allowed.

Cinematic cheats used:
- Low/stress HLOD coverage uses deterministic ALU noise instead of blue-noise texture bandwidth.
- High/Ultra keep the blue-noise transition quality only when the runtime gate permits it.

Exact microseconds saved:
- None measured. Static effect: disabled/low/stress dither paths skip one `_BlueNoiseTex` sample per clipped fragment. Profiler proof is still blocked.

Verification:
- HLSL brace count remains balanced (`63/63`).
- Forbidden-pattern scan over UberNoir-owned shader/runtime files found no `GrabPass`, legacy `sampler2D`, `tex2D`, DirectX-only marker, compute `numthreads`, `NativeArray<`, `Update()`, or `string.Format`.
- No Unity process was left running. Unity validation was not rerun because the previous batch session stalled during AssetDatabase script compilation.
- `dotnet build Assembly-CSharp.csproj --no-restore` failed before domain validation in `RealtimeCSG.csproj` due 216 missing source files; `Docs/AgentLogs/Dotnet_UBER_NOIR_INTEGRATOR.log` has no UberNoir matches.

## 2026-05-16 Loop 11 - Refraction Tap Audit

What was wrong:
- Base refraction was gated, but chromatic split still sampled `_CameraOpaqueTexture` twice whenever refraction was active, even with `_UberNoirRefractionParams.w = 0`.
- The documented "1-3 opaque texture taps" budget was therefore false in the one-tap configuration.

What was done:
- Added a `[branch]` guard around the chromatic red/blue scene-color samples.
- Base Snell refraction now pays one opaque-texture tap; chromatic High/Ultra overkill pays the two extra taps only when enabled.

Cinematic cheats used:
- One-tap screen-space Snell distortion for normal high-tier glass.
- Optional chromatic split for Ultra glass overkill without paying it on every refractive fragment.

Exact microseconds saved:
- None measured. Static effect: chromatic-off refractive fragments skip two `_CameraOpaqueTexture` samples. Profiler proof is still blocked.

Verification:
- HLSL brace count remains balanced (`64/64`).
- Forbidden-pattern scan over UberNoir-owned shader/runtime files remains clean.
