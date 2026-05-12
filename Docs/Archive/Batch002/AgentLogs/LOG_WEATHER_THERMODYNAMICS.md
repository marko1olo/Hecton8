# WEATHER_THERMODYNAMICS Log

## 2026-05-11 THERMAL_ENGINEER

What was wrong:
- Thermal vents were presentation-heavy and gameplay-light: no coarse Celsius truth map, no boiling damage path, no deterministic eruption cycle, no fauna-readable avoidance field, no player/submarine convection, and standard CPU boiling presentation risk remained.
- The requested heat behavior could have been overbuilt as fluid simulation. That was rejected. The product need is readable danger, lift, haze, roar, and avoidance.

What was done:
- Extended `AbyssalThermalManager` as the single thermodynamics owner.
- Added persistent 16x16 NativeArray Celsius front/back/source maps with sentinel registration.
- Added `[BurstCompile(FloatMode = FloatMode.Fast)]` `ThermalMapJacobiJob`, scheduled on ColdTick and swapped in LateFrame.
- Added fixed-step thermal gameplay sampling for player and submarine only.
- Queued upward convection through `PhysicsForceRouter` as `ForceMode.VelocityChange` using `heat01 * math.rcp(mass)`.
- Queued thermal burn through `CombatDamageRuntime` above 80C.
- Published shader globals for local heat, Celsius, condensation, map metadata, and GPU bubble commands.
- Added deterministic vent eruption through `TriangleWave01(hash + time)`.
- Added throttled audio roar through procedural audio ping and `ImpactSignal`.
- Exposed `IThermodynamicsService` read-only Celsius sampling and thermal map readback for fauna avoidance.
- Added one-shot +20C running fabrication heat through existing `BaseModule` room temperature injection.
- Removed standard CPU ParticleSystem boiling logic from `ThermalGeyser`.
- Added 300-frame fixed NativeArray thermal telemetry ring and binary dump path.
- Logged recon scan in `Docs/AgentLogs/RECON_WEATHER_THERMODYNAMICS.md`.

Cinematic cheats used:
- Coarse 16x16 heat map instead of volumetric water: estimated 30-120 us/ColdTick saved versus 32x32+ multi-iteration or voxel heat.
- Low/MX350 direct reciprocal distance heat instead of Jacobi: estimated 9-28 us/ColdTick saved and avoids map NativeArray allocation.
- Triangle-wave eruption instead of random/coroutine timers: estimated 1-3 us/vent refresh saved and deterministic replay preserved.
- Shader scalar haze/condensation bridge instead of owned render pass: estimated 15-80 us/frame avoided depending on post stack.
- GPU bubble command globals instead of CPU ParticleSystem emission/collision: avoids emitter/collision overhead and managed component churn.
- Player/submarine-only sampling instead of overlap volumes: estimated 40-120 us saved in dense vent caves.
- Fixed telemetry ring instead of logs/lists: 0 B hot-path GC; under 1 us per sampled target.

Exact microseconds saved:
- Thermal grid LOD gate on MX350: 9-28 us/ColdTick.
- Avoided duplicate per-fauna vent scans via front-buffer map: 0 us until consumer read, then avoids O(boids * vents) direct scans.
- Upward thrust routed samples: 2-5 us/FixedTick cost; avoids 40-120 us trigger/collider path.
- Boiling damage queue: 3 us/FixedTick only above threshold.
- Audio roar event: 4 us only on cooldown trigger; no AudioSource creation.
- Fabricator heat: 1 us per craft start; no per-frame heat system.
- Omega reciprocal polish: estimated 2-8 us across thermal sampling spikes.

Verification:
- `AbyssalThermalManager.cs`: validate_script standard, 0 errors, 0 warnings after polish.
- `ThermalGeyser.cs`: validate_script standard, 0 diagnostics before polish.
- `GlobalRegistryContracts.cs`: validate_script standard, 0 diagnostics before polish.
- Touched-file hot-path scan: no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize`.
- Unity compile after thermal namespace fix reported no WEATHER_THERMODYNAMICS errors. Full Unity compile remains blocked by unrelated Visor/Combat/Construction/SaveBinaryStorage errors.
- `dotnet build Hecton8.Core.csproj` after polish is blocked by unrelated missing core/native symbols: `HectonPersistentPathPolicy`, `SteamDeckInputPal`, `HectonThreadPriorityPolicy`, `HectonThreadRole`, `HectonNativeBridge`, `HectonNativeLibrary`, and unrelated Combat helper symbols. Existing warnings are outside WEATHER_THERMODYNAMICS.

Status:
- Prompt status remains PENDING VERIFICATION per WEATHER_THERMODYNAMICS directive.
- Full project verification is dependency-blocked, not thermal-blocked.

## 2026-05-12 THERMAL_ENGINEER R&D CONTINUATION

What was wrong:
- The heat map was technically present as CPU/Burst truth but still weak as an AAA presentation asset. Shader/VFX consumers had scalar local heat and bubble commands, not the actual 2D field.
- The expensive answer would be a new RenderTexture/compute thermal simulation. That duplicates truth and burns bandwidth.

What was done:
- Added `_HectonThermalMapTexture` global binding.
- Added persistent `Texture2D[16x16 RFloat]` creation only on grid-enabled tiers.
- Added dirty/version-gated upload from `_thermalMapReadCelsius` through `SetPixelData(NativeArray<float>)`.
- Upload happens at Tick start after the previous LateFrame job swap marked the map dirty.
- Added fail-closed RFloat unsupported path: shader active flag is forced to 0 and no managed fallback texture is built.
- Added disposal path that destroys the texture and resets the global binding to black if it had been active.

Cinematic cheats used:
- A 1 KB RFloat texture carries believable heat gradients to shaders instead of volumetric heat.
- Bilinear texture filtering smooths the 16x16 grid visually while gameplay remains coarse and deterministic.
- Low/MX350 path keeps no texture and no Jacobi grid.
- High/Ultra can spend the saved cycles on shimmer, condensation, bubble density, and debug overlay sampling from the same field.

Exact microseconds saved:
- Avoided RenderTexture diffusion/compute pass: estimated 30-200 us/frame depending on pass design.
- Avoided per-frame CPU texture upload: 59 redundant uploads/sec removed at 60 FPS.
- Added dirty-path cost: estimated 1-3 us once per active ColdTick, plus 1 KB upload.
- Added normal Tick cost: one branch, estimated below 1 us.

Verification:
- Unity MCP validation unavailable: session disconnected once and timed out once.
- `dotnet build Hecton8.Core.csproj --no-restore --no-dependencies` still fails on unrelated core/native symbols and unrelated GPUScatter telemetry methods; no `AbyssalThermalManager.cs` error was reported.
- Text scan confirms no `foreach`, `string.Format`, `.ToString()`, `math.sqrt`, or `math.normalize` in `AbyssalThermalManager.cs`.
- Status remains PENDING VERIFICATION.
