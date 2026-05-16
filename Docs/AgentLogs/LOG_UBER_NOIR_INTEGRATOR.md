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
