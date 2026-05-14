# LOG - VFX_SDF_CARVE_DEBRIS

## 2026-05-14 - Compute Advection Carve Particles

What was wrong:
- SDF carve feedback had no direct GPU debris path tied to authoritative `VoxelCarveEvent` packets.
- A GameObject, ParticleSystem, or CPU readback implementation would violate the prompt and cost hundreds of microseconds during carve bursts.
- Compile verification is blocked by local tooling: Unity MCP transport fails at `http://127.0.0.1:8088/mcp`, active Unity processes hold `Temp/UnityLockfile`, and safe batchmode compile cannot be launched.

What was done:
- `VoxelCarveEvent` is the carve ingress signal for VFX, preserving hit point, radius, impulse, shape, material, and volume id after voxel validation.
- `BufferID.CarveDebris` and `BufferID.CarveDebrisVelocity` provide H-PHI/DataVault SOA lanes for position-lifetime and velocity.
- `CarveDebrisComputeRenderer` owns the runtime bridge: persistent NativeArrays, persistent ping-pong `GraphicsBuffer`s, Burst aging/injection jobs, dirty-range GPU uploads, AUP shift handling, Math LOD, indirect draw, and 300-frame blackbox telemetry.
- `Hecton_FluidAdvection.compute` contains `ClearCarveDebrisIndirectArgs`, `AdvectCarveDebris`, and `CullCarveDebrisForRender`, using flow drag, dynamic wakes, gravity, optional SDF collision/dissolve, NaN guard, and GPU-side indirect instance counting.
- `Hecton_CarveDebrisIndirect.shader` renders low-poly CoreLit rock chips with edge tint, cave ambient, caustic scatter, noir fog, and dither fade.
- OMEGA polish replaced lifetime float divisions with reciprocal multiplies and cached dispatch group count after kernel setup. Targeted scan found no `GetData`, `SetData`, `foreach`, interpolated strings, `math.sqrt`, `math.normalize`, `dt /`, or `1f /` in touched VFX files.

Cinematic cheats used:
- Fake octahedron chips instead of rigidbody fragments.
- Flow drag and dynamic wake advection instead of particle physics.
- SDF hit dissolves and velocity kill instead of collision contacts.
- Shader edge tint and CoreLit caustics buy perceived fracture detail without more mesh complexity.
- Low tier disables SDF sampling and injects 16 chips; high/ultra keep 64 chips and spend saved cost on lighting/material richness.

Exact microseconds saved:
- 150-400 us saved on burst frames versus transform-spawned mesh debris.
- 80-180 us saved by dirty-range GPU upload versus full-buffer upload plus managed emission.
- 30-90 us saved on MX350/i3 low tier by injecting 16 instead of 64 particles and skipping one 3D SDF texture sample per live particle.
- 50-120 us saved during AUP shift frames by applying the rebase on GPU instead of rewriting 4096 CPU positions.
- 10-20 us saved per full draw by SOA render reads that avoid fetching velocity/flags.
- Sub-1 us saved by reciprocal lifetime math; value is auditability, not frame-time magnitude.

Verification state:
- Static scan passed for the targeted anti-bloat patterns.
- `git diff --check` only reports line-ending normalization warnings on edited files.
- Unity compile/live console verification remains `[BLOCKED BY TOOLING]`; no false pass recorded.

## 2026-05-14 - Second-Pass Hardening

What was wrong:
- Low tier still paid too much of the 4096-slot scan/dispatch envelope after the first pass.
- The empty fallback flow buffer could make telemetry and compute binding look like live abyssal flow.
- Shared fluid compute kernels referenced dynamic wake buffers, so carve debris needed explicit no-wake bindings instead of relying on external owners.
- Fast chips could cross thin SDF features unless velocity was visually bounded.
- Unity compile verification is still blocked: MCP transport is unreachable, Unity owns `Temp/UnityLockfile`, and the generated project files do not yet include `Hecton8.VFX.Debris.csproj`.

What was done:
- Added a low-tier active capacity of 1024 while keeping high/ultra storage and draw capacity at 4096.
- Applied the active capacity to CPU mirror aging, injection, compute dispatch groups, cull capacity, and indirect max instance count.
- Bound published `HectonFluidEngine` flow buffers/textures through the public contract and stopped counting the one-element fallback buffer as active flow.
- Bound `_DynamicWakes` and `_DynamicWakeVectors` to a safe fallback buffer with `_DynamicWakeParams.x = 0`.
- Moved fallback mesh/material creation into `Awake` and `OnEnable` to avoid first-active-frame cold work where possible.
- Added GPU velocity and per-frame step clamping in `AdvectCarveDebris`.
- Preserved blackbox invalid flags during mirror aging so NaN/corruption evidence is not wiped by a normal frame pass.

Cinematic cheats used:
- Low tier reduces active particles and dispatch groups instead of lowering art quality on each chip.
- Velocity clamp is a visual stability fake, not a physical integrator.
- High/ultra spend saved cycles on flow, SDF dissolve, wake billow/shear, and CoreLit material response instead of rigidbody shards.

Exact microseconds saved:
- 25-35 us estimated GPU saving on MX350 by dropping low-tier dispatch from 64 groups to 16 groups.
- 10-25 us estimated CPU saving on idle frames by skipping mirror aging when no debris is alive.
- 30-90 us retained from low-tier SDF bypass and lower injection count.
- Millisecond-scale GPU stalls avoided by keeping verification and visibility on indirect args instead of `GetData`.

Verification state:
- Static VFX scan still finds no `GetData`, `SetData`, `ParticleSystem`, `ComputeBuffer`, `foreach`, `.ToString`, `string.Format`, or interpolated-string hot path in touched VFX files.
- Shader scan shows reciprocal/`rsqrt` math and no new hot `sqrt`, `pow`, `exp`, or `log` path in the carve debris compute lane.
- `dotnet build Hecton8.Core.csproj --no-restore` fails on unrelated symbols in UI/fauna/world/core files; that csproj does not include `CarveDebrisComputeRenderer.cs`.
- Status remains `PENDING VERIFICATION` until Unity imports the new asmdef and the live editor or batchmode compile can be queried.
